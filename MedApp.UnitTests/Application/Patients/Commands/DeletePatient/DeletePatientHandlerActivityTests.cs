using AutoFixture;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Attachments.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Commands.DeletePatient;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Domain.Activity;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;

namespace MedApp.UnitTests.Application.Patients.Commands.DeletePatient;

/// <summary>What deleting a patient records in the activity log, and when it's refused.</summary>
[TestFixture]
public sealed class DeletePatientHandlerActivityTests
{
    private IFixture _fixture = null!;
    private Mock<IPatientRepository> _repository = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        _repository = new Mock<IPatientRepository>();
        _repository.Setup(r => r.CountPathwaysAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
        _fixture.Inject(_repository.Object);
        _fixture.Inject<IActivityLog>(new RecordingActivityLog());
        _fixture.Inject(new Mock<IAttachmentRepository>().Object);
        _fixture.Inject(new Mock<MedApp.Application.Results.Repositories.IResultRepository>().Object);
    }

    [Test]
    public async Task Handle_Deleted_SavesAPatientDeletedEntry()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        _repository.Setup(r => r.GetByIdAsync(patient.Id, It.IsAny<CancellationToken>())).ReturnsAsync(patient);

        await _fixture.Create<DeletePatientHandler>().Handle(new DeletePatientCommand(patient.Id), CancellationToken.None);

        _repository.Verify(r => r.DeleteAsync(
            patient.Id,
            It.Is<ActivityEntry>(e => e.Action == ActivityAction.PatientDeleted && e.PatientId == patient.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_RestrictedRecord_IsRefused()
    {
        var patient = PrivacyTestConstants.CreateRestrictedPatient();
        _repository.Setup(r => r.GetByIdAsync(patient.Id, It.IsAny<CancellationToken>())).ReturnsAsync(patient);

        var act = () => _fixture.Create<DeletePatientHandler>().Handle(new DeletePatientCommand(patient.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PatientRules.RestrictedMessage);
        _repository.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<ActivityEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
