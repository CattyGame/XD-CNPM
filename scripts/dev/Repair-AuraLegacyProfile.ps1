[CmdletBinding()]
param(
    [string]$RepositoryPath = ".",
    [Parameter(Mandatory = $true)]
    [string]$BackupFile,
    [string]$ContainerName = "aura-postgres-1",
    [switch]$PlanOnly
)

$ErrorActionPreference = "Stop"
$repo = (Resolve-Path -LiteralPath $RepositoryPath).Path
$backup = Get-Item -LiteralPath $BackupFile -ErrorAction Stop
if ($backup.PSIsContainer -or $backup.Length -lt 5) {
    throw "A non-empty pg_dump custom-format backup is required."
}
$stream = [IO.File]::OpenRead($backup.FullName)
try {
    $magic = New-Object byte[] 5
    if ($stream.Read($magic, 0, 5) -ne 5 -or [Text.Encoding]::ASCII.GetString($magic) -ne "PGDMP") {
        throw "Backup must be a pg_dump custom-format file (PGDMP header)."
    }
}
finally { $stream.Dispose() }

# Pin the reviewed migration contents, allowing normal Windows CRLF checkout.
$sources = @(
    @("infra/postgres/migrations/002_identity_and_profile.sql", "5e0d0ba919eadcbbb3123f106579d85acbe7595328597279d02ef913732d37a0"),
    @("infra/postgres/migrations/003_doctor_assignments.sql", "f04ca5b902ca90c77f6465f23a3c9cd95151038f06b3dc7544db15c2a9b8caa5")
)
$migrationSql = @()
foreach ($source in $sources) {
    $text = (Get-Content -LiteralPath (Join-Path $repo $source[0]) -Raw -Encoding UTF8).Replace("`r`n", "`n")
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $hash = [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($text))).Replace("-", "").ToLowerInvariant()
    }
    finally { $sha.Dispose() }
    if ($hash -ne $source[1]) {
        throw "Migration differs from the reviewed develop source: $($source[0]). No database change was made."
    }
    $migrationSql += $text
}

$guardSql = @'
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '60s';
SELECT pg_advisory_xact_lock(18310226, 1018);

DO $repair$
DECLARE
    obj record;
    has_rows boolean;
    actual_tables text[];
