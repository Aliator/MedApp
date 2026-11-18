using MedApp.Domain.Pathways.PatientPathways;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Pathways.PatientPathways;

public sealed class PatientPathwayNoteConfiguration : IEntityTypeConfiguration<PatientPathwayNote>
{
    public void Configure(EntityTypeBuilder<PatientPathwayNote> builder)
    {
        builder.ToTable("PatientPathwayNotes");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Text)
            .IsRequired()
            .HasMaxLength(PatientPathwayNote.MaxTextLength);

        builder.Property(x => x.CreatedAtUtc).IsRequired();

        builder.Property(x => x.Version).IsRequired();

        builder.Property(x => x.RowVersion).IsRowVersion();

        // Deleting is soft: the note stays, with who deleted it, when and why.
        builder.Property(x => x.DeletionDetail).HasMaxLength(PatientPathwayNote.MaxDeletionDetailLength);
        builder.Ignore(x => x.IsDeleted);

        // A note goes with its pathway; its versions (no foreign key) and audit entries stay.
        builder.HasOne(x => x.PatientPathway)
            .WithMany(x => x.Notes)
            .HasForeignKey(x => x.PatientPathwayId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.PatientPathwayId, x.CreatedAtUtc });
    }
}
