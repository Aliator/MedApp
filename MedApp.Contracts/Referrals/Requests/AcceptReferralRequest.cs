namespace MedApp.Contracts.Referrals.Requests;

/// <summary>
/// A triager accepting a referral awaiting triage into a new pathway. Every choice is the clinician's: nothing is
/// prefilled or suggested (hazard H-41). The pathway's target date comes from the referral's waiting-time clock, for
/// the priority chosen.
/// </summary>
public sealed class AcceptReferralRequest
{
    /// <summary>The pathway template, one of the referral's team's (not archived).</summary>
    public Guid TemplateId { get; set; }

    /// <summary>The pathway's priority, chosen by the triager: Low, Medium, High or Urgent.</summary>
    public string Priority { get; set; } = string.Empty;

    /// <summary>The pathway's title; blank uses the template's name.</summary>
    public string? Title { get; set; }

    /// <summary>Someone to assign to the pathway, who can work on pathways. Needs the AssignPathways permission.</summary>
    public Guid? AssigneeUserId { get; set; }

    /// <summary>The pathway's first note (Markdown, 4,000 characters), if any.</summary>
    public string? Notes { get; set; }

    /// <summary>The referral's <c>version</c> the decision was made from: a 409 if someone has changed it since.</summary>
    public int ExpectedVersion { get; set; }
}
