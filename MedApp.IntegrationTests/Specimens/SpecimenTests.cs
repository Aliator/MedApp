using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Contracts.Activity.Responses;
using MedApp.Contracts.Common;
using MedApp.Contracts.Notifications.Responses;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MedApp.Contracts.Pathways.Timeline.Responses;
using MedApp.Contracts.Patients.Responses;
using MedApp.Contracts.Specimens.Requests;
using MedApp.Contracts.Specimens.Responses;
using MedApp.IntegrationTests.Common;

namespace MedApp.IntegrationTests.Specimens;

/// <summary>Making a specimen through the API, for this fixture and the others that need one.</summary>
public static class SpecimenApi
{
    public const string BiopsyTypeName = "Synthetic biopsy";
    public const string LabName = "Synthetic lab";
    public const string Site = "Synthetic site: left forearm";
    public const string RequestNotes = "Synthetic request notes for the lab.";

    /// <summary>A specimen type (14 days) and a lab, made by an admin (ManageSetup).</summary>
    public static async Task<(SpecimenTypeResponse Type, LaboratoryResponse Lab)> CreateSetupAsync(HttpClient admin)
    {
        var type = await (await admin.PostAsJsonAsync("/api/specimen-types",
                new SpecimenTypeRequest { Name = $"{BiopsyTypeName} {Guid.NewGuid():N}"[..30], ExpectedTurnaroundDays = 14 }))
            .ReadAsAsync<SpecimenTypeResponse>(HttpStatusCode.Created);
        var lab = await (await admin.PostAsJsonAsync("/api/laboratories",
                new LaboratoryRequest { Name = $"{LabName} {Guid.NewGuid():N}"[..30], UsesMedApp = true }))
            .ReadAsAsync<LaboratoryResponse>(HttpStatusCode.Created);
        return (type, lab);
    }

    public static Task<HttpResponseMessage> PostRecordAsync(
        HttpClient client, PatientPathwayResponse pathway, Guid typeId, Guid labId, Guid requestingUserId, int stage = 0) =>
        client.PostAsJsonAsync($"/api/pathways/{pathway.Id}/specimens", new RecordSpecimenRequest
        {
            StageId = pathway.Stages[stage].Id,
            SpecimenTypeId = typeId,
            LaboratoryId = labId,
            RequestingUserId = requestingUserId,
            Site = Site,
            RequestNotes = RequestNotes,
        });

    public static async Task<SpecimenResponse> RecordAsync(
        HttpClient client, PatientPathwayResponse pathway, Guid typeId, Guid labId, Guid requestingUserId) =>
        await (await PostRecordAsync(client, pathway, typeId, labId, requestingUserId)).ReadAsAsync<SpecimenResponse>(HttpStatusCode.Created);

    public static Task<HttpResponseMessage> StepAsync(HttpClient client, SpecimenResponse specimen, string step, object? body = null) =>
        client.PostAsJsonAsync($"/api/specimens/{specimen.Id}/{step}", body ?? new SpecimenStepRequest { ExpectedVersion = specimen.Version });

    public static Task<HttpResponseMessage> ReportAsync(HttpClient client, SpecimenResponse specimen, byte[] bytes, string fileName)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/specimens/{specimen.Id}/report?expectedVersion={specimen.Version}") { Content = content };
        request.Headers.Add("X-File-Name", Uri.EscapeDataString(fileName));
        return client.SendAsync(request);
    }
}

/// <summary>
/// Specimens through the API against SQL Server: numbers from the sequence, the permissions (a nurse records and sends,
/// the lab receives, rejects and reports, admins neither), the steps and their versions, the report as an attachment
/// that can't be deleted on its own, the last stage's gate, the lab's worklist views in SQL, finding by number, the
/// reads in the activity log, the timeline (free text for admins only), the export, anonymisation, and the
/// notifications.
/// </summary>
[TestFixture]
public sealed class SpecimenTests : IntegrationTest
{
    private HttpClient _admin = null!;
    private StaffAccount _nurse = null!;

    /// <summary>Who requests the specimens the nurse takes: a clinician, who can sign off the result.</summary>
    private StaffAccount _clinician = null!;
    private StaffAccount _lab = null!;
    private SpecimenTypeResponse _type = null!;
    private LaboratoryResponse _laboratory = null!;

    [SetUp]
    public async Task SetUpAsync()
    {
        _admin = await SignInAsAdminAsync();
        _nurse = await CreateStaffAsync("Nurse");
        _clinician = await CreateStaffAsync("Clinician");
        _lab = await CreateStaffAsync("Lab");
        (_type, _laboratory) = await SpecimenApi.CreateSetupAsync(_admin);
    }

