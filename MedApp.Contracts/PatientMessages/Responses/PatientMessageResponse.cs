namespace MedApp.Contracts.PatientMessages.Responses;

/// <summary>
/// One text or email to a patient about an appointment: what was sent (or why nothing was), to where, and what became
/// of it.
/// </summary>
public sealed class PatientMessageResponse
{
    public Guid Id { get; set; }

    public Guid AppointmentId { get; set; }

    /// <summary>AppointmentReminder, AppointmentMoved, AppointmentCancelled or MissedAppointment.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Text or Email.</summary>
    public string Channel { get; set; } = string.Empty;

    /// <summary>The mobile or email address it went to; empty when it couldn't go.</summary>
    public string Destination { get; set; } = string.Empty;

    /// <summary>The text as sent (an email's body); empty when it couldn't go.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>An email's subject, fixed for each kind; null for a text.</summary>
    public string? Subject { get; set; }

    /// <summary>The appointment's start the message is about.</summary>
    public DateTime AppointmentStartUtc { get; set; }

    /// <summary>Pending, Sent, Delivered, Failed, Cancelled or NotSent.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Why it's NotSent, Cancelled or Failed: NoConsent, NoMobile, NoEmail, NoLongerApplies, ChoiceChanged, RecordClosed,
    /// Rejected, TooManyAttempts, or the provider's status (e.g. permanent-failure).
    /// </summary>
    public string? StatusReason { get; set; }

    /// <summary>Who asked for it (staff moving or cancelling the appointment); null for a reminder the app sent.</summary>
    public string? RequestedByUsername { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? SentAtUtc { get; set; }

    public DateTime? StatusAtUtc { get; set; }
}
