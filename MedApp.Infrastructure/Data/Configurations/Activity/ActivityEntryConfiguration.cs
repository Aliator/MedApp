using MedApp.Domain.Activity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Activity;

public sealed class ActivityEntryConfiguration : IEntityTypeConfiguration<ActivityEntry>
{
    public void Configure(EntityTypeBuilder<ActivityEntry> builder)
    {
        builder.ToTable("ActivityEntries");

        builder.HasKey(x => x.Id);

        // No foreign keys: the log outlives deleted patients, procedures and accounts.
        builder.Property(x => x.Action)
            .IsRequired();

        builder.Property(x => x.AtUtc)
            .IsRequired();

        builder.Property(x => x.Username)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.IpAddress)
            .HasMaxLength(45);

        builder.Property(x => x.TargetUsername)
            .HasMaxLength(256);

        builder.Property(x => x.Details)
            .IsRequired();

        // The log page (newest first), a patient's access report, and the check for a recent view by the same person.
        builder.HasIndex(x => x.AtUtc);
        builder.HasIndex(x => new { x.PatientId, x.AtUtc });
        builder.HasIndex(x => new { x.UserId, x.Action, x.PatientId, x.AtUtc });
    }
}
