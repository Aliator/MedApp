using MedApp.Domain.Calendar;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Calendar;

public sealed class MeetingAttendeeConfiguration : IEntityTypeConfiguration<MeetingAttendee>
{
    public void Configure(EntityTypeBuilder<MeetingAttendee> builder)
    {
        builder.ToTable("MeetingAttendees");

        // Each member of staff once per meeting. No foreign key to the users table for StaffUserId.
        builder.HasKey(x => new { x.MeetingId, x.StaffUserId });

        builder.HasOne(x => x.Meeting)
            .WithMany(x => x.Attendees)
            .HasForeignKey(x => x.MeetingId)
            .OnDelete(DeleteBehavior.Cascade);

        // A person's meetings.
        builder.HasIndex(x => x.StaffUserId);
    }
}
