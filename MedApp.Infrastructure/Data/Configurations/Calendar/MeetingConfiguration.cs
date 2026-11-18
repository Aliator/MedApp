using MedApp.Domain.Calendar;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Calendar;

public sealed class MeetingConfiguration : IEntityTypeConfiguration<Meeting>
{
    public void Configure(EntityTypeBuilder<Meeting> builder)
    {
        builder.ToTable("Meetings");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Notes)
            .IsRequired()
            .HasMaxLength(500)
            .HasDefaultValue(string.Empty);

        // No foreign key to the users table for OrganiserUserId, as with other staff IDs.
        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        // A meeting owns its attendees.
        builder.HasMany(x => x.Attendees)
            .WithOne(x => x.Meeting)
            .HasForeignKey(x => x.MeetingId)
            .OnDelete(DeleteBehavior.Cascade);

        // An MDT's team: one with any is archived, not deleted (the key refuses, should that race).
        builder.HasOne<MedApp.Domain.Teams.Team>()
            .WithMany()
            .HasForeignKey(x => x.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.StartUtc);
    }
}
