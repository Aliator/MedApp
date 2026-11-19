using System.Net;
using FluentAssertions;
using MedApp.Contracts.Activity.Responses;
using MedApp.Contracts.Common;
using MedApp.Contracts.Referrals.Requests;
using MedApp.Contracts.Referrals.Responses;
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
        ["api/appointments/{id:guid}/messages"] = Records("PatientViewed"), // the texts and emails, with the patient's mobile or address
        ["api/calendar"] = Records("PatientListed"), // each patient with an appointment on the calendar; nothing withoutAppointments
        ["api/calendar/team"] = Records("PatientListed"), // each patient with an appointment on any calendar shown
        ["api/attachments"] = Records("AttachmentsViewed"),
        ["api/attachments/{id:guid}/content"] = Records("AttachmentDownloaded"), // and every one in the pathway audit log
        ["api/referrals/{id:guid}"] = Records("ReferralViewed"), // the referral's ID in the details
        ["api/referrals/search"] = Records("PatientListed"),
        ["api/letters/{id:guid}"] = Records("LetterViewed"), // the letter's ID in the details
        ["api/letters/{id:guid}/preview"] = Records("LetterViewed"), // with preview: true
        ["api/letters/search"] = Records("PatientListed"),
        ["api/mdt/meetings/{meetingId:guid}/items"] = Records("PatientListed"), // each patient on the MDT's list
        ["api/mdt/awaiting-meeting"] = Records("PatientListed"), // each patient whose MDT was cancelled
        ["api/mdt/pathways/{pathwayId:guid}/items"] = Records("PathwayViewed"),
        ["api/consults/pathways/{pathwayId:guid}"] = Records("PathwayViewed"), // a pathway's consults, with their questions and replies
        ["api/consults/incoming"] = Records("PatientListed"), // each patient with an open consult asked of the viewer's teams
        ["api/clinics/sessions/{id:guid}"] = Records("PatientListed"), // each patient booked into one of its slots
        ["api/referrals/awaiting-triage/count"] = Nothing("a count for the side nav: names no patients"),
        ["api/letters/count"] = Nothing("counts for the side nav: names no patients"),
        ["api/specimens/{id:guid}"] = Records("SpecimenViewed"), // its page, label and request sheet; the specimen's ID in the details
        ["api/specimens/search"] = Records("PatientListed"),
        ["api/lab-cases/{id:guid}"] = Records("LabCaseViewed"), // the case's page; its ID and the viewer's access in the details
        ["api/hospital-requests/{id:guid}"] = Records("LabCaseViewed"), // a hospital request (MC18): its case's ID and the access
        ["api/results/{id:guid}"] = Records("ResultViewed"), // the result's ID in the details
        ["api/results/search"] = Records("PatientListed"),
        ["api/results/count"] = Nothing("a count for the side nav: names no patients"),
        ["api/pathways/{id:guid}/consent-records"] = Records("PathwayViewed"), // the pathway page's consent card
        ["api/data-requests/search"] = Records("PatientListed"),
        ["api/data-requests/{id:guid}"] = Records("PatientViewed"), // the request's ID in the details
        ["api/data-requests/{id:guid}/review-items"] = Records("PatientViewed"), // with review: true
        ["api/data-requests/{id:guid}/pack"] = Records("DataRequestPackDownloaded"), // the pack itself, never stored
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
        ["api/jobs"] = Nothing("the scheduled jobs' state: names, times and counts, never patient data"),
        ["api/auth/users"] = Nothing("staff accounts, not patients"),
        ["api/auth/users/search"] = Nothing("staff accounts, not patients"),
        ["api/auth/users/{username}"] = Nothing("staff accounts, not patients"),
        ["api/auth/users/{username}/roles"] = Nothing("staff accounts, not patients"),
        ["api/auth/users/{username}/teams"] = Nothing("which teams a member of staff belongs to: staff, not patients"),
        ["api/auth/me/teams"] = Nothing("the user's own teams"),
        ["api/auth/me/details"] = Nothing("the user's own full name and job title: staff, not patients"),
        ["api/auth/roles"] = Nothing("role names"),
        ["api/roles"] = Nothing("roles and how many hold each: configuration"),
        ["api/roles/{id:guid}"] = Nothing("a role, its permissions and the usernames of who holds it: staff, not patients"),
        ["api/permissions"] = Nothing("the permission catalogue: fixed in code"),
        ["api/calendar/staff"] = Nothing("staff accounts and their default weeks, not patients"),
        ["api/calendar/staff/{userId:guid}/week-template"] = Nothing("a member of staff's default week: configuration"),
        ["api/calendar/staff/{userId:guid}/hours"] = Nothing("a member of staff's working hours: staff data, no patient"),
        ["api/calendar/availability"] = Nothing("staff's free and busy times; an appointment is only \"another appointment\", never a patient"),
        ["api/calendar/meetings/{id:guid}"] = Nothing("a staff meeting: staff, not patients"),
        ["api/mdt/meetings"] = Nothing("MDTs and their counts: staff meetings, names no patients"),
        ["api/appointments/patient-messages/status"] = Nothing("whether patients are texted and emailed here: configuration"),
        ["api/demographics/status"] = Nothing("whether the demographics lookup is set up (the lookup itself is a POST, recorded as DemographicsLookedUp)"),

        // Configuration.
        ["api/pathways/patient-pathway-stage/definitions"] = Nothing("stage definitions: configuration"),
        ["api/pathways/patient-pathway-stage/definitions/{id:guid}"] = Nothing("stage definitions: configuration"),
        ["api/pathways/patient-pathway-stage/templates"] = Nothing("templates: configuration"),
        ["api/pathways/patient-pathway-stage/templates/{id:guid}"] = Nothing("templates: configuration"),
        ["api/teams"] = Nothing("teams: configuration"),
        ["api/teams/{id:guid}"] = Nothing("teams: configuration"),
        ["api/practices"] = Nothing("the GP practice directory: organisations and counts, never patients"),
        ["api/practices/{id:guid}"] = Nothing("one GP practice: an organisation and counts, never patients"),
        ["api/letter-templates"] = Nothing("letter templates: configuration (an organisation's wording, never a patient's)"),
        ["api/letter-templates/{id:guid}"] = Nothing("a letter template: configuration"),
        ["api/letter-templates/merge-fields"] = Nothing("the fixed list of merge fields, with made-up examples"),
        ["api/calendar/activity-types"] = Nothing("calendar activity types: configuration"),
        ["api/calendar/activity-types/{id:guid}"] = Nothing("calendar activity types: configuration"),
        ["api/calendar/closed-days"] = Nothing("closed days (bank holidays and the like): configuration"),
        ["api/sites"] = Nothing("sites and their rooms: configuration"),
        ["api/clinics/sessions"] = Nothing("clinic sessions with counts of their slots: staff time, no patient"),
        ["api/clinics/patterns"] = Nothing("clinic patterns: configuration"),
        ["api/sites/{id:guid}"] = Nothing("a site and its rooms: configuration"),
        ["api/sites/{id:guid}/hours"] = Nothing("a site's opening hours: configuration"),
        ["api/rooms"] = Nothing("rooms: configuration"),
        ["api/specimen-types"] = Nothing("specimen types: configuration"),
        ["api/consent-forms"] = Nothing("consent forms: configuration"),
        ["api/lab-tests"] = Nothing("the lab's test catalogue: configuration"),
        ["api/hospitals"] = Nothing("hospitals (MC18): configuration"),
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
        // An appointment with the clinic's clinician (admins are in no team), so their calendar names the patient.
        var clinician = await ClinicStaffAsync();
        var day = NextWorkingDay();
        var appointment = await BookNewAppointmentAsync(admin, patientId, clinician.UserId, UkAt(day, 9));
        var file = await UploadAttachmentOkAsync(admin, $"pathwayId={pathway.Id}", SyntheticPdf, "synthetic.pdf");
        // A request to delete a note, whose dialog shows the note and the patient.
        var note = await (await (await ClinicalAsync(admin)).PostAsJsonAsync($"/api/pathways/patient-pathways/{pathway.Id}/notes",
            new AddPathwayNoteRequest { Text = "Synthetic note." })).ReadAsAsync<PatientPathwayNoteResponse>(HttpStatusCode.Created);
        var requested = await (await (await ClinicalAsync(admin)).PostAsJsonAsync($"/api/pathways/patient-pathways/{pathway.Id}/notes/{note.Id}/deletion-requests",
            new RequestNoteDeletionRequest { Reason = "Duplicate" })).ReadAsAsync<PatientPathwayNoteResponse>(HttpStatusCode.Created);

        // A referral, for its page and the referral list.
        var referral = await (await (await ClinicalAsync(admin)).PostAsJsonAsync("/api/referrals", new RegisterReferralRequest
        {
            PatientId = patientId, TeamId = MedApp.Domain.Teams.Team.GeneralId, ReceivedOn = UkToday,
            ReferrerPractice = "Synthetic Lane Surgery", RequestedPriority = "Medium", Reason = "Synthetic reason."
        })).ReadAsAsync<ReferralResponse>(HttpStatusCode.Created);

        // A letter, for its page, its preview and the letters list.
        var letterTemplate = await (await admin.PostAsJsonAsync("/api/letter-templates", new MedApp.Contracts.Letters.Requests.CreateLetterTemplateRequest
        {
            Name = UniqueName("Letter"), Kind = "ClinicLetter", Letterhead = "Synthetic Centre", BodySkeleton = "Dear Doctor,", Closing = "Yours,"
        })).ReadAsAsync<MedApp.Contracts.Letters.Responses.LetterTemplateResponse>(HttpStatusCode.Created);
        // A clinician writes it (letters are clinical: the Admin role can't); its preview is theirs, checked below.
        var writer = await CreateStaffAsync("Clinician");
        var letter = await (await writer.Client.PostAsJsonAsync("/api/letters", new MedApp.Contracts.Letters.Requests.CreateLetterRequest
        {
            TemplateId = letterTemplate.Id, PathwayId = pathway.Id
        })).ReadAsAsync<MedApp.Contracts.Letters.Responses.LetterResponse>(HttpStatusCode.Created);

        // A specimen, taken by a nurse (admins don't record specimens) for the clinician (who signs off its result), for
        // its page and the lab's worklist.
        var nurse = await CreateStaffAsync("Nurse");
        var (specimenType, laboratory) = await MedApp.IntegrationTests.Specimens.SpecimenApi.CreateSetupAsync(admin);
        var specimen = await MedApp.IntegrationTests.Specimens.SpecimenApi.RecordAsync(
            nurse.Client, pathway, specimenType.Id, laboratory.Id, writer.UserId);
        // A second sample, sent and received by the lab, which opens its lab case (MC15): for the case's page.
        var lab = await CreateStaffAsync("Lab");
        var forCase = await MedApp.IntegrationTests.Specimens.SpecimenApi.RecordAsync(
            nurse.Client, pathway, specimenType.Id, laboratory.Id, writer.UserId);
        forCase = await (await MedApp.IntegrationTests.Specimens.SpecimenApi.StepAsync(nurse.Client, forCase, "sent"))
            .ReadAsAsync<MedApp.Contracts.Specimens.Responses.SpecimenResponse>();
        forCase = await (await MedApp.IntegrationTests.Specimens.SpecimenApi.StepAsync(lab.Client, forCase, "received"))
            .ReadAsAsync<MedApp.Contracts.Specimens.Responses.SpecimenResponse>();

        // A result from elsewhere (the attached file), for the clinician to sign off: for its page and the results list.
        var result = await (await writer.Client.PostAsJsonAsync("/api/results/external", new MedApp.Contracts.Results.Requests.RecordExternalResultRequest
        {
            AttachmentId = file.Id, Title = "Synthetic CT report", PathwayId = pathway.Id, ResponsibleUserId = writer.UserId
        })).ReadAsAsync<MedApp.Contracts.Results.Responses.ResultResponse>(HttpStatusCode.Created);

        // A data request, with its identity check and review (both by the admin: only admins deal with them), so its
        // pack can be made.
        var dataRequest = await (await admin.PostAsJsonAsync("/api/data-requests", new MedApp.Contracts.DataRequests.CreateDataRequestRequest
        {
            PatientId = patientId, ReceivedOn = UkToday, RequestedByPatient = true
        })).ReadAsAsync<MedApp.Contracts.DataRequests.DataRequestResponse>(HttpStatusCode.Created);
        dataRequest = await (await admin.PostAsJsonAsync($"/api/data-requests/{dataRequest.Id}/identity-check", new MedApp.Contracts.DataRequests.RecordIdentityCheckRequest
        {
            CheckedOn = UkToday, Method = "InPerson", ExpectedVersion = dataRequest.Version
        })).ReadAsAsync<MedApp.Contracts.DataRequests.DataRequestResponse>();
        (await admin.PutAsJsonAsync($"/api/data-requests/{dataRequest.Id}/review", new MedApp.Contracts.DataRequests.RecordDataRequestReviewRequest
        {
            ExpectedVersion = dataRequest.Version
        })).StatusCode.Should().Be(HttpStatusCode.OK);

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
            ($"{AppointmentUrl(appointment.Id)}/messages", "PatientViewed", patientId),
            ($"/api/calendar?staffUserId={clinician.UserId}&from={day:yyyy-MM-dd}&to={day:yyyy-MM-dd}", "PatientListed", patientId),
            ($"/api/calendar/team?date={day:yyyy-MM-dd}&staffUserIds={clinician.UserId}", "PatientListed", patientId),
            ($"/api/attachments?patientId={patientId}", "AttachmentsViewed", patientId),
            ($"/api/attachments/{file.Id}/content", "AttachmentDownloaded", patientId),
            ($"/api/referrals/{referral.Id}", "ReferralViewed", patientId),
            ("/api/referrals/search", "PatientListed", patientId),
            ($"/api/letters/{letter.Id}", "LetterViewed", patientId),
            ($"/api/letters/search?pathwayId={pathway.Id}", "PatientListed", patientId),
            ($"/api/specimens/{specimen.Id}", "SpecimenViewed", patientId),
            ($"/api/lab-cases/{forCase.LabCaseId}", "LabCaseViewed", patientId),
            ("/api/specimens/search?view=ToSend", "PatientListed", patientId),
            ($"/api/pathways/{pathway.Id}/consent-records", "PathwayViewed", patientId),
            ($"/api/results/{result.Id}", "ResultViewed", patientId),
            ("/api/results/search?view=Awaiting", "PatientListed", patientId),
            ("/api/data-requests/search", "PatientListed", patientId),
            ($"/api/data-requests/{dataRequest.Id}", "PatientViewed", patientId),
            ($"/api/data-requests/{dataRequest.Id}/review-items", "PatientViewed", patientId),
            ($"/api/data-requests/{dataRequest.Id}/pack", "DataRequestPackDownloaded", patientId),
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

        // A draft's preview is its author's (or an approver's): recorded as viewing the letter, by them.
        await ExecuteSqlAsync($"DELETE FROM ActivityEntries");
        (await writer.Client.GetAsync($"/api/letters/{letter.Id}/preview")).StatusCode.Should().Be(HttpStatusCode.OK);
        var previews = (await (await admin.GetAsync($"/api/activity/search?PageSize=100&UserId={writer.UserId}&Actions=LetterViewed"))
            .ReadAsAsync<PagedResponse<ActivityEntryResponse>>()).Items;
        previews.Should().Contain(e => e.PatientId == patientId);
    }

    [Test]
    public async Task CalendarWithoutAppointments_NamesNoPatientsAndRecordsNothing()
    {
        var admin = await SignInAsAdminAsync();
        var adminId = await GetUserIdAsync(admin, AdminUsername);
        var clinician = await ClinicStaffAsync();
        var pathway = await CreatePatientWithPathwayAsync(admin);
        var day = NextWorkingDay();
        await BookNewAppointmentAsync(admin, pathway.PatientId, clinician.UserId, UkAt(day, 9));
        await ExecuteSqlAsync($"DELETE FROM ActivityEntries");

        // What Apply Template reads: the days, which name no patients.
        var response = await admin.GetAsync($"/api/calendar?staffUserId={clinician.UserId}&from={day:yyyy-MM-dd}&to={day:yyyy-MM-dd}&withoutAppointments=true");
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
