namespace MedApp.Domain.Referrals;

/// <summary>
/// Why a referral's waiting-time clock was paused: only for delays the patient chose or caused. The centre's own
/// delays (on hold, waiting for a result) never pause it. Stored as its number, so never renumber these.
/// </summary>
public enum ClockPauseReason
{
    PatientChoseToWait = 0,
    PatientUnavailable = 1,
    Other = 2
}
