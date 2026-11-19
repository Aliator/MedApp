using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using MedApp.Contracts.Consent;
using MedApp.Contracts.Consults.Requests;
using MedApp.Contracts.Consults.Responses;
using MedApp.Contracts.Calendar.Requests;
using MedApp.Contracts.Calendar.Responses;
using MedApp.Contracts.Hospitals.Requests;
using MedApp.Contracts.Hospitals.Responses;
using MedApp.Contracts.Lab.Responses;
using MedApp.Contracts.Letters.Requests;
using MedApp.Contracts.Letters.Responses;
using MedApp.Contracts.Mdt.Requests;
using MedApp.Contracts.Mdt.Responses;
using MedApp.Contracts.Pathways.NoteDeletionRequests.Requests;
using MedApp.Contracts.Pathways.PatientPathways.Requests;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Requests;
using MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Responses;
using MedApp.Contracts.Referrals.Requests;
using MedApp.Contracts.Referrals.Responses;
using MedApp.Contracts.Results.Requests;
using MedApp.Contracts.Results.Responses;
using MedApp.Contracts.Specimens.Responses;
using MedApp.Contracts.Teams.Requests;
using MedApp.Contracts.Teams.Responses;
using MedApp.Domain.Teams;
using MedApp.Infrastructure.Data;
using MedApp.IntegrationTests.Common;
using MedApp.IntegrationTests.Specimens;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MedApp.IntegrationTests.Teams;

/// <summary>
/// The world the leak suites (decision 205 of <c>docs/multi-clinic-plan.md</c>) probe: three clinics (A, B and C) and
/// the lab, each with a member holding every permission staff can hold, and team A's records of every kind, their
/// free text marked so a leak is found by searching a response for the marker:
/// <list type="bullet">
/// <item><see cref="Private"/>: a pathway only A sees (its notes, stage notes, referral, visit, file, result, letter,
/// consent, MDT item and note deletion request);</item>
/// <item><see cref="LabShared"/>: a pathway whose sample the lab received, so the lab sees it (a StageOwner grant);</item>
/// <item><see cref="ConsultText"/>: a consult on the private pathway sent to C, replied to and closed;</item>
/// <item><see cref="HospitalText"/>: the lab's hospital request, for a patient known only through it.</item>
/// </list>
/// </summary>
public abstract class TeamLeakTest : IntegrationTest
{
    /// <summary>On team A's own records (and nobody else's).</summary>
    protected const string Private = "CANARY-A-PRIVATE";

    /// <summary>On the pathway team A passed to the lab: the lab may see it, B and C never.</summary>
    protected const string LabShared = "CANARY-A-LABSHARE";

    /// <summary>On the consult A sent to C: C may see its own consult, B never.</summary>
    protected const string ConsultText = "CANARY-A-CONSULT";

    /// <summary>On the lab's hospital request: B and C never see it.</summary>
    protected const string HospitalText = "CANARY-LAB-HOSPITAL";

    /// <summary>Team A's records, the teams and their members.</summary>
    protected sealed record LeakWorld(
        Guid TeamA,
        Guid TeamB,
        Guid TeamC,
        StaffAccount A,
        StaffAccount B,
        StaffAccount C,
        StaffAccount Lab,
        Guid PatientId,
        Guid TemplateId,
        PatientPathwayResponse PrivatePathway,
        Guid NoteId,
        Guid NoteDeletionRequestId,
        PatientPathwayResponse SharedPathway,
        Guid ReferralId,
        Guid AppointmentId,
        Guid AttachmentId,
        Guid ResultId,
        Guid LetterId,
        Guid LetterRecipientId,
        Guid ConsentRecordId,
        Guid MeetingId,
        Guid MdtItemId,
        Guid ConsultId,
        Guid SpecimenId,
        string SpecimenNumber,
        Guid LabCaseId,
        Guid LabCaseStepId,
        Guid HospitalRequestId,
        Guid HospitalPatientId)
    {
        /// <summary>Team A's record IDs that B must never be given (the patient is centre-wide, so isn't one).</summary>
        public IReadOnlyList<Guid> PrivateIds =>
        [
            PrivatePathway.Id, NoteId, NoteDeletionRequestId, SharedPathway.Id, ReferralId, AppointmentId, AttachmentId,
            ResultId, LetterId, ConsentRecordId, MdtItemId, ConsultId, SpecimenId, LabCaseId, HospitalRequestId,
            .. PrivatePathway.Stages.Select(s => s.Id), .. SharedPathway.Stages.Select(s => s.Id),
        ];
    }

