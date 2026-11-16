using MedApp.Domain.Calendar;
using MedApp.Domain.Notifications;

namespace MedApp.Application.Calendar.Common;

/// <summary>
/// What to tell members of staff when an admin arranges, moves or cancels a meeting they're going to, or adds them to
/// one or takes them off it. Nobody is told about their own change. Titles are fixed phrases for each kind
/// (<see cref="TitleOf"/>); the message says what and when, and the notification links to the Calendar that day.
/// </summary>
public static class MeetingNotifications
{
    public static string TitleOf(NotificationKind kind) => kind switch
    {
        NotificationKind.MeetingInvited => "Meeting invitation",
        NotificationKind.MeetingChanged => "Meeting moved",
        NotificationKind.MeetingCancelled => "Meeting cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    /// <summary>After arranging a meeting: an invitation to each attendee.</summary>
    public static IReadOnlyList<Notification> AfterCreating(Meeting meeting, CalendarActor actor, DateTime nowUtc) =>
        meeting.Attendees
            .Where(a => a.StaffUserId != actor.UserId)
            .Select(a => Create(
                NotificationKind.MeetingInvited,
                a.StaffUserId,
                meeting,
                $"{actor.Username} invited you to '{meeting.Title}', {UkTime.DescribeRange(meeting.StartUtc, meeting.EndUtc)}.",
                nowUtc))
            .ToList();

    /// <summary>
    /// After changing a meeting, given who was going and when it was before: those taken off are told it's cancelled
    /// for them, those added are invited, and those still going are told if it moved. A change of title or notes alone
    /// tells nobody.
    /// </summary>
    public static IReadOnlyList<Notification> AfterChanging(
        Meeting meeting,
        IReadOnlyCollection<Guid> previousAttendees,
        DateTime previousStartUtc,
        DateTime previousEndUtc,
        CalendarActor actor,
        DateTime nowUtc)
    {
        var current = meeting.Attendees.Select(a => a.StaffUserId).ToHashSet();
        var moved = previousStartUtc != meeting.StartUtc || previousEndUtc != meeting.EndUtc;
        var when = UkTime.DescribeRange(meeting.StartUtc, meeting.EndUtc);
        var notifications = new List<Notification>();

        foreach (var removed in previousAttendees.Where(id => !current.Contains(id) && id != actor.UserId))
            notifications.Add(Create(
                NotificationKind.MeetingCancelled,
                removed,
                meeting,
                $"{actor.Username} took you off '{meeting.Title}', {UkTime.DescribeRange(previousStartUtc, previousEndUtc)}.",
                nowUtc,
                UkTime.DateOf(previousStartUtc)));

        foreach (var attendee in meeting.Attendees.Select(a => a.StaffUserId).Where(id => id != actor.UserId))
        {
            if (!previousAttendees.Contains(attendee))
                notifications.Add(Create(
                    NotificationKind.MeetingInvited, attendee, meeting, $"{actor.Username} invited you to '{meeting.Title}', {when}.", nowUtc));
            else if (moved)
                notifications.Add(Create(
                    NotificationKind.MeetingChanged, attendee, meeting, $"{actor.Username} moved '{meeting.Title}' to {when}.", nowUtc));
        }

        return notifications;
    }

    /// <summary>After cancelling a meeting: each attendee is told.</summary>
    public static IReadOnlyList<Notification> AfterCancelling(Meeting meeting, CalendarActor actor, DateTime nowUtc) =>
        meeting.Attendees
            .Where(a => a.StaffUserId != actor.UserId)
            .Select(a => Create(
                NotificationKind.MeetingCancelled,
                a.StaffUserId,
                meeting,
                $"{actor.Username} cancelled '{meeting.Title}', {UkTime.DescribeRange(meeting.StartUtc, meeting.EndUtc)}.",
                nowUtc))
            .ToList();

    private static Notification Create(
        NotificationKind kind,
        Guid userId,
        Meeting meeting,
        string message,
        DateTime nowUtc,
        DateOnly? date = null) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Kind = kind,
        Title = TitleOf(kind),
        Message = message,
        CalendarDate = date ?? UkTime.DateOf(meeting.StartUtc),
        CreatedAtUtc = nowUtc
    };
}
