using AutoFixture;
using FluentAssertions;
using MedApp.API.Controllers;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.StaffCalendars.Commands.ApplyDayTemplate;
using MedApp.Application.Calendar.StaffCalendars.Commands.ChangeTemplateBlockForDay;
using MedApp.Application.Calendar.StaffCalendars.Commands.ClearDayTemplate;
using MedApp.Application.Calendar.StaffCalendars.Commands.CreateCalendarBlock;
using MedApp.Application.Calendar.StaffCalendars.Commands.DeleteCalendarBlock;
using MedApp.Application.Calendar.StaffCalendars.Commands.RemoveTemplateBlockForDay;
using MedApp.Application.Calendar.StaffCalendars.Commands.SetStaffWeekTemplate;
using MedApp.Application.Calendar.StaffCalendars.Commands.UpdateCalendarBlock;
using MedApp.Application.Calendar.StaffCalendars.Queries.GetAvailability;
using MedApp.Application.Calendar.StaffCalendars.Queries.GetCalendar;
using MedApp.Application.Calendar.StaffCalendars.Queries.GetCalendarStaff;
using MedApp.Application.Calendar.StaffCalendars.Queries.GetStaffWeekTemplate;
using MedApp.Application.Calendar.StaffCalendars.Queries.GetTeamCalendar;
using MedApp.Contracts.Calendar.Requests;
using MedApp.Contracts.Calendar.Responses;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Mvc;
using static MedApp.UnitTests.Common.Constants.ApiTestConstants;

namespace MedApp.UnitTests.Api.Controllers;

[TestFixture]
public sealed class CalendarControllerTests
{
    private static readonly CalendarActor StaffActor = new(UserId, Username, false);
    private static readonly CalendarActor AdminActor = new(UserId, Username, true);
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

    private static CalendarController Controller(RecordingMediator mediator, string role = "User") =>
        new CalendarController(mediator).WithRequest(SignedIn(role));

