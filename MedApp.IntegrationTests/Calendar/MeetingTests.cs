using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Authentication.Requests;
using MedApp.Contracts.Calendar.Requests;
using MedApp.Contracts.Calendar.Responses;
using MedApp.Contracts.Notifications.Responses;
using MedApp.Contracts.Patients.Responses;
using MedApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Mvc;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Calendar;

/// <summary>
/// Meetings, the team view and suggested times against SQL Server: a meeting is busy time on every attendee's
/// calendar, clashes are refused naming who and what (two meetings racing for one time included), admins arrange them
/// while staff only read them, attendees are told, and an account that goes leaves its meetings to come.
/// </summary>
[TestFixture]
public sealed class MeetingTests : IntegrationTest
{
    private const string Title = "Synthetic team meeting";

    private HttpClient _admin = null!;
    private Guid _adminId;
    private StaffAccount _staff = null!;
    private StaffAccount _other = null!;
    private DateOnly _day;

    [SetUp]
    public async Task SetUpMeetingsAsync()
    {
        _admin = await SignInAsAdminAsync();
        _adminId = await GetUserIdAsync(_admin, AdminUsername);
        _staff = await CreateStaffAsync(UserRole);
        _other = await CreateStaffAsync(UserRole);
        _day = NextWorkingDay();
    }

    private static MeetingRequest Meeting(DateTime startUtc, DateTime endUtc, params Guid[] attendees) => new()
    {
        Title = Title,
        Notes = "Synthetic notes about staff.",
        StartUtc = startUtc,
        EndUtc = endUtc,
        AttendeeUserIds = [.. attendees],
    };

    /// <summary>A change to a meeting, made from the copy <paramref name="seen"/> (its version is sent with it).</summary>
    private static MeetingRequest Change(MeetingResponse seen, DateTime startUtc, DateTime endUtc, params Guid[] attendees)
    {
        var request = Meeting(startUtc, endUtc, attendees);
        request.ExpectedLastUpdated = seen.LastUpdated;
        return request;
    }

    private static Task<HttpResponseMessage> PostMeetingAsync(HttpClient client, MeetingRequest request) =>
        client.PostAsJsonAsync("/api/calendar/meetings", request);

    private async Task<MeetingResponse> CreateMeetingAsync(DateTime startUtc, DateTime endUtc, params Guid[] attendees) =>
        await (await PostMeetingAsync(_admin, Meeting(startUtc, endUtc, attendees))).ReadAsAsync<MeetingResponse>(HttpStatusCode.Created);

    [Test]
    public async Task CreateMeeting_ShowsOnEveryAttendeesCalendarWithItsDetails()
    {
        var meeting = await CreateMeetingAsync(UkAt(_day, 9), UkAt(_day, 10), _staff.UserId, _other.UserId);

        meeting.Should().BeEquivalentTo(new { Title, OrganiserUserId = _adminId, OrganiserUsername = AdminUsername, Date = _day, CanEdit = true });
        meeting.Attendees.Select(a => a.Username).Should().BeEquivalentTo([_staff.Username, _other.Username]);

        foreach (var attendee in new[] { _staff, _other })
        {
            var calendar = await GetCalendarAsync(attendee.Client, attendee.UserId, _day);
            calendar.Meetings.Should().ContainSingle().Which.Should().BeEquivalentTo(new { meeting.Id, Title, CanEdit = false });
        }

        // The organiser isn't going unless they're listed.
        (await GetCalendarAsync(_admin, _adminId, _day)).Meetings.Should().BeEmpty();
        // Anyone with a staff role can read it.
        (await (await _other.Client.GetAsync($"/api/calendar/meetings/{meeting.Id}")).ReadAsAsync<MeetingResponse>()).Notes
            .Should().Be("Synthetic notes about staff.");
    }

