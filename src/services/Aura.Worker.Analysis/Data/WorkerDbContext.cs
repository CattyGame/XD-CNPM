using Microsoft.EntityFrameworkCore;
using Aura.Worker.Analysis.Data.Entities;

namespace Aura.Worker.Analysis.Data;

public class WorkerDbContext : DbContext
{
    public WorkerDbContext(DbContextOptions<WorkerDbContext> options) : base(options)
    {
    }

    public DbSet<JobRecord> Jobs => Set<JobRecord>();
    public DbSet<AnalysisRecord> Analyses => Set<AnalysisRecord>();
    public DbSet<OutboxRecord> OutboxMessages => Set<OutboxRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<JobRecord>(entity =>
        {
            entity.HasKey(e => e.JobId);
            entity.HasIndex(e => new { e.AnalysisId, e.AttemptNo });
            entity.HasIndex(e => e.Status);
        });

        modelBuilder.Entity<AnalysisRecord>(entity =>
        {
            entity.HasKey(e => e.AnalysisId);
            entity.HasIndex(e => e.BatchId);
            entity.HasIndex(e => e.TenantId);
        });

        modelBuilder.Entity<OutboxRecord>(entity =>
        {
            entity.HasKey(e => e.OutboxId);
            entity.HasIndex(e => e.EventId).IsUnique();
            entity.HasIndex(e => new { e.Status, e.AvailableAt });
        });
    }
}
