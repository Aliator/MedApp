namespace MedApp.Application.PatientMessages.Common;

/// <summary>
/// How texts and emails reach patients (the <c>PatientMessages</c> section; centre plan decision 128). Off unless set:
/// nothing is made or sent, and the pages offer no "Tell the patient".
/// </summary>
public sealed class PatientMessageOptions
{
    public const string Section = "PatientMessages";

    public PatientMessageMode Mode { get; set; } = PatientMessageMode.Off;

    /// <summary>The organisation's name, at the start of every message (e.g. "Riverside Community Clinic").</summary>
    public string SenderName { get; set; } = "MedApp";

    /// <summary>
    /// The number patients are asked to call when the appointment's site has none (or it has no room, so no site), e.g.
    /// the booking line. Empty: "contact us".
    /// </summary>
    public string? ContactPhone { get; set; }

    /// <summary>
    /// Offline only: the address every message is emailed to, with its real destination in the subject, through the
    /// staff email settings (Mailpit in development and on kind). A <c>.invalid</c> address by default, so no real mail
    /// server would deliver it.
    /// </summary>
    public string OfflineRecipient { get; set; } = "patient-messages@medapp.invalid";

    /// <summary>Whether messages are made and sent at all.</summary>
    public bool IsOn => Mode != PatientMessageMode.Off;
}

/// <summary>Which sender patient messages go through.</summary>
public enum PatientMessageMode
{
    /// <summary>None (the default): nothing is made or sent.</summary>
    Off,

    /// <summary>
    /// The offline stand-in: each message is emailed to <see cref="PatientMessageOptions.OfflineRecipient"/> (Mailpit),
    /// never to the patient. For development, kind, CI and demonstrations.
    /// </summary>
    Offline,

    /// <summary>
    /// GOV.UK Notify, the real sender: a disclosure to a processor (DPIA risk 32), so only once the organisation has its
    /// own Notify service and has listed it as a sub-processor (<c>docs/governance/sub-processors.md</c>).
    /// </summary>
    Live
}

/// <summary>When reminders and missed-appointment messages go (the <c>Reminders</c> section; decision 130).</summary>
public sealed class ReminderOptions
{
    public const string Section = "Reminders";

    public const string InvalidSettings = "Reminders:DaysBefore must be between 1 and 14.";

    /// <summary>How many days before a Booked appointment its reminder can go (from 1 to 14).</summary>
    public int DaysBefore { get; set; } = 2;

    /// <summary>
    /// Whether a patient marked Did not attend is texted or emailed, the next working day at 10:00, asking them to call.
    /// Off by default: the organisation decides.
    /// </summary>
    public bool MissedMessage { get; set; }

    public bool IsValid => DaysBefore is >= 1 and <= 14;
}
