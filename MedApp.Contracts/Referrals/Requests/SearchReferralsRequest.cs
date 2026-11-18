namespace MedApp.Contracts.Referrals.Requests;

/// <summary>Query string for <c>GET api/referrals/search</c>. Every filter is optional.</summary>
public sealed class SearchReferralsRequest
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;

    /// <summary>
    /// <c>receivedOn</c> (the default: oldest first, so the longest wait is at the top), <c>patient</c>,
    /// <c>specialty</c>, <c>priority</c> or <c>registered</c>.
    /// </summary>
    public string? Sort { get; set; }

    public bool Ascending { get; set; } = true;

    /// <summary>Any of these statuses: AwaitingTriage, Accepted, Rejected, RedirectedElsewhere, Withdrawn, EnteredInError.</summary>
    public List<string>? Statuses { get; set; }

    /// <summary>Any of these specialties.</summary>
    public List<Guid>? SpecialtyIds { get; set; }

    /// <summary>Only the specialties the caller belongs to (all, if they belong to none).</summary>
    public bool Mine { get; set; }

    public Guid? PatientId { get; set; }

    /// <summary>The patient's full name contains this, ignoring case.</summary>
    public string? Search { get; set; }
}
