using AutoFixture;
using FluentAssertions;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.Repositories;
using MedApp.Application.Procedures.Timeline.Common;
using MedApp.Application.Procedures.Timeline.Queries.GetPatientTimeline;
using MedApp.Domain.Patients;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using static MedApp.UnitTests.Common.Constants.TimelineTestConstants;
using Action = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureAuditAction;
using MedApp.Domain.Appointments;

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

    private void GivenAppointmentRows(params AppointmentTimelineRow[] rows) =>
        _appointments
            .Setup(r => r.GetPatientTimelineAsync(It.IsAny<GetPatientTimelineQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

    private static AppointmentTimelineRow AppointmentRow(AppointmentEvent entry, bool procedureExists = true) =>
        new(entry, procedureExists, procedureExists ? CurrentTitle : null);

    [Test]
    public async Task Handle_BothFeeds_MergesThemNewestFirst()
    {
        var created = CreateEntry(Action.Created, Now.AddDays(-2));
        var requested = CreateAppointmentEvent(AppointmentEventAction.Requested, Now.AddDays(-2).AddTicks(1));
        var notes = CreateEntry(Action.StageNotesUpdated, Now.AddDays(-1), NotesDetails);
        var booked = CreateAppointmentEvent(AppointmentEventAction.Booked, Now);
        GivenRows(new(notes, CurrentTitle), new(created, CurrentTitle));
        GivenAppointmentRows(AppointmentRow(booked), AppointmentRow(requested));

        var result = await _handler.Handle(CreateTimelineQuery(), CancellationToken.None);

        result.Items.Select(i => i.Id).Should().Equal(booked.Id, notes.Id, requested.Id, created.Id);
        result.Items.Select(i => i.Kind).Should().Equal("Appointments", "Notes", "Appointments", "Created");
        result.Items[0].Action.Should().Be("AppointmentBooked");
        result.NextCursor.Should().BeNull();
    }

    [Test]
    public async Task Handle_PageEndsOnAnAppointmentEntry_ReturnsACursorAfterIt()
    {
        var booked = CreateAppointmentEvent(AppointmentEventAction.Booked, Now);
        var notes = CreateEntry(Action.StageNotesUpdated, Now.AddMinutes(-1), NotesDetails);
        var requested = CreateAppointmentEvent(AppointmentEventAction.Requested, Now.AddMinutes(-2));
        var created = CreateEntry(Action.Created, Now.AddMinutes(-3));
        GivenRows(new(notes, CurrentTitle), new(created, CurrentTitle));
        GivenAppointmentRows(AppointmentRow(booked), AppointmentRow(requested));

        var result = await _handler.Handle(CreateTimelineQuery(pageSize: 3), CancellationToken.None);

        result.Items.Select(i => i.Id).Should().Equal(booked.Id, notes.Id, requested.Id);
        result.NextCursor.Should().Be(TimelineCursor.After(requested).Encode());
    }

    [Test]
    public async Task Handle_PageEndsOnAnAuditEntryWithAppointmentsLeft_ReturnsACursorAfterIt()
    {
        var notes = CreateEntry(Action.StageNotesUpdated, Now, NotesDetails);
        var booked = CreateAppointmentEvent(AppointmentEventAction.Booked, Now.AddMinutes(-1));
        GivenRows(new PatientTimelineRow(notes, CurrentTitle));
        GivenAppointmentRows(AppointmentRow(booked));

        var result = await _handler.Handle(CreateTimelineQuery(pageSize: 1), CancellationToken.None);

        result.Items.Select(i => i.Id).Should().Equal(notes.Id);
        result.NextCursor.Should().Be(TimelineCursor.After(notes).Encode());
    }

    [Test]
    public async Task Handle_FollowingEachCursor_ReturnsEveryEntryOfBothFeedsOnce()
    {
        // Entries of both feeds interleaved in time, over more pages than one; each "repository" pages itself with the
        // real searches, as the database would.
        var procedure = CreateTimelineProcedure();
        var audit = Enumerable.Range(0, 7)
            .Select(i => CreateEntry(Action.StageNotesUpdated, Now.AddMinutes(-2 * i)))
            .ToArray();
        var events = Enumerable.Range(0, 6)
            .Select(i => CreateAppointmentEvent(AppointmentEventAction.NotesUpdated, Now.AddMinutes(-2 * i - 1)))
            .ToArray();
        _audit
            .Setup(r => r.GetPatientTimelineAsync(It.IsAny<GetPatientTimelineQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetPatientTimelineQuery q, CancellationToken _) =>
                PatientTimelineSearch.Page(audit.AsQueryable(), new[] { procedure }.AsQueryable(), q).ToList());
        _appointments
            .Setup(r => r.GetPatientTimelineAsync(It.IsAny<GetPatientTimelineQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetPatientTimelineQuery q, CancellationToken _) =>
                AppointmentTimelineSearch.Page(events.AsQueryable(), new[] { procedure }.AsQueryable(), q).ToList());

        var seen = new List<Guid>();
        string? before = null;
        for (var pages = 0; pages < 10; pages++)
        {
            var result = await _handler.Handle(CreateTimelineQuery(pageSize: 4) with { Before = before }, CancellationToken.None);
            seen.AddRange(result.Items.Select(i => i.Id));
            before = result.NextCursor;
            if (before is null)
                break;
        }

        var expected = audit.Select(e => (e.AtUtc, e.Id)).Concat(events.Select(e => (e.AtUtc, e.Id)))
            .OrderByDescending(e => e.AtUtc)
            .Select(e => e.Id);
        seen.Should().Equal(expected);
    }

    [Test]
    public async Task Handle_KindsOnlyAppointments_DoesNotAskTheAuditLog()
    {
        var booked = CreateAppointmentEvent(AppointmentEventAction.Booked, Now);
        GivenAppointmentRows(AppointmentRow(booked));
        var query = CreateTimelineQuery() with { Kinds = ["Appointments"] };

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Items.Select(i => i.Id).Should().Equal(booked.Id);
        _audit.Verify(r => r.GetPatientTimelineAsync(It.IsAny<GetPatientTimelineQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_KindsWithoutAppointments_DoesNotAskForAppointments()
    {
        GivenRows(new PatientTimelineRow(CreateEntry(Action.StageNotesUpdated, Now, NotesDetails), CurrentTitle));
        var query = CreateTimelineQuery() with { Kinds = ["Notes"] };

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Items.Should().ContainSingle();
        _appointments.Verify(r => r.GetPatientTimelineAsync(It.IsAny<GetPatientTimelineQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_KindsIncludingAppointmentsAndOthers_AsksBoth()
    {
        GivenRows();
        var query = CreateTimelineQuery() with { Kinds = ["Appointments", "Notes"] };
        using var cts = new CancellationTokenSource();

        await _handler.Handle(query, cts.Token);

        _audit.Verify(r => r.GetPatientTimelineAsync(query, cts.Token), Times.Once);
        _appointments.Verify(r => r.GetPatientTimelineAsync(query, cts.Token), Times.Once);
    }

    [Test]
    public async Task Handle_StaffGivenAnAppointmentOfADeletedProcedure_LeavesItOut()
    {
        GivenRows();
        var visible = CreateAppointmentEvent(AppointmentEventAction.Booked, Now);
        var notForAProcedure = CreateAppointmentEvent(AppointmentEventAction.Booked, Now.AddMinutes(-1), withProcedure: false);
        var ofDeleted = CreateAppointmentEvent(AppointmentEventAction.Cancelled, Now.AddMinutes(-2), procedureId: DeletedProcedureId);
        GivenAppointmentRows(AppointmentRow(visible), new AppointmentTimelineRow(notForAProcedure, false, null), AppointmentRow(ofDeleted, false));

        var result = await _handler.Handle(CreateTimelineQuery(isAdmin: false), CancellationToken.None);

        result.Items.Select(i => i.Id).Should().Equal(visible.Id, notForAProcedure.Id);
    }

    [Test]
    public async Task Handle_AdminGivenAnAppointmentOfADeletedProcedure_IncludesItWithTheReason()
    {
        GivenRows();
        var ofDeleted = CreateAppointmentEvent(
            AppointmentEventAction.Cancelled, Now, "{\"reason\":\"" + CancelReasonText + "\"}", procedureId: DeletedProcedureId, procedureTitle: DeletedTitle);
        GivenAppointmentRows(AppointmentRow(ofDeleted, false));

        var result = await _handler.Handle(CreateTimelineQuery(isAdmin: true), CancellationToken.None);

        var item = result.Items.Should().ContainSingle().Subject;
        item.IsProcedureDeleted.Should().BeTrue();
        item.ProcedureTitle.Should().Be(DeletedTitle);
        item.Reason.Should().Be(CancelReasonText);
    }

    [Test]
    public async Task Handle_PatientDoesNotExist_AsksForNoAppointments()
    {
        var act = () => _handler.Handle(CreateTimelineQuery() with { PatientId = OtherPatientId }, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _appointments.Verify(r => r.GetPatientTimelineAsync(It.IsAny<GetPatientTimelineQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_Admin_GetsEachNotesTextFromTheVersions()
    {
        var noteId = Guid.NewGuid();
        var added = CreateEntry(Action.NoteAdded, Now.AddHours(-2), $$"""{"noteId":"{{noteId}}","version":1}""");
        var edited = CreateEntry(Action.NoteEdited, Now.AddHours(-1), $$"""{"noteId":"{{noteId}}","version":2}""");
        GivenRows(new(edited, CurrentTitle), new(added, CurrentTitle));
        _audit
            .Setup(r => r.GetNoteVersionsAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<NoteVersionKey>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([NoteVersion(noteId, 1, "Synthetic first."), NoteVersion(noteId, 2, "Synthetic second.")]);

        var result = await _handler.Handle(CreateTimelineQuery(isAdmin: true), CancellationToken.None);

        result.Items.Select(i => (i.Action, i.Changes.Single().From, i.Changes.Single().To)).Should().Equal(
            ("NoteEdited", "Synthetic first.", "Synthetic second."),
            ("NoteAdded", null, "Synthetic first."));
        _audit.Verify(r => r.GetNoteVersionsAsync(
            CreateTimelineQuery(isAdmin: true).PatientId,
            It.Is<IReadOnlyCollection<NoteVersionKey>>(keys => keys.SequenceEqual(new[] { new NoteVersionKey(noteId, 2), new NoteVersionKey(noteId, 1) })),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_Staff_NeverReadsTheNotesText()
    {
        var noteId = Guid.NewGuid();
        GivenRows(new PatientTimelineRow(CreateEntry(Action.NoteAdded, Now, $$"""{"noteId":"{{noteId}}","version":1}"""), CurrentTitle));

        var result = await _handler.Handle(CreateTimelineQuery(), CancellationToken.None);

        var change = result.Items.Should().ContainSingle().Which.Changes.Should().ContainSingle().Subject;
        change.Field.Should().Be(TimelineMapper.NoteField);
        change.To.Should().BeNull();
        _audit.Verify(r => r.GetNoteVersionsAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<NoteVersionKey>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_AdminWithoutNoteEntries_DoesNotLookForVersions()
    {
        GivenRows(new PatientTimelineRow(CreateEntry(Action.Created, Now), CurrentTitle));

        await _handler.Handle(CreateTimelineQuery(isAdmin: true), CancellationToken.None);

        _audit.Verify(r => r.GetNoteVersionsAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<NoteVersionKey>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static PatientProcedureNoteVersion NoteVersion(Guid noteId, int version, string text) => new()
    {
        Id = Guid.NewGuid(), NoteId = noteId, Version = version, PatientProcedureId = ProcedureId, PatientId = PatientId,
        Text = text, UserId = Staff.UserId, Username = Staff.Username, AtUtc = Now.AddHours(-3 + version)
    };
}
