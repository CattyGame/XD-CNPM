using System.Text.Json;
using System.Text.RegularExpressions;
using AURA.Analysis.Api.Upload;
using Npgsql;

namespace AURA.Analysis.PersistenceChecks;

internal static class Program
{
    private static string _current = "preflight";
    private static int _passed;
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static async Task Check(string name, Func<Task> action)
    {
        _current = name;
        await action();
        _passed++;
        Console.WriteLine("PASS: " + name);
    }

    private static IdempotencyScope Scope(Guid? tenant = null, Guid? requester = null, Guid? key = null) =>
        new(tenant ?? Guid.NewGuid(), requester ?? Guid.NewGuid(), key ?? Guid.NewGuid());

    private static BatchPlan Plan(IdempotencyScope scope, string fingerprint, Guid? patient = null)
    {
        var batch = Guid.NewGuid(); var image = Guid.NewGuid(); var file = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow; var job = new JobPlan(Guid.NewGuid(), Guid.NewGuid(), "repo-check");
        var response = new AcceptedResponse(batch, new[] { new AcceptedItem(image, "synthetic.png", "QUEUED", null) },
            now.UtcDateTime, $"/api/v1/batches/{batch:D}");
        var images = new[] { new ImagePlan(image, file, $"{scope.TenantId:D}/{batch:D}/{image:D}", "synthetic.png",
            "image/png", 100, 10, 10, new string('b',64), "FUNDUS", "left", "QUEUED", null, job) };
        var payload = JsonSerializer.Serialize(new { analysisId = image });
        var outbox = new[] { new OutboxPlan(Guid.NewGuid(), image, "analysis.requested", "0.6", payload, now),
            new OutboxPlan(job.MessageId, image, "analysis.job.run", "0.6", payload, now) };
        return new(batch, scope.TenantId, scope.RequesterId, scope.RequesterId, patient ?? Guid.NewGuid(), scope.Key,
            fingerprint, RequestFingerprint.Version, "repo-check", now, response, images, outbox);
    }

