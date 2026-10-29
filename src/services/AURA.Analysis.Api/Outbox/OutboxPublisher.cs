using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AURA.Analysis.Api.Outbox;

/// <summary>Mot vong publish: claim -&gt; publish (confirm) -&gt; cap nhat DB.
/// Bat bien: published_at chi duoc ghi SAU khi broker confirm va khong bi return. Crash giua publish va cap nhat DB
/// => lease het han => phat lai CUNG eventId/messageId (at-least-once; consumer phai idempotent). Khong exactly-once.</summary>
public sealed class OutboxPublisher
{
    private readonly IOutboxStore _store;
    private readonly IBrokerPublisher _broker;
    private readonly OutboxOptions _opt;
    private readonly TimeProvider _clock;
    private readonly ILogger<OutboxPublisher> _log;

    public OutboxPublisher(IOutboxStore store, IBrokerPublisher broker, IOptions<OutboxOptions> options,
        TimeProvider clock, ILogger<OutboxPublisher> log)
    {
        _store = store; _broker = broker; _opt = options.Value; _clock = clock; _log = log;
        _opt.Validate();
    }

    public async Task<OutboxRunResult> RunOnceAsync(CancellationToken ct)
    {
        var now = _clock.GetUtcNow();
        var minAt = _opt.MinOccurredAtUtc ?? DateTimeOffset.MaxValue; // khong cau hinh moc => khong claim gi
        int swept = await _store.SweepExhaustedAsync(now, _opt.MaxPublishAttempts, ct);
        if (swept > 0) _log.LogWarning("Outbox: {Count} record het {Max} lan thu, chuyen NEEDS_ATTENTION (khong xoa)", swept, _opt.MaxPublishAttempts);

        var claim = new OutboxClaim(now, _opt.BatchSize, TimeSpan.FromSeconds(_opt.LeaseSeconds),
            _opt.MaxPublishAttempts, minAt, _opt.ExcludedTenantIds);
        var batch = await _store.ClaimAsync(claim, ct);
        int published = 0, retried = 0, attention = swept, released = 0;

        for (int i = 0; i < batch.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var r = batch[i];
            var map = OutboxMessageMapper.Map(r, _opt);
            if (!map.Ok)
            {
                await SafeAsync(() => _store.MarkNeedsAttentionAsync(r.Id, map.ErrorCode!, _clock.GetUtcNow(), ct), r);
                attention++;
                _log.LogError("Outbox {EventId} khong hop le ({Code}); NEEDS_ATTENTION", r.Id, map.ErrorCode);
                continue;
            }

            PublishOutcome outcome;
            try { outcome = await _broker.PublishAsync(map.Message!, TimeSpan.FromSeconds(_opt.ConfirmTimeoutSeconds), ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Outbox {EventId}: loi broker khong phan loai", r.Id);
                outcome = PublishOutcome.BrokerUnavailable;
            }

            if (outcome == PublishOutcome.Confirmed)
            {
                // Neu buoc nay loi (DB), record van giu lease; het lease se phat lai cung ID. Khong nuot loi am tham.
                if (await SafePublishedAsync(() => _store.MarkPublishedAsync(r.Id, _clock.GetUtcNow(), ct), r)) published++;
                _log.LogInformation("Outbox published eventId={EventId} type={Type} correlationId={Correlation} attempt={Attempt}",
                    r.Id, r.EventType, map.Message!.CorrelationId, r.DeliveryAttempts);
                continue;
            }

            var code = outcome switch
            {
                PublishOutcome.Nacked => OutboxErrors.BrokerNack,
                PublishOutcome.Returned => OutboxErrors.BrokerReturned,
                PublishOutcome.ConfirmTimeout => OutboxErrors.ConfirmTimeout,
                _ => OutboxErrors.BrokerUnavailable
            };
            if (r.DeliveryAttempts >= _opt.MaxPublishAttempts)
            {
                await SafeAsync(() => _store.MarkNeedsAttentionAsync(r.Id, OutboxErrors.AttemptsExhausted + ":" + code, _clock.GetUtcNow(), ct), r);
                attention++;
                _log.LogError("Outbox {EventId} het {Max} lan thu ({Code}); NEEDS_ATTENTION, bao M1/M2", r.Id, _opt.MaxPublishAttempts, code);
            }
            else
            {
                var delay = Backoff(r.DeliveryAttempts);
                await SafeAsync(() => _store.MarkRetryAsync(r.Id, code, _clock.GetUtcNow(), _clock.GetUtcNow() + delay, ct), r);
                retried++;
                _log.LogWarning("Outbox {EventId} publish that bai ({Code}) lan {Attempt}; thu lai sau {Delay}s",
                    r.Id, code, r.DeliveryAttempts, (int)delay.TotalSeconds);
            }

            // Broker mat ket noi: dung batch, tra lai phan chua thu de khong ton luot thu.
            if (outcome == PublishOutcome.BrokerUnavailable && i + 1 < batch.Count)
            {
                var rest = batch.Skip(i + 1).Select(x => x.Id).ToArray();
                await _store.ReleaseUnattemptedAsync(rest, ct);
                released = rest.Length;
                break;
            }
        }
        return new OutboxRunResult(batch.Count, published, retried, attention, released);
    }

    public TimeSpan Backoff(int attempts)
    {
        var i = Math.Clamp(attempts - 1, 0, _opt.BackoffSeconds.Length - 1);
        return TimeSpan.FromSeconds(_opt.BackoffSeconds[i]);
    }

    /// <summary>Loi DB khi cap nhat trang thai khong duoc lam sap ca vong; record se duoc claim lai sau khi lease het.</summary>
    private async Task<bool> SafeAsync(Func<Task> action, OutboxRecord r)
    {
        try { await action(); return true; }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _log.LogError(ex, "Outbox {EventId}: cap nhat trang thai that bai; se phat lai cung ID sau khi lease het", r.Id);
            return false;
        }
    }

    private async Task<bool> SafePublishedAsync(Func<Task<bool>> action, OutboxRecord r)
    {
        try { return await action(); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _log.LogError(ex, "Outbox {EventId}: cap nhat published that bai; se phat lai cung ID sau khi lease het", r.Id);
            return false;
        }
    }
}
