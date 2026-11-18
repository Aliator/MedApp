using System.Text.RegularExpressions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Procedures.Common;
using MedApp.Domain.Appointments;
using MedApp.Domain.Procedures.PatientProcedures;
using static MedApp.Domain.Appointments.AppointmentStatus;

namespace MedApp.Application.Appointments.Common;

/// <summary>
/// The rules of an appointment's statuses.
/// <para>
/// Requested (Requires appointment) has no member of staff or time. Booking gives it both (in the future, clash-free), and a
/// Booked one can be moved the same way. Once its start has passed, it's marked Attended or Did not attend, which can
/// be corrected either way. Requested and Booked ones can be Cancelled, with a reason; Cancelled is final. An
/// appointment that already happened (a walk-in) is recorded after the event straight to Attended or Did not attend,
/// within <see cref="BookingTimes.RecordAfterEventDays"/> days; a stage's visit in Requires Appointment becomes that one.
/// One recorded in error is withdrawn (Entered in error, with a reason) by its recorder or an admin, in the same window.
/// </para>
/// </summary>
public static partial class AppointmentWorkflow
{
    /// <summary>Still to happen: a stage can have one of these at a time.</summary>
    public static readonly IReadOnlyList<AppointmentStatus> OpenStatuses = [Requested, Booked];

    /// <summary>On a member of staff's calendar, taking up their time.</summary>
    public static readonly IReadOnlyList<AppointmentStatus> OnCalendarStatuses = [Booked, Attended, DidNotAttend];

    /// <summary>The outcomes that can be recorded.</summary>
    public static readonly IReadOnlyList<AppointmentStatus> Outcomes = [Attended, DidNotAttend];

    public static string OutcomeNames => string.Join(", ", Outcomes);

    public static bool IsOpen(AppointmentStatus status) => OpenStatuses.Contains(status);

    public static bool IsOnCalendar(AppointmentStatus status) => OnCalendarStatuses.Contains(status);

    public static bool IsOutcome(AppointmentStatus status) => Outcomes.Contains(status);

    /// <summary>A Requested one can be booked.</summary>
    public static bool CanBook(Appointment appointment) => appointment.Status == Requested;

    /// <summary>A Booked one can be moved to another time or member of staff.</summary>
    public static bool CanReschedule(Appointment appointment) => appointment.Status == Booked;

    /// <summary>Booked (or already marked) and started by <paramref name="nowUtc"/>.</summary>
    public static bool CanRecordOutcome(Appointment appointment, DateTime nowUtc) =>
        appointment.Status is Booked or Attended or DidNotAttend
        && appointment.StartUtc is { } start
        && start <= nowUtc;

    public static bool CanCancel(Appointment appointment) => IsOpen(appointment.Status);

    public static bool CanEditNotes(Appointment appointment) => appointment.Status is not (Cancelled or EnteredInError);

    /// <summary>
    /// One recorded after the event can be withdrawn as entered in error (it didn't happen, or it's on the wrong
    /// patient) while it still has its outcome and its start is within the window it could be recorded in
    /// (<see cref="BookingTimes.RecordAfterEventDays"/> UK days). Who may (its recorder, or an admin) is checked apart.
    /// </summary>
    public static bool CanMarkEnteredInError(Appointment appointment, DateTime nowUtc) =>
        WhyCantMarkEnteredInError(appointment, nowUtc) is null;

    /// <summary>Why an appointment can't be marked entered in error now, or null if it can.</summary>
    public static string? WhyCantMarkEnteredInError(Appointment appointment, DateTime nowUtc)
    {
        if (!appointment.IsRecordedAfterEvent)
            return "Only an appointment recorded after the event can be marked entered in error. Cancel a booked one, or correct its outcome.";

        if (!IsOutcome(appointment.Status))
            return $"An appointment that's {Describe(appointment.Status)} can't be marked entered in error.";

        return appointment.StartUtc is { } start
               && UkTime.DateOf(start) >= UkTime.DateOf(nowUtc).AddDays(-BookingTimes.RecordAfterEventDays)
            ? null
            : $"An appointment recorded after the event can be marked entered in error for up to {BookingTimes.RecordAfterEventDays} days after it. Ask an admin to correct the record.";
    }

    /// <summary>Only whoever recorded it after the event, or an admin, can withdraw it.</summary>
    public const string NotYourRecordMessage =
        "Only whoever recorded this appointment, or an admin, can mark it entered in error.";

    /// <summary>Why an outcome can't be recorded now, or null if it can.</summary>
    public static string? WhyOutcomeCantBeRecorded(Appointment appointment, DateTime nowUtc)
    {
        if (appointment.Status is not (Booked or Attended or DidNotAttend))
            return $"An appointment that's {Describe(appointment.Status)} has no outcome to record.";

        return CanRecordOutcome(appointment, nowUtc)
            ? null
            : "Whether the patient attended can be recorded once the appointment has started.";
    }

