using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.DevMcp.DemoData;
using MedApp.Domain.Calendar;
using MedApp.Domain.Notifications;
using MedApp.UnitTests.Common.Constants;
using static MedApp.UnitTests.Common.Constants.DemoDataTestConstants;

namespace MedApp.UnitTests.Tools.DemoData;

/// <summary>
/// The demo data's meetings (<see cref="DemoMeetings"/>), planned in memory at many moments: each obeys the app's
/// meeting rules (the working day, no clash on any attendee's calendar, arranged by an admin before its day), and the
/// notifications their histories send are the app's, to others only, in the last 30 days.
/// </summary>
[TestFixture]
public sealed class DemoMeetingsTests
{
    private static (DemoTimeline Timeline, List<StaffMember> Staff, DemoCalendars Calendars) Plan(DateTime nowUtc)
    {
        var timeline = new DemoTimeline(nowUtc);
        var staff = Staff(timeline);
        return (timeline, staff, DemoCalendars.Plan(staff, timeline, new Random(Seed)));
    }

    [TestCaseSource(typeof(DemoDataTestConstants), nameof(CalendarMoments))]
    public void Plan_EveryMeeting_IsInsideTheWorkingDay_AndOverlapsNothingOnAnyAttendeesCalendar(DateTime nowUtc)
    {
        var (_, _, calendars) = Plan(nowUtc);

        calendars.Meetings.Should().HaveCountGreaterThan(150);
        foreach (var meeting in calendars.Meetings)
        {
            var date = UkTime.DateOf(meeting.StartUtc);
            WorkingHours.WhyOutside(meeting.StartUtc, meeting.EndUtc).Should().BeNull($"'{meeting.Title}' on {UkTime.Describe(meeting.StartUtc)}");
            meeting.Attendees.Should().NotBeEmpty().And.OnlyHaveUniqueItems(a => a.StaffUserId);
            foreach (var attendee in meeting.Attendees.Where(a => a.Reply != MeetingReply.Declined))
                CalendarClashes.Find(calendars.DataFor(attendee.StaffUserId, date, date), meeting.StartUtc, meeting.EndUtc, ignoreMeetingId: meeting.Id)
                    .Should().BeNull($"'{meeting.Title}' on {UkTime.Describe(meeting.StartUtc)} is clear on every attendee's calendar");
        }
    }

    [TestCaseSource(typeof(DemoDataTestConstants), nameof(CalendarMoments))]
    public void Plan_EveryMeeting_WasArrangedByAnAdmin_NotAfterItsDay_AndNothingHappensAfterNow(DateTime nowUtc)
    {
        var (timeline, staff, calendars) = Plan(nowUtc);

        foreach (var story in calendars.MeetingPlan.Stories)
        {
            var meeting = story.Meeting;
            staff.Single(s => s.Id == meeting.OrganiserUserId).IsAdminAt(meeting.CreatedAtUtc).Should().BeTrue($"'{meeting.Title}' was arranged by an admin");
            UkTime.DateOf(meeting.CreatedAtUtc).Should().BeOnOrBefore(UkTime.DateOf(meeting.StartUtc), "a meeting can't be arranged on a past day");
            meeting.CreatedAtUtc.Should().BeOnOrBefore(meeting.LastUpdated);
            meeting.LastUpdated.Should().BeOnOrBefore(timeline.NowUtc);
            story.Notifications.Should().OnlyContain(n => n.CreatedAtUtc >= meeting.CreatedAtUtc && n.CreatedAtUtc <= timeline.NowUtc);
        }
    }

    [TestCaseSource(typeof(DemoDataTestConstants), nameof(CalendarMoments))]
    public void Plan_HasTheRecurringAndOneOffMeetings_PastAndUpcoming(DateTime nowUtc)
    {
        var (timeline, _, calendars) = Plan(nowUtc);
        var titles = calendars.Meetings.Select(m => m.Title).ToHashSet();

        titles.Should().Contain([DemoMeetings.Huddle, DemoMeetings.Mdt, DemoMeetings.Governance, DemoMeetings.OneToOne, DemoMeetings.TheatrePlanning]);
        titles.Intersect(MeetingSchedule.TrainingTitles).Should().HaveCountGreaterThan(3, "training runs every six weeks");
        calendars.Meetings.Should().Contain(m => m.EndUtc <= timeline.NowUtc).And.Contain(m => m.StartUtc > timeline.NowUtc.AddDays(14));
        calendars.Meetings.Count(m => m.Title == DemoMeetings.Huddle).Should().BeGreaterThan(VolumeCurves.HistoryWeeks - 2, "one each Monday");
    }

