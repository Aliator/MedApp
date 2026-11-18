using MedApp.Domain.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Jobs;

public sealed class ScheduledJobConfiguration : IEntityTypeConfiguration<ScheduledJob>
{
    public void Configure(EntityTypeBuilder<ScheduledJob> builder)
    {
        builder.ToTable("ScheduledJobs");

        // One row per job, keyed by its code name; the lease is taken with one UPDATE on this key.
        builder.HasKey(job => job.Name);
        builder.Property(job => job.Name)
            .HasMaxLength(ScheduledJob.MaxNameLength)
            .IsUnicode(false)
            .ValueGeneratedNever();

        builder.Property(job => job.NextRunUtc).IsRequired();

        builder.Property(job => job.LeaseHolder)
            .HasMaxLength(ScheduledJob.MaxLeaseHolderLength);

        builder.Property(job => job.LastErrorType)
            .HasMaxLength(ScheduledJob.MaxErrorTypeLength);

        builder.Property(job => job.ConsecutiveFailures).IsRequired();
    }
}
