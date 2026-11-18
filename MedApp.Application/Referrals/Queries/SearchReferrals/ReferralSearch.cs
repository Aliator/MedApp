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

    /// <summary>Days waited on the clock (the inbox's "longest wait first" is this, descending).</summary>
    public const string SortByDaysWaited = "daysWaited";

    /// <summary>The clock's target date.</summary>
    public const string SortByTargetOn = "targetOn";

    /// <summary>The open pause's review date (the Paused clocks view); clocks not paused last.</summary>
    public const string SortByReviewOn = "reviewOn";

    public static readonly IReadOnlyList<string> SortColumns =
        [SortByReceivedOn, SortByPatient, SortBySpecialty, SortByPriority, SortByRegistered, SortByDaysWaited, SortByTargetOn, SortByReviewOn];

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
        if (query.BreachedOnly)
            used.Add("breachedOnly");
        if (query.Paused is not null)
            used.Add("paused");
        if (query.ClockStopped is not null)
            used.Add("clockStopped");
        return used;
    }

    /// <summary>
    /// Referrals matching every filter given. <paramref name="specialtyIds"/> is the specialties to keep (from the query,
    /// or the viewer's own for "mine"), or null for every one. The clock's filters count to <paramref name="today"/> (UK).
    /// </summary>
    public static IQueryable<Referral> Filter(
        this IQueryable<Referral> referrals, SearchReferralsQuery query, IReadOnlyCollection<Guid>? specialtyIds, DateOnly today = default)
    {
        // The same rules as WaitingClock's, in a form EF can translate.
        if (query.BreachedOnly)
            referrals = referrals.Where(r => (r.StoppedOn ?? r.PausedSinceOn ?? today) > r.TargetOn);

        if (query.Paused is { } paused)
            referrals = paused
                ? referrals.Where(r => r.StoppedOn == null && r.PausedSinceOn != null)
                : referrals.Where(r => r.StoppedOn != null || r.PausedSinceOn == null);

        if (query.ClockStopped is { } stopped)
            referrals = stopped
                ? referrals.Where(r => r.StoppedOn != null)
                : referrals.Where(r => r.StoppedOn == null);

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
    /// requested priority, when it was registered, days waited on the clock (to <paramref name="today"/>), the clock's
    /// target date, or the open pause's review date (clocks not paused last). Ties are broken by the date received,
    /// when it was registered, then ID, so paging never shows a row twice or skips one. Every order is by dates or
    /// labels staff set, never anything clinical.
    /// </summary>
    public static IOrderedQueryable<Referral> Sort(
        this IQueryable<Referral> referrals, string? sort, bool ascending, DateOnly today = default)
    {
        var column = SortColumns.FirstOrDefault(c => string.Equals(c, sort, StringComparison.OrdinalIgnoreCase)) ?? SortByReceivedOn;

        IOrderedQueryable<Referral> ordered = (column, ascending) switch
        {
            (SortByDaysWaited, true) => referrals
                .OrderBy(r => (r.StoppedOn ?? r.PausedSinceOn ?? today).DayNumber - r.WaitStartOn.DayNumber)
                .ThenBy(r => r.ReceivedOn),
            (SortByDaysWaited, false) => referrals
                .OrderByDescending(r => (r.StoppedOn ?? r.PausedSinceOn ?? today).DayNumber - r.WaitStartOn.DayNumber)
                .ThenBy(r => r.ReceivedOn),
            (SortByTargetOn, true) => referrals.OrderBy(r => r.TargetOn).ThenBy(r => r.ReceivedOn),
            (SortByTargetOn, false) => referrals.OrderByDescending(r => r.TargetOn).ThenBy(r => r.ReceivedOn),
            (SortByReviewOn, true) => referrals
                .OrderBy(r => r.PauseReviewOn == null ? 1 : 0)
                .ThenBy(r => r.PauseReviewOn)
                .ThenBy(r => r.ReceivedOn),
            (SortByReviewOn, false) => referrals
                .OrderBy(r => r.PauseReviewOn == null ? 1 : 0)
                .ThenByDescending(r => r.PauseReviewOn)
                .ThenBy(r => r.ReceivedOn),
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
