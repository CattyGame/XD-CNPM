using System.Text.Json.Serialization;

namespace Aura.Worker.Analysis.Messaging;

public class JobRunMessage
{
    [JsonPropertyName("messageId")]
    public Guid MessageId { get; set; }

    [JsonPropertyName("messageType")]
    public string MessageType { get; set; } = "analysis.job.run";

    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; set; } = "0.6";

    [JsonPropertyName("occurredAt")]
    public string OccurredAt { get; set; } = string.Empty;

    [JsonPropertyName("tenantId")]
    public Guid TenantId { get; set; }

    [JsonPropertyName("correlationId")]
    public string CorrelationId { get; set; } = string.Empty;

    [JsonPropertyName("jobId")]
    public Guid JobId { get; set; }

    [JsonPropertyName("analysisId")]
    public Guid AnalysisId { get; set; }

    [JsonPropertyName("batchId")]
    public Guid BatchId { get; set; }

    [JsonPropertyName("imageFileId")]
    public Guid ImageFileId { get; set; }

    [JsonPropertyName("attempt")]
    public int Attempt { get; set; }
}
