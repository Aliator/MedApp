namespace MedApp.Contracts.Referrals.Requests;

/// <summary>A referral's details, corrected while it awaits triage. Every field is replaced.</summary>
public sealed class EditReferralRequest
{
    public Guid SpecialtyId { get; set; }

    public DateOnly ReceivedOn { get; set; }

    /// <summary>
    /// The referring practice in the directory, if chosen (not archived, unless the referral already has it); null for
    /// none. When given, an empty <c>referrerPractice</c> takes the practice's name, and an empty <c>referrerOdsCode</c>
    /// its code.
    /// </summary>
    public Guid? PracticeId { get; set; }

    public string ReferrerPractice { get; set; } = string.Empty;

    public string? ReferrerClinician { get; set; }

    public string? ReferrerOdsCode { get; set; }

    /// <summary>Low, Medium, High or Urgent.</summary>
    public string RequestedPriority { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    /// <summary>The referral's <c>version</c> the change was made from: a 409 if someone has changed it since.</summary>
    public int ExpectedVersion { get; set; }
}
