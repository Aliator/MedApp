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

        var response = MeetingMapper.ToResponse(meeting, Usernames, isAdmin: true, Today);

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

        var attendees = MeetingMapper.ToResponse(meeting, usernames, isAdmin: false, Today).Attendees;

        attendees.Select(a => a.Username).Should().Equal(AdminUsername, "Calendar.middle", StaffUsername, null, null);
        attendees.Select(a => a.UserId).Should().Equal(AdminUserId, upper, StaffUserId, alsoDeleted, deleted);
    }

    [Test]
    public void ToResponse_OrganiserDeleted_HasNoUsername()
    {
        var meeting = NewMeeting(At(NextMonday, 10), At(NextMonday, 11));

        MeetingMapper.ToResponse(meeting, new Dictionary<Guid, string>(), isAdmin: true, Today).OrganiserUsername.Should().BeNull();
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

        MeetingMapper.ToResponse(meeting, Usernames, isAdmin, Today).CanEdit.Should().Be(canEdit);
    }

    [Test]
    public void ToResponse_SummerMeeting_IsOnItsUkDate()
    {
        var meeting = NewMeeting(At(SummerMonday, 8), At(SummerMonday, 9));

        var response = MeetingMapper.ToResponse(meeting, Usernames, isAdmin: false, Today);

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

        MeetingMapper.ToResponse(meeting, Usernames, isAdmin: true, Today).Attendees.Should().BeEmpty();
    }
}
