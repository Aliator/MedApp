using MedApp.Domain.Procedures.PatientProcedures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Procedures.PatientProcedures;

public sealed class PatientProcedureAuditEntryConfiguration : IEntityTypeConfiguration<PatientProcedureAuditEntry>
{
    public void Configure(EntityTypeBuilder<PatientProcedureAuditEntry> builder)
    {
        builder.ToTable("PatientProcedureAuditEntries");

        builder.HasKey(x => x.Id);

        // No foreign keys: the history outlives deleted procedures, stages and accounts.
        builder.Property(x => x.ProcedureTitle)
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

        builder.HasIndex(x => new { x.PatientProcedureId, x.AtUtc });
        builder.HasIndex(x => x.PatientId);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.AtUtc);
    }
}
