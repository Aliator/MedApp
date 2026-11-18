using MedApp.Application.Appointments.Common;
using MedApp.Domain.Appointments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Appointments;

public sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("Appointments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.Notes)
            .IsRequired()
            .HasMaxLength(2000)
            .HasDefaultValue(string.Empty);

        builder.Property(x => x.CancelReason)
            .HasMaxLength(500);

        builder.Property(x => x.DnaFollowUpDetail)
            .HasMaxLength(500);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.LastUpdated)
            .IsRequired();

        // Two people changing one appointment at once: the later save is refused (409) instead of overwriting the
        // earlier one (ConcurrencyExtensions.SaveChangesOrConflictAsync).
        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        // A patient with appointments can't be deleted (DeletePatientHandler), so no cascade.
        builder.HasOne(x => x.Patient)
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.NoAction);

        // Deleting a pathway detaches its appointments in the same save (PatientPathwayRepository.DeleteAsync).
        builder.HasOne(x => x.Pathway)
            .WithMany()
            .HasForeignKey(x => x.PathwayId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.Stage)
            .WithMany()
            .HasForeignKey(x => x.StageId)
            .OnDelete(DeleteBehavior.NoAction);

        // A session or room an appointment names is never deleted (sessions are cancelled, rooms archived).
        builder.HasOne<MedApp.Domain.Clinics.ClinicSession>()
            .WithMany()
            .HasForeignKey(x => x.ClinicSessionId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<MedApp.Domain.Sites.Room>()
            .WithMany()
            .HasForeignKey(x => x.RoomId)
            .OnDelete(DeleteBehavior.NoAction);

        // No foreign keys to the users table (staff, creator, booker), as with pathway assignments.

        // A calendar's appointments in a range of dates.
        builder.HasIndex(x => new { x.StaffUserId, x.StartUtc });
        // What requires an appointment (the Requires Appointment list), and a patient's appointments.
        builder.HasIndex(x => new { x.Status, x.CreatedAtUtc });
        builder.HasIndex(x => x.PatientId);
        builder.HasIndex(x => x.PathwayId);
        builder.HasIndex(x => x.StageId);
        // One open (Requested or Booked) appointment per stage: checked in CreateAppointmentHandler, and enforced here
        // too, so two requests at once can't both add one. A violation is recognised by the index's name
        // (AppointmentRepository). The filter's numbers are AppointmentWorkflow.OpenStatuses.
        builder.HasIndex(x => x.StageId, OpenPerStageIndex)
            .IsUnique()
            .HasFilter(OpenPerStageFilter);
        // One patient per slot of a clinic session (decision 124): checked under the clinician's calendar lock, and
        // enforced here too. Slots have fixed starts, so one appointment on the calendar per session and start.
        builder.HasIndex(x => new { x.ClinicSessionId, x.StartUtc }, SessionSlotIndex)
            .IsUnique()
            .HasFilter(SessionSlotFilter);
        // A room's appointments over a range (rooms are clash-checked).
        builder.HasIndex(x => new { x.RoomId, x.StartUtc });
    }

    /// <summary>The unique index that allows one appointment on the calendar per slot of a clinic session.</summary>
    public const string SessionSlotIndex = "IX_Appointments_ClinicSessionId_StartUtc_OnCalendar";

    private static readonly string SessionSlotFilter =
        $"[ClinicSessionId] IS NOT NULL AND [Status] IN ({(int)AppointmentStatus.Booked}, {(int)AppointmentStatus.Attended}, {(int)AppointmentStatus.DidNotAttend})";

    /// <summary>The unique index that allows one open appointment per stage.</summary>
    public const string OpenPerStageIndex = "IX_Appointments_StageId_Open";

    private static readonly string OpenPerStageFilter =
        $"[StageId] IS NOT NULL AND [Status] IN ({string.Join(", ", AppointmentWorkflow.OpenStatuses.Select(s => (int)s))})";
}
