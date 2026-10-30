namespace Aura.Worker.Analysis.Domain;

public static class AnalysisStatus
{
    public const string Pending = "PENDING";
    public const string Queued = "QUEUED";
    public const string Processing = "PROCESSING";
    public const string Completed = "COMPLETED";
    public const string Failed = "FAILED";
    public const string RetryReady = "RETRY_READY";
    public const string LowQuality = "LOW_QUALITY";
    public const string Rejected = "REJECTED";
    public const string Duplicate = "DUPLICATE";
}

public static class ReviewStatus
{
    public const string NotRequired = "NOT_REQUIRED";
    public const string Pending = "PENDING";
    public const string InReview = "IN_REVIEW";
    public const string Reviewed = "REVIEWED";
    public const string CorrectionRequired = "CORRECTION_REQUIRED";
}
