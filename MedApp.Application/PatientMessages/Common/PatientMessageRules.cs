using MedApp.Application.Calendar.Common;
using MedApp.Application.Patients.Common;
using MedApp.Domain.Appointments;
using MedApp.Domain.PatientMessages;
using MedApp.Domain.Patients;

namespace MedApp.Application.PatientMessages.Common;

/// <summary>
/// Who a message goes to and when (centre plan decisions 130 and 133): the channels the patient chose
/// (<see cref="ReminderChannel"/>) for which they have a mobile or email, the hours reminders go out, and whether a
/// message waiting to be sent still applies.
/// </summary>
public static class PatientMessageRules
{
    /// <summary>Reminders and missed-appointment messages go only from 09:00 to 19:00 UK time.</summary>
    public static readonly TimeOnly SendingStarts = new(9, 0);

    public static readonly TimeOnly SendingEnds = new(19, 0);

    /// <summary>A missed-appointment message goes from 10:00 on the next working day.</summary>
    public static readonly TimeOnly MissedFrom = new(10, 0);

    /// <summary>A reminder isn't made for an appointment starting sooner than this.</summary>
    public static readonly TimeSpan ReminderAtLeast = TimeSpan.FromHours(2);

    /// <summary>How often sending one may fail for a passing reason before it's given up.</summary>
    public const int MaxSendAttempts = 5;

    /// <summary>How long after sending delivery is asked about.</summary>
    public static readonly TimeSpan DeliveryCheckedFor = TimeSpan.FromDays(3);

    /// <summary>The channels a patient's choice covers.</summary>
    public static IReadOnlyList<PatientMessageChannel> ChannelsFor(ReminderChannel choice) => choice switch
    {
        ReminderChannel.Text => [PatientMessageChannel.Text],
        ReminderChannel.Email => [PatientMessageChannel.Email],
        ReminderChannel.TextAndEmail => [PatientMessageChannel.Text, PatientMessageChannel.Email],
        _ => []
    };

    /// <summary>The patient's mobile or email address for a channel, or null if they have none.</summary>
    public static string? DestinationFor(Patient patient, PatientMessageChannel channel) => channel switch
    {
        PatientMessageChannel.Text => string.IsNullOrWhiteSpace(patient.MobilePhone) ? null : patient.MobilePhone,
        PatientMessageChannel.Email => string.IsNullOrWhiteSpace(patient.Email) || !patient.Email.Contains('@')
            ? null
            : patient.Email.Trim(),
        _ => null
    };

    /// <summary>
    /// Whether a message of this kind may be sent now: a move or a cancellation at once (staff make them in the working
    /// day, and the news can't wait), reminders and missed-appointment messages only from 09:00 to 19:00.
    /// </summary>
    public static bool CanSendNow(PatientMessageKind kind, DateTime nowUtc) =>
        kind is PatientMessageKind.AppointmentMoved or PatientMessageKind.AppointmentCancelled || InSendingHours(nowUtc);

    /// <summary>Whether it's between 09:00 and 19:00 UK time.</summary>
    public static bool InSendingHours(DateTime nowUtc)
    {
        var time = UkTime.TimeOf(nowUtc);
        return time >= SendingStarts && time < SendingEnds;
    }

    /// <summary>
    /// Why a message waiting to be sent shouldn't go any more (a <see cref="PatientMessageReasons"/> code), or null if
    /// it still applies: the appointment is still as the message says (its time and room), still to come (for a reminder, a move or a
    /// cancellation), the record is open (a cancellation goes to a restricted record too, as cancelling does), and the
    /// patient still chose this channel and has the contact.
    /// </summary>
    public static string? WhyNotToSend(PatientMessage message, Appointment appointment, Patient patient, DateTime nowUtc)
    {
        if (PatientRules.IsAnonymised(patient))
            return PatientMessageReasons.RecordClosed;
        if (patient.RestrictedAtUtc is not null && message.Kind != PatientMessageKind.AppointmentCancelled)
            return PatientMessageReasons.RecordClosed;

        var stillApplies = message.Kind switch
        {
            // The room too: its site is in the text, and a room can move to another site at the same time.
            PatientMessageKind.AppointmentReminder or PatientMessageKind.AppointmentMoved =>
                appointment.Status == AppointmentStatus.Booked
                && appointment.StartUtc == message.AppointmentStartUtc
                && appointment.RoomId == message.AppointmentRoomId
                && appointment.StartUtc > nowUtc,
            PatientMessageKind.AppointmentCancelled =>
                appointment.Status == AppointmentStatus.Cancelled && message.AppointmentStartUtc > nowUtc,
            PatientMessageKind.MissedAppointment => appointment.Status == AppointmentStatus.DidNotAttend,
            _ => false
        };
        if (!stillApplies)
            return PatientMessageReasons.NoLongerApplies;

        if (!ChannelsFor(patient.ReminderChannel).Contains(message.Channel) || DestinationFor(patient, message.Channel) is null)
            return PatientMessageReasons.ChoiceChanged;

        return null;
    }
}
