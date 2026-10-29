namespace AURA.Analysis.Api.Outbox;

public interface IOutboxStore
{
    /// <summary>Claim bang lease (FOR UPDATE SKIP LOCKED) va tang delivery_attempts. Chi tra record chua published,
    /// chua NEEDS_ATTENTION, den han, lease het, occurred_at &gt;= MinOccurredAt, tenant khong bi loai.</summary>
    Task<IReadOnlyList<OutboxRecord>> ClaimAsync(OutboxClaim claim, CancellationToken ct);
    /// <summary>Chi goi sau khi broker CONFIRM. Tra false neu record khong con o trang thai chua published.</summary>
    Task<bool> MarkPublishedAsync(Guid id, DateTimeOffset at, CancellationToken ct);
    Task MarkRetryAsync(Guid id, string errorCode, DateTimeOffset at, DateTimeOffset nextPublishAt, CancellationToken ct);
    Task MarkNeedsAttentionAsync(Guid id, string errorCode, DateTimeOffset at, CancellationToken ct);
    /// <summary>Tra lai claim cho record chua tung thu publish (vd broker mat ket noi giua batch): giam attempts, xoa lease.</summary>
    Task ReleaseUnattemptedAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
    /// <summary>Record da het so lan thu (attempts &gt;= max) va lease het han -&gt; NEEDS_ATTENTION. Tra so record.</summary>
    Task<int> SweepExhaustedAsync(DateTimeOffset now, int maxAttempts, CancellationToken ct);
    Task<OutboxBacklog> GetBacklogAsync(DateTimeOffset now, CancellationToken ct);
}

public interface IBrokerPublisher
{
    /// <summary>Publish persistent + mandatory, cho confirm toi da confirmTimeout. Khong nem loi ra ngoai cho loi broker
    /// thong thuong: tra PublishOutcome tuong ung.</summary>
    Task<PublishOutcome> PublishAsync(BrokerMessage message, TimeSpan confirmTimeout, CancellationToken ct);
}
