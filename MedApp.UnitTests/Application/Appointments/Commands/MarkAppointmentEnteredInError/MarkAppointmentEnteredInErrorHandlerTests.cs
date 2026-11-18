using AutoFixture;
using FluentAssertions;
using MedApp.Application.Appointments.Commands.MarkAppointmentEnteredInError;
using MedApp.Application.Appointments.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Common;
using MedApp.Application.Procedures.Repositories;
using MedApp.Domain.Appointments;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.AppointmentsTestConstants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Appointments.Commands.MarkAppointmentEnteredInError;

[TestFixture]
public sealed class MarkAppointmentEnteredInErrorHandlerTests : AppointmentHandlerTestBase
{
    private const string Reason = "Synthetic reason: recorded on the wrong patient.";

    private Mock<IPatientProcedureRepository> _procedures = null!;
    private MarkAppointmentEnteredInErrorHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _procedures = new Mock<IPatientProcedureRepository>();
        Repository.Setup(r => r.GetForStagesForUpdateAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        Fixture.Inject(_procedures.Object);
        _handler = Fixture.Create<MarkAppointmentEnteredInErrorHandler>();
    }

    /// <summary>A walk-in recorded by <see cref="Colleague"/> three days ago, Attended.</summary>
    private Appointment GivenWalkIn(Guid? stageId = null, Guid? procedureId = null, int daysAgo = 3)
    {
        var appointment = CalendarTestConstants.CreateAppointment(AppointmentStatus.Attended, At(Today.AddDays(-daysAgo), 10), stageId: stageId);
        appointment.IsRecordedAfterEvent = true;
        appointment.BookedByUserId = Colleague.UserId;
        appointment.ProcedureId = procedureId;
        return GivenAppointment(appointment);
    }

    private MarkAppointmentEnteredInErrorCommand Command(Appointment appointment, bool isAdmin = false) =>
        new(appointment.Id, $"  {Reason} ", Colleague, isAdmin);

    private PatientProcedure GivenProcedure(VisitRequirement requirement = VisitRequirement.Required)
    {
        var procedure = ProceduresTestConstants.CreateProcedure(PatientProcedureStatus.Assigned, title: ProcedureTitle);
        procedure.StageAt(1).VisitRequirement = requirement;
        procedure.StageAt(1).Name = StageName;
        _procedures.Setup(p => p.GetByIdAsync(procedure.Id, It.IsAny<CancellationToken>())).ReturnsAsync(procedure);
        return procedure;
    }

    [Test]
    public async Task Handle_ByWhoeverRecordedIt_WithdrawsItWithTheReasonInItsHistory()
    {
        var walkIn = GivenWalkIn();
        using var cts = new CancellationTokenSource();

        var response = await _handler.Handle(Command(walkIn), cts.Token);

        walkIn.Status.Should().Be(AppointmentStatus.EnteredInError);
        walkIn.CancelReason.Should().Be(Reason);
        var entry = SavedEvents.Should().ContainSingle().Subject;
        entry.Action.Should().Be(AppointmentEventAction.EnteredInError);
        entry.Details.Should().Be($"{{\"reason\":\"{Reason}\",\"previousStatus\":\"Attended\"}}");
        entry.UserId.Should().Be(Colleague.UserId);
        SaveTokens.Should().Equal(cts.Token);
        Added.Should().BeEmpty();
        response.Appointment.Status.Should().Be(nameof(AppointmentStatus.EnteredInError));
        response.Appointment.CanMarkEnteredInError.Should().BeFalse();
        response.VisitRequestedAgain.Should().BeFalse();
        response.StageMessage.Should().BeNull();
    }

    [Test]
    public async Task Handle_ByAnAdminWhoDidntRecordIt_IsAllowed()
    {
        var walkIn = GivenWalkIn();
        walkIn.BookedByUserId = Guid.NewGuid();

        await _handler.Handle(Command(walkIn, isAdmin: true), CancellationToken.None);

        walkIn.Status.Should().Be(AppointmentStatus.EnteredInError);
    }

