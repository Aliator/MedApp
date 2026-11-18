using MedApp.Application.Pathways.Common;
using MedApp.Domain.Appointments;
using static MedApp.Domain.Appointments.AppointmentStatus;

namespace MedApp.Application.Appointments.Common;

/// <summary>
/// Applies one request's changes to an appointment: sets its status and who did what when, stamps LastUpdated, and
/// collects the history entries (<see cref="AppointmentEvent"/>) to save with the change. Handlers check that a change
/// is allowed first (<see cref="AppointmentWorkflow"/>); this only applies it.
/// <para>
/// Each entry copies what it shows: the pathway's title and stage's name (given, or from the appointment's
/// pathway and stage), the member of staff (<paramref name="staffUsername"/> is the current one's username) and
/// the time, as they are after the change.
/// </para>
/// </summary>
public sealed class AppointmentChange(
    Appointment appointment,
    PathwayActor actor,
    DateTime nowUtc,
    string? staffUsername = null,
    string? pathwayTitle = null,
    string? stageName = null)
{
    private readonly List<AppointmentEvent> _events = [];
    private string? _staffUsername = staffUsername;

    public Appointment Appointment => appointment;

    public IReadOnlyList<AppointmentEvent> Events => _events;

    /// <summary>Whether anything changed (every change is recorded).</summary>
    public bool HasChanges => _events.Count > 0;

    /// <summary>Records that a new Requested appointment was asked for, by someone or by the app (<paramref name="automatic"/>).</summary>
    public void Request(bool automatic = false) =>
        Record(AppointmentEventAction.Requested, automatic ? new { automatic = true } : null);

    /// <summary>
    /// Books a Requested appointment, or moves a Booked one (recorded with its previous time, member of staff, and clinic
    /// session and room if it had them), into a clinic session's slot or a room when <paramref name="place"/> says so
    /// (none: neither, so a move out of a session leaves it). Nothing happens if a Booked one is "moved" to exactly where
    /// it is. The history records the session's and room's IDs, never names.
    /// </summary>
    public void Book(Guid staffUserId, string bookedStaffUsername, DateTime startUtc, DateTime endUtc, AppointmentPlace? place = null)
    {
        place ??= AppointmentPlace.None;
        var wasBooked = appointment.Status == Booked;
        if (wasBooked
            && appointment.StaffUserId == staffUserId
            && appointment.StartUtc == startUtc
            && appointment.EndUtc == endUtc
            && appointment.ClinicSessionId == place.ClinicSessionId
            && appointment.RoomId == place.RoomId)
            return;

        // As the other details, nulls are left out.
        var previous = new Dictionary<string, object>();
        void Add(string key, object? value)
        {
            if (value is not null)
                previous[key] = value;
        }

        Add("previousStartUtc", appointment.StartUtc);
        Add("previousEndUtc", appointment.EndUtc);
        Add("previousStaffUserId", appointment.StaffUserId);
        Add("previousStaffUsername", _staffUsername);
        Add("previousClinicSessionId", appointment.ClinicSessionId);
        Add("previousRoomId", appointment.RoomId);

        appointment.StaffUserId = staffUserId;
        appointment.StartUtc = startUtc;
        appointment.EndUtc = endUtc;
        appointment.ClinicSessionId = place.ClinicSessionId;
        appointment.RoomId = place.RoomId;
        _staffUsername = bookedStaffUsername;

        Add("clinicSessionId", place.ClinicSessionId);
        Add("roomId", place.RoomId);

        if (wasBooked)
        {
            Record(AppointmentEventAction.Rescheduled, previous);
            return;
        }

        appointment.Status = Booked;
        appointment.BookedAtUtc = nowUtc;
        appointment.BookedByUserId = actor.UserId;
        Record(
            AppointmentEventAction.Booked,
            place.ClinicSessionId is null && place.RoomId is null
                ? null
                : new { clinicSessionId = place.ClinicSessionId, roomId = place.RoomId });
    }

    /// <summary>
    /// Records an appointment that has already happened (a walk-in, or one booked outside MedApp), with its member of
    /// staff, time and outcome, as one history entry (<see cref="AppointmentEventAction.RecordedAfterEvent"/>, with the
    /// outcome and, for a stage's visit in Requires Appointment, its previous status). The actor recorded it, now: they're
    /// its booker and the outcome's recorder. A new appointment, or a Requested one (a stage's visit in Requires Appointment,
    /// <paramref name="wasRequested"/>), only.
    /// </summary>
    public void RecordAfterEvent(
        Guid staffUserId,
        string recordedStaffUsername,
        DateTime startUtc,
        DateTime endUtc,
        AppointmentStatus outcome,
        bool wasRequested = false)
    {
        if (!AppointmentWorkflow.IsOutcome(outcome))
            throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "An outcome is Attended or DidNotAttend.");
        if (appointment.Status != Requested)
            throw new InvalidOperationException("Only a new or Requested appointment can be recorded after the event.");

        appointment.StaffUserId = staffUserId;
        appointment.StartUtc = startUtc;
        appointment.EndUtc = endUtc;
        appointment.Status = outcome;
        appointment.IsRecordedAfterEvent = true;
        appointment.BookedAtUtc = nowUtc;
        appointment.BookedByUserId = actor.UserId;
        appointment.OutcomeAtUtc = nowUtc;
        appointment.OutcomeByUserId = actor.UserId;
        _staffUsername = recordedStaffUsername;

        Record(
            AppointmentEventAction.RecordedAfterEvent,
            wasRequested
                ? new { outcome = outcome.ToString(), previousStatus = Requested.ToString() }
                : new { outcome = outcome.ToString() });
    }

    /// <summary>
    /// Withdraws an appointment recorded after the event as entered in error, with a reason (kept as its
    /// <see cref="Appointment.CancelReason"/>, and in the history with the outcome it had). Handlers check it's allowed
    /// (<see cref="AppointmentWorkflow.WhyCantMarkEnteredInError"/>) first.
    /// </summary>
    public void MarkEnteredInError(string reason)
    {
        var previous = appointment.Status;
        appointment.Status = EnteredInError;
        appointment.CancelReason = reason;
        Record(AppointmentEventAction.EnteredInError, new { reason, previousStatus = previous.ToString() });
    }

    /// <summary>Marks it Attended or Did not attend, or corrects one to the other. Nothing happens if it's already that.</summary>
    public void RecordOutcome(AppointmentStatus outcome)
    {
        if (!AppointmentWorkflow.IsOutcome(outcome))
            throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "An outcome is Attended or DidNotAttend.");

        if (appointment.Status == outcome)
            return;

        var previous = appointment.Status;
        appointment.Status = outcome;
        appointment.OutcomeAtUtc = nowUtc;
        appointment.OutcomeByUserId = actor.UserId;
        // A follow-up applies to a Did not attend only: corrected to Attended, it's cleared (the history keeps it).
        if (outcome != DidNotAttend)
            ClearDnaFollowUp();
        Record(
            outcome == Attended ? AppointmentEventAction.Attended : AppointmentEventAction.DidNotAttend,
            new { previousStatus = previous.ToString() });
    }

    /// <summary>
    /// Records what booking staff decided after a Did not attend (centre plan decision 132), with an optional detail of
    /// free text (kept on the appointment and in the history, which shows it to admins only). Nothing happens if the same
    /// decision and detail are recorded again. It changes nothing else: never the pathway.
    /// </summary>
    public void RecordDnaFollowUp(DnaFollowUpDecision decision, string? detail)
    {
        if (appointment.DnaFollowUp == decision && appointment.DnaFollowUpDetail == detail)
            return;

        appointment.DnaFollowUp = decision;
        appointment.DnaFollowUpDetail = detail;
        appointment.DnaFollowUpAtUtc = nowUtc;
        appointment.DnaFollowUpByUserId = actor.UserId;
        Record(AppointmentEventAction.DnaFollowUpRecorded, new { decision = decision.ToString(), detail });
    }

    private void ClearDnaFollowUp()
    {
        appointment.DnaFollowUp = null;
        appointment.DnaFollowUpDetail = null;
        appointment.DnaFollowUpAtUtc = null;
        appointment.DnaFollowUpByUserId = null;
    }

    /// <summary>Cancels it with a reason, by someone or by the app (<paramref name="automatic"/>).</summary>
    public void Cancel(string reason, bool automatic = false)
    {
        appointment.Status = Cancelled;
        appointment.CancelReason = reason;
        Record(AppointmentEventAction.Cancelled, new { reason, automatic });
    }

    /// <summary>Replaces its notes. Nothing happens if they're unchanged.</summary>
    public void UpdateNotes(string notes)
    {
        if (appointment.Notes == notes)
            return;

        var previous = appointment.Notes;
        appointment.Notes = notes;
        Record(AppointmentEventAction.NotesUpdated, new { from = previous, to = notes });
    }

    /// <summary>
    /// Records a history entry. Entries one change writes are each a tick (100 ns) after the one before, so they sort
    /// in the order they happened, as the pathway audit log's do.
    /// </summary>
    private void Record(AppointmentEventAction action, object? details = null)
    {
        _events.Add(new AppointmentEvent
        {
            Id = Guid.NewGuid(),
            AppointmentId = appointment.Id,
            PatientId = appointment.PatientId,
            PathwayId = appointment.PathwayId,
            PathwayTitle = appointment.PathwayId is null
                ? string.Empty
                : pathwayTitle ?? appointment.Pathway?.Title ?? string.Empty,
            StageName = appointment.StageId is null ? null : stageName ?? appointment.Stage?.Name,
            Action = action,
            UserId = actor.UserId,
            Username = actor.Username,
            AtUtc = nowUtc.AddTicks(_events.Count),
            StaffUserId = appointment.StaffUserId,
            StaffUsername = appointment.StaffUserId is null ? null : _staffUsername,
            StartUtc = appointment.StartUtc,
            EndUtc = appointment.EndUtc,
            Details = PathwayAudit.Details(details)
        });
        appointment.LastUpdated = nowUtc;
    }
}
