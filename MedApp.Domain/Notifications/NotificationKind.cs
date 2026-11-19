namespace MedApp.Domain.Notifications;

/// <summary>
/// What a notification is about. Stored as its number, so never renumber these. 1 to 9 are kept for the calendar's
/// kinds; the monitoring kinds (one of an admin's rules matched) are 10 to 17 and 32 to 38, one for each <c>MonitoringEvent</c>;
/// 18 to 21 are about deleting a pathway's note; 22 is a stage's visit withdrawn.
/// </summary>
public enum NotificationKind
{
    /// <summary>Someone assigned the user to a pathway.</summary>
    PathwayAssigned = 0,

    /// <summary>Someone booked an appointment with the user, or moved one to them.</summary>
    AppointmentBooked = 1,

    /// <summary>Someone moved one of the user's appointments to another time.</summary>
    AppointmentRescheduled = 2,

    /// <summary>Someone cancelled one of the user's appointments, or moved it to someone else.</summary>
    AppointmentCancelled = 3,

    /// <summary>An admin invited the user to a meeting, or added them to one.</summary>
    MeetingInvited = 4,

    /// <summary>An admin moved a meeting the user is going to another time.</summary>
    MeetingChanged = 5,

    /// <summary>An admin cancelled a meeting the user was going to, or took them off it.</summary>
    MeetingCancelled = 6,

    /// <summary>Someone declined a meeting the user arranged.</summary>
    MeetingDeclined = 7,

    /// <summary>
    /// An admin closed a day (a bank holiday, a training day) on which the user has appointments, blocked time or
    /// meetings. They stay; the user moves or cancels them.
    /// </summary>
    DayClosed = 8,

    /// <summary>
    /// Someone recorded an appointment with the user after the event (a walk-in, or one booked outside MedApp).
    /// </summary>
    AppointmentRecorded = 9,

    /// <summary>Monitoring: someone created a pathway.</summary>
    MonitoredPathwayCreated = 10,

    /// <summary>Monitoring: someone finished a stage.</summary>
    MonitoredStageFinished = 11,

    /// <summary>Monitoring: someone completed a pathway.</summary>
    MonitoredPathwayCompleted = 12,

    /// <summary>Monitoring: someone blocked a pathway.</summary>
    MonitoredPathwayBlocked = 13,

    /// <summary>Monitoring: someone cancelled a pathway.</summary>
    MonitoredPathwayCancelled = 14,

    /// <summary>Monitoring: someone was assigned to a pathway.</summary>
    MonitoredPathwayAssigned = 15,

    /// <summary>Monitoring: someone unlocked a stage.</summary>
    MonitoredStageUnlocked = 16,

    /// <summary>Monitoring: someone changed a pathway's target date.</summary>
    MonitoredDueDateChanged = 17,

    /// <summary>A member of staff asked for a note to be deleted (sent to every admin but them).</summary>
    NoteDeletionRequested = 18,

    /// <summary>An admin deleted the note the user asked to be deleted.</summary>
    NoteDeletionApproved = 19,

    /// <summary>An admin kept the note the user asked to be deleted.</summary>
    NoteDeletionDeclined = 20,

    /// <summary>An admin deleted a note the user wrote (unless they deleted it themselves, or asked for it).</summary>
    AuthorsNoteDeleted = 21,

    /// <summary>
    /// Someone marked a walk-in entered in error that was the only attended visit of a finished stage that requires
    /// one, so the stage is finished without its visit. Sent to the pathway's assignees and the holders of
    /// FinishWithoutVisit, never to whoever withdrew it.
    /// </summary>
    StageVisitWithdrawn = 22,

    /// <summary>
    /// A referral the user registered was triaged (accepted, rejected or redirected) by someone else: sent to whoever
    /// registered it, so the booking team can tell the referrer. A fixed title ("Referral triaged").
    /// </summary>
    ReferralTriaged = 23,

    /// <summary>
    /// The lab rejected a specimen the user asked for or took (never sent to whoever recorded the rejection). Its
    /// <see cref="Notification.SubjectId"/> is the specimen.
    /// </summary>
    SpecimenRejectedByLab = 24,

    /// <summary>
    /// A specimen the user asked for or took was marked lost (never sent to whoever marked it). Its
    /// <see cref="Notification.SubjectId"/> is the specimen.
    /// </summary>
    SpecimenLost = 25,

    /// <summary>
    /// A result the user is responsible for was reported (a specimen's report attached, or an external result recorded
    /// by someone else). A fixed title ("Result ready"); its <see cref="Notification.SubjectId"/> is the result.
    /// </summary>
    ResultReady = 26,