    [TestCaseSource(typeof(DemoDataTestConstants), nameof(CalendarMoments))]
    public void Plan_SomeMeetingsWereMovedChangedOrCancelled_AndTheirNotificationsAreRecent(DateTime nowUtc)
    {
        var (timeline, _, calendars) = Plan(nowUtc);
        var plan = calendars.MeetingPlan;
        var recent = plan.RecentNotifications.ToList();

        plan.Replanned.Should().NotBeNull("theatre list planning moved, with the coordinator added");
        plan.Cancelled.Should().NotBeNull("the away day planning meeting was cancelled");
        calendars.Meetings.Should().NotContain(plan.Cancelled!.Meeting, "a cancelled meeting is deleted");
        plan.Stories.Count(s => s.Changes > 0).Should().BeGreaterThan(5, "leave booked later took people off, and newcomers were added");

        recent.Select(n => n.Kind).Distinct().Should().BeEquivalentTo(
            [NotificationKind.MeetingInvited, NotificationKind.MeetingChanged, NotificationKind.MeetingCancelled, NotificationKind.MeetingDeclined]);
        recent.Should().OnlyContain(n => n.CreatedAtUtc >= timeline.NotificationsFromUtc && n.CreatedAtUtc <= timeline.NowUtc);
    }

    [TestCaseSource(typeof(DemoDataTestConstants), nameof(CalendarMoments))]
    public void Plan_MeetingNotifications_HaveFixedTitles_GoOnlyToOthers_AndNameWhoAndWhatOnly(DateTime nowUtc)
    {
        var (_, staff, calendars) = Plan(nowUtc);
        var titles = calendars.MeetingPlan.Stories.Select(s => s.Meeting.Title).ToHashSet();

        foreach (var notification in calendars.MeetingPlan.Stories.SelectMany(s => s.Notifications))
        {
            Notification.HasFixedTitle(notification.Kind).Should().BeTrue();
            notification.Title.Should().Be(MeetingNotifications.TitleOf(notification.Kind));
            notification.CalendarDate.Should().NotBeNull();
            notification.ProcedureId.Should().BeNull();
            notification.AppointmentId.Should().BeNull();

            // "admin invited you to 'MDT', ...": by an admin, never to themselves, about a meeting by its title.
            var by = staff.Single(s => notification.Message.StartsWith($"{s.Username} "));
            notification.UserId.Should().NotBe(by.Id, "nobody is told about their own change");
            titles.Should().Contain(t => notification.Message.Contains($"'{t}'"));
        }
    }

    [TestCaseSource(typeof(DemoDataTestConstants), nameof(CalendarMoments))]
    public void Plan_SomeoneTakenOffAMeeting_HadBookedTimeOverItAfterItWasArranged(DateTime nowUtc)
    {
        var (_, _, calendars) = Plan(nowUtc);

        var takenOff = calendars.MeetingPlan.Stories
            .SelectMany(s => s.Notifications.Where(n => n.Message.Contains("took you off")).Select(n => (Story: s, Notification: n)))
            .ToList();

        takenOff.Should().NotBeEmpty("leave booked after a meeting was arranged takes the person off it first");
        foreach (var (story, notification) in takenOff)
        {
            var meeting = story.Meeting;
            meeting.Attendees.Should().NotContain(a => a.StaffUserId == notification.UserId);
            calendars.StoredBlocks.Should().Contain(b => b.StaffUserId == notification.UserId
                                                         && b.CreatedAtUtc > meeting.CreatedAtUtc
                                                         && b.CreatedAtUtc >= notification.CreatedAtUtc
                                                         && CalendarClashes.Overlaps(b.StartUtc, b.EndUtc, meeting.StartUtc, meeting.EndUtc),
                $"they were taken off '{meeting.Title}' on {UkTime.Describe(meeting.StartUtc)} to book that time");
        }
    }

