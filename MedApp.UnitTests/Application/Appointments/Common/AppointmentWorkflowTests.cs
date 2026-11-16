using FluentAssertions;
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
}
