using FluentAssertions;
using MedApp.UnitTests.Common.Constants;
using MedApp.Application.Appointments.Common;
using MedApp.Domain.Appointments;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Appointments.Common;

[TestFixture]
public sealed class AppointmentWorkflowTests
{
    [TestCase(AppointmentStatus.Requested, true)]
    [TestCase(AppointmentStatus.Booked, false)]
    [TestCase(AppointmentStatus.Attended, false)]
    [TestCase(AppointmentStatus.DidNotAttend, false)]
    [TestCase(AppointmentStatus.Cancelled, false)]
    public void CanBook_Status_OnlyRequested(AppointmentStatus status, bool expected)
    {
        AppointmentWorkflow.CanBook(CreateAppointment(status)).Should().Be(expected);
    }

    [TestCase(AppointmentStatus.Requested, false)]
    [TestCase(AppointmentStatus.Booked, true)]
    [TestCase(AppointmentStatus.Attended, false)]
    [TestCase(AppointmentStatus.DidNotAttend, false)]
    [TestCase(AppointmentStatus.Cancelled, false)]
    public void CanReschedule_Status_OnlyBooked(AppointmentStatus status, bool expected)
    {
        AppointmentWorkflow.CanReschedule(CreateAppointment(status)).Should().Be(expected);
    }

    [TestCase(AppointmentStatus.Booked, true)]
    [TestCase(AppointmentStatus.Attended, true)]
    [TestCase(AppointmentStatus.DidNotAttend, true)]
    [TestCase(AppointmentStatus.Cancelled, false)]
    public void CanRecordOutcome_StartedAppointment_DependsOnTheStatus(AppointmentStatus status, bool expected)
    {
        var appointment = CreateAppointment(status, Now.AddHours(-1));

        AppointmentWorkflow.CanRecordOutcome(appointment, Now).Should().Be(expected);
    }

    [Test]
    public void CanRecordOutcome_NotStartedYet_IsFalse()
    {
        AppointmentWorkflow.CanRecordOutcome(CreateAppointment(AppointmentStatus.Booked, Now.AddMinutes(5)), Now).Should().BeFalse();
    }

    [Test]
    public void CanRecordOutcome_StartingNow_IsTrue()
    {
        AppointmentWorkflow.CanRecordOutcome(CreateAppointment(AppointmentStatus.Booked, Now), Now).Should().BeTrue();
    }

    [Test]
    public void CanRecordOutcome_Requested_IsFalse()
    {
        AppointmentWorkflow.CanRecordOutcome(CreateAppointment(AppointmentStatus.Requested), Now).Should().BeFalse();
    }

    [Test]
    public void WhyOutcomeCantBeRecorded_NotStartedYet_SaysWhenItCanBe()
    {
        AppointmentWorkflow.WhyOutcomeCantBeRecorded(CreateAppointment(AppointmentStatus.Booked, Now.AddHours(1)), Now)
            .Should().Be("Whether the patient attended can be recorded once the appointment has started.");
    }

    [Test]
    public void WhyOutcomeCantBeRecorded_Cancelled_SaysItHasNoOutcome()
    {
        AppointmentWorkflow.WhyOutcomeCantBeRecorded(CreateAppointment(AppointmentStatus.Cancelled, Now.AddHours(-1)), Now)
            .Should().Be("An appointment that's cancelled has no outcome to record.");
    }

    [Test]
    public void WhyOutcomeCantBeRecorded_Started_IsNull()
    {
        AppointmentWorkflow.WhyOutcomeCantBeRecorded(CreateAppointment(AppointmentStatus.Booked, Now.AddHours(-1)), Now).Should().BeNull();
    }

    [TestCase(AppointmentStatus.Requested, true)]
    [TestCase(AppointmentStatus.Booked, true)]
    [TestCase(AppointmentStatus.Attended, false)]
    [TestCase(AppointmentStatus.DidNotAttend, false)]
    [TestCase(AppointmentStatus.Cancelled, false)]
    public void CanCancel_Status_OnlyOpenOnes(AppointmentStatus status, bool expected)
    {
        AppointmentWorkflow.CanCancel(CreateAppointment(status)).Should().Be(expected);
        AppointmentWorkflow.IsOpen(status).Should().Be(expected);
    }

