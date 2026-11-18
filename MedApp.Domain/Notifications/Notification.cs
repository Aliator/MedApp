namespace MedApp.Domain.Notifications;

/// <summary>
/// Something that happened that a user should know about, e.g. being assigned to a pathway. Kept, so it's there
/// after signing in again, and marked read once they've seen it. Like the audit log, it has no foreign keys, so it
/// outlives the pathway it's about.
/// </summary>
public sealed class Notification
{
    public Guid Id { get; init; }

    /// <summary>Who it's for.</summary>
    public Guid UserId { get; init; }

    public NotificationKind Kind { get; init; }

    /// <summary>One line: the pathway's title, or a fixed phrase for the kind (<see cref="HasFixedTitle"/>).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>What happened, e.g. "admin assigned you to it."</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>The pathway it's about, if any, for a link to it.</summary>
    public Guid? PathwayId { get; init; }

    /// <summary>The appointment it's about, if any, for a link to it.</summary>
    public Guid? AppointmentId { get; init; }

    /// <summary>
    /// The request to delete a pathway's note it's about, if any (<see cref="NotificationKind.NoteDeletionRequested"/>),
    /// for a link that opens it for an admin to decide.
    /// </summary>
    public Guid? NoteDeletionRequestId { get; init; }

    /// <summary>The UK date on the calendar it's about, if any (a meeting's day), for a link to the Calendar that day.</summary>
    public DateOnly? CalendarDate { get; init; }

    /// <summary>
    /// The meeting it's about, if any, for a link that opens it on the Calendar that day; none for a cancellation (the
    /// meeting is deleted, or the person is no longer on it). No foreign key: the notification outlives the meeting.
    /// </summary>
    public Guid? MeetingId { get; init; }

    /// <summary>
    /// The referral it's about, if any (<see cref="NotificationKind.ReferralTriaged"/>), for a link to it. No foreign
    /// key: the notification outlives what it's about.
    /// </summary>
    public Guid? ReferralId { get; init; }

    /// <summary>
    /// What it's about, when that isn't one of the columns above: its meaning comes from the kind (a letter for
    /// <see cref="NotificationKind.LetterAwaitingApproval"/>, <see cref="NotificationKind.LetterApproved"/> and
    /// <see cref="NotificationKind.LetterReturned"/>; a specimen for <see cref="NotificationKind.SpecimenRejectedByLab"/>
    /// and <see cref="NotificationKind.SpecimenLost"/>; a result for the result kinds; a data request for <see cref="NotificationKind.DataRequestDueSoon"/>), and the web app links each kind to its page. No foreign key: the
    /// notification outlives what it's about.
    /// </summary>
    public Guid? SubjectId { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    /// <summary>When the user marked it read; null while unread.</summary>
    public DateTime? ReadAtUtc { get; set; }

    /// <summary>
    /// Whether it's a monitoring notification: an admin told about what a member of staff did. It's about the staff,
    /// not the patient, so a patient's export leaves it out. A set, not one range: 10 to 17, and the later kinds from 32
    /// (<see cref="NotificationKind.MonitoredStageFinishedWithoutVisit"/>).
    /// </summary>
    public static bool IsMonitoring(NotificationKind kind) =>
        kind is >= NotificationKind.MonitoredPathwayCreated and <= NotificationKind.MonitoredDueDateChanged
            or NotificationKind.MonitoredStageFinishedWithoutVisit;

    /// <summary>
    /// Whether notifications of this kind have a fixed title (the calendar kinds, 1 to 9: the appointment kinds'
    /// "Appointment booked", the meeting kinds' "Meeting invitation"; the monitoring kinds' "Stage finished"; the note
    /// deletion kinds' "Note deletion requested"; "Stage visit withdrawn"; "Referral triaged"; the specimen kinds' "Specimen rejected" and "Specimen lost"; the result kinds' "Result ready"; the letter kinds' "Letter to approve"; "Clinic session cancelled"; "Scheduled job failing"; "MDT outcome recorded"; and "Data request due soon"), rather than the
    /// pathway's title, so anonymising a patient leaves it as it is.
    /// </summary>
    public static bool HasFixedTitle(NotificationKind kind) =>
        kind is >= NotificationKind.AppointmentBooked and <= NotificationKind.AppointmentRecorded
            or NotificationKind.StageVisitWithdrawn
            or NotificationKind.ReferralTriaged
            or NotificationKind.SpecimenRejectedByLab
            or NotificationKind.SpecimenLost
            or NotificationKind.ResultReady
            or NotificationKind.ResultReassigned
            or NotificationKind.ResultSignedOffByOther
            or NotificationKind.LetterAwaitingApproval
            or NotificationKind.LetterApproved
            or NotificationKind.LetterReturned
            or NotificationKind.ClinicSessionCancelled
            or NotificationKind.ScheduledJobFailing
            or NotificationKind.MdtOutcomeRecorded
            or NotificationKind.DataRequestDueSoon
        || IsMonitoring(kind)
        || IsNoteDeletion(kind);

    /// <summary>Whether it's about deleting a pathway's note (a request asked for or decided, or an author told).</summary>
    public static bool IsNoteDeletion(NotificationKind kind) =>
        kind is >= NotificationKind.NoteDeletionRequested and <= NotificationKind.AuthorsNoteDeleted;
}
