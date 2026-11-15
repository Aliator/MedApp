namespace MedApp.Client.Models;

public sealed class PatientSearchCriteria
{
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Email { get; init; }
    public PartialDate Dob { get; init; } = PartialDate.Empty;

    public bool HasFilters =>
        !string.IsNullOrWhiteSpace(FirstName) ||
        !string.IsNullOrWhiteSpace(LastName) ||
        !string.IsNullOrWhiteSpace(Email) ||
        !Dob.IsEmpty;

    public static PatientSearchCriteria Empty { get; } = new();

    public static PatientSearchCriteria FromQuery(
        string? first,
        string? last,
        string? email,
        int? dobYear,
        int? dobMonth,
        int? dobDay) => new()
    {
        FirstName = string.IsNullOrWhiteSpace(first) ? null : first,
        LastName = string.IsNullOrWhiteSpace(last) ? null : last,
        Email = string.IsNullOrWhiteSpace(email) ? null : email,
        Dob = PartialDate.From(dobYear, dobMonth, dobDay)
    };
}