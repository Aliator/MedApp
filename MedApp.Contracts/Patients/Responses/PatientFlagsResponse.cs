namespace MedApp.Contracts.Patients.Responses;

/// <summary>A patient's restricted, disputed and anonymised flags, for banners on the pages about them.</summary>
public sealed class PatientFlagsResponse
{
    public bool IsRestricted { get; set; }
    public string? RestrictionReason { get; set; }
    public DateTime? RestrictedAtUtc { get; set; }
    public bool IsDisputed { get; set; }
    public string? DisputeReason { get; set; }
    public DateTime? DisputedAtUtc { get; set; }
    public bool IsAnonymised { get; set; }
    public DateTime? AnonymisedAtUtc { get; set; }
}
