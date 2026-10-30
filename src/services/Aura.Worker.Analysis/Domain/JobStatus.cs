namespace Aura.Worker.Analysis.Domain;

public static class JobStatus
{
    public const string Queued = "QUEUED";
    public const string Running = "RUNNING";
    public const string Succeeded = "SUCCEEDED";
    public const string Failed = "FAILED";
    public const string RetryReady = "RETRY_READY";
    public const string DeadLetter = "DEAD_LETTER";
}

public static class ErrorCodes
{
    public const string ErrAiFailed = "ERR_AI_FAILED";
    public const string ErrTimeout = "ERR_TIMEOUT";
    public const string ErrNetwork = "ERR_NETWORK";
    public const string ErrImageLowQuality = "ERR_IMAGE_LOW_QUALITY";
    public const string ErrImageMissing = "ERR_IMAGE_MISSING";
    public const string ErrUnauthorized = "ERR_UNAUTHORIZED";
    public const string ErrValidation = "ERR_VALIDATION";
    public const string ErrAiOutputInvalid = "ERR_AI_OUTPUT_INVALID";
}
