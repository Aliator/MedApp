using MedApp.Domain.Specimens;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Specimens;

public sealed class SpecimenConfiguration : IEntityTypeConfiguration<Specimen>
{
    /// <summary>The SQL sequence specimen numbers come from: one number per specimen, never reused, across every API copy.</summary>
    public const string NumberSequence = "SpecimenNumbers";

    public void Configure(EntityTypeBuilder<Specimen> builder)
    {
        builder.ToTable("Specimens");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Number)
            .IsRequired()
            .HasMaxLength(Specimen.MaxNumberLength)
            .IsUnicode(false);

        builder.HasIndex(x => x.Number).IsUnique();

        builder.Property(x => x.Site).HasMaxLength(Specimen.MaxSiteLength);
        builder.Property(x => x.RequestNotes).HasMaxLength(Specimen.MaxRequestNotesLength);
        builder.Property(x => x.CourierReference).HasMaxLength(Specimen.MaxReferenceLength);
        builder.Property(x => x.LabReference).HasMaxLength(Specimen.MaxReferenceLength);
        builder.Property(x => x.ClosedDetail).HasMaxLength(Specimen.MaxClosedDetailLength);

        builder.Property(x => x.Version).IsRequired();

        // Two people changing one specimen at once: the later save is refused (409) instead of overwriting the earlier.
        builder.Property(x => x.RowVersion).IsRowVersion();

        // A patient or pathway with specimens can't be deleted (DeletePatientHandler, DeletePatientPathwayHandler), and a
        // type or lab in use is archived, not deleted: the foreign keys refuse too, should either race the check.
        builder.HasOne(x => x.Patient)
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Pathway)
            .WithMany()
            .HasForeignKey(x => x.PathwayId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SpecimenType)
            .WithMany()
            .HasForeignKey(x => x.SpecimenTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Laboratory)
            .WithMany()
            .HasForeignKey(x => x.LaboratoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // The lab's worklist: a status's specimens at a lab, oldest sent first; and a pathway's and a patient's.
        builder.HasIndex(x => new { x.Status, x.LaboratoryId, x.SentAtUtc });
        builder.HasIndex(x => x.PathwayId);
        builder.HasIndex(x => x.PatientId);

        // Whether a file is a specimen's report (it can't be deleted on its own).
        builder.HasIndex(x => x.ReportAttachmentId)
            .HasFilter("[ReportAttachmentId] IS NOT NULL");
    }
}
