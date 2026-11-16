using AutoFixture;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Paging;
using MedApp.Application.Patients.Queries.SearchPatients;
using MedApp.Application.Patients.Repositories;
using MedApp.Domain.Activity;
using MedApp.Domain.Patients;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;

namespace MedApp.UnitTests.Application.Patients.Queries.SearchPatients;

/// <summary>What a patient search records in the activity log: who was seen, and which filters (never the terms).</summary>
[TestFixture]
public sealed class SearchPatientsHandlerActivityTests
{
    private IFixture _fixture = null!;
    private RecordingActivityLog _activity = null!;
    private Patient[] _patients = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        _patients = [PatientsTestConstants.CreateValidPatient(), PatientsTestConstants.CreateValidPatient()];
        var repository = new Mock<IPatientRepository>();
        repository.Setup(r => r.SearchAsync(It.IsAny<SearchPatientsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Patient>(_patients, 23));
        _activity = new RecordingActivityLog();
        _fixture.Inject(repository.Object);
        _fixture.Inject<IActivityLog>(_activity);
    }

    [Test]
    public async Task Handle_NoFilters_RecordsOnlyWhoWasSeen()
    {
        using var cts = new CancellationTokenSource();

        await _fixture.Create<SearchPatientsHandler>().Handle(PatientsTestConstants.CreateSearchQuery(), cts.Token);

        _activity.Recorded.Should().BeEmpty();
        var view = _activity.Views.Should().ContainSingle().Subject;
        view.Action.Should().Be(ActivityAction.PatientListed);
        view.PatientIds.Should().Equal(_patients.Select(p => p.Id));
        _activity.Tokens.Should().Equal(cts.Token);
    }

    [Test]
    public async Task Handle_Filtered_RecordsTheFilterNamesAndTheTotal_NotTheTerms()
    {
        var query = PatientsTestConstants.CreateSearchQuery() with
        {
            LastName = "Syntheticsurname",
            DateOfBirthFrom = new DateOnly(1980, 1, 1)
        };

        await _fixture.Create<SearchPatientsHandler>().Handle(query, CancellationToken.None);

        var entry = _activity.Recorded.Should().ContainSingle().Subject;
        entry.Action.Should().Be(ActivityAction.PatientsSearched);
        entry.Details.Should().Be("""{"filters":["lastName","dateOfBirthFrom"],"resultCount":23}""");
        entry.Details.Should().NotContain("Syntheticsurname").And.NotContain("1980");
        _activity.Views.Should().ContainSingle();
    }
}
