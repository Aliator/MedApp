using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Contracts.Notifications.Responses;
using MedApp.Contracts.Monitoring.Requests;
using MedApp.Application.Patients.Common;
using MedApp.Contracts.Activity.Responses;
using MedApp.Contracts.Appointments.Requests;
using MedApp.Contracts.Appointments.Responses;
using MedApp.Contracts.Common;
using MedApp.Contracts.Patients.Requests;
using MedApp.Contracts.Patients.Responses;
using MedApp.Contracts.Procedures.PatientProcedures.Requests;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MedApp.IntegrationTests.Common;
using MedApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Privacy;

/// <summary>
/// A patient's privacy through the API, with real SQL: restricting a record, anonymising it (across every table the
/// timeline and the export read), the subject access export, and the retention review.
/// </summary>
[TestFixture]
public sealed class PatientPrivacyTests : IntegrationTest
{
    /// <summary>One string as all the free text a test writes, to look for after anonymising.</summary>
    private const string Canary = "privacy-canary-5b1e";

    private HttpClient _admin = null!;

    [SetUp]
    public async Task SignInAsync() => _admin = await SignInAsAdminAsync();

    private static string PatientUrl(Guid id) => $"/api/patients/{id}";

    private Task<HttpResponseMessage> SetRestrictionAsync(Guid patientId, bool set, string? reason, bool confirm = false) =>
        _admin.PutAsJsonAsync(
            $"{PatientUrl(patientId)}/restriction",
            new SetPatientFlagRequest { Set = set, Reason = reason, ConfirmOpenProcedures = confirm });