    [Test]
    public void Plan_NewStarter_IsWelcomed_AndAddedToTheHuddlesArrangedBeforeTheyJoined()
    {
        var (_, staff, calendars) = Plan(Now);
        var starter = staff.Single(s => s.Kind == StaffKind.NewStarter);

        calendars.Meetings.Should().ContainSingle(m => m.Title == DemoMeetings.Welcome)
            .Which.Attendees.Should().Contain(a => a.StaffUserId == starter.Id);
        calendars.MeetingPlan.Stories
            .Where(s => s.Meeting.Title == DemoMeetings.Huddle && s.Meeting.StartUtc > Now)
            .Should().OnlyContain(s => s.Meeting.Attendees.Any(a => a.StaffUserId == starter.Id))
            .And.Contain(s => s.Notifications.Any(n => n.UserId == starter.Id && n.Kind == NotificationKind.MeetingInvited));
    }

    [TestCaseSource(typeof(DemoDataTestConstants), nameof(CalendarMoments))]
    public void Plan_Attendees_AcceptedDeclinedOrDidntAnswer_EachForThemselvesAtAPossibleMoment(DateTime nowUtc)
    {
        var (timeline, staff, calendars) = Plan(nowUtc);
        var stories = calendars.MeetingPlan.Stories.Where(s => !s.IsCancelled).ToList();
        var attendees = stories.SelectMany(s => s.Meeting.Attendees.Select(a => (s.Meeting, Attendee: a))).ToList();

        // A mix, upcoming and past.
        foreach (var upcoming in new[] { true, false })
            attendees.Where(r => r.Meeting.StartUtc > timeline.NowUtc == upcoming).Select(r => r.Attendee.Reply).Distinct()
                .Should().BeEquivalentTo([MeetingReply.NoReply, MeetingReply.Accepted, MeetingReply.Declined]);
        stories.Sum(s => s.Replies).Should().Be(attendees.Count(r => r.Attendee.Reply != MeetingReply.NoReply));

        foreach (var (meeting, attendee) in attendees)
        {
            if (attendee.Reply == MeetingReply.NoReply)
            {
                attendee.RepliedAtUtc.Should().BeNull();
                continue;
            }

            // Never the organiser's (they arranged it); after the invitation or the last move, before it starts and now.
            attendee.StaffUserId.Should().NotBe(meeting.OrganiserUserId);
            attendee.RepliedAtUtc.Should().NotBeNull();
            attendee.RepliedAtUtc!.Value.Should().BeOnOrAfter(meeting.LastUpdated).And.BeOnOrBefore(timeline.NowUtc).And.BeBefore(meeting.EndUtc);
            staff.Single(s => s.Id == attendee.StaffUserId).WorksAt(attendee.RepliedAtUtc.Value).Should().BeTrue();
        }
    }

    [TestCaseSource(typeof(DemoDataTestConstants), nameof(CalendarMoments))]
    public void Plan_EachDecline_ToldTheOrganiser(DateTime nowUtc)
    {
        var (_, staff, calendars) = Plan(nowUtc);

        foreach (var story in calendars.MeetingPlan.Stories.Where(s => !s.IsCancelled))
        {
            var meeting = story.Meeting;
            var decliners = meeting.Attendees.Where(a => a.Reply == MeetingReply.Declined).Select(a => staff.Single(s => s.Id == a.StaffUserId)).ToList();
            var told = story.Notifications.Where(n => n.Kind == NotificationKind.MeetingDeclined).ToList();

            told.Should().HaveCount(decliners.Count).And.OnlyContain(n => n.UserId == meeting.OrganiserUserId);
            foreach (var decliner in decliners)
                told.Should().ContainSingle(n => n.Message.StartsWith($"{decliner.Username} declined '{meeting.Title}', ")
                                                  && n.CreatedAtUtc == meeting.Attendees.Single(a => a.StaffUserId == decliner.Id).RepliedAtUtc);
        }
    }
}
