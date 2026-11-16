using AutoFixture;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Activity.Queries.SearchActivity;
using MedApp.Application.Activity.Repositories;
using MedApp.Application.Common.Paging;
using MedApp.Application.Patients.Repositories;
using MedApp.Domain.Activity;
using MedApp.UnitTests.Common.Fixtures;
using Moq;

namespace MedApp.UnitTests.Application.Activity.Queries.SearchActivity;

[TestFixture]
public sealed class SearchActivityHandlerTests
{
    private IFixture _fixture = null!;
    private Mock<IActivityRepository> _repository = null!;
    private Mock<IPatientRepository> _patients = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        _repository = new Mock<IActivityRepository>();
        _patients = new Mock<IPatientRepository>();
        _fixture.Inject(_repository.Object);
        _fixture.Inject(_patients.Object);
    }

    private static SearchActivityQuery Query() => new(2, 5, false, null, null, null, null, null);

    [Test]
    public async Task Handle_EntriesAboutPatients_NamesThem()
    {
        var patientId = Guid.NewGuid();
        var deletedId = Guid.NewGuid();
        var entries = new List<ActivityEntry>
        {
            new() { Id = Guid.NewGuid(), Action = ActivityAction.PatientViewed, PatientId = patientId },
            new() { Id = Guid.NewGuid(), Action = ActivityAction.PatientViewed, PatientId = patientId },
            new() { Id = Guid.NewGuid(), Action = ActivityAction.PatientDeleted, PatientId = deletedId },
            new() { Id = Guid.NewGuid(), Action = ActivityAction.SignedIn },
        };
        using var cts = new CancellationTokenSource();
        var query = Query();
        _repository.Setup(r => r.SearchAsync(query, cts.Token)).ReturnsAsync(new PagedResult<ActivityEntry>(entries, 12));
        _patients
            .Setup(p => p.GetNamesAsync(It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 2 && ids.Contains(patientId) && ids.Contains(deletedId)), cts.Token))
            .ReturnsAsync(new Dictionary<Guid, string> { [patientId] = "Test Patient" });

        var response = await _fixture.Create<SearchActivityHandler>().Handle(query, cts.Token);

        response.Page.Should().Be(2);
        response.PageSize.Should().Be(5);
        response.TotalCount.Should().Be(12);
        response.TotalPages.Should().Be(3);
        response.Items.Select(i => i.PatientName).Should().Equal("Test Patient", "Test Patient", ActivityEntryMapper.DeletedPatient, null);
    }

    [Test]
    public async Task Handle_NoPatients_DoesNotLookUpNames()
    {
        var query = Query();
        _repository
            .Setup(r => r.SearchAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<ActivityEntry>([new ActivityEntry { Action = ActivityAction.SignedIn }], 1));

        var response = await _fixture.Create<SearchActivityHandler>().Handle(query, CancellationToken.None);

        response.Items.Should().ContainSingle().Which.Action.Should().Be("SignedIn");
        _patients.Verify(p => p.GetNamesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
