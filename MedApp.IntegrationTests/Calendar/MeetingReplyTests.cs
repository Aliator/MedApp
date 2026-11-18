using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Calendar.Requests;
using MedApp.Contracts.Calendar.Responses;
using MedApp.Contracts.Notifications.Responses;
using MedApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Mvc;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Calendar;

/// <summary>
/// Replies to meetings against SQL Server: only an attendee replies, for themselves; a cancelled meeting is a 404; a
/// decline frees the time on the decliner's calendar (a booking there is accepted) and tells the organiser; and moving
/// the meeting puts every reply back to none.
/// </summary>
[TestFixture]
public sealed class MeetingReplyTests : IntegrationTest
{
    private const string Title = "Synthetic replies meeting";

    private HttpClient _admin = null!;
    private StaffAccount _staff = null!;
    private StaffAccount _other = null!;
    private DateOnly _day;

    [SetUp]
    public async Task SetUpRepliesAsync()
    {
        _admin = await SignInAsAdminAsync();
        _staff = await CreateStaffAsync(UserRole);
        _other = await CreateStaffAsync(UserRole);
        _day = NextWorkingDay();
    }

    private async Task<MeetingResponse> CreateMeetingAsync(DateTime startUtc, DateTime endUtc, params Guid[] attendees) =>
        await (await _admin.PostAsJsonAsync("/api/calendar/meetings", new MeetingRequest
        {
            Title = Title,
            StartUtc = startUtc,
            EndUtc = endUtc,
            AttendeeUserIds = [.. attendees],
        })).ReadAsAsync<MeetingResponse>(HttpStatusCode.Created);

    private static Task<HttpResponseMessage> ReplyAsync(HttpClient client, Guid meetingId, string reply) =>
        client.PutAsJsonAsync($"/api/calendar/meetings/{meetingId}/reply", new MeetingReplyRequest { Reply = reply });

    private async Task<MeetingResponse> GetMeetingAsync(HttpClient client, Guid meetingId) =>
        await (await client.GetAsync($"/api/calendar/meetings/{meetingId}")).ReadAsAsync<MeetingResponse>();

    private static async Task<IReadOnlyList<NotificationResponse>> NotificationsAsync(HttpClient client) =>
        (await (await client.GetAsync("/api/notifications")).ReadAsAsync<NotificationsResponse>()).Items;

    [Test]
    public async Task Reply_Attendee_IsStoredForThemAlone_AndTheyCanChangeIt()
    {
        var meeting = await CreateMeetingAsync(UkAt(_day, 9), UkAt(_day, 10), _staff.UserId, _other.UserId);

        var declined = await (await ReplyAsync(_staff.Client, meeting.Id, "Declined")).ReadAsAsync<MeetingResponse>();
        declined.Should().BeEquivalentTo(new { MyReply = "Declined", CanReply = true });
        declined.LastUpdated.Should().Be(meeting.LastUpdated, "a reply isn't a change to the meeting");

        var accepted = await (await ReplyAsync(_staff.Client, meeting.Id, "Accepted")).ReadAsAsync<MeetingResponse>();
        accepted.MyReply.Should().Be("Accepted");

        // Everyone sees everyone's reply; only an attendee has one of their own.
        var asAdmin = await GetMeetingAsync(_admin, meeting.Id);
        asAdmin.Attendees.Select(a => (a.UserId, a.Reply)).Should().BeEquivalentTo([(_staff.UserId, "Accepted"), (_other.UserId, "NoReply")]);
        asAdmin.Attendees.Single(a => a.UserId == _staff.UserId).RepliedAtUtc.Should().NotBeNull();
        asAdmin.Should().BeEquivalentTo(new { MyReply = (string?)null, CanReply = false });
        (await GetMeetingAsync(_other.Client, meeting.Id)).Should().BeEquivalentTo(new { MyReply = "NoReply", CanReply = true });
    }

