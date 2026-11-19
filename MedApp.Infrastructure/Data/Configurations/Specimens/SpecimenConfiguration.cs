using MedApp.Domain.Teams;
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

        // Its team (the team walls, decision 200): set when it's made, never edited by hand. A team anything has is
        // archived, never deleted.
        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(x => x.TeamId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => x.TeamId);

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

        // MC14: where it came from, the clinic that asked for it, and the test (archived, never deleted, once used).
        builder.Property(x => x.Source).IsRequired();
        builder.HasOne(x => x.RequestingTeam)
            .WithMany()
            .HasForeignKey(x => x.RequestingTeamId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => x.RequestingTeamId);

        builder.HasOne(x => x.LabTest)
            .WithMany()
            .HasForeignKey(x => x.LabTestId)
            .OnDelete(DeleteBehavior.Restrict);

        // The lab case it's in (MC15): a case entered in error lets its samples go first.
        builder.HasOne(x => x.LabCase)
            .WithMany(c => c.Specimens)
            .HasForeignKey(x => x.LabCaseId)
            .OnDelete(DeleteBehavior.Restrict);

        // The hospital and its request (MC18): a hospital that sent requests is archived, never deleted.
        builder.HasOne(x => x.Hospital)
            .WithMany()
            .HasForeignKey(x => x.HospitalId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.HospitalRequest)
            .WithMany()
            .HasForeignKey(x => x.HospitalRequestId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => x.HospitalRequestId).HasFilter("[HospitalRequestId] IS NOT NULL");
        builder.HasIndex(x => x.HospitalId).HasFilter("[HospitalId] IS NOT NULL");

        // The samples' tabs: a status's samples, oldest sent first; and a pathway's, a patient's and a case's.
        builder.HasIndex(x => new { x.Status, x.SentAtUtc });
        builder.HasIndex(x => x.LabCaseId);
        builder.HasIndex(x => x.PathwayId);
        builder.HasIndex(x => x.PatientId);

        // Whether a file is a specimen's report (it can't be deleted on its own).
        builder.HasIndex(x => x.ReportAttachmentId)
            .HasFilter("[ReportAttachmentId] IS NOT NULL");
    }
}
