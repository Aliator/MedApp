using AutoFixture;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Patients.Queries.GetPatientById;
using MedApp.Application.Patients.Repositories;
using MedApp.Domain.Activity;
using MedApp.Domain.Patients;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;

namespace MedApp.UnitTests.Application.Patients.Queries.GetPatientById;

/// <summary>Opening a patient's record is recorded in the activity log.</summary>
[TestFixture]
public sealed class GetPatientByIdHandlerActivityTests
{
    private IFixture _fixture = null!;
    private Mock<IPatientRepository> _repository = null!;
    private RecordingActivityLog _activity = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        _repository = new Mock<IPatientRepository>();
        _activity = new RecordingActivityLog();
        _fixture.Inject(_repository.Object);
        _fixture.Inject<IActivityLog>(_activity);
    }

    [Test]
    public async Task Handle_Found_RecordsAView_WithTheToken()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        _repository.Setup(r => r.GetByIdAsync(patient.Id, It.IsAny<CancellationToken>())).ReturnsAsync(patient);
        using var cts = new CancellationTokenSource();

        await _fixture.Create<GetPatientByIdHandler>().Handle(new GetPatientByIdQuery(patient.Id), cts.Token);

        _activity.Views.Should().ContainSingle().Which.Should().BeEquivalentTo(
            (ActivityAction.PatientViewed, new List<Guid> { patient.Id }, (Guid?)null));
        _activity.Tokens.Should().Equal(cts.Token);
    }

    [Test]
    public async Task Handle_NotFound_RecordsNothing()
    {
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Patient?)null);

        var result = await _fixture.Create<GetPatientByIdHandler>().Handle(new GetPatientByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.Should().BeNull();
        _activity.Views.Should().BeEmpty();
    }
}
