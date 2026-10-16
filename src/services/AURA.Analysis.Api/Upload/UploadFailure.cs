namespace AURA.Analysis.Api.Upload;

public sealed record ErrorDetail(string Field, string Issue);

/// <summary>Lỗi nghiệp vụ có mã HTTP và mã ERR_* theo contract. Endpoint chuyển thành envelope {error:{code,message,correlationId,retryable}}.</summary>
public sealed class UploadFailure : Exception
{
    public UploadFailure(int status, string code, string message, bool retryable = false,
        IReadOnlyList<ErrorDetail>? details = null) : base(message)
    {
        Status = status;
        Code = code;
        Retryable = retryable;
        Details = details ?? Array.Empty<ErrorDetail>();
    }

    public int Status { get; }
    public string Code { get; }
    public bool Retryable { get; }
    public IReadOnlyList<ErrorDetail> Details { get; }

    public static UploadFailure Validation(params ErrorDetail[] details) =>
        new(400, "ERR_VALIDATION", "Dữ liệu không hợp lệ.", false, details);

    public static UploadFailure Dependency(string what) =>
        new(503, "ERR_NETWORK", $"Không kết nối được {what}. Gửi lại cùng Idempotency-Key.", true);
}
