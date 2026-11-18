using MedApp.Domain.Referrals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Referrals;

public sealed class ReferralClockPauseConfiguration : IEntityTypeConfiguration<ReferralClockPause>
{
    /// <summary>One open pause per referral: two pauses at once can't both be saved.</summary>
    public const string OpenPerReferralIndex = "IX_ReferralClockPauses_ReferralId_Open";

    public void Configure(EntityTypeBuilder<ReferralClockPause> builder)
    {
        builder.ToTable("ReferralClockPauses");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.PausedByUsername)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.ResumedByUsername)
            .HasMaxLength(256);

        builder.Property(x => x.Detail)
            .HasMaxLength(ReferralClockPause.MaxDetailLength);

        builder.Ignore(x => x.Days);

        // A referral with pauses is never deleted (referrals aren't), and a patient with referrals can't be.
        builder.HasOne(x => x.Referral)
            .WithMany(x => x.Pauses)
            .HasForeignKey(x => x.ReferralId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ReferralId)
            .IsUnique()
            .HasFilter("[ResumedAtUtc] IS NULL")
            .HasDatabaseName(OpenPerReferralIndex);

        // The export and anonymisation find a patient's pauses directly.
        builder.HasIndex(x => x.PatientId);
    }
}
