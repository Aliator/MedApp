using MedApp.Domain.Referrals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Referrals;

public sealed class ReferralConfiguration : IEntityTypeConfiguration<Referral>
{
    public void Configure(EntityTypeBuilder<Referral> builder)
    {
        builder.ToTable("Referrals");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ReferrerPractice)
            .IsRequired()
            .HasMaxLength(Referral.MaxPracticeLength);

        builder.Property(x => x.ReferrerClinician)
            .HasMaxLength(Referral.MaxClinicianLength);

        builder.Property(x => x.ReferrerOdsCode)
            .HasMaxLength(Referral.MaxOdsCodeLength)
            .IsUnicode(false);

        builder.Property(x => x.Reason)
            .IsRequired()
            .HasMaxLength(Referral.MaxReasonLength);

        builder.Property(x => x.RegisteredByUsername)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.DecidedByUsername)
            .HasMaxLength(256);

        builder.Property(x => x.DecisionDetail)
            .HasMaxLength(Referral.MaxDetailLength);

        builder.Property(x => x.RedirectedTo)
            .HasMaxLength(Referral.MaxRedirectedToLength);

        builder.Property(x => x.Version)
            .IsRequired();

        // Two people changing one referral at once: the later save is refused (409) instead of overwriting the earlier.
        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        // A patient with referrals can't be deleted (DeletePatientHandler), and a specialty with any is archived, not
        // deleted: the foreign keys refuse too, should either race the check.
        builder.HasOne(x => x.Patient)
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Specialty)
            .WithMany()
            .HasForeignKey(x => x.SpecialtyId)
            .OnDelete(DeleteBehavior.Restrict);

        // The pathway it was accepted into. Deleting that pathway clears this in the same save
        // (PatientPathwayRepository.DeleteAsync); the referral and its history stay.
        builder.HasOne(x => x.Pathway)
            .WithMany()
            .HasForeignKey(x => x.PathwayId)
            .OnDelete(DeleteBehavior.NoAction);

        // One referral per pathway.
        builder.HasIndex(x => x.PathwayId)
            .IsUnique()
            .HasFilter("[PathwayId] IS NOT NULL");

        // The inbox: a status's referrals in a specialty, oldest first; and a patient's referrals.
        builder.HasIndex(x => new { x.Status, x.SpecialtyId, x.ReceivedOn });
        builder.HasIndex(x => x.PatientId);
    }
}