BEGIN
    IF current_database() <> 'aura_dev' THEN
        RAISE EXCEPTION 'Repair is restricted to aura_dev';
    END IF;
    IF EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = 'profile_legacy_m1_20261018') THEN
        RAISE EXCEPTION 'Archive already exists; stop and inspect instead of running repair twice';
    END IF;
    SELECT array_agg(c.relname::text ORDER BY c.relname)
      INTO actual_tables
      FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
     WHERE n.nspname = 'profile' AND c.relkind IN ('r', 'p');
    IF actual_tables IS DISTINCT FROM ARRAY[
        'clinic_audit_logs', 'clinic_members', 'clinics', 'doctor_assignments',
        'patients', 'tenants', 'user_profiles'
    ]::text[] THEN
        RAISE EXCEPTION 'Profile table set differs from the inspected empty legacy schema';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
        WHERE table_schema='profile' AND table_name='tenants' AND column_name='kind')
       OR EXISTS (SELECT 1 FROM information_schema.columns
        WHERE table_schema='profile' AND table_name='tenants' AND column_name='owner_principal_id')
       OR NOT EXISTS (SELECT 1 FROM information_schema.columns
        WHERE table_schema='profile' AND table_name='patients' AND column_name='full_name')
       OR EXISTS (SELECT 1 FROM information_schema.columns
        WHERE table_schema='profile' AND table_name='patients' AND column_name='status') THEN
        RAISE EXCEPTION 'Legacy fingerprint does not match the inspected schema';
    END IF;
    IF EXISTS (SELECT 1 FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace WHERE n.nspname='profile')
       OR EXISTS (SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
           WHERE n.nspname='profile' AND c.relkind IN ('v','m','f','p')) THEN
        RAISE EXCEPTION 'Profile contains routines/views/foreign/partitioned tables; manual review required';
    END IF;

    -- Lock both schemas before checking that no application data exists.
    FOR obj IN SELECT n.nspname, c.relname
      FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
     WHERE n.nspname IN ('identity','profile') AND c.relkind='r'
     ORDER BY n.nspname,c.relname
    LOOP
        EXECUTE format('LOCK TABLE %I.%I IN ACCESS EXCLUSIVE MODE', obj.nspname, obj.relname);
    END LOOP;
    FOR obj IN SELECT n.nspname, c.relname
      FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
     WHERE n.nspname IN ('identity','profile') AND c.relkind='r'
       AND NOT (n.nspname='identity' AND c.relname='roles')
     ORDER BY n.nspname,c.relname
    LOOP
        EXECUTE format('SELECT EXISTS (SELECT 1 FROM %I.%I)', obj.nspname, obj.relname) INTO has_rows;
        IF has_rows THEN
            RAISE EXCEPTION 'Data exists in %.%; repair refuses to archive a populated environment', obj.nspname,obj.relname;
        END IF;
    END LOOP;

    -- Do not redirect another schema's FK or view to archived relations.
    IF EXISTS (
        SELECT 1 FROM pg_constraint k
        JOIN pg_class parent ON parent.oid=k.confrelid
        JOIN pg_namespace pn ON pn.oid=parent.relnamespace
        JOIN pg_class child ON child.oid=k.conrelid
        JOIN pg_namespace cn ON cn.oid=child.relnamespace
        WHERE k.contype='f' AND pn.nspname='profile' AND cn.nspname<>'profile'
    ) OR EXISTS (
        SELECT 1 FROM pg_depend d
        JOIN pg_class parent ON parent.oid=d.refobjid
        JOIN pg_namespace pn ON pn.oid=parent.relnamespace
        JOIN pg_rewrite rw ON rw.oid=d.objid
        JOIN pg_class child ON child.oid=rw.ev_class
        JOIN pg_namespace cn ON cn.oid=child.relnamespace
        WHERE d.refclassid='pg_class'::regclass AND d.classid='pg_rewrite'::regclass
          AND pn.nspname='profile' AND cn.nspname<>'profile'
    ) THEN
        RAISE EXCEPTION 'Cross-schema dependencies target legacy Profile; manual migration required';
    END IF;

    ALTER SCHEMA profile RENAME TO profile_legacy_m1_20261018;
    REVOKE ALL ON SCHEMA profile_legacy_m1_20261018 FROM PUBLIC, aura_profile_app;
END
$repair$;
'@

$verifySql = @'
DO $verify$
BEGIN
    IF to_regclass('profile.profiles') IS NULL
       OR to_regclass('profile.memberships') IS NULL
       OR to_regclass('profile.doctor_assignments') IS NULL
       OR NOT EXISTS (SELECT 1 FROM information_schema.columns
           WHERE table_schema='profile' AND table_name='tenants' AND column_name='owner_principal_id')
       OR NOT EXISTS (SELECT 1 FROM information_schema.columns
           WHERE table_schema='profile' AND table_name='patients' AND column_name='status') THEN
        RAISE EXCEPTION 'Canonical Profile creation did not complete';
    END IF;
END
$verify$;
COMMIT;
SELECT 'PASS: empty legacy Profile archived; canonical Profile created atomically' AS result;
'@

if ($PlanOnly) {
    Write-Host "Plan: verify backup header and reviewed source hashes; check aura_dev and lock/check empty Identity/Profile."
    Write-Host "Plan: reject unknown schema/dependencies; archive Profile as profile_legacy_m1_20261018."
    Write-Host "Plan: create canonical Profile using the two reviewed migrations; commit together or roll back together."
    Write-Host "No data is copied/deleted. Identity roles, other service schemas and runtime passwords are preserved."
    return
}

Get-Command docker -ErrorAction Stop | Out-Null
$sql = $guardSql + "`n" + ($migrationSql -join "`n") + "`n" + $verifySql
$oldEncoding = $OutputEncoding
try {
    $OutputEncoding = New-Object Text.UTF8Encoding($false)
    $sql | & docker exec -i $ContainerName psql -X -v ON_ERROR_STOP=1 -U aura_dev -d aura_dev
    if ($LASTEXITCODE -ne 0) {
        throw "Repair returned an error. SQL errors before COMMIT roll back this repair; inspect the database state before retrying."
    }
}
finally { $OutputEncoding = $oldEncoding }
Write-Host "Next: run Initialize-AuraDevDatabase.ps1 -ConfigureRuntimeLogins to finish migrations/grants and runtime passwords."
