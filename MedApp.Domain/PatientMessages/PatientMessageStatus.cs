namespace MedApp.Domain.PatientMessages;

/// <summary>Where a message to a patient is. Stored by number, so never renumber these.</summary>
public enum PatientMessageStatus
{
    /// <summary>Waiting for the <c>patient-reminders</c> job to send it.</summary>
    Pending = 0,

    /// <summary>Handed to the provider (GOV.UK Notify, or the offline stand-in); delivery not yet known.</summary>
    Sent = 1,

    /// <summary>The provider says it was delivered.</summary>
    Delivered = 2,

    /// <summary>The provider refused it or couldn't deliver it, or sending it kept failing.</summary>
    Failed = 3,

    /// <summary>Not sent, because it no longer applied (the appointment moved, was cancelled or has passed) or the patient's choice changed.</summary>
    Cancelled = 4,

    /// <summary>Never sendable: the patient hasn't agreed to that channel, or has no mobile or email for it.</summary>
    NotSent = 5
}
