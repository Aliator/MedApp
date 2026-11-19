using MedApp.Application.Common.Teams;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Appointments;
using MedApp.Domain.Teams;

namespace MedApp.Application.Appointments.Queries.SearchAppointments;

/// <summary>
/// The filtering and sorting behind <see cref="SearchAppointmentsQuery"/>, written as plain LINQ so the repository
/// runs it in SQL and the unit tests run it in memory (see <c>PatientSearch</c>). Text matches ignore case either way.
/// </summary>
public static class AppointmentSearch
{
    public const string SortByStart = "start";
    public const string SortByRequested = "requested";
    public const string SortByDueDate = "dueDate";
    public const string SortByPatient = "patient";

    public static readonly IReadOnlyList<string> SortColumns = [SortByStart, SortByRequested, SortByDueDate, SortByPatient];

    /// <summary>The Did not attend list (centre plan decision 132).</summary>
    public const string DidNotAttendView = "didNotAttend";

    public static readonly IReadOnlyList<string> Views = [DidNotAttendView];

    public static bool IsView(string view) => Views.Contains(view, StringComparer.OrdinalIgnoreCase);

    public static bool IsDidNotAttendView(string? view) => string.Equals(view, DidNotAttendView, StringComparison.OrdinalIgnoreCase);

    public static string StatusNames => string.Join(", ", Enum.GetNames<AppointmentStatus>());

    public static bool IsSortColumn(string sort) => SortColumns.Contains(sort, StringComparer.OrdinalIgnoreCase);

    public static bool IsStatus(string? value) => PathwayEnums.TryParse<AppointmentStatus>(value, out _);

    /// <summary>
    /// Appointments the viewer sees (the team walls, <see cref="TeamAccess.ByPathway{T}"/>: its own team theirs, or its
    /// pathway seen through <paramref name="grants"/>, the grants table) matching every filter given, narrowed to the
    /// clinic picker's team (<see cref="SearchAppointmentsQuery.TeamId"/>, the appointment's own team). The Did not
    /// attend view needs <paramref name="all"/>, every appointment, to look for later ones (<see cref="AwaitingFollowUp"/>).
    /// </summary>
    public static IQueryable<Appointment> Filter(
        this IQueryable<Appointment> appointments,
        SearchAppointmentsQuery query,
        IQueryable<Appointment>? all = null,
        IQueryable<PathwayAccess>? grants = null)
    {
        appointments = appointments.Where(TeamAccess.ByPathway<Appointment>(
            query.Teams, grants ?? Enumerable.Empty<PathwayAccess>().AsQueryable(), a => a.PathwayId, a => a.TeamId));
        if (query.TeamId is { } teamId)
            appointments = appointments.Where(a => a.TeamId == teamId);

        if (IsDidNotAttendView(query.View))
            appointments = appointments.AwaitingFollowUp(all ?? appointments);

        if (query.PatientId is { } patientId)
            appointments = appointments.Where(a => a.PatientId == patientId);
        if (query.PathwayId is { } pathwayId)
            appointments = appointments.Where(a => a.PathwayId == pathwayId);
        if (query.StageId is { } stageId)
            appointments = appointments.Where(a => a.StageId == stageId);
        if (query.StaffUserId is { } staffUserId)
            appointments = appointments.Where(a => a.StaffUserId == staffUserId);

        var statuses = (query.Statuses ?? [])
            .Select(s => PathwayEnums.TryParse<AppointmentStatus>(s, out var status) ? status : (AppointmentStatus?)null)
            .OfType<AppointmentStatus>()
            .Distinct()
            .ToList();
        if (statuses.Count > 0)
            appointments = appointments.Where(a => statuses.Contains(a.Status));

        // UK days, as UTC moments: from the start of the first to the start of the day after the last.
        if (query.From is { } from)
        {
            var fromUtc = DueDates.StartOfDayUtc(from);
            appointments = appointments.Where(a => a.StartUtc >= fromUtc);
        }

        if (query.To is { } to)
        {
            var beforeUtc = DueDates.StartOfDayUtc(to.AddDays(1));
            appointments = appointments.Where(a => a.StartUtc < beforeUtc);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            appointments = appointments.Where(a => (a.Patient.FirstName + " " + a.Patient.LastName).ToLower().Contains(term));
        }

        return appointments;
    }

