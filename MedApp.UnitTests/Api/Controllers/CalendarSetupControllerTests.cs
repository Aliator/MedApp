using AutoFixture;
using FluentAssertions;
using MedApp.API.Controllers;
using MedApp.Application.Calendar.ActivityTypes.Commands.CreateActivityType;
using MedApp.Application.Calendar.ActivityTypes.Commands.DeleteActivityType;
using MedApp.Application.Calendar.ActivityTypes.Commands.SetActivityTypeArchived;
using MedApp.Application.Calendar.ActivityTypes.Commands.UpdateActivityType;
using MedApp.Application.Calendar.ActivityTypes.Queries.GetActivityTypeById;
using MedApp.Application.Calendar.ActivityTypes.Queries.GetActivityTypes;
using MedApp.Application.Calendar.ClosedDays.Commands.AddBankHolidays;
using MedApp.Application.Calendar.ClosedDays.Commands.CreateClosedDay;
using MedApp.Application.Calendar.ClosedDays.Commands.DeleteClosedDay;
using MedApp.Application.Calendar.ClosedDays.Queries.GetClosedDayBookings;
using MedApp.Application.Calendar.ClosedDays.Queries.GetClosedDays;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.DayTemplates.Commands.CreateDayTemplate;
using MedApp.Application.Calendar.DayTemplates.Commands.DeleteDayTemplate;
using MedApp.Application.Calendar.DayTemplates.Commands.SetDayTemplateArchived;
using MedApp.Application.Calendar.DayTemplates.Commands.UpdateDayTemplate;
using MedApp.Application.Calendar.DayTemplates.Queries.GetDayTemplateById;
using MedApp.Application.Calendar.DayTemplates.Queries.GetDayTemplates;
using MedApp.Application.Calendar.WeekTemplates.Commands.CreateWeekTemplate;
using MedApp.Application.Calendar.WeekTemplates.Commands.DeleteWeekTemplate;
using MedApp.Application.Calendar.WeekTemplates.Commands.SetWeekTemplateArchived;
using MedApp.Application.Calendar.WeekTemplates.Commands.UpdateWeekTemplate;
using MedApp.Application.Calendar.WeekTemplates.Queries.GetWeekTemplateById;
using MedApp.Application.Calendar.WeekTemplates.Queries.GetWeekTemplates;
using MedApp.Contracts.Calendar.Requests;
using MedApp.Contracts.Calendar.Responses;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Mvc;
using static MedApp.UnitTests.Common.Constants.ApiTestConstants;

namespace MedApp.UnitTests.Api.Controllers;

[TestFixture]
public sealed class CalendarSetupControllerTests
{
    /// <summary>Activity types are organisation-wide, so only admins change them.</summary>
    private static readonly string[] AdminOnly =
    [
        nameof(CalendarSetupController.CreateActivityType),
        nameof(CalendarSetupController.UpdateActivityType),
        nameof(CalendarSetupController.ArchiveActivityType),
        nameof(CalendarSetupController.RestoreActivityType),
        nameof(CalendarSetupController.DeleteActivityType),
        nameof(CalendarSetupController.GetClosedDayBookings),
        nameof(CalendarSetupController.CreateClosedDay),
        nameof(CalendarSetupController.AddBankHolidays),
        nameof(CalendarSetupController.DeleteClosedDay),
    ];

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

    private static CalendarSetupController Controller(RecordingMediator mediator, string role = "Admin") =>
        new CalendarSetupController(mediator).WithRequest(SignedIn(role));

    /// <summary>The signed-in user as the controller passes them on, an admin or not.</summary>
    private static CalendarActor Actor(string role) => new(UserId, Username, IsAdmin: role == "Admin");

    private async Task ExpectOk(
        object response, object request, Func<CalendarSetupController, Task<IActionResult>> act, string role = "Admin")
    {
        var mediator = new RecordingMediator(_ => response);

        var result = await act(Controller(mediator, role));

        mediator.Request.Should().Be(request);
        mediator.Tokens.Single().Should().Be(_cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(response);
    }

    private async Task ExpectNoContent(object request, Func<CalendarSetupController, Task<IActionResult>> act, string role = "Admin")
    {
        var mediator = new RecordingMediator();

        var result = await act(Controller(mediator, role));

        mediator.Request.Should().Be(request);
        mediator.Tokens.Single().Should().Be(_cts.Token);
        result.Should().BeOfType<NoContentResult>();
    }

    // Activity types

    [TestCase(true)]
    [TestCase(false)]
    public async Task GetActivityTypes_AsksForArchivedOnesOrNot(bool includeArchived)
    {
        IReadOnlyList<ActivityTypeResponse> types = [];
        await ExpectOk(types, new GetActivityTypesQuery(includeArchived), c => c.GetActivityTypes(includeArchived, _cts.Token));
    }