    /// <summary>Sent the request given, with the call's token, and returned the response.</summary>
    private async Task ExpectOk(object response, object request, Func<CalendarController, Task<IActionResult>> act, string role = "User")
    {
        var mediator = new RecordingMediator(_ => response);

        var result = await act(Controller(mediator, role));

        mediator.Request.Should().Be(request);
        mediator.Tokens.Single().Should().Be(_cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(response);
    }

    /// <summary>Sent the request given, with the call's token, and returned no content.</summary>
    private async Task ExpectNoContent(object request, Func<CalendarController, Task<IActionResult>> act, string role = "User")
    {
        var mediator = new RecordingMediator();

        var result = await act(Controller(mediator, role));

        mediator.Request.Should().Be(request);
        mediator.Tokens.Single().Should().Be(_cts.Token);
        result.Should().BeOfType<NoContentResult>();
    }

    [TestCase("User", false)]
    [TestCase("Admin", true)]
    public async Task GetCalendar_SendsTheRangeAndWhoIsAsking(string role, bool isAdmin)
    {
        var request = _fixture.Create<GetCalendarRequest>();

        await ExpectOk(_fixture.Create<CalendarResponse>(),
            new GetCalendarQuery(request.StaffUserId, request.From, request.To, new CalendarActor(UserId, Username, isAdmin), request.WithoutAppointments),
            c => c.GetCalendar(request, _cts.Token), role);
    }

    [Test]
    public async Task GetCalendar_NoStaffMember_AsksForTheirOwn()
    {
        var request = new GetCalendarRequest { From = new DateOnly(2026, 11, 16), To = new DateOnly(2026, 11, 20) };

        await ExpectOk(_fixture.Create<CalendarResponse>(),
            new GetCalendarQuery(null, request.From, request.To, StaffActor),
            c => c.GetCalendar(request, _cts.Token));
    }

    [Test]
    public async Task GetStaff_ReturnsThem()
    {
        IReadOnlyList<StaffMemberResponse> staff = _fixture.CreateMany<StaffMemberResponse>().ToList();
        await ExpectOk(staff, new GetCalendarStaffQuery(), c => c.GetStaff(_cts.Token));
    }

    [TestCase("User", false)]
    [TestCase("Admin", true)]
    public async Task CreateBlock_SendsItAsTheUserAndReturnsItWithItsAddress(string role, bool isAdmin)
    {
        var request = _fixture.Create<CreateCalendarBlockRequest>();
        var block = _fixture.Create<CalendarBlockResponse>();
        var mediator = new RecordingMediator(_ => block);

        var result = await Controller(mediator, role).CreateBlock(request, _cts.Token);

        mediator.Request.Should().Be(new CreateCalendarBlockCommand(
            request.StaffUserId, request.ActivityTypeId, request.StartUtc, request.EndUtc, request.Note, new CalendarActor(UserId, Username, isAdmin)));
        mediator.Tokens.Single().Should().Be(_cts.Token);
        var created = result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be($"/api/calendar/blocks/{block.BlockId}");
        created.Value.Should().Be(block);
    }

    [Test]
    public async Task UpdateBlock_SendsTheChangesAsTheUser()
    {
        var request = _fixture.Create<UpdateCalendarBlockRequest>();

        await ExpectOk(_fixture.Create<CalendarBlockResponse>(),
            new UpdateCalendarBlockCommand(RecordId, request.ActivityTypeId, request.StartUtc, request.EndUtc, request.Note, StaffActor),
            c => c.UpdateBlock(RecordId, request, _cts.Token));
    }

    [Test]
    public async Task DeleteBlock_DeletesAsTheUserAndReturnsNoContent() =>
        await ExpectNoContent(new DeleteCalendarBlockCommand(RecordId, AdminActor), c => c.DeleteBlock(RecordId, _cts.Token), "Admin");

    [Test]
    public async Task ChangeTemplateBlockForDay_SendsTheDayTheBlockAndTheNewValues()
    {
        var request = _fixture.Create<UpdateCalendarBlockRequest>();
        var date = new DateOnly(2026, 11, 23);

        await ExpectOk(_fixture.Create<CalendarBlockResponse>(),
            new ChangeTemplateBlockForDayCommand(OtherId, date, RecordId, request.ActivityTypeId, request.StartUtc, request.EndUtc, request.Note, StaffActor),
            c => c.ChangeTemplateBlockForDay(OtherId, date, RecordId, request, _cts.Token));
    }

    [Test]
    public async Task RemoveTemplateBlockForDay_SendsTheDayAndTheBlockAndReturnsNoContent()
    {
        var date = new DateOnly(2026, 11, 23);
        await ExpectNoContent(new RemoveTemplateBlockForDayCommand(OtherId, date, RecordId, StaffActor),
            c => c.RemoveTemplateBlockForDay(OtherId, date, RecordId, _cts.Token));
    }

    [Test]
    public async Task ApplyDayTemplate_SendsTheDayAndTheTemplate()
    {
        var date = new DateOnly(2026, 11, 23);
        await ExpectOk(_fixture.Create<CalendarDayResponse>(),
            new ApplyDayTemplateCommand(OtherId, date, RecordId, StaffActor),
            c => c.ApplyDayTemplate(OtherId, date, new ApplyDayTemplateRequest { DayTemplateId = RecordId }, _cts.Token));
    }

    [Test]
    public async Task ApplyDayTemplate_NoTemplate_SendsNone()
    {
        var date = new DateOnly(2026, 11, 23);
        await ExpectOk(_fixture.Create<CalendarDayResponse>(),
            new ApplyDayTemplateCommand(OtherId, date, null, AdminActor),
            c => c.ApplyDayTemplate(OtherId, date, new ApplyDayTemplateRequest(), _cts.Token), "Admin");
    }

    [Test]
    public async Task ClearDayTemplate_SendsTheDayAndReturnsNoContent()
    {
        var date = new DateOnly(2026, 11, 23);
        await ExpectNoContent(new ClearDayTemplateCommand(OtherId, date, StaffActor), c => c.ClearDayTemplate(OtherId, date, _cts.Token));
    }

    [Test]
    public async Task GetWeekTemplate_ReturnsTheirDefaultWeek() =>
        await ExpectOk(_fixture.Create<StaffWeekTemplateResponse>(), new GetStaffWeekTemplateQuery(OtherId), c => c.GetWeekTemplate(OtherId, _cts.Token));

    [TestCase(true)]
    [TestCase(false)]
    public async Task SetWeekTemplate_SendsTheTemplateOrNone(bool hasTemplate)
    {
        Guid? template = hasTemplate ? RecordId : null;

        await ExpectOk(_fixture.Create<StaffWeekTemplateResponse>(),
            new SetStaffWeekTemplateCommand(OtherId, template, StaffActor),
            c => c.SetWeekTemplate(OtherId, new SetStaffWeekTemplateRequest { WeekTemplateId = template }, _cts.Token));
    }

    [Test]
    public async Task GetTeamCalendar_SendsTheDatePeopleAndWhoIsAsking()
    {
        var request = _fixture.Create<GetTeamCalendarRequest>();

        await ExpectOk(_fixture.Create<TeamCalendarResponse>(),
            new GetTeamCalendarQuery(request.Date, request.StaffUserIds!, AdminActor),
            c => c.GetTeamCalendar(request, _cts.Token), "Admin");
    }

    [Test]
    public async Task GetTeamCalendar_NobodyNamed_AsksForEveryone()
    {
        var mediator = new RecordingMediator(_ => _fixture.Create<TeamCalendarResponse>());
        var date = new DateOnly(2026, 11, 23);

        await Controller(mediator, "Admin").GetTeamCalendar(new GetTeamCalendarRequest { Date = date, StaffUserIds = null }, _cts.Token);

        var query = mediator.Request.Should().BeOfType<GetTeamCalendarQuery>().Subject;
        query.Date.Should().Be(date);
        query.StaffUserIds.Should().BeEmpty();
        query.Actor.Should().Be(AdminActor);
        mediator.Tokens.Single().Should().Be(_cts.Token);
    }

    [Test]
    public async Task GetAvailability_SendsThePeopleRangeLengthAndMeetingBeingMoved()
    {
        var request = _fixture.Create<GetAvailabilityRequest>();

        await ExpectOk(_fixture.Create<AvailabilityResponse>(),
            new GetAvailabilityQuery(request.StaffUserIds, request.From, request.To, request.DurationMinutes, request.ExceptMeetingId),
            c => c.GetAvailability(request, _cts.Token), "Admin");
    }

    [Test]
    public async Task GetAvailability_NobodySent_SendsNobody()
    {
        var request = _fixture.Create<GetAvailabilityRequest>();
        request.StaffUserIds = null!;
        var mediator = new RecordingMediator(_ => _fixture.Create<AvailabilityResponse>());

        await Controller(mediator, "Admin").GetAvailability(request, _cts.Token);

        mediator.Request.Should().BeOfType<GetAvailabilityQuery>().Which.StaffUserIds.Should().BeEmpty();
    }

    private static readonly string[] AdminOnlyActions =
        [nameof(CalendarController.GetTeamCalendar), nameof(CalendarController.GetAvailability)];

    [TestCase("User", true)]
    [TestCase("Admin", true)]
    [TestCase(null, false)]
    public async Task EveryOtherAction_StaffOnly(string? role, bool allowed)
    {
        var user = role is null ? SignedIn() : SignedIn(role);

        foreach (var action in ControllerAuthorisation.Actions<CalendarController>().Except(AdminOnlyActions))
            (await ControllerAuthorisation.MayCallAsync<CalendarController>(action, user)).Should().Be(allowed, action);
    }

    [TestCase("User", false)]
    [TestCase("Admin", true)]
    [TestCase(null, false)]
    public async Task TeamViewAndSuggestedTimes_AdminsOnly(string? role, bool allowed)
    {
        var user = role is null ? SignedIn() : SignedIn(role);

        foreach (var action in AdminOnlyActions)
            (await ControllerAuthorisation.MayCallAsync<CalendarController>(action, user)).Should().Be(allowed, action);
    }
}
