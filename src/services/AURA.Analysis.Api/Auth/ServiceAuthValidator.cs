using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AURA.Analysis.Api.Upload;
using Microsoft.AspNetCore.Http;

namespace AURA.Analysis.Api.Auth;

public static class ServiceAuthValidator
{
    public static void RequireServiceScope(HttpRequest request, string requiredScope, string expectedAudience = "aura-analysis")
    {
        var authHeader = request.Headers.Authorization;
        if (authHeader.Count != 1 || !AuthenticationHeaderValue.TryParse(authHeader[0], out var bearer)
            || !string.Equals(bearer.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(bearer.Parameter))
        {
            throw new UploadFailure(401, "ERR_UNAUTHORIZED", "Cần service bearer token.");
        }

        var token = bearer.Parameter;
        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            throw new UploadFailure(401, "ERR_UNAUTHORIZED", "Service token không hợp lệ.");
        }

        JsonDocument payloadDoc;
        try
        {
            var payloadBytes = Base64UrlDecode(parts[1]);
            payloadDoc = JsonDocument.Parse(payloadBytes);
        }
        catch
        {
            throw new UploadFailure(401, "ERR_UNAUTHORIZED", "Service token payload không hợp lệ.");
        }

        using (payloadDoc)
        {
            var root = payloadDoc.RootElement;

            // 1. Check expiry
            if (root.TryGetProperty("exp", out var expProp) && expProp.TryGetInt64(out var expSeconds))
            {
                var expiry = DateTimeOffset.FromUnixTimeSeconds(expSeconds);
                if (expiry < DateTimeOffset.UtcNow)
                {
                    throw new UploadFailure(401, "ERR_UNAUTHORIZED", "Service token đã hết hạn.");
                }
            }

            // 2. Check audience
            bool audValid = false;
            if (root.TryGetProperty("aud", out var audProp))
            {
                if (audProp.ValueKind == JsonValueKind.String)
                {
                    audValid = string.Equals(audProp.GetString(), expectedAudience, StringComparison.OrdinalIgnoreCase);
                }
                else if (audProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var audItem in audProp.EnumerateArray())
                    {
                        if (string.Equals(audItem.GetString(), expectedAudience, StringComparison.OrdinalIgnoreCase))
                        {
                            audValid = true;
                            break;
                        }
                    }
                }
            }

            if (!audValid)
            {
                throw new UploadFailure(403, "ERR_FORBIDDEN", $"Service token sai audience, yêu cầu {expectedAudience}.");
            }

            // 3. Check scope
            bool scopeValid = false;
            if (root.TryGetProperty("scope", out var scopeProp) || root.TryGetProperty("scp", out scopeProp))
            {
                var scopeStr = scopeProp.GetString();
                if (!string.IsNullOrWhiteSpace(scopeStr))
                {
                    var scopes = scopeStr.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    scopeValid = scopes.Contains(requiredScope, StringComparer.Ordinal);
                }
            }

            if (!scopeValid)
            {
                throw new UploadFailure(403, "ERR_FORBIDDEN", $"Service token thiếu scope {requiredScope}.");
            }
        }
    }

    private static byte[] Base64UrlDecode(string input)
    {
        string base64 = input.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return Convert.FromBase64String(base64);
    }
}