    [Test]
    public async Task GetActivityType_ReturnsIt() =>
        await ExpectOk(_fixture.Create<ActivityTypeResponse>(), new GetActivityTypeByIdQuery(RecordId), c => c.GetActivityType(RecordId, _cts.Token));

    [Test]
    public async Task CreateActivityType_ReturnsItWithItsAddress()
    {
        var request = new CreateActivityTypeRequest { Name = "Synthetic activity", Colour = "teal" };
        var activityType = _fixture.Create<ActivityTypeResponse>();
        var mediator = new RecordingMediator(_ => activityType);

        var result = await Controller(mediator).CreateActivityType(request, _cts.Token);

        mediator.Request.Should().Be(new CreateActivityTypeCommand("Synthetic activity", "teal"));
        mediator.Tokens.Single().Should().Be(_cts.Token);
        var created = result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be($"/api/calendar/activity-types/{activityType.Id}");
        created.Value.Should().Be(activityType);
    }

    [Test]
    public async Task UpdateActivityType_SendsTheChanges() =>
        await ExpectOk(_fixture.Create<ActivityTypeResponse>(),
            new UpdateActivityTypeCommand(RecordId, null, "pink"),
            c => c.UpdateActivityType(RecordId, new UpdateActivityTypeRequest { Colour = "pink" }, _cts.Token));

    [Test]
    public async Task ArchiveAndRestoreActivityType_SetItsArchivedFlag()
    {
        var activityType = _fixture.Create<ActivityTypeResponse>();
        await ExpectOk(activityType, new SetActivityTypeArchivedCommand(RecordId, true), c => c.ArchiveActivityType(RecordId, _cts.Token));
        await ExpectOk(activityType, new SetActivityTypeArchivedCommand(RecordId, false), c => c.RestoreActivityType(RecordId, _cts.Token));
    }

    [Test]
    public async Task DeleteActivityType_ReturnsNoContent() =>
        await ExpectNoContent(new DeleteActivityTypeCommand(RecordId), c => c.DeleteActivityType(RecordId, _cts.Token));

    // Day templates

    [TestCase(true, "Admin")]
    [TestCase(false, "User")]
    public async Task GetDayTemplates_AsksForTheirsArchivedOrNotAsTheSignedInUser(bool includeArchived, string role)
    {
        IReadOnlyList<DayTemplateResponse> templates = [];
        await ExpectOk(templates,
            new GetDayTemplatesQuery(OtherId, Actor(role), includeArchived),
            c => c.GetDayTemplates(OtherId, includeArchived, _cts.Token),
            role);
    }

    [TestCase("Admin")]
    [TestCase("User")]
    public async Task GetDayTemplate_ReturnsItForTheSignedInUser(string role) =>
        await ExpectOk(_fixture.Create<DayTemplateResponse>(),
            new GetDayTemplateByIdQuery(RecordId, Actor(role)),
            c => c.GetDayTemplate(RecordId, _cts.Token),
            role);

    [TestCase("Admin")]
    [TestCase("User")]
    public async Task CreateDayTemplate_SendsTheOwnerItsBlocksAndTheSignedInUserAndReturnsItWithItsAddress(string role)
    {
        var request = DayTemplate();
        var template = _fixture.Create<DayTemplateResponse>();
        var mediator = new RecordingMediator(_ => template);

        var result = await Controller(mediator, role).CreateDayTemplate(OtherId, request, _cts.Token);

        var command = mediator.Request.Should().BeOfType<CreateDayTemplateCommand>().Subject;
        command.OwnerUserId.Should().Be(OtherId);
        command.Name.Should().Be(request.Name);
        command.Blocks.Should().Equal(ExpectedBlocks);
        command.Actor.Should().Be(Actor(role));
        mediator.Tokens.Single().Should().Be(_cts.Token);
        var created = result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be($"/api/calendar/day-templates/{template.Id}");
        created.Value.Should().Be(template);
    }

    [Test]
    public async Task CreateDayTemplate_NullBlocks_SendsNone()
    {
        var request = new DayTemplateRequest { Name = "Synthetic day", Blocks = null! };
        var mediator = new RecordingMediator(_ => _fixture.Create<DayTemplateResponse>());

        await Controller(mediator).CreateDayTemplate(OtherId, request, _cts.Token);

        mediator.Request.Should().BeOfType<CreateDayTemplateCommand>().Which.Blocks.Should().BeEmpty();
    }

