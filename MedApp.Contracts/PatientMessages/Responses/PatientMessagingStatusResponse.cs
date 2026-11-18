namespace MedApp.Contracts.PatientMessages.Responses;

/// <summary>Whether this deployment texts and emails patients, for the pages that offer "Tell the patient".</summary>
public sealed class PatientMessagingStatusResponse
{
    /// <summary>Off, Offline (to the development mailbox, never to patients) or Live (GOV.UK Notify).</summary>
    public string Mode { get; set; } = string.Empty;

    /// <summary>Whether messages are made at all (the mode isn't Off).</summary>
    public bool IsOn { get; set; }

    /// <summary>Whether a patient marked Did not attend is messaged the next working day.</summary>
    public bool SendsMissedAppointmentMessages { get; set; }

    /// <summary>How many days before a booked appointment its reminder can go.</summary>
    public int ReminderDaysBefore { get; set; }
}
