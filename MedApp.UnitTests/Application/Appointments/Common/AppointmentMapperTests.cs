using FluentAssertions;
using MedApp.Application.Appointments.Common;
using MedApp.Application.Common.Identity;
using MedApp.Domain.Appointments;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Constants;
using Moq;
using static MedApp.UnitTests.Common.Constants.AppointmentsTestConstants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Appointments.Common;

[TestFixture]
public sealed class AppointmentMapperTests
{
    private static Dictionary<Guid, string> Known => AppointmentsTestConstants.Usernames;

    [Test]
    public void ToResponse_Booked_CopiesEveryField()
    {
        var appointment = CreateAppointment(AppointmentStatus.Booked, At(NextMonday, 10), minutes: 45);
        appointment.Notes = Notes;

        var response = AppointmentMapper.ToResponse(appointment, Known, Now);

        response.Id.Should().Be(appointment.Id);
        response.PatientId.Should().Be(appointment.PatientId);
        response.PatientName.Should().Be($"{PatientFirstName} {PatientLastName}");
        response.Status.Should().Be("Booked");
        response.StaffUserId.Should().Be(StaffUserId);
        response.StaffUsername.Should().Be(StaffUsername);
        response.StartUtc.Should().Be(At(NextMonday, 10));
        response.EndUtc.Should().Be(At(NextMonday, 10, 45));
        response.DurationMinutes.Should().Be(45);
        response.Notes.Should().Be(Notes);
        response.CancelReason.Should().BeNull();
        response.IsAutomatic.Should().BeFalse();
        response.CreatedAtUtc.Should().Be(appointment.CreatedAtUtc);
        response.CreatedByUsername.Should().Be(StaffUsername);
        response.BookedAtUtc.Should().Be(appointment.BookedAtUtc);
        response.BookedByUsername.Should().Be(StaffUsername);
        response.OutcomeAtUtc.Should().BeNull();
        response.OutcomeByUsername.Should().BeNull();
        response.LastUpdated.Should().Be(appointment.LastUpdated);
    }

    [Test]
    public void ToResponse_NotForAProcedure_HasNoProcedureOrStage()
    {
        var response = AppointmentMapper.ToResponse(CreateAppointment(), Known, Now);

        response.ProcedureId.Should().BeNull();
        response.ProcedureTitle.Should().BeNull();
        response.ProcedureDueDate.Should().BeNull();
        response.StageId.Should().BeNull();
        response.StageName.Should().BeNull();
        response.VisitRequirement.Should().BeNull();
    }

    [Test]
    public void ToResponse_ForAProcedureStage_GivesItsTitleDueDateStageAndRequirement()
    {
        var appointment = CreateAppointment();
        var procedure = LinkToProcedure(appointment, requirement: VisitRequirement.Suggested);
        // Due at the end of 22 June UK time (BST), stored as 22:59:59 UTC.
        procedure.DueDateUtc = new DateTime(2026, 6, 22, 22, 59, 59, DateTimeKind.Utc);

        var response = AppointmentMapper.ToResponse(appointment, Known, Now);

        response.ProcedureId.Should().Be(procedure.Id);
        response.ProcedureTitle.Should().Be(ProcedureTitle);
        response.ProcedureDueDate.Should().Be(new DateOnly(2026, 6, 22));
        response.StageId.Should().Be(procedure.StageAt(1).Id);
        response.StageName.Should().Be(StageName);
        response.VisitRequirement.Should().Be("Suggested");
    }

    [Test]
    public void ToResponse_Requested_HasNoStaffTimeOrDuration()
    {
        var response = AppointmentMapper.ToResponse(CreateAppointment(AppointmentStatus.Requested), Known, Now);

        response.Status.Should().Be("Requested");
        response.StaffUserId.Should().BeNull();
        response.StaffUsername.Should().BeNull();
        response.StartUtc.Should().BeNull();
        response.EndUtc.Should().BeNull();
        response.DurationMinutes.Should().BeNull();
        response.BookedByUsername.Should().BeNull();
    }

    [Test]
    public void ToResponse_UsersWithNoAccount_AreDeletedUsers()
    {
        var appointment = CreateAppointment(AppointmentStatus.Attended, Now.AddHours(-2));
        appointment.OutcomeByUserId = Guid.NewGuid();
        appointment.OutcomeAtUtc = Now.AddHours(-1);

        var response = AppointmentMapper.ToResponse(appointment, new Dictionary<Guid, string>(), Now);

        response.StaffUsername.Should().Be("Deleted user");
        response.CreatedByUsername.Should().Be("Deleted user");
        response.BookedByUsername.Should().Be("Deleted user");
        response.OutcomeByUsername.Should().Be("Deleted user");
        response.OutcomeAtUtc.Should().Be(Now.AddHours(-1));
    }