    /// <summary>Makes the teams, their members and team A's records, through the API.</summary>
    protected static async Task<LeakWorld> SeedAsync()
    {
        var admin = await SignInAsAdminAsync();
        var teamA = await CreateTeamAsync(admin, "Leak A");
        var teamB = await CreateTeamAsync(admin, "Leak B");
        var teamC = await CreateTeamAsync(admin, "Leak C");
        var a = await CreateClinicStaffAsync(teamA);
        var b = await CreateClinicStaffAsync(teamB);
        var c = await CreateClinicStaffAsync(teamC);
        var lab = await CreateClinicStaffAsync(Team.LabId);

        // Team A's template (two stages) and a patient (centre-wide).
        var stages = new[] { (await CreateStageDefinitionAsync(a.Client)).Id, (await CreateStageDefinitionAsync(a.Client)).Id };
        var template = await (await a.Client.PostAsJsonAsync("/api/pathways/patient-pathway-stage/templates", new CreatePathwayTemplateRequest
        {
            Name = UniqueName("Leak template"), TeamId = teamA, StageDefinitionIdsInOrder = stages,
        })).ReadAsAsync<PathwayTemplateResponse>(HttpStatusCode.Created);
        var patient = await CreatePatientAsync(a.Client);

        // The private pathway: notes, a note deletion request, stage notes.
        var privatePathway = await CreatePathwayAsync(a.Client, patient.Id, template.Id);
        var note = await (await a.Client.PostAsJsonAsync($"{PathwayUrl(privatePathway.Id)}/notes",
            new AddPathwayNoteRequest { Text = $"{Private} note" })).ReadAsAsync<PatientPathwayNoteResponse>(HttpStatusCode.Created);
        var asked = await (await a.Client.PostAsJsonAsync($"{PathwayUrl(privatePathway.Id)}/notes/{note.Id}/deletion-requests",
            new RequestNoteDeletionRequest { Reason = "Duplicate", Detail = $"{Private} deletion detail" }))
            .ReadAsAsync<PatientPathwayNoteResponse>(HttpStatusCode.Created);
        await (await PutStageNotesAsync(a.Client, privatePathway.Id, privatePathway.Stages[0].Id, $"{Private} stage notes"))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);

        // A referral to team A, and a visit on the private pathway with notes.
        var referral = await (await a.Client.PostAsJsonAsync("/api/referrals", new RegisterReferralRequest
        {
            PatientId = patient.Id, TeamId = teamA, ReceivedOn = UkToday, ReferrerPractice = "Synthetic Lane Surgery",
            RequestedPriority = "Medium", Reason = $"{Private} referral reason",
        })).ReadAsAsync<ReferralResponse>(HttpStatusCode.Created);
        var appointment = await (await PostBookingAsync(a.Client, patient.Id, a.UserId, NextBookableStart(),
            pathwayId: privatePathway.Id, stageId: privatePathway.Stages[0].Id, notes: $"{Private} visit notes"))
            .ReadAsAsync<MedApp.Contracts.Appointments.Responses.AppointmentResponse>(HttpStatusCode.Created);

