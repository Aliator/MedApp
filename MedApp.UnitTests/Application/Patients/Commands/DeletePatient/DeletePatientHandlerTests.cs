using AutoFixture;
using MedApp.Domain.Activity;
using FluentAssertions;
using MedApp.Application.Attachments.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Commands.DeletePatient;
using MedApp.Application.Patients.Repositories;
using MedApp.Domain.Patients;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;

namespace MedApp.UnitTests.Application.Patients.Commands.DeletePatient;

[TestFixture]
public sealed class DeletePatientHandlerTests
{
    private IFixture _fixture = null!;
    private Mock<IPatientRepository> _repository = null!;
    private Mock<IAttachmentRepository> _attachments = null!;
    private DeletePatientHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();

        _repository = new Mock<IPatientRepository>();
        _fixture.Inject<IPatientRepository>(_repository.Object);

        _attachments = new Mock<IAttachmentRepository>();
        _fixture.Inject(_attachments.Object);

        _handler = _fixture.Create<DeletePatientHandler>();
    }


    [Test]
    public async Task Handle_PatientDoesNotExist_ReturnsFalse()
    {
        _repository
            .Setup(r => r.GetByIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Patient?)null);

        var command = new DeletePatientCommand(Guid.NewGuid());

        var result = await _handler.Handle(
            command,
            CancellationToken.None);

        result.Should().BeFalse();

        _repository.Verify(
            r => r.DeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<ActivityEntry>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task Handle_PatientExists_DeletesPatient_AndReturnsTrue()
    {
        var patient = PatientsTestConstants.CreateValidPatient();

        _repository
            .Setup(r => r.GetByIdAsync(
                patient.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(patient);

        var command = new DeletePatientCommand(patient.Id);

        var result = await _handler.Handle(
            command,
            CancellationToken.None);

        result.Should().BeTrue();

        _repository.Verify(
            r => r.DeleteAsync(
                patient.Id,
                It.IsAny<ActivityEntry>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Handle_PatientHasProcedures_ThrowsConflictAndKeepsThePatient()
    {
        var patient = PatientsTestConstants.CreateValidPatient();

        _repository
            .Setup(r => r.GetByIdAsync(patient.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(patient);
        _repository
            .Setup(r => r.CountProceduresAsync(patient.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var act = () => _handler.Handle(new DeletePatientCommand(patient.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("This patient has 2 procedures*");

        _repository.Verify(
            r => r.DeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<ActivityEntry>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task Handle_PatientHasOneProcedure_ThrowsConflictWithASingularMessage()
    {
        var patient = PatientsTestConstants.CreateValidPatient();

        _repository
            .Setup(r => r.GetByIdAsync(patient.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(patient);
        _repository
            .Setup(r => r.CountProceduresAsync(patient.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var act = () => _handler.Handle(new DeletePatientCommand(patient.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("This patient has 1 procedure, so they can't be deleted.");

        _repository.Verify(
            r => r.DeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<ActivityEntry>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestCase(1, "This patient has 1 file attached, so they can't be deleted. Delete the files first.")]
    [TestCase(2, "This patient has 2 files attached*")]
    public async Task Handle_PatientHasFiles_ThrowsConflictAndKeepsThePatient(int files, string message)
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        _repository
            .Setup(r => r.GetByIdAsync(patient.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(patient);
        _attachments
            .Setup(a => a.CountForPatientAsync(patient.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(files);

        var act = () => _handler.Handle(new DeletePatientCommand(patient.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(message);
        _repository.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<ActivityEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_PassesCancellationToken_ToRepository()
    {
        var patient = PatientsTestConstants.CreateValidPatient();

        _repository
            .Setup(r => r.GetByIdAsync(
                patient.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(patient);

        var command = new DeletePatientCommand(patient.Id);

        using var cts = new CancellationTokenSource();

        await _handler.Handle(
            command,
            cts.Token);

        _repository.Verify(
            r => r.DeleteAsync(
                patient.Id,
                It.IsAny<ActivityEntry>(),
                cts.Token),
            Times.Once);
        _attachments.Verify(a => a.CountForPatientAsync(patient.Id, cts.Token), Times.Once);
    }
}
