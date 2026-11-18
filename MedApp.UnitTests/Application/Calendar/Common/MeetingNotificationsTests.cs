using MedApp.UnitTests.Common.Constants;
using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Domain.Calendar;
using MedApp.Domain.Notifications;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;
using static MedApp.UnitTests.Common.Constants.MeetingsTestConstants;

namespace MedApp.UnitTests.Application.Calendar.Common;

[TestFixture]
public sealed class MeetingNotificationsTests
{
    private static readonly DateOnly Thursday = NextMonday.AddDays(3);

    /// <summary>The MDT, 10:00 to 11:00 next Monday, for both members of staff.</summary>
    private static Meeting Mdt(IEnumerable<Guid>? attendees = null) =>
        NewMeeting(At(NextMonday, 10), At(NextMonday, 11), attendees);

    [TestCase(NotificationKind.MeetingInvited, "Meeting invitation")]
    [TestCase(NotificationKind.MeetingChanged, "Meeting moved")]
    [TestCase(NotificationKind.MeetingCancelled, "Meeting cancelled")]
    [TestCase(NotificationKind.MeetingDeclined, "Meeting declined")]
    public void TitleOf_EachMeetingKind_IsAFixedPhrase(NotificationKind kind, string title)
    {
        MeetingNotifications.TitleOf(kind).Should().Be(title);
        Notification.HasFixedTitle(kind).Should().BeTrue();
    }

