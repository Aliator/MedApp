using AutoFixture;
using FluentAssertions;
using MedApp.API.Controllers;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Clinics.Commands.CancelClinicSession;
using MedApp.Application.Clinics.Commands.CreateClinicPattern;
using MedApp.Application.Clinics.Commands.CreateClinicSession;
using MedApp.Application.Clinics.Commands.CreateSessionsFromPattern;
using MedApp.Application.Clinics.Commands.UpdateClinicPattern;
using MedApp.Application.Clinics.Commands.UpdateClinicSession;
using MedApp.Application.Clinics.Queries.GetClinicPatterns;
using MedApp.Application.Clinics.Queries.GetClinicSessionById;
using MedApp.Application.Clinics.Queries.SearchClinicSessions;
using MedApp.Contracts.Clinics.Requests;
using MedApp.Contracts.Clinics.Responses;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Mvc;
using static MedApp.UnitTests.Common.Constants.ApiTestConstants;

namespace MedApp.UnitTests.Api.Controllers;

[TestFixture]
public sealed class ClinicsControllerTests
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

    private static ClinicsController Controller(RecordingMediator mediator) =>
        new ClinicsController(mediator).WithRequest(SignedIn("Admin"));

    private void ExpectSent(RecordingMediator mediator, object request)
    {
        mediator.Request.Should().Be(request);
        mediator.Tokens.Single().Should().Be(_cts.Token);
    }

    [Test]
    public async Task SearchSessions_SendsEveryFilterAsTheUser()
    {
        var request = _fixture.Create<SearchClinicSessionsRequest>();
        IReadOnlyList<ClinicSessionResponse> sessions = [_fixture.Create<ClinicSessionResponse>()];
        var mediator = new RecordingMediator(_ => sessions);

        var result = await Controller(mediator).SearchSessions(request, _cts.Token);

        ExpectSent(mediator, new SearchClinicSessionsQuery(
            request.From, request.To, request.StaffUserId, request.SiteId, request.RoomId, request.SpecialtyId,
            request.IncludeCancelled, request.WithFreeSlots, AdminActor));
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(sessions);
    }

    [Test]
    public async Task GetSession_SendsItsId()
    {
        var detail = _fixture.Create<ClinicSessionDetailResponse>();
        var mediator = new RecordingMediator(_ => detail);

        var result = await Controller(mediator).GetSession(RecordId, _cts.Token);

        ExpectSent(mediator, new GetClinicSessionByIdQuery(RecordId, AdminActor));
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(detail);
    }

    [Test]
    public async Task CreateSession_SendsItAndReturnsItWithItsAddress()
    {
        var request = _fixture.Create<ClinicSessionRequest>();
        var session = _fixture.Create<ClinicSessionResponse>();
        var mediator = new RecordingMediator(_ => session);

        var result = await Controller(mediator).CreateSession(request, _cts.Token);

        ExpectSent(mediator, new CreateClinicSessionCommand(
            request.StaffUserId, request.RoomId, request.SpecialtyId, request.StartUtc, request.EndUtc, request.SlotMinutes, AdminActor));
        var created = result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be($"/api/clinics/sessions/{session.Id}");
        created.Value.Should().Be(session);
    }

    [Test]
    public async Task UpdateSession_SendsEveryValue()
    {
        var request = _fixture.Create<UpdateClinicSessionRequest>();
        var session = _fixture.Create<ClinicSessionResponse>();
        var mediator = new RecordingMediator(_ => session);

        var result = await Controller(mediator).UpdateSession(RecordId, request, _cts.Token);

        ExpectSent(mediator, new UpdateClinicSessionCommand(
            RecordId, request.RoomId, request.SpecialtyId, request.StartUtc, request.EndUtc, request.SlotMinutes, request.ExpectedLastUpdated, AdminActor));
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(session);
    }

    [Test]
    public async Task CancelSession_SendsTheReason_AndIsNeverCached()
    {
        var request = _fixture.Create<CancelClinicSessionRequest>();
        var cancelled = _fixture.Create<CancelClinicSessionResponse>();
        var mediator = new RecordingMediator(_ => cancelled);
        var controller = Controller(mediator);

        var result = await controller.CancelSession(RecordId, request, _cts.Token);

        ExpectSent(mediator, new CancelClinicSessionCommand(RecordId, request.Reason, request.ExpectedLastUpdated, AdminActor));
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(cancelled);
        controller.Response.Headers.CacheControl.ToString().Should().Be("no-store");
    }

    [Test]
    public async Task GetPatterns_ReturnsThem()
    {
        IReadOnlyList<ClinicPatternResponse> patterns = [_fixture.Create<ClinicPatternResponse>()];
        var mediator = new RecordingMediator(_ => patterns);

        var result = await Controller(mediator).GetPatterns(_cts.Token);

        ExpectSent(mediator, new GetClinicPatternsQuery());
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(patterns);
    }

    [Test]
    public async Task CreatePattern_SendsItAndReturnsItWithItsAddress()
    {
        var request = _fixture.Create<ClinicPatternRequest>();
        var pattern = _fixture.Create<ClinicPatternResponse>();
        var mediator = new RecordingMediator(_ => pattern);

        var result = await Controller(mediator).CreatePattern(request, _cts.Token);

        ExpectSent(mediator, new CreateClinicPatternCommand(
            request.StaffUserId, request.RoomId, request.SpecialtyId, request.DayOfWeek, request.Starts, request.Ends,
            request.SlotMinutes, request.EveryWeeks, request.FromDate, request.UntilDate, AdminActor));
        var created = result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be($"/api/clinics/patterns/{pattern.Id}");
    }

    [Test]
    public async Task UpdatePattern_SendsEveryValue()
    {
        var request = _fixture.Create<ClinicPatternRequest>();
        var pattern = _fixture.Create<ClinicPatternResponse>();
        var mediator = new RecordingMediator(_ => pattern);

        var result = await Controller(mediator).UpdatePattern(RecordId, request, _cts.Token);

        ExpectSent(mediator, new UpdateClinicPatternCommand(
            RecordId, request.StaffUserId, request.RoomId, request.SpecialtyId, request.DayOfWeek, request.Starts, request.Ends,
            request.SlotMinutes, request.EveryWeeks, request.FromDate, request.UntilDate, request.ExpectedLastUpdated, AdminActor));
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(pattern);
    }

    [Test]
    public async Task CreateSessions_SendsTheWeeks()
    {
        var made = _fixture.Create<CreateSessionsResponse>();
        var mediator = new RecordingMediator(_ => made);

        var result = await Controller(mediator).CreateSessions(RecordId, new CreateSessionsRequest { Weeks = 4 }, _cts.Token);

        ExpectSent(mediator, new CreateSessionsFromPatternCommand(RecordId, 4, AdminActor));
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(made);
    }
}
