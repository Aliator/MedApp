using AutoFixture;
using MedApp.UnitTests.Common.Constants;
using FluentAssertions;
using MedApp.API.Controllers;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.NoteDeletionRequests.Commands.RequestNoteDeletion;
using MedApp.Application.Pathways.PatientPathways.Commands.AddPathwayNote;
using MedApp.Application.Pathways.PatientPathways.Commands.AssignPatientPathway;
using MedApp.Application.Pathways.PatientPathways.Commands.ChangePatientPathwayStatus;
using MedApp.Application.Pathways.PatientPathways.Commands.CreatePatientPathway;
using MedApp.Application.Pathways.PatientPathways.Commands.DeletePatientPathway;
using MedApp.Application.Pathways.PatientPathways.Commands.DeletePathwayNote;
using MedApp.Application.Pathways.PatientPathways.Commands.EditPathwayNote;
using MedApp.Application.Pathways.PatientPathways.Commands.FinishPathwayStage;
using MedApp.Application.Pathways.PatientPathways.Commands.ReinstatePathwayStage;
using MedApp.Application.Pathways.PatientPathways.Commands.SkipPathwayStage;
using MedApp.Application.Pathways.PatientPathways.Commands.StartPathwayStage;
using MedApp.Application.Pathways.PatientPathways.Commands.UnassignPatientPathway;
using MedApp.Application.Pathways.PatientPathways.Commands.UnlockPathwayStage;
using MedApp.Application.Pathways.PatientPathways.Commands.UpdatePatientPathway;
using MedApp.Application.Pathways.PatientPathways.Commands.UpdatePathwayStageNotes;
using MedApp.Application.Pathways.PatientPathways.Queries.GetPatientPathwayById;
using MedApp.Application.Pathways.PatientPathways.Queries.SearchPatientPathways;
using MedApp.Contracts.Common;
using MedApp.Contracts.Pathways.NoteDeletionRequests.Requests;
using MedApp.Contracts.Pathways.PatientPathways.Requests;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Mvc;
using static MedApp.UnitTests.Common.Constants.ApiTestConstants;

namespace MedApp.UnitTests.Api.Controllers;

