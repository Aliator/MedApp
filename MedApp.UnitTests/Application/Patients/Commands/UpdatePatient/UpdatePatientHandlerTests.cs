using AutoFixture;
using FluentAssertions;
using MedApp.Application.Patients.Commands.UpdatePatient;
using MedApp.Application.Patients.Repositories;
using MedApp.Domain.Patients;
using MedApp.UnitTests.Common;
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
            TestConstants.Patients.ValidFirstName,
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
        var existingPatient = new Patient
        {
            Id = Guid.NewGuid(),
            FirstName = "OldFirst",
            LastName = "OldLast",
            DateOfBirth = new DateOnly(1980, 1, 1),
            Email = "old@email.com",
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            LastUpdated = DateTime.UtcNow.AddDays(-10)
        };

        _repository
            .Setup(r => r.GetByIdAsync(
                existingPatient.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPatient);

        var command = new UpdatePatientCommand(
            existingPatient.Id,
            TestConstants.Patients.ValidFirstName,
            null,
            null,
            TestConstants.Patients.ValidEmail
        );

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result!.FirstName.Should().Be(TestConstants.Patients.ValidFirstName);
        result.LastName.Should().Be("OldLast");
        result.DateOfBirth.Should().Be(existingPatient.DateOfBirth);
        result.Email.Should().Be(TestConstants.Patients.ValidEmail);

        _repository.Verify(
            r => r.UpdateAsync(
                It.Is<Patient>(p =>
                    p.FirstName == TestConstants.Patients.ValidFirstName &&
                    p.LastName == existingPatient.LastName &&
                    p.DateOfBirth == existingPatient.DateOfBirth &&
                    p.Email == TestConstants.Patients.ValidEmail
                ),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Handle_UpdatesAllFields_WhenProvided()
    {
        var existingPatient = new Patient
        {
            Id = Guid.NewGuid(),
            FirstName = "OldFirst",
            LastName = "OldLast",
            DateOfBirth = new DateOnly(1980, 1, 1),
            Email = "old@email.com",
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            LastUpdated = DateTime.UtcNow.AddDays(-10)
        };

        _repository
            .Setup(r => r.GetByIdAsync(
                existingPatient.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPatient);

        var command = new UpdatePatientCommand(
            existingPatient.Id,
            TestConstants.Patients.ValidFirstName,
            TestConstants.Patients.ValidLastName,
            TestConstants.Patients.ValidDateOfBirth,
            TestConstants.Patients.ValidEmail
        );

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result!.FirstName.Should().Be(TestConstants.Patients.ValidFirstName);
        result.LastName.Should().Be(TestConstants.Patients.ValidLastName);
        result.DateOfBirth.Should().Be(TestConstants.Patients.ValidDateOfBirth);
        result.Email.Should().Be(TestConstants.Patients.ValidEmail);

        _repository.Verify(
            r => r.UpdateAsync(
                It.Is<Patient>(p =>
                    p.FirstName == TestConstants.Patients.ValidFirstName &&
                    p.LastName == TestConstants.Patients.ValidLastName &&
                    p.DateOfBirth == TestConstants.Patients.ValidDateOfBirth &&
                    p.Email == TestConstants.Patients.ValidEmail
                ),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Handle_PassesCancellationToken_ToRepository()
    {
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            FirstName = "OldFirst",
            LastName = "OldLast",
            DateOfBirth = new DateOnly(1980, 1, 1),
            Email = "old@email.com",
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            LastUpdated = DateTime.UtcNow.AddDays(-5)
        };

        _repository
            .Setup(r => r.GetByIdAsync(
                patient.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(patient);

        var command = new UpdatePatientCommand(
            patient.Id,
            TestConstants.Patients.ValidFirstName,
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
