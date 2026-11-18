using MedApp.UnitTests.Common.Constants;
using AutoFixture;
using FluentAssertions;
using MedApp.API.Controllers;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Meetings.Commands.CancelMeeting;
using MedApp.Application.Calendar.Meetings.Commands.CreateMeeting;
using MedApp.Application.Calendar.Meetings.Commands.ReplyToMeeting;
using MedApp.Application.Calendar.Meetings.Commands.UpdateMeeting;
using MedApp.Application.Calendar.Meetings.Queries.GetMeetingById;
using MedApp.Contracts.Calendar.Requests;
using MedApp.Contracts.Calendar.Responses;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Mvc;
using static MedApp.UnitTests.Common.Constants.ApiTestConstants;

namespace MedApp.UnitTests.Api.Controllers;

[TestFixture]
public sealed class MeetingsControllerTests
{
    private static readonly CalendarActor AdminActor = new(UserId, Username, TestPermissions.Admin);
    private IFixture _fixture = null!;
    private CancellationTokenSource _cts = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        _cts = new CancellationTokenSource();
    }

    [TearDown]
    public void TearDown() => _cts.Dispose();

    private static MeetingsController Controller(RecordingMediator mediator, string role = "Admin") =>
        new MeetingsController(mediator).WithRequest(SignedIn(role));

    [TestCase("User", false)]
    [TestCase("Admin", true)]
    public async Task GetMeeting_SendsItsIdAndWhoIsAsking(string role, bool isAdmin)
    {
        var meeting = _fixture.Create<MeetingResponse>();
        var mediator = new RecordingMediator(_ => meeting);

        var result = await Controller(mediator, role).GetMeeting(RecordId, _cts.Token);

        mediator.Request.Should().Be(new GetMeetingByIdQuery(RecordId, new CalendarActor(UserId, Username, TestPermissions.For(isAdmin))));
        mediator.Tokens.Single().Should().Be(_cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(meeting);
    }

    [Test]
    public async Task CreateMeeting_SendsItAsTheAdminAndReturnsItWithItsAddress()
    {
        var request = _fixture.Create<MeetingRequest>();
        var meeting = _fixture.Create<MeetingResponse>();
        var mediator = new RecordingMediator(_ => meeting);

        var result = await Controller(mediator).CreateMeeting(request, _cts.Token);

        mediator.Request.Should().Be(new CreateMeetingCommand(
            request.Title, request.Notes, request.StartUtc, request.EndUtc, request.AttendeeUserIds, AdminActor));
        mediator.Tokens.Single().Should().Be(_cts.Token);
        var created = result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be($"/api/calendar/meetings/{meeting.Id}");
        created.Value.Should().Be(meeting);
    }

    [Test]
    public async Task CreateMeeting_NoAttendeesSent_SendsNone()
    {
        var request = _fixture.Create<MeetingRequest>();
        request.AttendeeUserIds = null!;
        var mediator = new RecordingMediator(_ => _fixture.Create<MeetingResponse>());

        await Controller(mediator).CreateMeeting(request, _cts.Token);

        mediator.Request.Should().BeOfType<CreateMeetingCommand>().Which.AttendeeUserIds.Should().BeEmpty();
    }

    [Test]
    public async Task UpdateMeeting_SendsTheChangesAndTheVersionSeenAsTheAdmin()
    {
        var request = _fixture.Create<MeetingRequest>();
        var meeting = _fixture.Create<MeetingResponse>();
        var mediator = new RecordingMediator(_ => meeting);

        var result = await Controller(mediator).UpdateMeeting(RecordId, request, _cts.Token);

        mediator.Request.Should().Be(new UpdateMeetingCommand(
            RecordId, request.Title, request.Notes, request.StartUtc, request.EndUtc, request.AttendeeUserIds,
            request.ExpectedLastUpdated, AdminActor));
        mediator.Tokens.Single().Should().Be(_cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(meeting);
    }

    [Test]
    public async Task UpdateMeeting_NoAttendeesSent_SendsNone()
    {
        var request = _fixture.Create<MeetingRequest>();
        request.AttendeeUserIds = null!;
        var mediator = new RecordingMediator(_ => _fixture.Create<MeetingResponse>());

        await Controller(mediator).UpdateMeeting(RecordId, request, _cts.Token);

        mediator.Request.Should().BeOfType<UpdateMeetingCommand>().Which.AttendeeUserIds.Should().BeEmpty();
    }

    [Test]
    public async Task CancelMeeting_SendsItAsTheAdminAndReturnsNoContent()
    {
        var mediator = new RecordingMediator();

        var result = await Controller(mediator).CancelMeeting(RecordId, _cts.Token);

        mediator.Request.Should().Be(new CancelMeetingCommand(RecordId, AdminActor));
        mediator.Tokens.Single().Should().Be(_cts.Token);
        result.Should().BeOfType<NoContentResult>();
    }

    [Test]
    public async Task GetMeeting_AnyMemberOfStaff()
    {
        (await ControllerAuthorisation.MayCallAsync<MeetingsController>(nameof(MeetingsController.GetMeeting), SignedIn("User"))).Should().BeTrue();
        (await ControllerAuthorisation.MayCallAsync<MeetingsController>(nameof(MeetingsController.GetMeeting), SignedIn("Admin"))).Should().BeTrue();
        (await ControllerAuthorisation.MayCallAsync<MeetingsController>(nameof(MeetingsController.GetMeeting), SignedIn())).Should().BeFalse();
    }

    [TestCase(nameof(MeetingsController.CreateMeeting))]
    [TestCase(nameof(MeetingsController.UpdateMeeting))]
    [TestCase(nameof(MeetingsController.CancelMeeting))]
    public async Task Changes_AdminsOnly(string action)
    {
        (await ControllerAuthorisation.MayCallAsync<MeetingsController>(action, SignedIn("Admin"))).Should().BeTrue();
        (await ControllerAuthorisation.MayCallAsync<MeetingsController>(action, SignedIn("User"))).Should().BeFalse();
        (await ControllerAuthorisation.MayCallAsync<MeetingsController>(action, SignedIn())).Should().BeFalse();
    }

    [Test]
    public void EveryAction_IsCovered()
    {
        ControllerAuthorisation.Actions<MeetingsController>().Should().BeEquivalentTo(
            nameof(MeetingsController.GetMeeting),
            nameof(MeetingsController.CreateMeeting),
            nameof(MeetingsController.UpdateMeeting),
            nameof(MeetingsController.CancelMeeting),
            nameof(MeetingsController.ReplyToMeeting));
    }

    [TestCase("User", false)]
    [TestCase("Admin", true)]
    public async Task ReplyToMeeting_SendsTheReplyAsWhoeverIsAsking(string role, bool isAdmin)
    {
        var meeting = _fixture.Create<MeetingResponse>();
        var mediator = new RecordingMediator(_ => meeting);

        var start = new DateTime(2026, 11, 23, 10, 0, 0, DateTimeKind.Utc);
        var request = new MeetingReplyRequest { Reply = "Declined", ExpectedStartUtc = start, ExpectedEndUtc = start.AddHours(1) };

        var result = await Controller(mediator, role).ReplyToMeeting(RecordId, request, _cts.Token);

        mediator.Request.Should().Be(new ReplyToMeetingCommand(
            RecordId, "Declined", start, start.AddHours(1), new CalendarActor(UserId, Username, TestPermissions.For(isAdmin))));
        mediator.Tokens.Single().Should().Be(_cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(meeting);
    }

    [Test]
    public async Task ReplyToMeeting_AnyMemberOfStaff()
    {
        // Whether they're going is the handler's to check (a 403 otherwise).
        (await ControllerAuthorisation.MayCallAsync<MeetingsController>(nameof(MeetingsController.ReplyToMeeting), SignedIn("User"))).Should().BeTrue();
        (await ControllerAuthorisation.MayCallAsync<MeetingsController>(nameof(MeetingsController.ReplyToMeeting), SignedIn("Admin"))).Should().BeTrue();
        (await ControllerAuthorisation.MayCallAsync<MeetingsController>(nameof(MeetingsController.ReplyToMeeting), SignedIn())).Should().BeFalse();
    }
}