    [Test]
    public void ToResponse_NoPatientLoaded_HasAnEmptyPatientName()
    {
        var appointment = CreateAppointment();
        appointment.Patient = null!;

        AppointmentMapper.ToResponse(appointment, Known, Now).PatientName.Should().BeEmpty();
    }

    [Test]
    public void ToResponse_AutomaticCancelled_CopiesTheFlagAndReason()
    {
        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            Patient = PatientsTestConstants.CreateValidPatient(),
            Status = AppointmentStatus.Cancelled,
            CancelReason = CancelReason,
            IsAutomatic = true,
            CreatedByUserId = Colleague.UserId,
        };

        var response = AppointmentMapper.ToResponse(appointment, Known, Now);

        response.IsAutomatic.Should().BeTrue();
        response.CancelReason.Should().Be(CancelReason);
        response.CreatedByUsername.Should().Be(Colleague.Username);
    }

    [TestCase(AppointmentStatus.Requested, true, false, false, true, true)]
    [TestCase(AppointmentStatus.Booked, false, true, false, true, true)]
    [TestCase(AppointmentStatus.Attended, false, false, false, false, true)]
    [TestCase(AppointmentStatus.DidNotAttend, false, false, false, false, true)]
    [TestCase(AppointmentStatus.Cancelled, false, false, false, false, false)]
    public void ToResponse_FutureAppointment_SaysWhatCanBeDone(
        AppointmentStatus status, bool canBook, bool canReschedule, bool canRecordOutcome, bool canCancel, bool canEditNotes)
    {
        var response = AppointmentMapper.ToResponse(CreateAppointment(status, Now.AddHours(1)), Known, Now);

        response.CanBook.Should().Be(canBook);
        response.CanReschedule.Should().Be(canReschedule);
        response.CanRecordOutcome.Should().Be(canRecordOutcome);
        response.CanCancel.Should().Be(canCancel);
        response.CanEditNotes.Should().Be(canEditNotes);
    }

    [TestCase(AppointmentStatus.Booked, true)]
    [TestCase(AppointmentStatus.Attended, true)]
    [TestCase(AppointmentStatus.DidNotAttend, true)]
    [TestCase(AppointmentStatus.Cancelled, false)]
    public void ToResponse_StartedAppointment_CanRecordAnOutcomeUnlessCancelled(AppointmentStatus status, bool expected)
    {
        var response = AppointmentMapper.ToResponse(CreateAppointment(status, Now.AddMinutes(-30)), Known, Now);

        response.CanRecordOutcome.Should().Be(expected);
    }

    [Test]
    public void UserIds_EveryoneItNames_OnceEach()
    {
        var appointment = CreateAppointment();
        appointment.OutcomeByUserId = Colleague.UserId;
        appointment.BookedByUserId = OtherStaffUserId;

        AppointmentMapper.UserIds(appointment).Should().BeEquivalentTo([StaffUserId, OtherStaffUserId, Colleague.UserId]);
    }

    [Test]
    public void UserIds_Requested_OnlyWhoAskedForIt()
    {
        AppointmentMapper.UserIds(CreateAppointment(AppointmentStatus.Requested)).Should().Equal(StaffUserId);
    }

    [Test]
    public async Task ToResponseAsync_LooksUpItsUsernamesWithTheToken()
    {
        var appointment = CreateAppointment();
        var identity = new Mock<IIdentityReadService>();
        using var cts = new CancellationTokenSource();
        identity.Setup(i => i.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), cts.Token))
            .ReturnsAsync(new Dictionary<Guid, string> { [StaffUserId] = StaffUsername });

        var response = await AppointmentMapper.ToResponseAsync(appointment, identity.Object, Now, cts.Token);

        response.StaffUsername.Should().Be(StaffUsername);
        identity.Verify(i => i.GetUsernamesByIdAsync(
            It.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { StaffUserId })), cts.Token), Times.Once);
    }

    [Test]
    public async Task ToResponsesAsync_SeveralAppointments_LooksUpEveryUsernameAtOnce()
    {
        var mine = CreateAppointment();
        var theirs = CreateAppointment();
        theirs.StaffUserId = OtherStaffUserId;
        var identity = new Mock<IIdentityReadService>();
        using var cts = new CancellationTokenSource();
        identity.Setup(i => i.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), cts.Token))
            .ReturnsAsync(Known);

        var responses = await AppointmentMapper.ToResponsesAsync([mine, theirs], identity.Object, Now, cts.Token);

        responses.Select(r => r.StaffUsername).Should().Equal(StaffUsername, OtherStaffUsername);
        identity.Verify(i => i.GetUsernamesByIdAsync(
            It.Is<IEnumerable<Guid>>(ids => ids.Count() == 2 && ids.Contains(StaffUserId) && ids.Contains(OtherStaffUserId)),
            cts.Token), Times.Once);
    }
}
