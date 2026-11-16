using MedApp.Application.Common.Identity;
using MedApp.Application.Procedures.Common;
using MedApp.Contracts.Appointments.Responses;
using MedApp.Domain.Appointments;

namespace MedApp.Application.Appointments.Common;

public static class AppointmentMapper
{
    /// <summary>Every user an appointment's response names: who asked for it, booked it, recorded its outcome, and who it's with.</summary>
    public static IEnumerable<Guid> UserIds(Appointment appointment) =>
        new[] { appointment.CreatedByUserId, appointment.StaffUserId, appointment.BookedByUserId, appointment.OutcomeByUserId }
            .OfType<Guid>()
            .Distinct();

    /// <summary>An appointment's response, looking up the usernames it shows.</summary>
    public static async Task<AppointmentResponse> ToResponseAsync(
        Appointment appointment,
        IIdentityReadService identity,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var usernames = await identity.GetUsernamesByIdAsync(UserIds(appointment), ct);
        return ToResponse(appointment, usernames, nowUtc);
    }

    /// <summary>Appointments' responses, looking up every username they show at once.</summary>
    public static async Task<List<AppointmentResponse>> ToResponsesAsync(
        IEnumerable<Appointment> appointments,
        IIdentityReadService identity,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var list = appointments.ToList();
        var usernames = await identity.GetUsernamesByIdAsync(list.SelectMany(UserIds).Distinct(), ct);
        return list.Select(a => ToResponse(a, usernames, nowUtc)).ToList();
    }

    /// <summary>
    /// An appointment's response, with its patient, procedure and stage (when loaded) and the usernames given.
    /// What can be done with it is worked out at <paramref name="nowUtc"/>.
    /// </summary>
    public static AppointmentResponse ToResponse(
        Appointment appointment,
        IReadOnlyDictionary<Guid, string> usernames,
        DateTime nowUtc) => new()
    {
        Id = appointment.Id,
        PatientId = appointment.PatientId,
        PatientName = ProcedureMapper.PatientName(appointment.Patient),
        ProcedureId = appointment.ProcedureId,
        ProcedureTitle = appointment.Procedure?.Title,
        ProcedureDueDate = appointment.Procedure is { } procedure ? DueDates.ToUkDate(procedure.DueDateUtc) : null,
        StageId = appointment.StageId,
        StageName = appointment.Stage?.Name,
        VisitRequirement = appointment.Stage?.VisitRequirement.ToString(),
        Status = appointment.Status.ToString(),
        StaffUserId = appointment.StaffUserId,
        StaffUsername = Username(usernames, appointment.StaffUserId),
        StartUtc = appointment.StartUtc,
        EndUtc = appointment.EndUtc,
        DurationMinutes = appointment is { StartUtc: { } start, EndUtc: { } end } ? (int)(end - start).TotalMinutes : null,
        Notes = appointment.Notes,
        CancelReason = appointment.CancelReason,
        IsAutomatic = appointment.IsAutomatic,
        CreatedAtUtc = appointment.CreatedAtUtc,
        CreatedByUsername = Username(usernames, appointment.CreatedByUserId),
        BookedAtUtc = appointment.BookedAtUtc,
        BookedByUsername = Username(usernames, appointment.BookedByUserId),
        OutcomeAtUtc = appointment.OutcomeAtUtc,
        OutcomeByUsername = Username(usernames, appointment.OutcomeByUserId),
        LastUpdated = appointment.LastUpdated,
        CanBook = AppointmentWorkflow.CanBook(appointment),
        CanReschedule = AppointmentWorkflow.CanReschedule(appointment),
        CanRecordOutcome = AppointmentWorkflow.CanRecordOutcome(appointment, nowUtc),
        CanCancel = AppointmentWorkflow.CanCancel(appointment),
        CanEditNotes = AppointmentWorkflow.CanEditNotes(appointment)
    };

    private static string Username(IReadOnlyDictionary<Guid, string> usernames, Guid userId) =>
        usernames.TryGetValue(userId, out var name) ? name : ProcedureMapper.DeletedUser;

    private static string? Username(IReadOnlyDictionary<Guid, string> usernames, Guid? userId) =>
        userId is { } id ? Username(usernames, id) : null;
}
