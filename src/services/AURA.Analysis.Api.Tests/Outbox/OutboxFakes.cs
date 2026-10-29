using AURA.Analysis.Api.Outbox;

namespace AURA.Analysis.Api.Tests;

public sealed class FakeClock : TimeProvider
{
    private DateTimeOffset _now;
    public FakeClock(DateTimeOffset start) { _now = start; }
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan t) => _now += t;
}

public sealed class OutboxRow
{
    public Guid Id; public Guid TenantId; public string EventType = ""; public string SchemaVersion = "";
    public string Payload = ""; public DateTimeOffset OccurredAt;
    public DateTimeOffset? PublishedAt, NextPublishAt, LockedUntil, NeedsAttentionAt;
    public int Attempts; public string? LastError;
}

/// <summary>Mo phong dung ngu nghia claim/lease cua SQL trong OutboxSql (SQL that duoc test rieng bang psql).</summary>
public sealed class InMemoryOutboxStore : IOutboxStore
{
    public readonly List<OutboxRow> Rows = new();
    public Func<Guid, bool>? FailMarkPublished; // gia lap crash/loi DB sau khi publish
    public int MarkPublishedCalls;

    public Task<IReadOnlyList<OutboxRecord>> ClaimAsync(OutboxClaim c, CancellationToken ct)
    {
        var due = Rows.Where(r => r.PublishedAt is null && r.NeedsAttentionAt is null && r.Attempts < c.MaxAttempts
                && (r.NextPublishAt is null || r.NextPublishAt <= c.Now)
                && (r.LockedUntil is null || r.LockedUntil <= c.Now)
                && r.OccurredAt >= c.MinOccurredAt && !c.ExcludedTenants.Contains(r.TenantId))
            .OrderBy(r => r.OccurredAt).ThenBy(r => r.Id).Take(c.BatchSize).ToList();
        foreach (var r in due) { r.LockedUntil = c.Now + c.Lease; r.Attempts++; }
        return Task.FromResult<IReadOnlyList<OutboxRecord>>(due.Select(r =>
            new OutboxRecord(r.Id, r.TenantId, r.EventType, r.SchemaVersion, r.Payload, r.OccurredAt, r.Attempts)).ToList());
    }
    public Task<bool> MarkPublishedAsync(Guid id, DateTimeOffset at, CancellationToken ct)
    {
        MarkPublishedCalls++;
        if (FailMarkPublished?.Invoke(id) == true) throw new InvalidOperationException("db down");
        var r = Rows.Single(x => x.Id == id);
        if (r.PublishedAt is not null) return Task.FromResult(false);
        r.PublishedAt = at; r.LockedUntil = null; r.NextPublishAt = null; r.LastError = null;
        return Task.FromResult(true);
    }
    public Task MarkRetryAsync(Guid id, string code, DateTimeOffset at, DateTimeOffset next, CancellationToken ct)
    { var r = Rows.Single(x => x.Id == id); if (r.PublishedAt is null) { r.LastError = code; r.NextPublishAt = next; r.LockedUntil = null; } return Task.CompletedTask; }
    public Task MarkNeedsAttentionAsync(Guid id, string code, DateTimeOffset at, CancellationToken ct)
    { var r = Rows.Single(x => x.Id == id); if (r.PublishedAt is null) { r.NeedsAttentionAt = at; r.LastError = code; r.LockedUntil = null; } return Task.CompletedTask; }
    public Task ReleaseUnattemptedAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    { foreach (var r in Rows.Where(x => ids.Contains(x.Id) && x.PublishedAt is null)) { r.Attempts = Math.Max(0, r.Attempts - 1); r.LockedUntil = null; } return Task.CompletedTask; }
    public Task<int> SweepExhaustedAsync(DateTimeOffset now, int max, CancellationToken ct)
    {
        var x = Rows.Where(r => r.PublishedAt is null && r.NeedsAttentionAt is null && r.Attempts >= max && (r.LockedUntil is null || r.LockedUntil <= now)).ToList();
        foreach (var r in x) { r.NeedsAttentionAt = now; r.LastError = OutboxErrors.AttemptsExhausted; }
        return Task.FromResult(x.Count);
    }
    public Task<OutboxBacklog> GetBacklogAsync(DateTimeOffset now, CancellationToken ct)
    {
        var p = Rows.Where(r => r.PublishedAt is null && r.NeedsAttentionAt is null).ToList();
        return Task.FromResult(new OutboxBacklog(p.Count,
            p.Count(r => (r.NextPublishAt is null || r.NextPublishAt <= now) && (r.LockedUntil is null || r.LockedUntil <= now)),
            p.Count(r => r.LockedUntil > now), Rows.Count(r => r.PublishedAt is null && r.NeedsAttentionAt is not null),
            p.Count == 0 ? null : p.Min(r => r.OccurredAt)));
    }
}

/// <summary>Broker gia lap: kich ban ket qua theo thu tu; "DeliveredBeforeOutcome" gia lap confirm bi mat sau khi broker da nhan.</summary>
public sealed class FakeBroker : IBrokerPublisher
{
    public readonly Queue<PublishOutcome> Script = new();
    public PublishOutcome Default = PublishOutcome.Confirmed;
    public readonly List<BrokerMessage> Delivered = new();   // message ma consumer that su nhan
    public readonly List<BrokerMessage> Attempts = new();
    public bool DeliverOnTimeout = true;
    public Func<BrokerMessage, Exception?>? Throw;

    public Task<PublishOutcome> PublishAsync(BrokerMessage m, TimeSpan t, CancellationToken ct)
    {
        Attempts.Add(m);
        var ex = Throw?.Invoke(m); if (ex is not null) throw ex;
        var o = Script.Count > 0 ? Script.Dequeue() : Default;
        if (o == PublishOutcome.Confirmed || (o == PublishOutcome.ConfirmTimeout && DeliverOnTimeout)) Delivered.Add(m);
        return Task.FromResult(o);
    }
}

/// <summary>Consumer idempotent mau: dedupe theo messageId, dem tac dung phu that su.</summary>
public sealed class IdempotentConsumer
{
    private readonly HashSet<string> _seen = new();
    public int SideEffects;
    public void Handle(BrokerMessage m) { if (_seen.Add(m.MessageId)) SideEffects++; }
}
