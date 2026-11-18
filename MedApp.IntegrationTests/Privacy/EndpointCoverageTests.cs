using System.Net;
using FluentAssertions;
using MedApp.Contracts.Activity.Responses;
using MedApp.Contracts.Common;
using MedApp.Contracts.Patients.Requests;
using MedApp.Contracts.Pathways.NoteDeletionRequests.Requests;
using MedApp.Contracts.Pathways.PatientPathways.Requests;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MedApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Privacy;

/// <summary>
/// Every endpoint that reads, and what it records in the activity log (the DPIA's risk 1 rests on this list). Each GET
/// endpoint the API maps must be in <see cref="Coverage"/>: one that returns a patient's data names the action it
/// records, and one that doesn't says why it records nothing. A new GET endpoint fails here until it's classified.
/// </summary>
[TestFixture]
public sealed class EndpointCoverageTests : IntegrationTest
{
    /// <summary>Returns patient data, so records this action against each patient it returns.</summary>
    private static Coverage Records(string action) => new(action, null);

    /// <summary>Records nothing, for this reason.</summary>
    private static Coverage Nothing(string why) => new(null, why);

    private sealed record Coverage(string? Action, string? Why);

    /// <summary>Every GET endpoint (and the page fallback), by route pattern.</summary>
    private static readonly Dictionary<string, Coverage> CoverageTable = new()
    {
        // Patients and their records: every read of patient data is recorded.
        ["api/patients/{id:guid}"] = Records("PatientViewed"),
        ["api/patients/search"] = Records("PatientListed"),
        ["api/patients"] = Records("PatientsSearched"), // one entry, filters ["all"] (only the deprecated client calls it)
        ["api/patients/retention-review"] = Records("PatientListed"),
        ["api/patients/{id:guid}/export"] = Records("PatientExported"),
        ["api/patients/{id:guid}/timeline"] = Records("PatientViewed"),
        ["api/patients/{id:guid}/timeline/overview"] = Records("PatientViewed"),
        ["api/pathways/patient-pathways/{id:guid}"] = Records("PathwayViewed"),
        ["api/pathways/patient-pathways/search"] = Records("PatientListed"),
        ["api/pathways/note-deletion-requests/{id:guid}"] = Records("PathwayViewed"), // PatientViewed once the pathway is deleted
        ["api/fhir/Patient/{id}"] = Records("PatientViewed"),
        ["api/fhir/Patient"] = Records("PatientListed"), // and one PatientsSearched entry, via "fhir"
        ["api/appointments/{id:guid}"] = Records("PatientViewed"),
        ["api/appointments/search"] = Records("PatientListed"),
        ["api/calendar"] = Records("PatientListed"), // each patient with an appointment on the calendar; nothing withoutAppointments
        ["api/calendar/team"] = Records("PatientListed"), // each patient with an appointment on any calendar shown
        ["api/attachments"] = Records("AttachmentsViewed"),
        ["api/attachments/{id:guid}/content"] = Records("AttachmentDownloaded"), // and every one in the pathway audit log
        ["api/attachments/{id:guid}/thumbnail"] = Nothing("an image's thumbnail, shown in a list of files that records AttachmentsViewed"),

        // Labels for the side nav's pins: names of records the user already opened and chose to pin, not a read.
        ["api/patients/labels"] = Nothing("labels for pins the user chose; recording them would log views nobody made"),
        ["api/pathways/patient-pathways/labels"] = Nothing("labels for pins the user chose; recording them would log views nobody made"),

        // Admins' own audit reading: the logs themselves.
        ["api/pathways/audit/search"] = Nothing("the admin-only pathway audit log"),
        ["api/activity/search"] = Nothing("the admin-only activity log itself"),
        ["api/pathways/insights"] = Nothing("counts, not patient details"),

        // The user's own data and accounts: no patient data.
        ["api/notifications"] = Nothing("the user's own notifications"),
        ["api/notifications/preferences"] = Nothing("the user's own notification choices"),
        ["api/me/notepad"] = Nothing("the user's own notepad: not linked to a patient, so no patient's read to record"),
        ["api/monitoring/rules"] = Nothing("the admin's own monitoring rules: staff, not patients"),
        ["api/auth/session"] = Nothing("the user's own session"),
        ["api/auth/whoami"] = Nothing("the user's own identity"),
        ["api/auth/me/settings"] = Nothing("the user's own settings"),
        ["api/auth/me/email"] = Nothing("the user's own email address"),
        ["api/auth/me/two-factor"] = Nothing("the user's own two-factor state"),
        ["api/auth/features"] = Nothing("whether this deployment sends email; asked before signing in"),
        ["api/auth/security-settings"] = Nothing("the admins' sign-in rules: configuration"),
        ["api/auth/users"] = Nothing("staff accounts, not patients"),
        ["api/auth/users/search"] = Nothing("staff accounts, not patients"),
        ["api/auth/users/{username}"] = Nothing("staff accounts, not patients"),
        ["api/auth/users/{username}/roles"] = Nothing("staff accounts, not patients"),
        ["api/auth/users/{username}/specialties"] = Nothing("which specialties a member of staff belongs to: staff, not patients"),
        ["api/auth/me/specialties"] = Nothing("the user's own specialties"),
        ["api/auth/roles"] = Nothing("role names"),
        ["api/calendar/staff"] = Nothing("staff accounts and their default weeks, not patients"),
        ["api/calendar/staff/{userId:guid}/week-template"] = Nothing("a member of staff's default week: configuration"),
        ["api/calendar/availability"] = Nothing("staff's free and busy times; an appointment is only \"another appointment\", never a patient"),
        ["api/calendar/meetings/{id:guid}"] = Nothing("a staff meeting: staff, not patients"),
        ["api/demographics/status"] = Nothing("whether the demographics lookup is set up (the lookup itself is a POST, recorded as DemographicsLookedUp)"),

        // Configuration.
        ["api/pathways/patient-pathway-stage/definitions"] = Nothing("stage definitions: configuration"),
        ["api/pathways/patient-pathway-stage/definitions/{id:guid}"] = Nothing("stage definitions: configuration"),
        ["api/pathways/patient-pathway-stage/templates"] = Nothing("templates: configuration"),
        ["api/pathways/patient-pathway-stage/templates/{id:guid}"] = Nothing("templates: configuration"),
        ["api/specialties"] = Nothing("specialties: configuration"),
        ["api/specialties/{id:guid}"] = Nothing("specialties: configuration"),
        ["api/calendar/activity-types"] = Nothing("calendar activity types: configuration"),
        ["api/calendar/activity-types/{id:guid}"] = Nothing("calendar activity types: configuration"),
        ["api/calendar/closed-days"] = Nothing("closed days (bank holidays and the like): configuration"),
        ["api/calendar/closed-days/bookings"] = Nothing("how many appointments, blocks and meetings each member of staff has on a day: counts, never patients"),
        ["api/calendar/staff/{userId:guid}/day-templates"] = Nothing("a member of staff's day templates: configuration"),
        ["api/calendar/day-templates/{id:guid}"] = Nothing("calendar day templates: configuration"),
        ["api/calendar/staff/{userId:guid}/week-templates"] = Nothing("a member of staff's week templates: configuration"),
        ["api/calendar/week-templates/{id:guid}"] = Nothing("calendar week templates: configuration"),

        // Infrastructure.
        ["/api/notifications/hub"] = Nothing("the live notifications connection (it carries notifications already stored)"),
        ["/api/notifications/hub/negotiate"] = Nothing("the live notifications connection"),
        ["{*path:nonfile}"] = Nothing("the app's pages; their data comes from the endpoints above"),
        ["/api/{**path}"] = Nothing("an /api path that matches no endpoint: a 404"),
        ["api/fhir/metadata"] = Nothing("the FHIR API's CapabilityStatement: what it can do, no patient data"),
        ["/api/fhir/{**path}"] = Nothing("a FHIR path that matches no endpoint: a 404 OperationOutcome"),
        ["/health"] = Nothing("health check"),
        ["/alive"] = Nothing("health check"),
    };