    /// <summary>Someone else made the user responsible for a result. A fixed title ("Result reassigned to you").</summary>
    ResultReassigned = 27,

    /// <summary>Someone else signed off a result the user is responsible for. A fixed title ("Result signed off").</summary>
    ResultSignedOffByOther = 28,

    /// <summary>
    /// A letter was submitted to the user to approve (decision 102). A fixed title ("Letter to approve"); the author's
    /// username and the letter's kind. Its <see cref="Notification.SubjectId"/> is the letter.
    /// </summary>
    LetterAwaitingApproval = 29,

    /// <summary>Someone else approved the user's letter. A fixed title ("Letter approved"); the letter is the subject.</summary>
    LetterApproved = 30,

    /// <summary>The approver returned the user's letter with a comment (never in the notification). A fixed title ("Letter returned").</summary>
    LetterReturned = 31,

    /// <summary>
    /// Monitoring, a service alert: referrals awaiting triage too long (one per rule and run, with the count; its
    /// <see cref="Notification.SubjectId"/> is the rule's team, or none). 32 to 37 are the service alerts, one per
    /// time-based <c>MonitoringEvent</c> (8 to 13); fixed titles, counts and admin-written names only.
    /// </summary>
    MonitoredReferralNotTriaged = 32,

    /// <summary>Monitoring, a service alert: results awaiting sign-off too long.</summary>
    MonitoredResultNotSignedOff = 33,

    /// <summary>Monitoring, a service alert: visits Requested too long without being booked.</summary>
    MonitoredVisitNotBooked = 34,

    /// <summary>Monitoring, a service alert: specimens past their expected turnaround.</summary>
    MonitoredSpecimenOverdue = 35,

    /// <summary>Monitoring, a service alert: running waiting-time clocks near or past their target.</summary>
    MonitoredWaitBreached = 36,

    /// <summary>Monitoring, a service alert: paused clocks past their review date.</summary>
    MonitoredPauseReviewPassed = 37,

    /// <summary>Monitoring: someone finished a stage whose visit is Required without its visit.</summary>
    MonitoredStageFinishedWithoutVisit = 38,

    /// <summary>
    /// Someone else cancelled a clinic session of yours (sent to its clinician, with its day in
    /// <see cref="Notification.CalendarDate"/>): its booked appointments were cancelled with it.
    /// </summary>
    ClinicSessionCancelled = 39,

    /// <summary>
    /// A scheduled job failed three times in a row (sent once, to every admin, until it succeeds again).
    /// </summary>
    ScheduledJobFailing = 40,

    /// <summary>
    /// Someone recorded an MDT's outcome on a pathway you're assigned to (with its <see cref="Notification.PathwayId"/>).
    /// A fixed title ("MDT outcome recorded"); never the outcome, the question or the patient.
    /// </summary>
    MdtOutcomeRecorded = 41,

    /// <summary>
    /// A data request is due within 7 days and still open (the daily data-request reminder, to holders of
    /// ManagePrivacy), with the request in <see cref="Notification.SubjectId"/>. A fixed title ("Data request due
    /// soon"); the message holds the due date only, never the patient or the requester.
    /// </summary>
    DataRequestDueSoon = 42,

    // 43 is reserved by the earlier plan.

    /// <summary>
    /// A pathway was handed to your team (MC7, the relay): a stage your team works became current. A fixed title
    /// ("Pathway handed over"); the message names only who passed it, never the patient or the stage.
    /// </summary>
    PathwayHandedOver = 44,

    // 45 (MonitoredHandoverNotPickedUp) is reserved for MC7's monitoring.

    /// <summary>
    /// The lab found a sample insufficient and asked for a repeat (MC15): to whoever asked for it and took it. A fixed
    /// title ("Repeat sample requested"); the message names the test (admin-written) and the stage reopened, never the
    /// patient or why.
    /// </summary>
    RepeatSampleRequested = 46,

    /// <summary>A service alert (MC19): lab cases at risk of missing their due date ("Lab cases at risk").</summary>
    MonitoredLabCaseAtRisk = 47,

    /// <summary>A service alert (MC19): lab cases past their due date ("Lab cases overdue").</summary>
    MonitoredLabCaseOverdue = 48,

    /// <summary>
    /// Another team sent your team a consult (MC25), to the asked team's members who work pathways. A fixed title
    /// ("Consult requested"); the message names who sent it and their team, never the question or the patient.
    /// </summary>
    ConsultRequested = 49,

    /// <summary>
    /// The asked team replied to your team's consult, to whoever sent it and the pathway's assignees. A fixed title
    /// ("Consult replied"); the message names who replied and their team, never the reply or the patient.
    /// </summary>
    ConsultReplied = 50
}
