using AutoFixture;
using FluentAssertions;
using MedApp.API.Controllers;
using MedApp.Application.Calendar.ActivityTypes.Commands.CreateActivityType;
using MedApp.Application.Calendar.ActivityTypes.Commands.DeleteActivityType;
using MedApp.Application.Calendar.ActivityTypes.Commands.SetActivityTypeArchived;
using MedApp.Application.Calendar.ActivityTypes.Commands.UpdateActivityType;
using MedApp.Application.Calendar.ActivityTypes.Queries.GetActivityTypeById;
using MedApp.Application.Calendar.ActivityTypes.Queries.GetActivityTypes;
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
    private static readonly string[] Reads =
    [
        nameof(CalendarSetupController.GetActivityTypes),
        nameof(CalendarSetupController.GetActivityType),
        nameof(CalendarSetupController.GetDayTemplates),
        nameof(CalendarSetupController.GetDayTemplate),
        nameof(CalendarSetupController.GetWeekTemplates),
        nameof(CalendarSetupController.GetWeekTemplate),
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

    private static CalendarSetupController Controller(RecordingMediator mediator) =>
        new CalendarSetupController(mediator).WithRequest(SignedIn("Admin"));

    private async Task ExpectOk(object response, object request, Func<CalendarSetupController, Task<IActionResult>> act)
    {
        var mediator = new RecordingMediator(_ => response);

        var result = await act(Controller(mediator));

        mediator.Request.Should().Be(request);
        mediator.Tokens.Single().Should().Be(_cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(response);
    }

    private async Task ExpectNoContent(object request, Func<CalendarSetupController, Task<IActionResult>> act)
    {
        var mediator = new RecordingMediator();

        var result = await act(Controller(mediator));

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

    [TestCase(true)]
    [TestCase(false)]
    public async Task GetDayTemplates_AsksForArchivedOnesOrNot(bool includeArchived)
    {
        IReadOnlyList<DayTemplateResponse> templates = [];
        await ExpectOk(templates, new GetDayTemplatesQuery(includeArchived), c => c.GetDayTemplates(includeArchived, _cts.Token));
    }

    [Test]
    public async Task GetDayTemplate_ReturnsIt() =>
        await ExpectOk(_fixture.Create<DayTemplateResponse>(), new GetDayTemplateByIdQuery(RecordId), c => c.GetDayTemplate(RecordId, _cts.Token));

    [Test]
    public async Task CreateDayTemplate_SendsItsBlocksAndReturnsItWithItsAddress()
    {
        var request = DayTemplate();
        var template = _fixture.Create<DayTemplateResponse>();
        var mediator = new RecordingMediator(_ => template);

        var result = await Controller(mediator).CreateDayTemplate(request, _cts.Token);

        var command = mediator.Request.Should().BeOfType<CreateDayTemplateCommand>().Subject;
        command.Name.Should().Be(request.Name);
        command.Blocks.Should().Equal(ExpectedBlocks);
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

        await Controller(mediator).CreateDayTemplate(request, _cts.Token);

        mediator.Request.Should().BeOfType<CreateDayTemplateCommand>().Which.Blocks.Should().BeEmpty();
    }

    [Test]
    public async Task UpdateDayTemplate_SendsTheNameAndBlocks()
    {
        var request = DayTemplate();
        var template = _fixture.Create<DayTemplateResponse>();
        var mediator = new RecordingMediator(_ => template);

        var result = await Controller(mediator).UpdateDayTemplate(RecordId, request, _cts.Token);

        var command = mediator.Request.Should().BeOfType<UpdateDayTemplateCommand>().Subject;
        command.DayTemplateId.Should().Be(RecordId);
        command.Name.Should().Be(request.Name);
        command.Blocks.Should().Equal(ExpectedBlocks);
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

    [Test]
    public async Task ArchiveAndRestoreDayTemplate_SetItsArchivedFlag()
    {
        var template = _fixture.Create<DayTemplateResponse>();
        await ExpectOk(template, new SetDayTemplateArchivedCommand(RecordId, true), c => c.ArchiveDayTemplate(RecordId, _cts.Token));
        await ExpectOk(template, new SetDayTemplateArchivedCommand(RecordId, false), c => c.RestoreDayTemplate(RecordId, _cts.Token));
    }

    [Test]
    public async Task DeleteDayTemplate_ReturnsNoContent() =>
        await ExpectNoContent(new DeleteDayTemplateCommand(RecordId), c => c.DeleteDayTemplate(RecordId, _cts.Token));

    // Week templates

    [TestCase(true)]
    [TestCase(false)]
    public async Task GetWeekTemplates_AsksForArchivedOnesOrNot(bool includeArchived)
    {
        IReadOnlyList<WeekTemplateResponse> templates = [];
        await ExpectOk(templates, new GetWeekTemplatesQuery(includeArchived), c => c.GetWeekTemplates(includeArchived, _cts.Token));
    }

    [Test]
    public async Task GetWeekTemplate_ReturnsIt() =>
        await ExpectOk(_fixture.Create<WeekTemplateResponse>(), new GetWeekTemplateByIdQuery(RecordId), c => c.GetWeekTemplate(RecordId, _cts.Token));

    [Test]
    public async Task CreateWeekTemplate_SendsEachDayAndReturnsItWithItsAddress()
    {
        var request = WeekTemplate();
        var template = _fixture.Create<WeekTemplateResponse>();
        var mediator = new RecordingMediator(_ => template);

        var result = await Controller(mediator).CreateWeekTemplate(request, _cts.Token);

        mediator.Request.Should().Be(new CreateWeekTemplateCommand(request.Name, RecordId, null, OtherId, null, RecordId));
        mediator.Tokens.Single().Should().Be(_cts.Token);
        var created = result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be($"/api/calendar/week-templates/{template.Id}");
        created.Value.Should().Be(template);
    }

    [Test]
    public async Task UpdateWeekTemplate_SendsTheNameAndEachDay()
    {
        var request = WeekTemplate();
        await ExpectOk(_fixture.Create<WeekTemplateResponse>(),
            new UpdateWeekTemplateCommand(OtherId, request.Name, RecordId, null, OtherId, null, RecordId),
            c => c.UpdateWeekTemplate(OtherId, request, _cts.Token));
    }

    [Test]
    public async Task ArchiveAndRestoreWeekTemplate_SetItsArchivedFlag()
    {
        var template = _fixture.Create<WeekTemplateResponse>();
        await ExpectOk(template, new SetWeekTemplateArchivedCommand(RecordId, true), c => c.ArchiveWeekTemplate(RecordId, _cts.Token));
        await ExpectOk(template, new SetWeekTemplateArchivedCommand(RecordId, false), c => c.RestoreWeekTemplate(RecordId, _cts.Token));
    }

    [Test]
    public async Task DeleteWeekTemplate_ReturnsNoContent() =>
        await ExpectNoContent(new DeleteWeekTemplateCommand(RecordId), c => c.DeleteWeekTemplate(RecordId, _cts.Token));

    // Who may call what

    [Test]
    public async Task Reads_AnyStaff()
    {
        foreach (var action in Reads)
        {
            (await ControllerAuthorisation.MayCallAsync<CalendarSetupController>(action, SignedIn("User"))).Should().BeTrue(action);
            (await ControllerAuthorisation.MayCallAsync<CalendarSetupController>(action, SignedIn("Admin"))).Should().BeTrue(action);
            (await ControllerAuthorisation.MayCallAsync<CalendarSetupController>(action, SignedIn())).Should().BeFalse(action);
        }
    }

    [Test]
    public async Task Writes_AdminsOnly()
    {
        var writes = ControllerAuthorisation.Actions<CalendarSetupController>().Except(Reads).ToList();
        writes.Should().HaveCount(15);

        foreach (var action in writes)
        {
            (await ControllerAuthorisation.MayCallAsync<CalendarSetupController>(action, SignedIn("User"))).Should().BeFalse(action);
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
