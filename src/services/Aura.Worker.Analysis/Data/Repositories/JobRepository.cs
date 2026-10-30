using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Aura.Worker.Analysis.Data.Entities;
using Aura.Worker.Analysis.Domain;

namespace Aura.Worker.Analysis.Data.Repositories;

public interface IJobRepository
{
    Task<JobRecord?> GetJobAsync(Guid jobId, CancellationToken cancellationToken);
    Task<bool> TryAcquireLeaseAsync(Guid jobId, string workerId, TimeSpan leaseDuration, CancellationToken cancellationToken);
    Task<bool> RenewLeaseAsync(Guid jobId, string workerId, TimeSpan extension, CancellationToken cancellationToken);
    Task RecordCallIntentAsync(Guid jobId, CancellationToken cancellationToken);
    Task UpdateJobStatusAsync(Guid jobId, string status, CancellationToken cancellationToken);
    Task<JobRecord> CreateNextAttemptJobAsync(JobRecord previousJob, int nextAttempt, CancellationToken cancellationToken);
}

public class JobRepository : IJobRepository
{
    private readonly WorkerDbContext _db;
    private readonly ILogger<JobRepository> _logger;

    public JobRepository(WorkerDbContext db, ILogger<JobRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<JobRecord?> GetJobAsync(Guid jobId, CancellationToken cancellationToken)
    {
        return await _db.Jobs.FirstOrDefaultAsync(j => j.JobId == jobId, cancellationToken);
    }

    public async Task<bool> TryAcquireLeaseAsync(Guid jobId, string workerId, TimeSpan leaseDuration, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.JobId == jobId, cancellationToken);
        if (job == null) return false;

        // Can acquire if lease expired or already owned
        if (job.LeaseExpiresAt == null || job.LeaseExpiresAt < now || job.LeaseOwner == workerId)
        {
            job.LeaseOwner = workerId;
            job.LeaseExpiresAt = now.Add(leaseDuration);
            job.Status = JobStatus.Running;
            job.UpdatedAt = now;
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }

        _logger.LogWarning("Job {JobId} is leased by {Owner} until {Expiry}", jobId, job.LeaseOwner, job.LeaseExpiresAt);
        return false;
    }

    public async Task<bool> RenewLeaseAsync(Guid jobId, string workerId, TimeSpan extension, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.JobId == jobId && j.LeaseOwner == workerId, cancellationToken);
        if (job == null) return false;

        job.LeaseExpiresAt = now.Add(extension);
        job.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task RecordCallIntentAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.JobId == jobId, cancellationToken);
        if (job != null)
        {
            job.IntentRecorded = true;
            job.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task UpdateJobStatusAsync(Guid jobId, string status, CancellationToken cancellationToken)
    {
        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.JobId == jobId, cancellationToken);
        if (job != null)
        {
            job.Status = status;
            job.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<JobRecord> CreateNextAttemptJobAsync(JobRecord previousJob, int nextAttempt, CancellationToken cancellationToken)
    {
        var nextJob = new JobRecord
        {
            JobId = Guid.NewGuid(),
            AnalysisId = previousJob.AnalysisId,
            BatchId = previousJob.BatchId,
            TenantId = previousJob.TenantId,
            ImageFileId = previousJob.ImageFileId,
            AttemptNo = nextAttempt,
            Status = JobStatus.Queued,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Jobs.Add(nextJob);
        await _db.SaveChangesAsync(cancellationToken);
        return nextJob;
    }
}
