using AutoFixture;
using FluentAssertions;
using MedApp.Application.Patients.Queries.GetPatientById;
using MedApp.Application.Patients.Repositories;
using MedApp.Domain.Patients;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;

namespace MedApp.UnitTests.Application.Patients.Queries.GetPatientById;

[TestFixture]
public sealed class GetPatientByIdHandlerTests
{
    private IFixture _fixture = null!;
    private Mock<IPatientRepository> _repository = null!;
    private GetPatientByIdHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();

        _repository = new Mock<IPatientRepository>();
        _fixture.Inject<IPatientRepository>(_repository.Object);

        _handler = _fixture.Create<GetPatientByIdHandler>();
    }

    [Test]
    public async Task Handle_PatientExists_ReturnsPatient()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        
        _repository
            .Setup(r => r.GetByIdAsync(
                patient.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(patient);

        var query = new GetPatientByIdQuery(patient.Id);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().BeSameAs(patient);
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

        var query = new GetPatientByIdQuery(id);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().BeNull();
    }

    [Test]
    public async Task Handle_CallsRepository_Once()
    {
        var id = Guid.NewGuid();

        _repository
            .Setup(r => r.GetByIdAsync(
                id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Patient?)null);

        var query = new GetPatientByIdQuery(id);

        await _handler.Handle(query, CancellationToken.None);

        _repository.Verify(
            r => r.GetByIdAsync(
                id,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Handle_PassesCancellationToken_ToRepository()
    {
        var id = Guid.NewGuid();

        _repository
            .Setup(r => r.GetByIdAsync(
                id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Patient?)null);

        var query = new GetPatientByIdQuery(id);
        using var cts = new CancellationTokenSource();

        await _handler.Handle(query, cts.Token);

        _repository.Verify(
            r => r.GetByIdAsync(
                id,
                cts.Token),
            Times.Once);
    }
}