    [TestCase("Admin")]
    [TestCase("User")]
    public async Task UpdateDayTemplate_SendsTheNameBlocksAndSignedInUser(string role)
    {
        var request = DayTemplate();
        var template = _fixture.Create<DayTemplateResponse>();
        var mediator = new RecordingMediator(_ => template);

        var result = await Controller(mediator, role).UpdateDayTemplate(RecordId, request, _cts.Token);

        var command = mediator.Request.Should().BeOfType<UpdateDayTemplateCommand>().Subject;
        command.DayTemplateId.Should().Be(RecordId);
        command.Name.Should().Be(request.Name);
        command.Blocks.Should().Equal(ExpectedBlocks);
        command.Actor.Should().Be(Actor(role));
        mediator.Tokens.Single().Should().Be(_cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(template);
    }

    [Test]
    public async Task UpdateDayTemplate_NullBlocks_SendsNone()
    {
        var request = new DayTemplateRequest { Name = "Synthetic day", Blocks = null! };
        var mediator = new RecordingMediator(_ => _fixture.Create<DayTemplateResponse>());

        await Controller(mediator).UpdateDayTemplate(RecordId, request, _cts.Token);

        mediator.Request.Should().BeOfType<UpdateDayTemplateCommand>().Which.Blocks.Should().BeEmpty();
    }

    [TestCase("Admin")]
    [TestCase("User")]
    public async Task ArchiveAndRestoreDayTemplate_SetItsArchivedFlagAsTheSignedInUser(string role)
    {
        var template = _fixture.Create<DayTemplateResponse>();
        await ExpectOk(template, new SetDayTemplateArchivedCommand(RecordId, true, Actor(role)), c => c.ArchiveDayTemplate(RecordId, _cts.Token), role);
        await ExpectOk(template, new SetDayTemplateArchivedCommand(RecordId, false, Actor(role)), c => c.RestoreDayTemplate(RecordId, _cts.Token), role);
    }

    [TestCase("Admin")]
    [TestCase("User")]
    public async Task DeleteDayTemplate_ReturnsNoContent(string role) =>
        await ExpectNoContent(new DeleteDayTemplateCommand(RecordId, Actor(role)), c => c.DeleteDayTemplate(RecordId, _cts.Token), role);

    // Week templates

    [TestCase(true, "Admin")]
    [TestCase(false, "User")]
    public async Task GetWeekTemplates_AsksForTheirsArchivedOrNotAsTheSignedInUser(bool includeArchived, string role)
    {
        IReadOnlyList<WeekTemplateResponse> templates = [];
        await ExpectOk(templates,
            new GetWeekTemplatesQuery(OtherId, Actor(role), includeArchived),
            c => c.GetWeekTemplates(OtherId, includeArchived, _cts.Token),
            role);
    }

    [TestCase("Admin")]
    [TestCase("User")]
    public async Task GetWeekTemplate_ReturnsItForTheSignedInUser(string role) =>
        await ExpectOk(_fixture.Create<WeekTemplateResponse>(),
            new GetWeekTemplateByIdQuery(RecordId, Actor(role)),
            c => c.GetWeekTemplate(RecordId, _cts.Token),
            role);

    [TestCase("Admin")]
    [TestCase("User")]
    public async Task CreateWeekTemplate_SendsTheOwnerEachDayAndTheSignedInUserAndReturnsItWithItsAddress(string role)
    {
        var request = WeekTemplate();
        var template = _fixture.Create<WeekTemplateResponse>();
        var mediator = new RecordingMediator(_ => template);

        var result = await Controller(mediator, role).CreateWeekTemplate(OtherId, request, _cts.Token);

        mediator.Request.Should().Be(new CreateWeekTemplateCommand(OtherId, request.Name, RecordId, null, OtherId, null, RecordId, Actor(role)));
        mediator.Tokens.Single().Should().Be(_cts.Token);
        var created = result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be($"/api/calendar/week-templates/{template.Id}");
        created.Value.Should().Be(template);
    }

    [TestCase("Admin")]
    [TestCase("User")]
    public async Task UpdateWeekTemplate_SendsTheNameEachDayAndTheSignedInUser(string role)
    {
        var request = WeekTemplate();
        await ExpectOk(_fixture.Create<WeekTemplateResponse>(),
            new UpdateWeekTemplateCommand(OtherId, request.Name, RecordId, null, OtherId, null, RecordId, Actor(role)),
            c => c.UpdateWeekTemplate(OtherId, request, _cts.Token),
            role);
    }

    [TestCase("Admin")]
    [TestCase("User")]
    public async Task ArchiveAndRestoreWeekTemplate_SetItsArchivedFlagAsTheSignedInUser(string role)
    {
        var template = _fixture.Create<WeekTemplateResponse>();
        await ExpectOk(template, new SetWeekTemplateArchivedCommand(RecordId, true, Actor(role)), c => c.ArchiveWeekTemplate(RecordId, _cts.Token), role);
        await ExpectOk(template, new SetWeekTemplateArchivedCommand(RecordId, false, Actor(role)), c => c.RestoreWeekTemplate(RecordId, _cts.Token), role);
    }

    [TestCase("Admin")]
    [TestCase("User")]
    public async Task DeleteWeekTemplate_ReturnsNoContent(string role) =>
        await ExpectNoContent(new DeleteWeekTemplateCommand(RecordId, Actor(role)), c => c.DeleteWeekTemplate(RecordId, _cts.Token), role);

    // Closed days

    [TestCase("Admin")]
    [TestCase("User")]
    public async Task GetClosedDays_SendsTheRange(string role)
    {
        var from = new DateOnly(2027, 1, 1);
        var to = new DateOnly(2027, 12, 31);
        await ExpectOk(_fixture.CreateMany<ClosedDayResponse>().ToList(),
            new GetClosedDaysQuery(from, to),
            c => c.GetClosedDays(new GetClosedDaysRequest { From = from, To = to }, _cts.Token),
            role);
    }

    [Test]
    public async Task CreateClosedDay_SendsTheDateNameAndAdminAndReturnsItWithItsAddress()
    {
        var date = new DateOnly(2027, 6, 4);
        var request = new CreateClosedDayRequest { Date = date, Name = "Staff training day" };
        var day = _fixture.Create<ClosedDayResponse>();
        day.Date = date;
        var mediator = new RecordingMediator(_ => day);

        var result = await Controller(mediator).CreateClosedDay(request, _cts.Token);

        mediator.Request.Should().Be(new CreateClosedDayCommand(date, "Staff training day", Actor("Admin")));
        mediator.Tokens.Single().Should().Be(_cts.Token);
        var created = result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be("/api/calendar/closed-days?from=2027-06-04&to=2027-06-04");
        created.Value.Should().Be(day);
    }

    [Test]
    public async Task GetClosedDayBookings_SendsTheDate() =>
        await ExpectOk(_fixture.Create<DayBookingsResponse>(),
            new GetClosedDayBookingsQuery(new DateOnly(2027, 6, 4)),
            c => c.GetClosedDayBookings(new DateOnly(2027, 6, 4), _cts.Token));

    [Test]
    public async Task AddBankHolidays_SendsTheYearAndAdmin() =>
        await ExpectOk(_fixture.Create<AddBankHolidaysResponse>(),
            new AddBankHolidaysCommand(2027, Actor("Admin")),
            c => c.AddBankHolidays(new AddBankHolidaysRequest { Year = 2027 }, _cts.Token));

    [Test]
    public async Task DeleteClosedDay_ReturnsNoContent() =>
        await ExpectNoContent(new DeleteClosedDayCommand(RecordId), c => c.DeleteClosedDay(RecordId, _cts.Token));

    // Who may call what

    [Test]
    public async Task ActivityTypeWrites_AdminsOnly()
    {
        foreach (var action in AdminOnly)
        {
            (await ControllerAuthorisation.MayCallAsync<CalendarSetupController>(action, SignedIn("User"))).Should().BeFalse(action);
            (await ControllerAuthorisation.MayCallAsync<CalendarSetupController>(action, SignedIn("Admin"))).Should().BeTrue(action);
            (await ControllerAuthorisation.MayCallAsync<CalendarSetupController>(action, SignedIn())).Should().BeFalse(action);
        }
    }

    [Test]
    public async Task EverythingElse_AnyStaff()
    {
        // Reading anything (closed days too), and changing day and week templates: the handlers check whose templates they are.
        var anyStaff = ControllerAuthorisation.Actions<CalendarSetupController>().Except(AdminOnly).ToList();
        anyStaff.Should().HaveCount(17);

        foreach (var action in anyStaff)
        {
            (await ControllerAuthorisation.MayCallAsync<CalendarSetupController>(action, SignedIn("User"))).Should().BeTrue(action);
            (await ControllerAuthorisation.MayCallAsync<CalendarSetupController>(action, SignedIn("Admin"))).Should().BeTrue(action);
            (await ControllerAuthorisation.MayCallAsync<CalendarSetupController>(action, SignedIn())).Should().BeFalse(action);
        }
    }

    private static DayTemplateRequest DayTemplate() => new()
    {
        Name = "Synthetic clinic day",
        Blocks =
        [
            new DayTemplateBlockRequest { ActivityTypeId = RecordId, StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(13, 0), Note = "Synthetic note" },
            new DayTemplateBlockRequest { ActivityTypeId = OtherId, StartTime = new TimeOnly(16, 30), EndTime = new TimeOnly(17, 0) },
        ],
    };

    private static readonly List<DayTemplateBlockValue> ExpectedBlocks =
    [
        new(RecordId, new TimeOnly(12, 0), new TimeOnly(13, 0), "Synthetic note"),
        new(OtherId, new TimeOnly(16, 30), new TimeOnly(17, 0), null),
    ];

    private static WeekTemplateRequest WeekTemplate() => new()
    {
        Name = "Synthetic week",
        Monday = RecordId,
        Wednesday = OtherId,
        Friday = RecordId,
    };
}
