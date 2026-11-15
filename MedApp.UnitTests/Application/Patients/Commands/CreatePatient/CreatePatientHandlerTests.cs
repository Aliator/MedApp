using AutoFixture;
using FluentAssertions;
using MedApp.Application.Patients.Commands.CreatePatient;
using MedApp.Application.Patients.Repositories;
using MedApp.Domain.Patients;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;

namespace MedApp.UnitTests.Application.Patients.Commands.CreatePatient;

[TestFixture]
public sealed class CreatePatientHandlerTests
{
    private IFixture _fixture = null!;
    private Mock<IPatientRepository> _repository = null!;
    private CreatePatientHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();

        _repository = new Mock<IPatientRepository>();
        _fixture.Inject<IPatientRepository>(_repository.Object);

        _handler = _fixture.Create<CreatePatientHandler>();
    }

    [Test]
    public async Task Handle_ValidCommand_ReturnsPatientResponse()
    {
        var command = new CreatePatientCommand(
            PatientsTestConstants.ValidFirstName,
            PatientsTestConstants.ValidLastName,
            PatientsTestConstants.ValidDateOfBirth,
            PatientsTestConstants.ValidEmail
        );

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Id.Should().NotBe(Guid.Empty);
        result.FirstName.Should().Be(PatientsTestConstants.ValidFirstName);
        result.LastName.Should().Be(PatientsTestConstants.ValidLastName);
        result.DateOfBirth.Should().Be(PatientsTestConstants.ValidDateOfBirth);
        result.Email.Should().Be(PatientsTestConstants.ValidEmail);
    }

    [Test]
    public async Task Handle_ValidCommand_PersistsPatient()
    {
        var command = new CreatePatientCommand(
            PatientsTestConstants.ValidFirstName,
            PatientsTestConstants.ValidLastName,
            PatientsTestConstants.ValidDateOfBirth,
            PatientsTestConstants.ValidEmail
        );

        await _handler.Handle(command, CancellationToken.None);

        _repository.Verify(
            r => r.AddAsync(
                It.Is<Patient>(p =>
                    p.Id != Guid.Empty &&
                    p.FirstName == PatientsTestConstants.ValidFirstName &&
                    p.LastName == PatientsTestConstants.ValidLastName &&
                    p.DateOfBirth == PatientsTestConstants.ValidDateOfBirth &&
                    p.Email == PatientsTestConstants.ValidEmail &&
                    p.CreatedAt <= DateTime.UtcNow &&
                    p.LastUpdated <= DateTime.UtcNow
                ),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Handle_PassesCancellationToken_ToRepository()
    {
        var command = new CreatePatientCommand(
            PatientsTestConstants.ValidFirstName,
            PatientsTestConstants.ValidLastName,
            PatientsTestConstants.ValidDateOfBirth,
            PatientsTestConstants.ValidEmail
        );

        using var cts = new CancellationTokenSource();

        await _handler.Handle(command, cts.Token);

        _repository.Verify(
            r => r.AddAsync(
                It.IsAny<Patient>(),
                cts.Token),
            Times.Once);
    }
}
