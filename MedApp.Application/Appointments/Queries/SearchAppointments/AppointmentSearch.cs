using MedApp.Application.Procedures.Common;
using MedApp.Domain.Appointments;

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

    public static string StatusNames => string.Join(", ", Enum.GetNames<AppointmentStatus>());

    public static bool IsSortColumn(string sort) => SortColumns.Contains(sort, StringComparer.OrdinalIgnoreCase);

    public static bool IsStatus(string? value) => ProcedureEnums.TryParse<AppointmentStatus>(value, out _);

    /// <summary>Appointments matching every filter given.</summary>
    public static IQueryable<Appointment> Filter(this IQueryable<Appointment> appointments, SearchAppointmentsQuery query)
    {
        if (query.PatientId is { } patientId)
            appointments = appointments.Where(a => a.PatientId == patientId);
        if (query.ProcedureId is { } procedureId)
            appointments = appointments.Where(a => a.ProcedureId == procedureId);
        if (query.StageId is { } stageId)
            appointments = appointments.Where(a => a.StageId == stageId);
        if (query.StaffUserId is { } staffUserId)
            appointments = appointments.Where(a => a.StaffUserId == staffUserId);

        var statuses = (query.Statuses ?? [])
            .Select(s => ProcedureEnums.TryParse<AppointmentStatus>(s, out var status) ? status : (AppointmentStatus?)null)
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
    /// Sorts by start (the default; Requested ones, which have none, last, by when they were asked for), when they
    /// were asked for, the procedure's due date (none last) or patient. Ties are broken by when they were asked for,
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
                .OrderBy(a => a.Procedure == null ? 1 : 0)
                .ThenBy(a => a.Procedure == null ? DateTime.MaxValue : a.Procedure.DueDateUtc)
                .ThenBy(a => a.CreatedAtUtc),
            (SortByDueDate, false) => appointments
                .OrderBy(a => a.Procedure == null ? 1 : 0)
                .ThenByDescending(a => a.Procedure == null ? DateTime.MinValue : a.Procedure.DueDateUtc)
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