    [TestCase(AppointmentStatus.Requested, true)]
    [TestCase(AppointmentStatus.Booked, true)]
    [TestCase(AppointmentStatus.Attended, true)]
    [TestCase(AppointmentStatus.DidNotAttend, true)]
    [TestCase(AppointmentStatus.Cancelled, false)]
    public void CanEditNotes_Status_AllButCancelled(AppointmentStatus status, bool expected)
    {
        AppointmentWorkflow.CanEditNotes(CreateAppointment(status)).Should().Be(expected);
    }

    [TestCase(AppointmentStatus.Requested, false)]
    [TestCase(AppointmentStatus.Booked, true)]
    [TestCase(AppointmentStatus.Attended, true)]
    [TestCase(AppointmentStatus.DidNotAttend, true)]
    [TestCase(AppointmentStatus.Cancelled, false)]
    public void IsOnCalendar_Status_TakesUpTheirTime(AppointmentStatus status, bool expected)
    {
        AppointmentWorkflow.IsOnCalendar(status).Should().Be(expected);
    }

    [TestCase(AppointmentStatus.Attended, true)]
    [TestCase(AppointmentStatus.DidNotAttend, true)]
    [TestCase(AppointmentStatus.Booked, false)]
    public void IsOutcome_Status_AttendedOrDidNotAttend(AppointmentStatus status, bool expected)
    {
        AppointmentWorkflow.IsOutcome(status).Should().Be(expected);
    }

    [Test]
    public void Describe_MultiWordStatus_IsLowerCaseWords()
    {
        AppointmentWorkflow.Describe(AppointmentStatus.DidNotAttend).Should().Be("did not attend");
        AppointmentWorkflow.Describe(AppointmentStatus.Booked).Should().Be("booked");
    }

    [Test]
    public void OutcomeNames_ListsTheOutcomes()
    {
        AppointmentWorkflow.OutcomeNames.Should().Be("Attended, DidNotAttend");
    }

    [Test]
    public void StageHasOpenVisit_Booked_SaysWhenAndWithWhom()
    {
        AppointmentWorkflow.StageHasOpenVisit(CreateAppointment(AppointmentStatus.Booked, At(NextMonday, 10)), StaffUsername)
            .Should().Be($"This stage already has a visit booked, on Monday 23 November at 10:00 with {StaffUsername}. Reschedule or cancel that one instead.");
    }

    [Test]
    public void StageHasOpenVisit_BookedWithADeletedAccount_SaysWhen()
    {
        AppointmentWorkflow.StageHasOpenVisit(CreateAppointment(AppointmentStatus.Booked, At(NextMonday, 10)), null)
            .Should().Be("This stage already has a visit booked, on Monday 23 November at 10:00. Reschedule or cancel that one instead.");
    }

    [Test]
    public void StageHasOpenVisit_Requested_PointsToRequiresAppointment()
    {
        AppointmentWorkflow.StageHasOpenVisit(CreateAppointment(AppointmentStatus.Requested), null)
            .Should().Be("This stage already has a visit in Requires Appointment. Book that one instead.");
    }

    [Test]
    public void StageHasOpenVisit_NoneFound_SaysToTryAgain()
    {
        AppointmentWorkflow.StageHasOpenVisit(null, null)
            .Should().Be("This stage already has a visit requested or booked. Reload it and try again.");
    }

    [Test]
    public void StageHasBookedVisit_SaysWhenAndWithWhom_AndWhatToDoInstead()
    {
        var booked = CreateAppointment(AppointmentStatus.Booked, At(NextMonday, 11));

        AppointmentWorkflow.StageHasBookedVisit(booked, StaffUsername).Should().Be(
            $"This stage already has a visit booked, on Monday 23 November at 11:00 with {StaffUsername}. " +
            "If that's the one that happened, record its outcome instead; otherwise cancel it first.");
        AppointmentWorkflow.StageHasBookedVisit(booked, null)
            .Should().StartWith("This stage already has a visit booked, on Monday 23 November at 11:00. ");
    }

    // Entered in error

    private static Appointment WalkIn(AppointmentStatus status = AppointmentStatus.Attended, int daysAgo = 3)
    {
        var appointment = CreateAppointment(AppointmentStatus.Attended, At(Today.AddDays(-daysAgo), 10));
        appointment.Status = status;
        appointment.IsRecordedAfterEvent = true;
        return appointment;
    }

    [TestCase(AppointmentStatus.Attended)]
    [TestCase(AppointmentStatus.DidNotAttend)]
    public void CanMarkEnteredInError_AWalkInWithItsOutcome_IsTrue(AppointmentStatus status)
    {
        AppointmentWorkflow.CanMarkEnteredInError(WalkIn(status), Now).Should().BeTrue();
    }

