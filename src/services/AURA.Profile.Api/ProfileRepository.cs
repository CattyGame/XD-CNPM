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
        if (existing != null && existing.PersonalTenantId.HasValue && existing.PatientId.HasValue)
        {
            return existing;
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
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

                    await using var reFindCmd = new NpgsqlCommand(findTenantSql, connection, transaction);
                    reFindCmd.Parameters.AddWithValue(accountId);
                    var reObj = await reFindCmd.ExecuteScalarAsync(cancellationToken);
                    if (reObj is Guid reId)
                    {
                        tenantId = reId;
                    }
                }
            }

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

        const string checkSql = "SELECT profile_version FROM profile.profiles WHERE account_id = $1;";
        await using var checkCmd = new NpgsqlCommand(checkSql, connection);
        checkCmd.Parameters.AddWithValue(accountId);
        var currentVersionObj = await checkCmd.ExecuteScalarAsync(cancellationToken);

        if (currentVersionObj is int)
        {
            return (false, null, true);
        }

        return (false, null, false);
    }

    public async Task<AccessResolveResponse> ResolveAccessAsync(
        Guid actorAccountId, Guid patientId, Guid? clinicContextId, string action, CancellationToken cancellationToken = default)
    {
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

        // 1. Kiểm tra trạng thái tài khoản Actor trong identity.accounts (Fail-Closed nếu DISABLED)
        const string accountCheckSql = "SELECT account_status FROM identity.accounts WHERE account_id = $1 LIMIT 1;";
        await using (var accCmd = new NpgsqlCommand(accountCheckSql, connection))
        {
            accCmd.Parameters.AddWithValue(actorAccountId);
            var accStatusObj = await accCmd.ExecuteScalarAsync(cancellationToken);
            if (accStatusObj is string accStatus && !string.Equals(accStatus, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            {
                return new AccessResolveResponse(false, accStatus, patientId, null, null, null, null, 1);
            }
        }

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

        var patientBelongsToClinic = (pTenantId == clinicTenantId || pOwnerPrincipalId == clinicId);
        if (!patientBelongsToClinic)
        {
            return new AccessResolveResponse(false, "ACTIVE", patientId, null, null, null, null, 1);
        }

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

        if (memberRole == "CLINIC_ADMIN")
        {
            if (action.Equals("DOCTOR_REVIEW", StringComparison.OrdinalIgnoreCase))
            {
                return new AccessResolveResponse(false, "ACTIVE", patientId, null, null, null, null, 1);
            }

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
    // CAMERA DELEGATION RESOLUTION (Task W3-M6-02 / AC1)
    // ====================================================================

    public async Task<CameraDelegationResolveResponse> ResolveCameraDelegationAsync(
        CameraDelegationResolveRequest request, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(request.Action, "ANALYSIS_CREATE", StringComparison.OrdinalIgnoreCase))
        {
            return new CameraDelegationResolveResponse(false, null, null, null, null, null, null, "INVALID_ACTION");
        }

        if (string.IsNullOrWhiteSpace(request.DeviceId) || string.IsNullOrWhiteSpace(request.ClientId)
            || request.PatientId == Guid.Empty || request.ExpectedTenantId == Guid.Empty
            || request.ActorAccountId == Guid.Empty || request.ServicePrincipalId == Guid.Empty)
        {
            return new CameraDelegationResolveResponse(false, null, null, null, null, null, null, "INVALID_REQUEST_PARAMETERS");
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        // 1. Kiểm tra bệnh nhân và tenant
        const string patientSql = @"
            SELECT p.patient_id, p.tenant_id, p.linked_account_id, p.owner_principal_id, p.status, t.tenant_kind
            FROM profile.patients p
            JOIN profile.tenants t ON t.tenant_id = p.tenant_id
            WHERE p.patient_id = $1;";

        Guid pTenantId;
        Guid? pLinkedAccountId;
        Guid pOwnerPrincipalId;
        string pStatus;
        string tenantKind;

        await using (var pCmd = new NpgsqlCommand(patientSql, connection))
        {
            pCmd.Parameters.AddWithValue(request.PatientId);
            await using var reader = await pCmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return new CameraDelegationResolveResponse(false, null, null, null, null, null, null, "PATIENT_NOT_FOUND");
            }

            pTenantId = reader.GetGuid(1);
            pLinkedAccountId = reader.IsDBNull(2) ? (Guid?)null : reader.GetGuid(2);
            pOwnerPrincipalId = reader.GetGuid(3);
            pStatus = reader.GetString(4);
            tenantKind = reader.GetString(5);
        }

        // Camera Sprint 3 chỉ áp dụng cho tenant PERSONAL
        if (!string.Equals(tenantKind, "PERSONAL", StringComparison.OrdinalIgnoreCase))
        {
            return new CameraDelegationResolveResponse(false, request.PatientId, pTenantId, tenantKind, null, null, null, "CLINIC_CAMERA_UNSUPPORTED");
        }

        if (!string.Equals(pStatus, "ACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            return new CameraDelegationResolveResponse(false, request.PatientId, pTenantId, tenantKind, null, null, null, "PATIENT_DISABLED");
        }

        // Kiểm tra actorAccountId phải là chủ sở hữu hoặc tài khoản liên kết của bệnh nhân
        var isOwn = (pLinkedAccountId.HasValue && pLinkedAccountId.Value == request.ActorAccountId)
                    || pOwnerPrincipalId == request.ActorAccountId;
        if (!isOwn)
        {
            return new CameraDelegationResolveResponse(false, request.PatientId, pTenantId, tenantKind, null, null, null, "ACTOR_PATIENT_MISMATCH");
        }

        // Kiểm tra expectedTenantId phải khớp với tenantId của bệnh nhân
        if (request.ExpectedTenantId != pTenantId)
        {
            return new CameraDelegationResolveResponse(false, request.PatientId, pTenantId, tenantKind, null, null, null, "TENANT_MISMATCH");
        }

        // 2. Kiểm tra bản ghi ủy quyền Camera (profile.camera_grants)
        const string grantSql = @"
            SELECT grant_id, status, expires_at
            FROM profile.camera_grants
            WHERE device_id = $1 AND client_id = $2 AND service_principal_id = $3
              AND patient_id = $4 AND tenant_id = $5
            LIMIT 1;";

        Guid grantId;
        string grantStatus;
        DateTimeOffset expiresAt;

        await using (var gCmd = new NpgsqlCommand(grantSql, connection))
        {
            gCmd.Parameters.AddWithValue(request.DeviceId);
            gCmd.Parameters.AddWithValue(request.ClientId);
            gCmd.Parameters.AddWithValue(request.ServicePrincipalId);
            gCmd.Parameters.AddWithValue(request.PatientId);
            gCmd.Parameters.AddWithValue(pTenantId);

            await using var reader = await gCmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return new CameraDelegationResolveResponse(false, request.PatientId, pTenantId, tenantKind, null, null, null, "GRANT_NOT_FOUND");
            }

            grantId = reader.GetGuid(0);
            grantStatus = reader.GetString(1);
            expiresAt = reader.GetFieldValue<DateTimeOffset>(2);
        }

        if (!string.Equals(grantStatus, "ACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            return new CameraDelegationResolveResponse(false, request.PatientId, pTenantId, tenantKind, null, null, grantId, "GRANT_REVOKED");
        }

        if (expiresAt <= DateTimeOffset.UtcNow)
        {
            return new CameraDelegationResolveResponse(false, request.PatientId, pTenantId, tenantKind, null, null, grantId, "GRANT_EXPIRED");
        }

        // 3. Kiểm tra trạng thái tài khoản Actor trong identity.accounts (Fail-Closed)
        const string accSql = "SELECT account_status FROM identity.accounts WHERE account_id = $1 LIMIT 1;";
        await using (var aCmd = new NpgsqlCommand(accSql, connection))
        {
            aCmd.Parameters.AddWithValue(request.ActorAccountId);
            var aObj = await aCmd.ExecuteScalarAsync(cancellationToken);
            if (aObj is not string accStatus || !string.Equals(accStatus, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            {
                return new CameraDelegationResolveResponse(false, request.PatientId, pTenantId, tenantKind, null, null, grantId, "ACCOUNT_DISABLED");
            }
        }

        // 4. Hợp lệ hoàn toàn
        return new CameraDelegationResolveResponse(
            Allowed: true,
            PatientId: request.PatientId,
            TenantId: pTenantId,
            TenantKind: "PERSONAL",
            OwnerPrincipalId: request.ActorAccountId,
            BillingOwner: new BillingOwnerDto("ACCOUNT", request.ActorAccountId),
            GrantId: grantId,
            Reason: null
        );
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
            SELECT m.membership_id, m.clinic_id, m.account_id, COALESCE(p.display_name, 'Thành viên phòng khám'),
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

        const string checkProfileSql = "SELECT display_name FROM profile.profiles WHERE account_id = $1 LIMIT 1;";
        string displayName;
        await using (var profileCmd = new NpgsqlCommand(checkProfileSql, connection))
        {
            profileCmd.Parameters.AddWithValue(targetAccountId);
            var nameObj = await profileCmd.ExecuteScalarAsync(cancellationToken);
            if (nameObj is not string name)
            {
                return (false, null, "ERR_ACCOUNT_NOT_FOUND", "Không tìm thấy hồ sơ người dùng tương ứng.");
            }
            displayName = name;
        }

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
            return (false, null, "ERR_MEMBERSHIP_ALREADY_EXISTS", "Thành viên đã tồn tại và đang hoạt động trong phòng khám.");
        }

        if (existingMembershipId.HasValue)
        {
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

        return (false, null, "ERR_INTERNAL_DB", "Không thể ghi nhận thành viên phòng khám.");
    }

    public async Task<(bool Success, string? ErrorCode, string? ErrorMessage)> RevokeClinicMemberAsync(
        Guid clinicId, Guid targetAccountId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

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
                return (false, "ERR_MEMBERSHIP_NOT_FOUND", "Không tìm thấy thành viên hoạt động trong phòng khám.");
            }
            role = r;
        }

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
                return (false, "ERR_CANNOT_REVOKE_SOLE_ADMIN", "Không thể gỡ bỏ quản trị viên duy nhất của phòng khám.");
            }
        }

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

        return (false, "ERR_MEMBERSHIP_NOT_FOUND", "Không tìm thấy thành viên hoạt động trong phòng khám.");
    }

    // ====================================================================
    // DOCTOR ASSIGNMENT MANAGEMENT (Task W3-M6-02 / AC2)
    // ====================================================================

    public async Task<(bool Success, DoctorAssignmentDto? Assignment, string? ErrorCode, string? ErrorMessage)> CreateOrReactivateDoctorAssignmentAsync(
        Guid clinicId, Guid doctorId, Guid patientId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        // 1. Kiểm tra Clinic tồn tại và ACTIVE
        const string clinicSql = "SELECT tenant_id FROM profile.clinics WHERE clinic_id = $1 AND status = 'ACTIVE' LIMIT 1;";
        Guid clinicTenantId;
        await using (var clinicCmd = new NpgsqlCommand(clinicSql, connection))
        {
            clinicCmd.Parameters.AddWithValue(clinicId);
            var ctObj = await clinicCmd.ExecuteScalarAsync(cancellationToken);
            if (ctObj is not Guid cTId)
            {
                return (false, null, "ERR_CLINIC_NOT_FOUND", "Không tìm thấy phòng khám.");
            }
            clinicTenantId = cTId;
        }

        // 2. Kiểm tra Bác sĩ có membership DOCTOR và ACTIVE tại clinic
        const string checkDoctorSql = @"
            SELECT m.account_id, COALESCE(p.display_name, 'Bác sĩ')
            FROM profile.memberships m
            LEFT JOIN profile.profiles p ON p.account_id = m.account_id
            WHERE m.clinic_id = $1 AND m.account_id = $2 AND m.role = 'DOCTOR' AND m.status = 'ACTIVE'
            LIMIT 1;";

        string doctorName;
        await using (var docCmd = new NpgsqlCommand(checkDoctorSql, connection))
        {
            docCmd.Parameters.AddWithValue(clinicId);
            docCmd.Parameters.AddWithValue(doctorId);
            await using var reader = await docCmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return (false, null, "ERR_INVALID_DOCTOR", "Bác sĩ không hợp lệ hoặc không có tư cách hành nghề tại phòng khám.");
            }
            doctorName = reader.GetString(1);
        }

        // 3. Kiểm tra Bệnh nhân phải thuộc phạm vi phòng khám
        const string checkPatientSql = @"
            SELECT patient_id FROM profile.patients
            WHERE patient_id = $1 AND (tenant_id = $2 OR owner_principal_id = $3) AND status = 'ACTIVE'
            LIMIT 1;";

        await using (var patientCmd = new NpgsqlCommand(checkPatientSql, connection))
        {
            patientCmd.Parameters.AddWithValue(patientId);
            patientCmd.Parameters.AddWithValue(clinicTenantId);
            patientCmd.Parameters.AddWithValue(clinicId);
            var pObj = await patientCmd.ExecuteScalarAsync(cancellationToken);
            if (pObj is not Guid)
            {
                return (false, null, "ERR_PATIENT_NOT_IN_CLINIC", "Bệnh nhân không thuộc phạm vi quản lý của phòng khám.");
            }
        }

        // 4. Kiểm tra phân công đã tồn tại chưa
        const string checkAssignSql = @"
            SELECT assignment_id, status
            FROM profile.doctor_assignments
            WHERE clinic_id = $1 AND doctor_account_id = $2 AND patient_id = $3
            LIMIT 1;";

        Guid? existingAssignId = null;
        string? existingStatus = null;

        await using (var checkCmd = new NpgsqlCommand(checkAssignSql, connection))
        {
            checkCmd.Parameters.AddWithValue(clinicId);
            checkCmd.Parameters.AddWithValue(doctorId);
            checkCmd.Parameters.AddWithValue(patientId);
            await using var reader = await checkCmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                existingAssignId = reader.GetGuid(0);
                existingStatus = reader.GetString(1);
            }
        }

        if (existingAssignId.HasValue && string.Equals(existingStatus, DoctorAssignmentStatuses.Active, StringComparison.OrdinalIgnoreCase))
        {
            return (false, null, "ERR_ASSIGNMENT_ALREADY_EXISTS", "Phân công bác sĩ cho bệnh nhân này đã tồn tại và đang hoạt động.");
        }

        if (existingAssignId.HasValue)
        {
            // Tái kích hoạt phân công đã bị thu hồi trước đó
            const string reactivateSql = @"
                UPDATE profile.doctor_assignments
                SET status = 'ACTIVE', revoked_at = NULL, assigned_at = NOW(), updated_at = NOW()
                WHERE assignment_id = $1
                RETURNING assigned_at, created_at, updated_at;";

            await using var reactivateCmd = new NpgsqlCommand(reactivateSql, connection);
            reactivateCmd.Parameters.AddWithValue(existingAssignId.Value);

            await using var reader = await reactivateCmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                var assignedAt = reader.GetFieldValue<DateTimeOffset>(0);
                var createdAt = reader.GetFieldValue<DateTimeOffset>(1);
                var updatedAt = reader.GetFieldValue<DateTimeOffset>(2);

                return (true, new DoctorAssignmentDto(
                    existingAssignId.Value, clinicId, doctorId, doctorName, patientId, DoctorAssignmentStatuses.Active, assignedAt, null, createdAt, updatedAt), null, null);
            }
        }
        else
        {
            // Tạo mới phân công
            var newAssignId = Guid.NewGuid();
            const string insertSql = @"
                INSERT INTO profile.doctor_assignments (assignment_id, clinic_id, doctor_account_id, patient_id, status, assigned_at, created_at, updated_at)
                VALUES ($1, $2, $3, $4, 'ACTIVE', NOW(), NOW(), NOW())
                RETURNING assigned_at, created_at, updated_at;";

            await using var insertCmd = new NpgsqlCommand(insertSql, connection);
            insertCmd.Parameters.AddWithValue(newAssignId);
            insertCmd.Parameters.AddWithValue(clinicId);
            insertCmd.Parameters.AddWithValue(doctorId);
            insertCmd.Parameters.AddWithValue(patientId);

            await using var reader = await insertCmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                var assignedAt = reader.GetFieldValue<DateTimeOffset>(0);
                var createdAt = reader.GetFieldValue<DateTimeOffset>(1);
                var updatedAt = reader.GetFieldValue<DateTimeOffset>(2);

                return (true, new DoctorAssignmentDto(
                    newAssignId, clinicId, doctorId, doctorName, patientId, DoctorAssignmentStatuses.Active, assignedAt, null, createdAt, updatedAt), null, null);
            }
        }

        return (false, null, "ERR_INTERNAL_DB", "Không thể ghi nhận phân công bác sĩ.");
    }

    public async Task<(bool Success, IReadOnlyList<DoctorAssignmentDto>? Items, string? ErrorCode, string? ErrorMessage)> GetDoctorAssignmentsAsync(
        Guid clinicId, Guid actorAccountId, IReadOnlyList<string> actorRoles, Guid? doctorIdFilter, Guid? patientIdFilter, string? statusFilter, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        // 1. Kiểm tra Clinic tồn tại
        const string clinicSql = "SELECT tenant_id FROM profile.clinics WHERE clinic_id = $1 AND status = 'ACTIVE' LIMIT 1;";
        await using (var clinicCmd = new NpgsqlCommand(clinicSql, connection))
        {
            clinicCmd.Parameters.AddWithValue(clinicId);
            var ctObj = await clinicCmd.ExecuteScalarAsync(cancellationToken);
            if (ctObj is not Guid)
            {
                return (false, null, "ERR_CLINIC_NOT_FOUND", "Không tìm thấy phòng khám.");
            }
        }

        // 2. Xác định vai trò của Actor tại phòng khám
        var isSystemAdmin = actorRoles.Contains("SYSTEM_ADMIN", StringComparer.OrdinalIgnoreCase);
        string? memberRole = null;

        if (!isSystemAdmin)
        {
            const string memberSql = "SELECT role FROM profile.memberships WHERE clinic_id = $1 AND account_id = $2 AND status = 'ACTIVE' LIMIT 1;";
            await using var memberCmd = new NpgsqlCommand(memberSql, connection);
            memberCmd.Parameters.AddWithValue(clinicId);
            memberCmd.Parameters.AddWithValue(actorAccountId);
            var roleObj = await memberCmd.ExecuteScalarAsync(cancellationToken);
            memberRole = roleObj as string;

            if (string.IsNullOrEmpty(memberRole))
            {
                return (false, null, "ERR_CLINIC_NOT_FOUND", "Không tìm thấy phòng khám.");
            }
        }

        // 3. Ràng buộc quyền xem:
        // CLINIC_ADMIN hoặc SYSTEM_ADMIN: Xem được toàn bộ, tùy chọn lọc doctorId
        // DOCTOR: Chỉ được xem của chính mình (nếu truyền doctorId khác mình -> 403)
        Guid? effectiveDoctorId = doctorIdFilter;
        if (!isSystemAdmin && string.Equals(memberRole, "DOCTOR", StringComparison.OrdinalIgnoreCase))
        {
            if (doctorIdFilter.HasValue && doctorIdFilter.Value != actorAccountId)
            {
                return (false, null, "ERR_FORBIDDEN_DOCTOR_ACCESS", "Bác sĩ chỉ có quyền xem danh sách phân công của chính mình.");
            }
            effectiveDoctorId = actorAccountId;
        }

        var queryStatus = string.IsNullOrWhiteSpace(statusFilter) || statusFilter.Equals("ALL", StringComparison.OrdinalIgnoreCase)
            ? null
            : statusFilter.Trim().ToUpperInvariant();

        const string sql = @"
            SELECT a.assignment_id, a.clinic_id, a.doctor_account_id, COALESCE(p.display_name, 'Bác sĩ'),
                   a.patient_id, a.status, a.assigned_at, a.revoked_at, a.created_at, a.updated_at
            FROM profile.doctor_assignments a
            LEFT JOIN profile.profiles p ON p.account_id = a.doctor_account_id
            WHERE a.clinic_id = $1
              AND ($2::uuid IS NULL OR a.doctor_account_id = $2)
              AND ($3::uuid IS NULL OR a.patient_id = $3)
              AND ($4::text IS NULL OR a.status = $4)
            ORDER BY a.assigned_at DESC;";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue(clinicId);
        cmd.Parameters.AddWithValue((object?)effectiveDoctorId ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)patientIdFilter ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)queryStatus ?? DBNull.Value);

        var list = new List<DoctorAssignmentDto>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var assignmentId = reader.GetGuid(0);
            var cId = reader.GetGuid(1);
            var docId = reader.GetGuid(2);
            var doctorName = reader.GetString(3);
            var pId = reader.GetGuid(4);
            var status = reader.GetString(5);
            var assignedAt = reader.GetFieldValue<DateTimeOffset>(6);
            var revokedAt = reader.IsDBNull(7) ? (DateTimeOffset?)null : reader.GetFieldValue<DateTimeOffset>(7);
            var createdAt = reader.GetFieldValue<DateTimeOffset>(8);
            var updatedAt = reader.GetFieldValue<DateTimeOffset>(9);

            list.Add(new DoctorAssignmentDto(
                assignmentId, cId, docId, doctorName, pId, status, assignedAt, revokedAt, createdAt, updatedAt));
        }

        return (true, list, null, null);
    }

    public async Task<(bool Success, string? ErrorCode, string? ErrorMessage)> RevokeDoctorAssignmentAsync(
        Guid clinicId, Guid assignmentId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        const string checkSql = @"
            SELECT status FROM profile.doctor_assignments
            WHERE assignment_id = $1 AND clinic_id = $2
            LIMIT 1;";

        string status;
        await using (var checkCmd = new NpgsqlCommand(checkSql, connection))
        {
            checkCmd.Parameters.AddWithValue(assignmentId);
            checkCmd.Parameters.AddWithValue(clinicId);
            var statusObj = await checkCmd.ExecuteScalarAsync(cancellationToken);
            if (statusObj is not string s)
            {
                return (false, "ERR_ASSIGNMENT_NOT_FOUND", "Không tìm thấy phân công trong phòng khám.");
            }
            status = s;
        }

        if (!string.Equals(status, DoctorAssignmentStatuses.Active, StringComparison.OrdinalIgnoreCase))
        {
            return (false, "ERR_ASSIGNMENT_NOT_ACTIVE", "Phân công đã ở trạng thái thu hồi hoặc không còn hoạt động.");
        }

        const string revokeSql = @"
            UPDATE profile.doctor_assignments
            SET status = 'REVOKED', revoked_at = NOW(), updated_at = NOW()
            WHERE assignment_id = $1 AND clinic_id = $2 AND status = 'ACTIVE';";

        await using var revokeCmd = new NpgsqlCommand(revokeSql, connection);
        revokeCmd.Parameters.AddWithValue(assignmentId);
        revokeCmd.Parameters.AddWithValue(clinicId);
        var rows = await revokeCmd.ExecuteNonQueryAsync(cancellationToken);
        if (rows > 0)
        {
            return (true, null, null);
        }

        return (false, "ERR_ASSIGNMENT_NOT_FOUND", "Không tìm thấy phân công trong phòng khám.");
    }
}
