using AutoFixture;
using FluentAssertions;
using MedApp.Application.Appointments.Commands.CreateAppointment;
using MedApp.Application.Appointments.Common;
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

namespace MedApp.UnitTests.Application.Appointments.Commands.CreateAppointment;

[TestFixture]
public sealed class CreateAppointmentHandlerTests : AppointmentHandlerTestBase
{
    private Mock<IPatientRepository> _patients = null!;
    private Mock<IPatientPathwayRepository> _pathways = null!;
    private Patient _patient = null!;
    private CreateAppointmentHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _patients = new Mock<IPatientRepository>();
        _pathways = new Mock<IPatientPathwayRepository>();
        _patient = PatientsTestConstants.CreateValidPatient();
        _patients.Setup(p => p.GetByIdAsync(_patient.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_patient);

        Fixture.Inject(_patients.Object);
        Fixture.Inject(_pathways.Object);
        _handler = Fixture.Create<CreateAppointmentHandler>();
    }

    private CreateAppointmentCommand Request(Guid? pathwayId = null, Guid? stageId = null) =>
        new(_patient.Id, pathwayId, stageId, null, null, null, Notes, Colleague);

    private CreateAppointmentCommand Booking(Guid? pathwayId = null, Guid? stageId = null, PathwayActor? by = null) =>
        new(_patient.Id, pathwayId, stageId, StaffUserId, At(NextMonday, 10), At(NextMonday, 10, 30), Notes, by ?? Colleague);

