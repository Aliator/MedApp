using AutoFixture;
using FluentAssertions;
using MedApp.API.Controllers;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.NoteDeletionRequests.Commands.RequestNoteDeletion;
using MedApp.Application.Procedures.PatientProcedures.Commands.AddProcedureNote;
using MedApp.Application.Procedures.PatientProcedures.Commands.AssignPatientProcedure;
using MedApp.Application.Procedures.PatientProcedures.Commands.ChangePatientProcedureStatus;
using MedApp.Application.Procedures.PatientProcedures.Commands.CreatePatientProcedure;
using MedApp.Application.Procedures.PatientProcedures.Commands.DeletePatientProcedure;
using MedApp.Application.Procedures.PatientProcedures.Commands.DeleteProcedureNote;
using MedApp.Application.Procedures.PatientProcedures.Commands.EditProcedureNote;
using MedApp.Application.Procedures.PatientProcedures.Commands.FinishProcedureStage;
using MedApp.Application.Procedures.PatientProcedures.Commands.StartProcedureStage;
using MedApp.Application.Procedures.PatientProcedures.Commands.UnassignPatientProcedure;
using MedApp.Application.Procedures.PatientProcedures.Commands.UnlockProcedureStage;
using MedApp.Application.Procedures.PatientProcedures.Commands.UpdatePatientProcedure;
using MedApp.Application.Procedures.PatientProcedures.Commands.UpdateProcedureStageNotes;
using MedApp.Application.Procedures.PatientProcedures.Queries.GetPatientProcedureById;
using MedApp.Application.Procedures.PatientProcedures.Queries.SearchPatientProcedures;
using MedApp.Contracts.Common;
using MedApp.Contracts.Procedures.NoteDeletionRequests.Requests;
using MedApp.Contracts.Procedures.PatientProcedures.Requests;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Mvc;
using static MedApp.UnitTests.Common.Constants.ApiTestConstants;

namespace MedApp.UnitTests.Api.Controllers;

