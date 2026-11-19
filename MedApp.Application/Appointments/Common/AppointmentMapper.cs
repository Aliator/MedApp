using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Teams;
using MedApp.Application.Patients.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Appointments.Responses;
using MedApp.Domain.Appointments;
using MedApp.Domain.Patients;

namespace MedApp.Application.Appointments.Common;

public static class AppointmentMapper
{
    /// <summary>Every user an appointment's response names: who asked for it, booked it, recorded its outcome, and who it's with.</summary>
    public static IEnumerable<Guid> UserIds(Appointment appointment) =>
        new[]
            {
                appointment.CreatedByUserId, appointment.StaffUserId, appointment.BookedByUserId, appointment.OutcomeByUserId,
                appointment.DnaFollowUpByUserId
            }
            .OfType<Guid>()
            .Distinct();

    /// <summary>An appointment's response, looking up the usernames it shows.</summary>
    public static async Task<AppointmentResponse> ToResponseAsync(
        Appointment appointment,
        IIdentityReadService identity,
        DateTime nowUtc,
        CancellationToken ct,
        TeamScope? viewer = null)
    {
        var usernames = await identity.GetUsernamesByIdAsync(UserIds(appointment), ct);
        return ToResponse(appointment, usernames, nowUtc, viewer);
    }

    /// <summary>Appointments' responses, looking up every username they show at once.</summary>
    public static async Task<List<AppointmentResponse>> ToResponsesAsync(
        IEnumerable<Appointment> appointments,
        IIdentityReadService identity,
        DateTime nowUtc,
        CancellationToken ct,
        TeamScope? viewer = null)
    {
        var list = appointments.ToList();
        var usernames = await identity.GetUsernamesByIdAsync(list.SelectMany(UserIds).Distinct(), ct);
        return list.Select(a => ToResponse(a, usernames, nowUtc, viewer)).ToList();
    }

    /// <summary>
    /// An appointment's response, with its patient, pathway and stage (when loaded) and the usernames given.
    /// What can be done with it is worked out at <paramref name="nowUtc"/>: nothing but cancelling while its patient's
    /// record is restricted, and nothing at all once it's anonymised (<see cref="PatientRules"/>); and only by its own
    /// team (the team walls, MC5: <paramref name="viewer"/>'s; admins and other teams that see it only read it). No viewer
    /// is the system's view.
    /// </summary>
    public static AppointmentResponse ToResponse(
        Appointment appointment,
        IReadOnlyDictionary<Guid, string> usernames,
        DateTime nowUtc,
        TeamScope? viewer = null)
    {
        var ownTeam = viewer is null || TeamAccess.CanChangeTeam(appointment.TeamId, viewer);
        var canChange = ownTeam && PatientRules.CanChange(appointment.Patient);
        var canPause = ownTeam && !PatientRules.IsAnonymised(appointment.Patient);
        return new AppointmentResponse
        {
            Id = appointment.Id,
            PatientId = appointment.PatientId,
            PatientName = PathwayMapper.PatientName(appointment.Patient),
            PathwayId = appointment.PathwayId,
            PathwayTitle = appointment.Pathway?.Title,
            PathwayDueDate = appointment.Pathway is { } pathway ? DueDates.ToUkDate(pathway.DueDateUtc) : null,
            StageId = appointment.StageId,
            StageName = appointment.Stage?.Name,
            VisitRequirement = appointment.Stage?.VisitRequirement.ToString(),
            Status = appointment.Status.ToString(),
            StaffUserId = appointment.StaffUserId,
            StaffUsername = Username(usernames, appointment.StaffUserId),
            StartUtc = appointment.StartUtc,
            EndUtc = appointment.EndUtc,
            DurationMinutes = appointment is { StartUtc: { } start, EndUtc: { } end } ? (int)(end - start).TotalMinutes : null,
            ClinicSessionId = appointment.ClinicSessionId,
            RoomId = appointment.RoomId,
            Notes = appointment.Notes,
            CancelReason = appointment.CancelReason,
            IsAutomatic = appointment.IsAutomatic,
            CreatedAtUtc = appointment.CreatedAtUtc,
            CreatedByUsername = Username(usernames, appointment.CreatedByUserId),
            IsRecordedAfterEvent = appointment.IsRecordedAfterEvent,
            BookedAtUtc = appointment.BookedAtUtc,
            BookedByUsername = Username(usernames, appointment.BookedByUserId),
            OutcomeAtUtc = appointment.OutcomeAtUtc,
            OutcomeByUsername = Username(usernames, appointment.OutcomeByUserId),
            PatientReminderChannel = (appointment.Patient?.ReminderChannel ?? ReminderChannel.None).ToString(),
            DnaFollowUp = appointment.DnaFollowUp?.ToString(),
            DnaFollowUpDetail = appointment.DnaFollowUpDetail,
            DnaFollowUpAtUtc = appointment.DnaFollowUpAtUtc,
            DnaFollowUpByUsername = Username(usernames, appointment.DnaFollowUpByUserId),
            LastUpdated = appointment.LastUpdated,
            CanBook = canChange && AppointmentWorkflow.CanBook(appointment),
            CanReschedule = canChange && AppointmentWorkflow.CanReschedule(appointment),
            CanRecordOutcome = canChange && AppointmentWorkflow.CanRecordOutcome(appointment, nowUtc),
            CanCancel = canPause && AppointmentWorkflow.CanCancel(appointment),
            CanEditNotes = canChange && AppointmentWorkflow.CanEditNotes(appointment),
            CanRecordDnaFollowUp = canChange && AppointmentWorkflow.CanRecordDnaFollowUp(appointment),
            CanMarkEnteredInError = canPause && AppointmentWorkflow.CanMarkEnteredInError(appointment, nowUtc, isAdmin: true),
            CanRecorderMarkEnteredInError = canPause && AppointmentWorkflow.CanMarkEnteredInError(appointment, nowUtc, isAdmin: false)
        };
    }

    private static string Username(IReadOnlyDictionary<Guid, string> usernames, Guid userId) =>
        usernames.TryGetValue(userId, out var name) ? name : PathwayMapper.DeletedUser;

    private static string? Username(IReadOnlyDictionary<Guid, string> usernames, Guid? userId) =>
        userId is { } id ? Username(usernames, id) : null;
}
