using System.Text.Json;
using System.Text.RegularExpressions;
using AURA.Analysis.Api.Upload;
using Npgsql;

namespace AURA.Analysis.JournalChecks;

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
            var raw = Environment.GetEnvironmentVariable("AURA_ANALYSIS_JOURNAL_TEST_CONNECTION");
            Require(!string.IsNullOrWhiteSpace(raw), "Use the guarded Python runner to create an isolated test database first.");
            var options = new NpgsqlConnectionStringBuilder(raw);
            Require(options.Host is "127.0.0.1" or "localhost", "Test host must be loopback.");
            Require(options.Username == "aura_analysis_app", "Runtime credentials are required.");
            Require(Regex.IsMatch(options.Database ?? "", "^aura_m2_journal_[0-9a-f]{32}$"), "Refusing a non-test database.");
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
                Require(!reader.IsDBNull(1) && reader.GetString(1) == "aura-m2-journal-check:" + options.Database![16..], "Missing test database marker.");
                Require(reader.GetInt32(2) / 10000 == 17, "PostgreSQL 17 is required.");
                Require(!reader.GetBoolean(3) && !reader.GetBoolean(4) && !reader.GetBoolean(5), "Runtime role has admin privileges.");
            }
            const string allRows = """
                SELECT (SELECT count(*) FROM analysis.analysis_requests)+(SELECT count(*) FROM analysis.images)
                 +(SELECT count(*) FROM analysis.jobs)+(SELECT count(*) FROM analysis.outbox_events)
                 +(SELECT count(*) FROM analysis.analysis_results)+(SELECT count(*) FROM analysis.annotation_refs)
                 +(SELECT count(*) FROM analysis.doctor_reviews)+(SELECT count(*) FROM analysis.audit_logs)
                 +(SELECT count(*) FROM analysis.upload_recovery_operations)+(SELECT count(*) FROM analysis.upload_recovery_attempts);
                """;
            Require(await Scalar(source, allRows) == 0, "Test schema must start empty.");
            Console.WriteLine("PASS: isolated PostgreSQL 17 database and actual runtime login verified.");
            var writer = new PostgresUploadRepository(source);
            var journal = new PostgresUploadRecoveryRepository(source);
            var scope = Scope(); var plan = Plan(scope,new string('a',64));
            UploadRecoveryRecord r = null!;
            await Check("Prepared intent survives a fresh data source without accepting work", async () =>
            {
                r = await journal.PrepareAsync(plan,"ACCOUNT:"+plan.OwnerId,"aura-originals",default);
                await using var fresh = NpgsqlDataSource.Create(options.ConnectionString);
                var loaded = await new PostgresUploadRecoveryRepository(fresh).ReadAsync(scope,default);
                Require(loaded is not null && loaded.AttemptId==r.AttemptId && loaded.Plan.Images[0].ObjectKey==r.Plan.Images[0].ObjectKey,"Checkpoint did not survive reopen.");
                Require(await Scalar(source,"SELECT count(*) FROM analysis.analysis_requests;")==0,"Preparation accepted work early.");
            });
            await Check("Recovery is scoped and same key preserves its plan or rejects a changed fingerprint", async () =>
            {
                var again=await journal.PrepareAsync(plan with{CreatedAt=plan.CreatedAt.AddMinutes(1)},"ACCOUNT:"+plan.OwnerId,"aura-originals",default);
                Require(again.AttemptId==r.AttemptId&&again.Plan.CreatedAt==r.Plan.CreatedAt,"Replay rebuilt the prepared plan.");
                Require(again.Revision>r.Revision,"New coordinator owner did not fence the previous revision.");
                r=again;
                await Expect409(()=>journal.PrepareAsync(plan with{Fingerprint=new string('c',64)},"ACCOUNT:"+plan.OwnerId,"aura-originals",default),"ERR_IDEMPOTENCY_KEY_REUSED");
                foreach(var changed in new[]{scope with{TenantId=Guid.NewGuid()},scope with{RequesterId=Guid.NewGuid()},scope with{Key=Guid.NewGuid()}})
                    Require(await journal.ReadAsync(changed,default) is null,"Cross-scope journal disclosed.");
            });
            await Check("Concurrent checkpoint writers use one revision and acknowledgements replay without increment", async () =>
            {
                async Task<bool> TryWrite(){try{await journal.RecordAsync(scope,r.AttemptId,r.Revision,UploadRecoveryEvent.ReserveIntent,plan.Images[0].AnalysisId);return true;}catch(UploadFailure ex) when(ex.Status==409){return false;}}
                var outcomes=await Task.WhenAll(TryWrite(),TryWrite());Require(outcomes.Count(x=>x)==1,"Both checkpoint writers won.");
                r=(await journal.ReadAsync(scope,default))!;
                var again=await Step(journal,r,UploadRecoveryEvent.ReserveIntent);
                Require(again.Revision==r.Revision,"Replay advanced revision.");
                await Expect409(()=>journal.RecordAsync(scope,r.AttemptId,r.Revision-1,UploadRecoveryEvent.ReserveIntent,plan.Images[0].AnalysisId),"ERR_REQUEST_IN_PROGRESS");
            });
            await Check("Unknown reserve blocks abort completion; confirmed release enables the next generation", async () =>
            {
                r=await Step(journal,r,UploadRecoveryEvent.BeginAbort,reason:"ANALYSIS_ABORT");
                await Expect409(()=>Step(journal,r,UploadRecoveryEvent.FinishAbort),"ERR_REQUEST_IN_PROGRESS");
                r=await Step(journal,r,UploadRecoveryEvent.Reserved,Guid.NewGuid());
                r=await Step(journal,r,UploadRecoveryEvent.Released);r=await Step(journal,r,UploadRecoveryEvent.FinishAbort);
                var next=await journal.PrepareAsync(plan,"ACCOUNT:"+plan.OwnerId,"aura-originals",default);
                Require(next.AttemptNo==2&&next.Effects[0].Generation==2&&next.Effects[0].ReserveKey!=r.Effects[0].ReserveKey
                    && next.Plan.Images[0].ObjectKey!=r.Plan.Images[0].ObjectKey,"Retry did not isolate released generation.");
                Require(await Scalar(source,"SELECT count(*) FROM analysis.upload_recovery_attempts WHERE operation_id=$1;",plan.BatchId)==2,"Aborted history was lost.");
                await Expect409(()=>Step(journal,r,UploadRecoveryEvent.BeginAbort,reason:"ANALYSIS_ABORT"),"ERR_REQUEST_IN_PROGRESS");
            });
            await Check("Confirmed no-credit abort retains generation one on retry", async () =>
            {
                var p=Plan(Scope(),new string('a',64));var x=await journal.PrepareAsync(p,"ACCOUNT:"+p.OwnerId,"aura-originals",default);
                x=await Step(journal,x,UploadRecoveryEvent.ReserveIntent);x=await Step(journal,x,UploadRecoveryEvent.CreditDenied);
                x=await Step(journal,x,UploadRecoveryEvent.BeginAbort,reason:"ANALYSIS_ABORT");x=await Step(journal,x,UploadRecoveryEvent.FinishAbort);
                var next=await journal.PrepareAsync(p,"ACCOUNT:"+p.OwnerId,"aura-originals",default);
                Require(next.Effects[0].Generation==1&&next.Effects[0].ReserveKey==x.Effects[0].ReserveKey,"No-credit retry fabricated a generation.");
            });
            await Check("Commit requires all durable effects and cannot bypass the journal or replace its plan", async () =>
            {
                var p=Plan(Scope(),new string('a',64));var x=await journal.PrepareAsync(p,"ACCOUNT:"+p.OwnerId,"aura-originals",default);
                await Expect409(()=>Commit(writer,x.Plan),"ERR_REQUEST_IN_PROGRESS");
                await Expect409(()=>Commit(writer,p),"ERR_REQUEST_IN_PROGRESS");
                x=await CompleteEffects(journal,x);
                await Expect409(()=>Commit(writer,x.Plan with{CorrelationId="different-plan"}),"ERR_REQUEST_IN_PROGRESS");
                Require((await journal.ReadAsync(UploadRecovery.Scope(p),default))!.Phase==UploadRecoveryPhase.Pending,"Rejected commit changed phase.");
                Require(await Scalar(source,"SELECT count(*) FROM analysis.analysis_requests WHERE id=$1;",p.BatchId)==0,"Rejected commit inserted a request.");
            });
            UploadRecoveryRecord acceptedRecord=null!;
            await Check("Journal acceptance and request/image/job/outbox commit atomically and replay original response", async () =>
            {
                var p=Plan(Scope(),new string('a',64));var x=await journal.PrepareAsync(p,"ACCOUNT:"+p.OwnerId,"aura-originals",default);
                x=await CompleteEffects(journal,x);await Commit(writer,x.Plan);
                acceptedRecord=(await journal.ReadAsync(UploadRecovery.Scope(p),default))!;
                Require(acceptedRecord.Phase==UploadRecoveryPhase.Accepted&&acceptedRecord.Revision==x.Revision+1,"Acceptance checkpoint missing.");
                Require(await Scalar(source,"SELECT count(*) FROM analysis.outbox_events WHERE analysis_request_id=$1;",p.BatchId)==2,"Outbox commit missing.");
                await using var replay=await writer.BeginAsync(UploadRecovery.Scope(p),p.Fingerprint,default);
                Require(replay.State==IdempotencyState.Replay&&replay.StoredResponse!.CreatedAt==p.Response.CreatedAt,"Original response was not replayed.");
                var unchanged=await Step(journal,acceptedRecord,UploadRecoveryEvent.BeginAbort,reason:"ANALYSIS_ABORT");
                Require(unchanged.Phase==UploadRecoveryPhase.Accepted&&unchanged.Revision==acceptedRecord.Revision,"Accepted work authorized cleanup.");
            });
            await Check("Failure after the acceptance fence rolls back journal and accepted rows together", async () =>
            {
                var p=Plan(Scope(),new string('a',64));
                p=p with{Outbox=p.Outbox.Select((o,i)=>i==0?o with{PayloadJson="{invalid-json}"}:o).ToArray()};
                var x=await journal.PrepareAsync(p,"ACCOUNT:"+p.OwnerId,"aura-originals",default);x=await CompleteEffects(journal,x);
                try{await Commit(writer,x.Plan);throw new InvalidOperationException("Bad JSON unexpectedly committed.");}catch(PostgresException ex) when(ex.SqlState=="22P02"){}
                var after=(await journal.ReadAsync(UploadRecovery.Scope(p),default))!;
                Require(after.Phase==UploadRecoveryPhase.Pending&&after.Revision==x.Revision,"Failed transaction retained ACCEPTED.");
                Require(await Scalar(source,"SELECT count(*) FROM analysis.analysis_requests WHERE id=$1;",p.BatchId)==0,"Failed transaction left accepted rows.");
            });
            await Check("Abort fence rejects a late commit even after all effects were acknowledged", async () =>
            {
                var p=Plan(Scope(),new string('a',64));var x=await journal.PrepareAsync(p,"ACCOUNT:"+p.OwnerId,"aura-originals",default);x=await CompleteEffects(journal,x);
                x=await Step(journal,x,UploadRecoveryEvent.BeginAbort,reason:"STORAGE_ABORT");
                await Expect409(()=>Commit(writer,x.Plan),"ERR_REQUEST_IN_PROGRESS");
                Require((await journal.ReadAsync(UploadRecovery.Scope(p),default))!.Phase==UploadRecoveryPhase.Aborting,"Late commit overrode abort.");
            });
            await Check("Fifteen-minute scan only returns pending candidates and never releases or changes state", async () =>
            {
                var pending=(await journal.ReadAsync(scope,default))!;
                await Execute(source,"UPDATE analysis.upload_recovery_attempts SET updated_at=now()-interval '20 minutes' WHERE id=$1 OR id=$2;",pending.AttemptId,acceptedRecord.AttemptId);
                var before=(await journal.ReadAsync(scope,default))!;
                var due=await journal.FindDueAsync(100,default);
                Require(due.Any(x=>x.AttemptId==pending.AttemptId)&&due.All(x=>x.AttemptId!=acceptedRecord.AttemptId),"Due scan included accepted work or omitted pending work.");
                var after=(await journal.ReadAsync(scope,default))!;
                Require(after.Revision==before.Revision&&after.Phase==before.Phase,"Due scan changed lifecycle state.");
            });
            await Check("Runtime cannot rewrite frozen bindings/plans, delete recovery evidence or run DDL", async () =>
            {
                foreach(var sql in new[]{"UPDATE analysis.upload_recovery_operations SET owner_id=owner_id;","UPDATE analysis.upload_recovery_attempts SET plan=plan;",
                    "DELETE FROM analysis.upload_recovery_operations WHERE false;","DELETE FROM analysis.upload_recovery_attempts WHERE false;","CREATE TABLE analysis.denied_recovery_ddl(id integer);"})
                {try{await Execute(source,sql);throw new InvalidOperationException("Runtime permission unexpectedly granted.");}catch(PostgresException ex) when(ex.SqlState=="42501") {}}
            });
            await Check("Corrupt checkpoint fails closed and caller cancellation propagates", async () =>
            {
                var p=Plan(Scope(),new string('a',64));var x=await journal.PrepareAsync(p,"ACCOUNT:"+p.OwnerId,"aura-originals",default);
                await Execute(source,"UPDATE analysis.upload_recovery_attempts SET effects='[{}]'::jsonb WHERE id=$1;",x.AttemptId);
                try{await journal.ReadAsync(UploadRecovery.Scope(p),default);throw new InvalidOperationException("Corrupt checkpoint accepted.");}catch(UploadFailure ex) when(ex.Status==503){}
                using var canceled=new CancellationTokenSource();canceled.Cancel();
                try{await journal.ReadAsync(scope,canceled.Token);throw new InvalidOperationException("Cancellation ignored.");}catch(OperationCanceledException){}
            });
            await Check("Readiness validates the actual runtime role, ten tables and restricted journal grants",async()=>
            {
                var readiness=new AURA.Analysis.Api.Http.AnalysisReadinessChecks(source,null!);
                Require(await readiness.DatabaseAsync(default),"Database readiness did not confirm the tested schema/grants.");
            });
            Console.WriteLine($"RESULT: {_passed}/13 PostgreSQL recovery groups passed with aura_analysis_app.");
            return 0;
        }
        catch(Exception ex)
        {
            var code=ex is PostgresException pg ? "; SQLSTATE="+pg.SqlState : ex is UploadFailure u ? "; code="+u.Code : "";
            Console.Error.WriteLine($"STOP: {_current}; {ex.GetType().Name}{code}. Test database retained by runner on failure.");
            return 1;
        }
    }
    private static async Task Execute(NpgsqlDataSource source,string sql,params object[] values)
    { await using var cmd=source.CreateCommand(sql);foreach(var v in values)cmd.Parameters.AddWithValue(v);await cmd.ExecuteNonQueryAsync(); }
    private static async Task Expect409<T>(Func<Task<T>> action,string code)
    {try{await action();}catch(UploadFailure ex) when(ex.Status==409&&ex.Code==code){return;}throw new InvalidOperationException("Expected scoped conflict.");}
    private static async Task Expect409(Func<Task> action,string code)
    {try{await action();}catch(UploadFailure ex) when(ex.Status==409&&ex.Code==code){return;}throw new InvalidOperationException("Expected scoped conflict.");}
    private static Task<UploadRecoveryRecord> Step(PostgresUploadRecoveryRepository journal,UploadRecoveryRecord r,UploadRecoveryEvent action,Guid? reservation=null,string? reason=null)
        =>journal.RecordAsync(UploadRecovery.Scope(r.Plan),r.AttemptId,r.Revision,action,r.Plan.Images[0].AnalysisId,reservation,reason);
    private static async Task<UploadRecoveryRecord> CompleteEffects(PostgresUploadRecoveryRepository journal,UploadRecoveryRecord r)
    { r=await Step(journal,r,UploadRecoveryEvent.ReserveIntent);r=await Step(journal,r,UploadRecoveryEvent.Reserved,Guid.NewGuid());r=await Step(journal,r,UploadRecoveryEvent.BlobIntent);return await Step(journal,r,UploadRecoveryEvent.BlobStored); }
    private static async Task Commit(PostgresUploadRepository writer,BatchPlan plan)
    {await using var session=await writer.BeginAsync(UploadRecovery.Scope(plan),plan.Fingerprint,default);await session.CommitAsync(plan,default);}
}
