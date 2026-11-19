using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Contracts.Authentication.Requests;
using MedApp.Contracts.Common;
using MedApp.Contracts.Letters.Requests;
using MedApp.Contracts.Letters.Responses;
using MedApp.Contracts.Notifications.Responses;
using MedApp.Contracts.Pathways.PatientPathwayStages.Definitions.Requests;
using MedApp.Contracts.Pathways.Timeline.Responses;
using MedApp.Contracts.Patients.Requests;
using MedApp.Contracts.Patients.Responses;
using MedApp.Contracts.Practices.Requests;
using MedApp.Contracts.Practices.Responses;
using MedApp.Contracts.Referrals.Requests;
using MedApp.Contracts.Referrals.Responses;
using MedApp.Domain.Teams;
using MedApp.IntegrationTests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Letters;

/// <summary>
/// Letters through the API against SQL Server: written from a template by a nurse, returned and approved by a named
/// clinician (the PDF made by PDFsharp and stored in Azurite), recorded as posted and sent by secure email (a relay
/// kept in memory), the views and count, the stage gate, the records refused and kept, reads logged, the timeline and
/// export, and the referral's "Ask for more information".
/// </summary>
[TestFixture]
public sealed class LetterTests : IntegrationTest
{
    private const string Letters = "/api/letters";

    private HttpClient _admin = null!;
    private StaffAccount _nurse = null!;
    private StaffAccount _clinician = null!;

    [SetUp]
    public async Task SetUpAsync()
    {
        _admin = await SignInAsAdminAsync();
        _nurse = await CreateStaffAsync("Nurse");
        _clinician = await CreateStaffAsync("Clinician");
        await GiveDetailsAsync(_nurse, "Alex Writer", "Specialist Nurse");
        await GiveDetailsAsync(_clinician, "Dr Bo Approver", "Consultant");
    }

