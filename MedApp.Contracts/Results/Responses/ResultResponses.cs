using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MedApp.Contracts.Patients.Responses;

namespace MedApp.Contracts.Results.Responses;

/// <summary>
/// One result, as its page shows it: the patient (for the banner), where it came from, its report (a file, shown as it
/// came: MedApp never reads it), who must sign it off, and what was done. Free text (the title, action detail and error
/// detail) is here for anyone who can see patients; it never reaches a staff timeline or a notification.
/// </summary>
public sealed class ResultResponse
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }

    /// <summary>The patient, for the banner.</summary>
    public PathwayPatientResponse? Patient { get; set; }

    /// <summary>The patient's restricted, disputed and anonymised flags, for the banners.</summary>
    public PatientFlagsResponse? PatientFlags { get; set; }

    public Guid? PathwayId { get; set; }

    public string? PathwayTitle { get; set; }

    public Guid? SpecimenId { get; set; }

    public string? SpecimenNumber { get; set; }

    public string? SpecimenTypeName { get; set; }

    /// <summary><c>Specimen</c> or <c>ExternalReport</c>.</summary>
    public string Source { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    /// <summary>The report: an attached file, downloaded through the attachments API.</summary>
    public Guid ReportAttachmentId { get; set; }

    /// <summary>The report's file name, or null once it's deleted (entered in error).</summary>
    public string? ReportFileName { get; set; }

    /// <summary>Whether the report has a thumbnail (an image).</summary>
    public bool ReportHasThumbnail { get; set; }

    /// <summary>The lab case whose authorised report made it (MC15), if any.</summary>
    public Guid? LabCaseId { get; set; }

    public string? LabCaseNumber { get; set; }

    /// <summary>A lab case's results as entered (free text); null for a report that's a file.</summary>
    public string? ReportText { get; set; }

    public DateTime ReportedAtUtc { get; set; }

    /// <summary>Who attached the report or recorded the result, or "Deleted user".</summary>
    public string ReportedByUsername { get; set; } = string.Empty;

    /// <summary>Whole days since it was reported.</summary>
    public int AgeDays { get; set; }

    public Guid ResponsibleUserId { get; set; }

    public string ResponsibleUsername { get; set; } = string.Empty;

    /// <summary>Awaiting sign-off, and its responsible clinician can no longer sign off (roles changed, or account gone).</summary>
    public bool NeedsClinician { get; set; }

    /// <summary>AwaitingSignOff, Seen, SignedOff or EnteredInError.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>AwaitingSignOff or Seen.</summary>
    public bool IsAwaitingSignOff { get; set; }

    public DateTime? SeenAtUtc { get; set; }

    public string? SeenByUsername { get; set; }

    public DateTime? SignedOffAtUtc { get; set; }

    public string? SignedOffByUsername { get; set; }

    /// <summary>What the clinician did, by name (<c>ResultAction</c>).</summary>
    public string? Action { get; set; }

    public string? ActionDetail { get; set; }

    public DateTime? EnteredInErrorAtUtc { get; set; }

    public string? EnteredInErrorByUsername { get; set; }

    public string? ErrorDetail { get; set; }

    public int Version { get; set; }

    public bool CanMarkSeen { get; set; }

    public bool CanSignOff { get; set; }

    /// <summary>Whether the caller may take it over or give it to someone else.</summary>
    public bool CanReassign { get; set; }

    public bool CanMarkEnteredInError { get; set; }

    /// <summary>Why it can't change, if it can't: the record's anonymisation, or its being closed.</summary>
    public string? ReadOnlyReason { get; set; }
}

/// <summary>A result in a list: dates, ages, names and statuses only.</summary>
public sealed class ResultSummaryResponse
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }

    public string PatientName { get; set; } = string.Empty;

    public string? PatientNhsNumber { get; set; }

    public DateOnly PatientDateOfBirth { get; set; }

    public Guid? PathwayId { get; set; }

    public string? PathwayTitle { get; set; }

    public string Source { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? SpecimenNumber { get; set; }

    public DateTime ReportedAtUtc { get; set; }

    public int AgeDays { get; set; }

    public string ResponsibleUsername { get; set; } = string.Empty;

    public bool NeedsClinician { get; set; }

    public string Status { get; set; } = string.Empty;

    public bool IsAwaitingSignOff { get; set; }

    public DateTime? SignedOffAtUtc { get; set; }

    public string? SignedOffByUsername { get; set; }

    public string? Action { get; set; }
}

/// <summary>The side nav's count: results awaiting the caller's sign-off.</summary>
public sealed class ResultCountResponse
{
    public int AwaitingMine { get; set; }
}
