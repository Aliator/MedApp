using FluentValidation;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Meetings.Commands.CreateMeeting;
using MedApp.Application.Calendar.Meetings.Commands.UpdateMeeting;
using MedApp.Domain.Calendar;
using MedApp.Domain.Notifications;

namespace MedApp.DevMcp.DemoData;

/// <summary>
/// One meeting's history, replayed through the app's own meeting steps in memory: each version checked by the
/// commands' validators (<see cref="CreateMeetingValidator"/>, <see cref="UpdateMeetingValidator"/>: the working
/// day's rules, bank holidays included, the title, notes and attendees), the past-day guard (<see cref="CalendarGuards.EnsureNotPast"/>), and
/// its notifications made by <see cref="MeetingNotifications"/>, as the create, update and cancel handlers send them.
/// Whoever changes it must be an admin then. Clashes are the caller's to check (<see cref="CalendarClashes"/>), on
/// every attendee's calendar, as the handlers do before each step.
/// </summary>
public sealed class MeetingStory
{
    private static readonly CreateMeetingValidator CreateValidator = new(DemoClosedDays.WorkingDays);
    private static readonly UpdateMeetingValidator UpdateValidator = new(DemoClosedDays.WorkingDays);

    private readonly List<Notification> _notifications = [];

    private MeetingStory(Meeting meeting) => Meeting = meeting;

    /// <summary>The meeting as it stands after its history (as saved, unless it was cancelled).</summary>
    public Meeting Meeting { get; }

    /// <summary>Every notification its history sent, oldest first.</summary>
    public IReadOnlyList<Notification> Notifications => _notifications;

    public bool IsCancelled { get; private set; }

    /// <summary>How many changes were made after it was arranged (moves, people added or taken off).</summary>
    public int Changes { get; private set; }

    /// <summary>An admin arranges a meeting (<c>CreateMeetingHandler</c>): everyone going but them is invited.</summary>
    public static MeetingStory Arrange(
        string title, string notes, DateTime startUtc, DateTime endUtc, IReadOnlyCollection<Guid> attendees, StaffMember by, DateTime atUtc)
    {
        var actor = ActorAt(by, atUtc);
        DemoClosedDays.Validate(CreateValidator, new CreateMeetingCommand(title, notes, startUtc, endUtc, attendees.ToList(), actor));
        CalendarGuards.EnsureNotPast(UkTime.DateOf(startUtc), UkTime.DateOf(atUtc));

        var id = Guid.NewGuid();
        var story = new MeetingStory(new Meeting
        {
            Id = id,
            Title = title.Trim(),
            Notes = notes.Trim(),
            StartUtc = startUtc,
            EndUtc = endUtc,
            OrganiserUserId = by.Id,
            CreatedAtUtc = atUtc,
            LastUpdated = atUtc,
            Attendees = MeetingStaff.Attendees(id, attendees),
        });
        story._notifications.AddRange(MeetingNotifications.AfterCreating(story.Meeting, actor, atUtc));
        return story;
    }

    /// <summary>
    /// An admin changes who's going or when (<c>UpdateMeetingHandler</c>): those taken off are told it's cancelled for
    /// them, those added are invited, and the rest are told if it moved.
    /// </summary>
    public void Change(IReadOnlyCollection<Guid> attendees, DateTime startUtc, DateTime endUtc, StaffMember by, DateTime atUtc)
    {
        EnsureOpen(atUtc);
        var meeting = Meeting;
        var actor = ActorAt(by, atUtc);
        DemoClosedDays.Validate(UpdateValidator, new UpdateMeetingCommand(
            meeting.Id, meeting.Title, meeting.Notes, startUtc, endUtc, attendees.ToList(), meeting.LastUpdated, actor));
        var today = UkTime.DateOf(atUtc);
        CalendarGuards.EnsureNotPast(UkTime.DateOf(meeting.StartUtc), today);
        CalendarGuards.EnsureNotPast(UkTime.DateOf(startUtc), today);

        var before = (Attendees: meeting.Attendees.Select(a => a.StaffUserId).ToList(), meeting.StartUtc, meeting.EndUtc);
        var wanted = attendees.ToHashSet();
        meeting.StartUtc = startUtc;
        meeting.EndUtc = endUtc;
        meeting.LastUpdated = atUtc;
        meeting.Attendees.RemoveAll(a => !wanted.Contains(a.StaffUserId));
        meeting.Attendees.AddRange(MeetingStaff.Attendees(
            meeting.Id, wanted.Where(id => meeting.Attendees.All(a => a.StaffUserId != id))));

        _notifications.AddRange(MeetingNotifications.AfterChanging(meeting, before.Attendees, before.StartUtc, before.EndUtc, actor, atUtc));
        Changes++;
    }

    /// <summary>An admin cancels it (<c>CancelMeetingHandler</c>): it's deleted, and everyone going but them is told.</summary>
    public void Cancel(StaffMember by, DateTime atUtc)
    {
        EnsureOpen(atUtc);
        CalendarGuards.EnsureNotPast(UkTime.DateOf(Meeting.StartUtc), UkTime.DateOf(atUtc));
        _notifications.AddRange(MeetingNotifications.AfterCancelling(Meeting, ActorAt(by, atUtc), atUtc));
        IsCancelled = true;
    }

    private void EnsureOpen(DateTime atUtc)
    {
        if (IsCancelled)
            throw new InvalidOperationException("A cancelled meeting is deleted: it can't change.");
        if (atUtc < Meeting.LastUpdated)
            throw new InvalidOperationException("A meeting's history is told in order.");
    }

    /// <summary>The admin making a change, as the API passes them (only admins arrange meetings).</summary>
    private static CalendarActor ActorAt(StaffMember by, DateTime atUtc) =>
        by.IsAdminAt(atUtc)
            ? new CalendarActor(by.Id, by.Username, IsAdmin: true)
            : throw new InvalidOperationException($"{by.Username} isn't an admin at {UkTime.Describe(atUtc)}, so can't arrange meetings.");
}
