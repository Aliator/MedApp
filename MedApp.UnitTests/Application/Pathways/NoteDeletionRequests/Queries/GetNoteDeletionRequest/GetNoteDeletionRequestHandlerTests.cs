using AutoFixture;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Pathways.NoteDeletionRequests.Queries.GetNoteDeletionRequest;
using MedApp.Application.Pathways.Repositories;
using MedApp.Domain.Activity;
using MedApp.Domain.Patients;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.UnitTests.Application.Pathways.NoteDeletionRequests.Queries.GetNoteDeletionRequest;

[TestFixture]
public sealed class GetNoteDeletionRequestHandlerTests : PathwayHandlerTestBase
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

    private PathwayNoteDeletionRequest Given(PathwayNoteDeletionRequest request)
    {
        _requests.Setup(r => r.GetByIdAsync(request.Id, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        return request;
    }

    [Test]
    public async Task Handle_Pending_ReturnsTheNotePatientAndPathwayAndRecordsAViewOfThePathway()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));
        var note = pathway.AddNote(OtherUserId);
        var request = Given(pathway.RequestDeletion(note));

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
            PathwayId = pathway.Id,
            PathwayTitle = pathway.Title,
            Patient = new
            {
                pathway.Patient.Id,
                pathway.Patient.FirstName,
                pathway.Patient.LastName,
                pathway.Patient.DateOfBirth,
                pathway.Patient.NhsNumber
            },
            Note = new { note.Id, Text = NoteText, AuthorUsername = OtherUsername, note.CreatedAtUtc, DeletedAtUtc = (DateTime?)null },
            CanDecide = true,
            WhyCantDecide = (string?)null
        });
        _activity.Views.Should().ContainSingle().Which.Should().BeEquivalentTo((ActivityAction.PathwayViewed, new List<Guid> { pathway.PatientId }, (Guid?)pathway.Id));
    }

    [Test]
    public async Task Handle_PatientRestricted_CantBeDecidedAndSaysWhy()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        patient.RestrictedAtUtc = Now.AddDays(-1);
        var pathway = GivenPathway(CreatePathway(Status.InProgress, patient: patient));
        var request = Given(pathway.RequestDeletion(pathway.AddNote()));

        var response = await Handler.Handle(new GetNoteDeletionRequestQuery(request.Id), CancellationToken.None);

        response.CanDecide.Should().BeFalse();
        response.WhyCantDecide.Should().Be(PatientRules.RestrictedMessage);
    }

    [Test]
    public async Task Handle_PathwayDeleted_HasNoNoteAndRecordsAViewOfThePatient()
    {
        var pathway = CreatePathway(Status.Cancelled);
        var request = pathway.RequestDeletion(pathway.AddNote());
        request.Close(NoteDeletionRequestStatus.Closed, Admin.UserId, Admin.Username, Now);
        Given(request);
        _patients.Setup(r => r.GetByIdAsync(pathway.PatientId, It.IsAny<CancellationToken>())).ReturnsAsync(pathway.Patient);

        var response = await Handler.Handle(new GetNoteDeletionRequestQuery(request.Id), CancellationToken.None);

        response.Should().BeEquivalentTo(new
        {
            Status = "Closed",
            PathwayTitle = (string?)null,
            Note = (object?)null,
            CanDecide = false,
            Patient = new { pathway.Patient.Id }
        });
        _activity.Views.Should().ContainSingle().Which.Should().BeEquivalentTo((ActivityAction.PatientViewed, new List<Guid> { pathway.PatientId }, (Guid?)null));
    }

    [Test]
    public async Task Handle_PatientGoneToo_RecordsNothing()
    {
        var pathway = CreatePathway(Status.Cancelled);
        var request = Given(pathway.RequestDeletion(pathway.AddNote()));

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
        var pathway = GivenPathway(CreatePathway(Status.InProgress));
        var request = Given(pathway.RequestDeletion(pathway.AddNote()));
        using var cts = new CancellationTokenSource();

        await Handler.Handle(new GetNoteDeletionRequestQuery(request.Id), cts.Token);

        _requests.Verify(r => r.GetByIdAsync(request.Id, cts.Token), Times.Once);
        Repository.Verify(r => r.GetByIdAsync(pathway.Id, cts.Token), Times.Once);
        Identity.Verify(i => i.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), cts.Token), Times.Once);
        _activity.Tokens.Should().Equal(cts.Token);
    }
}