    private PatientPathway GivenPathway(PatientPathwayStatus status = PatientPathwayStatus.Assigned, Patient? patient = null)
    {
        var pathway = PathwaysTestConstants.CreatePathway(status, patient: patient ?? _patient, title: PathwayTitle);
        pathway.StageAt(1).Name = StageName;
        _pathways.Setup(p => p.GetByIdAsync(pathway.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pathway);
        return pathway;
    }

    [Test]
    public async Task Handle_PatientDoesNotExist_ThrowsNotFound()
    {
        var act = () => _handler.Handle(Request() with { PatientId = Guid.NewGuid() }, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        Added.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_PathwayDoesNotExist_ThrowsNotFound()
    {
        var act = () => _handler.Handle(Request(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        Added.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_PathwayForAnotherPatient_ThrowsConflict()
    {
        var pathway = GivenPathway(patient: PatientsTestConstants.CreateValidPatient());

        var act = () => _handler.Handle(Request(pathway.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("That pathway is for another patient.");
        Added.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_StageNotOnThePathway_ThrowsNotFound()
    {
        var pathway = GivenPathway();

        var act = () => _handler.Handle(Request(pathway.Id, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        Added.Should().BeEmpty();
    }

    [TestCase(PatientPathwayStatus.Completed)]
    [TestCase(PatientPathwayStatus.Cancelled)]
    [TestCase(PatientPathwayStatus.Archived)]
    public async Task Handle_StageOfAClosedPathway_ThrowsConflict(PatientPathwayStatus status)
    {
        var pathway = GivenPathway(status);

        var act = () => _handler.Handle(Request(pathway.Id, pathway.StageAt(1).Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("A visit can't be booked for a stage of a pathway that's *");
        Added.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_StageAlreadyHasAVisitInRequiresAppointment_ThrowsConflictPointingToIt()
    {
        var pathway = GivenPathway();
        var stageId = pathway.StageAt(1).Id;
        using var cts = new CancellationTokenSource();
        Repository.Setup(r => r.GetOpenForStageAsync(stageId, cts.Token))
            .ReturnsAsync(CalendarTestConstants.CreateAppointment(AppointmentStatus.Requested, stageId: stageId));

        var act = () => _handler.Handle(Request(pathway.Id, stageId), cts.Token);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("This stage already has a visit in Requires Appointment. Book that one instead.");
        Added.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_StageAlreadyHasABookedVisit_ThrowsConflictSayingWhenAndWithWhom()
    {
        var pathway = GivenPathway();
        var stageId = pathway.StageAt(1).Id;
        using var cts = new CancellationTokenSource();
        Repository.Setup(r => r.GetOpenForStageAsync(stageId, cts.Token))
            .ReturnsAsync(CalendarTestConstants.CreateAppointment(AppointmentStatus.Booked, At(NextMonday, 11), stageId: stageId));

        var act = () => _handler.Handle(Booking(pathway.Id, stageId), cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(
            $"This stage already has a visit booked, on Monday 23 November at 11:00 with {StaffUsername}. Reschedule or cancel that one instead.");
        Identity.Verify(i => i.GetUsernamesByIdAsync(It.Is<IEnumerable<Guid>>(ids => ids.Single() == StaffUserId), cts.Token), Times.Once);
        Lock.Locked.Should().BeEmpty();
        Added.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_AnotherRequestAddsAVisitForTheStageFirst_ThrowsTheSameConflictPointingToIt()
    {
        // Both passed the check; the database's unique index refused this one's save.
        var pathway = GivenPathway();
        var stageId = pathway.StageAt(1).Id;
        var theirs = CalendarTestConstants.CreateAppointment(AppointmentStatus.Requested, stageId: stageId);
        using var cts = new CancellationTokenSource();
        Repository.SetupSequence(r => r.GetOpenForStageAsync(stageId, cts.Token))
            .ReturnsAsync((Appointment?)null)
            .ReturnsAsync(theirs);
        Repository
            .Setup(r => r.AddAsync(It.IsAny<Appointment>(), It.IsAny<IEnumerable<AppointmentEvent>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new StageHasOpenAppointmentException(stageId));

        var act = () => _handler.Handle(Request(pathway.Id, stageId), cts.Token);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("This stage already has a visit in Requires Appointment. Book that one instead.");
        Repository.Verify(r => r.GetOpenForStageAsync(stageId, cts.Token), Times.Exactly(2));
    }

    [Test]
    public async Task Handle_BookingLosesTheRaceForTheStage_ThrowsTheConflictAndNotifiesNobody()
    {
        var pathway = GivenPathway();
        var stageId = pathway.StageAt(1).Id;
        var theirs = CalendarTestConstants.CreateAppointment(AppointmentStatus.Booked, At(NextMonday, 11), stageId: stageId);
        Repository.SetupSequence(r => r.GetOpenForStageAsync(stageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Appointment?)null)
            .ReturnsAsync(theirs);
        Repository
            .Setup(r => r.AddAsync(It.IsAny<Appointment>(), It.IsAny<IEnumerable<AppointmentEvent>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new StageHasOpenAppointmentException(stageId));

        var act = () => _handler.Handle(Booking(pathway.Id, stageId), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("This stage already has a visit booked, on Monday 23 November at 11:00*");
        Sent.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_TheVisitThatWonTheRaceIsClosedByTheTimeItIsLookedFor_ThrowsAConflictSayingToTryAgain()
    {
        var pathway = GivenPathway();
        var stageId = pathway.StageAt(1).Id;
        Repository
            .Setup(r => r.AddAsync(It.IsAny<Appointment>(), It.IsAny<IEnumerable<AppointmentEvent>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new StageHasOpenAppointmentException(stageId));

        var act = () => _handler.Handle(Request(pathway.Id, stageId), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("This stage already has a visit requested or booked. Reload it and try again.");
    }

    [Test]
    public async Task Handle_PathwayWithoutAStage_DoesNotCheckForOpenVisits()
    {
        var pathway = GivenPathway(PatientPathwayStatus.Cancelled);

        var response = await _handler.Handle(Request(pathway.Id), CancellationToken.None);

        response.PathwayId.Should().Be(pathway.Id);
        Repository.Verify(r => r.GetOpenForStageAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_NoStaffOrTime_AddsARequestedAppointmentWithoutTheLock()
    {
        using var cts = new CancellationTokenSource();

        var response = await _handler.Handle(Request(), cts.Token);

        var added = Added.Should().ContainSingle().Subject;
        added.Status.Should().Be(AppointmentStatus.Requested);
        added.PatientId.Should().Be(_patient.Id);
        added.StaffUserId.Should().BeNull();
        added.StartUtc.Should().BeNull();
        added.Notes.Should().Be(Notes);
        added.IsAutomatic.Should().BeFalse();
        added.CreatedAtUtc.Should().Be(Now);
        added.CreatedByUserId.Should().Be(Colleague.UserId);
        added.LastUpdated.Should().Be(Now);

        SavedEvents.Should().ContainSingle().Which.Action.Should().Be(AppointmentEventAction.Requested);
        SaveTokens.Should().Equal(cts.Token);
        Lock.Locked.Should().BeEmpty();
        Sent.Should().BeEmpty();
        response.Status.Should().Be(nameof(AppointmentStatus.Requested));
        response.CreatedByUsername.Should().Be(Colleague.Username);
    }

    [Test]
    public async Task Handle_NoNotes_StoresEmptyNotes()
    {
        await _handler.Handle(Request() with { Notes = null }, CancellationToken.None);

        Added.Single().Notes.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_ForAStage_LinksItAndRecordsThePathwayTitleAndStageName()
    {
        var pathway = GivenPathway();
        var stage = pathway.StageAt(1);

        await _handler.Handle(Request(pathway.Id, stage.Id), CancellationToken.None);

        var added = Added.Single();
        added.PathwayId.Should().Be(pathway.Id);
        added.StageId.Should().Be(stage.Id);
        var entry = SavedEvents.Single();
        entry.PathwayTitle.Should().Be(PathwayTitle);
        entry.StageName.Should().Be(StageName);
    }

    [Test]
    public async Task Handle_ReadBack_ReturnsTheSavedAppointmentWithItsPathway()
    {
        var pathway = GivenPathway();
        Repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) =>
            {
                var saved = Added.Single(a => a.Id == id);
                saved.Pathway = pathway;
                saved.Patient = _patient;
                return saved;
            });

        var response = await _handler.Handle(Request(pathway.Id), CancellationToken.None);

        response.PathwayTitle.Should().Be(PathwayTitle);
        response.PatientName.Should().Be($"{_patient.FirstName} {_patient.LastName}");
    }

    [Test]
    public async Task Handle_Booking_ChecksAndSavesHoldingTheStaffMembersLock()
    {
        using var cts = new CancellationTokenSource();

        var response = await _handler.Handle(Booking(), cts.Token);

        Lock.LockedOnce.Should().Equal(StaffUserId);
        Lock.Tokens.Should().Equal(cts.Token);
        Calendar.Verify(c => c.GetCalendarDataAsync(StaffUserId, NextMonday, NextMonday, cts.Token), Times.Once);

        var added = Added.Should().ContainSingle().Subject;
        added.Status.Should().Be(AppointmentStatus.Booked);
        added.StaffUserId.Should().Be(StaffUserId);
        added.StartUtc.Should().Be(At(NextMonday, 10));
        added.EndUtc.Should().Be(At(NextMonday, 10, 30));
        added.BookedAtUtc.Should().Be(Now);
        added.BookedByUserId.Should().Be(Colleague.UserId);
        SavedEvents.Should().ContainSingle().Which.Action.Should().Be(AppointmentEventAction.Booked);
        SavedEvents.Single().StaffUsername.Should().Be(StaffUsername);
        SaveTokens.Should().Equal(cts.Token);

        response.Status.Should().Be(nameof(AppointmentStatus.Booked));
        response.StaffUsername.Should().Be(StaffUsername);
        response.DurationMinutes.Should().Be(30);
    }

    [Test]
    public async Task Handle_BookingTimeWithoutAKind_IsTakenAsUtc()
    {
        var start = DateTime.SpecifyKind(At(NextMonday, 10), DateTimeKind.Unspecified);
        var command = Booking() with { StartUtc = start, EndUtc = start.AddMinutes(30) };

        await _handler.Handle(command, CancellationToken.None);

        Added.Single().StartUtc!.Value.Kind.Should().Be(DateTimeKind.Utc);
        Added.Single().StartUtc.Should().Be(At(NextMonday, 10));
    }

    [Test]
    public async Task Handle_BookingWithSomeoneElse_NotifiesThemWithoutThePatientOrNotes()
    {
        var pathway = GivenPathway();
        using var cts = new CancellationTokenSource();

        await _handler.Handle(Booking(pathway.Id), cts.Token);

        var (notification, token) = Sent.Should().ContainSingle().Subject;
        token.Should().Be(cts.Token);
        notification.UserId.Should().Be(StaffUserId);
        notification.Kind.Should().Be(NotificationKind.AppointmentBooked);
        notification.Title.Should().Be("Appointment booked");
        notification.PathwayId.Should().Be(pathway.Id);
        notification.AppointmentId.Should().Be(Added.Single().Id);
        notification.Message.Should().Be($"{Colleague.Username} booked you an appointment on Monday 23 November at 10:00.");
        notification.Message.Should().NotContain(_patient.FirstName).And.NotContain(Notes);
    }

    [Test]
    public async Task Handle_BookingWithYourself_SendsNoNotification()
    {
        await _handler.Handle(Booking(by: Self), CancellationToken.None);

        Added.Should().ContainSingle();
        Sent.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_BookingWithSomeoneWhoIsNotStaff_ThrowsNotFound()
    {
        var act = () => _handler.Handle(Booking() with { StaffUserId = Colleague.UserId }, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        Lock.Locked.Should().BeEmpty();
        Added.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_BookingOverAClash_ThrowsConflictNamingItAndSavesNothing()
    {
        CalendarData = CreateData(NextMonday, blocks: [CreateBlock(At(NextMonday, 9), At(NextMonday, 10, 15))]);

        var act = () => _handler.Handle(Booking(), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("That time overlaps Training, 09:00 to 10:15.");
        Lock.Locked.Should().ContainSingle();
        Added.Should().BeEmpty();
        Sent.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_BookingOverAnotherAppointment_ThrowsConflict()
    {
        CalendarData = CreateData(NextMonday, appointments: [CalendarTestConstants.CreateAppointment(AppointmentStatus.Booked, At(NextMonday, 10, 15))]);

        var act = () => _handler.Handle(Booking(), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        Added.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_PassesTheTokenToEveryLookup()
    {
        var pathway = GivenPathway();
        var stageId = pathway.StageAt(1).Id;
        using var cts = new CancellationTokenSource();

        await _handler.Handle(Booking(pathway.Id, stageId), cts.Token);

        _patients.Verify(p => p.GetByIdAsync(_patient.Id, cts.Token), Times.Once);
        _pathways.Verify(p => p.GetByIdAsync(pathway.Id, cts.Token), Times.Once);
        Repository.Verify(r => r.GetOpenForStageAsync(stageId, cts.Token), Times.Once);
        Identity.Verify(i => i.GetStaffAsync(cts.Token), Times.Once);
        Repository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), cts.Token), Times.Once);
        Identity.Verify(i => i.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), cts.Token), Times.Once);
    }

    [Test]
    public async Task Handle_PatientRestricted_ThrowsConflictWithoutAddingIt()
    {
        _patient.RestrictedAtUtc = PrivacyTestConstants.FlaggedAtUtc;

        var act = () => _handler.Handle(Booking(), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PatientRules.RestrictedMessage);
        Added.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_PatientAnonymised_ThrowsConflictWithoutAddingIt()
    {
        _patient.AnonymisedAtUtc = PrivacyTestConstants.FlaggedAtUtc;

        var act = () => _handler.Handle(Request(), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PatientRules.AnonymisedMessage);
        Added.Should().BeEmpty();
    }
}
