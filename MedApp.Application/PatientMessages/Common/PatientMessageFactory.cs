using MedApp.Domain.Appointments;
using MedApp.Domain.PatientMessages;
using MedApp.Domain.Patients;

namespace MedApp.Application.PatientMessages.Common;

/// <summary>
/// Makes the messages one event sends a patient: one per channel they chose
/// (<see cref="PatientMessageRules.ChannelsFor"/>), Pending, with its text (<see cref="PatientMessageTexts"/>) and
/// destination. A channel they chose but have no contact for, or no choice at all, gives a
/// <see cref="PatientMessageStatus.NotSent"/> message instead (with no text), so the appointment shows that they weren't
/// told, and why (decision 133).
/// </summary>
public static class PatientMessageFactory
{
    public static IReadOnlyList<PatientMessage> For(
        Patient patient,
        Appointment appointment,
        PatientMessageKind kind,
        MessageSite? site,
        PatientMessageOptions settings,
        DateTime nowUtc,
        Guid? requestedByUserId = null)
    {
        var startUtc = appointment.StartUtc
                       ?? throw new InvalidOperationException("A message is about an appointment with a time.");

        var channels = PatientMessageRules.ChannelsFor(patient.ReminderChannel);
        if (channels.Count == 0)
            return [NotSent(PatientMessageChannel.Text, PatientMessageReasons.NoConsent)];

        return channels
            .Select(channel => PatientMessageRules.DestinationFor(patient, channel) is { } destination
                ? new PatientMessage
                {
                    Id = Guid.NewGuid(),
                    PatientId = patient.Id,
                    TeamId = appointment.TeamId,
                    AppointmentId = appointment.Id,
                    Kind = kind,
                    Channel = channel,
                    Destination = destination,
                    Body = PatientMessageTexts.Body(kind, channel, settings.SenderName, startUtc, site, settings.ContactPhone),
                    AppointmentStartUtc = startUtc,
                    AppointmentRoomId = appointment.RoomId,
                    Status = PatientMessageStatus.Pending,
                    RequestedByUserId = requestedByUserId,
                    CreatedAtUtc = nowUtc,
                    StatusAtUtc = nowUtc,
                }
                : NotSent(channel, channel == PatientMessageChannel.Text ? PatientMessageReasons.NoMobile : PatientMessageReasons.NoEmail))
            .ToList();

        PatientMessage NotSent(PatientMessageChannel channel, string reason) => new()
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            TeamId = appointment.TeamId,
            AppointmentId = appointment.Id,
            Kind = kind,
            Channel = channel,
            AppointmentStartUtc = startUtc,
            AppointmentRoomId = appointment.RoomId,
            Status = PatientMessageStatus.NotSent,
            StatusReason = reason,
            RequestedByUserId = requestedByUserId,
            CreatedAtUtc = nowUtc,
            StatusAtUtc = nowUtc,
        };
    }
}