    [Test]
    public async Task Handle_BySomeoneElseOnTheStaff_IsForbidden()
    {
        var walkIn = GivenWalkIn();
        walkIn.BookedByUserId = Guid.NewGuid();

        var act = () => _handler.Handle(Command(walkIn), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>().WithMessage(AppointmentWorkflow.NotYourRecordMessage);
        SavedEvents.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_ABookedAppointment_IsAConflictSayingToCancelIt()
    {
        var booked = GivenAppointment(CalendarTestConstants.CreateAppointment(AppointmentStatus.Attended, At(Today.AddDays(-1), 10)));

        var act = () => _handler.Handle(Command(booked, isAdmin: true), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("Only an appointment recorded after the event*");
    }

    [Test]
    public async Task Handle_MoreThanThirtyDaysAfterIt_IsAConflict()
    {
        var walkIn = GivenWalkIn(daysAgo: 31);

        var act = () => _handler.Handle(Command(walkIn), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*up to 30 days after it*");
    }

    [Test]
    public async Task Handle_AlreadyWithdrawn_IsAConflict()
    {
        var walkIn = GivenWalkIn();
        walkIn.Status = AppointmentStatus.EnteredInError;

        var act = () => _handler.Handle(Command(walkIn), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("An appointment that's entered in error can't be marked entered in error.");
    }

    [Test]
    public async Task Handle_ARestrictedRecord_IsAllowed_AnAnonymisedOneIsNot()
    {
        var restricted = GivenWalkIn();
        restricted.Patient.RestrictedAtUtc = Now.AddDays(-1);
        await _handler.Handle(Command(restricted), CancellationToken.None);
        restricted.Status.Should().Be(AppointmentStatus.EnteredInError);

        var anonymised = GivenWalkIn();
        anonymised.Patient.AnonymisedAtUtc = Now.AddDays(-1);
        var act = () => _handler.Handle(Command(anonymised), CancellationToken.None);
        await act.Should().ThrowAsync<ConflictException>().WithMessage(PatientRules.AnonymisedMessage);
    }

    [Test]
    public async Task Handle_TheCurrentStagesVisit_AsksForItAgainInTheSameSave()
    {
        var procedure = GivenProcedure();
        var stage = procedure.StageAt(1);
        var walkIn = GivenWalkIn(stage.Id, procedure.Id);
        Repository.Setup(r => r.GetForStagesForUpdateAsync(It.Is<IEnumerable<Guid>>(ids => ids.Single() == stage.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync([walkIn]);

        var response = await _handler.Handle(Command(walkIn), CancellationToken.None);

        var visit = Added.Should().ContainSingle().Subject;
        visit.Should().BeEquivalentTo(new
        {
            Status = AppointmentStatus.Requested,
            StageId = (Guid?)stage.Id,
            ProcedureId = (Guid?)procedure.Id,
            PatientId = procedure.PatientId,
            IsAutomatic = true,
        });
        SavedEvents.Select(e => e.Action).Should().Equal(AppointmentEventAction.EnteredInError, AppointmentEventAction.Requested);
        SavedEvents[1].AtUtc.Should().BeAfter(SavedEvents[0].AtUtc);
        Repository.Verify(r => r.SaveAsync(It.IsAny<IEnumerable<AppointmentEvent>>(), It.IsAny<CancellationToken>()), Times.Never);
        response.VisitRequestedAgain.Should().BeTrue();
        response.StageMessage.Should().Be($"The '{StageName}' stage's visit is back in Requires Appointment.");
    }

    [Test]
    public async Task Handle_AStageFinishedWithThisVisit_SaysAnAdminCanUnlockIt()
    {
        var procedure = GivenProcedure();
        procedure.StageAt(1).IsCompleted = true;
        var walkIn = GivenWalkIn(procedure.StageAt(1).Id, procedure.Id);

        var response = await _handler.Handle(Command(walkIn), CancellationToken.None);

        Added.Should().BeEmpty();
        response.VisitRequestedAgain.Should().BeFalse();
        response.StageMessage.Should().Contain("was finished with this visit").And.Contain("an admin can unlock it");
    }

    [Test]
    public async Task Handle_AStageWithoutAVisit_SaysNothing()
    {
        var procedure = GivenProcedure(VisitRequirement.None);
        var walkIn = GivenWalkIn(procedure.StageAt(1).Id, procedure.Id);

        var response = await _handler.Handle(Command(walkIn), CancellationToken.None);

        Added.Should().BeEmpty();
        response.StageMessage.Should().BeNull();
    }

    [Test]
    public async Task Handle_ADeletedProcedure_SaysNothing()
    {
        var walkIn = GivenWalkIn(Guid.NewGuid(), Guid.NewGuid());

        var response = await _handler.Handle(Command(walkIn), CancellationToken.None);

        Added.Should().BeEmpty();
        response.StageMessage.Should().BeNull();
    }
}