[TestFixture]
public sealed class PatientProceduresControllerTests
{
    private static readonly ProcedureActor Actor = new(UserId, Username);
    private IFixture _fixture = null!;
    private PatientProcedureResponse _procedure = null!;
    private RecordingMediator _mediator = null!;
    private CancellationTokenSource _cts = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        _procedure = _fixture.Create<PatientProcedureResponse>();
        _mediator = new RecordingMediator(_ => _procedure);
        _cts = new CancellationTokenSource();
    }

    [TearDown]
    public void TearDown() => _cts.Dispose();

    private PatientProceduresController Controller => new PatientProceduresController(_mediator).WithRequest(SignedIn("User"));

    /// <summary>Sent the request given, as the signed-in user, with the call's token, and returned the procedure.</summary>
    private void ExpectSentAndOk(object request, IActionResult result)
    {
        _mediator.Request.Should().Be(request);
        _mediator.Tokens.Single().Should().Be(_cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(_procedure);
    }

    [Test]
    public async Task CreatePatientProcedure_ReturnsTheNewProcedureWithItsAddress()
    {
        var request = _fixture.Create<CreatePatientProcedureRequest>();

        var result = await Controller.CreatePatientProcedure(request, _cts.Token);

        _mediator.Request.Should().Be(new CreatePatientProcedureCommand(
            request.PatientId, request.TemplateId, request.Title, request.Notes, request.DueDate, request.Priority, Actor));
        var created = result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be($"/api/procedures/patient-procedures/{_procedure.Id}");
        created.Value.Should().Be(_procedure);
    }

    [Test]
    public async Task GetPatientProcedureById_ReturnsIt() =>
        ExpectSentAndOk(new GetPatientProcedureByIdQuery(RecordId, UserId), await Controller.GetPatientProcedureById(RecordId, _cts.Token));

    [Test]
    public async Task AddProcedureNote_ReturnsTheNoteWithItsProceduresAddress()
    {
        var note = _fixture.Create<PatientProcedureNoteResponse>();
        _mediator = new RecordingMediator(_ => note);
        var request = _fixture.Create<AddProcedureNoteRequest>();

        var result = await Controller.AddProcedureNote(RecordId, request, _cts.Token);

        _mediator.Request.Should().Be(new AddProcedureNoteCommand(RecordId, request.Text, Actor));
        _mediator.Tokens.Single().Should().Be(_cts.Token);
        var created = result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be($"/api/procedures/patient-procedures/{RecordId}");
        created.Value.Should().Be(note);
    }

    [Test]
    public async Task EditProcedureNote_SendsTheTextAndVersionAsTheUser()
    {
        var note = _fixture.Create<PatientProcedureNoteResponse>();
        _mediator = new RecordingMediator(_ => note);
        var request = _fixture.Create<EditProcedureNoteRequest>();
        var noteId = Guid.NewGuid();

        var result = await Controller.EditProcedureNote(RecordId, noteId, request, _cts.Token);

        _mediator.Request.Should().Be(new EditProcedureNoteCommand(RecordId, noteId, request.Text, request.ExpectedVersion, Actor));
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(note);
    }

    [Test]
    public async Task SearchPatientProcedures_SendsEveryFilterForTheSignedInUser()
    {
        var request = _fixture.Create<SearchPatientProceduresRequest>();
        var page = new PagedResponse<PatientProcedureSummaryResponse>([], 1, 10, 0, 0);
        _mediator = new RecordingMediator(_ => page);

        var result = await Controller.SearchPatientProcedures(request, _cts.Token);

        _mediator.Request.Should().Be(new SearchPatientProceduresQuery(
            request.Page, request.PageSize, request.Sort, request.Ascending, request.Search, request.PatientId,
            request.TemplateId, request.Statuses, request.Priority, request.AssignedToUserId, request.AssignedToMe,
            request.OverdueOnly, request.DueFrom, request.DueTo, request.IncludeClosed, request.Started, UserId,
            request.Priorities, request.Title, request.TemplateName, request.PatientName, request.NobodyAssigned));
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(page);
    }

    [Test]
    public async Task UpdatePatientProcedure_SendsTheChangesAsTheUser()
    {
        var request = _fixture.Create<UpdatePatientProcedureRequest>();

        var result = await Controller.UpdatePatientProcedure(RecordId, request, _cts.Token);

        ExpectSentAndOk(new UpdatePatientProcedureCommand(RecordId, request.Title, request.DueDate, request.Priority, Actor, request.Notes), result);
    }

    [Test]
    public async Task ChangePatientProcedureStatus_SendsTheStatusAndReasonAsTheUser()
    {
        var request = _fixture.Create<ChangePatientProcedureStatusRequest>();

        var result = await Controller.ChangePatientProcedureStatus(RecordId, request, _cts.Token);

        ExpectSentAndOk(new ChangePatientProcedureStatusCommand(RecordId, request.Status, request.Reason, Actor), result);
    }

    [Test]
    public async Task DeletePatientProcedure_DeletesAsTheUserAndReturnsNoContent()
    {
        var result = await Controller.DeletePatientProcedure(RecordId, _cts.Token);

        _mediator.Request.Should().Be(new DeletePatientProcedureCommand(RecordId, Actor));
        result.Should().BeOfType<NoContentResult>();
    }

    [Test]
    public async Task DeleteProcedureNote_DeletesAsTheUserWithTheReasonAndReturnsNoContent()
    {
        var noteId = Guid.NewGuid();

        var result = await Controller.DeleteProcedureNote(RecordId, noteId, new DeleteProcedureNoteRequest { Reason = "Synthetic reason." }, _cts.Token);

        _mediator.Request.Should().Be(new DeleteProcedureNoteCommand(RecordId, noteId, "Synthetic reason.", Actor));
        _mediator.Tokens.Single().Should().Be(_cts.Token);
        result.Should().BeOfType<NoContentResult>();
    }

    [Test]
    public async Task RequestNoteDeletion_ReturnsTheNoteWithTheRequestsAddress()
    {
        var note = _fixture.Create<PatientProcedureNoteResponse>();
        _mediator = new RecordingMediator(_ => note);
        var noteId = Guid.NewGuid();

        var result = await Controller.RequestNoteDeletion(RecordId, noteId, new RequestNoteDeletionRequest { Reason = "Synthetic reason." }, _cts.Token);

        _mediator.Request.Should().Be(new RequestNoteDeletionCommand(RecordId, noteId, "Synthetic reason.", Actor));
        _mediator.Tokens.Single().Should().Be(_cts.Token);
        var created = result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be($"/api/procedures/note-deletion-requests/{note.DeletionRequestId}");
        created.Value.Should().Be(note);
    }

    [Test]
    public async Task DeleteProcedureNote_AdminsOnly_AndRequestNoteDeletion_AnyStaff()
    {
        const string delete = nameof(PatientProceduresController.DeleteProcedureNote);
        const string request = nameof(PatientProceduresController.RequestNoteDeletion);

        (await ControllerAuthorisation.MayCallAsync<PatientProceduresController>(delete, SignedIn("Admin"))).Should().BeTrue();
        (await ControllerAuthorisation.MayCallAsync<PatientProceduresController>(delete, SignedIn("User"))).Should().BeFalse();
        (await ControllerAuthorisation.MayCallAsync<PatientProceduresController>(request, SignedIn("User"))).Should().BeTrue();
        (await ControllerAuthorisation.MayCallAsync<PatientProceduresController>(request, SignedIn("Admin"))).Should().BeTrue();
        (await ControllerAuthorisation.MayCallAsync<PatientProceduresController>(request, SignedIn())).Should().BeFalse();
    }

    [Test]
    public async Task AssignPatientProcedure_AssignsAsTheUser() =>
        ExpectSentAndOk(new AssignPatientProcedureCommand(RecordId, OtherId, Actor), await Controller.AssignPatientProcedure(RecordId, OtherId, _cts.Token));

    [Test]
    public async Task UnassignPatientProcedure_UnassignsAsTheUser() =>
        ExpectSentAndOk(new UnassignPatientProcedureCommand(RecordId, OtherId, Actor), await Controller.UnassignPatientProcedure(RecordId, OtherId, _cts.Token));

    [Test]
    public async Task StartProcedureStage_StartsAsTheUser() =>
        ExpectSentAndOk(new StartProcedureStageCommand(RecordId, OtherId, Actor), await Controller.StartProcedureStage(RecordId, OtherId, _cts.Token));

    [Test]
    public async Task UpdateProcedureStageNotes_SavesTheNotesAsTheUser()
    {
        var result = await Controller.UpdateProcedureStageNotes(RecordId, OtherId, new UpdateProcedureStageNotesRequest { Notes = "Synthetic **notes**." }, _cts.Token);

        ExpectSentAndOk(new UpdateProcedureStageNotesCommand(RecordId, OtherId, "Synthetic **notes**.", Actor), result);
    }

    [Test]
    public async Task FinishProcedureStage_FinishesAsTheUser() =>
        ExpectSentAndOk(new FinishProcedureStageCommand(RecordId, OtherId, Actor), await Controller.FinishProcedureStage(RecordId, OtherId, null, _cts.Token));

    [Test]
    public async Task FinishProcedureStage_EmptyBody_FinishesWithAVisitAsStaff()
    {
        var result = await Controller.FinishProcedureStage(RecordId, OtherId, new FinishProcedureStageRequest(), _cts.Token);

        ExpectSentAndOk(new FinishProcedureStageCommand(RecordId, OtherId, Actor, WithoutVisit: false, Reason: null, IsAdmin: false), result);
    }

    [TestCase("User", false)]
    [TestCase("Admin", true)]
    public async Task FinishProcedureStage_WithoutVisit_SendsTheReasonAndWhetherTheyAreAnAdmin(string role, bool isAdmin)
    {
        var controller = new PatientProceduresController(_mediator).WithRequest(SignedIn(role));
        var request = new FinishProcedureStageRequest { WithoutVisit = true, Reason = "Synthetic reason." };

        var result = await controller.FinishProcedureStage(RecordId, OtherId, request, _cts.Token);

        ExpectSentAndOk(new FinishProcedureStageCommand(RecordId, OtherId, Actor, WithoutVisit: true, Reason: "Synthetic reason.", IsAdmin: isAdmin), result);
    }

    [Test]
    public async Task FinishProcedureStage_NoBodyAsAnAdmin_FinishesWithAVisit()
    {
        var controller = new PatientProceduresController(_mediator).WithRequest(SignedIn("Admin"));

        var result = await controller.FinishProcedureStage(RecordId, OtherId, null, _cts.Token);

        ExpectSentAndOk(new FinishProcedureStageCommand(RecordId, OtherId, Actor, WithoutVisit: false, Reason: null, IsAdmin: true), result);
    }

    [Test]
    public void FinishProcedureStage_Body_IsOptional()
    {
        var parameter = typeof(PatientProceduresController).GetMethod(nameof(PatientProceduresController.FinishProcedureStage))!
            .GetParameters().Single(p => p.ParameterType == typeof(FinishProcedureStageRequest));

        var fromBody = parameter.GetCustomAttributes(typeof(FromBodyAttribute), false).Cast<FromBodyAttribute>().Single();
        fromBody.EmptyBodyBehavior.Should().Be(Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow);
    }

    [Test]
    public async Task UnlockProcedureStage_UnlocksAsTheUser() =>
        ExpectSentAndOk(new UnlockProcedureStageCommand(RecordId, OtherId, Actor), await Controller.UnlockProcedureStage(RecordId, OtherId, _cts.Token));
}
