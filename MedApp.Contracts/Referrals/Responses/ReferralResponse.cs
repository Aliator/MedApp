using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MedApp.Contracts.Patients.Responses;

namespace MedApp.Contracts.Referrals.Responses;

/// <summary>
/// One referral, as its page shows it: the patient (for the banner), what the referrer asked for, where it is, its
/// history, and what the caller may do with it. Free text (the reason, the referrer, the decision's detail) is here for
/// anyone who can see patients, as notes are on a pathway's page; it never reaches a staff timeline.
/// </summary>
public sealed class ReferralResponse
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }

    /// <summary>The patient, for the banner.</summary>
    public PathwayPatientResponse? Patient { get; set; }

    /// <summary>The patient's restricted, disputed and anonymised flags, for the banners.</summary>
    public PatientFlagsResponse? PatientFlags { get; set; }

    public Guid SpecialtyId { get; set; }

    public string SpecialtyName { get; set; } = string.Empty;

    public DateOnly ReceivedOn { get; set; }

    /// <summary>The referring practice in the directory, if staff chose one; null for none.</summary>
    public Guid? PracticeId { get; set; }

    public string ReferrerPractice { get; set; } = string.Empty;

    public string? ReferrerClinician { get; set; }

    public string? ReferrerOdsCode { get; set; }

    /// <summary>The priority the referrer asked for: Low, Medium, High or Urgent.</summary>
    public string RequestedPriority { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    /// <summary>AwaitingTriage, Accepted, Rejected, RedirectedElsewhere, Withdrawn or EnteredInError.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Whether it's still open (awaiting triage).</summary>
    public bool IsOpen { get; set; }

    public DateTime RegisteredAtUtc { get; set; }

    public string RegisteredByUsername { get; set; } = string.Empty;

    /// <summary>When it left Awaiting triage, and who by.</summary>
    public DateTime? DecidedAtUtc { get; set; }

    public string? DecidedByUsername { get; set; }

    /// <summary>Rejected: why (NotOfferedHere, DoesNotMeetCriteria, InsufficientInformation, Duplicate or Other).</summary>
    public string? RejectionReason { get; set; }

    /// <summary>The detail given with the outcome (a withdrawal, an entry in error, a rejection or a redirection).</summary>
    public string? DecisionDetail { get; set; }

    /// <summary>Redirected elsewhere: where to.</summary>
    public string? RedirectedTo { get; set; }

    /// <summary>
    /// Redirected here from another specialty: why (the latest such redirection's detail). The referral stays awaiting
    /// triage, so this is kept apart from <see cref="DecisionDetail"/>.
    /// </summary>
    public string? RedirectDetail { get; set; }

    /// <summary>Accepted: the pathway it was accepted into.</summary>
    public Guid? PathwayId { get; set; }

    /// <summary>Pass back as <c>expectedVersion</c> with a change.</summary>
    public int Version { get; set; }

    public DateTime LastUpdated { get; set; }

    /// <summary>How many files (not deleted) are attached to it: its letter, usually one.</summary>
    public int LetterCount { get; set; }

    /// <summary>The caller may edit its details: it awaits triage, the record can change and they can register referrals.</summary>
    public bool CanEdit { get; set; }

    /// <summary>The caller may withdraw it, or mark it entered in error: it awaits triage and they can register referrals.</summary>
    public bool CanClose { get; set; }

    /// <summary>The caller may attach its letter: the record can change, it isn't entered in error, and they can attach files.</summary>
    public bool CanAttachFiles { get; set; }

    /// <summary>Why it can't be changed now (closed, or the record restricted or anonymised); null when it can.</summary>
    public string? ReadOnlyReason { get; set; }

    /// <summary>Whether the caller may triage it. Always false until triage is built.</summary>
    public bool CanTriage { get; set; }

    /// <summary>Why the caller can't triage it, when they can't.</summary>
    public string? WhyCantTriage { get; set; }

    /// <summary>Its waiting-time clock.</summary>
    public WaitingClockResponse? Clock { get; set; }

    /// <summary>The clock's pauses, oldest first.</summary>
    public List<ClockPauseResponse> Pauses { get; set; } = [];

    /// <summary>The caller may pause the clock: it runs, the referral was accepted, and they can work on pathways.</summary>
    public bool CanPauseClock { get; set; }

    /// <summary>The caller may resume it: it's paused, the record can change, and they can work on pathways.</summary>
    public bool CanResumeClock { get; set; }

    /// <summary>Everything that happened to it, oldest first.</summary>
    public List<ReferralEventResponse> History { get; set; } = [];
}
