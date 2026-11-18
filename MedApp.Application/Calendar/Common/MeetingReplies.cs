using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Calendar;

namespace MedApp.Application.Calendar.Common;

/// <summary>
/// Answers to meeting invitations (<see cref="MeetingReply"/>): each attendee accepts or declines for themselves,
/// any time before the meeting ends, and may change their mind. A reply is about the time the person saw: if the
/// meeting has moved since, it's refused (<see cref="EnsureNotMoved"/>). A declined meeting isn't busy time on the
/// decliner's calendar (<see cref="CalendarClashes.BusyTimes"/>); moving a meeting to another time puts everyone back
/// to no reply, except those who declined and are busy at the new time (<see cref="ResetAfterMove"/>). A reply isn't
/// a change to the meeting: its <c>LastUpdated</c> doesn't move.
/// </summary>
public static class MeetingReplies
{
    public const string NotAnAttendee = "Only the people going to a meeting can reply to it, each for themselves.";
    public const string Ended = "This meeting has ended, so it can't be replied to.";
    public const string NotAReply = "Reply must be Accepted or Declined.";
    public const string NoTimeSeen = "The meeting's start and end, as you saw them, are required.";

    /// <summary>
    /// Before what a clash names, when accepting again after declining finds something else at that time: put there
    /// since the decline, or there before it (a template block may overlap a meeting, and a move keeps the decline of
    /// someone busy at the new time), so it says neither.
    /// </summary>
    public const string NotFreeToAccept = "You can't accept it while something else is on your calendar at that time:";

    /// <summary>The 409 when the meeting has moved since the person looked: "This meeting has moved to ...".</summary>
    public static string MovedMessage(Meeting meeting) =>
        $"This meeting has moved to {UkTime.DescribeRange(meeting.StartUtc, meeting.EndUtc)}: look at its new time, then reply.";

    /// <summary>The replies a person can give: Accepted or Declined (never back to no reply).</summary>
    public static bool IsReply(MeetingReply reply) => reply is MeetingReply.Accepted or MeetingReply.Declined;

    /// <summary>This person's reply to the meeting, or null if they aren't going to it.</summary>
    public static MeetingReply? ReplyOf(Meeting meeting, Guid staffUserId) =>
        meeting.Attendees.FirstOrDefault(a => a.StaffUserId == staffUserId)?.Reply;

    /// <summary>Whether this person declined the meeting, so it isn't busy time on their calendar.</summary>
    public static bool HasDeclined(Meeting meeting, Guid staffUserId) =>
        ReplyOf(meeting, staffUserId) == MeetingReply.Declined;

    /// <summary>The attendees who declined it.</summary>
    public static IReadOnlySet<Guid> Decliners(Meeting meeting) =>
        meeting.Attendees.Where(a => a.Reply == MeetingReply.Declined).Select(a => a.StaffUserId).ToHashSet();

    /// <summary>Whether this person can reply now: they're going, and it hasn't ended.</summary>
    public static bool CanReply(Meeting meeting, Guid staffUserId, DateTime nowUtc) =>
        WhyCantReply(meeting, staffUserId, nowUtc) is null;

    /// <summary>Why this person can't reply now (not going, or it has ended), or null if they can.</summary>
    public static string? WhyCantReply(Meeting meeting, Guid staffUserId, DateTime nowUtc) =>
        meeting.Attendees.All(a => a.StaffUserId != staffUserId) ? NotAnAttendee
        : nowUtc >= meeting.EndUtc ? Ended
        : null;

    /// <summary>
    /// A 409 if the meeting isn't at the time the person saw (<paramref name="expectedStartUtc"/> to
    /// <paramref name="expectedEndUtc"/>): a reply is about a time, so one from a screen opened before a move mustn't
    /// count for the new one. A change of title, notes or attendees doesn't refuse it.
    /// </summary>
    public static void EnsureNotMoved(Meeting meeting, DateTime expectedStartUtc, DateTime expectedEndUtc)
    {
        if (meeting.StartUtc != UkTime.Utc(expectedStartUtc) || meeting.EndUtc != UkTime.Utc(expectedEndUtc))
            throw new ConflictException(MovedMessage(meeting));
    }

    /// <summary>
    /// Records this person's reply to the meeting at the time they saw, at <paramref name="nowUtc"/>: a 403 if they
    /// aren't going (nobody replies for anyone else), a 409 if it has moved since (<see cref="EnsureNotMoved"/>) or has
    /// ended. Returns their reply before; the same reply again changes nothing (not even when it was given).
    /// </summary>
    public static MeetingReply Reply(
        Meeting meeting,
        Guid staffUserId,
        MeetingReply reply,
        DateTime expectedStartUtc,
        DateTime expectedEndUtc,
        DateTime nowUtc)
    {
        if (!IsReply(reply))
            throw new ArgumentOutOfRangeException(nameof(reply), reply, NotAReply);

        if (meeting.Attendees.All(a => a.StaffUserId != staffUserId))
            throw new ForbiddenException(NotAnAttendee);
        EnsureNotMoved(meeting, expectedStartUtc, expectedEndUtc);
        if (WhyCantReply(meeting, staffUserId, nowUtc) is { } reason)
            throw new ConflictException(reason);

        var attendee = meeting.Attendees.Single(a => a.StaffUserId == staffUserId);
        var before = attendee.Reply;
        if (before == reply)
            return before;

        attendee.Reply = reply;
        attendee.RepliedAtUtc = nowUtc;
        return before;
    }

    /// <summary>
    /// After a move: everyone back to no reply, as what they said was about another time, except those in
    /// <paramref name="keepDeclined"/>: people who declined and are busy at the new time, who still aren't going (asking
    /// them again would only clash with what they have on).
    /// </summary>
    public static void ResetAfterMove(Meeting meeting, IReadOnlySet<Guid>? keepDeclined = null)
    {
        foreach (var attendee in meeting.Attendees)
        {
            if (attendee.Reply == MeetingReply.Declined && keepDeclined?.Contains(attendee.StaffUserId) == true)
                continue;
            attendee.Reply = MeetingReply.NoReply;
            attendee.RepliedAtUtc = null;
        }
    }
}