        // A file, a result from it, a letter, and consent.
        var file = await UploadAttachmentOkAsync(a.Client, $"pathwayId={privatePathway.Id}", SyntheticPdf, $"{Private}.pdf");
        var result = await (await a.Client.PostAsJsonAsync("/api/results/external", new RecordExternalResultRequest
        {
            AttachmentId = file.Id, Title = $"{Private} result", PathwayId = privatePathway.Id, ResponsibleUserId = a.UserId,
        })).ReadAsAsync<ResultResponse>(HttpStatusCode.Created);
        var letterTemplate = await (await a.Client.PostAsJsonAsync("/api/letter-templates", new CreateLetterTemplateRequest
        {
            Name = UniqueName("Leak letter"), Kind = "ClinicLetter", Letterhead = "Synthetic Centre", BodySkeleton = "Dear Doctor,", Closing = "Yours,",
            TeamId = teamA,
        })).ReadAsAsync<LetterTemplateResponse>(HttpStatusCode.Created);
        var letter = await (await a.Client.PostAsJsonAsync("/api/letters", new CreateLetterRequest
        {
            TemplateId = letterTemplate.Id, PathwayId = privatePathway.Id,
        })).ReadAsAsync<LetterResponse>(HttpStatusCode.Created);
        letter = await (await a.Client.PutAsJsonAsync($"/api/letters/{letter.Id}", new UpdateLetterRequest
        {
            Letterhead = letter.Letterhead, Body = $"{Private} letter body", Closing = letter.Closing,
            Recipients = [.. letter.Recipients.Select(r => new LetterRecipientRequest { Kind = r.Kind, PracticeId = r.PracticeId, Name = r.Name, AddressLines = r.AddressLines, Postcode = r.Postcode, Email = r.Email })],
            ExpectedVersion = letter.Version,
        })).ReadAsAsync<LetterResponse>();
        var form = await (await a.Client.PostAsJsonAsync("/api/consent-forms", new ConsentFormRequest { Name = UniqueName("Leak consent"), FormVersion = "1" }))
            .ReadAsAsync<ConsentFormResponse>(HttpStatusCode.Created);
        var consent = await (await a.Client.PostAsJsonAsync($"/api/pathways/{privatePathway.Id}/consent-records", new RecordConsentRequest
        {
            StageId = privatePathway.Stages[0].Id, ConsentFormId = form.Id, SignedOn = UkToday, SignedBy = "Patient",
        })).ReadAsAsync<ConsentRecordResponse>(HttpStatusCode.Created);

        // Team A's MDT, with the private pathway listed.
        var start = UkAt(NextWorkingDay(2), 13);
        var meeting = await (await a.Client.PostAsJsonAsync("/api/calendar/meetings", new MeetingRequest
        {
            Title = "Synthetic MDT", Notes = string.Empty, StartUtc = start, EndUtc = start.AddHours(1), AttendeeUserIds = [a.UserId],
            IsMdt = true, TeamId = teamA,
        })).ReadAsAsync<MeetingResponse>(HttpStatusCode.Created);
        var item = await (await a.Client.PostAsJsonAsync("/api/mdt/items", new ListForMdtRequest
        {
            PathwayId = privatePathway.Id, MeetingId = meeting.Id, Question = $"{Private} MDT question",
        })).ReadAsAsync<MdtItemResponse>(HttpStatusCode.Created);

        // A consult to C, replied to and closed.
        var consult = await (await a.Client.PostAsJsonAsync("/api/consults", new RequestConsultRequest
        {
            PathwayId = privatePathway.Id, AskedTeamId = teamC, Question = $"{ConsultText} question",
        })).ReadAsAsync<ConsultResponse>(HttpStatusCode.Created);
        consult = await (await c.Client.PostAsJsonAsync($"/api/consults/{consult.Id}/reply", new ReplyToConsultRequest
        {
            Reply = $"{ConsultText} reply", ExpectedVersion = consult.Version,
        })).ReadAsAsync<ConsultResponse>();
        consult = await (await a.Client.PostAsJsonAsync($"/api/consults/{consult.Id}/close", new ChangeConsultRequest
        {
            ExpectedVersion = consult.Version,
        })).ReadAsAsync<ConsultResponse>();

        // The shared pathway: a note, and a sample the lab received (opening its lab case).
        var shared = await CreatePathwayAsync(a.Client, patient.Id, template.Id);
        await (await a.Client.PostAsJsonAsync($"{PathwayUrl(shared.Id)}/notes", new AddPathwayNoteRequest { Text = $"{LabShared} note" }))
            .ShouldHaveStatusAsync(HttpStatusCode.Created);
        var (type, test) = await SpecimenApi.CreateSetupAsync(a.Client);
        var sample = await SpecimenApi.RecordAsync(a.Client, shared, type.Id, test.Id, a.UserId);
        sample = await (await SpecimenApi.StepAsync(a.Client, sample, "sent")).ReadAsAsync<SpecimenResponse>();
        sample = await (await SpecimenApi.StepAsync(lab.Client, sample, "received")).ReadAsAsync<SpecimenResponse>();
        sample.LabCaseId.Should().NotBeNull("receiving a sample with a test opens its lab case");
        var labCase = await (await lab.Client.GetAsync($"/api/lab-cases/{sample.LabCaseId}")).ReadAsAsync<LabCaseResponse>();

