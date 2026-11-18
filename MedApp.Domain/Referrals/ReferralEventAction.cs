namespace MedApp.Domain.Referrals;

/// <summary>
/// What a referral's history entry records. Stored as its number, so never renumber these. Every value is declared
/// now, so the branches adding triage and the waiting-time clock don't collide: so far only <see cref="Registered"/>,
/// <see cref="DetailsEdited"/>, <see cref="Withdrawn"/> and <see cref="EnteredInError"/> are written.
/// </summary>
public enum ReferralEventAction
{
    Registered = 0,
    DetailsEdited = 1,
    SpecialtyChanged = 2,
    Accepted = 3,
    Rejected = 4,
    RedirectedElsewhere = 5,
    Withdrawn = 6,
    EnteredInError = 7,
    ClockPaused = 8,
    ClockResumed = 9,
    ClockStopped = 10,
    TargetChanged = 11,

    /// <summary>A triager asked the referrer for more information: it waits in its own view, still open.</summary>
    InformationRequested = 12,

    /// <summary>The information asked for came: it's back awaiting triage.</summary>
    InformationReceived = 13
}
