using MedApp.Application.Pathways.Common;
using MedApp.Domain.Referrals;

namespace MedApp.Application.Referrals.Queries.SearchReferrals;

/// <summary>
/// The filtering and sorting behind <see cref="SearchReferralsQuery"/>, written as plain LINQ so the repository runs it
/// in SQL and the unit tests run it in memory (see <c>PatientSearch</c>). Orders are by dates and labels staff chose,
/// never by anything clinical.
/// </summary>
public static class ReferralSearch
{
    public const string SortByReceivedOn = "receivedOn";
    public const string SortByPatient = "patient";
    public const string SortBySpecialty = "specialty";
    public const string SortByPriority = "priority";
    public const string SortByRegistered = "registered";

    public static readonly IReadOnlyList<string> SortColumns =
        [SortByReceivedOn, SortByPatient, SortBySpecialty, SortByPriority, SortByRegistered];

    public static string StatusNames => string.Join(", ", Enum.GetNames<ReferralStatus>());

    public static bool IsSortColumn(string sort) => SortColumns.Contains(sort, StringComparer.OrdinalIgnoreCase);

    public static bool IsStatus(string? value) => PathwayEnums.TryParse<ReferralStatus>(value, out _);

    /// <summary>
    /// Whether the search looks for a patient by name (the term typed), which the activity log records as a search
    /// (<c>PatientsSearched</c>); the other filters are views of the inbox.
    /// </summary>
    public static bool SearchesForPatient(SearchReferralsQuery query) => !string.IsNullOrWhiteSpace(query.Search);

    /// <summary>
    /// The names of the filters a search used, for the activity log: never their values (the name typed could itself
    /// be a patient's details).
    /// </summary>
    public static IReadOnlyList<string> FiltersUsed(SearchReferralsQuery query)
    {
        var used = new List<string>();
        if (!string.IsNullOrWhiteSpace(query.Search))
            used.Add("patientName");
        if (query.PatientId is not null)
            used.Add("patientId");
        if (query.Statuses is { Count: > 0 })
            used.Add("statuses");
        if (query.SpecialtyIds is { Count: > 0 })
            used.Add("specialtyIds");
        if (query.Mine)
            used.Add("mine");
        return used;
    }

    /// <summary>
    /// Referrals matching every filter given. <paramref name="specialtyIds"/> is the specialties to keep (from the query,
    /// or the viewer's own for "mine"), or null for every one.
    /// </summary>
    public static IQueryable<Referral> Filter(
        this IQueryable<Referral> referrals, SearchReferralsQuery query, IReadOnlyCollection<Guid>? specialtyIds)
    {
        if (query.PatientId is { } patientId)
            referrals = referrals.Where(r => r.PatientId == patientId);

        var statuses = (query.Statuses ?? [])
            .Select(s => PathwayEnums.TryParse<ReferralStatus>(s, out var status) ? status : (ReferralStatus?)null)
            .OfType<ReferralStatus>()
            .Distinct()
            .ToList();
        if (statuses.Count > 0)
            referrals = referrals.Where(r => statuses.Contains(r.Status));

        if (specialtyIds is not null)
        {
            var ids = specialtyIds.Distinct().ToList();
            referrals = referrals.Where(r => ids.Contains(r.SpecialtyId));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            referrals = referrals.Where(r => (r.Patient.FirstName + " " + r.Patient.LastName).ToLower().Contains(term));
        }

        return referrals;
    }

    /// <summary>
    /// Sorts by the date received (the default, oldest first, so the longest wait is at the top), patient, specialty,
    /// requested priority or when it was registered. Ties are broken by the date received, when it was registered,
    /// then ID, so paging never shows a row twice or skips one.
    /// </summary>
    public static IOrderedQueryable<Referral> Sort(this IQueryable<Referral> referrals, string? sort, bool ascending)
    {
        var column = SortColumns.FirstOrDefault(c => string.Equals(c, sort, StringComparison.OrdinalIgnoreCase)) ?? SortByReceivedOn;

        IOrderedQueryable<Referral> ordered = (column, ascending) switch
        {
            (SortByPatient, true) => referrals
                .OrderBy(r => r.Patient.LastName.ToLower())
                .ThenBy(r => r.Patient.FirstName.ToLower())
                .ThenBy(r => r.ReceivedOn),
            (SortByPatient, false) => referrals
                .OrderByDescending(r => r.Patient.LastName.ToLower())
                .ThenByDescending(r => r.Patient.FirstName.ToLower())
                .ThenBy(r => r.ReceivedOn),
            (SortBySpecialty, true) => referrals.OrderBy(r => r.Specialty.Name.ToLower()).ThenBy(r => r.ReceivedOn),
            (SortBySpecialty, false) => referrals.OrderByDescending(r => r.Specialty.Name.ToLower()).ThenBy(r => r.ReceivedOn),
            (SortByPriority, true) => referrals.OrderBy(r => r.RequestedPriority).ThenBy(r => r.ReceivedOn),
            (SortByPriority, false) => referrals.OrderByDescending(r => r.RequestedPriority).ThenBy(r => r.ReceivedOn),
            (SortByRegistered, true) => referrals.OrderBy(r => r.RegisteredAtUtc),
            (SortByRegistered, false) => referrals.OrderByDescending(r => r.RegisteredAtUtc),
            (_, true) => referrals.OrderBy(r => r.ReceivedOn),
            (_, false) => referrals.OrderByDescending(r => r.ReceivedOn),
        };

        return ordered.ThenBy(r => r.RegisteredAtUtc).ThenBy(r => r.Id);
    }
}
