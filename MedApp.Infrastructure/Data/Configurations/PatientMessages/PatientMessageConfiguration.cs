using MedApp.Domain.PatientMessages;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.PatientMessages;

public sealed class PatientMessageConfiguration : IEntityTypeConfiguration<PatientMessage>
{
    /// <summary>One message per appointment, kind, channel, appointment start and room, so nothing is sent twice.</summary>
    public const string UniqueIndex = "IX_PatientMessages_AppointmentId_Kind_Channel_AppointmentStartUtc_AppointmentRoomId";

    public void Configure(EntityTypeBuilder<PatientMessage> builder)
    {
        builder.ToTable("PatientMessages");

        builder.HasKey(x => x.Id);

        // No foreign keys, like the appointment's history: a message outlives what it's about.
        builder.Property(x => x.Kind).IsRequired();
        builder.Property(x => x.Channel).IsRequired();
        builder.Property(x => x.Status).IsRequired();

        builder.Property(x => x.Destination)
            .IsRequired()
            .HasMaxLength(PatientMessage.MaxDestinationLength)
            .HasDefaultValue(string.Empty);

        builder.Property(x => x.Body)
            .IsRequired()
            .HasMaxLength(PatientMessage.MaxBodyLength)
            .HasDefaultValue(string.Empty);

        builder.Property(x => x.StatusReason).HasMaxLength(PatientMessage.MaxCodeLength);
        builder.Property(x => x.ProviderReference).HasMaxLength(PatientMessage.MaxCodeLength);

        builder.Property(x => x.AppointmentStartUtc).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();

        // Unfiltered, so two messages for no room count as the same (SQL Server treats the nulls as equal).
        builder.HasIndex(x => new { x.AppointmentId, x.Kind, x.Channel, x.AppointmentStartUtc, x.AppointmentRoomId }, UniqueIndex)
            .IsUnique()
            .HasFilter(null);

        builder.Property(x => x.RowVersion).IsRowVersion();

        // The job's work: what's waiting, and what's awaiting delivery.
        builder.HasIndex(x => new { x.Status, x.CreatedAtUtc });

        // A patient's whole record (export and anonymisation).
        builder.HasIndex(x => x.PatientId);
    }
}
