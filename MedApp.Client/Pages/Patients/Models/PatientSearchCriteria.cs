namespace MedApp.Client.Pages.Patients.Models;

public sealed class PatientSearchCriteria
{
    private string? Query { get; init; }
    private string? FirstName { get; init; }
    private string? LastName { get; init; }
    private DateOnly? DateOfBirth { get; init; }

    public bool HasFilters =>
        !string.IsNullOrWhiteSpace(Query) ||
        !string.IsNullOrWhiteSpace(FirstName) ||
        !string.IsNullOrWhiteSpace(LastName) ||
        DateOfBirth is not null;

    public static PatientSearchCriteria Empty { get; } = new();

    public static PatientSearchCriteria FromQuery(
        string? query,
        string? first,
        string? last,
        string? dob)
    {
        DateOnly? parsedDob = null;
        if (!string.IsNullOrWhiteSpace(dob) && DateOnly.TryParse(dob, out var d))
            parsedDob = d;

        return new PatientSearchCriteria
        {
            Query = string.IsNullOrWhiteSpace(query) ? null : query,
            FirstName = string.IsNullOrWhiteSpace(first) ? null : first,
            LastName = string.IsNullOrWhiteSpace(last) ? null : last,
            DateOfBirth = parsedDob
        };
    }
}