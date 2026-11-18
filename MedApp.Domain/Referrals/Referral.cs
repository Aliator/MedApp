using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Patients;
using MedApp.Domain.Teams;

namespace MedApp.Domain.Referrals;

/// <summary>
/// One request for the centre to see one patient in one team, from a referrer (usually a GP practice), as staff
/// registered it from the letter. It waits for triage (<see cref="ReferralStatus.AwaitingTriage"/>), then is accepted
/// into a pathway, rejected or redirected; or it's withdrawn or marked entered in error before then. Every change is
/// recorded in its history (<see cref="ReferralEvent"/>), in the same save. The letter is an attachment on it.
/// <para>
/// It's patient data, from a third party: the reason, the referrer's details and the decision's detail are free text,
/// shown on the referral's page but never on a staff timeline, in a notification, an email or a log.
/// </para>
/// </summary>
public sealed class Referral
{
    public const int MaxPracticeLength = 200;
    public const int MaxClinicianLength = 200;
    public const int MaxOdsCodeLength = 10;
    public const int MaxReasonLength = 2000;
    public const int MaxDetailLength = 500;
    public const int MaxRedirectedToLength = 200;

    public Guid Id { get; init; }

    public Guid PatientId { get; init; }
    public Patient Patient { get; set; } = null!;

    /// <summary>The team asked for.</summary>
    public Guid TeamId { get; set; }
    public Team Team { get; set; } = null!;

    /// <summary>When it arrived, a UK date typed by staff: not in the future, at most a year back.</summary>
    public DateOnly ReceivedOn { get; set; }

    /// <summary>The referring practice (or other organisation), as on the letter. Free text.</summary>
    public string ReferrerPractice { get; set; } = string.Empty;

    /// <summary>The referring clinician, if given. Free text: a third party's personal data.</summary>
    public string? ReferrerClinician { get; set; }

    /// <summary>The practice's Organisation Data Service code, if given (3 to 10 letters and digits, upper case; format only).</summary>
    public string? ReferrerOdsCode { get; set; }

    /// <summary>
    /// The referring practice in the directory, if staff chose one. The typed <see cref="ReferrerPractice"/> stays as the
    /// letter gave it (filled from the directory's name when left empty); referrals from before the directory have none.
    /// </summary>
    public Guid? PracticeId { get; set; }

    /// <summary>The priority the referrer asked for, on the pathway priority scale. The triager chooses the pathway's own.</summary>
    public PatientPathwayPriority RequestedPriority { get; set; }

    /// <summary>The reason for referral, typed from the letter. Free text, clinical.</summary>
    public string Reason { get; set; } = string.Empty;

    public ReferralStatus Status { get; set; }

    public DateTime RegisteredAtUtc { get; init; }
    public Guid RegisteredByUserId { get; init; }

    /// <summary>The username at the time, kept even if the account is later deleted.</summary>
    public string RegisteredByUsername { get; init; } = string.Empty;

    /// <summary>When it left Awaiting triage (triaged, withdrawn or marked entered in error); null until then.</summary>
    public DateTime? DecidedAtUtc { get; set; }
    public Guid? DecidedByUserId { get; set; }
    public string? DecidedByUsername { get; set; }

    /// <summary>Rejected: why, as a fixed choice.</summary>
    public ReferralRejectionReason? RejectionReason { get; set; }

    /// <summary>The detail given with the outcome (a rejection, redirection, withdrawal or entry in error). Free text.</summary>
    public string? DecisionDetail { get; set; }

    /// <summary>Redirected elsewhere: where to. Free text.</summary>
    public string? RedirectedTo { get; set; }

    /// <summary>
    /// Redirected here from another team: why, as the triager who moved it wrote it (the latest such move's). Free
    /// text. Kept apart from <see cref="DecisionDetail"/>, as the referral still awaits triage after it.
    /// </summary>
    public string? RedirectDetail { get; set; }

    /// <summary>Accepted: the pathway it was accepted into, until that pathway is deleted.</summary>
    public Guid? PathwayId { get; set; }
    public PatientPathway? Pathway { get; set; }

    /// <summary>One more for each change. A change names the version it started from (409 if it's changed since).</summary>
    public int Version { get; set; }

    /// <summary>The database's row version, a concurrency token: two saves at once can't both win.</summary>
    public byte[] RowVersion { get; set; } = [];

    public DateTime LastUpdated { get; set; }

    // The waiting-time clock (WaitingClock has the arithmetic). It starts on ReceivedOn, counts whole UK days, stops
    // only for patient-initiated pauses after acceptance, and stops for good when the pathway's clock-stop stage is
    // finished, the pathway is discharged or cancelled, or the referral is closed without one.

    /// <summary>The target in days, copied from the team for the priority when registered, and re-read when the priority or team changes.</summary>
    public int TargetDays { get; set; }

    /// <summary><see cref="ReceivedOn"/> plus every finished pause's days: the day the wait counts from.</summary>
    public DateOnly WaitStartOn { get; set; }

    /// <summary><see cref="WaitStartOn"/> plus <see cref="TargetDays"/>: breached once the wait goes past it.</summary>
    public DateOnly TargetOn { get; set; }

    /// <summary>The total of finished pauses' days.</summary>
    public int PausedDays { get; set; }

    /// <summary>The open pause's start, or null when it isn't paused.</summary>
    public DateOnly? PausedSinceOn { get; set; }

    /// <summary>The open pause's review date (copied for sorting), or null when it isn't paused.</summary>
    public DateOnly? PauseReviewOn { get; set; }

    /// <summary>When the clock stopped, for good; null while it runs or is paused.</summary>
    public DateOnly? StoppedOn { get; set; }

    public ClockStopReason? StopReason { get; set; }

    /// <summary>Its clock's pauses, oldest first.</summary>
    public List<ReferralClockPause> Pauses { get; set; } = [];
}
