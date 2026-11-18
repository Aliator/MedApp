using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Calendar;

namespace MedApp.Application.Calendar.Common;

/// <summary>
/// Answers to meeting invitations (<see cref="MeetingReply"/>): each attendee accepts or declines for themselves,
/// any time before the meeting ends, and may change their mind. A declined meeting isn't busy time on the decliner's
/// calendar (<see cref="CalendarClashes.BusyTimes"/>); moving a meeting to another time puts everyone back to no reply
/// (<see cref="ResetAll"/>). A reply isn't a change to the meeting: its <c>LastUpdated</c> doesn't move.
/// </summary>
public static class MeetingReplies
{
    public const string NotAnAttendee = "Only the people going to a meeting can reply to it, each for themselves.";
    public const string Ended = "This meeting has ended, so it can't be replied to.";
    public const string NotAReply = "Reply must be Accepted or Declined.";

    /// <summary>Before what a clash names, when accepting again after declining finds the time taken.</summary>
    public const string TakenSinceDeclining = "That time has been used since you declined.";

    /// <summary>The replies a person can give: Accepted or Declined (never back to no reply).</summary>
    public static bool IsReply(MeetingReply reply) => reply is MeetingReply.Accepted or MeetingReply.Declined;

    /// <summary>This person's reply to the meeting, or null if they aren't going to it.</summary>
    public static MeetingReply? ReplyOf(Meeting meeting, Guid staffUserId) =>
        meeting.Attendees.FirstOrDefault(a => a.StaffUserId == staffUserId)?.Reply;

    /// <summary>Whether this person declined the meeting, so it isn't busy time on their calendar.</summary>
    public static bool HasDeclined(Meeting meeting, Guid staffUserId) =>
        ReplyOf(meeting, staffUserId) == MeetingReply.Declined;

    /// <summary>Whether this person can reply now: they're going, and it hasn't ended.</summary>
    public static bool CanReply(Meeting meeting, Guid staffUserId, DateTime nowUtc) =>
        WhyCantReply(meeting, staffUserId, nowUtc) is null;

    /// <summary>Why this person can't reply now (not going, or it has ended), or null if they can.</summary>
    public static string? WhyCantReply(Meeting meeting, Guid staffUserId, DateTime nowUtc) =>
        meeting.Attendees.All(a => a.StaffUserId != staffUserId) ? NotAnAttendee
        : nowUtc >= meeting.EndUtc ? Ended
        : null;

    /// <summary>
    /// Records this person's reply, at <paramref name="nowUtc"/>: a 403 if they aren't going (nobody replies for
    /// anyone else), a 409 once it has ended. Returns their reply before; the same reply again changes
    /// nothing (not even when it was given).
    /// </summary>
    public static MeetingReply Reply(Meeting meeting, Guid staffUserId, MeetingReply reply, DateTime nowUtc)
    {
        if (!IsReply(reply))
            throw new ArgumentOutOfRangeException(nameof(reply), reply, NotAReply);

        switch (WhyCantReply(meeting, staffUserId, nowUtc))
        {
            case NotAnAttendee:
                throw new ForbiddenException(NotAnAttendee);
            case { } reason:
                throw new ConflictException(reason);
        }

        var attendee = meeting.Attendees.Single(a => a.StaffUserId == staffUserId);
        var before = attendee.Reply;
        if (before == reply)
            return before;

        attendee.Reply = reply;
        attendee.RepliedAtUtc = nowUtc;
        return before;
    }

    /// <summary>Puts every attendee back to no reply: the meeting moved, so what they said was about another time.</summary>
    public static void ResetAll(Meeting meeting)
    {
        foreach (var attendee in meeting.Attendees)
        {
            attendee.Reply = MeetingReply.NoReply;
            attendee.RepliedAtUtc = null;
        }
    }
}
