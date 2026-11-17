using MedApp.Domain.Procedures.PatientProcedures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Procedures.PatientProcedures;

public sealed class PatientProcedureNoteVersionConfiguration : IEntityTypeConfiguration<PatientProcedureNoteVersion>
{
    public void Configure(EntityTypeBuilder<PatientProcedureNoteVersion> builder)
    {
        builder.ToTable("PatientProcedureNoteVersions");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        // No foreign keys, like the audit log: a note's history outlives its procedure, and is found by its patient.
        builder.Property(x => x.Text)
            .IsRequired()
            .HasMaxLength(PatientProcedureNote.MaxTextLength);

        builder.Property(x => x.Username)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.AtUtc).IsRequired();

        builder.HasIndex(x => new { x.NoteId, x.Version }).IsUnique();
        // A patient's export and anonymisation.
        builder.HasIndex(x => x.PatientId);
    }
}
