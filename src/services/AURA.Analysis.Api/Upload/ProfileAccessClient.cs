using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace AURA.Analysis.Api.Upload;

/// <summary>
/// Adapter HTTP cho IPatientAccessResolver, gọi Profile (M6): POST /internal/v1/profile/access/resolve.
/// HttpClient phải có BaseAddress của Profile và header service token (cấu hình ở DI, không nằm trong lớp này).
/// RequesterId = accountId (Identity), không phải Firebase uid.
/// </summary>
public sealed class ProfileAccessClient : IPatientAccessResolver
{
    private readonly HttpClient _http;
    public ProfileAccessClient(HttpClient http) => _http = http;

    public async Task<AccessDecision> ResolveAsync(PatientAccessRequest request, CancellationToken ct)
    {
        var body = new ResolveBody(request.RequesterId, request.PatientId, request.ClinicId, request.Action);
        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsJsonAsync("/internal/v1/profile/access/resolve", body, ct);
        }
        catch (HttpRequestException) { throw UploadFailure.Dependency("Profile"); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { throw UploadFailure.Dependency("Profile"); }

        using (response)
        {
            if (!response.IsSuccessStatusCode) throw UploadFailure.Dependency("Profile");
            var dto = await response.Content.ReadFromJsonAsync<ResolveReply>(cancellationToken: ct);
            if (dto is null) throw UploadFailure.Dependency("Profile");

            // Bị từ chối hoặc thiếu dữ liệu tenant: coi như không có quyền (404 giống nhau ở UploadService).
            if (!dto.Allowed || dto.TenantId is null || dto.OwnerPrincipalId is null || dto.BillingOwner is null)
                return new AccessDecision(false, Guid.Empty, "", Guid.Empty, "", dto.PermissionVersion);

            return new AccessDecision(true, dto.TenantId.Value, dto.TenantKind ?? "",
                dto.OwnerPrincipalId.Value, $"{dto.BillingOwner.Type}:{dto.BillingOwner.Id}", dto.PermissionVersion);
        }
    }

    private sealed record ResolveBody(
        [property: JsonPropertyName("actorAccountId")] Guid ActorAccountId,
        [property: JsonPropertyName("patientId")] Guid PatientId,
        [property: JsonPropertyName("clinicContextId")] Guid? ClinicContextId,
        [property: JsonPropertyName("action")] string Action);

    private sealed record Owner(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("id")] Guid Id);

    private sealed record ResolveReply(
        [property: JsonPropertyName("allowed")] bool Allowed,
        [property: JsonPropertyName("tenantId")] Guid? TenantId,
        [property: JsonPropertyName("tenantKind")] string? TenantKind,
        [property: JsonPropertyName("ownerPrincipalId")] Guid? OwnerPrincipalId,
        [property: JsonPropertyName("billingOwner")] Owner? BillingOwner,
        [property: JsonPropertyName("permissionVersion")] long PermissionVersion);
}
