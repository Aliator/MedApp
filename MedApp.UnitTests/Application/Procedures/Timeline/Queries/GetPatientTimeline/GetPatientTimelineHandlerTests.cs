using AutoFixture;
using FluentAssertions;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Procedures.Repositories;
using MedApp.Application.Procedures.Timeline.Common;
using MedApp.Application.Procedures.Timeline.Queries.GetPatientTimeline;
using MedApp.Domain.Patients;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using static MedApp.UnitTests.Common.Constants.TimelineTestConstants;
using Action = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureAuditAction;

namespace MedApp.UnitTests.Application.Procedures.Timeline.Queries.GetPatientTimeline;

[TestFixture]
public sealed class GetPatientTimelineHandlerTests
{
    private const string NotesDetails = "{\"from\":\"\",\"to\":\"" + NotesText + "\"}";

    private Mock<IPatientRepository> _patients = null!;
    private Mock<IProcedureAuditRepository> _audit = null!;
    private Mock<IAppointmentRepository> _appointments = null!;
    private GetPatientTimelineHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        var fixture = AutoFixtureFactory.Create();
        _patients = new Mock<IPatientRepository>();
        _audit = new Mock<IProcedureAuditRepository>();
        _appointments = new Mock<IAppointmentRepository>();
        _appointments
            .Setup(r => r.GetPatientTimelineAsync(It.IsAny<GetPatientTimelineQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _patients
            .Setup(r => r.GetByIdAsync(PatientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Patient { Id = PatientId });
        fixture.Inject(_patients.Object);
        fixture.Inject(_audit.Object);
        fixture.Inject(_appointments.Object);
        _handler = fixture.Create<GetPatientTimelineHandler>();
    }

    private void GivenRows(params PatientTimelineRow[] rows) =>
        _audit
            .Setup(r => r.GetPatientTimelineAsync(It.IsAny<GetPatientTimelineQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

    [Test]
    public async Task Handle_PatientDoesNotExist_ThrowsNotFound()
    {
        var act = () => _handler.Handle(CreateTimelineQuery() with { PatientId = OtherPatientId }, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _audit.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Handle_NoMoreThanAPage_ReturnsThemWithNoNextCursor()
    {
        var entry = CreateEntry(Action.Created, Now);
        GivenRows(new PatientTimelineRow(entry, CurrentTitle));

        var result = await _handler.Handle(CreateTimelineQuery(), CancellationToken.None);

        result.Items.Should().ContainSingle().Which.ProcedureTitle.Should().Be(CurrentTitle);
        result.NextCursor.Should().BeNull();
    }

    [Test]
    public async Task Handle_MoreThanAPage_ReturnsAPageAndACursorAfterItsLastEntry()
    {
        var newest = CreateEntry(Action.StageFinished, Now);
        var older = CreateEntry(Action.StageStarted, Now.AddMinutes(-5));
        var oldest = CreateEntry(Action.Created, Now.AddDays(-1));
        GivenRows(new(newest, CurrentTitle), new(older, CurrentTitle), new(oldest, CurrentTitle));

        var result = await _handler.Handle(CreateTimelineQuery(pageSize: 2), CancellationToken.None);

        result.Items.Select(i => i.Id).Should().Equal(newest.Id, older.Id);
        result.NextCursor.Should().Be(new TimelineCursor(older.AtUtc, older.Id).Encode());
    }

    [Test]
    public async Task Handle_Staff_NeverGetDeletedProceduresOrFreeText()
    {
        GivenRows(
            new(CreateEntry(Action.StageNotesUpdated, Now, NotesDetails), CurrentTitle),
            new(CreateEntry(Action.Deleted, Now.AddDays(-1), procedureId: DeletedProcedureId), null));

        var result = await _handler.Handle(CreateTimelineQuery(isAdmin: false), CancellationToken.None);

        var item = result.Items.Should().ContainSingle().Subject;
        item.IsProcedureDeleted.Should().BeFalse();
        item.Changes.Should().ContainSingle().Which.To.Should().BeNull();
    }

    [Test]
    public async Task Handle_Admin_GetsDeletedProceduresAndFreeText()
    {
        GivenRows(
            new(CreateEntry(Action.StageNotesUpdated, Now, NotesDetails), CurrentTitle),
            new(CreateEntry(Action.Deleted, Now.AddDays(-1), procedureId: DeletedProcedureId, procedureTitle: DeletedTitle), null));

        var result = await _handler.Handle(CreateTimelineQuery(isAdmin: true), CancellationToken.None);

        result.Items.Should().HaveCount(2);
        result.Items[0].Changes.Should().ContainSingle().Which.To.Should().Be(NotesText);
        result.Items[1].IsProcedureDeleted.Should().BeTrue();
        result.Items[1].ProcedureTitle.Should().Be(DeletedTitle);
    }

    [Test]
    public async Task Handle_PassesTheQueryAndCancellationToken_ToRepositories()
    {
        GivenRows();
        var query = CreateTimelineQuery();
        using var cts = new CancellationTokenSource();

        await _handler.Handle(query, cts.Token);

        _patients.Verify(r => r.GetByIdAsync(PatientId, cts.Token), Times.Once);
        _audit.Verify(r => r.GetPatientTimelineAsync(query, cts.Token), Times.Once);
    }
}
