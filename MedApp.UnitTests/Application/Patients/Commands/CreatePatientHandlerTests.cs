using AutoFixture;
using FluentAssertions;
using MedApp.Application.Patients.Commands.CreatePatient;
using MedApp.Application.Patients.Repositories;
using MedApp.Domain.Patients;
using MedApp.UnitTests.Common;
using MedApp.UnitTests.Common.Fixtures;
using Moq;

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
    public async Task Handle_ValidCommand_CreatesPatient()
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

        result.Email.Should().Be(
            TestConstants.Patients.ValidEmail);

        _repository.Verify(
            r => r.AddAsync(
                It.IsAny<Patient>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}