    /// <summary>
    /// The Did not attend list (centre plan decision 132, hazard H-55): appointments marked Did not attend with no later
    /// appointment for the same thing (another not cancelled or entered in error, starting after it, or still to be
    /// booked and asked for after it), and no decision of No further appointment. "The same thing" is the stage, for an
    /// appointment for one; else the pathway; else the patient's appointments not for a pathway. Rebook and Clinician to
    /// review stay on the list until the later appointment exists, so neither can be forgotten. Nothing here ever
    /// changes a pathway.
    /// </summary>
    public static IQueryable<Appointment> AwaitingFollowUp(this IQueryable<Appointment> appointments, IQueryable<Appointment> all) =>
        appointments.Where(a =>
            a.Status == AppointmentStatus.DidNotAttend
            && a.DnaFollowUp != DnaFollowUpDecision.NoFurtherAppointment
            && !all.Any(o =>
                o.Id != a.Id
                && o.PatientId == a.PatientId
                && o.Status != AppointmentStatus.Cancelled
                && o.Status != AppointmentStatus.EnteredInError
                && ((a.StageId != null && o.StageId == a.StageId)
                    || (a.StageId == null && a.PathwayId != null && o.PathwayId == a.PathwayId)
                    || (a.StageId == null && a.PathwayId == null && o.PathwayId == null))
                && (o.StartUtc != null ? o.StartUtc > a.StartUtc : o.CreatedAtUtc > a.StartUtc)));

    /// <summary>
    /// Sorts by start (the default; Requested ones, which have none, last, by when they were asked for), when they
    /// were asked for, the pathway's target date (none last) or patient. Ties are broken by when they were asked for,
    /// then ID, so paging never shows a row twice or skips one.
    /// </summary>
    public static IOrderedQueryable<Appointment> Sort(this IQueryable<Appointment> appointments, string? sort, bool ascending)
    {
        var column = SortColumns.FirstOrDefault(c => string.Equals(c, sort, StringComparison.OrdinalIgnoreCase)) ?? SortByStart;

        IOrderedQueryable<Appointment> ordered = (column, ascending) switch
        {
            (SortByRequested, true) => appointments.OrderBy(a => a.CreatedAtUtc),
            (SortByRequested, false) => appointments.OrderByDescending(a => a.CreatedAtUtc),
            (SortByDueDate, true) => appointments
                .OrderBy(a => a.Pathway == null ? 1 : 0)
                .ThenBy(a => a.Pathway == null ? DateTime.MaxValue : a.Pathway.DueDateUtc)
                .ThenBy(a => a.CreatedAtUtc),
            (SortByDueDate, false) => appointments
                .OrderBy(a => a.Pathway == null ? 1 : 0)
                .ThenByDescending(a => a.Pathway == null ? DateTime.MinValue : a.Pathway.DueDateUtc)
                .ThenBy(a => a.CreatedAtUtc),
            (SortByPatient, true) => appointments
                .OrderBy(a => a.Patient.LastName.ToLower())
                .ThenBy(a => a.Patient.FirstName.ToLower())
                .ThenBy(a => a.CreatedAtUtc),
            (SortByPatient, false) => appointments
                .OrderByDescending(a => a.Patient.LastName.ToLower())
                .ThenByDescending(a => a.Patient.FirstName.ToLower())
                .ThenBy(a => a.CreatedAtUtc),
            (_, true) => appointments
                .OrderBy(a => a.StartUtc == null ? 1 : 0)
                .ThenBy(a => a.StartUtc)
                .ThenBy(a => a.CreatedAtUtc),
            (_, false) => appointments
                .OrderBy(a => a.StartUtc == null ? 1 : 0)
                .ThenByDescending(a => a.StartUtc)
                .ThenBy(a => a.CreatedAtUtc),
        };

        return ordered.ThenBy(a => a.Id);
    }
}
