using Microsoft.EntityFrameworkCore;
using Aura.Worker.Analysis.Data.Entities;
using Aura.Worker.Analysis.Domain;

namespace Aura.Worker.Analysis.Data.Repositories;

public interface IAnalysisRepository
{
    Task<AnalysisRecord?> GetAnalysisAsync(Guid analysisId, CancellationToken cancellationToken);
    Task CommitSuccessAsync(Guid analysisId, Guid jobId, string resultJson, OutboxRecord outboxEvent, CancellationToken cancellationToken);
    Task CommitFailureAsync(Guid analysisId, Guid jobId, string status, string errorJson, OutboxRecord outboxEvent, CancellationToken cancellationToken);
    Task ScheduleRetryAsync(Guid analysisId, Guid jobId, int nextAttempt, TimeSpan delay, OutboxRecord retryJobCommand, CancellationToken cancellationToken);
}

public class AnalysisRepository : IAnalysisRepository
{
    private readonly WorkerDbContext _db;

    public AnalysisRepository(WorkerDbContext db)
    {
        _db = db;
    }

    public async Task<AnalysisRecord?> GetAnalysisAsync(Guid analysisId, CancellationToken cancellationToken)
    {
        return await _db.Analyses.FirstOrDefaultAsync(a => a.AnalysisId == analysisId, cancellationToken);
    }

    public async Task CommitSuccessAsync(Guid analysisId, Guid jobId, string resultJson, OutboxRecord outboxEvent, CancellationToken cancellationToken)
    {
        using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        var analysis = await _db.Analyses.FirstOrDefaultAsync(a => a.AnalysisId == analysisId, cancellationToken);
        if (analysis != null)
        {
            analysis.ProcessingStatus = AnalysisStatus.Completed;
            analysis.ReviewStatus = ReviewStatus.Pending;
            analysis.ResultJson = resultJson;
            analysis.UpdatedAt = DateTime.UtcNow;
        }

        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.JobId == jobId, cancellationToken);
        if (job != null)
        {
            job.Status = JobStatus.Succeeded;
            job.UpdatedAt = DateTime.UtcNow;
        }

        _db.OutboxMessages.Add(outboxEvent);

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    public async Task CommitFailureAsync(Guid analysisId, Guid jobId, string status, string errorJson, OutboxRecord outboxEvent, CancellationToken cancellationToken)
    {
        using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        var analysis = await _db.Analyses.FirstOrDefaultAsync(a => a.AnalysisId == analysisId, cancellationToken);
        if (analysis != null)
        {
            analysis.ProcessingStatus = status;
            analysis.ErrorJson = errorJson;
            analysis.UpdatedAt = DateTime.UtcNow;
        }

        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.JobId == jobId, cancellationToken);
        if (job != null)
        {
            job.Status = JobStatus.Failed;
            job.UpdatedAt = DateTime.UtcNow;
        }

        _db.OutboxMessages.Add(outboxEvent);

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    public async Task ScheduleRetryAsync(Guid analysisId, Guid jobId, int nextAttempt, TimeSpan delay, OutboxRecord retryJobCommand, CancellationToken cancellationToken)
    {
        using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        var analysis = await _db.Analyses.FirstOrDefaultAsync(a => a.AnalysisId == analysisId, cancellationToken);
        if (analysis != null)
        {
            analysis.ProcessingStatus = AnalysisStatus.RetryReady;
            analysis.UpdatedAt = DateTime.UtcNow;
        }

        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.JobId == jobId, cancellationToken);
        if (job != null)
        {
            job.Status = JobStatus.RetryReady;
            job.UpdatedAt = DateTime.UtcNow;
        }

        retryJobCommand.AvailableAt = DateTime.UtcNow.Add(delay);
        _db.OutboxMessages.Add(retryJobCommand);

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }
}
