namespace MedApp.Contracts.Patients.Requests;

/// <summary>Query string for <c>GET api/patients/retention-review</c> (admins only).</summary>
public sealed class SearchRetentionReviewRequest
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;

    /// <summary>Also list records past their period that an admin has decided to keep, until their review date.</summary>
    public bool IncludeKept { get; set; }
}
