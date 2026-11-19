using MedApp.Domain.Teams;
using MedApp.Domain.Pathways.PatientPathways;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Pathways.PatientPathways;

public sealed class PatientPathwayAuditEntryConfiguration : IEntityTypeConfiguration<PatientPathwayAuditEntry>
{
    public void Configure(EntityTypeBuilder<PatientPathwayAuditEntry> builder)
    {
        builder.ToTable("PatientPathwayAuditEntries");

        // Its team (the team walls, decision 200): set when it's made, never edited by hand. A team anything has is
        // archived, never deleted.
        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(x => x.TeamId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => x.TeamId);

        builder.HasKey(x => x.Id);

        // No foreign keys: the history outlives deleted pathways, stages and accounts.
        builder.Property(x => x.PathwayTitle)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.StageName)
            .HasMaxLength(100);

        builder.Property(x => x.Username)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.Action)
            .IsRequired();

        builder.Property(x => x.AtUtc)
            .IsRequired();

        builder.Property(x => x.Details)
            .IsRequired();

        builder.HasIndex(x => new { x.PatientPathwayId, x.AtUtc });
        // A patient's timeline: their entries, newest first.
        builder.HasIndex(x => new { x.PatientId, x.AtUtc });
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.AtUtc);
    }
}
