using MedApp.Domain.Patients;

namespace MedApp.Application.Patients.Queries.SearchPatients;

/// <summary>
/// The filtering and sorting behind <see cref="SearchPatientsQuery"/>, written as plain LINQ so the repository
/// runs it in SQL and the unit tests run it in memory. Text matches ignore case either way.
/// </summary>
public static class PatientSearch
{
    public const string SortByName = "name";
    public const string SortByDateOfBirth = "dateOfBirth";
    public const string SortByEmail = "email";

    public static readonly IReadOnlyList<string> SortColumns = [SortByName, SortByDateOfBirth, SortByEmail];

    public static bool IsSortColumn(string sort) =>
        SortColumns.Contains(sort, StringComparer.OrdinalIgnoreCase);

    /// <summary>Patients whose names and email contain the given text, born within the given dates (inclusive).</summary>
    public static IQueryable<Patient> Filter(this IQueryable<Patient> patients, SearchPatientsQuery query)
    {
        if (Term(query.FirstName) is { } firstName)
            patients = patients.Where(p => p.FirstName.ToLower().Contains(firstName));
        if (Term(query.LastName) is { } lastName)
            patients = patients.Where(p => p.LastName.ToLower().Contains(lastName));
        if (Term(query.Email) is { } email)
            patients = patients.Where(p => p.Email.ToLower().Contains(email));
        if (query.DateOfBirthFrom is { } from)
            patients = patients.Where(p => p.DateOfBirth >= from);
        if (query.DateOfBirthTo is { } to)
            patients = patients.Where(p => p.DateOfBirth <= to);
        return patients;
    }

    /// <summary>
    /// Sorts by name (last, then first; the default), date of birth or email. Ties are broken by ID, so paging
    /// never shows a row twice or skips one.
    /// </summary>
    public static IOrderedQueryable<Patient> Sort(this IQueryable<Patient> patients, string? sort, bool ascending)
    {
        var column = SortColumns.FirstOrDefault(c => string.Equals(c, sort, StringComparison.OrdinalIgnoreCase)) ?? SortByName;

        return (column, ascending) switch
        {
            (SortByDateOfBirth, true) => patients.OrderBy(p => p.DateOfBirth).ThenBy(p => p.LastName).ThenBy(p => p.FirstName).ThenBy(p => p.Id),
            (SortByDateOfBirth, false) => patients.OrderByDescending(p => p.DateOfBirth).ThenBy(p => p.LastName).ThenBy(p => p.FirstName).ThenBy(p => p.Id),
            (SortByEmail, true) => patients.OrderBy(p => p.Email.ToLower()).ThenBy(p => p.Id),
            (SortByEmail, false) => patients.OrderByDescending(p => p.Email.ToLower()).ThenBy(p => p.Id),
            (_, true) => patients.OrderBy(p => p.LastName.ToLower()).ThenBy(p => p.FirstName.ToLower()).ThenBy(p => p.Id),
            (_, false) => patients.OrderByDescending(p => p.LastName.ToLower()).ThenByDescending(p => p.FirstName.ToLower()).ThenBy(p => p.Id),
        };
    }

    private static string? Term(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim().ToLower();
}
