using System.Text.Json.Serialization;

namespace AURA.Profile.Api.Models;

public sealed record ErrorDetails(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("correlationId")] string CorrelationId,
    [property: JsonPropertyName("retryable")] bool Retryable
);

public sealed record ErrorEnvelope(
    [property: JsonPropertyName("error")] ErrorDetails Error
);

public sealed record UserProfileResponse(
    [property: JsonPropertyName("accountId")] Guid AccountId,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("phone")] string? Phone,
    [property: JsonPropertyName("patientId")] Guid? PatientId,
    [property: JsonPropertyName("personalTenantId")] Guid? PersonalTenantId,
    [property: JsonPropertyName("profileVersion")] int ProfileVersion
);

// CHÚ Ý AN TOÀN: Request cập nhật hồ sơ TUYỆT ĐỐI KHÔNG chứa role, accountId, tenantId, patientId.
// Chống sửa role qua Profile theo chuẩn bảo mật Sprint 2.
public sealed record UpdateProfileRequest(
    [property: JsonPropertyName("displayName")] string? DisplayName,
    [property: JsonPropertyName("phone")] string? Phone
);

public sealed record ProvisionProfileRequest(
    [property: JsonPropertyName("accountId")] Guid AccountId,
    [property: JsonPropertyName("firebaseUid")] string FirebaseUid,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("email")] string? Email
);

public sealed record AccessResolveRequest(
    [property: JsonPropertyName("actorAccountId")] Guid ActorAccountId,
    [property: JsonPropertyName("patientId")] Guid PatientId,
    [property: JsonPropertyName("clinicContextId")] Guid? ClinicContextId,
    [property: JsonPropertyName("action")] string Action
);

public sealed record BillingOwnerDto(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("id")] Guid Id
);

public sealed record AccessResolveResponse(
    [property: JsonPropertyName("allowed")] bool Allowed,
    [property: JsonPropertyName("accountStatus")] string? AccountStatus,
    [property: JsonPropertyName("patientId")] Guid? PatientId,
    [property: JsonPropertyName("tenantId")] Guid? TenantId,
    [property: JsonPropertyName("tenantKind")] string? TenantKind,
    [property: JsonPropertyName("ownerPrincipalId")] Guid? OwnerPrincipalId,
    [property: JsonPropertyName("billingOwner")] BillingOwnerDto? BillingOwner,
    [property: JsonPropertyName("permissionVersion")] int PermissionVersion
);
