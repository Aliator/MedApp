using MedApp.Domain.Mdt;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Mdt;

public sealed class MdtItemAttendeeConfiguration : IEntityTypeConfiguration<MdtItemAttendee>
{
    public void Configure(EntityTypeBuilder<MdtItemAttendee> builder)
    {
        builder.ToTable("MdtItemAttendees");

        // Each person once; no foreign key to the users table, as with other staff IDs.
        builder.HasKey(x => new { x.MdtItemId, x.StaffUserId });

        builder.Property(x => x.Username)
            .IsRequired()
            .HasMaxLength(256);
    }
}