    [Test]
    public async Task Restriction_RefusesChangesButPausing_UntilLifted()
    {
        var procedure = await CreatePatientWithProcedureAsync(_admin);
        var staff = await CreateStaffAsync(UserRole);

        // A procedure is under way, so the admin must confirm its work stops.
        var unconfirmed = await (await SetRestrictionAsync(procedure.PatientId, true, "Synthetic accuracy check"))
            .ReadAsAsync<ValidationProblemDetails>(HttpStatusCode.BadRequest);
        unconfirmed.Errors.Should().ContainKey(nameof(SetPatientFlagRequest.ConfirmOpenProcedures));
        var restricted = await (await _admin.PutAsJsonAsync($"{PatientUrl(procedure.PatientId)}/restriction",
                new SetPatientFlagRequest { Set = true, Reason = "Synthetic accuracy check", ConfirmOpenProcedures = true }))
            .ReadAsAsync<PatientResponse>();
        restricted.IsRestricted.Should().BeTrue();
        restricted.RestrictionReason.Should().Be("Synthetic accuracy check");

        (await staff.Client.PatchAsJsonAsync(PatientUrl(procedure.PatientId), new UpdatePatientRequest
            {
                FirstName = "Changed", LastName = "Changed", DateOfBirth = DateOfBirth, Email = $"changed@{PatientEmailDomain}",
            }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await PutStageNotesAsync(staff.Client, procedure.Id, procedure.Stages[0].Id, StageNotes)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await FinishStageAsync(staff.Client, procedure.Id, procedure.Stages[0].Id)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var shown = await (await staff.Client.GetAsync(ProcedureUrl(procedure.Id))).ReadAsAsync<PatientProcedureResponse>();
        shown.ReadOnlyReason.Should().Be(PatientRules.RestrictedMessage);
        shown.CanWorkOnStages.Should().BeFalse();
        shown.PatientFlags!.IsRestricted.Should().BeTrue();
        shown.PatientFlags.RestrictionReason.Should().Be("Synthetic accuracy check");
        shown.AllowedStatusChanges.Should().BeEquivalentTo("Blocked", "Cancelled");

        // Care can still be paused: blocked with a reason, but not unblocked while restricted.
        (await staff.Client.PostAsJsonAsync($"{ProcedureUrl(procedure.Id)}/status", new ChangePatientProcedureStatusRequest { Status = "Blocked", Reason = BlockReason }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await staff.Client.PostAsJsonAsync($"{ProcedureUrl(procedure.Id)}/status", new ChangePatientProcedureStatusRequest { Status = "Assigned" }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await SetRestrictionAsync(procedure.PatientId, false, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        var unblocked = await (await staff.Client.GetAsync(ProcedureUrl(procedure.Id))).ReadAsAsync<PatientProcedureResponse>();
        (await staff.Client.PostAsJsonAsync($"{ProcedureUrl(procedure.Id)}/status",
                new ChangePatientProcedureStatusRequest { Status = unblocked.BlockedFromStatus! }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await PutStageNotesAsync(staff.Client, procedure.Id, procedure.Stages[0].Id, StageNotes)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task Restriction_CancellingWithAReason_IsAllowed()
    {
        var procedure = await CreatePatientWithProcedureAsync(_admin);
        (await _admin.PutAsJsonAsync($"{PatientUrl(procedure.PatientId)}/restriction",
                new SetPatientFlagRequest { Set = true, Reason = "Synthetic check", ConfirmOpenProcedures = true }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var cancelled = await (await _admin.PostAsJsonAsync($"{ProcedureUrl(procedure.Id)}/status",
                new ChangePatientProcedureStatusRequest { Status = "Cancelled", Reason = "Synthetic: no longer needed" }))
            .ReadAsAsync<PatientProcedureResponse>();

        cancelled.Status.Should().Be("Cancelled");
        cancelled.AllowedStatusChanges.Should().BeEmpty("archiving isn't a pause, so it waits until the restriction is lifted");
    }

    [Test]
    public async Task Restriction_RefusesAppointmentChanges_ButCancelling()
    {
        var patient = await CreatePatientAsync(_admin);
        var staff = await CreateStaffAsync(UserRole);
        var booked = await BookNewAppointmentAsync(_admin, patient.Id, staff.UserId, NextBookableStart());
        // No procedure is under way, but the open appointment freezes too, so the admin must confirm.
        var unconfirmed = await (await SetRestrictionAsync(patient.Id, true, "Synthetic check"))
            .ReadAsAsync<ValidationProblemDetails>(HttpStatusCode.BadRequest);
        unconfirmed.Errors[nameof(SetPatientFlagRequest.ConfirmOpenProcedures)].Should().ContainSingle()
            .Which.Should().StartWith("This patient has 1 open appointment.");
        (await SetRestrictionAsync(patient.Id, true, "Synthetic check", confirm: true)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await PostBookingAsync(staff.Client, patient.Id, staff.UserId, NextBookableStart().AddHours(2)))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await staff.Client.PostAsJsonAsync("/api/appointments", new CreateAppointmentRequest { PatientId = patient.Id }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await PostRescheduleAsync(staff.Client, booked.Id, staff.UserId, NextBookableStart().AddHours(3)))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await staff.Client.PutAsJsonAsync($"{AppointmentUrl(booked.Id)}/notes", new UpdateAppointmentNotesRequest { Notes = "Changed" }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Care can still be paused: the appointment can be cancelled, with a reason.
        var cancelled = await (await PostCancelAsync(staff.Client, booked.Id, "Synthetic: paused while restricted"))
            .ReadAsAsync<AppointmentResponse>();
        cancelled.Status.Should().Be("Cancelled");
    }

    [Test]
    public async Task Restriction_ProceduresUnderWay_ConfirmationCountsTheOpenAppointmentsToo()
    {
        var procedure = await CreatePatientWithProcedureAsync(_admin);
        var staff = await CreateStaffAsync(UserRole);
        await RequestAppointmentAsync(_admin, procedure.PatientId, procedure.Id);
        await BookNewAppointmentAsync(_admin, procedure.PatientId, staff.UserId, NextBookableStart());
        var cancelled = await RequestAppointmentAsync(_admin, procedure.PatientId);
        (await PostCancelAsync(_admin, cancelled.Id, "Synthetic: not needed")).StatusCode.Should().Be(HttpStatusCode.OK);

        var unconfirmed = await (await SetRestrictionAsync(procedure.PatientId, true, "Synthetic check"))
            .ReadAsAsync<ValidationProblemDetails>(HttpStatusCode.BadRequest);

        unconfirmed.Errors[nameof(SetPatientFlagRequest.ConfirmOpenProcedures)].Should().ContainSingle()
            .Which.Should().StartWith("This patient has 1 procedure under way and 2 open appointments.");
    }

    [Test]
    public async Task Restriction_NothingUnderWay_NeedsNoConfirmation()
    {
        var patient = await CreatePatientAsync(_admin);

        (await SetRestrictionAsync(patient.Id, true, "Synthetic check")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task Restriction_Staff_Is403()
    {
        var patient = await CreatePatientAsync(_admin);
        var staff = await CreateStaffAsync(UserRole);

        (await staff.Client.PutAsJsonAsync($"{PatientUrl(patient.Id)}/restriction", new SetPatientFlagRequest { Set = true, Reason = "No" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Anonymise_LeavesNoneOfTheFreeTextAnywhere_AndRecordsWhy()
    {
        var patient = await CreatePatientAsync(_admin, firstName: "Canary", lastName: Canary);
        var template = await CreateTemplateWithStagesAsync(_admin, 1);
        var procedure = await CreateProcedureAsync(_admin, patient.Id, template.Id);
        var staff = await CreateStaffAsync(UserRole);
        (await PutStageNotesAsync(_admin, procedure.Id, procedure.Stages[0].Id, $"Notes about {Canary}")).StatusCode.Should().Be(HttpStatusCode.OK);
        // Files whose names hold the canary: on the patient, the procedure and the stage, and one deleted already.
        var files = new[]
        {
            await UploadAttachmentOkAsync(_admin, $"patientId={patient.Id}", SyntheticPdf, $"Letter for {Canary}.pdf"),
            await UploadAttachmentOkAsync(_admin, $"procedureId={procedure.Id}", SyntheticPdf, $"{Canary} referral.pdf"),
            await UploadAttachmentOkAsync(_admin, $"stageId={procedure.Stages[0].Id}", SyntheticPdf, $"{Canary} consent.pdf"),
            await UploadAttachmentOkAsync(_admin, $"patientId={patient.Id}", SyntheticPdf, $"Old {Canary}.pdf"),
        };
        (await _admin.GetAsync($"/api/attachments/{files[1].Id}/content")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _admin.DeleteAsync($"/api/attachments/{files[3].Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _admin.PatchAsJsonAsync(ProcedureUrl(procedure.Id), new UpdatePatientProcedureRequest
            {
                Title = $"Procedure for {Canary}",
                DueDate = UkToday.AddDays(7),
                Priority = "High",
            }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        // A note about the canary, edited, so two versions hold it; and the old single notes field, kept for a rollout.
        var note = await (await _admin.PostAsJsonAsync($"{ProcedureUrl(procedure.Id)}/notes", new AddProcedureNoteRequest { Text = $"More about {Canary}" }))
            .ReadAsAsync<PatientProcedureNoteResponse>(HttpStatusCode.Created);
        (await _admin.PutAsJsonAsync($"{ProcedureUrl(procedure.Id)}/notes/{note.Id}",
                new EditProcedureNoteRequest { Text = $"Even more about {Canary}", ExpectedVersion = 1 }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        await ExecuteSqlAsync($"UPDATE PatientProcedures SET Notes = {"Legacy notes about " + Canary} WHERE Id = {procedure.Id}");
        // Another admin watching cancellations, so they have a monitoring notification about this procedure too.
        var watcher = await CreateStaffAsync(AdminRole, UserRole);
        (await watcher.Client.PostAsJsonAsync("/api/monitoring/rules", new SaveMonitoringRuleRequest { Event = "ProcedureCancelled" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        // Assigned, so the staff member has a notification with the procedure's title.
        (await _admin.PostAsync($"{ProcedureUrl(procedure.Id)}/assignments/{staff.UserId}", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        // An appointment for the procedure with the staff member (so they're told, under fixed titles), with notes,
        // cancelled with a reason; and one not for a procedure, waiting to be booked, whose notes were changed.
        var booked = await (await PostBookingAsync(_admin, patient.Id, staff.UserId, NextBookableStart(), procedureId: procedure.Id,
                notes: $"Visit notes about {Canary}"))
            .ReadAsAsync<AppointmentResponse>(HttpStatusCode.Created);
        (await PostCancelAsync(_admin, booked.Id, $"Cancelled for {Canary}")).StatusCode.Should().Be(HttpStatusCode.OK);
        var requested = await RequestAppointmentAsync(_admin, patient.Id);
        (await _admin.PutAsJsonAsync($"{AppointmentUrl(requested.Id)}/notes", new UpdateAppointmentNotesRequest { Notes = $"Needs {Canary}" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        // Every reason an admin can type, with the canary in it.
        (await _admin.PutAsJsonAsync($"{PatientUrl(patient.Id)}/restriction",
                new SetPatientFlagRequest { Set = true, Reason = $"Restricted for {Canary}", ConfirmOpenProcedures = true }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await SetRestrictionAsync(patient.Id, false, $"Lifted for {Canary}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _admin.PutAsJsonAsync($"{PatientUrl(patient.Id)}/dispute", new SetPatientFlagRequest { Set = true, Reason = $"Disputed by {Canary}" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await _admin.PostAsJsonAsync($"{PatientUrl(patient.Id)}/retention/keep",
                new KeepPatientRecordRequest { KeepUntil = UkToday.AddYears(1), Reason = $"Kept for {Canary}" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await (await staff.Client.GetAsync("/api/notifications")).Content.ReadAsStringAsync()).Should().Contain(Canary, "the canary must be there to be removed");
        var anonymise = new AnonymisePatientRequest
        {
            Reason = "ErasureRequest",
            Note = "Synthetic request, ticket 42",
            Confirmation = $"canary {Canary.ToUpperInvariant()}",
        };

        // Refused while the procedure is under way, then while the appointment waiting to be booked is open.
        (await _admin.PostAsJsonAsync($"{PatientUrl(patient.Id)}/anonymise", anonymise)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await _admin.PostAsJsonAsync($"{ProcedureUrl(procedure.Id)}/status", new ChangePatientProcedureStatusRequest { Status = "Cancelled", Reason = $"Cancelled for {Canary}" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await (await _admin.PostAsJsonAsync($"{PatientUrl(patient.Id)}/anonymise", anonymise)).ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict))
            .Title.Should().Contain("1 open appointment");
        (await PostCancelAsync(_admin, requested.Id, $"Not needed by {Canary}")).StatusCode.Should().Be(HttpStatusCode.OK);
        // Refused without the patient's name, or with a reason not in the list.
        (await _admin.PostAsJsonAsync($"{PatientUrl(patient.Id)}/anonymise", new AnonymisePatientRequest { Reason = "ErasureRequest", Confirmation = "Someone Else" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _admin.PostAsJsonAsync($"{PatientUrl(patient.Id)}/anonymise",
                new AnonymisePatientRequest { Reason = $"Asked by {Canary}", Confirmation = anonymise.Confirmation }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var anonymised = await (await _admin.PostAsJsonAsync($"{PatientUrl(patient.Id)}/anonymise", anonymise)).ReadAsAsync<PatientResponse>();

        anonymised.IsAnonymised.Should().BeTrue();
        anonymised.IsDisputed.Should().BeFalse();
        anonymised.FirstName.Should().Be(PatientAnonymiser.FirstName);
        anonymised.DateOfBirth.Should().Be(new DateOnly(DateOfBirth.Year, 1, 1));
        foreach (var (client, url) in new[]
                 {
                     (_admin, PatientUrl(patient.Id)),
                     (_admin, ProcedureUrl(procedure.Id)),
                     (_admin, $"{PatientUrl(patient.Id)}/timeline"),
                     (_admin, $"{PatientUrl(patient.Id)}/timeline/overview"),
                     (_admin, $"/api/procedures/audit/search?PatientId={patient.Id}&PageSize=100"),
                     (_admin, $"/api/activity/search?PatientId={patient.Id}&PageSize=100"),
                     (_admin, $"{PatientUrl(patient.Id)}/export"),
                     (_admin, AppointmentUrl(booked.Id)),
                     (_admin, AppointmentUrl(requested.Id)),
                     (_admin, $"/api/appointments/search?PatientId={patient.Id}&PageSize=100"),
                     (_admin, $"/api/attachments?patientId={patient.Id}&includeDeleted=true"),
                     (_admin, $"/api/attachments?procedureId={procedure.Id}&includeDeleted=true"),
                     (staff.Client, "/api/notifications"),
                     (watcher.Client, "/api/notifications"),
                     (staff.Client, $"/api/procedures/patient-procedures/search?PatientId={patient.Id}&Statuses=Cancelled"),
                 })
        {
            var body = await (await client.GetAsync(url)).ShouldHaveStatusAsync(HttpStatusCode.OK);
            (await body.Content.ReadAsStringAsync()).Should().NotContainEquivalentOf(Canary, url);
        }

        // The note's versions and the old notes field are scrubbed in the database too (no endpoint reads them all).
        var stored = await RunInScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<MedAppDbContext>();
            var versions = await db.PatientProcedureNoteVersions.AsNoTracking().Where(v => v.PatientId == patient.Id).Select(v => v.Text).ToListAsync();
            var legacy = await db.PatientProcedures.AsNoTracking().Where(p => p.Id == procedure.Id).Select(p => p.LegacyNotes).SingleAsync();
            return (versions, legacy);
        });
        stored.versions.Should().HaveCount(2).And.AllBe(PatientAnonymiser.RemovedText);
        stored.legacy.Should().Be(PatientAnonymiser.RemovedText);

        // Every file is deleted (its bytes wait for the purge), renamed, and can't be downloaded.
        var listed = await (await _admin.GetAsync($"/api/attachments?patientId={patient.Id}&includeDeleted=true"))
            .ReadAsAsync<MedApp.Contracts.Attachments.Responses.AttachmentListResponse>();
        listed.Items.Should().HaveCount(4).And.AllSatisfy(a =>
        {
            a.IsDeleted.Should().BeTrue();
            a.FileName.Should().Be("Anonymised file.pdf");
        });
        (await (await staff.Client.GetAsync($"/api/attachments?patientId={patient.Id}"))
            .ReadAsAsync<MedApp.Contracts.Attachments.Responses.AttachmentListResponse>()).Items.Should().BeEmpty();
        (await _admin.GetAsync($"/api/attachments/{files[0].Id}/content")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await UploadAttachmentAsync(_admin, $"patientId={patient.Id}", SyntheticPdf, "after.pdf")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        // The monitoring notification never held free text, keeps its fixed title, and isn't in the patient's export.
        (await (await _admin.GetAsync($"{PatientUrl(patient.Id)}/export")).Content.ReadAsStringAsync())
            .Should().NotContain("MonitoredProcedureCancelled").And.NotContain(watcher.Username);
        var watched = await (await watcher.Client.GetAsync("/api/notifications")).ReadAsAsync<NotificationsResponse>();
        watched.Items.Should().ContainSingle().Which.Title.Should().Be("Procedure cancelled");
        // The appointment notifications have fixed titles, so they never held the procedure's title.
        var told = await (await staff.Client.GetAsync("/api/notifications")).ReadAsAsync<NotificationsResponse>();
        told.Items.Where(n => n.AppointmentId == booked.Id).Select(n => n.Title)
            .Should().BeEquivalentTo("Appointment booked", "Appointment cancelled");

        var log = await (await _admin.GetAsync($"/api/activity/search?PatientId={patient.Id}&Actions=PatientAnonymised"))
            .ReadAsAsync<PagedResponse<ActivityEntryResponse>>();
        log.Items.Should().ContainSingle().Which.Details.Should().Contain("\"reason\":\"ErasureRequest\"").And.Contain("ticket 42");
        (await _admin.PatchAsJsonAsync(PatientUrl(patient.Id), new UpdatePatientRequest
            {
                FirstName = "Back", LastName = "Again", DateOfBirth = DateOfBirth, Email = $"back@{PatientEmailDomain}",
            }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Export_HoldsTheWholeRecord_IsNeverCached_AndIsRecordedInItsOwnAccessLog()
    {
        var procedure = await CreatePatientWithProcedureAsync(_admin, stages: 2);
        (await PutStageNotesAsync(_admin, procedure.Id, procedure.Stages[0].Id, StageNotes)).StatusCode.Should().Be(HttpStatusCode.OK);
        var appointment = await RequestAppointmentAsync(_admin, procedure.PatientId, procedure.Id);

        var response = await _admin.GetAsync($"{PatientUrl(procedure.PatientId)}/export");

        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        var export = await response.ReadAsAsync<PatientExportResponse>();
        export.Patient.Id.Should().Be(procedure.PatientId);
        export.ExportedBy.Should().Be(AdminUsername);
        var exported = export.Procedures.Should().ContainSingle().Subject;
        exported.Stages.Should().HaveCount(2);
        exported.Stages[0].Notes.Should().Be(StageNotes);
        exported.AssignedTo.Should().BeEmpty();
        export.ProcedureHistory.Select(e => e.Action).Should().Contain(["Created", "StageNotesUpdated"]);
        export.AccessLog.Select(e => e.Action).Should().Contain(["PatientCreated", "PatientExported"]);
        var exportedAppointment = export.Appointments.Should().ContainSingle().Subject;
        exportedAppointment.Id.Should().Be(appointment.Id);
        exportedAppointment.Status.Should().Be("Requested");
        exportedAppointment.CreatedBy.Should().Be(AdminUsername);
        export.AppointmentHistory.Should().ContainSingle().Which.Action.Should().Be("Requested");

        var json = await (await _admin.GetAsync($"{PatientUrl(procedure.PatientId)}/export")).Content.ReadAsStringAsync();
        json.Should().NotContainEquivalentOf("ipAddress").And.NotContainEquivalentOf("userId").And.NotContainEquivalentOf("readAtUtc");
        json.Should().Contain(AdminUsername, "staff are named by username");
    }

    /// <summary>
    /// History details are stored with staff account IDs (an assignment's <c>userId</c>, a rescheduled appointment's
    /// <c>previousStaffUserId</c>); the export leaves every one out, in any field and any format, and keeps the usernames.
    /// </summary>
    [Test]
    public async Task Export_HoldsNoStaffAccountIds_AnywhereInItsJson()
    {
        var procedure = await CreatePatientWithProcedureAsync(_admin, stages: 2);
        var adminId = await GetUserIdAsync(_admin, AdminUsername);
        var staff = await CreateStaffAsync(UserRole);
        (await _admin.PostAsync($"{ProcedureUrl(procedure.Id)}/assignments/{staff.UserId}", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _admin.PostAsync($"{ProcedureUrl(procedure.Id)}/assignments/{adminId}", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _admin.DeleteAsync($"{ProcedureUrl(procedure.Id)}/assignments/{staff.UserId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        // Booked with the staff member, then moved to the admin: the history keeps who it was with before.
        var start = NextBookableStart();
        var appointment = await BookNewAppointmentAsync(_admin, procedure.PatientId, staff.UserId, start, procedureId: procedure.Id);
        await (await PostRescheduleAsync(_admin, appointment.Id, adminId, start.AddHours(1))).ShouldHaveStatusAsync(HttpStatusCode.OK);

        var response = await _admin.GetAsync($"{PatientUrl(procedure.PatientId)}/export");
        var json = await (await response.ShouldHaveStatusAsync(HttpStatusCode.OK)).Content.ReadAsStringAsync();

        foreach (var id in new[] { adminId, staff.UserId })
        {
            foreach (var form in new[] { id.ToString("D"), id.ToString("N"), Convert.ToBase64String(id.ToByteArray()) })
                json.Should().NotContainEquivalentOf(form, "the export holds no staff account IDs");
        }

        json.Should().NotContainEquivalentOf("userId");
        var export = System.Text.Json.JsonSerializer.Deserialize<PatientExportResponse>(
            json, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        export.ProcedureHistory.Where(e => e.Action is "Assigned" or "Unassigned").Select(e => (e.Action, e.Details))
            .Should().Equal(
                ("Assigned", $$"""{"username":"{{staff.Username}}"}"""),
                ("Assigned", $$"""{"username":"{{AdminUsername}}"}"""),
                ("Unassigned", $$"""{"username":"{{staff.Username}}"}"""));
        export.AppointmentHistory.Single(e => e.Action == "Rescheduled").Details
            .Should().Contain($"\"previousStaffUsername\":\"{staff.Username}\"", "who it was with before is still there, by username");
        export.Appointments.Single().StaffUsername.Should().Be(AdminUsername);
    }

    [Test]
    public async Task Export_Staff_Is403()
    {
        var patient = await CreatePatientAsync(_admin);
        var staff = await CreateStaffAsync(UserRole);

        (await staff.Client.GetAsync($"{PatientUrl(patient.Id)}/export")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task RetentionReview_ListsARecordPastItsPeriod_UntilAnAdminKeepsIt()
    {
        var due = await CreatePatientAsync(_admin);
        var recent = await CreatePatientAsync(_admin);
        // Last changed ten years ago: past an adult's eight.
        await ExecuteSqlAsync($"UPDATE Patients SET LastUpdated = DATEADD(year, -10, SYSUTCDATETIME()) WHERE Id = {due.Id}");

        var listed = await (await _admin.GetAsync("/api/patients/retention-review")).ReadAsAsync<PagedResponse<RetentionReviewItemResponse>>();
        var item = listed.Items.Should().ContainSingle().Subject;
        item.PatientId.Should().Be(due.Id);
        item.RetentionClass.Should().Be("Adult");
        listed.Items.Should().NotContain(i => i.PatientId == recent.Id);

        (await _admin.PostAsJsonAsync($"{PatientUrl(due.Id)}/retention/keep",
                new KeepPatientRecordRequest { KeepUntil = UkToday.AddYears(1), Reason = "Synthetic legal hold" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await (await _admin.GetAsync("/api/patients/retention-review")).ReadAsAsync<PagedResponse<RetentionReviewItemResponse>>())
            .Items.Should().BeEmpty();
        var kept = await (await _admin.GetAsync("/api/patients/retention-review?IncludeKept=true"))
            .ReadAsAsync<PagedResponse<RetentionReviewItemResponse>>();
        kept.Items.Should().ContainSingle().Which.KeptReason.Should().Be("Synthetic legal hold");
    }
}