    private static async Task GiveDetailsAsync(StaffAccount staff, string fullName, string jobTitle) =>
        await (await staff.Client.PutAsJsonAsync("/api/auth/me/details", new StaffDetailsRequest { FullName = fullName, JobTitle = jobTitle }))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);

    private async Task<Guid> CreateLetterTemplateAsync(string kind = "ClinicLetter") =>
        (await (await _admin.PostAsJsonAsync("/api/letter-templates", new CreateLetterTemplateRequest
            {
                Name = UniqueName("Letter"),
                Kind = kind,
                Letterhead = "Synthetic Specialist Centre\n{{site.name}}",
                BodySkeleton = "Dear Doctor,\n\nRe: {{patient.name}}, born {{patient.dateOfBirth}}\n\n## Plan",
                Closing = "Yours sincerely,\n\n{{approver.name}}\n{{approver.jobTitle}}",
            }))
            .ReadAsAsync<LetterTemplateResponse>(HttpStatusCode.Created)).Id;

    private async Task<PracticeResponse> CreatePracticeAsync() =>
        await (await _admin.PostAsJsonAsync("/api/practices", new CreatePracticeRequest
            {
                Name = UniqueName("Testbrook Surgery"),
                AddressLines = ["2 Madeup Road", "Fictionton"],
                Postcode = "zz2 2zz",
                SecureEmail = "testbrook.surgery@nhs.net",
            }))
            .ReadAsAsync<PracticeResponse>(HttpStatusCode.Created);

    /// <summary>A patient with an address, registered with a practice, and a pathway for them.</summary>
    private async Task<(Guid PatientId, Guid PathwayId, PracticeResponse Practice)> PatientWithPathwayAsync(int stages = 2)
    {
        var practice = await CreatePracticeAsync();
        var patient = await (await _admin.PostAsJsonAsync("/api/patients", new CreatePatientRequest
            {
                FirstName = UniqueName("First"),
                LastName = UniqueName("Last"),
                DateOfBirth = DateOfBirth,
                Email = $"{Guid.NewGuid():N}@{PatientEmailDomain}",
                AddressLines = ["3 Invented Street", "Fictionton"],
                Postcode = "zz1 1zz",
                PracticeId = practice.Id,
            }))
            .ReadAsAsync<PatientResponse>(HttpStatusCode.Created);
        var template = await CreateTemplateWithStagesAsync(_admin, stages);
        var pathway = await CreatePathwayAsync(_admin, patient.Id, template.Id);
        return (patient.Id, pathway.Id, practice);
    }

    private static async Task<LetterResponse> Ok(Task<HttpResponseMessage> request, HttpStatusCode status = HttpStatusCode.OK) =>
        await (await request).ReadAsAsync<LetterResponse>(status);

    private static Task<HttpResponseMessage> Approve(HttpClient client, LetterResponse letter) =>
        client.PostAsJsonAsync($"{Letters}/{letter.Id}/approve", new LetterVersionRequest { ExpectedVersion = letter.Version });

    private static async Task<PagedResponse<LetterSummaryResponse>> ViewAsync(HttpClient client, string query) =>
        await (await client.GetAsync($"{Letters}/search?{query}")).ReadAsAsync<PagedResponse<LetterSummaryResponse>>();

    [Test]
    public async Task Letter_IsWrittenReturnedApprovedAndPosted_WithEachStepAuditedAndShownInTheViews()
    {
        var (patientId, pathwayId, practice) = await PatientWithPathwayAsync();
        var templateId = await CreateLetterTemplateAsync();

        // A nurse starts it: the template's text, to the patient's practice and the patient.
        var letter = await Ok(_nurse.Client.PostAsJsonAsync(Letters, new CreateLetterRequest { TemplateId = templateId, PathwayId = pathwayId }), HttpStatusCode.Created);
        letter.Status.Should().Be("Draft");
        letter.Recipients.Select(r => r.Kind).Should().Equal("GpPractice", "Patient");
        letter.Recipients[0].Should().BeEquivalentTo(new { Name = practice.Name, Postcode = "ZZ2 2ZZ", Email = "testbrook.surgery@nhs.net" });
        letter.CanEdit.Should().BeTrue();
        letter.CanApprove.Should().BeFalse("a nurse can't approve letters");

        // Saved with the body and a copy to someone else.
        letter = await Ok(_nurse.Client.PutAsJsonAsync($"{Letters}/{letter.Id}", new UpdateLetterRequest
        {
            Letterhead = letter.Letterhead,
            Body = letter.Body + "\n\nSynthetic plan: review in six weeks.",
            Closing = letter.Closing,
            Recipients =
            [
                new LetterRecipientRequest { Kind = "GpPractice", PracticeId = practice.Id },
                new LetterRecipientRequest { Kind = "Patient" },
                new LetterRecipientRequest { Kind = "Other", Name = "Synthetic Physio", AddressLines = "4 Made Lane\nFictionton", Postcode = "zz3 3zz" },
            ],
            ExpectedVersion = letter.Version,
        }));
        letter.Recipients.Should().HaveCount(3);
        (await ViewAsync(_nurse.Client, "view=MyDrafts")).Items.Should().ContainSingle(l => l.Id == letter.Id);

        // The draft's preview is a PDF marked as a draft.
        var preview = await _nurse.Client.GetAsync($"{Letters}/{letter.Id}/preview");
        preview.StatusCode.Should().Be(HttpStatusCode.OK);
        preview.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        (await preview.Content.ReadAsByteArrayAsync()).Take(5).Should().Equal("%PDF-"u8.ToArray());

        // Submitted to the clinician, who is told and counts it; returned with a comment; submitted again.
        letter = await Ok(_nurse.Client.PostAsJsonAsync($"{Letters}/{letter.Id}/submit",
            new SubmitLetterRequest { ApproverUserId = _clinician.UserId, ExpectedVersion = letter.Version }));
        var count = await (await _clinician.Client.GetAsync($"{Letters}/count")).ReadAsAsync<LetterCountResponse>();
        count.ToApprove.Should().Be(1);
        var told = await (await _clinician.Client.GetAsync("/api/notifications")).ReadAsAsync<NotificationsResponse>();
        told.Items.Should().ContainSingle(n => n.Kind == "LetterAwaitingApproval").Which.SubjectId.Should().Be(letter.Id);
        (await ViewAsync(_clinician.Client, "view=AwaitingMyApproval")).Items.Should().ContainSingle(l => l.Id == letter.Id);

        letter = await Ok(_clinician.Client.PostAsJsonAsync($"{Letters}/{letter.Id}/return",
            new ReturnLetterRequest { Comment = "Synthetic: add the dressing advice.", ExpectedVersion = letter.Version }));
        letter.IsReturned.Should().BeTrue();
        (await ViewAsync(_nurse.Client, "view=ReturnedToMe")).Items.Should().ContainSingle(l => l.Id == letter.Id);
        letter = await Ok(_nurse.Client.PostAsJsonAsync($"{Letters}/{letter.Id}/submit",
            new SubmitLetterRequest { ApproverUserId = _clinician.UserId, ExpectedVersion = letter.Version }));

        // A page loaded before that can't approve it.
        (await _clinician.Client.PostAsJsonAsync($"{Letters}/{letter.Id}/approve", new LetterVersionRequest { ExpectedVersion = 1 }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Approved: its PDF made by PDFsharp and stored as a file on the pathway.
        letter = await Ok(Approve(_clinician.Client, letter));
        letter.Status.Should().Be("Approved");
        letter.PdfAttachmentId.Should().NotBeNull();
        var pdf = await _nurse.Client.GetAsync($"/api/attachments/{letter.PdfAttachmentId}/content");
        pdf.StatusCode.Should().Be(HttpStatusCode.OK);
        (await pdf.Content.ReadAsByteArrayAsync()).Take(5).Should().Equal("%PDF-"u8.ToArray());
        (await (await _nurse.Client.GetAsync("/api/notifications")).ReadAsAsync<NotificationsResponse>())
            .Items.Should().Contain(n => n.Kind == "LetterApproved" && n.SubjectId == letter.Id);

        // The PDF is the record: it can't be deleted on its own, even by an admin.
        (await _admin.DeleteAsync($"/api/attachments/{letter.PdfAttachmentId}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        // Nor can the template, now a letter was written from it.
        (await _admin.DeleteAsync($"/api/letter-templates/{templateId}")).StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await ViewAsync(_nurse.Client, "view=ToSend")).Items.Should().ContainSingle(l => l.Id == letter.Id);
        (await (await _nurse.Client.GetAsync($"{Letters}/count")).ReadAsAsync<LetterCountResponse>()).ToSend.Should().Be(1);

        // Posted to each, recorded by the nurse.
        foreach (var recipient in letter.Recipients)
        {
            var method = recipient.Kind == "Patient" ? "HandedToPatient" : "Post";
            letter = await Ok(_nurse.Client.PostAsJsonAsync($"{Letters}/{letter.Id}/recipients/{recipient.Id}/record-sent",
                new RecordLetterSentRequest { Method = method, SentOn = UkToday, ExpectedVersion = letter.Version }));
        }

        letter.Recipients.Should().AllSatisfy(r => r.DispatchStatus.Should().Be("Sent"));
        letter.UnsentCount.Should().Be(0);
        (await ViewAsync(_nurse.Client, "view=Sent")).Items.Should().ContainSingle(l => l.Id == letter.Id);
        (await ViewAsync(_nurse.Client, $"pathwayId={pathwayId}")).Items.Should().ContainSingle(l => l.Id == letter.Id);

        // Its history is on the patient's timeline, without the return comment for staff.
        var timeline = await (await _nurse.Client.GetAsync($"/api/patients/{patientId}/timeline?kinds=Letters&pageSize=50"))
            .ReadAsAsync<PatientTimelinePageResponse>();
        timeline.Items.Select(e => e.Action).Should().Contain(["LetterDrafted", "LetterSubmitted", "LetterReturned", "LetterApproved", "LetterSent"]);
        timeline.Items.Should().AllSatisfy(e => e.Reason.Should().BeNull());

        // Every read of the letter was logged against the patient.
        var views = await RunInScopeAsync(async services =>
        {
            var db = services.GetRequiredService<MedApp.Infrastructure.Data.MedAppDbContext>();
            return await db.ActivityEntries.CountAsync(e => e.PatientId == patientId && e.Action == MedApp.Domain.Activity.ActivityAction.LetterViewed);
        });
        views.Should().BeGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task SecureEmail_IsSentToThePracticesNhsAddress_AndARefusalIsShownAndRecorded()
    {
        var (_, pathwayId, _) = await PatientWithPathwayAsync();
        var templateId = await CreateLetterTemplateAsync();
        var letter = await Ok(_clinician.Client.PostAsJsonAsync(Letters, new CreateLetterRequest { TemplateId = templateId, PathwayId = pathwayId }), HttpStatusCode.Created);
        letter = await Ok(Approve(_clinician.Client, letter));
        var practice = letter.Recipients.Single(r => r.Kind == "GpPractice");
        var patient = letter.Recipients.Single(r => r.Kind == "Patient");
        practice.CanSendSecureEmail.Should().BeTrue();
        patient.CanSendSecureEmail.Should().BeFalse();

        Api.Letters.FailNext = true;
        var refused = await _nurse.Client.PostAsJsonAsync($"{Letters}/{letter.Id}/recipients/{practice.Id}/send", new LetterVersionRequest { ExpectedVersion = letter.Version });
        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        letter = await (await _nurse.Client.GetAsync($"{Letters}/{letter.Id}")).ReadAsAsync<LetterResponse>();
        letter.Recipients.Single(r => r.Id == practice.Id).DispatchStatus.Should().Be("Failed");

        letter = await Ok(_nurse.Client.PostAsJsonAsync($"{Letters}/{letter.Id}/recipients/{practice.Id}/send", new LetterVersionRequest { ExpectedVersion = letter.Version }));

        letter.Recipients.Single(r => r.Id == practice.Id).Should().BeEquivalentTo(new { DispatchStatus = "Sent", Method = "SecureEmail", SentOn = (DateOnly?)UkToday });
        var sent = Api.Letters.Sent.Single();
        sent.To.Should().Be("testbrook.surgery@nhs.net");
        sent.FileName.Should().Be("letter.pdf");
        sent.Pdf.Take(5).Should().Equal("%PDF-"u8.ToArray());
        sent.TextBody.Should().NotContain(letter.PatientName);

        // The patient is never emailed.
        (await _nurse.Client.PostAsJsonAsync($"{Letters}/{letter.Id}/recipients/{patient.Id}/send", new LetterVersionRequest { ExpectedVersion = letter.Version }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task AStageThatNeedsADischargeSummary_CantBeFinished_UntilOneIsApprovedAndSentToEveryone()
    {
        var definition = await CreateStageDefinitionAsync(_admin, UniqueName("Discharge letter to GP"));
        (await _admin.PatchAsJsonAsync($"/api/pathways/patient-pathway-stage/definitions/{definition.Id}",
                new UpdatePatientPathwayStageDefinitionRequest { NeedsLetterKind = "DischargeSummary" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var template = await CreateTemplateAsync(_admin, definition.Id);
        var patient = await CreatePatientAsync(_admin);
        var pathway = await CreatePathwayAsync(_admin, patient.Id, template.Id);
        pathway.Stages.Single().NeedsLetterKind.Should().Be("DischargeSummary");

        var refused = await FinishStageAsync(_clinician.Client, pathway.Id, pathway.Stages[0].Id);
        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("discharge summary");

        var letterTemplate = await CreateLetterTemplateAsync("DischargeSummary");
        var letter = await Ok(_clinician.Client.PostAsJsonAsync(Letters, new CreateLetterRequest { TemplateId = letterTemplate, PathwayId = pathway.Id }), HttpStatusCode.Created);
        letter = await Ok(Approve(_clinician.Client, letter));
        foreach (var recipient in letter.Recipients)
            letter = await Ok(_clinician.Client.PostAsJsonAsync($"{Letters}/{letter.Id}/recipients/{recipient.Id}/record-sent",
                new RecordLetterSentRequest { Method = "HandedToPatient", SentOn = UkToday, ExpectedVersion = letter.Version }));

        (await FinishStageAsync(_clinician.Client, pathway.Id, pathway.Stages[0].Id)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task Permissions_TheirRecords_AndTheRecordsThatStopLetters()
    {
        var (patientId, pathwayId, _) = await PatientWithPathwayAsync();
        var templateId = await CreateLetterTemplateAsync();
        var lab = await CreateStaffAsync("Lab");

        // An admin doesn't write letters (it's clinical), and the lab can't read them.
        (await _admin.PostAsJsonAsync(Letters, new CreateLetterRequest { TemplateId = templateId, PathwayId = pathwayId }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var letter = await Ok(_nurse.Client.PostAsJsonAsync(Letters, new CreateLetterRequest { TemplateId = templateId, PathwayId = pathwayId }), HttpStatusCode.Created);
        (await Approve(_nurse.Client, letter)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _clinician.Client.PutAsJsonAsync($"{Letters}/{letter.Id}", new UpdateLetterRequest { ExpectedVersion = letter.Version }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "a draft is its author's alone");
        (await lab.Client.GetAsync($"{Letters}/{letter.Id}")).StatusCode.Should().Be(HttpStatusCode.OK, "Lab can see patients");

        // A draft stops anonymisation; discarding it doesn't.
        (await _admin.PostAsJsonAsync($"/api/patients/{patientId}/anonymise", new AnonymisePatientRequest
            { Reason = "retentionPeriodEnded", Confirmation = "x" }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        letter = await Ok(_nurse.Client.PostAsJsonAsync($"{Letters}/{letter.Id}/discard", new LetterVersionRequest { ExpectedVersion = letter.Version }));
        letter.Status.Should().Be("Withdrawn");

        // A restricted record's letters wait; an approved one can still be withdrawn.
        var approved = await Ok(_clinician.Client.PostAsJsonAsync(Letters, new CreateLetterRequest { TemplateId = templateId, PathwayId = pathwayId }), HttpStatusCode.Created);
        approved = await Ok(Approve(_clinician.Client, approved));
        (await _admin.PutAsJsonAsync($"/api/patients/{patientId}/restriction",
                new SetPatientFlagRequest { Set = true, Reason = "Synthetic restriction.", ConfirmOpenPathways = true }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await _nurse.Client.PostAsJsonAsync($"{Letters}/{approved.Id}/recipients/{approved.Recipients[0].Id}/record-sent",
                new RecordLetterSentRequest { Method = "Post", SentOn = UkToday, ExpectedVersion = approved.Version }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await _nurse.Client.PostAsJsonAsync(Letters, new CreateLetterRequest { TemplateId = templateId, PathwayId = pathwayId }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        var withdrawn = await Ok(_admin.PostAsJsonAsync($"{Letters}/{approved.Id}/withdraw",
            new WithdrawLetterRequest { Reason = "Synthetic: wrong practice.", ExpectedVersion = approved.Version }));
        withdrawn.Status.Should().Be("Withdrawn");
        withdrawn.PdfAttachmentId.Should().Be(approved.PdfAttachmentId, "the PDF stays on file");

        // The export has the letters, with their text.
        var export = await (await _admin.GetAsync($"/api/patients/{patientId}/export")).ReadAsAsync<PatientExportResponse>();
        export.Letters.Should().HaveCount(2);
        export.Letters.Should().Contain(l => l.WithdrawnReason == "Synthetic: wrong practice.");
    }

    [Test]
    public async Task Templates_NoLetterWasWrittenFrom_CanBeDeleted()
    {
        var templateId = await CreateLetterTemplateAsync();
        (await (await _admin.GetAsync($"/api/letter-templates/{templateId}")).ReadAsAsync<LetterTemplateResponse>())
            .CanBeDeleted.Should().BeTrue();

        (await _admin.DeleteAsync($"/api/letter-templates/{templateId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _admin.GetAsync($"/api/letter-templates/{templateId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Referral_AskForMoreInformation_WaitsInItsOwnView_ThenInformationReceivedPutsItBack()
    {
        var booking = await CreateStaffAsync("Booking");
        (await _admin.PutAsJsonAsync($"/api/auth/users/{_clinician.Username}/teams", new SetUserTeamsRequest([new(Team.GeneralId, null)])))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var patient = await CreatePatientAsync(_admin);
        var referral = await (await booking.Client.PostAsJsonAsync("/api/referrals", new RegisterReferralRequest
            {
                PatientId = patient.Id,
                TeamId = Team.GeneralId,
                ReceivedOn = UkToday.AddDays(-2),
                ReferrerPractice = "Synthetic Lane Surgery",
                RequestedPriority = "Medium",
                Reason = "Synthetic reason for referral.",
            }))
            .ReadAsAsync<ReferralResponse>(HttpStatusCode.Created);

        (await booking.Client.PostAsJsonAsync($"/api/referrals/{referral.Id}/ask-for-information", new ReferralVersionRequest { ExpectedVersion = referral.Version }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        referral = await (await _clinician.Client.PostAsJsonAsync($"/api/referrals/{referral.Id}/ask-for-information",
            new ReferralVersionRequest { ExpectedVersion = referral.Version })).ReadAsAsync<ReferralResponse>();
        referral.Status.Should().Be("InformationRequested");
        referral.IsOpen.Should().BeTrue();

        var waiting = await (await _clinician.Client.GetAsync("/api/referrals/search?statuses=InformationRequested"))
            .ReadAsAsync<PagedResponse<ReferralSummaryResponse>>();
        waiting.Items.Should().ContainSingle(r => r.Id == referral.Id);
        (await (await _clinician.Client.GetAsync("/api/referrals/awaiting-triage/count")).ReadAsAsync<ReferralCountResponse>())
            .Count.Should().Be(0, "it isn't awaiting triage while the information is awaited");

        // The request is written from the referral's page: a request for more information, to the referrer.
        var requestTemplate = await CreateLetterTemplateAsync("InformationRequest");
        var letter = await Ok(_clinician.Client.PostAsJsonAsync(Letters, new CreateLetterRequest { TemplateId = requestTemplate, ReferralId = referral.Id }), HttpStatusCode.Created);
        letter.Recipients.Should().ContainSingle().Which.Kind.Should().Be("Referrer");

        referral = await (await _clinician.Client.PostAsJsonAsync($"/api/referrals/{referral.Id}/information-received",
            new ReferralVersionRequest { ExpectedVersion = referral.Version })).ReadAsAsync<ReferralResponse>();
        referral.Status.Should().Be("AwaitingTriage");
        referral.History.Select(e => e.Action).Should().Contain(["InformationRequested", "InformationReceived"]);
    }
}
