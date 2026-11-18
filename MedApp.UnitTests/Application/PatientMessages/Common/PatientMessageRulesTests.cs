using FluentAssertions;
using MedApp.Application.PatientMessages.Common;
using MedApp.Domain.Appointments;
using MedApp.Domain.PatientMessages;
using MedApp.Domain.Patients;
using MedApp.UnitTests.Common.Constants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;
using static MedApp.UnitTests.Common.Constants.PatientMessagesTestConstants;

namespace MedApp.UnitTests.Application.PatientMessages.Common;

[TestFixture]
public sealed class PatientMessageRulesTests
{
    private Patient _patient = null!;
    private Appointment _appointment = null!;

    [SetUp]
    public void SetUp()
    {
        _patient = Patient();
        _appointment = CalendarTestConstants.CreateAppointment(AppointmentStatus.Booked, At(NextMonday, 10));
        _appointment.Patient = _patient;
    }

    [TestCase(ReminderChannel.None, new PatientMessageChannel[0])]
    [TestCase(ReminderChannel.Text, new[] { PatientMessageChannel.Text })]
    [TestCase(ReminderChannel.Email, new[] { PatientMessageChannel.Email })]
    [TestCase(ReminderChannel.TextAndEmail, new[] { PatientMessageChannel.Text, PatientMessageChannel.Email })]
    public void ChannelsFor_EachChoice_GivesItsChannels(ReminderChannel choice, PatientMessageChannel[] channels) =>
        PatientMessageRules.ChannelsFor(choice).Should().Equal(channels);

    [TestCase(8, 59, false)]
    [TestCase(9, 0, true)]
    [TestCase(18, 59, true)]
    [TestCase(19, 0, false)]
    public void CanSendNow_ReminderInWinter_OnlyFromNineToSeven(int hour, int minute, bool expected)
    {
        var now = At(NextMonday, hour, minute);

        PatientMessageRules.CanSendNow(PatientMessageKind.AppointmentReminder, now).Should().Be(expected);
        PatientMessageRules.CanSendNow(PatientMessageKind.MissedAppointment, now).Should().Be(expected);
        PatientMessageRules.CanSendNow(PatientMessageKind.AppointmentMoved, now).Should().BeTrue();
        PatientMessageRules.CanSendNow(PatientMessageKind.AppointmentCancelled, now).Should().BeTrue();
    }

    [Test]
    public void InSendingHours_SummerTime_CountsInUkTime() =>
        // 08:30 UTC is 09:30 in British Summer Time.
        PatientMessageRules.InSendingHours(new DateTime(2026, 7, 14, 8, 30, 0, DateTimeKind.Utc)).Should().BeTrue();

    [Test]
    public void WhyNotToSend_ReminderStillAsBooked_IsNull() =>
        PatientMessageRules.WhyNotToSend(Message(_appointment), _appointment, _patient, Now).Should().BeNull();

    [Test]
    public void WhyNotToSend_AppointmentMovedSince_NoLongerApplies()
    {
        var message = Message(_appointment);
        _appointment.StartUtc = _appointment.StartUtc!.Value.AddHours(1);

        PatientMessageRules.WhyNotToSend(message, _appointment, _patient, Now).Should().Be(PatientMessageReasons.NoLongerApplies);
    }

    [Test]
    public void WhyNotToSend_ReminderForAnAppointmentThatHasStarted_NoLongerApplies() =>
        PatientMessageRules.WhyNotToSend(Message(_appointment), _appointment, _patient, _appointment.StartUtc!.Value)
            .Should().Be(PatientMessageReasons.NoLongerApplies);

    [Test]
    public void WhyNotToSend_CancelledMessageForACancelledAppointment_IsNull()
    {
        _appointment.Status = AppointmentStatus.Cancelled;

        PatientMessageRules.WhyNotToSend(Message(_appointment, PatientMessageKind.AppointmentCancelled), _appointment, _patient, Now)
            .Should().BeNull();
    }

    [Test]
    public void WhyNotToSend_MissedMessageAfterOutcomeCorrected_NoLongerApplies()
    {
        _appointment.Status = AppointmentStatus.Attended;

        PatientMessageRules.WhyNotToSend(Message(_appointment, PatientMessageKind.MissedAppointment), _appointment, _patient, Now)
            .Should().Be(PatientMessageReasons.NoLongerApplies);
    }

    [Test]
    public void WhyNotToSend_ChannelNoLongerChosen_ChoiceChanged()
    {
        _patient.ReminderChannel = ReminderChannel.Email;

        PatientMessageRules.WhyNotToSend(Message(_appointment), _appointment, _patient, Now).Should().Be(PatientMessageReasons.ChoiceChanged);
    }

    [Test]
    public void WhyNotToSend_MobileRemovedSince_ChoiceChanged()
    {
        _patient.MobilePhone = null;

        PatientMessageRules.WhyNotToSend(Message(_appointment), _appointment, _patient, Now).Should().Be(PatientMessageReasons.ChoiceChanged);
    }

    [Test]
    public void WhyNotToSend_Restricted_ReminderStops_ButACancellationStillGoes()
    {
        _patient.RestrictedAtUtc = Now;

        PatientMessageRules.WhyNotToSend(Message(_appointment), _appointment, _patient, Now).Should().Be(PatientMessageReasons.RecordClosed);

        _appointment.Status = AppointmentStatus.Cancelled;
        PatientMessageRules.WhyNotToSend(Message(_appointment, PatientMessageKind.AppointmentCancelled), _appointment, _patient, Now)
            .Should().BeNull();
    }

    [Test]
    public void WhyNotToSend_Anonymised_RecordClosed()
    {
        _patient.AnonymisedAtUtc = Now;
        _appointment.Status = AppointmentStatus.Cancelled;

        PatientMessageRules.WhyNotToSend(Message(_appointment, PatientMessageKind.AppointmentCancelled), _appointment, _patient, Now)
            .Should().Be(PatientMessageReasons.RecordClosed);
    }
}
