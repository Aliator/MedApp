using MedApp.Domain.Appointments;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Patients;
using MedApp.Domain.Referrals;

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

    /// <summary>
    /// Patients whose names and email contain the given text, born within the given dates (inclusive), and whose NHS
    /// number contains the given digits (spaces ignored).
    /// </summary>
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
        if (NhsNumberDigits(query.NhsNumber) is { } nhsNumber)
            patients = patients.Where(p => p.NhsNumber != null && p.NhsNumber.Contains(nhsNumber));
        return patients;
    }

    /// <summary>
    /// The clinic picker (MC6, the light demo's patient search): with a team, only patients with a pathway, a referral or
    /// an appointment in it (any status); without one, every patient. The tables are passed in (DbSets, or lists in a
    /// test), so it's three <c>EXISTS</c> in SQL.
    /// </summary>
    public static IQueryable<Patient> InTeam(
        this IQueryable<Patient> patients,
        Guid? teamId,
        IQueryable<PatientPathway> pathways,
        IQueryable<Referral> referrals,
        IQueryable<Appointment> appointments)
    {
        if (teamId is not { } id)
            return patients;

        return patients.Where(p => pathways.Any(x => x.PatientId == p.Id && x.TeamId == id)
                                   || referrals.Any(x => x.PatientId == p.Id && x.TeamId == id)
                                   || appointments.Any(x => x.PatientId == p.Id && x.TeamId == id));
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

    /// <summary>
    /// The names of the filters a search used (<c>firstName</c>, <c>lastName</c>, <c>email</c>,
    /// <c>dateOfBirthFrom</c>, <c>dateOfBirthTo</c>, <c>nhsNumber</c>), for the activity log, which never keeps the terms themselves.
    /// </summary>
    public static IReadOnlyList<string> FiltersUsed(SearchPatientsQuery query)
    {
        var used = new List<string>();
        if (Term(query.FirstName) is not null)
            used.Add("firstName");
        if (Term(query.LastName) is not null)
            used.Add("lastName");
        if (Term(query.Email) is not null)
            used.Add("email");
        if (query.DateOfBirthFrom is not null)
            used.Add("dateOfBirthFrom");
        if (query.DateOfBirthTo is not null)
            used.Add("dateOfBirthTo");
        if (NhsNumberDigits(query.NhsNumber) is not null)
            used.Add("nhsNumber");
        return used;
    }

    /// <summary>The NHS number filter's digits, its spaces left out, or null when it's empty.</summary>
    public static string? NhsNumberDigits(string? text) => NhsNumbers.Normalise(text);

    private static string? Term(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim().ToLower();
}
