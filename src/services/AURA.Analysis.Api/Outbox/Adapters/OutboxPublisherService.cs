using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AURA.Analysis.Api.Outbox.Adapters;

/// <summary>Vong lap nen. Khi Enabled=false thi khong lam gi. Loi bat ngo (DB mat ket noi...) khong lam sap host: ghi log, doi roi thu lai.</summary>
public sealed class OutboxPublisherService : BackgroundService
{
    private readonly OutboxPublisher _publisher;
    private readonly OutboxOptions _opt;
    private readonly ILogger<OutboxPublisherService> _log;

    public OutboxPublisherService(OutboxPublisher publisher, IOptions<OutboxOptions> opt, ILogger<OutboxPublisherService> log)
    { _publisher = publisher; _opt = opt.Value; _log = log; }

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        if (!_opt.Enabled) { _log.LogInformation("Outbox publisher TAT (Outbox:Enabled=false)"); return; }
        _log.LogInformation("Outbox publisher bat: poll {Poll}s, batch {Batch}, toi da {Max} lan thu", _opt.PollSeconds, _opt.BatchSize, _opt.MaxPublishAttempts);
        while (!stop.IsCancellationRequested)
        {
            var full = false;
            try
            {
                var r = await _publisher.RunOnceAsync(stop);
                full = r.Claimed >= _opt.BatchSize && r.Released == 0; // con viec: chay tiep ngay
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { break; }
            catch (Exception ex) { _log.LogError(ex, "Outbox publisher loi vong lap; thu lai sau {Poll}s", _opt.PollSeconds); }
            if (!full)
                try { await Task.Delay(TimeSpan.FromSeconds(_opt.PollSeconds), stop); } catch (OperationCanceledException) { break; }
        }
    }
}
