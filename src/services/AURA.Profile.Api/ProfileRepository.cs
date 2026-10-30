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
        // Kiểm tra xem hồ sơ đã khởi tạo đầy đủ chưa
        var existing = await GetProfileByAccountIdAsync(accountId, cancellationToken);
        if (existing != null && existing.PersonalTenantId.HasValue && existing.PatientId.HasValue)
        {
            return existing;
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            // 1. Tìm hoặc tạo Personal Tenant (Đảm bảo Idempotent, không tạo thừa khi chạy đồng thời)
            Guid tenantId;
            const string findTenantSql = @"
                SELECT tenant_id FROM profile.tenants
                WHERE owner_principal_id = $1 AND tenant_kind = 'PERSONAL'
                LIMIT 1;";

            await using (var findTenantCmd = new NpgsqlCommand(findTenantSql, connection, transaction))
            {
                findTenantCmd.Parameters.AddWithValue(accountId);
                var obj = await findTenantCmd.ExecuteScalarAsync(cancellationToken);
                if (obj is Guid existingTenantId)
                {
                    tenantId = existingTenantId;
                }
                else
                {
                    tenantId = Guid.NewGuid();
                    const string insertTenantSql = @"
                        INSERT INTO profile.tenants (tenant_id, tenant_kind, owner_principal_id)
                        VALUES ($1, 'PERSONAL', $2)
                        ON CONFLICT DO NOTHING;";

                    await using var tenantCmd = new NpgsqlCommand(insertTenantSql, connection, transaction);
                    tenantCmd.Parameters.AddWithValue(tenantId);
                    tenantCmd.Parameters.AddWithValue(accountId);
                    await tenantCmd.ExecuteNonQueryAsync(cancellationToken);

                    // Re-query để phòng trường hợp một transaction khác vừa tạo đồng thời
                    await using var reFindCmd = new NpgsqlCommand(findTenantSql, connection, transaction);
                    reFindCmd.Parameters.AddWithValue(accountId);
                    var reObj = await reFindCmd.ExecuteScalarAsync(cancellationToken);
                    if (reObj is Guid reId)
                    {
                        tenantId = reId;
                    }
                }
            }

            // 2. Khởi tạo hoặc cập nhật hồ sơ cá nhân
            const string insertProfileSql = @"
                INSERT INTO profile.profiles (account_id, display_name, personal_tenant_id, profile_version)
                VALUES ($1, $2, $3, 1)
                ON CONFLICT (account_id) DO UPDATE
                    SET display_name = EXCLUDED.display_name,
                        personal_tenant_id = COALESCE(profile.profiles.personal_tenant_id, EXCLUDED.personal_tenant_id);";

            await using (var profileCmd = new NpgsqlCommand(insertProfileSql, connection, transaction))
            {
                profileCmd.Parameters.AddWithValue(accountId);
                profileCmd.Parameters.AddWithValue(displayName);
                profileCmd.Parameters.AddWithValue(tenantId);
                await profileCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // 3. Tìm hoặc tạo Hồ sơ Bệnh nhân cá nhân mặc định
            const string findPatientSql = @"
                SELECT patient_id FROM profile.patients
                WHERE linked_account_id = $1 AND tenant_id = $2
                LIMIT 1;";

            await using (var findPatientCmd = new NpgsqlCommand(findPatientSql, connection, transaction))
            {
                findPatientCmd.Parameters.AddWithValue(accountId);
                findPatientCmd.Parameters.AddWithValue(tenantId);
                var pObj = await findPatientCmd.ExecuteScalarAsync(cancellationToken);
                if (pObj is not Guid)
                {
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
                }
            }

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
        // 1. Kiểm tra action hợp lệ theo quy chuẩn Sprint 2 Contract v1.0 Section 5.3:
        // ANALYSIS_CREATE, ANALYSIS_READ, ANALYSIS_FILE_READ, DOCTOR_REVIEW
        var allowedActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ANALYSIS_CREATE",
            "ANALYSIS_READ",
            "ANALYSIS_FILE_READ",
            "DOCTOR_REVIEW"
        };

        if (string.IsNullOrWhiteSpace(action) || !allowedActions.Contains(action))
        {
            return new AccessResolveResponse(false, "ACTIVE", patientId, null, null, null, null, 1);
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        // 2. Tìm thông tin bệnh nhân
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
                // Bệnh nhân không tồn tại -> fail-closed (allowed: false)
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

        // 3. Ngữ cảnh cá nhân (clinicContextId is null)
        if (!clinicContextId.HasValue)
        {
            // Bệnh nhân cá nhân không có quyền thực hiện đánh giá chuyên môn bác sĩ
            if (action.Equals("DOCTOR_REVIEW", StringComparison.OrdinalIgnoreCase))
            {
                return new AccessResolveResponse(false, "ACTIVE", patientId, null, null, null, null, 1);
            }

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

        // 4. Ngữ cảnh phòng khám (Clinic context)
        var clinicId = clinicContextId.Value;

        // 4.1. Kiểm tra Clinic có tồn tại và ACTIVE
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

        // 4.2. Kiểm tra bệnh nhân PHẢI thuộc phạm vi của Clinic này
        var patientBelongsToClinic = (pTenantId == clinicTenantId || pOwnerPrincipalId == clinicId);
        if (!patientBelongsToClinic)
        {
            return new AccessResolveResponse(false, "ACTIVE", patientId, null, null, null, null, 1);
        }

        // 4.3. Kiểm tra tư cách thành viên (Membership) của actor tại phòng khám
        const string memberSql = "SELECT role FROM profile.memberships WHERE clinic_id = $1 AND account_id = $2 AND status = 'ACTIVE';";
        string? memberRole = null;
        await using (var mCmd = new NpgsqlCommand(memberSql, connection))
        {
            mCmd.Parameters.AddWithValue(clinicId);
            mCmd.Parameters.AddWithValue(actorAccountId);
            var roleObj = await mCmd.ExecuteScalarAsync(cancellationToken);
            memberRole = roleObj as string;
        }

        if (string.IsNullOrEmpty(memberRole))
        {
            return new AccessResolveResponse(false, "ACTIVE", patientId, null, null, null, null, 1);
        }

        // Quản trị viên phòng khám: Toàn quyền quản trị hành chính phòng khám
        if (memberRole == "CLINIC_ADMIN")
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

        // Bác sĩ: Phải có phân công hoạt động (DoctorAssignment)
        if (memberRole == "DOCTOR")
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

    // ====================================================================
    // CLINIC MEMBERSHIP MANAGEMENT (Task W3-M6-01)
    // ====================================================================

    public async Task<(bool Allowed, string? FailureReason)> CheckClinicManagementAuthorityAsync(
        Guid actorAccountId, Guid clinicId, IReadOnlyList<string> actorRoles, CancellationToken cancellationToken = default)
    {
        if (actorRoles.Contains("SYSTEM_ADMIN", StringComparer.OrdinalIgnoreCase))
        {
            return (true, null);
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        const string clinicSql = "SELECT tenant_id FROM profile.clinics WHERE clinic_id = $1 AND status = 'ACTIVE' LIMIT 1;";
        await using (var clinicCmd = new NpgsqlCommand(clinicSql, connection))
        {
            clinicCmd.Parameters.AddWithValue(clinicId);
            var tenantObj = await clinicCmd.ExecuteScalarAsync(cancellationToken);
            if (tenantObj is not Guid)
            {
                return (false, "CLINIC_NOT_FOUND");
            }
        }

        const string memberSql = @"
            SELECT role FROM profile.memberships
            WHERE clinic_id = $1 AND account_id = $2 AND status = 'ACTIVE'
            LIMIT 1;";

        await using (var memberCmd = new NpgsqlCommand(memberSql, connection))
        {
            memberCmd.Parameters.AddWithValue(clinicId);
            memberCmd.Parameters.AddWithValue(actorAccountId);
            var roleObj = await memberCmd.ExecuteScalarAsync(cancellationToken);
            if (roleObj is not string role)
            {
                return (false, "NOT_A_MEMBER");
            }

            if (string.Equals(role, ClinicMemberRoles.ClinicAdmin, StringComparison.OrdinalIgnoreCase))
            {
                return (true, null);
            }

            return (false, "FORBIDDEN");
        }
    }

    public async Task<IReadOnlyList<ClinicMembershipDto>> GetClinicMembersAsync(
        Guid clinicId, string? statusFilter = null, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        var queryFilter = string.IsNullOrWhiteSpace(statusFilter) || statusFilter.Equals("ALL", StringComparison.OrdinalIgnoreCase)
            ? null
            : statusFilter.Trim().ToUpperInvariant();

        const string sql = @"
            SELECT m.membership_id, m.clinic_id, m.account_id, COALESCE(p.display_name, 'Th�nh vi�n ph�ng kh�m'),
                   m.role, m.status, m.created_at, m.updated_at
            FROM profile.memberships m
            LEFT JOIN profile.profiles p ON p.account_id = m.account_id
            WHERE m.clinic_id = $1
              AND ($2::text IS NULL OR m.status = $2)
            ORDER BY m.created_at ASC;";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue(clinicId);
        cmd.Parameters.AddWithValue((object?)queryFilter ?? DBNull.Value);

        var items = new List<ClinicMembershipDto>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var membershipId = reader.GetGuid(0);
            var cId = reader.GetGuid(1);
            var accId = reader.GetGuid(2);
            var displayName = reader.GetString(3);
            var role = reader.GetString(4);
            var status = reader.GetString(5);
            var createdAt = reader.GetFieldValue<DateTimeOffset>(6);
            var updatedAt = reader.GetFieldValue<DateTimeOffset>(7);

            items.Add(new ClinicMembershipDto(
                membershipId, cId, accId, displayName, role, status, createdAt, updatedAt));
        }

        return items;
    }

    public async Task<(bool Success, ClinicMembershipDto? Membership, string? ErrorCode, string? ErrorMessage)> AddOrReactivateClinicMemberAsync(
        Guid clinicId, Guid targetAccountId, string role, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        // 1. Ki?m tra t�i kho?n ngu?i d�ng c� t?n t?i trong profile.profiles
        const string checkProfileSql = "SELECT display_name FROM profile.profiles WHERE account_id = $1 LIMIT 1;";
        string displayName;
        await using (var profileCmd = new NpgsqlCommand(checkProfileSql, connection))
        {
            profileCmd.Parameters.AddWithValue(targetAccountId);
            var nameObj = await profileCmd.ExecuteScalarAsync(cancellationToken);
            if (nameObj is not string name)
            {
                return (false, null, "ERR_ACCOUNT_NOT_FOUND", "Kh�ng t�m th?y h? so ngu?i d�ng tuong ?ng.");
            }
            displayName = name;
        }

        // 2. Ki?m tra b?n ghi membership hi?n t?i
        const string checkMembershipSql = @"
            SELECT membership_id, role, status
            FROM profile.memberships
            WHERE clinic_id = $1 AND account_id = $2
            LIMIT 1;";

        Guid? existingMembershipId = null;
        string? existingStatus = null;

        await using (var checkCmd = new NpgsqlCommand(checkMembershipSql, connection))
        {
            checkCmd.Parameters.AddWithValue(clinicId);
            checkCmd.Parameters.AddWithValue(targetAccountId);
            await using var reader = await checkCmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                existingMembershipId = reader.GetGuid(0);
                existingStatus = reader.GetString(2);
            }
        }

        if (existingMembershipId.HasValue && string.Equals(existingStatus, MembershipStatuses.Active, StringComparison.OrdinalIgnoreCase))
        {
            return (false, null, "ERR_MEMBERSHIP_ALREADY_EXISTS", "Th�nh vi�n d� t?n t?i v� dang ho?t d?ng trong ph�ng kh�m.");
        }

        if (existingMembershipId.HasValue)
        {
            // T�i k�ch ho?t membership d� b? thu h?i tru?c d�
            const string reactivateSql = @"
                UPDATE profile.memberships
                SET role = $1, status = 'ACTIVE', updated_at = NOW()
                WHERE clinic_id = $2 AND account_id = $3
                RETURNING membership_id, created_at, updated_at;";

            await using var reactivateCmd = new NpgsqlCommand(reactivateSql, connection);
            reactivateCmd.Parameters.AddWithValue(role);
            reactivateCmd.Parameters.AddWithValue(clinicId);
            reactivateCmd.Parameters.AddWithValue(targetAccountId);

            await using var reader = await reactivateCmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                var membershipId = reader.GetGuid(0);
                var createdAt = reader.GetFieldValue<DateTimeOffset>(1);
                var updatedAt = reader.GetFieldValue<DateTimeOffset>(2);

                return (true, new ClinicMembershipDto(
                    membershipId, clinicId, targetAccountId, displayName, role, MembershipStatuses.Active, createdAt, updatedAt), null, null);
            }
        }
        else
        {
            // T?o m?i membership
            var newMembershipId = Guid.NewGuid();
            const string insertSql = @"
                INSERT INTO profile.memberships (membership_id, clinic_id, account_id, role, status, created_at, updated_at)
                VALUES ($1, $2, $3, $4, 'ACTIVE', NOW(), NOW())
                RETURNING created_at, updated_at;";

            await using var insertCmd = new NpgsqlCommand(insertSql, connection);
            insertCmd.Parameters.AddWithValue(newMembershipId);
            insertCmd.Parameters.AddWithValue(clinicId);
            insertCmd.Parameters.AddWithValue(targetAccountId);
            insertCmd.Parameters.AddWithValue(role);

            await using var reader = await insertCmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                var createdAt = reader.GetFieldValue<DateTimeOffset>(0);
                var updatedAt = reader.GetFieldValue<DateTimeOffset>(1);

                return (true, new ClinicMembershipDto(
                    newMembershipId, clinicId, targetAccountId, displayName, role, MembershipStatuses.Active, createdAt, updatedAt), null, null);
            }
        }

        return (false, null, "ERR_INTERNAL_DB", "Kh�ng th? ghi nh?n th�nh vi�n ph�ng kh�m.");
    }

    public async Task<(bool Success, string? ErrorCode, string? ErrorMessage)> RevokeClinicMemberAsync(
        Guid clinicId, Guid targetAccountId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        // 1. Ki?m tra th�nh vi�n c� t?n t?i v� dang ACTIVE
        const string checkSql = @"
            SELECT role FROM profile.memberships
            WHERE clinic_id = $1 AND account_id = $2 AND status = 'ACTIVE'
            LIMIT 1;";

        string role;
        await using (var checkCmd = new NpgsqlCommand(checkSql, connection))
        {
            checkCmd.Parameters.AddWithValue(clinicId);
            checkCmd.Parameters.AddWithValue(targetAccountId);
            var roleObj = await checkCmd.ExecuteScalarAsync(cancellationToken);
            if (roleObj is not string r)
            {
                return (false, "ERR_MEMBERSHIP_NOT_FOUND", "Kh�ng t�m th?y th�nh vi�n ho?t d?ng trong ph�ng kh�m.");
            }
            role = r;
        }

        // 2. Ch?n t? g? b? n?u l� CLINIC_ADMIN duy nh?t c�n l?i
        if (string.Equals(role, ClinicMemberRoles.ClinicAdmin, StringComparison.OrdinalIgnoreCase))
        {
            const string countAdminSql = @"
                SELECT COUNT(*) FROM profile.memberships
                WHERE clinic_id = $1 AND role = 'CLINIC_ADMIN' AND status = 'ACTIVE';";

            await using var countCmd = new NpgsqlCommand(countAdminSql, connection);
            countCmd.Parameters.AddWithValue(clinicId);
            var count = Convert.ToInt64(await countCmd.ExecuteScalarAsync(cancellationToken));
            if (count <= 1)
            {
                return (false, "ERR_CANNOT_REVOKE_SOLE_ADMIN", "Kh�ng th? g? b? qu?n tr? vi�n duy nh?t c?a ph�ng kh�m.");
            }
        }

        // 3. Th?c hi?n chuy?n tr?ng th�i luu v?t audit: status = 'REVOKED'
        const string revokeSql = @"
            UPDATE profile.memberships
            SET status = 'REVOKED', updated_at = NOW()
            WHERE clinic_id = $1 AND account_id = $2 AND status = 'ACTIVE';";

        await using (var revokeCmd = new NpgsqlCommand(revokeSql, connection))
        {
            revokeCmd.Parameters.AddWithValue(clinicId);
            revokeCmd.Parameters.AddWithValue(targetAccountId);
            var rowsAffected = await revokeCmd.ExecuteNonQueryAsync(cancellationToken);
            if (rowsAffected > 0)
            {
                return (true, null, null);
            }
        }

        return (false, "ERR_MEMBERSHIP_NOT_FOUND", "Kh�ng t�m th?y th�nh vi�n ho?t d?ng trong ph�ng kh�m.");
    }
}