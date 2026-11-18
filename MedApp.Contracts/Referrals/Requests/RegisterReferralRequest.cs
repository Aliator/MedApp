namespace MedApp.Contracts.Referrals.Requests;

/// <summary>A new referral for a patient already in MedApp, typed from the referrer's letter.</summary>
public sealed class RegisterReferralRequest
{
    public Guid PatientId { get; set; }

    /// <summary>The specialty asked for (not archived).</summary>
    public Guid SpecialtyId { get; set; }

    /// <summary>When it arrived (a UK date): not in the future, at most a year back.</summary>
    public DateOnly ReceivedOn { get; set; }

    /// <summary>The referring practice or organisation, as on the letter (200 characters).</summary>
    public string ReferrerPractice { get; set; } = string.Empty;

    /// <summary>The referring clinician, if given (200 characters).</summary>
    public string? ReferrerClinician { get; set; }

    /// <summary>The practice's ODS code, if known: 3 to 10 letters and digits.</summary>
    public string? ReferrerOdsCode { get; set; }

    /// <summary>The priority the referrer asked for: Low, Medium, High or Urgent.</summary>
    public string RequestedPriority { get; set; } = string.Empty;

    /// <summary>The reason for referral, from the letter (2,000 characters).</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// The patient already has an open referral to this specialty, and this is a different one. Without it, that's a
    /// 400 that says when the open ones were received.
    /// </summary>
    public bool ConfirmPossibleDuplicate { get; set; }
}
