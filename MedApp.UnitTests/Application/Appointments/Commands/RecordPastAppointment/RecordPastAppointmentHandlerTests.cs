using AutoFixture;
using FluentAssertions;
using MedApp.Application.Appointments.Commands.RecordPastAppointment;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.Repositories;
using MedApp.Domain.Appointments;
using MedApp.Domain.Notifications;
using MedApp.Domain.Patients;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.AppointmentsTestConstants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Appointments.Commands.RecordPastAppointment;

[TestFixture]
public sealed class RecordPastAppointmentHandlerTests : AppointmentHandlerTestBase
{
    /// <summary>Thursday 12 November 2026, four days before the clock's Monday.</summary>
    private static readonly DateOnly LastThursday = new(2026, 11, 12);

    private Mock<IPatientRepository> _patients = null!;
    private Mock<IPatientProcedureRepository> _procedures = null!;
    private Patient _patient = null!;
    private RecordPastAppointmentHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _patients = new Mock<IPatientRepository>();
        _procedures = new Mock<IPatientProcedureRepository>();
        _patient = PatientsTestConstants.CreateValidPatient();
        _patients.Setup(p => p.GetByIdAsync(_patient.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_patient);
        CalendarData = CreateData(LastThursday);

        Fixture.Inject(_patients.Object);
        Fixture.Inject(_procedures.Object);
        _handler = Fixture.Create<RecordPastAppointmentHandler>();
    }

    private RecordPastAppointmentCommand Command(
        Guid? procedureId = null, Guid? stageId = null, string outcome = "Attended", ProcedureActor? by = null) =>
        new(_patient.Id, procedureId, stageId, StaffUserId, At(LastThursday, 10), At(LastThursday, 10, 30), outcome, Notes, by ?? Colleague);

    private PatientProcedure GivenProcedure(PatientProcedureStatus status = PatientProcedureStatus.Assigned, Patient? patient = null)
    {
        var procedure = ProceduresTestConstants.CreateProcedure(status, patient: patient ?? _patient, title: ProcedureTitle);
        procedure.StageAt(1).Name = StageName;
        _procedures.Setup(p => p.GetByIdAsync(procedure.Id, It.IsAny<CancellationToken>())).ReturnsAsync(procedure);
        return procedure;
    }

