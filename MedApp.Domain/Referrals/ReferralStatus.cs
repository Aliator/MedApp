namespace MedApp.Domain.Referrals;

/// <summary>Where a referral is. Stored as its number, so never renumber these.</summary>
public enum ReferralStatus
{
    /// <summary>Registered, waiting for a clinician of its specialty to triage it. Open, as <see cref="InformationRequested"/> is.</summary>
    AwaitingTriage = 0,

    /// <summary>Accepted into a pathway (<see cref="Referral.PathwayId"/>).</summary>
    Accepted = 1,

    /// <summary>Rejected, with a reason (<see cref="ReferralRejectionReason"/>).</summary>
    Rejected = 2,

    /// <summary>Sent to another provider, outside the centre.</summary>
    RedirectedElsewhere = 3,

    /// <summary>The referrer or the patient withdrew it before it was triaged.</summary>
    Withdrawn = 4,

    /// <summary>Registered in error: on the wrong patient, or twice.</summary>
    EnteredInError = 5,

    /// <summary>
    /// A triager asked the referrer for more information (decision 106 of docs/centre-phases-3-5-plan.md): still open,
    /// waiting in its own view until it comes, when it goes back to <see cref="AwaitingTriage"/>.
    /// </summary>
    InformationRequested = 6
}
