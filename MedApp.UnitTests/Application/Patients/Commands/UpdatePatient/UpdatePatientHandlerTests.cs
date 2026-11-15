using AutoFixture;
using FluentAssertions;
using MedApp.Application.Patients.Commands.UpdatePatient;
using MedApp.Application.Patients.Repositories;
using MedApp.Domain.Patients;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;

namespace MedApp.UnitTests.Application.Patients.Commands.UpdatePatient;

[TestFixture]
public sealed class UpdatePatientHandlerTests
{
    private IFixture _fixture = null!;
    private Mock<IPatientRepository> _repository = null!;
    private UpdatePatientHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();

        _repository = new Mock<IPatientRepository>();
        _fixture.Inject<IPatientRepository>(_repository.Object);

        _handler = _fixture.Create<UpdatePatientHandler>();
    }

    [Test]
    public async Task Handle_PatientDoesNotExist_ReturnsNull()
    {
        var id = Guid.NewGuid();

        _repository
            .Setup(r => r.GetByIdAsync(
                id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Patient?)null);

        var command = new UpdatePatientCommand(
            id,
            PatientsTestConstants.ValidFirstName,
            null,
            null,
            null
        );

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().BeNull();

        _repository.Verify(
            r => r.UpdateAsync(
                It.IsAny<Patient>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task Handle_UpdatesProvidedFields_Only()
    {
        var patient = PatientsTestConstants.CreateValidPatient();

        _repository
            .Setup(r => r.GetByIdAsync(
                patient.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(patient);

        var command = new UpdatePatientCommand(
            patient.Id,
            PatientsTestConstants.ValidFirstName,
            null,
            null,
            PatientsTestConstants.ValidEmail
        );

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result!.FirstName.Should().Be(PatientsTestConstants.ValidFirstName);
        result.LastName.Should().Be(patient.LastName);
        result.DateOfBirth.Should().Be(patient.DateOfBirth);
        result.Email.Should().Be(PatientsTestConstants.ValidEmail);

        _repository.Verify(
            r => r.UpdateAsync(
                It.Is<Patient>(p =>
                    p.FirstName == PatientsTestConstants.ValidFirstName &&
                    p.LastName == patient.LastName &&
                    p.DateOfBirth == patient.DateOfBirth &&
                    p.Email == PatientsTestConstants.ValidEmail
                ),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Handle_UpdatesAllFields_WhenProvided()
    {
        var patient = PatientsTestConstants.CreateValidPatient();

        _repository
            .Setup(r => r.GetByIdAsync(
                patient.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(patient);

        var command = new UpdatePatientCommand(
            patient.Id,
            PatientsTestConstants.ValidFirstName,
            PatientsTestConstants.ValidLastName,
            PatientsTestConstants.ValidDateOfBirth,
            PatientsTestConstants.ValidEmail
        );

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result!.FirstName.Should().Be(PatientsTestConstants.ValidFirstName);
        result.LastName.Should().Be(PatientsTestConstants.ValidLastName);
        result.DateOfBirth.Should().Be(PatientsTestConstants.ValidDateOfBirth);
        result.Email.Should().Be(PatientsTestConstants.ValidEmail);

        _repository.Verify(
            r => r.UpdateAsync(
                It.Is<Patient>(p =>
                    p.FirstName == PatientsTestConstants.ValidFirstName &&
                    p.LastName == PatientsTestConstants.ValidLastName &&
                    p.DateOfBirth == PatientsTestConstants.ValidDateOfBirth &&
                    p.Email == PatientsTestConstants.ValidEmail
                ),
                It.IsAny<CancellationToken>()),
            Times.Once);
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

        var command = new UpdatePatientCommand(
            patient.Id,
            PatientsTestConstants.ValidFirstName,
            null,
            null,
            null
        );

        using var cts = new CancellationTokenSource();

        await _handler.Handle(command, cts.Token);

        _repository.Verify(
            r => r.UpdateAsync(
                It.IsAny<Patient>(),
                cts.Token),
            Times.Once);
    }
}
