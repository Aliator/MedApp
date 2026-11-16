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

        // Deleting a procedure detaches its appointments in the same save (PatientProcedureRepository.DeleteAsync).
        builder.HasOne(x => x.Procedure)
            .WithMany()
            .HasForeignKey(x => x.ProcedureId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.Stage)
            .WithMany()
            .HasForeignKey(x => x.StageId)
            .OnDelete(DeleteBehavior.NoAction);

        // No foreign keys to the users table (staff, creator, booker), as with procedure assignments.

        // A calendar's appointments in a range of dates.
        builder.HasIndex(x => new { x.StaffUserId, x.StartUtc });
        // What needs booking, and a patient's appointments.
        builder.HasIndex(x => new { x.Status, x.CreatedAtUtc });
        builder.HasIndex(x => x.PatientId);
        builder.HasIndex(x => x.ProcedureId);
        builder.HasIndex(x => x.StageId);
        // One open (Requested or Booked) appointment per stage: checked in CreateAppointmentHandler, and enforced here
        // too, so two requests at once can't both add one. A violation is recognised by the index's name
        // (AppointmentRepository). The filter's numbers are AppointmentWorkflow.OpenStatuses.
        builder.HasIndex(x => x.StageId, OpenPerStageIndex)
            .IsUnique()
            .HasFilter(OpenPerStageFilter);
    }

    /// <summary>The unique index that allows one open appointment per stage.</summary>
    public const string OpenPerStageIndex = "IX_Appointments_StageId_Open";

    private static readonly string OpenPerStageFilter =
        $"[StageId] IS NOT NULL AND [Status] IN ({string.Join(", ", AppointmentWorkflow.OpenStatuses.Select(s => (int)s))})";
}
