using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Domain.Calendar;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;
using static MedApp.UnitTests.Common.Constants.MeetingsTestConstants;

namespace MedApp.UnitTests.Application.Calendar.Common;

[TestFixture]
public sealed class MeetingMapperTests
{
    [Test]
    public void UserIds_Meeting_IsItsOrganiserThenAttendeesEachOnce()
    {
        var meeting = NewMeeting(At(NextMonday, 10), At(NextMonday, 11), [StaffUserId, AdminUserId, OtherStaffUserId]);

        MeetingMapper.UserIds(meeting).Should().Equal(AdminUserId, StaffUserId, OtherStaffUserId);
    }

    [Test]
    public void ToResponse_Meeting_CopiesEveryField()
    {
        var meeting = NewMeeting(At(NextMonday, 10), At(NextMonday, 11, 30));
        meeting.LastUpdated = Now;

        var response = MeetingMapper.ToResponse(meeting, Usernames, AdminActor, Now);

        response.Id.Should().Be(MeetingId);
        response.Title.Should().Be(Title);
        response.Notes.Should().Be(Notes);
        response.Date.Should().Be(NextMonday);
        response.StartUtc.Should().Be(At(NextMonday, 10));
        response.EndUtc.Should().Be(At(NextMonday, 11, 30));
        response.OrganiserUserId.Should().Be(AdminUserId);
        response.OrganiserUsername.Should().Be(AdminUsername);
        response.CanEdit.Should().BeTrue();
        response.CreatedAtUtc.Should().Be(Now.AddDays(-1));
        response.LastUpdated.Should().Be(Now);
    }

    [Test]
    public void ToResponse_Attendees_ByUsernameIgnoringCaseThenDeletedAccountsLast()
    {
        var deleted = Guid.Parse("0000000d-0000-0000-0000-0000000000a1");
        var alsoDeleted = Guid.Parse("0000000d-0000-0000-0000-0000000000a0");
        var upper = Guid.Parse("0000000d-0000-0000-0000-0000000000b0");
        var usernames = new Dictionary<Guid, string>(Usernames) { [upper] = "Calendar.middle" };
        var meeting = NewMeeting(At(NextMonday, 10), At(NextMonday, 11), [deleted, StaffUserId, upper, alsoDeleted, AdminUserId]);

        var attendees = MeetingMapper.ToResponse(meeting, usernames, StaffActor, Now).Attendees;

        attendees.Select(a => a.Username).Should().Equal(AdminUsername, "Calendar.middle", StaffUsername, null, null);
        attendees.Select(a => a.UserId).Should().Equal(AdminUserId, upper, StaffUserId, alsoDeleted, deleted);
    }

    [Test]
    public void ToResponse_OrganiserDeleted_HasNoUsername()
    {
        var meeting = NewMeeting(At(NextMonday, 10), At(NextMonday, 11));

        MeetingMapper.ToResponse(meeting, new Dictionary<Guid, string>(), AdminActor, Now).OrganiserUsername.Should().BeNull();
    }

    [TestCase(true, 0, true)]
    [TestCase(true, 7, true)]
    [TestCase(true, -3, false)]
    [TestCase(false, 7, false)]
    [TestCase(false, 0, false)]
    public void ToResponse_CanEdit_OnlyAnAdminFromToday(bool isAdmin, int daysFromToday, bool canEdit)
    {
        var date = Today.AddDays(daysFromToday);
        var meeting = NewMeeting(At(date, 10), At(date, 11));

        MeetingMapper.ToResponse(meeting, Usernames, isAdmin ? AdminActor : StaffActor, Now).CanEdit.Should().Be(canEdit);
    }

    [Test]
    public void ToResponse_SummerMeeting_IsOnItsUkDate()
    {
        var meeting = NewMeeting(At(SummerMonday, 8), At(SummerMonday, 9));

        var response = MeetingMapper.ToResponse(meeting, Usernames, StaffActor, Now);

        response.Date.Should().Be(SummerMonday);
        response.StartUtc.Should().Be(new DateTime(2026, 6, 15, 7, 0, 0, DateTimeKind.Utc));
    }

    [Test]
    public void ToResponse_TimeRange_CopiesItsTimes()
    {
        var response = MeetingMapper.ToResponse(new TimeRange(At(NextMonday, 9), At(NextMonday, 9, 45)));

        response.StartUtc.Should().Be(At(NextMonday, 9));
        response.EndUtc.Should().Be(At(NextMonday, 9, 45));
    }

    [Test]
    public void ToResponse_MeetingWithNoAttendees_HasNone()
    {
        var meeting = new Meeting { Id = MeetingId, Title = Title, StartUtc = At(NextMonday, 10), EndUtc = At(NextMonday, 11) };

        MeetingMapper.ToResponse(meeting, Usernames, AdminActor, Now).Attendees.Should().BeEmpty();
    }

    [Test]
    public void ToResponse_Attendees_CarryTheirRepliesByName()
    {
        var meeting = NewMeeting(At(NextMonday, 10), At(NextMonday, 11), [StaffUserId, OtherStaffUserId, AdminUserId]);
        meeting.Attendees[0].Reply = MeetingReply.Accepted;
        meeting.Attendees[0].RepliedAtUtc = Now.AddHours(-2);
        meeting.Attendees[1].Reply = MeetingReply.Declined;
        meeting.Attendees[1].RepliedAtUtc = Now.AddHours(-1);

        var attendees = MeetingMapper.ToResponse(meeting, Usernames, AdminActor, Now).Attendees.ToDictionary(a => a.UserId);

        attendees[StaffUserId].Reply.Should().Be("Accepted");
        attendees[StaffUserId].RepliedAtUtc.Should().Be(Now.AddHours(-2));
        attendees[OtherStaffUserId].Reply.Should().Be("Declined");
        attendees[OtherStaffUserId].RepliedAtUtc.Should().Be(Now.AddHours(-1));
        attendees[AdminUserId].Reply.Should().Be("NoReply");
        attendees[AdminUserId].RepliedAtUtc.Should().BeNull();
    }

    [Test]
    public void ToResponse_ViewerGoing_SeesTheirOwnReplyAndCanReply()
    {
        var meeting = NewMeeting(At(NextMonday, 10), At(NextMonday, 11));
        meeting.Attendees.Single(a => a.StaffUserId == StaffUserId).Reply = MeetingReply.Declined;

        var response = MeetingMapper.ToResponse(meeting, Usernames, StaffActor, Now);

        response.MyReply.Should().Be("Declined");
        response.CanReply.Should().BeTrue();
    }

    [Test]
    public void ToResponse_ViewerNotGoing_HasNoReplyAndCantReply()
    {
        // The organiser (an admin) isn't going unless listed, and can't reply for anyone.
        var response = MeetingMapper.ToResponse(NewMeeting(At(NextMonday, 10), At(NextMonday, 11)), Usernames, AdminActor, Now);

        response.MyReply.Should().BeNull();
        response.CanReply.Should().BeFalse();
    }

    [TestCase(-60, true)]
    [TestCase(-1, true)]
    [TestCase(0, false)]
    [TestCase(30, false)]
    public void ToResponse_CanReply_UntilItEnds(int minutesAfterEnd, bool canReply)
    {
        // Under way (started an hour ago) still takes a reply; from its end, none.
        var meeting = NewMeeting(Now.AddMinutes(-60 - minutesAfterEnd), Now.AddMinutes(-minutesAfterEnd));

        MeetingMapper.ToResponse(meeting, Usernames, StaffActor, Now).CanReply.Should().Be(canReply);
    }
}
