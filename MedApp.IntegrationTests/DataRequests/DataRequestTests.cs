using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using MedApp.Contracts.Common;
using MedApp.Contracts.DataRequests;
using MedApp.Contracts.Pathways.PatientPathways.Requests;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MedApp.IntegrationTests.Common;

namespace MedApp.IntegrationTests.DataRequests;

/// <summary>
/// Data requests through the API against SQL Server: logging one, the identity check and the review before the pack, the
/// pack's contents (the report, the record and the files, leaving out what was withheld), its no-store download, closing
/// it, and that a patient with a request can't be deleted nor anonymised while it's open.
/// </summary>
[TestFixture]
public sealed class DataRequestTests : IntegrationTest
{
    private HttpClient _admin = null!;
    private StaffAccount _clinician = null!;

    [SetUp]
    public async Task SetUpAsync()
    {
        _admin = await SignInAsAdminAsync();
        _clinician = await CreateStaffAsync("Clinician");
    }

    private async Task<DataRequestResponse> CreateAsync(Guid patientId) =>
        await (await _admin.PostAsJsonAsync("/api/data-requests", new CreateDataRequestRequest
        {
            PatientId = patientId, ReceivedOn = UkToday, RequestedByPatient = true
        })).ReadAsAsync<DataRequestResponse>(HttpStatusCode.Created);

    [Test]
    public async Task ThePack_NeedsTheIdentityCheckAndTheReview_AndLeavesOutWhatWasWithheld()
    {
        var pathway = await CreatePatientWithPathwayAsync(_admin);
        var kept = await (await (await ClinicalAsync(_admin)).PostAsJsonAsync($"/api/pathways/patient-pathways/{pathway.Id}/notes",
            new AddPathwayNoteRequest { Text = "Synthetic kept note." })).ReadAsAsync<PatientPathwayNoteResponse>(HttpStatusCode.Created);
        var withheld = await (await (await ClinicalAsync(_admin)).PostAsJsonAsync($"/api/pathways/patient-pathways/{pathway.Id}/notes",
            new AddPathwayNoteRequest { Text = "Synthetic withheld note." })).ReadAsAsync<PatientPathwayNoteResponse>(HttpStatusCode.Created);
        await UploadAttachmentOkAsync(_admin, $"pathwayId={pathway.Id}", SyntheticPdf, "synthetic.pdf");

        var request = await CreateAsync(pathway.PatientId);
        request.DueOn.Should().Be(UkToday.AddMonths(1));
        request.CanMakePack.Should().BeFalse();
        (await _admin.GetAsync($"/api/data-requests/{request.Id}/pack")).StatusCode.Should().Be(HttpStatusCode.Conflict);

        request = await (await _admin.PostAsJsonAsync($"/api/data-requests/{request.Id}/identity-check", new RecordIdentityCheckRequest
        {
            CheckedOn = UkToday, Method = "Documents", ExpectedVersion = request.Version
        })).ReadAsAsync<DataRequestResponse>();
        (await _admin.GetAsync($"/api/data-requests/{request.Id}/pack")).StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Only admins deal with data requests: a clinician can't see it nor review it; the admin sees the items and
        // withholds one.
        (await _clinician.Client.GetAsync($"/api/data-requests/{request.Id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _clinician.Client.GetAsync($"/api/data-requests/{request.Id}/review-items")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _clinician.Client.PutAsJsonAsync($"/api/data-requests/{request.Id}/review", new RecordDataRequestReviewRequest
        {
            ExpectedVersion = request.Version
        })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var items = await (await _admin.GetAsync($"/api/data-requests/{request.Id}/review-items"))
            .ReadAsAsync<List<DataRequestReviewItemResponse>>();
        items.Should().Contain(i => i.Kind == "Note" && i.ItemId == kept.Id);
        (await _admin.PutAsJsonAsync($"/api/data-requests/{request.Id}/review", new RecordDataRequestReviewRequest
        {
            Withheld = [new WithheldItemRequest { Kind = "Note", ItemId = withheld.Id, Reason = "Synthetic serious harm reason." }],
            ExpectedVersion = request.Version
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        var pack = await _admin.GetAsync($"/api/data-requests/{request.Id}/pack");
        pack.StatusCode.Should().Be(HttpStatusCode.OK);
        pack.Content.Headers.ContentType!.MediaType.Should().Be("application/zip");
        pack.Headers.CacheControl!.NoStore.Should().BeTrue();

        using var zip = new ZipArchive(await pack.Content.ReadAsStreamAsync(), ZipArchiveMode.Read);
        zip.Entries.Select(e => e.FullName).Should().Contain(["Your data request report.pdf", "record.json"]);
        zip.Entries.Should().ContainSingle(e => e.FullName.StartsWith("files/"));
        using var reader = new StreamReader(zip.GetEntry("record.json")!.Open(), Encoding.UTF8);
        var json = await reader.ReadToEndAsync();
        json.Should().Contain("Synthetic kept note.").And.NotContain("Synthetic withheld note.").And.NotContain("Synthetic serious harm reason.");
    }

    [Test]
    public async Task APatientWithARequest_CantBeDeleted_NorAnonymisedWhileItsOpen_AndClosingIsFinal()
    {
        var patient = await CreatePatientAsync(_admin);
        var request = await CreateAsync(patient.Id);

        (await (await ClinicalAsync(_admin)).DeleteAsync($"/api/patients/{patient.Id}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await _admin.PostAsJsonAsync($"/api/patients/{patient.Id}/anonymise", new
        {
            confirmation = $"{patient.FirstName} {patient.LastName}", reason = "RetentionPeriodEnded"
        })).StatusCode.Should().Be(HttpStatusCode.Conflict);

        var withdrawn = await (await _admin.PostAsJsonAsync($"/api/data-requests/{request.Id}/withdraw",
            new WithdrawDataRequestRequest { ExpectedVersion = request.Version })).ReadAsAsync<DataRequestResponse>();
        withdrawn.Status.Should().Be("Withdrawn");
        (await _admin.PostAsJsonAsync($"/api/data-requests/{request.Id}/refuse",
                new RefuseDataRequestRequest { Reason = "Synthetic.", ExpectedVersion = withdrawn.Version }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        var list = await (await _admin.GetAsync($"/api/data-requests/search?patientId={patient.Id}"))
            .ReadAsAsync<PagedResponse<DataRequestSummaryResponse>>();
        list.Items.Should().ContainSingle(r => r.Id == request.Id && r.Status == "Withdrawn");
    }
}
