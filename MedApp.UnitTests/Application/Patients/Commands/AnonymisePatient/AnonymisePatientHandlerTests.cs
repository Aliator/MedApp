using AutoFixture;
using FluentAssertions;
using FluentValidation;
using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Commands.AnonymisePatient;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Domain.Activity;
using MedApp.Domain.Appointments;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.UnitTests.Application.Patients.Commands.AnonymisePatient;

[TestFixture]
public sealed class AnonymisePatientHandlerTests
{
    private const string FullName = $"{PatientsTestConstants.ValidFirstName} {PatientsTestConstants.ValidLastName}";

    private IFixture _fixture = null!;
    private Mock<IPatientRecordRepository> _repository = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        _repository = new Mock<IPatientRecordRepository>();
        _fixture.Inject(_repository.Object);
        _fixture.Inject<IActivityLog>(new RecordingActivityLog());
        _fixture.Inject<TimeProvider>(new FixedTimeProvider(ActivityTestConstants.Now));
    }

    private AnonymisePatientHandler Handler => _fixture.Create<AnonymisePatientHandler>();

    private PatientRecord Given(PatientRecord record)
    {
        _repository.Setup(r => r.GetAsync(record.Patient.Id, true, It.IsAny<CancellationToken>())).ReturnsAsync(record);
        return record;
    }

    private static AnonymisePatientCommand Command(Guid id, string confirmation = FullName, string? note = "  Reviewed by the records manager  ") =>
        new(id, "retentionPeriodEnded", note, confirmation);

    [Test]
    public async Task Handle_UnknownPatient_ThrowsNotFound()
    {
        var act = () => Handler.Handle(Command(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_AlreadyAnonymised_ThrowsConflict()
    {
        var record = Given(PrivacyTestConstants.CreateRecord(PrivacyTestConstants.CreateAnonymisedPatient()));

        var act = () => Handler.Handle(Command(record.Patient.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*already been anonymised*");
    }

    [Test]
    public async Task Handle_Restricted_ThrowsConflict()
    {
        var record = Given(PrivacyTestConstants.CreateRecord(PrivacyTestConstants.CreateRestrictedPatient()));

        var act = () => Handler.Handle(Command(record.Patient.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*Lift the restriction*");
    }

    [TestCase(Status.InProgress, "1 pathway under way")]
    [TestCase(Status.Blocked, "1 pathway under way")]
    public async Task Handle_PathwayUnderWay_ThrowsConflict(Status status, string expected)
    {
        var record = Given(PrivacyTestConstants.CreateRecord(null, CreatePathway(status), CreatePathway(Status.Completed)));

        var act = () => Handler.Handle(Command(record.Patient.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"*{expected}*");
        _repository.Verify(r => r.SaveAsync(It.IsAny<ActivityEntry>(), It.IsAny<IReadOnlyCollection<PatientPathwayAuditEntry>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_SeveralUnderWay_SaysHowMany()
    {
        var record = Given(PrivacyTestConstants.CreateRecord(null, CreatePathway(Status.Assigned), CreatePathway(Status.InReview)));

        var act = () => Handler.Handle(Command(record.Patient.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*2 pathways under way*");
    }

    [TestCase(AppointmentStatus.Requested)]
    [TestCase(AppointmentStatus.Booked)]
    public async Task Handle_OpenAppointment_ThrowsConflict(AppointmentStatus status)
    {
        var record = Given(PrivacyTestConstants.CreateRecord(null, CreatePathway(Status.Completed)));
        record.Appointments[0].Status = status;

        var act = () => Handler.Handle(Command(record.Patient.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("This patient has 1 open appointment (needing booking or booked), so their record can't be anonymised. " +
                         "Cancel it, or record its outcome, first.");
        record.Patient.FirstName.Should().Be(PatientsTestConstants.ValidFirstName);
        _repository.Verify(r => r.SaveAsync(It.IsAny<ActivityEntry>(), It.IsAny<IReadOnlyCollection<PatientPathwayAuditEntry>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_SeveralOpenAppointments_SaysHowMany()
    {
        var record = PrivacyTestConstants.CreateRecord(null, CreatePathway(Status.Completed));
        record.Appointments[0].Status = AppointmentStatus.Booked;
        Given(new PatientRecord
        {
            Patient = record.Patient,
            Pathways = record.Pathways,
            PathwayHistory = record.PathwayHistory,
            Activity = record.Activity,
            Appointments =
            [
                .. record.Appointments,
                new Appointment { Id = Guid.NewGuid(), PatientId = record.Patient.Id, Status = AppointmentStatus.Requested }
            ],
            AppointmentHistory = record.AppointmentHistory,
            Notifications = record.Notifications,
            NoteVersions = [],
            NoteDeletionRequests = [],
            Attachments = []
        });

        var act = () => Handler.Handle(Command(record.Patient.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*2 open appointments*Cancel them, or record their outcome*");
    }

    [TestCase(AppointmentStatus.Attended)]
    [TestCase(AppointmentStatus.DidNotAttend)]
    [TestCase(AppointmentStatus.Cancelled)]
    public async Task Handle_OnlyClosedAppointments_Anonymises(AppointmentStatus status)
    {
        var record = Given(PrivacyTestConstants.CreateRecord(null, CreatePathway(Status.Completed)));
        record.Appointments[0].Status = status;

        var response = await Handler.Handle(Command(record.Patient.Id), CancellationToken.None);

        response.IsAnonymised.Should().BeTrue();
        _repository.Verify(r => r.SaveAsync(It.IsAny<ActivityEntry>(), It.IsAny<IReadOnlyCollection<PatientPathwayAuditEntry>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_WrongConfirmation_IsAValidationErrorOnConfirmation()
    {
        var record = Given(PrivacyTestConstants.CreateRecord());

        var act = () => Handler.Handle(Command(record.Patient.Id, "Someone Else"), CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationException>())
            .Which.Errors.Should().ContainSingle(e => e.PropertyName == nameof(AnonymisePatientCommand.Confirmation));
        record.Patient.FirstName.Should().Be(PatientsTestConstants.ValidFirstName);
    }

    [Test]
    public async Task Handle_Confirmed_IgnoringCaseAndSpaces_AnonymisesAndSavesWithTheReason()
    {
        var record = Given(PrivacyTestConstants.CreateRecord(null, CreatePathway(Status.Completed), CreatePathway(Status.Archived)));
        ActivityEntry? entry = null;
        _repository.Setup(r => r.SaveAsync(It.IsAny<ActivityEntry>(), It.IsAny<IReadOnlyCollection<PatientPathwayAuditEntry>>(), It.IsAny<CancellationToken>()))
            .Callback((ActivityEntry e, IReadOnlyCollection<PatientPathwayAuditEntry> _, CancellationToken _) => entry = e)
            .Returns(Task.CompletedTask);
        using var cts = new CancellationTokenSource();

        var response = await Handler.Handle(Command(record.Patient.Id, "  firstname    LASTNAME "), cts.Token);

        response.IsAnonymised.Should().BeTrue();
        response.FirstName.Should().Be(PatientAnonymiser.FirstName);
        record.Patient.AnonymisedAtUtc.Should().Be(ActivityTestConstants.Now);
        record.Pathways.Should().AllSatisfy(p => p.Title.Should().Be(TemplateName));
        entry!.Action.Should().Be(ActivityAction.PatientAnonymised);
        entry.PatientId.Should().Be(record.Patient.Id);
        entry.Details.Should().Be("""{"reason":"RetentionPeriodEnded","note":"Reviewed by the records manager","pathwayCount":2}""");
        _repository.Verify(r => r.GetAsync(record.Patient.Id, true, cts.Token), Times.Once);
        _repository.Verify(r => r.SaveAsync(It.IsAny<ActivityEntry>(), It.IsAny<IReadOnlyCollection<PatientPathwayAuditEntry>>(), cts.Token), Times.Once);
    }

    [TestCase(null)]
    [TestCase("   ")]
    public async Task Handle_NoNote_RecordsOnlyTheReasonByName(string? note)
    {
        var record = Given(PrivacyTestConstants.CreateRecord(null));
        ActivityEntry? entry = null;
        _repository.Setup(r => r.SaveAsync(It.IsAny<ActivityEntry>(), It.IsAny<IReadOnlyCollection<PatientPathwayAuditEntry>>(), It.IsAny<CancellationToken>()))
            .Callback((ActivityEntry e, IReadOnlyCollection<PatientPathwayAuditEntry> _, CancellationToken _) => entry = e)
            .Returns(Task.CompletedTask);

        await Handler.Handle(new AnonymisePatientCommand(record.Patient.Id, "CreatedInError", note, FullName), CancellationToken.None);

        entry!.Details.Should().Be("""{"reason":"CreatedInError","pathwayCount":0}""");
    }
}
