using AutoFixture;
using FluentAssertions;
using MedApp.API.Controllers;
using MedApp.Application.Appointments.Commands.BookAppointment;
using MedApp.Application.Appointments.Commands.CancelAppointment;
using MedApp.Application.Appointments.Commands.CreateAppointment;
using MedApp.Application.Appointments.Commands.MarkAppointmentEnteredInError;
using MedApp.Application.Appointments.Commands.RecordAppointmentOutcome;
using MedApp.Application.Appointments.Commands.RecordPastAppointment;
using MedApp.Application.Appointments.Commands.UpdateAppointmentNotes;
using MedApp.Application.Appointments.Queries.GetAppointmentById;
using MedApp.Application.Appointments.Queries.SearchAppointments;
using MedApp.Application.Procedures.Common;
using MedApp.Contracts.Appointments.Requests;
using MedApp.Contracts.Appointments.Responses;
using MedApp.Contracts.Common;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Mvc;
using static MedApp.UnitTests.Common.Constants.ApiTestConstants;

namespace MedApp.UnitTests.Api.Controllers;

[TestFixture]
public sealed class AppointmentsControllerTests
{
    private static readonly ProcedureActor Actor = new(UserId, Username);
    private IFixture _fixture = null!;
    private AppointmentResponse _appointment = null!;
    private RecordingMediator _mediator = null!;
    private CancellationTokenSource _cts = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        _appointment = _fixture.Create<AppointmentResponse>();
        _mediator = new RecordingMediator(_ => _appointment);
        _cts = new CancellationTokenSource();
    }

    [TearDown]
    public void TearDown() => _cts.Dispose();

    private AppointmentsController Controller => new AppointmentsController(_mediator).WithRequest(SignedIn("User"));

    /// <summary>Sent the request given, as the signed-in user, with the call's token, and returned the appointment.</summary>
    private void ExpectSentAndOk(object request, IActionResult result)
    {
        _mediator.Request.Should().Be(request);
        _mediator.Tokens.Single().Should().Be(_cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(_appointment);
    }

    [Test]
    public async Task CreateAppointment_Booking_SendsEveryValueAsTheUserAndReturnsItWithItsAddress()
    {
        var request = _fixture.Create<CreateAppointmentRequest>();

        var result = await Controller.CreateAppointment(request, _cts.Token);

        _mediator.Request.Should().Be(new CreateAppointmentCommand(
            request.PatientId, request.ProcedureId, request.StageId, request.StaffUserId, request.StartUtc, request.EndUtc, request.Notes, Actor));
        _mediator.Tokens.Single().Should().Be(_cts.Token);
        var created = result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be($"/api/appointments/{_appointment.Id}");
        created.Value.Should().Be(_appointment);
    }

    [Test]
    public async Task CreateAppointment_Request_SendsNoStaffOrTimes()
    {
        var request = new CreateAppointmentRequest { PatientId = RecordId };

        await Controller.CreateAppointment(request, _cts.Token);

        _mediator.Request.Should().Be(new CreateAppointmentCommand(RecordId, null, null, null, null, null, null, Actor));
    }

    [Test]
    public async Task RecordPastAppointment_SendsEveryValueAsTheUserAndReturnsItWithItsAddress()
    {
        var request = _fixture.Create<RecordPastAppointmentRequest>();

        var result = await Controller.RecordPastAppointment(request, _cts.Token);

        _mediator.Request.Should().Be(new RecordPastAppointmentCommand(
            request.PatientId, request.ProcedureId, request.StageId, request.StaffUserId, request.StartUtc, request.EndUtc,
            request.Outcome, request.Notes, Actor));
        _mediator.Tokens.Single().Should().Be(_cts.Token);
        var created = result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be($"/api/appointments/{_appointment.Id}");
        created.Value.Should().Be(_appointment);
    }

    [TestCase("User", false)]
    [TestCase("Admin", true)]
    public async Task MarkEnteredInError_SendsTheReasonTheUserAndWhetherTheyreAnAdmin(string role, bool isAdmin)
    {
        var response = _fixture.Create<EnteredInErrorResponse>();
        var mediator = new RecordingMediator(_ => response);

        var result = await new AppointmentsController(mediator).WithRequest(SignedIn(role))
            .MarkEnteredInError(RecordId, new MarkEnteredInErrorRequest { Reason = "Wrong patient." }, _cts.Token);

        mediator.Request.Should().Be(new MarkAppointmentEnteredInErrorCommand(RecordId, "Wrong patient.", Actor with { IsAdmin = isAdmin }, isAdmin));
        mediator.Tokens.Single().Should().Be(_cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(response);
    }

    [Test]
    public async Task GetAppointmentById_ReturnsIt() =>
        ExpectSentAndOk(new GetAppointmentByIdQuery(RecordId), await Controller.GetAppointmentById(RecordId, _cts.Token));

    [Test]
    public async Task SearchAppointments_SendsEveryFilter()
    {
        var request = _fixture.Create<SearchAppointmentsRequest>();
        var page = new PagedResponse<AppointmentResponse>([], 1, 10, 0, 0);
        _mediator = new RecordingMediator(_ => page);

        var result = await Controller.SearchAppointments(request, _cts.Token);

        _mediator.Request.Should().BeOfType<SearchAppointmentsQuery>().Which.Should().BeEquivalentTo(new SearchAppointmentsQuery(
            request.Page, request.PageSize, request.Sort, request.Ascending, request.PatientId, request.ProcedureId, request.StageId,
            request.StaffUserId, request.Statuses, request.From, request.To, request.Search));
        _mediator.Tokens.Single().Should().Be(_cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(page);
    }

    [Test]
    public async Task SearchAppointments_NoStatuses_SendsNone()
    {
        _mediator = new RecordingMediator(_ => new PagedResponse<AppointmentResponse>([], 1, 10, 0, 0));

        await Controller.SearchAppointments(new SearchAppointmentsRequest(), _cts.Token);

        _mediator.Request.Should().Be(new SearchAppointmentsQuery(1, 10, null, true, null, null, null, null, null, null, null, null));
    }

    [Test]
    public async Task BookAppointment_BooksAsTheUser()
    {
        var request = _fixture.Create<BookAppointmentRequest>();

        ExpectSentAndOk(
            new BookAppointmentCommand(RecordId, request.StaffUserId, request.StartUtc, request.EndUtc, request.ExpectedStatus, Actor),
            await Controller.BookAppointment(RecordId, request, _cts.Token));
    }

    [Test]
    public async Task RecordOutcome_RecordsAsTheUser() =>
        ExpectSentAndOk(
            new RecordAppointmentOutcomeCommand(RecordId, "Attended", Actor),
            await Controller.RecordOutcome(RecordId, new RecordAppointmentOutcomeRequest { Outcome = "Attended" }, _cts.Token));

    [Test]
    public async Task CancelAppointment_CancelsAsTheUserWithTheReason() =>
        ExpectSentAndOk(
            new CancelAppointmentCommand(RecordId, "Synthetic reason.", Actor),
            await Controller.CancelAppointment(RecordId, new CancelAppointmentRequest { Reason = "Synthetic reason." }, _cts.Token));

    [Test]
    public async Task UpdateNotes_SavesThemAsTheUser() =>
        ExpectSentAndOk(
            new UpdateAppointmentNotesCommand(RecordId, "<p>Synthetic notes.</p>", Actor),
            await Controller.UpdateNotes(RecordId, new UpdateAppointmentNotesRequest { Notes = "<p>Synthetic notes.</p>" }, _cts.Token));

    [TestCase("User", true)]
    [TestCase("Admin", true)]
    [TestCase(null, false)]
    public async Task EveryAction_StaffOnly(string? role, bool allowed)
    {
        var user = role is null ? SignedIn() : SignedIn(role);

        foreach (var action in ControllerAuthorisation.Actions<AppointmentsController>())
            (await ControllerAuthorisation.MayCallAsync<AppointmentsController>(action, user)).Should().Be(allowed, action);
    }
}
