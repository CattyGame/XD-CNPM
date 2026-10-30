using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Aura.Worker.Analysis.Data.Entities;

[Table("jobs")]
public class JobRecord
{
    [Key]
    [Column("job_id")]
    public Guid JobId { get; set; }

    [Column("analysis_id")]
    public Guid AnalysisId { get; set; }

    [Column("batch_id")]
    public Guid BatchId { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("image_file_id")]
    public Guid ImageFileId { get; set; }

    [Column("attempt_no")]
    public int AttemptNo { get; set; }

    [Column("status")]
    public string Status { get; set; } = "QUEUED";

    [Column("lease_owner")]
    public string? LeaseOwner { get; set; }

    [Column("lease_expires_at")]
    public DateTime? LeaseExpiresAt { get; set; }

    [Column("intent_recorded")]
    public bool IntentRecorded { get; set; } = false;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

[Table("analyses")]
public class AnalysisRecord
{
    [Key]
    [Column("analysis_id")]
    public Guid AnalysisId { get; set; }

    [Column("batch_id")]
    public Guid BatchId { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("patient_id")]
    public Guid PatientId { get; set; }

    [Column("eye_side")]
    public string EyeSide { get; set; } = "unknown";

    [Column("processing_status")]
    public string ProcessingStatus { get; set; } = "QUEUED";

    [Column("review_status")]
    public string ReviewStatus { get; set; } = "NOT_REQUIRED";

    [Column("original_blob_key")]
    public string OriginalBlobKey { get; set; } = string.Empty;

    [Column("result_json")]
    public string? ResultJson { get; set; }

    [Column("error_json")]
    public string? ErrorJson { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

[Table("outbox_messages")]
public class OutboxRecord
{
    [Key]
    [Column("outbox_id")]
    public Guid OutboxId { get; set; } = Guid.NewGuid();

    [Column("event_id")]
    public Guid EventId { get; set; }

    [Column("event_type")]
    public string EventType { get; set; } = string.Empty;

    [Column("payload_json")]
    public string PayloadJson { get; set; } = string.Empty;

    [Column("available_at")]
    public DateTime AvailableAt { get; set; } = DateTime.UtcNow;

    [Column("published_at")]
    public DateTime? PublishedAt { get; set; }

    [Column("attempt_count")]
    public int AttemptCount { get; set; } = 0;

    [Column("status")]
    public string Status { get; set; } = "PENDING"; // PENDING, PUBLISHED, NEEDS_ATTENTION

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