    private static async Task<long> Scalar(NpgsqlDataSource source, string sql, params object[] parameters)
    {
        await using var command = source.CreateCommand(sql);
        foreach (var value in parameters) command.Parameters.AddWithValue(value);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    public static async Task<int> Main()
    {
        try
        {
            var raw = Environment.GetEnvironmentVariable("AURA_ANALYSIS_PERSISTENCE_TEST_CONNECTION");
            Require(!string.IsNullOrWhiteSpace(raw), "Use the guarded Python runner to create an isolated test database first.");
            var options = new NpgsqlConnectionStringBuilder(raw);
            Require(options.Host is "127.0.0.1" or "localhost", "Test host must be loopback.");
            Require(options.Username == "aura_analysis_app", "Runtime credentials are required.");
            Require(Regex.IsMatch(options.Database ?? "", "^aura_m2_repo_[0-9a-f]{32}$"), "Refusing a non-test database.");
            options.IncludeErrorDetail = false;
            options.Timeout = 5; options.CommandTimeout = 10; options.MaxPoolSize = 16;
            await using var source = NpgsqlDataSource.Create(options.ConnectionString);
            await using (var command = source.CreateCommand("""
                SELECT current_user, shobj_description(d.oid,'pg_database'),
                       current_setting('server_version_num')::int, r.rolsuper, r.rolcreatedb, r.rolcreaterole
                FROM pg_database d JOIN pg_roles r ON r.rolname=current_user WHERE d.datname=current_database();
                """))
            await using (var reader = await command.ExecuteReaderAsync())
            {
                Require(await reader.ReadAsync(), "Missing database metadata.");
                Require(reader.GetString(0) == "aura_analysis_app", "Wrong database principal.");
                Require(!reader.IsDBNull(1) && reader.GetString(1) == "aura-m2-repo-check:" + options.Database![13..], "Missing test database marker.");
                Require(reader.GetInt32(2) / 10000 == 17, "PostgreSQL 17 is required.");
                Require(!reader.GetBoolean(3) && !reader.GetBoolean(4) && !reader.GetBoolean(5), "Runtime role has admin privileges.");
            }
            const string allRows = """
                SELECT (SELECT count(*) FROM analysis.analysis_requests)+(SELECT count(*) FROM analysis.images)
                 +(SELECT count(*) FROM analysis.jobs)+(SELECT count(*) FROM analysis.outbox_events)
                 +(SELECT count(*) FROM analysis.analysis_results)+(SELECT count(*) FROM analysis.annotation_refs)
                 +(SELECT count(*) FROM analysis.doctor_reviews)+(SELECT count(*) FROM analysis.audit_logs);
                """;
            Require(await Scalar(source, allRows) == 0, "Test schema must start empty.");
            Console.WriteLine("PASS: isolated PostgreSQL 17 database and actual runtime login verified.");
            var repository = new PostgresUploadRepository(source);
            var files = new PostgresFileAccessRepository(source);
            var scope = Scope(); var fingerprint = new string('a',64); var plan = Plan(scope,fingerprint);

            await Check("Atomic batch/image/job/outbox commit", async () =>
            {
                await using var session = await repository.BeginAsync(scope,fingerprint,default);
                Require(session.State == IdempotencyState.New,"First request is not new.");
                await session.CommitAsync(plan,default);
                Require(await Scalar(source,"SELECT count(*) FROM analysis.analysis_requests WHERE id=$1;",plan.BatchId)==1,"Batch missing.");
                Require(await Scalar(source,"SELECT count(*) FROM analysis.images WHERE analysis_request_id=$1;",plan.BatchId)==1,"Image missing.");
                Require(await Scalar(source,"SELECT count(*) FROM analysis.jobs WHERE image_id=$1;",plan.Images[0].AnalysisId)==1,"Job missing.");
                Require(await Scalar(source,"SELECT count(*) FROM analysis.outbox_events WHERE analysis_request_id=$1;",plan.BatchId)==2,"Outbox missing.");
                Require(await Scalar(source,"SELECT count(*) FROM analysis.analysis_requests WHERE id=$1 AND request_schema_version=$2;",plan.BatchId,RequestFingerprint.Version)==1,"Fingerprint format version was not preserved.");
            });
            await Check("Replay preserves committed response and creates no extra rows", async () =>
            {
                await using var session = await repository.BeginAsync(scope,fingerprint,default);
                Require(session.State==IdempotencyState.Replay,"Replay not detected.");
                Require(JsonSerializer.Serialize(session.StoredResponse,UploadJson.Options)==JsonSerializer.Serialize(plan.Response,UploadJson.Options),"Replay response changed.");
                Require(await Scalar(source,allRows)==5,"Replay changed row counts.");
            });
            await Check("Same key with changed fingerprint is a conflict", async () =>
            {
                await using var session = await repository.BeginAsync(scope,new string('c',64),default);
                Require(session.State==IdempotencyState.Conflict && session.StoredResponse is null,"Fingerprint conflict not detected.");
            });
            await Check("Concurrent same-key request gets retryable conflict; disposal releases lock", async () =>
            {
                var same=Scope();
                await using (var held=await repository.BeginAsync(same,fingerprint,default))
                {
                    try { await using var unexpected=await repository.BeginAsync(same,fingerprint,default); throw new InvalidOperationException("Concurrent lock succeeded."); }
                    catch(UploadFailure ex) { Require(ex.Status==409 && ex.Code=="ERR_REQUEST_IN_PROGRESS" && ex.Retryable,"Wrong contention error."); }
                }
                await using var reopened=await repository.BeginAsync(same,fingerprint,default);
                Require(reopened.State==IdempotencyState.New,"Lock was not released.");
            });
            await Check("Idempotency key is independent across tenants and requesters", async () =>
            {
                foreach(var candidate in new[] { Scope(requester:scope.RequesterId,key:scope.Key), Scope(scope.TenantId,key:scope.Key) })
                {
                    await using var session=await repository.BeginAsync(candidate,fingerprint,default);
                    Require(session.State==IdempotencyState.New,"Scope leaked across tenant/requester.");
                }
            });
            await Check("Dedupe respects tenant, patient, modality and eye side", async () =>
            {
                await using(var session=await repository.BeginAsync(Scope(scope.TenantId),fingerprint,default))
                {
                    Require(await session.FindExistingAnalysisAsync(plan.PatientId,"FUNDUS","left",plan.Images[0].Sha256,default)==plan.Images[0].AnalysisId,"Existing image not found.");
                    Require(await session.FindExistingAnalysisAsync(Guid.NewGuid(),"FUNDUS","left",plan.Images[0].Sha256,default) is null,"Cross-patient dedupe.");
                    Require(await session.FindExistingAnalysisAsync(plan.PatientId,"FUNDUS","right",plan.Images[0].Sha256,default) is null,"Cross-eye dedupe.");
                }
                await using var other=await repository.BeginAsync(Scope(),fingerprint,default);
                Require(await other.FindExistingAnalysisAsync(plan.PatientId,"FUNDUS","left",plan.Images[0].Sha256,default) is null,"Cross-tenant dedupe.");
            });
            await Check("Different keys contend on the same new-image dedupe scope", async () =>
            {
                var tenant=Guid.NewGuid();var patient=Guid.NewGuid();var sha=new string('d',64);
                await using var first=await repository.BeginAsync(Scope(tenant),fingerprint,default);
                await using var second=await repository.BeginAsync(Scope(tenant),fingerprint,default);
                Require(await first.FindExistingAnalysisAsync(patient,"FUNDUS","left",sha,default) is null,"Unexpected fixture image.");
                try { await second.FindExistingAnalysisAsync(patient,"FUNDUS","left",sha,default); throw new InvalidOperationException("Dedupe race was allowed."); }
                catch(UploadFailure ex) { Require(ex.Status==409 && ex.Retryable,"Wrong dedupe contention error."); }
            });
            await Check("Failed outbox insert rolls back the entire batch", async () =>
            {
                var failedScope=Scope();var failedPlan=Plan(failedScope,fingerprint);
                var bad=failedPlan with { Outbox=new[] { failedPlan.Outbox[0] with { ImageId=Guid.NewGuid() } } };
                await using(var session=await repository.BeginAsync(failedScope,fingerprint,default))
                {
                    try { await session.CommitAsync(bad,default); throw new InvalidOperationException("Bad FK was accepted."); }
                    catch(PostgresException ex) { Require(ex.SqlState=="23503","Wrong failure for outbox FK."); }
                }
                Require(await Scalar(source,"SELECT count(*) FROM analysis.analysis_requests WHERE id=$1;",failedPlan.BatchId)==0,"Partial batch retained.");
                Require(await Scalar(source,"SELECT count(*) FROM analysis.images WHERE analysis_request_id=$1;",failedPlan.BatchId)==0,"Partial image retained.");
                Require(await Scalar(source,"SELECT count(*) FROM analysis.jobs WHERE image_id=$1;",failedPlan.Images[0].AnalysisId)==0,"Partial job retained.");
            });
            await Check("A plan cannot be committed under a different locked scope", async () =>
            {
                var own=Scope();await using var session=await repository.BeginAsync(own,fingerprint,default);
                try { await session.CommitAsync(Plan(Scope(),fingerprint),default); throw new InvalidOperationException("Mismatched plan was accepted."); }
                catch(ArgumentException) { }
            });
            await Check("Original file metadata is bound to the requested analysis and file IDs", async () =>
            {
                var image=plan.Images[0];var record=await files.FindAsync(image.AnalysisId,image.FileId,default);
                Require(record is not null && record.TenantId==plan.TenantId && record.PatientId==plan.PatientId
                    && record.OwnerId==plan.OwnerId && record.Kind==StoredFileKind.Original && record.ObjectKey==image.ObjectKey,"Wrong original metadata.");
                Require(await files.FindAsync(Guid.NewGuid(),image.FileId,default) is null,"File leaked under another analysis.");
                Require(await files.FindAsync(image.AnalysisId,Guid.NewGuid(),default) is null,"Unknown file found.");
            });
            await Check("Annotation file metadata uses its own private object", async () =>
            {
                var image=plan.Images[0];var result=Guid.NewGuid();var annotation=Guid.NewGuid();var file=Guid.NewGuid();
                var key=$"{plan.TenantId:D}/{image.AnalysisId:D}/{annotation:D}";
                await using(var command=source.CreateCommand("""
                    INSERT INTO analysis.analysis_results(id,tenant_id,image_id,job_id,version,result_schema_version,
                      risk_assessment,metrics,is_mock,model_version,config_version,threshold_version,processing_time_ms,limitations)
                    VALUES($1,$2,$3,$4,1,'0.6','{}'::jsonb,'{}'::jsonb,true,'test','test','test',1,'synthetic');
                    """))
                {
                    foreach(var value in new object[] { result,plan.TenantId,image.AnalysisId,image.Job!.JobId }) command.Parameters.AddWithValue(value);
                    await command.ExecuteNonQueryAsync();
                }
                await using(var command=source.CreateCommand("""
                    INSERT INTO analysis.annotation_refs(id,tenant_id,image_id,analysis_result_id,file_id,type,object_key,mime_type,size_bytes,width,height,sha256)
                    VALUES($1,$2,$3,$4,$5,'MASK',$6,'image/png',100,10,10,repeat('b',64));
                    """))
                {
                    foreach(var value in new object[] { annotation,plan.TenantId,image.AnalysisId,result,file,key }) command.Parameters.AddWithValue(value);
                    await command.ExecuteNonQueryAsync();
                }
                var record=await files.FindAsync(image.AnalysisId,file,default);
                Require(record is not null && record.Kind==StoredFileKind.Annotation && record.ObjectKey==key
                    && record.PatientId==plan.PatientId && record.TenantId==plan.TenantId,"Wrong annotation metadata.");
            });
            await Check("Fresh repository/data source replays the original response", async () =>
            {
                await using var freshSource=NpgsqlDataSource.Create(options.ConnectionString);
                var fresh=new PostgresUploadRepository(freshSource);
                await using var session=await fresh.BeginAsync(scope,fingerprint,default);
                Require(session.State==IdempotencyState.Replay && session.StoredResponse!.BatchId==plan.BatchId
                    && session.StoredResponse.CreatedAt==plan.Response.CreatedAt,"Durable replay changed after reopening.");
            });
            await Check("Caller cancellation propagates without leaking a lock", async () =>
            {
                var canceled=Scope();using var cts=new CancellationTokenSource();cts.Cancel();
                try { await using var unexpected=await repository.BeginAsync(canceled,fingerprint,cts.Token); throw new InvalidOperationException("Cancellation ignored."); }
                catch(OperationCanceledException) { }
                await using var session=await repository.BeginAsync(canceled,fingerprint,default);
                Require(session.State==IdempotencyState.New,"Canceled begin retained a lock.");
            });
            Console.WriteLine($"RESULT: {_passed}/13 PostgreSQL repository groups passed with aura_analysis_app.");
            return 0;
        }
        catch(Exception ex)
        {
            var code=ex is PostgresException pg ? "; SQLSTATE="+pg.SqlState : ex is UploadFailure u ? "; code="+u.Code : "";
            Console.Error.WriteLine($"STOP: {_current}; {ex.GetType().Name}{code}. Test database retained by runner on failure.");
            return 1;
        }
    }
}
