using System.Security.Cryptography;
using System.Text;

namespace AURA.Analysis.Api.Upload;

public static class RequestFingerprint
{
    /// <summary>Phiên bản cách chuẩn hóa; lưu ở analysis_requests.request_schema_version.</summary>
    public const string Version = "1";

    /// <summary>
    /// SHA-256 (hex thường) của metadata đã chuẩn hóa và checksum từng ảnh theo thứ tự request.
    /// Không gồm token, correlationId, timestamp, tên file.
    /// </summary>
    public static string Compute(Guid patientId, string modality, string eyeSide, IEnumerable<string> imageSha256InOrder)
    {
        var sb = new StringBuilder();
        sb.Append('v').Append(Version).Append('\n');
        sb.Append(patientId.ToString("D")).Append('\n');
        sb.Append(modality).Append('\n');
        sb.Append(eyeSide).Append('\n');
        foreach (var sha in imageSha256InOrder) sb.Append(sha).Append('\n');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()))).ToLowerInvariant();
    }
}

public static class DeterministicIds
{
    /// <summary>
    /// UUID ổn định theo đầu vào, để retry cùng Idempotency-Key tạo lại đúng ID cũ
    /// (reservation Billing, object key, job không bị nhân đôi). ID không phải bí mật; quyền truy cập vẫn kiểm tra riêng.
    /// </summary>
    public static Guid Create(string purpose, params string[] parts)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(purpose + "\u001f" + string.Join("\u001f", parts)));
        var g = bytes.AsSpan(0, 16).ToArray();
        g[6] = (byte)((g[6] & 0x0F) | 0x50);
        g[8] = (byte)((g[8] & 0x3F) | 0x80);
        return new Guid(g, bigEndian: true);
    }
}
