namespace MedApp.Contracts.Activity.Requests;

/// <summary>Query string for <c>GET api/activity/search</c> (admins only). Every filter is optional.</summary>
public sealed class SearchActivityRequest
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;

    /// <summary>Oldest first; newest first by default.</summary>
    public bool Ascending { get; set; }

    /// <summary>Only entries about this patient (the access report on a patient's page).</summary>
    public Guid? PatientId { get; set; }

    public Guid? UserId { get; set; }

    /// <summary>Any of these actions, e.g. PatientViewed, SignInFailed.</summary>
    public List<string>? Actions { get; set; }

    /// <summary>Matches the username, the account changed or the address, ignoring case.</summary>
    public string? Search { get; set; }

    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }
}
