namespace MedApp.Domain.Referrals;

/// <summary>Why a referral's waiting-time clock stopped. Stored as its number, so never renumber these.</summary>
public enum ClockStopReason
{
    /// <summary>The pathway's clock-stop stage (the template's marked stage) was finished.</summary>
    StopStageFinished = 0,

    /// <summary>The pathway was discharged (<c>Completed</c>) before its clock-stop stage, or it has none.</summary>
    PathwayDischarged = 1,

    /// <summary>The pathway was cancelled.</summary>
    PathwayCancelled = 2,

    Rejected = 3,
    RedirectedElsewhere = 4,
    Withdrawn = 5,
    EnteredInError = 6
}
