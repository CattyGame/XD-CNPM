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

// --- Models cho Clinic Memberships (Sprint 3 - Task W3-M6-01) ---

public static class ClinicMemberRoles
{
    public const string ClinicAdmin = "CLINIC_ADMIN";
    public const string Doctor = "DOCTOR";
    public const string Staff = "STAFF";

    public static readonly HashSet<string> AllowedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        ClinicAdmin, Doctor, Staff
    };
}

public static class MembershipStatuses
{
    public const string Active = "ACTIVE";
    public const string Revoked = "REVOKED";
}

public sealed record AddClinicMemberRequest(
    [property: JsonPropertyName("accountId")] Guid AccountId,
    [property: JsonPropertyName("role")] string Role
);

public sealed record ClinicMembershipDto(
    [property: JsonPropertyName("membershipId")] Guid MembershipId,
    [property: JsonPropertyName("clinicId")] Guid ClinicId,
    [property: JsonPropertyName("accountId")] Guid AccountId,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updatedAt")] DateTimeOffset UpdatedAt
);

public sealed record ClinicMemberListResponse(
    [property: JsonPropertyName("clinicId")] Guid ClinicId,
    [property: JsonPropertyName("items")] IReadOnlyList<ClinicMembershipDto> Items
);
