using AutoFixture;
using FluentAssertions;
using MedApp.Application.Appointments.Commands.RecordPastAppointment;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Domain.Appointments;
using MedApp.Domain.Notifications;
using MedApp.Domain.Patients;
using MedApp.Domain.Pathways.PatientPathways;
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
    private Mock<IPatientPathwayRepository> _pathways = null!;
    private Patient _patient = null!;
    private RecordPastAppointmentHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _patients = new Mock<IPatientRepository>();
        _pathways = new Mock<IPatientPathwayRepository>();
        _patient = PatientsTestConstants.CreateValidPatient();
        _patients.Setup(p => p.GetByIdAsync(_patient.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_patient);
        CalendarData = CreateData(LastThursday);

        Fixture.Inject(_patients.Object);
        Fixture.Inject(_pathways.Object);
        _handler = Fixture.Create<RecordPastAppointmentHandler>();
    }

    private RecordPastAppointmentCommand Command(
        Guid? pathwayId = null, Guid? stageId = null, string outcome = "Attended", PathwayActor? by = null) =>
        new(_patient.Id, pathwayId, stageId, StaffUserId, At(LastThursday, 10), At(LastThursday, 10, 30), outcome, Notes, by ?? Colleague);

    private PatientPathway GivenPathway(PatientPathwayStatus status = PatientPathwayStatus.Assigned, Patient? patient = null)
    {
        var pathway = PathwaysTestConstants.CreatePathway(status, patient: patient ?? _patient, title: PathwayTitle);
        pathway.TeamId = PathwaysTestConstants.TeamId;
        pathway.StageAt(1).Name = StageName;
        _pathways.Setup(p => p.GetByIdAsync(pathway.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pathway);
        return pathway;
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
    public async Task Handle_PathwayDoesNotExist_ThrowsNotFound()
    {
        var act = () => _handler.Handle(Command(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_PathwayForAnotherPatient_ThrowsConflict()
    {
        var pathway = GivenPathway(patient: PatientsTestConstants.CreateValidPatient());

        var act = () => _handler.Handle(Command(pathway.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("That pathway is for another patient.");
    }

    [TestCase(PatientPathwayStatus.Completed)]
    [TestCase(PatientPathwayStatus.Cancelled)]
    [TestCase(PatientPathwayStatus.Archived)]
    public async Task Handle_StageOfAClosedPathway_ThrowsConflict(PatientPathwayStatus status)
    {
        var pathway = GivenPathway(status);

        var act = () => _handler.Handle(Command(pathway.Id, pathway.StageAt(1).Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("A visit can't be recorded for a stage of a pathway that's *");
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
    public async Task Handle_ForAStageWithNoOpenVisit_AddsOneLinkedToItWithThePathwayTitleAndStageName()
    {
        var pathway = GivenPathway();
        var stage = pathway.StageAt(1);

        await _handler.Handle(Command(pathway.Id, stage.Id), CancellationToken.None);

        var added = Added.Single();
        added.PathwayId.Should().Be(pathway.Id);
        added.StageId.Should().Be(stage.Id);
        var entry = SavedEvents.Single();
        entry.PathwayTitle.Should().Be(PathwayTitle);
        entry.StageName.Should().Be(StageName);
    }

    [Test]
    public async Task Handle_StageHasAVisitInNeedsBooking_RecordsThatOneInsteadOfAddingAnother()
    {
        var pathway = GivenPathway();
        var stageId = pathway.StageAt(1).Id;
        var requested = GivenAppointment(CalendarTestConstants.CreateAppointment(AppointmentStatus.Requested, stageId: stageId));
        requested.Notes = string.Empty;
        using var cts = new CancellationTokenSource();
        Repository.Setup(r => r.GetOpenForStageAsync(stageId, cts.Token)).ReturnsAsync(requested);

        var response = await _handler.Handle(Command(pathway.Id, stageId), cts.Token);

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
        var pathway = GivenPathway();
        var stageId = pathway.StageAt(1).Id;
        var requested = GivenAppointment(CalendarTestConstants.CreateAppointment(AppointmentStatus.Requested, stageId: stageId));
        requested.Notes = OtherNotes;
        Repository.Setup(r => r.GetOpenForStageAsync(stageId, It.IsAny<CancellationToken>())).ReturnsAsync(requested);

        await _handler.Handle(Command(pathway.Id, stageId) with { Notes = " " }, CancellationToken.None);

        requested.Notes.Should().Be(OtherNotes);
        SavedEvents.Should().ContainSingle().Which.Action.Should().Be(AppointmentEventAction.RecordedAfterEvent);
    }

    [Test]
    public async Task Handle_StageVisitBookedMeanwhile_ThrowsConflictAndSavesNothing()
    {
        var pathway = GivenPathway();
        var stageId = pathway.StageAt(1).Id;
        var seen = CalendarTestConstants.CreateAppointment(AppointmentStatus.Requested, stageId: stageId);
        var now = CalendarTestConstants.CreateAppointment(AppointmentStatus.Booked, stageId: stageId);
        Repository.Setup(r => r.GetOpenForStageAsync(stageId, It.IsAny<CancellationToken>())).ReturnsAsync(seen);
        Repository.Setup(r => r.GetForUpdateAsync(seen.Id, It.IsAny<CancellationToken>())).ReturnsAsync(now);

        var act = () => _handler.Handle(Command(pathway.Id, stageId), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("Someone else changed this stage's visit just now. Reload it and try again.");
        SavedEvents.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_StageHasAVisitBooked_ThrowsConflictSayingWhenAndWithWhom()
    {
        var pathway = GivenPathway();
        var stageId = pathway.StageAt(1).Id;
        Repository.Setup(r => r.GetOpenForStageAsync(stageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CalendarTestConstants.CreateAppointment(AppointmentStatus.Booked, At(NextMonday, 11), stageId: stageId));

        var act = () => _handler.Handle(Command(pathway.Id, stageId), CancellationToken.None);

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

    [Test]
    public async Task Handle_BeforeTheDayThePathwayWasCreated_IsAConflict()
    {
        var pathway = PathwaysTestConstants.CreatePathway(PatientPathwayStatus.Assigned, patient: _patient, title: PathwayTitle, createdAt: At(LastThursday.AddDays(1), 9));
        pathway.TeamId = PathwaysTestConstants.TeamId;
        _pathways.Setup(p => p.GetByIdAsync(pathway.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pathway);

        var act = () => _handler.Handle(Command(pathway.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("This visit was before the day the pathway was created (Friday 13 November at 09:00)*");
        Added.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_EarlierOnTheDayThePathwayWasCreated_IsRecorded()
    {
        // Seen at 10:00; the pathway created at 11:00, once the clinician knew what was needed.
        var pathway = PathwaysTestConstants.CreatePathway(PatientPathwayStatus.Assigned, patient: _patient, title: PathwayTitle, createdAt: At(LastThursday, 11));
        pathway.TeamId = PathwaysTestConstants.TeamId;
        _pathways.Setup(p => p.GetByIdAsync(pathway.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pathway);

        await _handler.Handle(Command(pathway.Id), CancellationToken.None);

        Added.Should().ContainSingle().Which.PathwayId.Should().Be(pathway.Id);
    }

    [Test]
    public async Task Handle_AStageNotReachedYet_IsAConflict()
    {
        var pathway = GivenPathway();

        var act = () => _handler.Handle(Command(pathway.Id, pathway.StageAt(2).Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*hasn't been reached yet*");
        Lock.Locked.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_TheWaitingVisitsNotes_AreKept_WithTheNewOnesAfterThem()
    {
        var pathway = GivenPathway();
        var stageId = pathway.StageAt(1).Id;
        var requested = GivenAppointment(CalendarTestConstants.CreateAppointment(AppointmentStatus.Requested, stageId: stageId));
        requested.Notes = OtherNotes;
        Repository.Setup(r => r.GetOpenForStageAsync(stageId, It.IsAny<CancellationToken>())).ReturnsAsync(requested);

        await _handler.Handle(Command(pathway.Id, stageId), CancellationToken.None);

        requested.Notes.Should().Be($"{OtherNotes}\n\n{Notes}");
    }

    [Test]
    public async Task Handle_AVisitAddedForTheStageMeanwhile_IsAConflict()
    {
        // None when first looked for; one by the time the calendar lock is held.
        var pathway = GivenPathway();
        var stageId = pathway.StageAt(1).Id;
        Repository.SetupSequence(r => r.GetOpenForStageAsync(stageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Appointment?)null)
            .ReturnsAsync(CalendarTestConstants.CreateAppointment(AppointmentStatus.Booked, At(NextMonday, 11), stageId: stageId));

        var act = () => _handler.Handle(Command(pathway.Id, stageId), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("Someone else changed this stage's visit just now. Reload it and try again.");
        Added.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_OnADayClosedSince_IsRecorded()
    {
        CalendarData = CreateData(LastThursday) with { ClosedDays = new ClosedDaySet([CreateClosedDay(LastThursday)]) };

        await _handler.Handle(Command(), CancellationToken.None);

        Added.Should().ContainSingle();
    }
}
