using MedApp.Domain.Referrals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Referrals;

public sealed class ReferralEventConfiguration : IEntityTypeConfiguration<ReferralEvent>
{
    public void Configure(EntityTypeBuilder<ReferralEvent> builder)
    {
        builder.ToTable("ReferralEvents");

        builder.HasKey(x => x.Id);

        // No foreign keys: the history outlives the accounts and the pathway it names.
        builder.Property(x => x.Username)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.Action)
            .IsRequired();

        builder.Property(x => x.AtUtc)
            .IsRequired();

        builder.Property(x => x.Details)
            .IsRequired();

        // A referral's history; and a patient's timeline, newest first, then by ID (the cursor's order).
        builder.HasIndex(x => new { x.ReferralId, x.AtUtc });
        builder.HasIndex(x => new { x.PatientId, x.AtUtc, x.Id });
    }
}
