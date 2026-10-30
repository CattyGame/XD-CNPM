namespace AURA.Common.Cache;

/// <summary>
/// Chuẩn hóa tiền tố phân vùng namespace cache theo hợp đồng kiến trúc Sprint 4–5 (Task SCRUM-339 / W4-M1-02).
/// Ngăn ngừa xung đột key giữa các microservices và hỗ trợ xóa cache có chủ đích theo pattern.
/// </summary>
public static class AuraCacheNamespaces
{
    /// <summary>
    /// API Gateway rate-limiting counters (ví dụ: gw:ratelimit:ip_127_0_0_1)
    /// </summary>
    public const string GatewayRateLimit = "gw:ratelimit:";

    /// <summary>
    /// API Gateway user session metadata (ví dụ: gw:session:usr_xxxx)
    /// </summary>
    public const string GatewaySession = "gw:session:";

    /// <summary>
    /// Billing packages catalog & price cache (ví dụ: billing:package:standard_pack)
    /// </summary>
    public const string BillingPackage = "billing:package:";

    /// <summary>
    /// Billing idempotency locks & webhook callback keys (ví dụ: billing:idempotency:pay_tx_xxxx)
    /// </summary>
    public const string BillingIdempotency = "billing:idempotency:";

    /// <summary>
    /// Analysis batch progress & image processing state (ví dụ: analysis:batch:batch_xxxx)
    /// </summary>
    public const string AnalysisBatch = "analysis:batch:";

    public static string BuildKey(string @namespace, string entityId)
    {
        if (string.IsNullOrWhiteSpace(@namespace)) throw new ArgumentException("Namespace cannot be empty.", nameof(@namespace));
        if (string.IsNullOrWhiteSpace(entityId)) throw new ArgumentException("EntityId cannot be empty.", nameof(entityId));

        return $"{@namespace}{entityId.Trim()}";
    }
}
