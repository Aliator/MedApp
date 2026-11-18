using MedApp.Domain.Consent;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Consent;

public sealed class ConsentFormConfiguration : IEntityTypeConfiguration<ConsentForm>
{
    public void Configure(EntityTypeBuilder<ConsentForm> builder)
    {
        builder.ToTable("ConsentForms");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(ConsentForm.MaxNameLength);
        builder.Property(x => x.FormVersion).IsRequired().HasMaxLength(ConsentForm.MaxFormVersionLength);

        // One form of a name per version (the collation ignores case, as the handlers' check does).
        builder.HasIndex(x => new { x.Name, x.FormVersion }).IsUnique();
    }
}

public sealed class ConsentRecordConfiguration : IEntityTypeConfiguration<ConsentRecord>
{
    public void Configure(EntityTypeBuilder<ConsentRecord> builder)
    {
        builder.ToTable("ConsentRecords");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.FormName).IsRequired().HasMaxLength(ConsentForm.MaxNameLength);
        builder.Property(x => x.FormVersion).IsRequired().HasMaxLength(ConsentForm.MaxFormVersionLength);
        builder.Property(x => x.StatusDetail).HasMaxLength(ConsentRecord.MaxDetailLength);

        builder.Property(x => x.Version).IsRequired();

        // Two people changing one record at once: the later save is refused (409) instead of overwriting the earlier.
        builder.Property(x => x.RowVersion).IsRowVersion();

        // A pathway with consent records can't be deleted (DeletePatientPathwayHandler; nor can a patient with
        // pathways), and a form in use is archived, not deleted: the foreign keys refuse a race too.
        builder.HasOne(x => x.Patient)
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Pathway)
            .WithMany()
            .HasForeignKey(x => x.PathwayId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ConsentForm)
            .WithMany()
            .HasForeignKey(x => x.ConsentFormId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.PathwayId);
        builder.HasIndex(x => x.PatientId);

        // The stage gate: a stage's records.
        builder.HasIndex(x => x.StageId);

        // Whether a file is a consent form's scan (it can't be deleted on its own).
        builder.HasIndex(x => x.ScanAttachmentId)
            .HasFilter("[ScanAttachmentId] IS NOT NULL");
    }
}
