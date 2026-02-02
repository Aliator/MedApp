using AutoFixture;
using FluentAssertions;
using MedApp.Application.Patients.Queries.GetAllPatients;
using MedApp.Application.Patients.Repositories;
using MedApp.Domain.Patients;
using MedApp.UnitTests.Common.Fixtures;
using Moq;

namespace MedApp.UnitTests.Application.Patients.Queries.GetAllPatients;

[TestFixture]
public sealed class GetAllPatientsHandlerTests
{
    private IFixture _fixture = null!;
    private Mock<IPatientRepository> _repository = null!;
    private GetAllPatientsHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();

        _repository = new Mock<IPatientRepository>();
        _fixture.Inject<IPatientRepository>(_repository.Object);

        _handler = _fixture.Create<GetAllPatientsHandler>();
    }

    [Test]
    public async Task Handle_ReturnsPatients_FromRepository()
    {
        var patients = new[]
        {
            new Patient
            {
                Id = Guid.NewGuid(),
                FirstName = "John",
                LastName = "Smith",
                DateOfBirth = new DateOnly(1990, 1, 1),
                Email = "john@test.com",
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                LastUpdated = DateTime.UtcNow.AddDays(-5)
            },
            new Patient
            {
                Id = Guid.NewGuid(),
                FirstName = "Jane",
                LastName = "Doe",
                DateOfBirth = new DateOnly(1985, 6, 6),
                Email = "jane@test.com",
                CreatedAt = DateTime.UtcNow.AddDays(-8),
                LastUpdated = DateTime.UtcNow.AddDays(-3)
            }
        };

        _repository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(patients);

        var query = new GetAllPatientsQuery();

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().BeEquivalentTo(patients);
    }

    [Test]
    public async Task Handle_CallsRepository_Once()
    {
        _repository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Patient>());

        var query = new GetAllPatientsQuery();

        await _handler.Handle(query, CancellationToken.None);

        _repository.Verify(
            r => r.GetAllAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Handle_PassesCancellationToken_ToRepository()
    {
        _repository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Patient>());

        var query = new GetAllPatientsQuery();
        using var cts = new CancellationTokenSource();

        await _handler.Handle(query, cts.Token);

        _repository.Verify(
            r => r.GetAllAsync(cts.Token),
            Times.Once);
    }
}
