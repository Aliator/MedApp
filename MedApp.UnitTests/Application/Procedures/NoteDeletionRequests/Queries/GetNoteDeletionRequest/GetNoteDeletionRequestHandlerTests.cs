using AutoFixture;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Procedures.NoteDeletionRequests.Queries.GetNoteDeletionRequest;
using MedApp.Application.Procedures.Repositories;
using MedApp.Domain.Activity;
using MedApp.Domain.Patients;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;

namespace MedApp.UnitTests.Application.Procedures.NoteDeletionRequests.Queries.GetNoteDeletionRequest;

[TestFixture]
public sealed class GetNoteDeletionRequestHandlerTests : ProcedureHandlerTestBase
{
    private Mock<INoteDeletionRequestRepository> _requests = null!;
    private Mock<IPatientRepository> _patients = null!;
    private RecordingActivityLog _activity = null!;

    [SetUp]
    public void SetUp()
    {
        _requests = new Mock<INoteDeletionRequestRepository>();
        _patients = new Mock<IPatientRepository>();
        _activity = new RecordingActivityLog();
        Fixture.Inject(_requests.Object);
        Fixture.Inject(_patients.Object);
        Fixture.Inject<IActivityLog>(_activity);
    }

    private GetNoteDeletionRequestHandler Handler => Fixture.Create<GetNoteDeletionRequestHandler>();

    private ProcedureNoteDeletionRequest Given(ProcedureNoteDeletionRequest request)
    {
        _requests.Setup(r => r.GetByIdAsync(request.Id, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        return request;
    }

    [Test]
    public async Task Handle_Pending_ReturnsTheNotePatientAndProcedureAndRecordsAViewOfTheProcedure()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress));
        var note = procedure.AddNote(OtherUserId);
        var request = Given(procedure.RequestDeletion(note));

        var response = await Handler.Handle(new GetNoteDeletionRequestQuery(request.Id), CancellationToken.None);

        response.Should().BeEquivalentTo(new
        {
            request.Id,
            Status = "Pending",
            Reason = "WrongPatient",
            Detail = DeletionDetail,
            RequestedByUsername = Staff.Username,
            request.RequestedAtUtc,
            DecidedByUsername = (string?)null,
            ProcedureId = procedure.Id,
            ProcedureTitle = procedure.Title,
            Patient = new
            {
                procedure.Patient.Id,
                procedure.Patient.FirstName,
                procedure.Patient.LastName,
                procedure.Patient.DateOfBirth,
                procedure.Patient.NhsNumber
            },
            Note = new { note.Id, Text = NoteText, AuthorUsername = OtherUsername, note.CreatedAtUtc, DeletedAtUtc = (DateTime?)null },
            CanDecide = true,
            WhyCantDecide = (string?)null
        });
        _activity.Views.Should().ContainSingle().Which.Should().BeEquivalentTo((ActivityAction.ProcedureViewed, new List<Guid> { procedure.PatientId }, (Guid?)procedure.Id));
    }

    [Test]
    public async Task Handle_PatientRestricted_CantBeDecidedAndSaysWhy()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        patient.RestrictedAtUtc = Now.AddDays(-1);
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress, patient: patient));
        var request = Given(procedure.RequestDeletion(procedure.AddNote()));

        var response = await Handler.Handle(new GetNoteDeletionRequestQuery(request.Id), CancellationToken.None);

        response.CanDecide.Should().BeFalse();
        response.WhyCantDecide.Should().Be(PatientRules.RestrictedMessage);
    }

    [Test]
    public async Task Handle_ProcedureDeleted_HasNoNoteAndRecordsAViewOfThePatient()
    {
        var procedure = CreateProcedure(Status.Cancelled);
        var request = procedure.RequestDeletion(procedure.AddNote());
        request.Close(NoteDeletionRequestStatus.Closed, Admin.UserId, Admin.Username, Now);
        Given(request);
        _patients.Setup(r => r.GetByIdAsync(procedure.PatientId, It.IsAny<CancellationToken>())).ReturnsAsync(procedure.Patient);

        var response = await Handler.Handle(new GetNoteDeletionRequestQuery(request.Id), CancellationToken.None);

        response.Should().BeEquivalentTo(new
        {
            Status = "Closed",
            ProcedureTitle = (string?)null,
            Note = (object?)null,
            CanDecide = false,
            Patient = new { procedure.Patient.Id }
        });
        _activity.Views.Should().ContainSingle().Which.Should().BeEquivalentTo((ActivityAction.PatientViewed, new List<Guid> { procedure.PatientId }, (Guid?)null));
    }

    [Test]
    public async Task Handle_PatientGoneToo_RecordsNothing()
    {
        var procedure = CreateProcedure(Status.Cancelled);
        var request = Given(procedure.RequestDeletion(procedure.AddNote()));

        var response = await Handler.Handle(new GetNoteDeletionRequestQuery(request.Id), CancellationToken.None);

        response.Patient.Should().BeNull();
        _activity.Views.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_RequestNotFound_ThrowsNotFound()
    {
        var act = () => Handler.Handle(new GetNoteDeletionRequestQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _activity.Views.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_PassesCancellationToken()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress));
        var request = Given(procedure.RequestDeletion(procedure.AddNote()));
        using var cts = new CancellationTokenSource();

        await Handler.Handle(new GetNoteDeletionRequestQuery(request.Id), cts.Token);

        _requests.Verify(r => r.GetByIdAsync(request.Id, cts.Token), Times.Once);
        Repository.Verify(r => r.GetByIdAsync(procedure.Id, cts.Token), Times.Once);
        Identity.Verify(i => i.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), cts.Token), Times.Once);
        _activity.Tokens.Should().Equal(cts.Token);
    }
}