    [Test]
    public async Task Handle_PatientDoesNotExist_ThrowsNotFound()
    {
        var act = () => _handler.Handle(Command() with { PatientId = Guid.NewGuid() }, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        Added.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_RestrictedPatient_ThrowsConflictAndRecordsNothing()
    {
        _patient.RestrictedAtUtc = Now.AddDays(-1);

        var act = () => _handler.Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PatientRules.RestrictedMessage);
        Added.Should().BeEmpty();
        Lock.Locked.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_ProcedureDoesNotExist_ThrowsNotFound()
    {
        var act = () => _handler.Handle(Command(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_ProcedureForAnotherPatient_ThrowsConflict()
    {
        var procedure = GivenProcedure(patient: PatientsTestConstants.CreateValidPatient());

        var act = () => _handler.Handle(Command(procedure.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("That procedure is for another patient.");
    }

    [TestCase(PatientProcedureStatus.Completed)]
    [TestCase(PatientProcedureStatus.Cancelled)]
    [TestCase(PatientProcedureStatus.Archived)]
    public async Task Handle_StageOfAClosedProcedure_ThrowsConflict(PatientProcedureStatus status)
    {
        var procedure = GivenProcedure(status);

        var act = () => _handler.Handle(Command(procedure.Id, procedure.StageAt(1).Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("A visit can't be recorded for a stage of a procedure that's *");
        Added.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_UnknownStaffMember_ThrowsNotFound()
    {
        var act = () => _handler.Handle(Command() with { StaffUserId = Guid.NewGuid() }, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        Added.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_NewWalkIn_RecordsItAttendedUnderTheStaffMembersLock()
    {
        using var cts = new CancellationTokenSource();

        var response = await _handler.Handle(Command(), cts.Token);

        Lock.LockedOnce.Should().Equal(StaffUserId);
        Lock.Tokens.Should().Equal(cts.Token);
        Calendar.Verify(c => c.GetCalendarDataAsync(StaffUserId, LastThursday, LastThursday, cts.Token), Times.Once);

        var added = Added.Should().ContainSingle().Subject;
        added.Status.Should().Be(AppointmentStatus.Attended);
        added.IsRecordedAfterEvent.Should().BeTrue();
        added.PatientId.Should().Be(_patient.Id);
        added.StaffUserId.Should().Be(StaffUserId);
        added.StartUtc.Should().Be(At(LastThursday, 10));
        added.EndUtc.Should().Be(At(LastThursday, 10, 30));
        added.Notes.Should().Be(Notes);
        added.IsAutomatic.Should().BeFalse();
        added.CreatedByUserId.Should().Be(Colleague.UserId);
        added.BookedAtUtc.Should().Be(Now);
        added.BookedByUserId.Should().Be(Colleague.UserId);
        added.OutcomeAtUtc.Should().Be(Now);
        added.OutcomeByUserId.Should().Be(Colleague.UserId);

        var entry = SavedEvents.Should().ContainSingle().Subject;
        entry.Action.Should().Be(AppointmentEventAction.RecordedAfterEvent);
        entry.UserId.Should().Be(Colleague.UserId);
        entry.Username.Should().Be(Colleague.Username);
        entry.AtUtc.Should().Be(Now);
        entry.StaffUsername.Should().Be(StaffUsername);
        entry.Details.Should().Be("{\"outcome\":\"Attended\"}");
        SaveTokens.Should().Equal(cts.Token);

        response.Status.Should().Be(nameof(AppointmentStatus.Attended));
        response.IsRecordedAfterEvent.Should().BeTrue();
        response.BookedByUsername.Should().Be(Colleague.Username);
    }

    [Test]
    public async Task Handle_DidNotAttend_RecordsThatOutcome()
    {
        await _handler.Handle(Command(outcome: "DidNotAttend"), CancellationToken.None);

        Added.Single().Status.Should().Be(AppointmentStatus.DidNotAttend);
        SavedEvents.Single().Details.Should().Be("{\"outcome\":\"DidNotAttend\"}");
    }

    [Test]
    public async Task Handle_NoNotes_StoresEmptyNotes()
    {
        await _handler.Handle(Command() with { Notes = null }, CancellationToken.None);

        Added.Single().Notes.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_OverlapsAnotherAppointment_ThrowsConflictNamingItAndRecordsNothing()
    {
        CalendarData = CreateData(LastThursday, appointments: [CalendarTestConstants.CreateAppointment(AppointmentStatus.Attended, At(LastThursday, 10, 15))]);

        var act = () => _handler.Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("That time overlaps another appointment, 10:15 to 10:45.");
        Added.Should().BeEmpty();
        Sent.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_OverlapsBlockedTime_RecordsItAnyway()
    {
        // What happened, happened: the block shows flagged as overlapping it.
        CalendarData = CreateData(LastThursday, blocks: [CreateBlock(At(LastThursday, 9), At(LastThursday, 12))]);

        await _handler.Handle(Command(), CancellationToken.None);

        Added.Should().ContainSingle();
    }

    [Test]
    public async Task Handle_ForAStageWithNoOpenVisit_AddsOneLinkedToItWithTheProcedureTitleAndStageName()
    {
        var procedure = GivenProcedure();
        var stage = procedure.StageAt(1);

        await _handler.Handle(Command(procedure.Id, stage.Id), CancellationToken.None);

        var added = Added.Single();
        added.ProcedureId.Should().Be(procedure.Id);
        added.StageId.Should().Be(stage.Id);
        var entry = SavedEvents.Single();
        entry.ProcedureTitle.Should().Be(ProcedureTitle);
        entry.StageName.Should().Be(StageName);
    }

    [Test]
    public async Task Handle_StageHasAVisitInNeedsBooking_RecordsThatOneInsteadOfAddingAnother()
    {
        var procedure = GivenProcedure();
        var stageId = procedure.StageAt(1).Id;
        var requested = GivenAppointment(CalendarTestConstants.CreateAppointment(AppointmentStatus.Requested, stageId: stageId));
        requested.Notes = string.Empty;
        using var cts = new CancellationTokenSource();
        Repository.Setup(r => r.GetOpenForStageAsync(stageId, cts.Token)).ReturnsAsync(requested);

        var response = await _handler.Handle(Command(procedure.Id, stageId), cts.Token);

        Added.Should().BeEmpty();
        Repository.Verify(r => r.GetForUpdateAsync(requested.Id, cts.Token), Times.Once);
        requested.Status.Should().Be(AppointmentStatus.Attended);
        requested.IsRecordedAfterEvent.Should().BeTrue();
        requested.StaffUserId.Should().Be(StaffUserId);
        requested.StartUtc.Should().Be(At(LastThursday, 10));
        requested.Notes.Should().Be(Notes);
        SavedEvents.Select(e => e.Action).Should().Equal(AppointmentEventAction.RecordedAfterEvent, AppointmentEventAction.NotesUpdated);
        SavedEvents[0].Details.Should().Be("{\"outcome\":\"Attended\",\"previousStatus\":\"Requested\"}");
        SaveTokens.Should().Equal(cts.Token);
        response.Id.Should().Be(requested.Id);
    }

    [Test]
    public async Task Handle_StageVisitInNeedsBookingWithoutNewNotes_KeepsItsNotes()
    {
        var procedure = GivenProcedure();
        var stageId = procedure.StageAt(1).Id;
        var requested = GivenAppointment(CalendarTestConstants.CreateAppointment(AppointmentStatus.Requested, stageId: stageId));
        requested.Notes = OtherNotes;
        Repository.Setup(r => r.GetOpenForStageAsync(stageId, It.IsAny<CancellationToken>())).ReturnsAsync(requested);

        await _handler.Handle(Command(procedure.Id, stageId) with { Notes = " " }, CancellationToken.None);

        requested.Notes.Should().Be(OtherNotes);
        SavedEvents.Should().ContainSingle().Which.Action.Should().Be(AppointmentEventAction.RecordedAfterEvent);
    }

    [Test]
    public async Task Handle_StageVisitBookedMeanwhile_ThrowsConflictAndSavesNothing()
    {
        var procedure = GivenProcedure();
        var stageId = procedure.StageAt(1).Id;
        var seen = CalendarTestConstants.CreateAppointment(AppointmentStatus.Requested, stageId: stageId);
        var now = CalendarTestConstants.CreateAppointment(AppointmentStatus.Booked, stageId: stageId);
        Repository.Setup(r => r.GetOpenForStageAsync(stageId, It.IsAny<CancellationToken>())).ReturnsAsync(seen);
        Repository.Setup(r => r.GetForUpdateAsync(seen.Id, It.IsAny<CancellationToken>())).ReturnsAsync(now);

        var act = () => _handler.Handle(Command(procedure.Id, stageId), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("Someone else changed this stage's visit just now. Reload it and try again.");
        SavedEvents.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_StageHasAVisitBooked_ThrowsConflictSayingWhenAndWithWhom()
    {
        var procedure = GivenProcedure();
        var stageId = procedure.StageAt(1).Id;
        Repository.Setup(r => r.GetOpenForStageAsync(stageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CalendarTestConstants.CreateAppointment(AppointmentStatus.Booked, At(NextMonday, 11), stageId: stageId));

        var act = () => _handler.Handle(Command(procedure.Id, stageId), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(
            $"This stage already has a visit booked, on Monday 23 November at 11:00 with {StaffUsername}. " +
            "If that's the one that happened, record its outcome instead; otherwise cancel it first.");
        Lock.Locked.Should().BeEmpty();
        Added.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_RecordedBySomeoneElse_NotifiesTheStaffMemberWithoutThePatient()
    {
        using var cts = new CancellationTokenSource();

        await _handler.Handle(Command(), cts.Token);

        var (notification, token) = Sent.Should().ContainSingle().Subject;
        token.Should().Be(cts.Token);
        notification.UserId.Should().Be(StaffUserId);
        notification.Kind.Should().Be(NotificationKind.AppointmentRecorded);
        notification.Title.Should().Be("Appointment recorded");
        notification.Message.Should().Be($"{Colleague.Username} recorded an appointment you had on Thursday 12 November at 10:00, as attended.");
        notification.AppointmentId.Should().Be(Added.Single().Id);
        notification.Message.Should().NotContain(_patient.LastName).And.NotContain(Notes);
    }

    [Test]
    public async Task Handle_RecordedByTheStaffMemberThemselves_NotifiesNobody()
    {
        await _handler.Handle(Command(by: Self), CancellationToken.None);

        Sent.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_TimeWithoutAKind_IsTakenAsUtc()
    {
        var start = DateTime.SpecifyKind(At(LastThursday, 10), DateTimeKind.Unspecified);

        await _handler.Handle(Command() with { StartUtc = start, EndUtc = start.AddMinutes(30) }, CancellationToken.None);

        Added.Single().StartUtc!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }
}
