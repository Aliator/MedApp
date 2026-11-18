using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Application.Appointments.Commands.RecordDnaFollowUp;
using MedApp.Application.PatientMessages.Jobs;
using MedApp.Contracts.Appointments.Requests;
using MedApp.Contracts.Appointments.Responses;
using MedApp.Contracts.Clinics.Requests;
using MedApp.Contracts.Clinics.Responses;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MedApp.Contracts.Pathways.Timeline.Responses;
using MedApp.Contracts.Common;
using MedApp.Contracts.PatientMessages.Responses;
using MedApp.Contracts.Patients.Requests;
using MedApp.Contracts.Patients.Responses;
using MedApp.IntegrationTests.Common;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Appointments;

/// <summary>
/// Patient reminders and the Did not attend list (centre plan decisions 128 to 133) against SQL Server: the
/// <c>patient-reminders</c> job's queries (what's due, what's waiting, what's awaiting delivery), a message unique per
/// appointment, kind, channel and start, telling a patient of a cancellation, the Did not attend view's query and its
/// follow-up, and the patient's export. Messages go to a fake sender (<see cref="FakePatientMessageTransport"/>).
/// </summary>
[TestFixture]
public sealed class PatientMessagesTests : IntegrationTest
{
    private const string Mobile = "+447700900321";

    private HttpClient _admin = null!;
    private StaffAccount _staff = null!;

    [SetUp]
    public async Task SetUpAsync()
    {
        _admin = await SignInAsAdminAsync();
        _staff = await CreateStaffAsync(UserRole);
    }

    private async Task<PatientResponse> CreatePatientWithChoiceAsync(string channel)
    {
        var response = await _staff.Client.PostAsJsonAsync("/api/patients", new CreatePatientRequest
        {
            FirstName = UniqueName("First"),
            LastName = UniqueName("Last"),
            DateOfBirth = DateOfBirth,
            Email = $"{Guid.NewGuid():N}@{PatientEmailDomain}",
            MobilePhone = "07700 900321",
            ReminderChannel = channel,
        });
        return await response.ReadAsAsync<PatientResponse>(HttpStatusCode.Created);
    }

    private async Task RunRemindersAsync() =>
        (await _admin.PostAsync($"/api/jobs/{PatientRemindersJob.JobName}/run", null)).StatusCode.Should().Be(HttpStatusCode.OK);

    private async Task<List<PatientMessageResponse>> MessagesAsync(Guid appointmentId) =>
        await (await _staff.Client.GetAsync($"{AppointmentUrl(appointmentId)}/messages")).ReadAsAsync<List<PatientMessageResponse>>();

    [Test]
    public async Task Reminders_TheDayBefore_AreMadeSentAndDelivered_OnceOnly()
    {
        var patient = await CreatePatientWithChoiceAsync("TextAndEmail");
        patient.ReminderChannel.Should().Be("TextAndEmail");
        patient.MobilePhone.Should().Be(Mobile);
        var start = NextBookableStart();
        var appointment = await BookNewAppointmentAsync(_staff.Client, patient.Id, _staff.UserId, start);
        appointment.PatientReminderChannel.Should().Be("TextAndEmail");

        // 10:00 the day before: inside the sending hours, and within two days of it.
        Clock.SetUtcNow(start.AddHours(-23));
        await RunRemindersAsync();
        await RunRemindersAsync();

        var messages = await MessagesAsync(appointment.Id);
        messages.Should().HaveCount(2);
        messages.Should().AllSatisfy(m =>
        {
            m.Kind.Should().Be("AppointmentReminder");
            m.Status.Should().Be("Delivered");
            m.AppointmentStartUtc.Should().Be(start);
            m.Body.Should().Contain("MedApp Integration Tests: reminder of your appointment on").And.NotContain(patient.LastName);
        });
        messages.Single(m => m.Channel == "Text").Destination.Should().Be(Mobile);
        messages.Single(m => m.Channel == "Email").Subject.Should().Be("Appointment reminder");
        Api.PatientMessages.Sent.Should().HaveCount(2, "a second run sends nothing again");
    }