    [Test]
    public void CanMarkEnteredInError_OnTheThirtiethDayButNotTheThirtyFirst()
    {
        AppointmentWorkflow.CanMarkEnteredInError(WalkIn(daysAgo: 30), Now).Should().BeTrue();
        AppointmentWorkflow.WhyCantMarkEnteredInError(WalkIn(daysAgo: 31), Now).Should().Contain("up to 30 days");
    }

    [Test]
    public void CanMarkEnteredInError_ABookedOne_IsFalse()
    {
        var booked = CreateAppointment(AppointmentStatus.Attended, At(Today.AddDays(-1), 10));

        AppointmentWorkflow.WhyCantMarkEnteredInError(booked, Now).Should().StartWith("Only an appointment recorded after the event");
    }

    [TestCase(AppointmentStatus.EnteredInError)]
    [TestCase(AppointmentStatus.Cancelled)]
    public void CanMarkEnteredInError_WithoutItsOutcome_IsFalse(AppointmentStatus status)
    {
        AppointmentWorkflow.CanMarkEnteredInError(WalkIn(status), Now).Should().BeFalse();
    }

    [Test]
    public void EnteredInError_IsOffTheCalendar_NotOpen_AndItsNotesCantChange()
    {
        var withdrawn = WalkIn(AppointmentStatus.EnteredInError);

        AppointmentWorkflow.IsOnCalendar(AppointmentStatus.EnteredInError).Should().BeFalse();
        AppointmentWorkflow.IsOpen(AppointmentStatus.EnteredInError).Should().BeFalse();
        AppointmentWorkflow.CanEditNotes(withdrawn).Should().BeFalse();
        AppointmentWorkflow.CanCancel(withdrawn).Should().BeFalse();
        AppointmentWorkflow.CanRecordOutcome(withdrawn, Now).Should().BeFalse();
        AppointmentWorkflow.Describe(AppointmentStatus.EnteredInError).Should().Be("entered in error");
    }

    // A walk-in for a procedure

    [Test]
    public void WhyStageNotReached_TheCurrentStageOrAnEarlierOne_IsNull_ALaterOneIsNamed()
    {
        var procedure = MedApp.UnitTests.Common.Constants.ProceduresTestConstants.CreateProcedure(MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus.Assigned, finishedStages: 1);

        AppointmentWorkflow.WhyStageNotReached(procedure, procedure.StageAt(1)).Should().BeNull();
        AppointmentWorkflow.WhyStageNotReached(procedure, procedure.StageAt(2)).Should().BeNull();
        AppointmentWorkflow.WhyStageNotReached(procedure, procedure.StageAt(3)).Should()
            .Be($"The '{procedure.StageAt(3).Name}' stage hasn't been reached yet: the current stage is '{procedure.StageAt(2).Name}'. Record the visit for that stage, or without one.");
    }

    [Test]
    public void WhyStageNotReached_EveryStageFinished_IsNullForAny()
    {
        var procedure = MedApp.UnitTests.Common.Constants.ProceduresTestConstants.CreateProcedure(MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus.InReview, finishedStages: 3);

        AppointmentWorkflow.WhyStageNotReached(procedure, procedure.StageAt(3)).Should().BeNull();
    }

    [Test]
    public void BeforeProcedureMessage_SaysWhenItWasCreated()
    {
        AppointmentWorkflow.BeforeProcedureMessage(At(Today, 11)).Should().Be(
            "This visit was before the procedure was created (Monday 16 November at 11:00). Record it without the procedure, or for the procedure it was for.");
    }

    [TestCase("", "Seen.", "Seen.")]
    [TestCase("Needs an interpreter.", "Seen.", "Needs an interpreter.\n\nSeen.")]
    [TestCase("Needs an interpreter.", " ", null)]
    [TestCase("Needs an interpreter.", null, null)]
    public void KeepingNotes_AddsNewNotesAfterTheVisitsOwn(string existing, string? added, string? expected)
    {
        AppointmentWorkflow.KeepingNotes(existing, added).Should().Be(expected);
    }

    [Test]
    public void KeepingNotes_TogetherTooLong_IsAConflict()
    {
        var act = () => AppointmentWorkflow.KeepingNotes(new string('a', 1500), new string('b', 600));

        act.Should().Throw<MedApp.Application.Common.Exceptions.ConflictException>().WithMessage("*the visit's own notes are kept*");
    }
}
