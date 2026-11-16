using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Procedures.Repositories;
using MedApp.Application.Procedures.Timeline.Queries.GetPatientTimelineOverview;
using MedApp.Domain.Patients;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using static MedApp.UnitTests.Common.Constants.TimelineTestConstants;
using Action = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureAuditAction;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;

namespace MedApp.UnitTests.Application.Procedures.Timeline.Queries.GetPatientTimelineOverview;

[TestFixture]
public sealed class GetPatientTimelineOverviewHandlerTests
{
    private Mock<IPatientRepository> _patients = null!;
    private Mock<IPatientProcedureRepository> _procedures = null!;
    private Mock<IProcedureAuditRepository> _audit = null!;
    private GetPatientTimelineOverviewHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        var fixture = AutoFixtureFactory.Create();
        _patients = new Mock<IPatientRepository>();
        _procedures = new Mock<IPatientProcedureRepository>();
        _audit = new Mock<IProcedureAuditRepository>();
        _patients
            .Setup(r => r.GetByIdAsync(PatientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Patient { Id = PatientId });
        _audit
            .Setup(r => r.GetDeletionsAsync(PatientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        fixture.Inject(_patients.Object);
        fixture.Inject(_procedures.Object);
        fixture.Inject(_audit.Object);
        fixture.Inject<TimeProvider>(new FixedTimeProvider(Now));
        _handler = fixture.Create<GetPatientTimelineOverviewHandler>();
    }

    private void GivenProcedures(params PatientProcedure[] procedures) =>
        _procedures
            .Setup(r => r.GetByPatientAsync(PatientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(procedures);

    private static PatientProcedure Procedure(string title, Status status, int dueInDays, int createdDaysAgo = 10) =>
        CreateTimelineProcedure(Guid.NewGuid(), title, status, Now.AddDays(dueInDays), Now.AddDays(-createdDaysAgo));

    [Test]
    public async Task Handle_PatientDoesNotExist_ThrowsNotFound()
    {
        var act = () => _handler.Handle(new GetPatientTimelineOverviewQuery(OtherPatientId, true), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _procedures.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Handle_Procedures_ListsThoseNotClosedSoonestDueFirstWithOverdueFlagged()
    {
        GivenProcedures(
            Procedure("Later", Status.Assigned, 5),
            Procedure("Overdue", Status.InProgress, -2),
            Procedure("Blocked", Status.Blocked, 1),
            Procedure("Done", Status.Completed, -5),
            Procedure("Stopped", Status.Cancelled, 2),
            Procedure("Filed", Status.Archived, 3));

        var result = await _handler.Handle(new GetPatientTimelineOverviewQuery(PatientId, false), CancellationToken.None);

        result.Upcoming.Select(u => (u.Title, u.IsOverdue)).Should().Equal(("Overdue", true), ("Blocked", false), ("Later", false));
        result.OpenCount.Should().Be(3);
    }

    [Test]
    public async Task Handle_ManyDue_ListsTheFirstFewAndCountsThemAll()
    {
        GivenProcedures(Enumerable.Range(1, 8).Select(i => Procedure($"P{i}", Status.InProgress, i)).ToArray());

        var result = await _handler.Handle(new GetPatientTimelineOverviewQuery(PatientId, false), CancellationToken.None);

        result.Upcoming.Should().HaveCount(GetPatientTimelineOverviewHandler.UpcomingLimit);
        result.Upcoming.Select(u => u.Title).Should().Equal("P1", "P2", "P3", "P4", "P5");
        result.OpenCount.Should().Be(8);
    }

    [Test]
    public async Task Handle_Staff_ListsEveryProcedureThatExistsNewestFirstAndNoDeletedOnes()
    {
        GivenProcedures(Procedure("Old", Status.Completed, 1, createdDaysAgo: 30), Procedure("New", Status.Assigned, 1, createdDaysAgo: 1));

        var result = await _handler.Handle(new GetPatientTimelineOverviewQuery(PatientId, false), CancellationToken.None);

        result.Procedures.Select(p => (p.Title, p.IsDeleted)).Should().Equal(("New", false), ("Old", false));
        _audit.Verify(r => r.GetDeletionsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_Admin_AlsoListsDeletedProceduresOnceEachNewestFirst()
    {
        var existing = Procedure("Existing", Status.Assigned, 1);
        GivenProcedures(existing);
        var otherDeletedId = Guid.NewGuid();
        _audit
            .Setup(r => r.GetDeletionsAsync(PatientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                CreateEntry(Action.Deleted, Now.AddDays(-5), procedureId: otherDeletedId, procedureTitle: "Deleted earlier"),
                CreateEntry(Action.Deleted, Now.AddDays(-1), procedureId: DeletedProcedureId, procedureTitle: DeletedTitle),
                CreateEntry(Action.Deleted, Now.AddDays(-2), procedureId: DeletedProcedureId, procedureTitle: "Older title"),
                // A deletion recorded for a procedure that exists can't happen, but it isn't listed twice if it does.
                CreateEntry(Action.Deleted, Now, procedureId: existing.Id),
            ]);

        var result = await _handler.Handle(new GetPatientTimelineOverviewQuery(PatientId, true), CancellationToken.None);

        result.Procedures.Select(p => (p.Title, p.IsDeleted)).Should().Equal(
            ("Existing", false), (DeletedTitle, true), ("Deleted earlier", true));
    }

    [Test]
    public async Task Handle_PassesCancellationToken_ToRepositories()
    {
        GivenProcedures();
        using var cts = new CancellationTokenSource();

        await _handler.Handle(new GetPatientTimelineOverviewQuery(PatientId, true), cts.Token);

        _patients.Verify(r => r.GetByIdAsync(PatientId, cts.Token), Times.Once);
        _procedures.Verify(r => r.GetByPatientAsync(PatientId, cts.Token), Times.Once);
        _audit.Verify(r => r.GetDeletionsAsync(PatientId, cts.Token), Times.Once);
    }
}