    [Test]
    public async Task Reminders_NoChoice_AreRecordedAsNotSent()
    {
        var patient = await CreatePatientWithChoiceAsync("None");
        var start = NextBookableStart();
        var appointment = await BookNewAppointmentAsync(_staff.Client, patient.Id, _staff.UserId, start);
        var other = await CreatePatientWithChoiceAsync("Text");
        var texted = await BookNewAppointmentAsync(_staff.Client, other.Id, _staff.UserId, start.AddHours(1));

        Clock.SetUtcNow(start.AddDays(-1).AddHours(1));
        await RunRemindersAsync();

        var message = (await MessagesAsync(appointment.Id)).Should().ContainSingle().Subject;
        message.Status.Should().Be("NotSent");
        message.StatusReason.Should().Be("NoConsent");
        message.Destination.Should().BeEmpty();
        Api.PatientMessages.Sent.Should().ContainSingle(m => m.AppointmentId == texted.Id);
    }

    [Test]
    public async Task Reminders_PatientOptsInLater_ReplacesTheNotSentOne()
    {
        var patient = await CreatePatientWithChoiceAsync("None");
        var start = NextBookableStart();
        var appointment = await BookNewAppointmentAsync(_staff.Client, patient.Id, _staff.UserId, start);
        Clock.SetUtcNow(start.AddHours(-23));
        await RunRemindersAsync();
        (await MessagesAsync(appointment.Id)).Single().Status.Should().Be("NotSent");

        (await _staff.Client.PatchAsJsonAsync($"/api/patients/{patient.Id}", new UpdatePatientRequest
        {
            FirstName = patient.FirstName, LastName = patient.LastName, DateOfBirth = patient.DateOfBirth, Email = patient.Email,
            ReminderChannel = "Text",
        })).StatusCode.Should().Be(HttpStatusCode.OK);
        await RunRemindersAsync();
        await RunRemindersAsync();

        var message = (await MessagesAsync(appointment.Id)).Should().ContainSingle().Subject;
        message.Should().BeEquivalentTo(new { Channel = "Text", Status = "Delivered", Destination = Mobile });
        Api.PatientMessages.Sent.Should().ContainSingle();
    }

    [Test]
    public async Task MovedToAnotherSitesRoomAtTheSameTime_TellsThePatientTheNewPlace()
    {
        var first = await CreateSiteAsync(_admin);
        var firstRoom = await CreateRoomAsync(_admin, first.Id);
        var second = await CreateSiteAsync(_admin);
        var secondRoom = await CreateRoomAsync(_admin, second.Id);
        var patient = await CreatePatientWithChoiceAsync("Text");
        var start = NextBookableStart();
        var appointment = await (await _staff.Client.PostAsJsonAsync("/api/appointments", new CreateAppointmentRequest
        {
            PatientId = patient.Id, StaffUserId = _staff.UserId, StartUtc = start, EndUtc = start.AddMinutes(AppointmentMinutes), RoomId = firstRoom.Id,
        })).ReadAsAsync<AppointmentResponse>(HttpStatusCode.Created);
        Clock.SetUtcNow(start.AddHours(-23));
        await RunRemindersAsync();

        (await _staff.Client.PostAsJsonAsync($"{AppointmentUrl(appointment.Id)}/book", new BookAppointmentRequest
        {
            StaffUserId = _staff.UserId, StartUtc = start, EndUtc = start.AddMinutes(AppointmentMinutes), ExpectedStatus = "Booked",
            RoomId = secondRoom.Id, TellPatient = true,
        })).StatusCode.Should().Be(HttpStatusCode.OK);
        await RunRemindersAsync();

        var messages = await MessagesAsync(appointment.Id);
        messages.Single(m => m.Kind == "AppointmentMoved").Should().Match<PatientMessageResponse>(m =>
            m.Status == "Delivered" && m.Body.Contains(second.Name) && !m.Body.Contains(first.Name));
        messages.Where(m => m.Kind == "AppointmentReminder").Select(m => m.Body.Contains(second.Name))
            .Should().BeEquivalentTo([false, true], "the first reminder named the first site; a new one names the second");
    }

