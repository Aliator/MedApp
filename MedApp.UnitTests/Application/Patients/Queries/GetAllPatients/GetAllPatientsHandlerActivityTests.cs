using AutoFixture;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Patients.Queries.GetAllPatients;
using MedApp.Application.Patients.Repositories;
using MedApp.Domain.Activity;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;

namespace MedApp.UnitTests.Application.Patients.Queries.GetAllPatients;

/// <summary>Loading every patient is recorded as one search for all of them.</summary>
[TestFixture]
public sealed class GetAllPatientsHandlerActivityTests
{
    [Test]
    public async Task Handle_RecordsOneSearchForAll_WithTheCount()
    {
        var fixture = AutoFixtureFactory.Create();
        var repository = new Mock<IPatientRepository>();
        repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([PatientsTestConstants.CreateValidPatient(), PatientsTestConstants.CreateValidPatient()]);
        var activity = new RecordingActivityLog();
        fixture.Inject(repository.Object);
        fixture.Inject<IActivityLog>(activity);
        using var cts = new CancellationTokenSource();

        await fixture.Create<GetAllPatientsHandler>().Handle(new GetAllPatientsQuery(), cts.Token);

        var entry = activity.Recorded.Should().ContainSingle().Subject;
        entry.Action.Should().Be(ActivityAction.PatientsSearched);
        entry.PatientId.Should().BeNull();
        entry.Details.Should().Be("""{"filters":["all"],"resultCount":2}""");
        activity.Tokens.Should().Equal(cts.Token);
        activity.Views.Should().BeEmpty();
    }
}
