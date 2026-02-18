namespace MedApp.Client.Models;

public sealed class PatientSearchCriteria
{
    public string? Query { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public PartialDate Dob { get; init; } = PartialDate.Empty;

    public bool HasFilters =>
        !string.IsNullOrWhiteSpace(Query) ||
        !string.IsNullOrWhiteSpace(FirstName) ||
        !string.IsNullOrWhiteSpace(LastName) ||
        !Dob.IsEmpty;

    public static PatientSearchCriteria Empty { get; } = new();

    public static PatientSearchCriteria FromQuery(
        string? query,
        string? first,
        string? last,
        int? dobYear,
        int? dobMonth,
        int? dobDay) => new()
    {
        Query = string.IsNullOrWhiteSpace(query) ? null : query,
        FirstName = string.IsNullOrWhiteSpace(first) ? null : first,
        LastName = string.IsNullOrWhiteSpace(last) ? null : last,
        Dob = PartialDate.From(dobYear, dobMonth, dobDay)
    };
}