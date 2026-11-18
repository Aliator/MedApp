using MedApp.Domain.Pathways.PatientPathways;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Pathways.PatientPathways;

public sealed class PatientPathwayNoteVersionConfiguration : IEntityTypeConfiguration<PatientPathwayNoteVersion>
{
    public void Configure(EntityTypeBuilder<PatientPathwayNoteVersion> builder)
    {
        builder.ToTable("PatientPathwayNoteVersions");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        // No foreign keys, like the audit log: a note's history outlives its pathway, and is found by its patient.
        builder.Property(x => x.Text)
            .IsRequired()
            .HasMaxLength(PatientPathwayNote.MaxTextLength);

        builder.Property(x => x.Username)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.AtUtc).IsRequired();

        builder.HasIndex(x => new { x.NoteId, x.Version }).IsUnique();
        // A patient's export and anonymisation.
        builder.HasIndex(x => x.PatientId);
    }
}