    [Test]
    public void EveryGetEndpoint_IsClassified()
    {
        var mapped = Api.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<IHttpMethodMetadata>() is not { } methods || methods.HttpMethods.Contains("GET"))
            .Select(e => e.RoutePattern.RawText ?? string.Empty)
            .Distinct()
            .ToList();

        mapped.Should().Contain("api/patients/{id:guid}", "the scan must see the controllers' endpoints");
        mapped.Where(pattern => !CoverageTable.ContainsKey(pattern)).Should().BeEmpty(
            "every GET endpoint must say what it records in the activity log (add it to the table)");
    }

    [Test]
    public async Task EveryReadOfPatientData_RecordsItsAction()
    {
        var admin = await SignInAsAdminAsync();
        var adminId = await GetUserIdAsync(admin, AdminUsername);
        var pathway = await CreatePatientWithPathwayAsync(admin);
        var patientId = pathway.PatientId;
        // Last changed ten years ago, so it's on the retention review.
        var old = await CreatePatientAsync(admin);
        await ExecuteSqlAsync($"UPDATE Patients SET LastUpdated = DATEADD(year, -10, SYSUTCDATETIME()) WHERE Id = {old.Id}");
        // A synthetic NHS number (the 999 range), so the FHIR search finds them.
        await ExecuteSqlAsync($"UPDATE Patients SET NhsNumber = '9990000018' WHERE Id = {patientId}");
        // An appointment with the admin, so their calendar names the patient.
        var day = NextWorkingDay();
        var appointment = await BookNewAppointmentAsync(admin, patientId, adminId, UkAt(day, 9));
        var file = await UploadAttachmentOkAsync(admin, $"pathwayId={pathway.Id}", SyntheticPdf, "synthetic.pdf");
        // A request to delete a note, whose dialog shows the note and the patient.
        var note = await (await admin.PostAsJsonAsync($"/api/pathways/patient-pathways/{pathway.Id}/notes",
            new AddPathwayNoteRequest { Text = "Synthetic note." })).ReadAsAsync<PatientPathwayNoteResponse>(HttpStatusCode.Created);
        var requested = await (await admin.PostAsJsonAsync($"/api/pathways/patient-pathways/{pathway.Id}/notes/{note.Id}/deletion-requests",
            new RequestNoteDeletionRequest { Reason = "Duplicate" })).ReadAsAsync<PatientPathwayNoteResponse>(HttpStatusCode.Created);

        var calls = new (string Url, string Action, Guid? PatientId)[]
        {
            ($"/api/patients/{patientId}", "PatientViewed", patientId),
            ("/api/patients/search", "PatientListed", patientId),
            ("/api/patients", "PatientsSearched", null),
            ("/api/patients/retention-review", "PatientListed", old.Id),
            ($"/api/patients/{patientId}/export", "PatientExported", patientId),
            ($"/api/patients/{patientId}/timeline", "PatientViewed", patientId),
            ($"/api/patients/{patientId}/timeline/overview", "PatientViewed", patientId),
            ($"/api/pathways/patient-pathways/{pathway.Id}", "PathwayViewed", patientId),
            ("/api/pathways/patient-pathways/search", "PatientListed", patientId),
            ($"/api/pathways/note-deletion-requests/{requested.DeletionRequestId}", "PathwayViewed", patientId),
            ($"/api/fhir/Patient/{patientId}", "PatientViewed", patientId),
            ("/api/fhir/Patient?identifier=https%3A%2F%2Ffhir.nhs.uk%2FId%2Fnhs-number%7C9990000018", "PatientListed", patientId),
            (AppointmentUrl(appointment.Id), "PatientViewed", patientId),
            ("/api/appointments/search", "PatientListed", patientId),
            ($"/api/calendar?staffUserId={adminId}&from={day:yyyy-MM-dd}&to={day:yyyy-MM-dd}", "PatientListed", patientId),
            ($"/api/calendar/team?date={day:yyyy-MM-dd}&staffUserIds={adminId}", "PatientListed", patientId),
            ($"/api/attachments?patientId={patientId}", "AttachmentsViewed", patientId),
            ($"/api/attachments/{file.Id}/content", "AttachmentDownloaded", patientId),
        };
        calls.Select(c => c.Action).Distinct().Should().BeEquivalentTo(
            CoverageTable.Values.Select(c => c.Action).OfType<string>().Distinct(), "each recorded action is called here");

        foreach (var (url, action, expectedPatient) in calls)
        {
            await ExecuteSqlAsync($"DELETE FROM ActivityEntries");

            (await admin.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.OK, url);

            var entries = (await (await admin.GetAsync($"/api/activity/search?PageSize=100&UserId={adminId}&Actions={action}"))
                .ReadAsAsync<PagedResponse<ActivityEntryResponse>>()).Items;
            entries.Should().Contain(e => e.PatientId == expectedPatient, url);
        }
    }

    [Test]
    public async Task CalendarWithoutAppointments_NamesNoPatientsAndRecordsNothing()
    {
        var admin = await SignInAsAdminAsync();
        var adminId = await GetUserIdAsync(admin, AdminUsername);
        var pathway = await CreatePatientWithPathwayAsync(admin);
        var day = NextWorkingDay();
        await BookNewAppointmentAsync(admin, pathway.PatientId, adminId, UkAt(day, 9));
        await ExecuteSqlAsync($"DELETE FROM ActivityEntries");

        // What Apply Template reads: the days, which name no patients.
        var response = await admin.GetAsync($"/api/calendar?staffUserId={adminId}&from={day:yyyy-MM-dd}&to={day:yyyy-MM-dd}&withoutAppointments=true");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain(pathway.PatientId.ToString());

        var entries = (await (await admin.GetAsync($"/api/activity/search?PageSize=100&UserId={adminId}&Actions=PatientListed"))
            .ReadAsAsync<PagedResponse<ActivityEntryResponse>>()).Items;
        entries.Should().BeEmpty();
    }

    [Test]
    public async Task AllPatients_RecordsOneSearchForAll_NotAnEntryPerPatient()
    {
        var admin = await SignInAsAdminAsync();
        await CreatePatientAsync(admin);
        await CreatePatientAsync(admin);
        await ExecuteSqlAsync($"DELETE FROM ActivityEntries");

        (await admin.GetAsync("/api/patients")).StatusCode.Should().Be(HttpStatusCode.OK);

        var entries = (await (await admin.GetAsync("/api/activity/search?PageSize=100&Actions=PatientsSearched&Actions=PatientListed"))
            .ReadAsAsync<PagedResponse<ActivityEntryResponse>>()).Items;
        var entry = entries.Should().ContainSingle().Subject;
        entry.Action.Should().Be("PatientsSearched");
        entry.Details.Should().Contain("\"filters\":[\"all\"]").And.Contain("\"resultCount\":2");
    }
}