    [Test]
    public async Task Reply_SomeoneNotGoing_Returns403_EvenTheOrganiser()
    {
        var meeting = await CreateMeetingAsync(UkAt(_day, 9), UkAt(_day, 10), _staff.UserId);

        var outsider = await ReplyAsync(_other.Client, meeting.Id, "Declined");
        var organiser = await ReplyAsync(_admin, meeting.Id, "Declined");

        (await outsider.ReadAsAsync<ProblemDetails>(HttpStatusCode.Forbidden)).Title.Should().Be(MeetingReplies.NotAnAttendee);
        await organiser.ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
        (await GetMeetingAsync(_admin, meeting.Id)).Attendees.Should().OnlyContain(a => a.Reply == "NoReply");
    }

    [Test]
    public async Task Reply_NotAReply_Returns400()
    {
        var meeting = await CreateMeetingAsync(UkAt(_day, 9), UkAt(_day, 10), _staff.UserId);

        await (await ReplyAsync(_staff.Client, meeting.Id, "NoReply")).ShouldHaveStatusAsync(HttpStatusCode.BadRequest);
        await (await ReplyAsync(_staff.Client, meeting.Id, "Maybe")).ShouldHaveStatusAsync(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Reply_CancelledMeeting_Returns404()
    {
        var meeting = await CreateMeetingAsync(UkAt(_day, 9), UkAt(_day, 10), _staff.UserId);
        await (await _admin.DeleteAsync($"/api/calendar/meetings/{meeting.Id}")).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        await (await ReplyAsync(_staff.Client, meeting.Id, "Accepted")).ShouldHaveStatusAsync(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Reply_OnceItHasEnded_Returns409()
    {
        var meeting = await CreateMeetingAsync(UkAt(_day, 9), UkAt(_day, 10), _staff.UserId);
        MoveClockPast(UkAt(_day, 10));

        var response = await ReplyAsync(_staff.Client, meeting.Id, "Declined");

        (await response.ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict)).Title.Should().Be(MeetingReplies.Ended);
    }

    [Test]
    public async Task Decline_FreesTheTimeForABooking_AndTellsTheOrganiser()
    {
        var meeting = await CreateMeetingAsync(UkAt(_day, 9), UkAt(_day, 10), _staff.UserId, _other.UserId);
        var patient = await CreatePatientAsync(_staff.Client);

        await (await PostBookingAsync(_staff.Client, patient.Id, _staff.UserId, UkAt(_day, 9, 30))).ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        await (await ReplyAsync(_staff.Client, meeting.Id, "Declined")).ShouldHaveStatusAsync(HttpStatusCode.OK);

        // Their time is free now; the others going are still busy.
        await (await PostBookingAsync(_staff.Client, patient.Id, _staff.UserId, UkAt(_day, 9, 30))).ShouldHaveStatusAsync(HttpStatusCode.Created);
        (await (await PostBookingAsync(_staff.Client, patient.Id, _other.UserId, UkAt(_day, 9, 30))).ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict))
            .Title.Should().Be($"That time overlaps the meeting '{Title}', 09:00 to 10:00.");

        // The meeting still shows on their calendar, with their reply (the web app fades it).
        (await GetCalendarAsync(_staff.Client, _staff.UserId, _day)).Meetings.Should().ContainSingle().Which.MyReply.Should().Be("Declined");

        var told = (await NotificationsAsync(_admin)).Should().ContainSingle(n => n.Kind == "MeetingDeclined").Subject;
        told.Title.Should().Be("Meeting declined");
        told.Message.Should().StartWith($"{_staff.Username} declined '{Title}', ");
        told.CalendarDate.Should().Be(_day);

        // Accepting again now finds the time taken: a 409 naming it, and the decline stands.
        var again = await ReplyAsync(_staff.Client, meeting.Id, "Accepted");
        (await again.ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict)).Title.Should().StartWith(MeetingReplies.TakenSinceDeclining);
        (await GetMeetingAsync(_staff.Client, meeting.Id)).MyReply.Should().Be("Declined");
    }

    [Test]
    public async Task Accept_TellsNobody()
    {
        var meeting = await CreateMeetingAsync(UkAt(_day, 9), UkAt(_day, 10), _staff.UserId);

        await (await ReplyAsync(_staff.Client, meeting.Id, "Accepted")).ShouldHaveStatusAsync(HttpStatusCode.OK);

        (await NotificationsAsync(_admin)).Should().NotContain(n => n.Kind == "MeetingDeclined");
    }

    [Test]
    public async Task UpdateMeeting_Moved_PutsEveryReplyBackToNoReply_ButATitleChangeKeepsThem()
    {
        var meeting = await CreateMeetingAsync(UkAt(_day, 9), UkAt(_day, 10), _staff.UserId, _other.UserId);
        await (await ReplyAsync(_staff.Client, meeting.Id, "Declined")).ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await ReplyAsync(_other.Client, meeting.Id, "Accepted")).ShouldHaveStatusAsync(HttpStatusCode.OK);

        // A new title, the same time: replies stay, and the decliner's calendar isn't checked.
        var renamed = await (await _admin.PutAsJsonAsync($"/api/calendar/meetings/{meeting.Id}", new MeetingRequest
        {
            Title = "Synthetic renamed meeting",
            StartUtc = UkAt(_day, 9),
            EndUtc = UkAt(_day, 10),
            AttendeeUserIds = [_staff.UserId, _other.UserId],
            ExpectedLastUpdated = meeting.LastUpdated,
        })).ReadAsAsync<MeetingResponse>();
        renamed.Attendees.Select(a => (a.UserId, a.Reply)).Should().BeEquivalentTo([(_staff.UserId, "Declined"), (_other.UserId, "Accepted")]);

        var moved = await (await _admin.PutAsJsonAsync($"/api/calendar/meetings/{meeting.Id}", new MeetingRequest
        {
            Title = renamed.Title,
            StartUtc = UkAt(_day, 11),
            EndUtc = UkAt(_day, 12),
            AttendeeUserIds = [_staff.UserId, _other.UserId],
            ExpectedLastUpdated = renamed.LastUpdated,
        })).ReadAsAsync<MeetingResponse>();

        moved.Attendees.Should().OnlyContain(a => a.Reply == "NoReply" && a.RepliedAtUtc == null);
        var changed = (await NotificationsAsync(_other.Client)).Where(n => n.Kind == "MeetingChanged").Should().ContainSingle().Subject;
        changed.Message.Should().EndWith(MeetingNotifications.ReplyAgain);
        (await NotificationsAsync(_staff.Client)).Should().Contain(n => n.Kind == "MeetingChanged");
    }

    [Test]
    public async Task UpdateMeeting_NotMoved_ADeclinerWhoBookedTheTimeSince_DoesntBlockIt()
    {
        var meeting = await CreateMeetingAsync(UkAt(_day, 9), UkAt(_day, 10), _staff.UserId, _other.UserId);
        await (await ReplyAsync(_staff.Client, meeting.Id, "Declined")).ShouldHaveStatusAsync(HttpStatusCode.OK);
        var patient = await CreatePatientAsync(_staff.Client);
        await (await PostBookingAsync(_staff.Client, patient.Id, _staff.UserId, UkAt(_day, 9))).ShouldHaveStatusAsync(HttpStatusCode.Created);

        var renamed = await _admin.PutAsJsonAsync($"/api/calendar/meetings/{meeting.Id}", new MeetingRequest
        {
            Title = "Synthetic renamed meeting",
            StartUtc = UkAt(_day, 9),
            EndUtc = UkAt(_day, 10),
            AttendeeUserIds = [_staff.UserId, _other.UserId],
            ExpectedLastUpdated = meeting.LastUpdated,
        });

        await renamed.ShouldHaveStatusAsync(HttpStatusCode.OK);
    }
}