        // The lab's hospital request, for a patient known only through it.
        var hospital = await (await lab.Client.PostAsJsonAsync("/api/hospitals", new HospitalDetailsRequest
        {
            Name = UniqueName("Synthetic hospital"), OdsCode = "RXX99", ReportDeliveryMethod = "Post", DefaultDeadlineDays = 10,
        })).ReadAsAsync<HospitalResponse>(HttpStatusCode.Created);
        var request = await (await lab.Client.PostAsJsonAsync("/api/hospital-requests", new CreateHospitalRequestRequest
        {
            HospitalId = hospital.Id,
            NewPatient = new HospitalPatientRequest { FirstName = "Synthetic", LastName = UniqueName("Hospital"), DateOfBirth = new DateOnly(1970, 1, 1) },
            HospitalPatientNumber = "HOSP-0001", SurgeonName = $"{HospitalText} surgeon", Priority = "Routine",
            AuthoredOn = Now.AddHours(-1), SpecimenTypeId = type.Id, LabTestId = test.Id,
        })).ReadAsAsync<HospitalRequestResponse>(HttpStatusCode.Created);

        return new LeakWorld(
            teamA, teamB, teamC, a, b, c, lab, patient.Id, template.Id, privatePathway, note.Id, asked.DeletionRequestId!.Value,
            await GetPathwayAsync(a.Client, shared.Id), referral.Id, appointment.Id, file.Id, result.Id, letter.Id,
            letter.Recipients[0].Id, consent.Id, meeting.Id, item.Id, consult.Id, sample.Id, sample.Number, labCase.Id,
            labCase.Steps[0].Id, request.Id, request.PatientId);
    }

    private static async Task<Guid> CreateTeamAsync(HttpClient admin, string name) =>
        (await (await admin.PostAsJsonAsync("/api/teams", new CreateTeamRequest { Name = UniqueName(name) }))
            .ReadAsAsync<TeamResponse>(HttpStatusCode.Created)).Id;

    protected static async Task<PatientPathwayResponse> GetPathwayAsync(HttpClient client, Guid pathwayId) =>
        await (await client.GetAsync(PathwayUrl(pathwayId))).ReadAsAsync<PatientPathwayResponse>();

    /// <summary>
    /// A fingerprint of team A's rows (and the lab's hospital requests and cases): any change to one, or a row added,
    /// changes it. Reads never change it.
    /// </summary>
    protected static Task<string> FingerprintAsync(LeakWorld world) =>
        RunInScopeAsync(services => services.GetRequiredService<MedAppDbContext>().Database.SqlQuery<string>($"""
            SELECT CONCAT_WS('|',
                (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM PatientPathways WHERE TeamId = {world.TeamA}),
                (SELECT COUNT(*) FROM PatientPathways WHERE TeamId = {world.TeamA}),
                (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM PatientPathwayStages WHERE PatientPathwayId IN (SELECT Id FROM PatientPathways WHERE TeamId = {world.TeamA})),
                (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM PatientPathwayNotes WHERE PatientPathwayId IN (SELECT Id FROM PatientPathways WHERE TeamId = {world.TeamA})),
                (SELECT COUNT(*) FROM PatientPathwayNotes WHERE PatientPathwayId IN (SELECT Id FROM PatientPathways WHERE TeamId = {world.TeamA})),
                (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM PathwayNoteDeletionRequests WHERE PatientPathwayId IN (SELECT Id FROM PatientPathways WHERE TeamId = {world.TeamA})),
                (SELECT COUNT(*) FROM PatientPathwayStageLinks WHERE PatientPathwayId IN (SELECT Id FROM PatientPathways WHERE TeamId = {world.TeamA})),
                (SELECT COUNT(*) FROM PatientPathwayAssignments WHERE PatientPathwayId IN (SELECT Id FROM PatientPathways WHERE TeamId = {world.TeamA})),
                (SELECT COUNT(*) FROM PathwayAccess WHERE PathwayId IN (SELECT Id FROM PatientPathways WHERE TeamId = {world.TeamA})),
                (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM Referrals WHERE TeamId = {world.TeamA}),
                (SELECT COUNT(*) FROM Referrals WHERE TeamId = {world.TeamA}),
                (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM Appointments WHERE TeamId = {world.TeamA}),
                (SELECT COUNT(*) FROM Appointments WHERE TeamId = {world.TeamA}),
                (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM Attachments WHERE TeamId = {world.TeamA}),
                (SELECT COUNT(*) FROM Attachments WHERE TeamId = {world.TeamA}),
                (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM Results WHERE TeamId = {world.TeamA}),
                (SELECT COUNT(*) FROM Results WHERE TeamId = {world.TeamA}),
                (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM Letters WHERE TeamId = {world.TeamA}),
                (SELECT COUNT(*) FROM Letters WHERE TeamId = {world.TeamA}),
                (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM ConsentRecords WHERE TeamId = {world.TeamA}),
                (SELECT COUNT(*) FROM ConsentRecords WHERE TeamId = {world.TeamA}),
                (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM MdtItems WHERE TeamId = {world.TeamA}),
                (SELECT COUNT(*) FROM MdtItems WHERE PathwayId IN (SELECT Id FROM PatientPathways WHERE TeamId = {world.TeamA})),
                (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM Consults WHERE AskingTeamId = {world.TeamA}),
                (SELECT COUNT(*) FROM Consults WHERE PathwayId IN (SELECT Id FROM PatientPathways WHERE TeamId = {world.TeamA})),
                (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM Specimens),
                (SELECT COUNT(*) FROM Specimens),
                (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM LabCases),
                (SELECT COUNT(*) FROM LabCaseSteps),
                (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM HospitalRequests),
                (SELECT COUNT(*) FROM PatientPathwayAuditEntries WHERE TeamId = {world.TeamA})) AS Value
            """).SingleAsync());

    /// <summary>The GUIDs in a response, lower case.</summary>
    protected static IReadOnlySet<Guid> GuidsIn(string body) =>
        Regex.Matches(body, "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")
            .Select(m => Guid.Parse(m.Value))
            .ToHashSet();

    /// <summary>
    /// A route pattern (<c>api/letters/{id:guid}</c>) with team A's record in each parameter, by the area it's in and
    /// the parameter's name; null when the world has no record for it.
    /// </summary>
    protected static string? WithTeamAIds(string pattern, LeakWorld w)
    {
        var url = "/" + pattern.TrimStart('/');
        var parameters = Regex.Matches(url, @"\{(\w+)(:[^}]*)?\}").Select(m => (Whole: m.Value, Name: m.Groups[1].Value)).ToList();
        foreach (var (whole, name) in parameters)
        {
            Guid? id = (url, name) switch
            {
                _ when name == "stageId" => w.PrivatePathway.Stages[0].Id,
                _ when name == "noteId" => w.NoteId,
                _ when name == "userId" && url.Contains("/assignments/") => w.A.UserId,
                _ when name == "recipientId" => w.LetterRecipientId,
                _ when name == "stepId" => w.LabCaseStepId,
                _ when name == "pathwayId" => w.PrivatePathway.Id,
                _ when name == "meetingId" => w.MeetingId,
                _ when url.StartsWith("/api/pathways/patient-pathways/") => w.PrivatePathway.Id,
                _ when url.StartsWith("/api/pathways/note-deletion-requests/") => w.NoteDeletionRequestId,
                _ when url.StartsWith("/api/pathways/{id") && url.EndsWith("/specimens") => w.SharedPathway.Id,
                _ when url.StartsWith("/api/pathways/{id") => w.PrivatePathway.Id,
                _ when url.StartsWith("/api/appointments/") => w.AppointmentId,
                _ when url.StartsWith("/api/attachments/") => w.AttachmentId,
                _ when url.StartsWith("/api/consent-records/") => w.ConsentRecordId,
                _ when url.StartsWith("/api/consults/") => w.ConsultId,
                _ when url.StartsWith("/api/hospital-requests/") => w.HospitalRequestId,
                _ when url.StartsWith("/api/lab-cases/") => w.LabCaseId,
                _ when url.StartsWith("/api/letters/") => w.LetterId,
                _ when url.StartsWith("/api/mdt/items/") => w.MdtItemId,
                _ when url.StartsWith("/api/referrals/") => w.ReferralId,
                _ when url.StartsWith("/api/results/") => w.ResultId,
                _ when url.StartsWith("/api/specimens/") => w.SpecimenId,
                _ when url.StartsWith("/api/patients/") => w.PatientId,
                _ => null,
            };
            if (id is null)
                return null;
            url = url.Replace(whole, id.Value.ToString());
        }

        return url;
    }
}
