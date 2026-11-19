using MedApp.Domain.Teams;
using MedApp.Domain.Appointments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Appointments;

public sealed class AppointmentEventConfiguration : IEntityTypeConfiguration<AppointmentEvent>
{
    public void Configure(EntityTypeBuilder<AppointmentEvent> builder)
    {
        builder.ToTable("AppointmentEvents");

        // Its team (the team walls, decision 200): set when it's made, never edited by hand. A team anything has is
        // archived, never deleted.
        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(x => x.TeamId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => x.TeamId);

        builder.HasKey(x => x.Id);

        // No foreign keys: the history outlives deleted appointments, pathways, stages and accounts.
        builder.Property(x => x.PathwayTitle)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.StageName)
            .HasMaxLength(100);

        builder.Property(x => x.Username)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.StaffUsername)
            .HasMaxLength(256);

        builder.Property(x => x.Action)
            .IsRequired();

        builder.Property(x => x.AtUtc)
            .IsRequired();

        builder.Property(x => x.Details)
            .IsRequired();

        builder.HasIndex(x => new { x.AppointmentId, x.AtUtc });
        // A patient's timeline: their entries, newest first.
        builder.HasIndex(x => new { x.PatientId, x.AtUtc });
    }
}
