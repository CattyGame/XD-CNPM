using AURA.Analysis.Api.Upload;
using Npgsql;

namespace AURA.Analysis.Api.Http;

public interface IAnalysisReadinessChecks
{
    Task<bool> DatabaseAsync(CancellationToken ct);
    Task<bool> StorageAsync(CancellationToken ct);
}

public sealed class AnalysisReadinessChecks(NpgsqlDataSource source,IPrivateFileStorage storage):IAnalysisReadinessChecks
{
    private static readonly string[] Tables=["analysis_requests","images","jobs","analysis_results","outbox_events",
        "annotation_refs","doctor_reviews","audit_logs","upload_recovery_operations","upload_recovery_attempts"];
    public async Task<bool> DatabaseAsync(CancellationToken ct)
    {
        await using var c=await source.OpenConnectionAsync(ct);
        await using(var command=new NpgsqlCommand("SELECT current_user, count(*) FILTER (WHERE to_regclass('analysis.' || name) IS NOT NULL) FROM unnest($1::text[]) AS name;",c))
        {
            command.Parameters.AddWithValue(Tables);
            await using var reader=await command.ExecuteReaderAsync(ct);
            if(!await reader.ReadAsync(ct)||reader.GetString(0)!="aura_analysis_app"||reader.GetInt64(1)!=Tables.Length)return false;
        }
        await using(var read=new NpgsqlCommand("SELECT bool_and(has_table_privilege(current_user,'analysis.' || name,'SELECT')) FROM unnest($1::text[]) AS name;",c))
        {
            read.Parameters.AddWithValue(Tables);
            if(await read.ExecuteScalarAsync(ct) is not true)return false;
        }
        await using var privileges=new NpgsqlCommand("""
            SELECT has_table_privilege(current_user,'analysis.upload_recovery_operations','SELECT')
               AND has_table_privilege(current_user,'analysis.upload_recovery_operations','INSERT')
               AND NOT has_table_privilege(current_user,'analysis.upload_recovery_operations','UPDATE')
               AND NOT has_table_privilege(current_user,'analysis.upload_recovery_operations','DELETE')
               AND NOT has_table_privilege(current_user,'analysis.upload_recovery_attempts','UPDATE')
               AND NOT has_table_privilege(current_user,'analysis.upload_recovery_attempts','DELETE')
               AND has_table_privilege(current_user,'analysis.upload_recovery_attempts','INSERT')
               AND has_column_privilege(current_user,'analysis.upload_recovery_attempts','revision','UPDATE')
               AND has_column_privilege(current_user,'analysis.upload_recovery_attempts','abort_reason','UPDATE')
               AND has_column_privilege(current_user,'analysis.upload_recovery_attempts','updated_at','UPDATE')
               AND has_column_privilege(current_user,'analysis.upload_recovery_attempts','effects','UPDATE')
               AND has_column_privilege(current_user,'analysis.upload_recovery_attempts','phase','UPDATE')
               AND has_table_privilege(current_user,'analysis.analysis_requests','INSERT')
               AND has_table_privilege(current_user,'analysis.images','INSERT')
               AND has_table_privilege(current_user,'analysis.jobs','INSERT')
               AND has_table_privilege(current_user,'analysis.outbox_events','INSERT');
            """,c);
        return await privileges.ExecuteScalarAsync(ct) is true;
    }
    public Task<bool> StorageAsync(CancellationToken ct)=>storage is IPrivateStorageReadiness probe
        ?probe.CheckPrivateContainersAsync(ct):Task.FromResult(false);
}

public static class AnalysisReadinessEndpoint
{
    public static void MapAnalysisReadiness(this WebApplication app,bool enabled)
    {
        app.MapGet("/health/ready",(Func<HttpContext,Task<IResult>>)(async c=>
        {
            c.Response.Headers.CacheControl="no-store";
            if(!enabled)return Results.Json(new{status="disabled"},statusCode:503);
            try
            {
                using var deadline=CancellationTokenSource.CreateLinkedTokenSource(c.RequestAborted);
                deadline.CancelAfter(TimeSpan.FromSeconds(5));
                var checks=c.RequestServices.GetRequiredService<IAnalysisReadinessChecks>();
                var results=await Task.WhenAll(checks.DatabaseAsync(deadline.Token),checks.StorageAsync(deadline.Token));
                var ready=results.All(x=>x);
                return Results.Json(new{status=ready?"ready":"not_ready"},statusCode:ready?200:503);
            }
            catch(OperationCanceledException) when(c.RequestAborted.IsCancellationRequested){throw;}
            catch(Exception){return Results.Json(new{status="not_ready"},statusCode:503);}
        }));
    }
}