    /// <summary>A save refused because someone else saved the appointment first (its row version changed).</summary>
    public const string ChangedMeanwhileMessage =
        "Someone else changed this appointment just now. Reload it and try again.";

    /// <summary>A procedure change refused because someone else saved one of its appointments first.</summary>
    public const string ProcedureAppointmentChangedMeanwhileMessage =
        "Someone else changed one of this procedure's appointments just now. Reload the procedure and try again.";

    /// <summary>A booking of a Requested appointment that someone else booked first.</summary>
    public const string BookedMeanwhileMessage =
        "Someone else has already booked this appointment. Reload it to see when; reschedule it from there if it needs to move.";

    /// <summary>A procedure change whose new visit lost a race with another one for the same stage.</summary>
    public const string StageVisitAddedMeanwhileMessage =
        "Someone else asked for a visit for this stage just now. Reload the procedure and try again.";

    /// <summary>
    /// Why a stage can't have another open appointment, pointing to the one it has (decision 18): when and with whom
    /// it's booked, or that it's in Requires Appointment. <paramref name="open"/> is null if it was closed meanwhile.
    /// </summary>
    public static string StageHasOpenVisit(Appointment? open, string? staffUsername) => open switch
    {
        { Status: Booked, StartUtc: { } start } =>
            $"This stage already has a visit booked, on {UkTime.Describe(start)}{(staffUsername is null ? "" : $" with {staffUsername}")}. " +
            "Reschedule or cancel that one instead.",
        { Status: Requested } =>
            "This stage already has a visit in Requires Appointment. Book that one instead.",
        _ => "This stage already has a visit requested or booked. Reload it and try again."
    };

    /// <summary>
    /// Why an appointment can't be recorded after the event for a stage that has a visit Booked (one in Requires Appointment
    /// becomes the recorded one instead): record that one's outcome, or cancel it, so the stage isn't left with two.
    /// </summary>
    public static string StageHasBookedVisit(Appointment booked, string? staffUsername) =>
        $"This stage already has a visit booked, on {UkTime.Describe(booked.StartUtc!.Value)}" +
        $"{(staffUsername is null ? "" : $" with {staffUsername}")}. " +
        "If that's the one that happened, record its outcome instead; otherwise cancel it first.";

    /// <summary>A walk-in for a procedure, from before the procedure was created (decision 74).</summary>
    public static string BeforeProcedureMessage(DateTime procedureCreatedUtc) =>
        $"This visit was before the procedure was created ({UkTime.Describe(procedureCreatedUtc)}). " +
        "Record it without the procedure, or for the procedure it was for.";

    /// <summary>
    /// Why a walk-in can't be for this stage: one after the procedure's current stage hasn't been reached yet (decision
    /// 77). The current stage and earlier ones can have one; so can any stage once every stage is finished.
    /// </summary>
    public static string? WhyStageNotReached(PatientProcedure procedure, PatientProcedureStage stage) =>
        ProcedureWorkflow.CurrentStage(procedure) is { } current && stage.StageOrder > current.StageOrder
            ? $"The '{stage.Name}' stage hasn't been reached yet: the current stage is '{current.Name}'. Record the visit for that stage, or without one."
            : null;

    /// <summary>
    /// A waiting visit's notes once a walk-in's are added after them (a blank line between), or null when nothing new
    /// was given. A 409 if together they're over <see cref="AppointmentLimits.MaxNotesLength"/> characters.
    /// </summary>
    public static string? KeepingNotes(string existing, string? added)
    {
        if (string.IsNullOrWhiteSpace(added))
            return null;

        var notes = string.IsNullOrWhiteSpace(existing) ? added : $"{existing}\n\n{added}";
        return notes.Length <= AppointmentLimits.MaxNotesLength
            ? notes
            : throw new ConflictException(
                $"The visit's notes and these together are over {AppointmentLimits.MaxNotesLength} characters. Shorten these: the visit's own notes are kept.");
    }

    /// <summary>A stage's visit in Requires Appointment that someone booked or cancelled while it was being recorded.</summary>
    public const string RequestedVisitChangedMeanwhileMessage =
        "Someone else changed this stage's visit just now. Reload it and try again.";

    /// <summary>A status in words, e.g. "did not attend".</summary>
    public static string Describe(AppointmentStatus status) =>
        WordBoundary().Replace(status.ToString(), " $1").ToLowerInvariant();

    [GeneratedRegex("(?<=[a-z])([A-Z])")]
    private static partial Regex WordBoundary();
}