    [Test]
    public async Task CreateMeeting_OverAnAttendeesTemplateLunch_Returns409NamingWhoAndWhat()
    {
        await GiveDefaultWeekAsync(_admin, _other.UserId, TemplateBlock(12, 13));

        var response = await PostMeetingAsync(_admin, Meeting(UkAt(_day, 12, 30), UkAt(_day, 13, 30), _staff.UserId, _other.UserId));

        (await response.ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict)).Title.Should().Be($"{_other.Username} has Lunch, 12:00 to 13:00.");
        (await GetCalendarAsync(_staff.Client, _staff.UserId, _day)).Meetings.Should().BeEmpty();
    }

    [Test]
    public async Task BookingAndBlocking_OverAMeeting_Return409NamingTheMeeting()
    {
        await CreateMeetingAsync(UkAt(_day, 9), UkAt(_day, 10), _staff.UserId);
        var patient = await CreatePatientAsync(_staff.Client);

        var booking = await PostBookingAsync(_staff.Client, patient.Id, _staff.UserId, UkAt(_day, 9, 30));
        var block = await PostBlockAsync(_staff.Client, _staff.UserId, UkAt(_day, 8), UkAt(_day, 9, 15));

        (await booking.ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict)).Title.Should().Be($"That time overlaps the meeting '{Title}', 09:00 to 10:00.");
        (await block.ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict)).Title.Should().Be($"That time overlaps the meeting '{Title}', 09:00 to 10:00.");
    }

    [Test]
    public async Task CreateMeeting_TwoRacingForOneTime_OneWinsAndTheOtherGets409()
    {
        var slots = Enumerable.Range(0, 3).Select(i => UkAt(_day, 9 + i)).ToList();
        var secondAdmin = await CreateStaffAsync(AdminRole, UserRole);

        foreach (var slot in slots)
        {
            // Different attendee lists, one person in both: their lock serialises the two.
            var responses = await Task.WhenAll(
                PostMeetingAsync(_admin, Meeting(slot, slot.AddMinutes(45), _staff.UserId)),
                PostMeetingAsync(secondAdmin.Client, Meeting(slot.AddMinutes(15), slot.AddHours(1), _other.UserId, _staff.UserId)));

            responses.Select(r => r.StatusCode).Should().BeEquivalentTo([HttpStatusCode.Created, HttpStatusCode.Conflict]);
            (await responses.Single(r => r.StatusCode == HttpStatusCode.Conflict).ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict))
                .Title.Should().StartWith($"{_staff.Username} has the meeting '{Title}'");
        }

        (await GetCalendarAsync(_staff.Client, _staff.UserId, _day)).Meetings.Should().HaveCount(slots.Count);
    }

    [Test]
    public async Task UpdateMeeting_MovedAndAttendeesChanged_ChecksTheNewTimeAndTellsEachPerson()
    {
        var meeting = await CreateMeetingAsync(UkAt(_day, 9), UkAt(_day, 10), _staff.UserId, _other.UserId);
        var third = await CreateStaffAsync(UserRole);

        // Overlapping its own old time is fine: the meeting itself isn't a clash.
        var moved = Change(meeting, UkAt(_day, 9, 30), UkAt(_day, 10, 30), _staff.UserId, third.UserId);
        var response = await _admin.PutAsJsonAsync($"/api/calendar/meetings/{meeting.Id}", moved);

        (await response.ReadAsAsync<MeetingResponse>()).Attendees.Select(a => a.UserId).Should().BeEquivalentTo([_staff.UserId, third.UserId]);
        (await GetCalendarAsync(_other.Client, _other.UserId, _day)).Meetings.Should().BeEmpty();

        (await LatestNotificationAsync(_staff.Client)).Should().BeEquivalentTo(new { Kind = "MeetingChanged", Title = "Meeting moved", CalendarDate = (DateOnly?)_day });
        (await LatestNotificationAsync(_other.Client)).Should().BeEquivalentTo(new { Kind = "MeetingCancelled", Title = "Meeting cancelled" });
        (await LatestNotificationAsync(third.Client)).Should().BeEquivalentTo(new { Kind = "MeetingInvited", Title = "Meeting invitation" });
    }

    [Test]
    public async Task UpdateMeeting_FromACopyOlderThanAnotherAdminsChange_Returns409AndKeepsTheirs()
    {
        var meeting = await CreateMeetingAsync(UkAt(_day, 9), UkAt(_day, 10), _staff.UserId);
        var secondAdmin = await CreateStaffAsync(AdminRole, UserRole);

        // Another admin adds _other.
        await (await secondAdmin.Client.PutAsJsonAsync($"/api/calendar/meetings/{meeting.Id}",
                Change(meeting, UkAt(_day, 9), UkAt(_day, 10), _staff.UserId, _other.UserId)))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);

        // This admin, still looking at the copy from before, drags it half an hour on, with the attendees it showed.
        var stale = await _admin.PutAsJsonAsync($"/api/calendar/meetings/{meeting.Id}",
            Change(meeting, UkAt(_day, 9, 30), UkAt(_day, 10, 30), _staff.UserId));

        (await stale.ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict)).Title.Should().Be(MeetingLimits.ChangedMeanwhile);
        var kept = await (await _admin.GetAsync($"/api/calendar/meetings/{meeting.Id}")).ReadAsAsync<MeetingResponse>();
        kept.Attendees.Select(a => a.UserId).Should().BeEquivalentTo([_staff.UserId, _other.UserId]);
        kept.StartUtc.Should().Be(UkAt(_day, 9));

        // From the copy that change returned, it goes through; without saying which copy, it's a 400.
        await (await _admin.PutAsJsonAsync($"/api/calendar/meetings/{meeting.Id}",
                Change(kept, UkAt(_day, 9, 30), UkAt(_day, 10, 30), _staff.UserId, _other.UserId)))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await _admin.PutAsJsonAsync($"/api/calendar/meetings/{meeting.Id}", Meeting(UkAt(_day, 11), UkAt(_day, 12), _staff.UserId)))
            .ShouldHaveStatusAsync(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task UpdateMeeting_AnAttendeeWhoseAccountHasGone_DropsThemRatherThanRefusing()
    {
        // A meeting already under way when an attendee's account goes keeps them: only meetings to come lose them.
        var meeting = await CreateMeetingAsync(UkAt(_day, 9), UkAt(_day, 11), _staff.UserId, _other.UserId);
        MoveClockPast(UkAt(_day, 9));
        await (await _admin.DeleteAsJsonAsync($"/api/auth/users/{_other.Username}", new DeleteUserRequest(AdminPassword)))
            .ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        var started = await (await _admin.GetAsync($"/api/calendar/meetings/{meeting.Id}")).ReadAsAsync<MeetingResponse>();
        started.Attendees.Should().HaveCount(2);

        // Cut short, with everyone the dialog shows: the account that has gone is dropped, not a 404.
        var response = await _admin.PutAsJsonAsync($"/api/calendar/meetings/{meeting.Id}",
            Change(started, UkAt(_day, 9), UkAt(_day, 10), _staff.UserId, _other.UserId));

        (await response.ReadAsAsync<MeetingResponse>()).Attendees.Select(a => a.UserId).Should().Equal(_staff.UserId);
    }

    [Test]
    public async Task CancelMeeting_DeletesItAndTellsTheAttendees()
    {
        var meeting = await CreateMeetingAsync(UkAt(_day, 9), UkAt(_day, 10), _staff.UserId);
        (await LatestNotificationAsync(_staff.Client)).Should().BeEquivalentTo(new { Kind = "MeetingInvited", CalendarDate = (DateOnly?)_day });

        await (await _admin.DeleteAsync($"/api/calendar/meetings/{meeting.Id}")).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        await (await _staff.Client.GetAsync($"/api/calendar/meetings/{meeting.Id}")).ShouldHaveStatusAsync(HttpStatusCode.NotFound);
        (await GetCalendarAsync(_staff.Client, _staff.UserId, _day)).Meetings.Should().BeEmpty();
        var cancelled = await LatestNotificationAsync(_staff.Client);
        cancelled.Kind.Should().Be("MeetingCancelled");
        cancelled.Message.Should().Contain(Title).And.Contain(AdminUsername);
    }

    [Test]
    public async Task Meetings_AsStaff_WritesReturn403AndAvailabilityToo()
    {
        var meeting = await CreateMeetingAsync(UkAt(_day, 9), UkAt(_day, 10), _staff.UserId);
        var client = _staff.Client;

        var refused = new[]
        {
            await PostMeetingAsync(client, Meeting(UkAt(_day, 11), UkAt(_day, 12), _staff.UserId)),
            await client.PutAsJsonAsync($"/api/calendar/meetings/{meeting.Id}", Change(meeting, UkAt(_day, 11), UkAt(_day, 12), _staff.UserId)),
            await client.DeleteAsync($"/api/calendar/meetings/{meeting.Id}"),
            await client.GetAsync($"/api/calendar/availability?staffUserIds={_staff.UserId}&from={_day:yyyy-MM-dd}&to={_day:yyyy-MM-dd}&durationMinutes=30"),
        };

        refused.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.Forbidden));
        await (await client.GetAsync($"/api/calendar/meetings/{meeting.Id}")).ShouldHaveStatusAsync(HttpStatusCode.OK);
        // A team's calendar is anyone's (MC11).
        await (await client.GetAsync($"/api/calendar/team?date={_day:yyyy-MM-dd}")).ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    [Test]
    public async Task CreateMeeting_OnAPastDayOrOutsideTheWorkingDay_IsRefused()
    {
        var yesterday = UkToday.AddDays(-1);

        var past = await PostMeetingAsync(_admin, Meeting(UkAt(yesterday, 9), UkAt(yesterday, 10), _staff.UserId));
        var evening = await PostMeetingAsync(_admin, Meeting(UkAt(_day, 17, 30), UkAt(_day, 18, 30), _staff.UserId));
        var nobody = await PostMeetingAsync(_admin, Meeting(UkAt(_day, 9), UkAt(_day, 10)));

        // A weekend "yesterday" is outside the working day (400) rather than past (409); either way it's refused.
        past.StatusCode.Should().BeOneOf(HttpStatusCode.Conflict, HttpStatusCode.BadRequest);
        await evening.ShouldHaveStatusAsync(HttpStatusCode.BadRequest);
        await nobody.ShouldHaveStatusAsync(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task TeamCalendar_ShowsEachPersonsDayAndWhenEveryoneIsFree()
    {
        await GiveDefaultWeekAsync(_admin, _staff.UserId, TemplateBlock(12, 13));
        await CreateBlockAsync(_other.Client, _other.UserId, UkAt(_day, 9), UkAt(_day, 11));
        await CreateMeetingAsync(UkAt(_day, 15), UkAt(_day, 16), _staff.UserId, _other.UserId);

        var team = await (await _admin.GetAsync($"/api/calendar/team?date={_day:yyyy-MM-dd}&staffUserIds={_staff.UserId}&staffUserIds={_other.UserId}"))
            .ReadAsAsync<TeamCalendarResponse>();

        team.Members.Select(m => m.StaffUserId).Should().BeEquivalentTo([_staff.UserId, _other.UserId]);
        team.Members.Should().AllSatisfy(m => m.Meetings.Should().ContainSingle());
        team.Day.Should().BeEquivalentTo(new { Date = _day, IsWorkingDay = true, WorkStartUtc = UkAt(_day, 8), WorkEndUtc = UkAt(_day, 18) });
        team.EveryoneFree.Select(f => (f.StartUtc, f.EndUtc)).Should().Equal(
            (UkAt(_day, 8), UkAt(_day, 9)),
            (UkAt(_day, 11), UkAt(_day, 12)),
            (UkAt(_day, 13), UkAt(_day, 15)),
            (UkAt(_day, 16), UkAt(_day, 18)));

        // With nobody named, it's everyone with a staff role, the admin included.
        var everyone = await (await _admin.GetAsync($"/api/calendar/team?date={_day:yyyy-MM-dd}")).ReadAsAsync<TeamCalendarResponse>();
        everyone.Members.Select(m => m.StaffUsername).Should().Contain([AdminUsername, _staff.Username, _other.Username]);
    }

    [Test]
    public async Task Availability_SuggestsTimesEveryoneIsFreeAndSaysWhoIsBusyWhen()
    {
        // _staff is busy until 12:00, _other from 13:00 on: the only common hour is 12:00 to 13:00.
        await CreateBlockAsync(_staff.Client, _staff.UserId, UkAt(_day, 8), UkAt(_day, 12));
        await CreateBlockAsync(_other.Client, _other.UserId, UkAt(_day, 13), UkAt(_day, 18));

        var availability = await (await _admin.GetAsync(
                $"/api/calendar/availability?staffUserIds={_staff.UserId}&staffUserIds={_other.UserId}&from={_day:yyyy-MM-dd}&to={_day:yyyy-MM-dd}&durationMinutes=45"))
            .ReadAsAsync<AvailabilityResponse>();

        availability.Days.Should().ContainSingle().Which.Free.Select(f => (f.StartUtc, f.EndUtc)).Should().Equal((UkAt(_day, 12), UkAt(_day, 13)));
        availability.Suggestions.Should().ContainSingle().Which.Should().BeEquivalentTo(new { StartUtc = UkAt(_day, 12), EndUtc = UkAt(_day, 12, 45) });
        availability.Busy.Select(b => (b.Username, b.StartUtc)).Should().BeEquivalentTo([(_staff.Username, UkAt(_day, 8)), (_other.Username, UkAt(_day, 13))]);

        // A suggestion is a time the server takes.
        var suggested = availability.Suggestions[0];
        await (await PostMeetingAsync(_admin, Meeting(suggested.StartUtc, suggested.EndUtc, _staff.UserId, _other.UserId)))
            .ShouldHaveStatusAsync(HttpStatusCode.Created);
    }

    [Test]
    public async Task Availability_ForAMeetingBeingMoved_DoesNotCountItAsBusy()
    {
        var meeting = await CreateMeetingAsync(UkAt(_day, 8), UkAt(_day, 9), _staff.UserId);
        var url = $"/api/calendar/availability?staffUserIds={_staff.UserId}&from={_day:yyyy-MM-dd}&to={_day:yyyy-MM-dd}&durationMinutes=60";

        var asIs = await (await _admin.GetAsync(url)).ReadAsAsync<AvailabilityResponse>();
        var moving = await (await _admin.GetAsync($"{url}&exceptMeetingId={meeting.Id}")).ReadAsAsync<AvailabilityResponse>();

        asIs.Suggestions[0].StartUtc.Should().Be(UkAt(_day, 9));
        moving.Suggestions[0].StartUtc.Should().Be(UkAt(_day, 8));
        moving.Busy.Should().BeEmpty();
    }

    [Test]
    public async Task DeleteUser_GoingToMeetings_TakesThemOffAndCancelsAMeetingLeftEmpty()
    {
        var shared = await CreateMeetingAsync(UkAt(_day, 9), UkAt(_day, 10), _staff.UserId, _other.UserId);
        var theirs = await CreateMeetingAsync(UkAt(_day, 11), UkAt(_day, 12), _other.UserId);

        await (await _admin.DeleteAsJsonAsync($"/api/auth/users/{_other.Username}", new DeleteUserRequest(AdminPassword)))
            .ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        (await (await _admin.GetAsync($"/api/calendar/meetings/{shared.Id}")).ReadAsAsync<MeetingResponse>())
            .Attendees.Should().ContainSingle().Which.UserId.Should().Be(_staff.UserId);
        await (await _admin.GetAsync($"/api/calendar/meetings/{theirs.Id}")).ShouldHaveStatusAsync(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task SetRoles_TakingEveryStaffRole_TakesThemOffUpcomingMeetings()
    {
        var meeting = await CreateMeetingAsync(UkAt(_day, 9), UkAt(_day, 10), _staff.UserId, _other.UserId);

        await (await _admin.PutAsJsonAsync($"/api/auth/users/{_other.Username}/roles", new SetUserRolesRequest([])))
            .ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        (await (await _admin.GetAsync($"/api/calendar/meetings/{meeting.Id}")).ReadAsAsync<MeetingResponse>())
            .Attendees.Select(a => a.UserId).Should().Equal(_staff.UserId);
    }

    private static async Task<NotificationResponse> LatestNotificationAsync(HttpClient client) =>
        (await (await client.GetAsync("/api/notifications")).ReadAsAsync<NotificationsResponse>()).Items
        .OrderByDescending(n => n.CreatedAtUtc)
        .First();
}