[TestFixture]
public sealed class PatientPathwaysControllerTests
{
    // As the controllers see it: signed in with the old User role, now "All staff (before roles)".
    private static readonly PathwayActor Actor = new(UserId, Username, TestPermissions.Staff);
    private IFixture _fixture = null!;
    private PatientPathwayResponse _pathway = null!;
    private RecordingMediator _mediator = null!;
    private CancellationTokenSource _cts = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        _pathway = _fixture.Create<PatientPathwayResponse>();
        _mediator = new RecordingMediator(_ => _pathway);
        _cts = new CancellationTokenSource();
    }

    [TearDown]
    public void TearDown() => _cts.Dispose();

    private PatientPathwaysController Controller => new PatientPathwaysController(_mediator).WithRequest(SignedIn("User"));

    /// <summary>Sent the request given, as the signed-in user, with the call's token, and returned the pathway.</summary>
    private void ExpectSentAndOk(object request, IActionResult result)
    {
        _mediator.Request.Should().Be(request);
        _mediator.Tokens.Single().Should().Be(_cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(_pathway);
    }

    [Test]
    public async Task CreatePatientPathway_ReturnsTheNewPathwayWithItsAddress()
    {
        var request = _fixture.Create<CreatePatientPathwayRequest>();

        var result = await Controller.CreatePatientPathway(request, _cts.Token);

        _mediator.Request.Should().Be(new CreatePatientPathwayCommand(
            request.PatientId, request.TemplateId, request.Title, request.Notes, request.DueDate, request.Priority, Actor));
        var created = result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be($"/api/pathways/patient-pathways/{_pathway.Id}");
        created.Value.Should().Be(_pathway);
    }

    [Test]
    public async Task GetPatientPathwayById_ReturnsIt() =>
        ExpectSentAndOk(new GetPatientPathwayByIdQuery(RecordId, UserId, TestPermissions.Staff), await Controller.GetPatientPathwayById(RecordId, _cts.Token));

    [Test]
    public async Task AddPathwayNote_ReturnsTheNoteWithItsPathwaysAddress()
    {
        var note = _fixture.Create<PatientPathwayNoteResponse>();
        _mediator = new RecordingMediator(_ => note);
        var request = _fixture.Create<AddPathwayNoteRequest>();

        var result = await Controller.AddPathwayNote(RecordId, request, _cts.Token);

        _mediator.Request.Should().Be(new AddPathwayNoteCommand(RecordId, request.Text, Actor));
        _mediator.Tokens.Single().Should().Be(_cts.Token);
        var created = result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be($"/api/pathways/patient-pathways/{RecordId}");
        created.Value.Should().Be(note);
    }

    [Test]
    public async Task EditPathwayNote_SendsTheTextAndVersionAsTheUser()
    {
        var note = _fixture.Create<PatientPathwayNoteResponse>();
        _mediator = new RecordingMediator(_ => note);
        var request = _fixture.Create<EditPathwayNoteRequest>();
        var noteId = Guid.NewGuid();

        var result = await Controller.EditPathwayNote(RecordId, noteId, request, _cts.Token);

        _mediator.Request.Should().Be(new EditPathwayNoteCommand(RecordId, noteId, request.Text, request.ExpectedVersion, Actor));
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(note);
    }

    [Test]
    public async Task SearchPatientPathways_SendsEveryFilterForTheSignedInUser()
    {
        var request = _fixture.Create<SearchPatientPathwaysRequest>();
        var page = new PagedResponse<PatientPathwaySummaryResponse>([], 1, 10, 0, 0);
        _mediator = new RecordingMediator(_ => page);

        var result = await Controller.SearchPatientPathways(request, _cts.Token);

        _mediator.Request.Should().Be(new SearchPatientPathwaysQuery(
            request.Page, request.PageSize, request.Sort, request.Ascending, request.Search, request.PatientId,
            request.TemplateId, request.Statuses, request.Priority, request.AssignedToUserId, request.AssignedToMe,
            request.OverdueOnly, request.DueFrom, request.DueTo, request.IncludeClosed, request.Started, UserId,
            request.Priorities, request.Title, request.TemplateName, request.PatientName, request.NobodyAssigned,
            request.TeamIds, request.StageDefinitionIds, request.BreachedOnly));
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(page);
    }

    [Test]
    public async Task UpdatePatientPathway_SendsTheChangesAsTheUser()
    {
        var request = _fixture.Create<UpdatePatientPathwayRequest>();

        var result = await Controller.UpdatePatientPathway(RecordId, request, _cts.Token);

        ExpectSentAndOk(new UpdatePatientPathwayCommand(RecordId, request.Title, request.DueDate, request.Priority, Actor, request.Notes), result);
    }

    [Test]
    public async Task ChangePatientPathwayStatus_SendsTheStatusAndReasonAsTheUser()
    {
        var request = _fixture.Create<ChangePatientPathwayStatusRequest>();

        var result = await Controller.ChangePatientPathwayStatus(RecordId, request, _cts.Token);

        ExpectSentAndOk(new ChangePatientPathwayStatusCommand(RecordId, request.Status, request.Reason, Actor), result);
    }

    [Test]
    public async Task DeletePatientPathway_DeletesAsTheUserAndReturnsNoContent()
    {
        var result = await Controller.DeletePatientPathway(RecordId, _cts.Token);

        _mediator.Request.Should().Be(new DeletePatientPathwayCommand(RecordId, Actor));
        result.Should().BeOfType<NoContentResult>();
    }

    [Test]
    public async Task DeletePathwayNote_DeletesAsTheUserWithTheReasonAndReturnsNoContent()
    {
        var noteId = Guid.NewGuid();

        var request = new DeletePathwayNoteRequest { Reason = "Other", Detail = "Synthetic detail.", ExpectedVersion = 3 };

        var result = await Controller.DeletePathwayNote(RecordId, noteId, request, _cts.Token);

        _mediator.Request.Should().Be(new DeletePathwayNoteCommand(RecordId, noteId, "Other", "Synthetic detail.", 3, Actor));
        _mediator.Tokens.Single().Should().Be(_cts.Token);
        result.Should().BeOfType<NoContentResult>();
    }

    [Test]
    public async Task RequestNoteDeletion_ReturnsTheNoteWithTheRequestsAddress()
    {
        var note = _fixture.Create<PatientPathwayNoteResponse>();
        _mediator = new RecordingMediator(_ => note);
        var noteId = Guid.NewGuid();

        var result = await Controller.RequestNoteDeletion(
            RecordId, noteId, new RequestNoteDeletionRequest { Reason = "Duplicate", Detail = "Synthetic detail." }, _cts.Token);

        _mediator.Request.Should().Be(new RequestNoteDeletionCommand(RecordId, noteId, "Duplicate", "Synthetic detail.", Actor));
        _mediator.Tokens.Single().Should().Be(_cts.Token);
        var created = result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be($"/api/pathways/note-deletion-requests/{note.DeletionRequestId}");
        created.Value.Should().Be(note);
    }

    [Test]
    public async Task DeletePathwayNote_AdminsOnly_AndRequestNoteDeletion_AnyStaff()
    {
        const string delete = nameof(PatientPathwaysController.DeletePathwayNote);
        const string request = nameof(PatientPathwaysController.RequestNoteDeletion);

        (await ControllerAuthorisation.MayCallAsync<PatientPathwaysController>(delete, SignedIn("Admin"))).Should().BeTrue();
        (await ControllerAuthorisation.MayCallAsync<PatientPathwaysController>(delete, SignedIn("User"))).Should().BeFalse();
        (await ControllerAuthorisation.MayCallAsync<PatientPathwaysController>(request, SignedIn("User"))).Should().BeTrue();
        (await ControllerAuthorisation.MayCallAsync<PatientPathwaysController>(request, SignedIn("Admin"))).Should().BeTrue();
        (await ControllerAuthorisation.MayCallAsync<PatientPathwaysController>(request, SignedIn())).Should().BeFalse();
    }

    [Test]
    public async Task AssignPatientPathway_AssignsAsTheUser() =>
        ExpectSentAndOk(new AssignPatientPathwayCommand(RecordId, OtherId, Actor), await Controller.AssignPatientPathway(RecordId, OtherId, _cts.Token));

    [Test]
    public async Task UnassignPatientPathway_UnassignsAsTheUser() =>
        ExpectSentAndOk(new UnassignPatientPathwayCommand(RecordId, OtherId, Actor), await Controller.UnassignPatientPathway(RecordId, OtherId, _cts.Token));

    [Test]
    public async Task StartPathwayStage_StartsAsTheUser() =>
        ExpectSentAndOk(new StartPathwayStageCommand(RecordId, OtherId, Actor), await Controller.StartPathwayStage(RecordId, OtherId, _cts.Token));

    [Test]
    public async Task UpdatePathwayStageNotes_SavesTheNotesAsTheUser()
    {
        var result = await Controller.UpdatePathwayStageNotes(RecordId, OtherId, new UpdatePathwayStageNotesRequest { Notes = "Synthetic **notes**." }, _cts.Token);

        ExpectSentAndOk(new UpdatePathwayStageNotesCommand(RecordId, OtherId, "Synthetic **notes**.", Actor), result);
    }

    [Test]
    public async Task FinishPathwayStage_FinishesAsTheUser() =>
        ExpectSentAndOk(new FinishPathwayStageCommand(RecordId, OtherId, Actor), await Controller.FinishPathwayStage(RecordId, OtherId, null, _cts.Token));

    [Test]
    public async Task FinishPathwayStage_EmptyBody_FinishesWithAVisitAsStaff()
    {
        var result = await Controller.FinishPathwayStage(RecordId, OtherId, new FinishPathwayStageRequest(), _cts.Token);

        ExpectSentAndOk(new FinishPathwayStageCommand(RecordId, OtherId, Actor, WithoutVisit: false, Reason: null, CanFinishWithoutVisit: false), result);
    }

    /// <summary>Whether they may finish without the visit is FinishWithoutVisit, which the built-in Admin role hasn't.</summary>
    [TestCase("User", false)]
    [TestCase("Admin", false)]
    [TestCase("Clinician", true)]
    [TestCase("Senior clinician", true)]
    public async Task FinishPathwayStage_WithoutVisit_SendsTheReasonAndWhetherTheyMayFinishWithoutAVisit(string role, bool mayFinishWithoutVisit)
    {
        var controller = new PatientPathwaysController(_mediator).WithRequest(SignedIn(role));
        var request = new FinishPathwayStageRequest { WithoutVisit = true, Reason = "Synthetic reason." };

        var result = await controller.FinishPathwayStage(RecordId, OtherId, request, _cts.Token);

        ExpectSentAndOk(new FinishPathwayStageCommand(RecordId, OtherId, Actor with { Permissions = TestPermissions.ForRoles(role) }, WithoutVisit: true, Reason: "Synthetic reason.", CanFinishWithoutVisit: mayFinishWithoutVisit), result);
    }

    [Test]
    public async Task FinishPathwayStage_NoBodyAsASeniorClinician_FinishesWithAVisit()
    {
        var controller = new PatientPathwaysController(_mediator).WithRequest(SignedIn("Senior clinician"));

        var result = await controller.FinishPathwayStage(RecordId, OtherId, null, _cts.Token);

        ExpectSentAndOk(new FinishPathwayStageCommand(RecordId, OtherId, Actor with { Permissions = TestPermissions.ForRoles("Senior clinician") }, WithoutVisit: false, Reason: null, CanFinishWithoutVisit: true), result);
    }

    [TestCase("Admin", false)]
    [TestCase("Senior clinician", true)]
    public async Task SkipPathwayStage_SendsWhetherTheyMayFinishWithoutAVisit(string role, bool mayFinishWithoutVisit)
    {
        var controller = new PatientPathwaysController(_mediator).WithRequest(SignedIn(role));

        var result = await controller.SkipPathwayStage(RecordId, OtherId, new SkipPathwayStageRequest { Reason = "Other", Detail = "Synthetic detail." }, _cts.Token);

        ExpectSentAndOk(new SkipPathwayStageCommand(RecordId, OtherId, "Other", "Synthetic detail.",
            Actor with { Permissions = TestPermissions.ForRoles(role) }, mayFinishWithoutVisit), result);
    }

    [Test]
    public void FinishPathwayStage_Body_IsOptional()
    {
        var parameter = typeof(PatientPathwaysController).GetMethod(nameof(PatientPathwaysController.FinishPathwayStage))!
            .GetParameters().Single(p => p.ParameterType == typeof(FinishPathwayStageRequest));

        var fromBody = parameter.GetCustomAttributes(typeof(FromBodyAttribute), false).Cast<FromBodyAttribute>().Single();
        fromBody.EmptyBodyBehavior.Should().Be(Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow);
    }

    [Test]
    public async Task UnlockPathwayStage_UnlocksAsTheUser() =>
        ExpectSentAndOk(new UnlockPathwayStageCommand(RecordId, OtherId, Actor), await Controller.UnlockPathwayStage(RecordId, OtherId, _cts.Token));

    [Test]
    public async Task FinishPathwayStage_WithABranch_SendsIt() =>
        ExpectSentAndOk(
            new FinishPathwayStageCommand(RecordId, OtherId, Actor, Branch: "Treatment"),
            await Controller.FinishPathwayStage(RecordId, OtherId, new FinishPathwayStageRequest { Branch = "Treatment" }, _cts.Token));

    [Test]
    public async Task SkipPathwayStage_SkipsAsTheUserWithTheReason() =>
        ExpectSentAndOk(
            new SkipPathwayStageCommand(RecordId, OtherId, "Other", "Synthetic detail.", Actor),
            await Controller.SkipPathwayStage(RecordId, OtherId, new SkipPathwayStageRequest { Reason = "Other", Detail = "Synthetic detail." }, _cts.Token));

    [Test]
    public async Task ReinstatePathwayStage_ReinstatesAsTheUserWithTheReason() =>
        ExpectSentAndOk(
            new ReinstatePathwayStageCommand(RecordId, OtherId, "Synthetic reason.", Actor),
            await Controller.ReinstatePathwayStage(RecordId, OtherId, new ReinstatePathwayStageRequest { Reason = "Synthetic reason." }, _cts.Token));
}
