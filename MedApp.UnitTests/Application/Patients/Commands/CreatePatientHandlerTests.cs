using AutoFixture;
using FluentAssertions;
using MedApp.Application.Patients.Commands.CreatePatient;
using MedApp.Application.Patients.Repositories;
using MedApp.Domain.Patients;
using MedApp.UnitTests.Common;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using NUnit.Framework;

namespace MedApp.UnitTests.Application.Patients.Commands;

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
        _repository = _fixture.Freeze<Mock<IPatientRepository>>();
        _handler = _fixture.Create<CreatePatientHandler>();
    }

    [Test]
    public async Task Handle_ValidCommand_ReturnsPatientResponse()
    {
        var command = new CreatePatientCommand(
            TestConstants.Patients.ValidFirstName,
            TestConstants.Patients.ValidLastName,
            TestConstants.Patients.ValidDateOfBirth,
            TestConstants.Patients.ValidEmail
        );

        var result = await _handler.Handle(
            command,
            CancellationToken.None);

        result.Should().NotBeNull();
        result.FirstName.Should().Be(TestConstants.Patients.ValidFirstName);
        result.LastName.Should().Be(TestConstants.Patients.ValidLastName);
        result.DateOfBirth.Should().Be(TestConstants.Patients.ValidDateOfBirth);
        result.Email.Should().Be(TestConstants.Patients.ValidEmail);
        result.Id.Should().NotBe(Guid.Empty);
    }

    [Test]
    public async Task Handle_ValidCommand_PersistsPatient()
    {
        var command = new CreatePatientCommand(
            TestConstants.Patients.ValidFirstName,
            TestConstants.Patients.ValidLastName,
            TestConstants.Patients.ValidDateOfBirth,
            TestConstants.Patients.ValidEmail
        );

        await _handler.Handle(
            command,
            CancellationToken.None);

        _repository.Verify(
            r => r.AddAsync(
                It.Is<Patient>(p =>
                    p.FirstName == TestConstants.Patients.ValidFirstName &&
                    p.LastName == TestConstants.Patients.ValidLastName &&
                    p.DateOfBirth == TestConstants.Patients.ValidDateOfBirth &&
                    p.Email == TestConstants.Patients.ValidEmail &&
                    p.Id != Guid.Empty &&
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
            TestConstants.Patients.ValidFirstName,
            TestConstants.Patients.ValidLastName,
            TestConstants.Patients.ValidDateOfBirth,
            TestConstants.Patients.ValidEmail
        );

        using var cts = new CancellationTokenSource();

        await _handler.Handle(
            command,
            cts.Token);

        _repository.Verify(
            r => r.AddAsync(
                It.IsAny<Patient>(),
                cts.Token),
            Times.Once);
    }
}