    [TestCase(NotificationKind.PathwayAssigned)]
    [TestCase(NotificationKind.AppointmentBooked)]
    public void TitleOf_AnotherKind_Throws(NotificationKind kind)
    {
        var act = () => MeetingNotifications.TitleOf(kind);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // AfterCreating

    [Test]
    public void AfterCreating_InvitesEachAttendee()
    {
        var notifications = MeetingNotifications.AfterCreating(Mdt(), AdminActor, Now);

        notifications.Select(n => n.UserId).Should().Equal(StaffUserId, OtherStaffUserId);
        notifications.Should().OnlyContain(n => n.Kind == NotificationKind.MeetingInvited);
        notifications.Should().OnlyContain(n => n.Title == "Meeting invitation");
        notifications.Should().OnlyContain(n =>
            n.Message == $"{AdminUsername} invited you to '{Title}', Monday 23 November, 10:00 to 11:00.");
        notifications.Should().OnlyContain(n => n.CalendarDate == NextMonday);
        notifications.Should().OnlyContain(n => n.CreatedAtUtc == Now);
        notifications.Should().OnlyContain(n => n.PathwayId == null && n.AppointmentId == null && n.ReadAtUtc == null);
        notifications.Select(n => n.Id).Should().OnlyHaveUniqueItems().And.NotContain(Guid.Empty);
    }

    [Test]
    public void AfterCreating_TheActorGoing_IsNotToldAboutTheirOwnMeeting()
    {
        MeetingNotifications.AfterCreating(Mdt([AdminUserId, StaffUserId]), AdminActor, Now)
            .Select(n => n.UserId).Should().Equal(StaffUserId);
    }

    [Test]
    public void AfterCreating_InSummer_DescribesTheUkTime()
    {
        var meeting = NewMeeting(At(SummerMonday, 10), At(SummerMonday, 11, 30), [StaffUserId]);

        MeetingNotifications.AfterCreating(meeting, AdminActor, Now).Single().Message
            .Should().EndWith("Monday 15 June, 10:00 to 11:30.");
    }

    // AfterChanging

    [Test]
    public void AfterChanging_OnlyTheTitleOrNotes_TellsNobody()
    {
        var meeting = Mdt();
        meeting.Title = OtherTitle;

        MeetingNotifications.AfterChanging(meeting, [StaffUserId, OtherStaffUserId], At(NextMonday, 10), At(NextMonday, 11), AdminActor, Now)
            .Should().BeEmpty();
    }

    [Test]
    public void AfterChanging_Moved_TellsEachAttendeeWhereTo()
    {
        var meeting = NewMeeting(At(Thursday, 9), At(Thursday, 10));

        var notifications = MeetingNotifications.AfterChanging(
            meeting, [StaffUserId, OtherStaffUserId], At(NextMonday, 10), At(NextMonday, 11), AdminActor, Now);

        notifications.Select(n => n.UserId).Should().Equal(StaffUserId, OtherStaffUserId);
        notifications.Should().OnlyContain(n => n.Kind == NotificationKind.MeetingChanged && n.Title == "Meeting moved");
        notifications.Should().OnlyContain(n => n.Message == $"{AdminUsername} moved '{Title}' to Thursday 26 November, 09:00 to 10:00. Please reply again.");
        notifications.Should().OnlyContain(n => n.CalendarDate == Thursday);
    }

    [Test]
    public void AfterChanging_OnlyTheEndMoved_CountsAsMoved()
    {
        var meeting = NewMeeting(At(NextMonday, 10), At(NextMonday, 11, 30), [StaffUserId]);

        MeetingNotifications.AfterChanging(meeting, [StaffUserId], At(NextMonday, 10), At(NextMonday, 11), AdminActor, Now)
            .Single().Kind.Should().Be(NotificationKind.MeetingChanged);
    }

    [Test]
    public void AfterChanging_AttendeeAdded_IsInvitedAndTheRestAreNotTold()
    {
        var meeting = Mdt([StaffUserId, OtherStaffUserId]);

        var notification = MeetingNotifications.AfterChanging(meeting, [StaffUserId], At(NextMonday, 10), At(NextMonday, 11), AdminActor, Now)
            .Should().ContainSingle().Subject;

        notification.UserId.Should().Be(OtherStaffUserId);
        notification.Kind.Should().Be(NotificationKind.MeetingInvited);
        notification.Message.Should().Be($"{AdminUsername} invited you to '{Title}', Monday 23 November, 10:00 to 11:00.");
    }

    [Test]
    public void AfterChanging_AttendeeTakenOff_IsToldItsCancelledWithTheOldTimeAndDate()
    {
        var meeting = NewMeeting(At(Thursday, 9), At(Thursday, 10), [OtherStaffUserId]);

        var notifications = MeetingNotifications.AfterChanging(
            meeting, [StaffUserId, OtherStaffUserId], At(NextMonday, 10), At(NextMonday, 11), AdminActor, Now);

        var removed = notifications.Single(n => n.UserId == StaffUserId);
        removed.Kind.Should().Be(NotificationKind.MeetingCancelled);
        removed.Title.Should().Be("Meeting cancelled");
        removed.Message.Should().Be($"{AdminUsername} took you off '{Title}', Monday 23 November, 10:00 to 11:00.");
        removed.CalendarDate.Should().Be(NextMonday);
        removed.CreatedAtUtc.Should().Be(Now);
        notifications.Single(n => n.UserId == OtherStaffUserId).Kind.Should().Be(NotificationKind.MeetingChanged);
    }

    [Test]
    public void AfterChanging_TheActor_IsNeverToldWhetherAddedTakenOffOrMoved()
    {
        var actor = StaffActor with { Permissions = TestPermissions.Admin };
        var moved = NewMeeting(At(Thursday, 9), At(Thursday, 10), [StaffUserId]);

        MeetingNotifications.AfterChanging(moved, [StaffUserId], At(NextMonday, 10), At(NextMonday, 11), actor, Now).Should().BeEmpty();
        MeetingNotifications.AfterChanging(moved, [], At(NextMonday, 10), At(NextMonday, 11), actor, Now).Should().BeEmpty();
        MeetingNotifications.AfterChanging(Mdt([OtherStaffUserId]), [StaffUserId, OtherStaffUserId], At(NextMonday, 10), At(NextMonday, 11), actor, Now)
            .Should().BeEmpty();
    }

    // AfterCancelling

    [Test]
    public void AfterCancelling_TellsEachAttendeeButTheActor()
    {
        var notifications = MeetingNotifications.AfterCancelling(Mdt([StaffUserId, AdminUserId, OtherStaffUserId]), AdminActor, Now);

        notifications.Select(n => n.UserId).Should().Equal(StaffUserId, OtherStaffUserId);
        notifications.Should().OnlyContain(n => n.Kind == NotificationKind.MeetingCancelled && n.Title == "Meeting cancelled");
        notifications.Should().OnlyContain(n => n.Message == $"{AdminUsername} cancelled '{Title}', Monday 23 November, 10:00 to 11:00.");
        notifications.Should().OnlyContain(n => n.CalendarDate == NextMonday && n.CreatedAtUtc == Now);
    }

    // AfterDeclining

    [Test]
    public void AfterDeclining_TellsTheOrganiserWhoAndWhatAndWhenOnly()
    {
        var meeting = Mdt();

        var notification = MeetingNotifications.AfterDeclining(meeting, StaffActor, Now);

        notification.Should().NotBeNull();
        notification!.UserId.Should().Be(AdminUserId);
        notification.Kind.Should().Be(NotificationKind.MeetingDeclined);
        notification.Title.Should().Be("Meeting declined");
        notification.Message.Should().Be($"{StaffUsername} declined '{Title}', Monday 23 November, 10:00 to 11:00.");
        notification.Message.Should().NotContain(Notes, "a meeting's notes are never in a notification");
        notification.CalendarDate.Should().Be(NextMonday);
        notification.PathwayId.Should().BeNull();
        notification.AppointmentId.Should().BeNull();
        notification.MeetingId.Should().Be(MeetingId, "it opens the meeting, so the organiser sees who declined");
        notification.CreatedAtUtc.Should().Be(Now);
    }

    [Test]
    public void MeetingNotifications_LinkToTheMeeting_ExceptACancellation()
    {
        var mdt = Mdt([StaffUserId, OtherStaffUserId]);
        var moved = NewMeeting(At(Thursday, 9), At(Thursday, 10), [StaffUserId]);

        MeetingNotifications.AfterCreating(mdt, AdminActor, Now).Should().OnlyContain(n => n.MeetingId == MeetingId);
        MeetingNotifications.AfterCancelling(mdt, AdminActor, Now).Should().OnlyContain(n => n.MeetingId == null);
        var changed = MeetingNotifications.AfterChanging(moved, [StaffUserId, OtherStaffUserId], At(NextMonday, 10), At(NextMonday, 11), AdminActor, Now);
        changed.Single(n => n.Kind == NotificationKind.MeetingChanged).MeetingId.Should().Be(MeetingId);
        changed.Single(n => n.Kind == NotificationKind.MeetingCancelled).MeetingId.Should().BeNull("they were taken off it");
    }

    [Test]
    public void AfterChanging_Moved_SomeoneWhoseDeclineStands_IsToldSo_NotAskedAgain()
    {
        var moved = NewMeeting(At(Thursday, 9), At(Thursday, 10));
        moved.Attendees.Single(a => a.StaffUserId == StaffUserId).Reply = MeetingReply.Declined;

        var notifications = MeetingNotifications.AfterChanging(
            moved, [StaffUserId, OtherStaffUserId], At(NextMonday, 10), At(NextMonday, 11), AdminActor, Now);

        notifications.Single(n => n.UserId == StaffUserId).Message.Should().Be(
            $"{AdminUsername} moved '{Title}' to Thursday 26 November, 09:00 to 10:00. You have something else on then, so your decline stands.");
        notifications.Single(n => n.UserId == OtherStaffUserId).Message.Should().EndWith("Please reply again.");
    }

    [Test]
    public void AfterDeclining_TheOrganiserThemselves_TellsNobody()
    {
        MeetingNotifications.AfterDeclining(Mdt([AdminUserId, StaffUserId]), AdminActor, Now).Should().BeNull();
    }
}
