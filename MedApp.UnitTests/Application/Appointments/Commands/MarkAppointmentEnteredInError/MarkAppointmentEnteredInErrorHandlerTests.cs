using MedApp.Application.Common.Identity;
using AutoFixture;
using FluentAssertions;
using MedApp.Application.Appointments.Commands.MarkAppointmentEnteredInError;
using MedApp.Application.Appointments.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Domain.Appointments;
using MedApp.Domain.Notifications;
using MedApp.Domain.Pathways.PatientPathways;
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

    private static readonly Guid AdminId = Guid.Parse("0000000b-0000-0000-0000-0000000000ad");

    private Mock<IPatientPathwayRepository> _pathways = null!;
    private Mock<IPermissionHolders> _holders = null!;
    private MarkAppointmentEnteredInErrorHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _pathways = new Mock<IPatientPathwayRepository>();
        Repository.Setup(r => r.GetForStagesForUpdateAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _holders = new Mock<IPermissionHolders>();
        _holders.Setup(h => h.GetUserIdsAsync(Permission.FinishWithoutVisit, It.IsAny<CancellationToken>())).ReturnsAsync([AdminId, Colleague.UserId]);
        Fixture.Inject(_holders.Object);
        Fixture.Inject(_pathways.Object);
        _handler = Fixture.Create<MarkAppointmentEnteredInErrorHandler>();
    }

    /// <summary>A walk-in recorded by <see cref="Colleague"/> three days ago, Attended.</summary>
    private Appointment GivenWalkIn(Guid? stageId = null, Guid? pathwayId = null, int daysAgo = 3)
    {
        var appointment = CalendarTestConstants.CreateAppointment(AppointmentStatus.Attended, At(Today.AddDays(-daysAgo), 10), stageId: stageId);
        appointment.IsRecordedAfterEvent = true;
        appointment.BookedByUserId = Colleague.UserId;
        appointment.PathwayId = pathwayId;
        return GivenAppointment(appointment);
    }

    private MarkAppointmentEnteredInErrorCommand Command(Appointment appointment, bool isAdmin = false) =>
        new(appointment.Id, $"  {Reason} ", Colleague, isAdmin);

    private PatientPathway GivenPathway(VisitRequirement requirement = VisitRequirement.Required)
    {
        var pathway = PathwaysTestConstants.CreatePathway(PatientPathwayStatus.Assigned, title: PathwayTitle);
        pathway.StageAt(1).VisitRequirement = requirement;
        pathway.StageAt(1).Name = StageName;
        _pathways.Setup(p => p.GetByIdAsync(pathway.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pathway);
        return pathway;
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

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*up to 30 days after it; after that, only an admin can. Ask an admin to correct the record.");
        SavedEvents.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_ByAnAdminMoreThanThirtyDaysAfterIt_IsAllowed()
    {
        var walkIn = GivenWalkIn(daysAgo: 90);

        await _handler.Handle(Command(walkIn, isAdmin: true), CancellationToken.None);

        walkIn.Status.Should().Be(AppointmentStatus.EnteredInError);
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
        var pathway = GivenPathway();
        var stage = pathway.StageAt(1);
        var walkIn = GivenWalkIn(stage.Id, pathway.Id);
        Repository.Setup(r => r.GetForStagesForUpdateAsync(It.Is<IEnumerable<Guid>>(ids => ids.Single() == stage.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync([walkIn]);

        var response = await _handler.Handle(Command(walkIn), CancellationToken.None);

        var visit = Added.Should().ContainSingle().Subject;
        visit.Should().BeEquivalentTo(new
        {
            Status = AppointmentStatus.Requested,
            StageId = (Guid?)stage.Id,
            PathwayId = (Guid?)pathway.Id,
            PatientId = pathway.PatientId,
            IsAutomatic = true,
        });
        SavedEvents.Select(e => e.Action).Should().Equal(AppointmentEventAction.EnteredInError, AppointmentEventAction.Requested);
        SavedEvents[1].AtUtc.Should().BeAfter(SavedEvents[0].AtUtc);
        Repository.Verify(r => r.SaveAsync(It.IsAny<IEnumerable<AppointmentEvent>>(), It.IsAny<CancellationToken>()), Times.Never);
        response.VisitRequestedAgain.Should().BeTrue();
        response.StageMessage.Should().Be($"The '{StageName}' stage's visit is back in Requires Appointment.");
    }

    [Test]
    public async Task Handle_TheCurrentStagesVisitOnARestrictedRecord_AsksForNoVisitAgain()
    {
        var pathway = GivenPathway();
        var walkIn = GivenWalkIn(pathway.StageAt(1).Id, pathway.Id);
        walkIn.Patient.RestrictedAtUtc = Now.AddDays(-1);

        var response = await _handler.Handle(Command(walkIn), CancellationToken.None);

        walkIn.Status.Should().Be(AppointmentStatus.EnteredInError);
        Added.Should().BeEmpty();
        SavedEvents.Select(e => e.Action).Should().Equal(AppointmentEventAction.EnteredInError);
        response.VisitRequestedAgain.Should().BeFalse();
        response.StageMessage.Should().Be("No visit is asked for again while the record is restricted.");
    }

    [Test]
    public async Task Handle_TheStagesVisitAskedForMeanwhile_IsAConflictToTryAgain()
    {
        var pathway = GivenPathway();
        var walkIn = GivenWalkIn(pathway.StageAt(1).Id, pathway.Id);
        Repository
            .Setup(r => r.AddAsync(It.IsAny<Appointment>(), It.IsAny<IEnumerable<AppointmentEvent>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new StageHasOpenAppointmentException(pathway.StageAt(1).Id));

        var act = () => _handler.Handle(Command(walkIn), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(AppointmentWorkflow.RequestedVisitChangedMeanwhileMessage);
        Sent.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_AFinishedRequiredStageLeftWithoutAVisit_TellsTheAssigneesAndAdminsButNotWhoWithdrewIt()
    {
        var pathway = GivenPathway();
        var stage = pathway.StageAt(1);
        stage.IsCompleted = true;
        stage.CompletedAtUtc = At(Today.AddDays(-2), 15);
        pathway.Assignments.Clear();
        pathway.Assignments.Add(new PatientPathwayAssignment { PatientPathwayId = pathway.Id, UserId = StaffUserId });
        var walkIn = GivenWalkIn(stage.Id, pathway.Id);
        using var cts = new CancellationTokenSource();

        var response = await _handler.Handle(Command(walkIn), cts.Token);

        Added.Should().BeEmpty();
        response.VisitRequestedAgain.Should().BeFalse();
        response.StageMessage.Should().Contain("is finished, and now has no attended visit")
            .And.Contain("someone who can unlock stages can unlock it").And.Contain("The pathway's assignees and those who can finish a stage without its visit are told.");
        Sent.Select(s => s.Notification.UserId).Should().Equal(StaffUserId, AdminId);
        Sent.Should().AllSatisfy(s =>
        {
            s.Notification.Kind.Should().Be(NotificationKind.StageVisitWithdrawn);
            s.Notification.Title.Should().Be("Stage visit withdrawn");
            s.Notification.PathwayId.Should().Be(pathway.Id);
            s.Notification.Message.Should().Contain($"'{StageName}'").And.NotContain(PathwayTitle).And.NotContain(Reason);
            s.Token.Should().Be(cts.Token);
        });
    }

    [Test]
    public async Task Handle_AFinishedStageWithAnotherAttendedVisit_TellsNobody()
    {
        var pathway = GivenPathway();
        var stage = pathway.StageAt(1);
        stage.IsCompleted = true;
        var walkIn = GivenWalkIn(stage.Id, pathway.Id);
        var other = CalendarTestConstants.CreateAppointment(AppointmentStatus.Attended, At(Today.AddDays(-5), 10), stageId: stage.Id);
        Repository.Setup(r => r.GetForStagesForUpdateAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([walkIn, other]);

        var response = await _handler.Handle(Command(walkIn), CancellationToken.None);

        response.StageMessage.Should().Be($"The '{StageName}' stage is finished, and still has an attended visit.");
        Sent.Should().BeEmpty();
        _holders.Verify(h => h.GetUserIdsAsync(It.IsAny<Permission>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_AStageWithoutAVisit_SaysNothing()
    {
        var pathway = GivenPathway(VisitRequirement.None);
        var walkIn = GivenWalkIn(pathway.StageAt(1).Id, pathway.Id);

        var response = await _handler.Handle(Command(walkIn), CancellationToken.None);

        Added.Should().BeEmpty();
        response.StageMessage.Should().BeNull();
    }

    [Test]
    public async Task Handle_ADeletedPathway_SaysNothing()
    {
        var walkIn = GivenWalkIn(Guid.NewGuid(), Guid.NewGuid());

        var response = await _handler.Handle(Command(walkIn), CancellationToken.None);

        Added.Should().BeEmpty();
        response.StageMessage.Should().BeNull();
    }
}
