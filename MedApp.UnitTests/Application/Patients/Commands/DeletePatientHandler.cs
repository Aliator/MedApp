using AutoFixture;
using FluentAssertions;
using MedApp.Application.Patients.Commands.DeletePatient;
using MedApp.Application.Patients.Repositories;
using MedApp.Domain.Patients;
using MedApp.UnitTests.Common.Fixtures;
using Moq;

namespace MedApp.UnitTests.Application.Patients.Commands;

[TestFixture]
public sealed class DeletePatientHandlerTests
{
    private IFixture _fixture = null!;
    private Mock<IPatientRepository> _repository = null!;
    private DeletePatientHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();

        _repository = new Mock<IPatientRepository>();
        _fixture.Inject<IPatientRepository>(_repository.Object);

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
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task Handle_PatientExists_DeletesPatient_AndReturnsTrue()
    {
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            FirstName = "John",
            LastName = "Smith",
            DateOfBirth = new DateOnly(1990, 1, 1),
            Email = "john.smith@test.com",
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            LastUpdated = DateTime.UtcNow.AddDays(-5)
        };

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
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Handle_PassesCancellationToken_ToRepository()
    {
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            FirstName = "John",
            LastName = "Smith",
            DateOfBirth = new DateOnly(1990, 1, 1),
            Email = "john.smith@test.com",
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            LastUpdated = DateTime.UtcNow.AddDays(-5)
        };

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
                cts.Token),
            Times.Once);
    }
}
