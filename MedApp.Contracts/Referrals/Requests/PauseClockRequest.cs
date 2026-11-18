namespace MedApp.Contracts.Referrals.Requests;

/// <summary>Pausing an accepted referral's waiting-time clock, for a delay the patient chose or caused.</summary>
public sealed class PauseClockRequest
{
    /// <summary>PatientChoseToWait, PatientUnavailable or Other.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>More about why (500 characters), required for Other.</summary>
    public string? Detail { get; set; }

    /// <summary>When someone must look at it again: from tomorrow to 90 days ahead. The pathway's target date while it lasts.</summary>
    public DateOnly ReviewOn { get; set; }

    /// <summary>The referral's <c>version</c> the change was made from: a 409 if someone has changed it since.</summary>
    public int ExpectedVersion { get; set; }
}
