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
public sealed class PatientMessageFactoryTests
{
    private static Appointment AppointmentFor(Patient patient)
    {
        var appointment = CalendarTestConstants.CreateAppointment(AppointmentStatus.Booked, At(NextMonday, 10));
        appointment.Patient = patient;
        appointment.PatientId = patient.Id;
        return appointment;
    }

    [Test]
    public void For_TextAndEmail_MakesOnePendingMessageForEach()
    {
        var patient = Patient();
        var appointment = AppointmentFor(patient);
        var requester = Guid.NewGuid();

        var messages = PatientMessageFactory.For(
            patient, appointment, PatientMessageKind.AppointmentMoved, Site, Options(), Now, requester);

        messages.Should().HaveCount(2);
        messages.Should().AllSatisfy(m =>
        {
            m.Status.Should().Be(PatientMessageStatus.Pending);
            m.Kind.Should().Be(PatientMessageKind.AppointmentMoved);
            m.PatientId.Should().Be(patient.Id);
            m.AppointmentId.Should().Be(appointment.Id);
            m.AppointmentStartUtc.Should().Be(appointment.StartUtc!.Value);
            m.RequestedByUserId.Should().Be(requester);
            m.CreatedAtUtc.Should().Be(Now);
            m.Body.Should().Contain("has moved to Mon 23 Nov at 10:00");
        });
        messages.Single(m => m.Channel == PatientMessageChannel.Text).Destination.Should().Be(Mobile);
        messages.Single(m => m.Channel == PatientMessageChannel.Email).Destination.Should().Be(Email);
        messages.Single(m => m.Channel == PatientMessageChannel.Email).Body.Should().EndWith(PatientMessageTexts.EmailFooter);
    }

    [Test]
    public void For_NoChoice_IsOneNotSentWithNoConsent()
    {
        var patient = Patient(ReminderChannel.None);

        var message = PatientMessageFactory.For(
                patient, AppointmentFor(patient), PatientMessageKind.AppointmentReminder, Site, Options(), Now)
            .Should().ContainSingle().Subject;

        message.Status.Should().Be(PatientMessageStatus.NotSent);
        message.StatusReason.Should().Be(PatientMessageReasons.NoConsent);
        message.Destination.Should().BeEmpty();
        message.Body.Should().BeEmpty();
        message.RequestedByUserId.Should().BeNull();
    }

    [Test]
    public void For_TextChosenWithoutAMobile_IsNotSentWithNoMobile_AndEmailStillGoes()
    {
        var patient = Patient(ReminderChannel.TextAndEmail, mobile: null);

        var messages = PatientMessageFactory.For(
            patient, AppointmentFor(patient), PatientMessageKind.AppointmentReminder, null, Options(), Now);

        messages.Single(m => m.Channel == PatientMessageChannel.Text).Should()
            .Match<PatientMessage>(m => m.Status == PatientMessageStatus.NotSent && m.StatusReason == PatientMessageReasons.NoMobile);
        messages.Single(m => m.Channel == PatientMessageChannel.Email).Status.Should().Be(PatientMessageStatus.Pending);
    }

    [Test]
    public void For_EmailChosenWithoutAnAddress_IsNotSentWithNoEmail()
    {
        var patient = Patient(ReminderChannel.Email, email: "");

        PatientMessageFactory.For(patient, AppointmentFor(patient), PatientMessageKind.AppointmentReminder, null, Options(), Now)
            .Should().ContainSingle().Which.StatusReason.Should().Be(PatientMessageReasons.NoEmail);
    }

    [Test]
    public void For_AppointmentWithoutATime_Throws()
    {
        var patient = Patient();
        var appointment = CalendarTestConstants.CreateAppointment(AppointmentStatus.Requested);

        var act = () => PatientMessageFactory.For(patient, appointment, PatientMessageKind.AppointmentReminder, null, Options(), Now);

        act.Should().Throw<InvalidOperationException>();
    }
}