    private async Task<(PatientPathwayResponse Pathway, SpecimenResponse Specimen)> TakenAsync(int stages = 2)
    {
        var pathway = await CreatePatientWithPathwayAsync(_admin, stages);
        var specimen = await SpecimenApi.RecordAsync(_nurse.Client, pathway, _type.Id, _laboratory.Id, _clinician.UserId);
        return (pathway, specimen);
    }

    [Test]
    public async Task Record_GivesItANumberFromTheSequence_AndALabelHoldingOnlyThatNumber()
    {
        var (_, first) = await TakenAsync();
        var (_, second) = await TakenAsync();

        first.Number.Should().MatchRegex(@"^MA\d{2}-\d{6,}$");
        second.Number.Should().NotBe(first.Number);
        first.Status.Should().Be("Taken");
        first.Version.Should().Be(1);
        first.Label.QrPath.Should().NotBeNullOrEmpty();
        first.Label.Size.Should().BeGreaterThan(20, "a version 1 code is 21 modules, plus the quiet zone");
        first.RequestingUsername.Should().Be(_clinician.Username);
        first.Site.Should().Be(SpecimenApi.Site);
    }

    [Test]
    public async Task Record_ByAdminOrAllStaff_Is403_ByTheLabRole_Works()
    {
        var pathway = await CreatePatientWithPathwayAsync(_admin);

        (await SpecimenApi.PostRecordAsync(_admin, pathway, _type.Id, _laboratory.Id, _clinician.UserId))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "admins don't record specimens: it's clinical");
        (await SpecimenApi.PostRecordAsync(_lab.Client, pathway, _type.Id, _laboratory.Id, _clinician.UserId))
            .StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Test]
    public async Task Steps_TakenSentReceivedReported_ThenTheReportCantBeDeletedOnItsOwn()
    {
        var (pathway, specimen) = await TakenAsync();

        // The lab can't receive what hasn't been sent; the nurse can't receive at all.
        (await SpecimenApi.StepAsync(_lab.Client, specimen, "received")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var sent = await (await SpecimenApi.StepAsync(_nurse.Client, specimen, "sent",
            new SpecimenStepRequest { Reference = "BATCH-1", ExpectedVersion = specimen.Version })).ReadAsAsync<SpecimenResponse>();
        (await SpecimenApi.StepAsync(_nurse.Client, sent, "received")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        // A stale version is refused.
        (await SpecimenApi.StepAsync(_lab.Client, specimen, "received")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var received = await (await SpecimenApi.StepAsync(_lab.Client, sent, "received",
            new SpecimenStepRequest { Reference = "LAB-1", ExpectedVersion = sent.Version })).ReadAsAsync<SpecimenResponse>();

        var reported = await (await SpecimenApi.ReportAsync(_lab.Client, received, SyntheticPdf, "synthetic-report.pdf"))
            .ReadAsAsync<SpecimenResponse>();

        reported.Status.Should().Be("Reported");
        reported.CourierReference.Should().Be("BATCH-1");
        reported.LabReference.Should().Be("LAB-1");
        reported.ReportedByUsername.Should().Be(_lab.Username);
        reported.ReportAttachmentId.Should().NotBeNull();
        var files = await (await _admin.GetAsync($"/api/attachments?pathwayId={pathway.Id}"))
            .ReadAsAsync<MedApp.Contracts.Attachments.Responses.AttachmentListResponse>();
        files.Items.Should().ContainSingle(f => f.Id == reported.ReportAttachmentId);

        (await _admin.DeleteAsync($"/api/attachments/{reported.ReportAttachmentId}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await _lab.Client.DeleteAsync($"/api/attachments/{reported.ReportAttachmentId}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task LastStage_CantBeFinishedWhileASpecimenIsOpen()
    {
        var pathway = await CreatePatientWithPathwayAsync(_admin, stages: 1);
        var specimen = await SpecimenApi.RecordAsync(_nurse.Client, pathway, _type.Id, _laboratory.Id, _clinician.UserId);

        var refused = await FinishStageAsync(_nurse.Client, pathway.Id, pathway.Stages[0].Id);
        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await refused.Content.ReadAsStringAsync()).Should().Contain(specimen.Number);

        await (await SpecimenApi.StepAsync(_nurse.Client, specimen, "lost",
            new CloseSpecimenRequest { Detail = "Synthetic: not in the bag", ExpectedVersion = specimen.Version })).ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await FinishStageAsync(_nurse.Client, pathway.Id, pathway.Stages[0].Id)).ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    [Test]
    public async Task WorklistViews_RunInSql_OldestFirst_AndOverdueByTheTypesDays()
    {
        var (_, toSend) = await TakenAsync();
        var (_, expected) = await TakenAsync();
        await (await SpecimenApi.StepAsync(_nurse.Client, expected, "sent")).ShouldHaveStatusAsync(HttpStatusCode.OK);
        // A type the lab turns round in a day, taken two days ago: overdue (DATEADD on the type's days, in SQL).
        var quick = await (await _admin.PostAsJsonAsync("/api/specimen-types",
            new SpecimenTypeRequest { Name = "Synthetic quick test", ExpectedTurnaroundDays = 1 })).ReadAsAsync<SpecimenTypeResponse>(HttpStatusCode.Created);
        var pathway = await CreatePatientWithPathwayAsync(_admin);
        var overdue = await (await _nurse.Client.PostAsJsonAsync($"/api/pathways/{pathway.Id}/specimens", new RecordSpecimenRequest
        {
            StageId = pathway.Stages[0].Id, SpecimenTypeId = quick.Id, LaboratoryId = _laboratory.Id,
            RequestingUserId = _clinician.UserId, TakenAtUtc = Now.AddDays(-2),
        })).ReadAsAsync<SpecimenResponse>(HttpStatusCode.Created);

        async Task<IReadOnlyList<SpecimenSummaryResponse>> View(string view) =>
            (await (await _lab.Client.GetAsync($"/api/specimens/search?view={view}&laboratoryId={_laboratory.Id}&pageSize=100"))
                .ReadAsAsync<PagedResponse<SpecimenSummaryResponse>>()).Items;

        (await View("ToSend")).Select(s => s.Id).Should().Equal(overdue.Id, toSend.Id);
        (await View("Expected")).Select(s => s.Id).Should().Equal(expected.Id);
        (await View("Overdue")).Should().ContainSingle().Which.Should().BeEquivalentTo(new { overdue.Id, IsOverdueAtLab = true });
        overdue.IsOverdueAtLab.Should().BeTrue();
    }

    [Test]
    public async Task Find_ByNumber_InTheBody_RecordsTheView()
    {
        var (_, specimen) = await TakenAsync();
        await ExecuteSqlAsync($"DELETE FROM ActivityEntries");

        var found = await (await _lab.Client.PostAsJsonAsync("/api/specimens/find",
            new FindSpecimenRequest { Number = $" {specimen.Number.ToLowerInvariant()} " })).ReadAsAsync<SpecimenResponse>();
        (await _lab.Client.PostAsJsonAsync("/api/specimens/find", new FindSpecimenRequest { Number = "ZZ99-999999" }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        found.Id.Should().Be(specimen.Id);
        var entries = (await (await _admin.GetAsync("/api/activity/search?PageSize=100&Actions=SpecimenViewed"))
            .ReadAsAsync<PagedResponse<ActivityEntryResponse>>()).Items;
        entries.Should().ContainSingle().Which.Details.Should().Contain(specimen.Id.ToString());
    }

    [Test]
    public async Task Timeline_StaffSeeTheStepsWithoutFreeText_AdminsSeeIt()
    {
        var (pathway, specimen) = await TakenAsync();
        Clock.Advance(TimeSpan.FromMinutes(1));
        await (await SpecimenApi.StepAsync(_nurse.Client, specimen, "lost",
            new CloseSpecimenRequest { Detail = "Synthetic lost detail", ExpectedVersion = specimen.Version })).ShouldHaveStatusAsync(HttpStatusCode.OK);

        var url = $"/api/patients/{pathway.PatientId}/timeline?kinds=Specimens";
        var staffBody = await (await _nurse.Client.GetAsync(url)).Content.ReadAsStringAsync();
        var adminPage = await (await _admin.GetAsync(url)).ReadAsAsync<PatientTimelinePageResponse>();

        staffBody.Should().Contain(specimen.Number).And.NotContain(SpecimenApi.Site).And.NotContain("Synthetic lost detail");
        adminPage.Items.Select(i => i.Action).Should().Equal("SpecimenLost", "SpecimenTaken");
        adminPage.Items[0].Reason.Should().Be("Synthetic lost detail");
        adminPage.Items[1].Changes!.Should().Contain(c => c.To == SpecimenApi.Site);
    }

    [Test]
    public async Task Rejected_TellsTheRequesterWithAFixedTitle_LinkingToTheSpecimen()
    {
        var (_, specimen) = await TakenAsync();
        var sent = await (await SpecimenApi.StepAsync(_nurse.Client, specimen, "sent")).ReadAsAsync<SpecimenResponse>();

        await (await SpecimenApi.StepAsync(_lab.Client, sent, "rejected",
            new RejectSpecimenRequest { Reason = "Leaked", Detail = "Synthetic: the pot leaked", ExpectedVersion = sent.Version }))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);

        var notifications = await (await _clinician.Client.GetAsync("/api/notifications")).ReadAsAsync<NotificationsResponse>();
        var notification = notifications.Items.Should().ContainSingle(n => n.Kind == "SpecimenRejectedByLab").Subject;
        notification.Title.Should().Be("Specimen rejected");
        notification.SubjectId.Should().Be(specimen.Id);
        notification.Message.Should().NotContain("leaked");
    }

    [Test]
    public async Task Export_ListsTheSpecimen_AndAnonymisingWaitsForItToComeBack()
    {
        var (pathway, specimen) = await TakenAsync(stages: 1);
        var patient = await (await _admin.GetAsync($"/api/patients/{pathway.PatientId}")).ReadAsAsync<PatientResponse>();

        var export = await (await _admin.GetAsync($"/api/patients/{pathway.PatientId}/export")).ReadAsAsync<PatientExportResponse>();
        export.Specimens.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            specimen.Number, Site = SpecimenApi.Site, RequestedBy = _clinician.Username, Status = "Taken",
        });

        await (await SpecimenApi.StepAsync(_nurse.Client, specimen, "lost",
            new CloseSpecimenRequest { Detail = "Synthetic lost", ExpectedVersion = specimen.Version })).ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await FinishStageAsync(_nurse.Client, pathway.Id, pathway.Stages[0].Id)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await PostPathwayStatusAsync(_nurse.Client, pathway.Id, "Completed")).ShouldHaveStatusAsync(HttpStatusCode.OK);

        await (await _admin.PostAsJsonAsync($"/api/patients/{pathway.PatientId}/anonymise", new
        {
            reason = "RetentionPeriodEnded",
            confirmation = $"{patient.FirstName} {patient.LastName}",
        })).ShouldHaveStatusAsync(HttpStatusCode.OK);

        var after = await (await _admin.GetAsync($"/api/specimens/{specimen.Id}")).ReadAsAsync<SpecimenResponse>();
        after.Site.Should().Be("[Removed when the record was anonymised]");
        after.ClosedDetail.Should().Be("[Removed when the record was anonymised]");
        after.Number.Should().Be(specimen.Number);
    }

    [Test]
    public async Task Anonymise_WhileASpecimenIsWithTheLab_Is409()
    {
        var (pathway, _) = await TakenAsync();
        var patient = await (await _admin.GetAsync($"/api/patients/{pathway.PatientId}")).ReadAsAsync<PatientResponse>();
        await (await PostPathwayStatusAsync(_admin, pathway.Id, "Cancelled", "Synthetic reason")).ShouldHaveStatusAsync(HttpStatusCode.OK);

        var response = await _admin.PostAsJsonAsync($"/api/patients/{pathway.PatientId}/anonymise", new
        {
            reason = "RetentionPeriodEnded",
            confirmation = $"{patient.FirstName} {patient.LastName}",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("specimen");
    }

    [Test]
    public async Task Setup_TypeInUse_IsArchivedNotDeleted_AndCantBeChosenAgain()
    {
        var pathway = await CreatePatientWithPathwayAsync(_admin);
        await SpecimenApi.RecordAsync(_nurse.Client, pathway, _type.Id, _laboratory.Id, _clinician.UserId);

        (await _admin.DeleteAsync($"/api/specimen-types/{_type.Id}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        await (await _admin.PostAsync($"/api/specimen-types/{_type.Id}/archive", null)).ShouldHaveStatusAsync(HttpStatusCode.OK);

        (await SpecimenApi.PostRecordAsync(_nurse.Client, pathway, _type.Id, _laboratory.Id, _clinician.UserId))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var listed = await (await _nurse.Client.GetAsync("/api/specimen-types")).ReadAsAsync<List<SpecimenTypeResponse>>();
        listed.Should().NotContain(t => t.Id == _type.Id);
        (await _nurse.Client.PostAsJsonAsync("/api/specimen-types", new SpecimenTypeRequest { Name = "x", ExpectedTurnaroundDays = 1 }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task DeletePathway_WithASpecimen_Is409()
    {
        var (pathway, specimen) = await TakenAsync();
        await (await SpecimenApi.StepAsync(_nurse.Client, specimen, "entered-in-error",
            new CloseSpecimenRequest { Detail = "Synthetic: recorded twice", ExpectedVersion = specimen.Version })).ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await PostPathwayStatusAsync(_admin, pathway.Id, "Cancelled", "Synthetic reason")).ShouldHaveStatusAsync(HttpStatusCode.OK);

        (await _admin.DeleteAsync(PathwayUrl(pathway.Id))).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
