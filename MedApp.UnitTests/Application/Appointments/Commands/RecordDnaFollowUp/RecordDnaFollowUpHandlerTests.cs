using System.Text.Json;
using AutoFixture;
using FluentAssertions;
using MedApp.Application.Appointments.Commands.RecordAppointmentOutcome;
using MedApp.Application.Appointments.Commands.RecordDnaFollowUp;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Appointments;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.AppointmentsTestConstants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Appointments.Commands.RecordDnaFollowUp;

[TestFixture]
public sealed class RecordDnaFollowUpHandlerTests : AppointmentHandlerTestBase
{
    private const string Detail = "Synthetic detail: discussed with the clinician.";

    private RecordDnaFollowUpHandler _handler = null!;

    [SetUp]
    public void SetUp() => _handler = Fixture.Create<RecordDnaFollowUpHandler>();

    private Appointment GivenMissed() =>
        GivenAppointment(CalendarTestConstants.CreateAppointment(AppointmentStatus.DidNotAttend, Now.AddDays(-1)));

    [Test]
    public async Task Handle_AppointmentDoesNotExist_ThrowsNotFound()
    {
        var act = () => _handler.Handle(new RecordDnaFollowUpCommand(Guid.NewGuid(), "Rebook", null, Colleague), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [TestCase(AppointmentStatus.Booked)]
    [TestCase(AppointmentStatus.Attended)]
    [TestCase(AppointmentStatus.Cancelled)]
    public async Task Handle_NotMissed_ThrowsConflictAndSavesNothing(AppointmentStatus status)
    {
        var appointment = GivenAppointment(CalendarTestConstants.CreateAppointment(status, Now.AddDays(-1)));

        var act = () => _handler.Handle(new RecordDnaFollowUpCommand(appointment.Id, "Rebook", null, Colleague), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(RecordDnaFollowUpHandler.NotMissedMessage);
        SaveTokens.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_RestrictedRecord_ThrowsConflict()
    {
        var appointment = GivenMissed();
        appointment.Patient.RestrictedAtUtc = Now;

        var act = () => _handler.Handle(new RecordDnaFollowUpCommand(appointment.Id, "Rebook", null, Colleague), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        SaveTokens.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_Missed_RecordsTheDecisionInTheHistoryAndChangesNothingElse()
    {
        var appointment = GivenMissed();
        using var cts = new CancellationTokenSource();

        var response = await _handler.Handle(
            new RecordDnaFollowUpCommand(appointment.Id, "clinicianToReview", $"  {Detail}  ", Colleague), cts.Token);

        appointment.Status.Should().Be(AppointmentStatus.DidNotAttend);
        appointment.DnaFollowUp.Should().Be(DnaFollowUpDecision.ClinicianToReview);
        appointment.DnaFollowUpDetail.Should().Be(Detail);
        appointment.DnaFollowUpAtUtc.Should().Be(Now);
        appointment.DnaFollowUpByUserId.Should().Be(Colleague.UserId);
        var entry = SavedEvents.Should().ContainSingle().Subject;
        entry.Action.Should().Be(AppointmentEventAction.DnaFollowUpRecorded);
        using (var details = JsonDocument.Parse(entry.Details))
        {
            details.RootElement.GetProperty("decision").GetString().Should().Be("ClinicianToReview");
            details.RootElement.GetProperty("detail").GetString().Should().Be(Detail);
        }

        SaveTokens.Should().Equal(cts.Token);
        Sent.Should().BeEmpty();
        response.DnaFollowUp.Should().Be("ClinicianToReview");
        response.DnaFollowUpDetail.Should().Be(Detail);
        response.DnaFollowUpByUsername.Should().Be(Colleague.Username);
        response.CanRecordDnaFollowUp.Should().BeTrue();
    }

    [Test]
    public async Task Handle_SameDecisionAgain_SavesNothing()
    {
        var appointment = GivenMissed();
        appointment.DnaFollowUp = DnaFollowUpDecision.Rebook;

        await _handler.Handle(new RecordDnaFollowUpCommand(appointment.Id, "Rebook", "  ", Colleague), CancellationToken.None);

        SavedEvents.Should().BeEmpty();
        SaveTokens.Should().BeEmpty();
    }

    [Test]
    public async Task OutcomeCorrectedToAttended_ClearsTheFollowUp()
    {
        var appointment = GivenMissed();
        appointment.DnaFollowUp = DnaFollowUpDecision.Rebook;
        appointment.DnaFollowUpDetail = Detail;
        appointment.DnaFollowUpByUserId = Colleague.UserId;
        appointment.DnaFollowUpAtUtc = Now;

        await Fixture.Create<RecordAppointmentOutcomeHandler>()
            .Handle(new RecordAppointmentOutcomeCommand(appointment.Id, "Attended", Colleague), CancellationToken.None);

        appointment.DnaFollowUp.Should().BeNull();
        appointment.DnaFollowUpDetail.Should().BeNull();
        appointment.DnaFollowUpAtUtc.Should().BeNull();
        appointment.DnaFollowUpByUserId.Should().BeNull();
    }
}
