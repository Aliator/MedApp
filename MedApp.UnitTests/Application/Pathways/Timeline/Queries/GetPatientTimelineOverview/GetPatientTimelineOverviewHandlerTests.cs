using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Pathways.Timeline.Queries.GetPatientTimelineOverview;
using MedApp.Domain.Patients;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using static MedApp.UnitTests.Common.Constants.TimelineTestConstants;
using Action = MedApp.Domain.Pathways.PatientPathways.PatientPathwayAuditAction;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.UnitTests.Application.Pathways.Timeline.Queries.GetPatientTimelineOverview;

[TestFixture]
public sealed class GetPatientTimelineOverviewHandlerTests
{
    private Mock<IPatientRepository> _patients = null!;
    private Mock<IPatientPathwayRepository> _pathways = null!;
    private Mock<IPathwayAuditRepository> _audit = null!;
    private GetPatientTimelineOverviewHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        var fixture = AutoFixtureFactory.Create();
        _patients = new Mock<IPatientRepository>();
        _pathways = new Mock<IPatientPathwayRepository>();
        _audit = new Mock<IPathwayAuditRepository>();
        _patients
            .Setup(r => r.GetByIdAsync(PatientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Patient { Id = PatientId });
        _audit
            .Setup(r => r.GetDeletionsAsync(PatientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        fixture.Inject(_patients.Object);
        fixture.Inject(_pathways.Object);
        fixture.Inject(_audit.Object);
        fixture.Inject(EmptyReferrals.Repository());
        fixture.Inject<TimeProvider>(new FixedTimeProvider(Now));
        _handler = fixture.Create<GetPatientTimelineOverviewHandler>();
    }

    private void GivenPathways(params PatientPathway[] pathways) =>
        _pathways
            .Setup(r => r.GetByPatientAsync(PatientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pathways);

    private static PatientPathway Pathway(string title, Status status, int dueInDays, int createdDaysAgo = 10) =>
        CreateTimelinePathway(Guid.NewGuid(), title, status, Now.AddDays(dueInDays), Now.AddDays(-createdDaysAgo));

    [Test]
    public async Task Handle_PatientDoesNotExist_ThrowsNotFound()
    {
        var act = () => _handler.Handle(new GetPatientTimelineOverviewQuery(OtherPatientId, true), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _pathways.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Handle_Pathways_ListsThoseNotClosedSoonestDueFirstWithOverdueFlagged()
    {
        GivenPathways(
            Pathway("Later", Status.Assigned, 5),
            Pathway("Overdue", Status.InProgress, -2),
            Pathway("Blocked", Status.Blocked, 1),
            Pathway("Done", Status.Completed, -5),
            Pathway("Stopped", Status.Cancelled, 2),
            Pathway("Filed", Status.Archived, 3));

        var result = await _handler.Handle(new GetPatientTimelineOverviewQuery(PatientId, false), CancellationToken.None);

        result.Upcoming.Select(u => (u.Title, u.IsOverdue)).Should().Equal(("Overdue", true), ("Blocked", false), ("Later", false));
        result.OpenCount.Should().Be(3);
    }

    [Test]
    public async Task Handle_ManyDue_ListsTheFirstFewAndCountsThemAll()
    {
        GivenPathways(Enumerable.Range(1, 8).Select(i => Pathway($"P{i}", Status.InProgress, i)).ToArray());

        var result = await _handler.Handle(new GetPatientTimelineOverviewQuery(PatientId, false), CancellationToken.None);

        result.Upcoming.Should().HaveCount(GetPatientTimelineOverviewHandler.UpcomingLimit);
        result.Upcoming.Select(u => u.Title).Should().Equal("P1", "P2", "P3", "P4", "P5");
        result.OpenCount.Should().Be(8);
    }

    [Test]
    public async Task Handle_Staff_ListsEveryPathwayThatExistsNewestFirstAndNoDeletedOnes()
    {
        GivenPathways(Pathway("Old", Status.Completed, 1, createdDaysAgo: 30), Pathway("New", Status.Assigned, 1, createdDaysAgo: 1));

        var result = await _handler.Handle(new GetPatientTimelineOverviewQuery(PatientId, false), CancellationToken.None);

        result.Pathways.Select(p => (p.Title, p.IsDeleted)).Should().Equal(("New", false), ("Old", false));
        _audit.Verify(r => r.GetDeletionsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_Admin_AlsoListsDeletedPathwaysOnceEachNewestFirst()
    {
        var existing = Pathway("Existing", Status.Assigned, 1);
        GivenPathways(existing);
        var otherDeletedId = Guid.NewGuid();
        _audit
            .Setup(r => r.GetDeletionsAsync(PatientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                CreateEntry(Action.Deleted, Now.AddDays(-5), pathwayId: otherDeletedId, pathwayTitle: "Deleted earlier"),
                CreateEntry(Action.Deleted, Now.AddDays(-1), pathwayId: DeletedPathwayId, pathwayTitle: DeletedTitle),
                CreateEntry(Action.Deleted, Now.AddDays(-2), pathwayId: DeletedPathwayId, pathwayTitle: "Older title"),
                // A deletion recorded for a pathway that exists can't happen, but it isn't listed twice if it does.
                CreateEntry(Action.Deleted, Now, pathwayId: existing.Id),
            ]);

        var result = await _handler.Handle(new GetPatientTimelineOverviewQuery(PatientId, true), CancellationToken.None);

        result.Pathways.Select(p => (p.Title, p.IsDeleted)).Should().Equal(
            ("Existing", false), (DeletedTitle, true), ("Deleted earlier", true));
    }

    [Test]
    public async Task Handle_PassesCancellationToken_ToRepositories()
    {
        GivenPathways();
        using var cts = new CancellationTokenSource();

        await _handler.Handle(new GetPatientTimelineOverviewQuery(PatientId, true), cts.Token);

        _patients.Verify(r => r.GetByIdAsync(PatientId, cts.Token), Times.Once);
        _pathways.Verify(r => r.GetByPatientAsync(PatientId, cts.Token), Times.Once);
        _audit.Verify(r => r.GetDeletionsAsync(PatientId, cts.Token), Times.Once);
    }
}
