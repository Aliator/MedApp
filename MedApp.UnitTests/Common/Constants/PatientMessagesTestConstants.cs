using MedApp.Application.PatientMessages.Common;
using MedApp.Domain.Appointments;
using MedApp.Domain.PatientMessages;
using MedApp.Domain.Patients;

namespace MedApp.UnitTests.Common.Constants;

/// <summary>Synthetic patient messages: Ofcom's drama range for mobiles, example.com addresses, invented sites.</summary>
public static class PatientMessagesTestConstants
{
    public const string SenderName = "Riverside Test Clinic";
    public const string ContactPhone = "01632 960000";
    public const string Mobile = "+447700900123";
    public const string FailingMobile = "+447700900666";
    public const string Email = "pat.test@example.com";

    public static readonly MessageSite Site = new("Hilltop Outreach", ["1 Test Street", "Testtown"], "ZZ1 1ZZ", "01632 960123");

    public static readonly MessageSite SiteWithoutPhone = new("Hilltop Outreach", [], null, null);

    public static PatientMessageOptions Options(PatientMessageMode mode = PatientMessageMode.Offline) => new()
    {
        Mode = mode,
        SenderName = SenderName,
        ContactPhone = ContactPhone,
    };

    /// <summary>A synthetic patient with a mobile, an email address and this choice.</summary>
    public static Patient Patient(ReminderChannel channel = ReminderChannel.TextAndEmail, string? mobile = Mobile, string email = Email)
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        patient.MobilePhone = mobile;
        patient.Email = email;
        patient.ReminderChannel = channel;
        return patient;
    }

    /// <summary>A message about an appointment, as the factory makes one.</summary>
    public static PatientMessage Message(
        Appointment appointment,
        PatientMessageKind kind = PatientMessageKind.AppointmentReminder,
        PatientMessageChannel channel = PatientMessageChannel.Text,
        PatientMessageStatus status = PatientMessageStatus.Pending,
        DateTime? startUtc = null,
        DateTime? createdAtUtc = null) => new()
    {
        Id = Guid.NewGuid(),
        PatientId = appointment.PatientId,
        AppointmentId = appointment.Id,
        Kind = kind,
        Channel = channel,
        Destination = channel == PatientMessageChannel.Text ? Mobile : Email,
        Body = "Synthetic message",
        AppointmentStartUtc = startUtc ?? appointment.StartUtc!.Value,
        AppointmentRoomId = appointment.RoomId,
        Status = status,
        CreatedAtUtc = createdAtUtc ?? CalendarTestConstants.Now,
        StatusAtUtc = createdAtUtc ?? CalendarTestConstants.Now,
    };
}
