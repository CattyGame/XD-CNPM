using AURA.Profile.Api.Models;
using Npgsql;

namespace AURA.Profile.Api;

public class ProfileRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public ProfileRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<UserProfileResponse?> GetProfileByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT p.account_id, p.display_name, p.phone, p.personal_tenant_id, p.profile_version,
                   pt.patient_id
            FROM profile.profiles p
            LEFT JOIN profile.patients pt ON pt.linked_account_id = p.account_id
            WHERE p.account_id = $1
            LIMIT 1;";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue(accountId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            var accId = reader.GetGuid(0);
            var displayName = reader.GetString(1);
            var phone = reader.IsDBNull(2) ? null : reader.GetString(2);
            var personalTenantId = reader.IsDBNull(3) ? (Guid?)null : reader.GetGuid(3);
            var profileVersion = reader.GetInt32(4);
            var patientId = reader.IsDBNull(5) ? (Guid?)null : reader.GetGuid(5);

            return new UserProfileResponse(accId, displayName, phone, patientId, personalTenantId, profileVersion);
        }

        return null;
    }

    public async Task<UserProfileResponse> ProvisionProfileAsync(Guid accountId, string displayName, CancellationToken cancellationToken = default)
    {
        var existing = await GetProfileByAccountIdAsync(accountId, cancellationToken);
        if (existing != null)
        {
            return existing;
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var tenantId = Guid.NewGuid();
            const string insertTenantSql = @"
                INSERT INTO profile.tenants (tenant_id, tenant_kind, owner_principal_id)
                VALUES ($1, 'PERSONAL', $2)
                ON CONFLICT (tenant_id) DO NOTHING;";

            await using var tenantCmd = new NpgsqlCommand(insertTenantSql, connection, transaction);
            tenantCmd.Parameters.AddWithValue(tenantId);
            tenantCmd.Parameters.AddWithValue(accountId);
            await tenantCmd.ExecuteNonQueryAsync(cancellationToken);

            const string insertProfileSql = @"
                INSERT INTO profile.profiles (account_id, display_name, personal_tenant_id, profile_version)
                VALUES ($1, $2, $3, 1)
                ON CONFLICT (account_id) DO UPDATE SET display_name = EXCLUDED.display_name;";

            await using var profileCmd = new NpgsqlCommand(insertProfileSql, connection, transaction);
            profileCmd.Parameters.AddWithValue(accountId);
            profileCmd.Parameters.AddWithValue(displayName);
            profileCmd.Parameters.AddWithValue(tenantId);
            await profileCmd.ExecuteNonQueryAsync(cancellationToken);

            var patientId = Guid.NewGuid();
            const string insertPatientSql = @"
                INSERT INTO profile.patients (patient_id, tenant_id, linked_account_id, owner_principal_id, status)
                VALUES ($1, $2, $3, $4, 'ACTIVE')
                ON CONFLICT DO NOTHING;";

            await using var patientCmd = new NpgsqlCommand(insertPatientSql, connection, transaction);
            patientCmd.Parameters.AddWithValue(patientId);
            patientCmd.Parameters.AddWithValue(tenantId);
            patientCmd.Parameters.AddWithValue(accountId);
            patientCmd.Parameters.AddWithValue(accountId);
            await patientCmd.ExecuteNonQueryAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return (await GetProfileByAccountIdAsync(accountId, cancellationToken))!;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<(bool Success, UserProfileResponse? UpdatedProfile, bool Conflict)> UpdateProfileAsync(
        Guid accountId, string displayName, string? phone, int expectedVersion, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        const string updateSql = @"
            UPDATE profile.profiles
            SET display_name = $1,
                phone = $2,
                profile_version = profile_version + 1,
                updated_at = NOW()
            WHERE account_id = $3 AND profile_version = $4
            RETURNING profile_version;";

        await using var cmd = new NpgsqlCommand(updateSql, connection);
        cmd.Parameters.AddWithValue(displayName);
        cmd.Parameters.AddWithValue((object?)phone ?? DBNull.Value);
        cmd.Parameters.AddWithValue(accountId);
        cmd.Parameters.AddWithValue(expectedVersion);

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        if (result is int)
        {
            var updated = await GetProfileByAccountIdAsync(accountId, cancellationToken);
            return (true, updated, false);
        }

        // Kiểm tra xem hồ sơ có tồn tại hay do sai phiên bản (optimistic lock conflict)
        const string checkSql = "SELECT profile_version FROM profile.profiles WHERE account_id = $1;";
        await using var checkCmd = new NpgsqlCommand(checkSql, connection);
        checkCmd.Parameters.AddWithValue(accountId);
        var currentVersionObj = await checkCmd.ExecuteScalarAsync(cancellationToken);

        if (currentVersionObj is int)
        {
            // Hồ sơ có tồn tại nhưng phiên bản không khớp
            return (false, null, true);
        }

        // Hồ sơ không tồn tại
        return (false, null, false);
    }

    public async Task<AccessResolveResponse> ResolveAccessAsync(
        Guid actorAccountId, Guid patientId, Guid? clinicContextId, string action, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        // 1. Tìm thông tin bệnh nhân
        const string patientSql = @"
            SELECT patient_id, tenant_id, linked_account_id, owner_principal_id, status
            FROM profile.patients
            WHERE patient_id = $1;";

        Guid pTenantId;
        Guid? pLinkedAccountId;
        Guid pOwnerPrincipalId;
        string pStatus;

        await using (var pCmd = new NpgsqlCommand(patientSql, connection))
        {
            pCmd.Parameters.AddWithValue(patientId);
            await using var reader = await pCmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return new AccessResolveResponse(false, null, null, null, null, null, null, 1);
            }

            pTenantId = reader.GetGuid(1);
            pLinkedAccountId = reader.IsDBNull(2) ? (Guid?)null : reader.GetGuid(2);
            pOwnerPrincipalId = reader.GetGuid(3);
            pStatus = reader.GetString(4);
        }

        if (pStatus != "ACTIVE")
        {
            return new AccessResolveResponse(false, "DISABLED", patientId, null, null, null, null, 1);
        }

        // 2. Ngữ cảnh cá nhân (clinicContextId is null)
        if (!clinicContextId.HasValue)
        {
            var isOwn = (pLinkedAccountId.HasValue && pLinkedAccountId.Value == actorAccountId)
                        || pOwnerPrincipalId == actorAccountId;

            if (isOwn)
            {
                return new AccessResolveResponse(
                    Allowed: true,
                    AccountStatus: "ACTIVE",
                    PatientId: patientId,
                    TenantId: pTenantId,
                    TenantKind: "PERSONAL",
                    OwnerPrincipalId: actorAccountId,
                    BillingOwner: new BillingOwnerDto("ACCOUNT", actorAccountId),
                    PermissionVersion: 1
                );
            }

            return new AccessResolveResponse(false, "ACTIVE", patientId, null, null, null, null, 1);
        }

        // 3. Ngữ cảnh phòng khám (Clinic context)
        var clinicId = clinicContextId.Value;

        // 3.1. Kiểm tra Clinic có tồn tại và ACTIVE
        const string clinicSql = "SELECT tenant_id FROM profile.clinics WHERE clinic_id = $1 AND status = 'ACTIVE';";
        Guid clinicTenantId;
        await using (var cCmd = new NpgsqlCommand(clinicSql, connection))
        {
            cCmd.Parameters.AddWithValue(clinicId);
            var ctObj = await cCmd.ExecuteScalarAsync(cancellationToken);
            if (ctObj is not Guid cTId)
            {
                return new AccessResolveResponse(false, "ACTIVE", patientId, null, null, null, null, 1);
            }
            clinicTenantId = cTId;
        }

        // 3.2. Kiểm tra tư cách thành viên (Membership) của actor
        const string memberSql = "SELECT role FROM profile.memberships WHERE clinic_id = $1 AND account_id = $2 AND status = 'ACTIVE';";
        string? role = null;
        await using (var mCmd = new NpgsqlCommand(memberSql, connection))
        {
            mCmd.Parameters.AddWithValue(clinicId);
            mCmd.Parameters.AddWithValue(actorAccountId);
            var roleObj = await mCmd.ExecuteScalarAsync(cancellationToken);
            role = roleObj as string;
        }

        if (string.IsNullOrEmpty(role))
        {
            return new AccessResolveResponse(false, "ACTIVE", patientId, null, null, null, null, 1);
        }

        // Quản trị viên phòng khám: Có quyền trên bệnh nhân thuộc tenant của phòng khám
        if (role == "CLINIC_ADMIN")
        {
            if (pTenantId == clinicTenantId || pOwnerPrincipalId == clinicId)
            {
                return new AccessResolveResponse(
                    Allowed: true,
                    AccountStatus: "ACTIVE",
                    PatientId: patientId,
                    TenantId: clinicTenantId,
                    TenantKind: "CLINIC",
                    OwnerPrincipalId: clinicId,
                    BillingOwner: new BillingOwnerDto("CLINIC", clinicId),
                    PermissionVersion: 1
                );
            }
        }

        // Bác sĩ: Phải có phân công hoạt động (DoctorAssignment)
        if (role == "DOCTOR")
        {
            const string assignSql = @"
                SELECT assignment_id FROM profile.doctor_assignments
                WHERE clinic_id = $1 AND doctor_account_id = $2 AND patient_id = $3 AND status = 'ACTIVE';";

            await using var aCmd = new NpgsqlCommand(assignSql, connection);
            aCmd.Parameters.AddWithValue(clinicId);
            aCmd.Parameters.AddWithValue(actorAccountId);
            aCmd.Parameters.AddWithValue(patientId);

            var assignObj = await aCmd.ExecuteScalarAsync(cancellationToken);
            if (assignObj is Guid)
            {
                return new AccessResolveResponse(
                    Allowed: true,
                    AccountStatus: "ACTIVE",
                    PatientId: patientId,
                    TenantId: clinicTenantId,
                    TenantKind: "CLINIC",
                    OwnerPrincipalId: clinicId,
                    BillingOwner: new BillingOwnerDto("CLINIC", clinicId),
                    PermissionVersion: 1
                );
            }
        }

        return new AccessResolveResponse(false, "ACTIVE", patientId, null, null, null, null, 1);
    }
}
