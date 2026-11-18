using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Contracts.Consent;
using MedApp.Contracts.Pathways.PatientPathwayStages.Definitions.Requests;
using MedApp.Contracts.Patients.Requests;
using MedApp.Contracts.Patients.Responses;
using MedApp.IntegrationTests.Common;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Consent;

/// <summary>
/// Consent records through the API against SQL Server: the forms list, recording by a nurse, the stage gate, the scan
/// that can't be deleted on its own, withdrawing, entered in error (deleting the scan), a restricted record, the
/// permissions, and the export. MedApp records consent; it never assesses it.
/// </summary>
[TestFixture]
public sealed class ConsentTests : IntegrationTest
{
    private HttpClient _admin = null!;
    private StaffAccount _nurse = null!;

    [SetUp]
    public async Task SetUpAsync()
    {
        _admin = await SignInAsAdminAsync();
        _nurse = await CreateStaffAsync("Nurse");
    }

    private static string RecordsUrl(Guid pathwayId) => $"/api/pathways/{pathwayId}/consent-records";

    private static async Task<ConsentRecordResponse> Ok(Task<HttpResponseMessage> request, HttpStatusCode status = HttpStatusCode.OK) =>
        await (await request).ReadAsAsync<ConsentRecordResponse>(status);

    private async Task<ConsentFormResponse> CreateFormAsync() =>
        await (await _admin.PostAsJsonAsync("/api/consent-forms", new ConsentFormRequest { Name = UniqueName("Consent form"), FormVersion = "3" }))
            .ReadAsAsync<ConsentFormResponse>(HttpStatusCode.Created);

    /// <summary>A pathway of one stage that needs consent.</summary>
    private async Task<MedApp.Contracts.Pathways.PatientPathways.Responses.PatientPathwayResponse> PathwayNeedingConsentAsync()
    {
        var definition = await CreateStageDefinitionAsync(_admin, UniqueName("Biopsy"));
        (await _admin.PatchAsJsonAsync($"/api/pathways/patient-pathway-stage/definitions/{definition.Id}",
                new UpdatePatientPathwayStageDefinitionRequest { NeedsConsent = true }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var template = await CreateTemplateAsync(_admin, definition.Id);
        var patient = await CreatePatientAsync(_admin);
        var pathway = await CreatePathwayAsync(_admin, patient.Id, template.Id);
        pathway.Stages.Single().NeedsConsent.Should().BeTrue();
        return pathway;
    }

    [Test]
    public async Task AStageThatNeedsConsent_CantBeFinished_UntilValidConsentIsRecorded_AndWithdrawalIsFlagged()
    {
        var pathway = await PathwayNeedingConsentAsync();
        var stage = pathway.Stages[0];
        var form = await CreateFormAsync();

        var refused = await FinishStageAsync(_nurse.Client, pathway.Id, stage.Id);
        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("consent");

        var scan = await UploadAttachmentOkAsync(_nurse.Client, $"pathwayId={pathway.Id}", SyntheticPdf, "signed-form.pdf");
        var record = await Ok(_nurse.Client.PostAsJsonAsync(RecordsUrl(pathway.Id), new RecordConsentRequest
        {
            StageId = stage.Id, ConsentFormId = form.Id, SignedOn = UkToday, SignedBy = "Patient", ScanAttachmentId = scan.Id,
        }), HttpStatusCode.Created);
        record.Should().BeEquivalentTo(new { Status = "Valid", FormVersion = "3", SignedBy = "Patient", ScanAttachmentId = (Guid?)scan.Id, Version = 1 });

        // The scan goes only with the record's own correction.
        (await _admin.DeleteAsync($"/api/attachments/{scan.Id}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        // A used form is archived, not deleted.
        (await _admin.DeleteAsync($"/api/consent-forms/{form.Id}")).StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await FinishStageAsync(_nurse.Client, pathway.Id, stage.Id)).StatusCode.Should().Be(HttpStatusCode.OK);

        record = await Ok(_nurse.Client.PostAsJsonAsync($"/api/consent-records/{record.Id}/withdraw",
            new WithdrawConsentRequest { WithdrawnOn = UkToday, Detail = "Synthetic: changed their mind.", ExpectedVersion = record.Version }));
        record.Status.Should().Be("Withdrawn");
        record.WithdrawnAfterStageFinished.Should().BeTrue();

        var list = await (await _nurse.Client.GetAsync(RecordsUrl(pathway.Id))).ReadAsAsync<List<ConsentRecordResponse>>();
        list.Should().ContainSingle().Which.WithdrawnAfterStageFinished.Should().BeTrue();

        // The export carries it, with its detail: the patient is entitled to it.
        var export = await (await _admin.GetAsync($"/api/patients/{pathway.PatientId}/export")).ReadAsAsync<PatientExportResponse>();
        export.ConsentRecords.Should().ContainSingle().Which.StatusDetail.Should().Be("Synthetic: changed their mind.");
    }

    [Test]
    public async Task EnteredInError_ByAnAdmin_DeletesTheScan_AndTheStageWaitsAgain()
    {
        var pathway = await PathwayNeedingConsentAsync();
        var form = await CreateFormAsync();
        var scan = await UploadAttachmentOkAsync(_nurse.Client, $"pathwayId={pathway.Id}", SyntheticPdf, "signed-form.pdf");
        var record = await Ok(_nurse.Client.PostAsJsonAsync(RecordsUrl(pathway.Id), new RecordConsentRequest
        {
            StageId = pathway.Stages[0].Id, ConsentFormId = form.Id, SignedOn = UkToday, SignedBy = "BestInterestsDecision", ScanAttachmentId = scan.Id,
        }), HttpStatusCode.Created);

        record = await Ok(_admin.PostAsJsonAsync($"/api/consent-records/{record.Id}/entered-in-error",
            new MarkConsentEnteredInErrorRequest { Detail = "Synthetic: wrong patient.", ExpectedVersion = record.Version }));
        record.Status.Should().Be("EnteredInError");

        (await _admin.GetAsync($"/api/attachments/{scan.Id}/content")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await FinishStageAsync(_nurse.Client, pathway.Id, pathway.Stages[0].Id)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Permissions_AndARestrictedRecord()
    {
        var pathway = await PathwayNeedingConsentAsync();
        var form = await CreateFormAsync();
        var request = new RecordConsentRequest { StageId = pathway.Stages[0].Id, ConsentFormId = form.Id, SignedOn = UkToday, SignedBy = "Patient" };

        // Recording consent is clinical: the Admin role can't.
        (await _admin.PostAsJsonAsync(RecordsUrl(pathway.Id), request)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var record = await Ok(_nurse.Client.PostAsJsonAsync(RecordsUrl(pathway.Id), request), HttpStatusCode.Created);

        await (await _admin.PutAsJsonAsync($"/api/patients/{pathway.PatientId}/restriction",
                new SetPatientFlagRequest { Set = true, Reason = "Synthetic restriction", ConfirmOpenPathways = true }))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);

        // New consent can't be recorded, but a withdrawal still can (it protects the patient).
        (await _nurse.Client.PostAsJsonAsync(RecordsUrl(pathway.Id), request)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await _nurse.Client.PostAsJsonAsync($"/api/consent-records/{record.Id}/withdraw",
                new WithdrawConsentRequest { WithdrawnOn = UkToday, Detail = "Synthetic withdrawal.", ExpectedVersion = record.Version }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
