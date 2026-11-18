using System.Text.Json;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Patients.Common;
using MedApp.Application.PatientMessages.Common;
using MedApp.Application.Pathways.Timeline.Common;
using MedApp.Domain.Appointments;
using MedApp.Domain.PatientMessages;
using MedApp.Domain.Patients;
using MedApp.UnitTests.Common.Constants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;
using static MedApp.UnitTests.Common.Constants.PatientMessagesTestConstants;

namespace MedApp.UnitTests.Application.Patients.Common;

/// <summary>
/// Texts and emails to patients, and Did not attend follow-ups, are patient data (centre plan decision 76): exported,
/// anonymised (destination and text replaced), and the follow-up's detail kept from staff on the timeline.
/// </summary>
[TestFixture]
public sealed class PatientMessagesPrivacyTests
{
    private const string FollowUpDetail = "Synthetic follow-up detail about the patient.";

    private static (PatientRecord Record, Appointment Appointment, PatientMessage Sent, PatientMessage Pending, PatientMessage NotSent) Record()
    {
        var patient = Patient();
        var appointment = CalendarTestConstants.CreateAppointment(AppointmentStatus.DidNotAttend, At(NextMonday, 10));
        appointment.Patient = patient;
        appointment.PatientId = patient.Id;
        appointment.DnaFollowUp = DnaFollowUpDecision.ClinicianToReview;
        appointment.DnaFollowUpDetail = FollowUpDetail;
        appointment.DnaFollowUpAtUtc = Now;
        appointment.DnaFollowUpByUserId = StaffUserId;

        var sent = Message(appointment, status: PatientMessageStatus.Delivered);
        var pending = Message(appointment, PatientMessageKind.MissedAppointment, PatientMessageChannel.Email);
        var notSent = new PatientMessage
        {
            Id = Guid.NewGuid(), PatientId = patient.Id, AppointmentId = appointment.Id, Kind = PatientMessageKind.AppointmentMoved,
            Channel = PatientMessageChannel.Text, AppointmentStartUtc = appointment.StartUtc!.Value,
            Status = PatientMessageStatus.NotSent, StatusReason = PatientMessageReasons.NoConsent, CreatedAtUtc = Now,
            RequestedByUserId = StaffUserId,
        };

        var record = PrivacyTestConstants.CreateRecord(patient) with
        {
            Appointments = [appointment],
            PatientMessages = [sent, pending, notSent],
        };
        return (record, appointment, sent, pending, notSent);
    }

    [Test]
    public void Export_ListsEveryMessage_AndTheFollowUpAndTheChoice()
    {
        var (record, _, sent, _, notSent) = Record();

        var export = PatientExportMapper.ToResponse(
            record, new Dictionary<Guid, string> { [StaffUserId] = StaffUsername }, Now, "admin");

        export.Patient.ReminderChannel.Should().Be("TextAndEmail");
        export.Messages.Should().HaveCount(3);
        var first = export.Messages[0];
        first.Should().BeEquivalentTo(new
        {
            Kind = "AppointmentReminder", Channel = "Text", Destination = Mobile, Body = sent.Body, Status = "Delivered",
            RequestedBy = (string?)null
        });
        export.Messages[2].Should().BeEquivalentTo(new { Status = "NotSent", StatusReason = "NoConsent", RequestedBy = StaffUsername });
        export.Appointments.Single().Should().BeEquivalentTo(new
        {
            DnaFollowUp = "ClinicianToReview", DnaFollowUpDetail = FollowUpDetail, DnaFollowUpBy = StaffUsername
        });
        PatientExportMapper.UserIds(record).Should().Contain(StaffUserId);
        notSent.Destination.Should().BeEmpty();
    }

    [Test]
    public void Anonymise_ReplacesDestinationsTextAndTheFollowUpDetail_AndCancelsWhatsWaiting()
    {
        var (record, appointment, sent, pending, notSent) = Record();

        PatientAnonymiser.Apply(record, Now, new ActivityActor(StaffUserId, StaffUsername, null));

        sent.Destination.Should().Be(PatientAnonymiser.MessageDestination);
        sent.Body.Should().Be(PatientAnonymiser.RemovedText);
        sent.Status.Should().Be(PatientMessageStatus.Delivered, "what happened is kept");
        pending.Status.Should().Be(PatientMessageStatus.Cancelled);
        pending.StatusReason.Should().Be(PatientMessageReasons.RecordClosed);
        notSent.Destination.Should().BeEmpty("nothing was there to replace");
        notSent.Body.Should().BeEmpty();
        appointment.DnaFollowUpDetail.Should().Be(PatientAnonymiser.RemovedText);
        appointment.DnaFollowUp.Should().Be(DnaFollowUpDecision.ClinicianToReview, "the decision is a fixed choice");
        record.Patient.ReminderChannel.Should().Be(ReminderChannel.None);
    }

    [Test]
    public void Anonymise_FollowUpHistory_LosesItsDetail()
    {
        var details = PatientAnonymiser.ScrubDetails(
            JsonSerializer.Serialize(new { decision = "Rebook", detail = FollowUpDetail }), isNotesChange: false);

        details.Should().Contain("Rebook").And.NotContain(FollowUpDetail);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Timeline_FollowUp_ShowsTheDecision_AndTheDetailToAdminsOnly(bool isAdmin)
    {
        var entry = new AppointmentEvent
        {
            Id = Guid.NewGuid(), AppointmentId = Guid.NewGuid(), PatientId = Guid.NewGuid(),
            Action = AppointmentEventAction.DnaFollowUpRecorded, UserId = StaffUserId, Username = StaffUsername, AtUtc = Now,
            Details = JsonSerializer.Serialize(new { decision = "NoFurtherAppointment", detail = FollowUpDetail }),
        };

        var response = TimelineMapper.ToResponse(new AppointmentTimelineRow(entry, false, null), isAdmin);

        response.Action.Should().Be("AppointmentDnaFollowUpRecorded");
        response.DnaFollowUp.Should().Be("NoFurtherAppointment");
        response.Reason.Should().Be(isAdmin ? FollowUpDetail : null);
    }
}
