using MedApp.Contracts.Calendar.Responses;
using MedApp.Domain.Calendar;

namespace MedApp.Application.Calendar.Common;

/// <summary>Meetings as the API returns them.</summary>
public static class MeetingMapper
{
    /// <summary>Every user a meeting's response names: its organiser and attendees.</summary>
    public static IEnumerable<Guid> UserIds(Meeting meeting) =>
        meeting.Attendees.Select(a => a.StaffUserId).Prepend(meeting.OrganiserUserId).Distinct();

    /// <summary>
    /// A meeting as <paramref name="viewer"/> sees it, with the usernames given (a deleted account's is null), its
    /// attendees by username with their replies. Admins can change it unless it's on a day before today; the viewer
    /// can reply if they're going and it hasn't ended (<see cref="MeetingReplies.CanReply"/>), and sees their own
    /// reply (<c>MyReply</c>).
    /// </summary>
    public static MeetingResponse ToResponse(
        Meeting meeting,
        IReadOnlyDictionary<Guid, string> usernames,
        CalendarActor viewer,
        DateTime nowUtc)
    {
        var date = UkTime.DateOf(meeting.StartUtc);
        return new MeetingResponse
        {
            Id = meeting.Id,
            Title = meeting.Title,
            Notes = meeting.Notes,
            Date = date,
            StartUtc = meeting.StartUtc,
            EndUtc = meeting.EndUtc,
            OrganiserUserId = meeting.OrganiserUserId,
            OrganiserUsername = usernames.GetValueOrDefault(meeting.OrganiserUserId),
            Attendees = meeting.Attendees
                .Select(a => new MeetingAttendeeResponse
                {
                    UserId = a.StaffUserId,
                    Username = usernames.GetValueOrDefault(a.StaffUserId),
                    Reply = a.Reply.ToString(),
                    RepliedAtUtc = a.RepliedAtUtc
                })
                .OrderBy(a => a.Username is null)
                .ThenBy(a => a.Username, StringComparer.OrdinalIgnoreCase)
                .ThenBy(a => a.UserId)
                .ToList(),
            CanEdit = viewer.IsAdmin && date >= UkTime.DateOf(nowUtc),
            MyReply = MeetingReplies.ReplyOf(meeting, viewer.UserId)?.ToString(),
            CanReply = MeetingReplies.CanReply(meeting, viewer.UserId, nowUtc),
            CreatedAtUtc = meeting.CreatedAtUtc,
            LastUpdated = meeting.LastUpdated
        };
    }

    public static TimeRangeResponse ToResponse(TimeRange range) => new()
    {
        StartUtc = range.StartUtc,
        EndUtc = range.EndUtc
    };
}
