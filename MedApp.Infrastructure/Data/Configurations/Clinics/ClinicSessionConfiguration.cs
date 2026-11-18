using MedApp.Application.Clinics.Common;
using MedApp.Domain.Clinics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Clinics;

public sealed class ClinicSessionConfiguration : IEntityTypeConfiguration<ClinicSession>
{
    /// <summary>The unique index that lets a pattern make at most one session on a date (decision 125).</summary>
    public const string PatternDateIndex = "IX_ClinicSessions_PatternId_Date";

    public void Configure(EntityTypeBuilder<ClinicSession> builder)
    {
        builder.ToTable("ClinicSessions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).IsRequired();

        builder.Property(x => x.CancelReason)
            .HasMaxLength(ClinicLimits.MaxCancelReasonLength);

        // Two people changing one session at once: the later save is refused (409).
        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        // A room or specialty a session names is archived, never deleted.
        builder.HasOne(x => x.Room)
            .WithMany()
            .HasForeignKey(x => x.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Specialty)
            .WithMany()
            .HasForeignKey(x => x.SpecialtyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ClinicPattern>()
            .WithMany()
            .HasForeignKey(x => x.PatternId)
            .OnDelete(DeleteBehavior.Restrict);

        // A calendar's sessions, and a room's, over a range of dates.
        builder.HasIndex(x => new { x.StaffUserId, x.StartUtc });
        builder.HasIndex(x => new { x.RoomId, x.StartUtc });
        builder.HasIndex(x => new { x.Date, x.StartUtc });

        builder.HasIndex(x => new { x.PatternId, x.Date }, PatternDateIndex)
            .IsUnique()
            .HasFilter("[PatternId] IS NOT NULL");
    }
}