    [Test]
    public async Task Cancelling_AndTellingThePatient_SendsTheCancellationOnTheNextRun()
    {
        var patient = await CreatePatientWithChoiceAsync("Text");
        var appointment = await BookNewAppointmentAsync(_staff.Client, patient.Id, _staff.UserId, NextBookableStart());

        var cancelled = await _staff.Client.PostAsJsonAsync($"{AppointmentUrl(appointment.Id)}/cancel",
            new CancelAppointmentRequest { Reason = "Synthetic: clinician away.", TellPatient = true });
        cancelled.StatusCode.Should().Be(HttpStatusCode.OK);

        var waiting = (await MessagesAsync(appointment.Id)).Should().ContainSingle().Subject;
        waiting.Should().BeEquivalentTo(new { Kind = "AppointmentCancelled", Status = "Pending", RequestedByUsername = _staff.Username });
        waiting.Body.Should().NotContain("clinician away", "a message never says why");

        // Even at night: a cancellation can't wait for the morning.
        Clock.SetUtcNow(UkAt(UkToday, 22));
        await RunRemindersAsync();

        (await MessagesAsync(appointment.Id)).Single().Status.Should().Be("Delivered");
    }

    [Test]
    public async Task CancellingASession_AndTellingThePatients_StagesTheirMessages()
    {
        var site = await CreateSiteAsync(_admin);
        var room = await CreateRoomAsync(_admin, site.Id);
        var date = NextWorkingDay(2);
        var session = await (await _admin.PostAsJsonAsync("/api/clinics/sessions", new ClinicSessionRequest
        {
            StaffUserId = _staff.UserId,
            RoomId = room.Id,
            SpecialtyId = MedApp.Domain.Specialties.Specialty.GeneralId,
            StartUtc = UkAt(date, 9),
            EndUtc = UkAt(date, 10),
            SlotMinutes = 30,
        })).ReadAsAsync<ClinicSessionResponse>(HttpStatusCode.Created);
        var patient = await CreatePatientWithChoiceAsync("Text");
        var booked = await (await _staff.Client.PostAsJsonAsync("/api/appointments", new CreateAppointmentRequest
        {
            PatientId = patient.Id, StaffUserId = _staff.UserId, StartUtc = UkAt(date, 9), EndUtc = UkAt(date, 9, 30), ClinicSessionId = session.Id,
        })).ReadAsAsync<AppointmentResponse>(HttpStatusCode.Created);

        (await _admin.PostAsJsonAsync($"/api/clinics/sessions/{session.Id}/cancel",
                new CancelClinicSessionRequest { Reason = "Synthetic: room closed.", ExpectedLastUpdated = session.LastUpdated, TellPatients = true }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var message = (await MessagesAsync(booked.Id)).Should().ContainSingle().Subject;
        message.Kind.Should().Be("AppointmentCancelled");
        message.Body.Should().Contain(site.Name);
    }

    [Test]
    public async Task DidNotAttend_IsListedWithItsMissedCount_UntilNoFurtherAppointmentIsRecorded()
    {
        var pathway = await CreatePatientWithPathwayAsync(_admin);
        var start = NextBookableStart();
        var first = await BookNewAppointmentAsync(_staff.Client, pathway.PatientId, _staff.UserId, start, pathwayId: pathway.Id);
        var second = await BookNewAppointmentAsync(_staff.Client, pathway.PatientId, _staff.UserId, start.AddHours(2), pathwayId: pathway.Id);
        MoveClockPast(start.AddHours(2));
        (await PostOutcomeAsync(_staff.Client, first.Id, "DidNotAttend")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await PostOutcomeAsync(_staff.Client, second.Id, "DidNotAttend")).StatusCode.Should().Be(HttpStatusCode.OK);

        var listed = await SearchAppointmentsAsync(_staff.Client, "view=didNotAttend&sort=start&ascending=true");
        // The first has a later appointment on its pathway (the second), so only the second waits for a follow-up.
        var item = listed.Should().ContainSingle(a => a.PatientId == pathway.PatientId).Subject;
        item.Id.Should().Be(second.Id);
        item.MissedOnPathwayCount.Should().Be(2);
        item.CanRecordDnaFollowUp.Should().BeTrue();

        var noDetail = await _staff.Client.PostAsJsonAsync($"{AppointmentUrl(second.Id)}/dna-follow-up",
            new RecordDnaFollowUpRequest { Decision = "NoFurtherAppointment" });
        (await noDetail.ReadAsAsync<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>(HttpStatusCode.BadRequest))
            .Errors.Values.SelectMany(e => e).Should().Contain(RecordDnaFollowUpValidator.DetailRequiredMessage);

        var review = await (await _staff.Client.PostAsJsonAsync($"{AppointmentUrl(second.Id)}/dna-follow-up",
            new RecordDnaFollowUpRequest { Decision = "ClinicianToReview" })).ReadAsAsync<AppointmentResponse>();
        review.DnaFollowUp.Should().Be("ClinicianToReview");
        review.DnaFollowUpByUsername.Should().Be(_staff.Username);
        (await SearchAppointmentsAsync(_staff.Client, "view=didNotAttend")).Should().Contain(a => a.Id == second.Id, "a review still needs doing");

        (await _staff.Client.PostAsJsonAsync($"{AppointmentUrl(second.Id)}/dna-follow-up",
                new RecordDnaFollowUpRequest { Decision = "NoFurtherAppointment", Detail = "Synthetic: agreed with the clinician." }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await SearchAppointmentsAsync(_staff.Client, "view=didNotAttend")).Should().NotContain(a => a.PatientId == pathway.PatientId);

        // The pathway itself is untouched.
        (await (await _staff.Client.GetAsync(PathwayUrl(pathway.Id))).ReadAsAsync<PatientPathwayResponse>())
            .Status.Should().Be(pathway.Status);

        var history = await (await _admin.GetAsync($"/api/patients/{pathway.PatientId}/timeline")).ReadAsAsync<PatientTimelinePageResponse>();
        history.Items.Where(e => e.Action == "AppointmentDnaFollowUpRecorded").Select(e => e.DnaFollowUp)
            .Should().BeEquivalentTo(["ClinicianToReview", "NoFurtherAppointment"]);
    }

    [Test]
    public async Task FollowUp_ForAnAppointmentNotMissed_IsAConflict()
    {
        var patient = await CreatePatientAsync(_staff.Client);
        var appointment = await BookNewAppointmentAsync(_staff.Client, patient.Id, _staff.UserId, NextBookableStart());

        var response = await _staff.Client.PostAsJsonAsync($"{AppointmentUrl(appointment.Id)}/dna-follow-up",
            new RecordDnaFollowUpRequest { Decision = "Rebook" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Export_IncludesTheChoiceAndTheMessages()
    {
        var patient = await CreatePatientWithChoiceAsync("Text");
        var appointment = await BookNewAppointmentAsync(_staff.Client, patient.Id, _staff.UserId, NextBookableStart());
        (await _staff.Client.PostAsJsonAsync($"{AppointmentUrl(appointment.Id)}/cancel",
            new CancelAppointmentRequest { Reason = "Synthetic.", TellPatient = true })).StatusCode.Should().Be(HttpStatusCode.OK);

        var export = await (await _admin.GetAsync($"/api/patients/{patient.Id}/export")).ReadAsAsync<PatientExportResponse>();

        export.Patient.ReminderChannel.Should().Be("Text");
        export.Messages.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            AppointmentId = appointment.Id, Kind = "AppointmentCancelled", Channel = "Text", Destination = Mobile,
            Status = "Pending", RequestedBy = _staff.Username
        });
    }

    [Test]
    public async Task Status_SaysMessagesAreOn()
    {
        var status = await (await _staff.Client.GetAsync("/api/appointments/patient-messages/status")).ReadAsAsync<PatientMessagingStatusResponse>();

        status.Should().BeEquivalentTo(new { Mode = "Offline", IsOn = true, SendsMissedAppointmentMessages = false, ReminderDaysBefore = 2 });
    }
}
