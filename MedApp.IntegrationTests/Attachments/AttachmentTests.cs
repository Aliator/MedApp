using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using MedApp.Contracts.Attachments.Responses;
using MedApp.Contracts.Procedures.PatientProcedures.Requests;
using MedApp.Contracts.Procedures.Timeline.Responses;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.Infrastructure.Data;
using MedApp.IntegrationTests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Attachments;

/// <summary>
/// Attached files through the real API, SQL Server and Azurite: an upload and download round trip with the headers
/// that keep a file from running on the app's origin, the types refused, the size limit, thumbnails, the audit log
/// and timeline, and who may do what.
/// </summary>
[TestFixture]
public sealed class AttachmentTests : IntegrationTest
{
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj\n<< /Type /Catalog >>\nendobj\n%%EOF\n");

    private static Task<HttpResponseMessage> UploadAsync(HttpClient client, string target, byte[] bytes, string fileName)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/attachments?{target}") { Content = content };
        request.Headers.Add("X-File-Name", Uri.EscapeDataString(fileName));
        return client.SendAsync(request);
    }

    private static async Task<AttachmentResponse> UploadOkAsync(HttpClient client, string target, byte[] bytes, string fileName) =>
        await (await UploadAsync(client, target, bytes, fileName)).ReadAsAsync<AttachmentResponse>(HttpStatusCode.Created);

    private static async Task<AttachmentListResponse> ListAsync(HttpClient client, string target) =>
        await (await client.GetAsync($"/api/attachments?{target}")).ReadAsAsync<AttachmentListResponse>();

    private static byte[] Png(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.SeaGreen);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static Task<List<PatientProcedureAuditEntry>> AuditAsync(Guid patientId) =>
        RunInScopeAsync(services => services.GetRequiredService<MedAppDbContext>().PatientProcedureAuditEntries
            .Where(e => e.PatientId == patientId)
            .OrderBy(e => e.AtUtc)
            .ToListAsync());

    [Test]
    public async Task UploadAndDownload_PatientFile_RoundTripsWithTheSafeHeadersAndIsAudited()
    {
        var staff = await CreateStaffAsync(UserRole);
        var patient = await CreatePatientAsync(staff.Client);
        const string name = "Résumé \"scan\"; 1.pdf";

        var uploaded = await UploadOkAsync(staff.Client, $"patientId={patient.Id}", Pdf, name);

        uploaded.FileName.Should().Be(name);
        uploaded.ContentType.Should().Be("application/pdf");
        uploaded.SizeBytes.Should().Be(Pdf.Length);
        uploaded.Sha256.Should().Be(Convert.ToHexStringLower(SHA256.HashData(Pdf)));
        uploaded.ScanStatus.Should().Be("NotScanned");
        uploaded.AttachedTo.Should().Be("Patient");
        uploaded.CanDelete.Should().BeTrue();

        var download = await staff.Client.GetAsync($"/api/attachments/{uploaded.Id}/content");

        await download.ShouldHaveStatusAsync(HttpStatusCode.OK);
        (await download.Content.ReadAsByteArrayAsync()).Should().Equal(Pdf);
        download.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        download.Content.Headers.ContentLength.Should().Be(Pdf.Length);
        var disposition = download.Content.Headers.ContentDisposition!;
        disposition.DispositionType.Should().Be("attachment");
        // Its name as Windows allows it (no quotes), exactly in filename*, and in ASCII in filename.
        disposition.FileNameStar.Should().Be("Résumé _scan_; 1.pdf");
        disposition.FileName.Should().Be("R_sum_ _scan_; 1.pdf");
        download.Headers.GetValues("X-Content-Type-Options").Should().Equal("nosniff");
        download.Headers.CacheControl!.NoStore.Should().BeTrue();
        download.Headers.GetValues("Content-Security-Policy").Should().Equal("sandbox; default-src 'none'");
        download.Headers.GetValues("Cross-Origin-Resource-Policy").Should().Equal("same-origin");

        (await ListAsync(staff.Client, $"patientId={patient.Id}")).Items.Select(i => i.Id).Should().Equal(uploaded.Id);
        (await AuditAsync(patient.Id)).Select(e => (e.Action, e.PatientProcedureId, e.UserId)).Should().Equal(
            (PatientProcedureAuditAction.AttachmentAdded, PatientProcedureAuditEntry.NoProcedure, staff.UserId),
            (PatientProcedureAuditAction.AttachmentDownloaded, PatientProcedureAuditEntry.NoProcedure, staff.UserId));
    }

    [Test]
    public async Task Timeline_FileEntries_ShowStaffThatAFileWasAttachedAndAdminsItsName()
    {
        var admin = await SignInAsAdminAsync();
        var staff = await CreateStaffAsync(UserRole);
        var procedure = await CreatePatientWithProcedureAsync(admin);
        var file = await UploadOkAsync(staff.Client, $"procedureId={procedure.Id}", Pdf, "synthetic-letter.pdf");
        await (await staff.Client.GetAsync($"/api/attachments/{file.Id}/content")).ShouldHaveStatusAsync(HttpStatusCode.OK);

        var staffView = await (await staff.Client.GetAsync($"/api/patients/{procedure.PatientId}/timeline?kinds=Files"))
            .ReadAsAsync<PatientTimelinePageResponse>();
        var adminView = await (await admin.GetAsync($"/api/patients/{procedure.PatientId}/timeline?kinds=Files"))
            .ReadAsAsync<PatientTimelinePageResponse>();

        var seen = staffView.Items.Should().ContainSingle("downloads are never on the timeline").Subject;
        seen.Action.Should().Be("AttachmentAdded");
        seen.AttachmentId.Should().Be(file.Id);
        seen.AttachedTo.Should().Be("Procedure");
        seen.ProcedureId.Should().Be(procedure.Id);
        seen.FileName.Should().BeNull();
        adminView.Items.Single().FileName.Should().Be("synthetic-letter.pdf");
    }

    [Test]
    public async Task Upload_Image_GetsAThumbnailServedTheSafeWay()
    {
        var staff = await CreateStaffAsync(UserRole);
        var patient = await CreatePatientAsync(staff.Client);

        var uploaded = await UploadOkAsync(staff.Client, $"patientId={patient.Id}", Png(1000, 500), "photo.png");
        var thumbnail = await staff.Client.GetAsync($"/api/attachments/{uploaded.Id}/thumbnail");

        uploaded.HasThumbnail.Should().BeTrue();
        await thumbnail.ShouldHaveStatusAsync(HttpStatusCode.OK);
        thumbnail.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        thumbnail.Headers.GetValues("X-Content-Type-Options").Should().Equal("nosniff");
        thumbnail.Headers.GetValues("Content-Security-Policy").Should().Equal("sandbox; default-src 'none'");
        using var decoded = SKBitmap.Decode(await thumbnail.Content.ReadAsByteArrayAsync());
        (decoded.Width, decoded.Height).Should().Be((320, 160));
    }

    private static IEnumerable<TestCaseData> RefusedFiles()
    {
        yield return new TestCaseData(Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>"), "image.svg", "SVG images*").SetName("Upload_Svg_Returns415");
        yield return new TestCaseData(Encoding.UTF8.GetBytes("<!doctype html><script>alert(1)</script>"), "notes.txt", "Web pages (HTML)*").SetName("Upload_HtmlNamedTxt_Returns415");
        yield return new TestCaseData(Pdf, "photo.png", "The file's name ends in .png, but it's a PDF*").SetName("Upload_PdfNamedPng_Returns415");
        yield return new TestCaseData(Encoding.ASCII.GetBytes("GIF89a\u0001\u0000"), "anim.gif", "This kind of file can't be attached*").SetName("Upload_Gif_Returns415");
        yield return new TestCaseData(Array.Empty<byte>(), "empty.txt", "The file is empty.").SetName("Upload_EmptyFile_Returns415");
    }

    [TestCaseSource(nameof(RefusedFiles))]
    public async Task Upload_RefusedType_Returns415AndStoresNothing(byte[] bytes, string fileName, string message)
    {
        var staff = await CreateStaffAsync(UserRole);
        var patient = await CreatePatientAsync(staff.Client);

        var response = await UploadAsync(staff.Client, $"patientId={patient.Id}", bytes, fileName);

        var problem = await response.ReadAsAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>(HttpStatusCode.UnsupportedMediaType);
        problem.Title.Should().Match(message);
        (await ListAsync(staff.Client, $"patientId={patient.Id}")).Items.Should().BeEmpty();
        (await AuditAsync(patient.Id)).Should().BeEmpty();
    }

    [Test]
    public async Task Upload_OverTwentyMegabytes_Returns413()
    {
        var staff = await CreateStaffAsync(UserRole);
        var patient = await CreatePatientAsync(staff.Client);
        var bytes = new byte[20 * 1024 * 1024 + 1];
        Pdf.CopyTo(bytes, 0);

        var response = await UploadAsync(staff.Client, $"patientId={patient.Id}", bytes, "big.pdf");

        await response.ShouldHaveStatusAsync(HttpStatusCode.RequestEntityTooLarge);
        (await ListAsync(staff.Client, $"patientId={patient.Id}")).Items.Should().BeEmpty();
    }

    [Test]
    public async Task Upload_ExactlyTwentyMegabytes_IsAccepted()
    {
        var staff = await CreateStaffAsync(UserRole);
        var patient = await CreatePatientAsync(staff.Client);
        var bytes = new byte[20 * 1024 * 1024];
        Pdf.CopyTo(bytes, 0);

        var uploaded = await UploadOkAsync(staff.Client, $"patientId={patient.Id}", bytes, "big.pdf");

        uploaded.SizeBytes.Should().Be(bytes.Length);
        (await (await staff.Client.GetAsync($"/api/attachments/{uploaded.Id}/content")).Content.ReadAsByteArrayAsync())
            .Should().HaveCount(bytes.Length);
    }

    [Test]
    public async Task Upload_OtherBodyType_Returns415()
    {
        var staff = await CreateStaffAsync(UserRole);
        var patient = await CreatePatientAsync(staff.Client);

        var response = await staff.Client.PostAsJsonAsync($"/api/attachments?patientId={patient.Id}", new { file = "x" });

        await response.ShouldHaveStatusAsync(HttpStatusCode.UnsupportedMediaType);
    }

    private static IEnumerable<TestCaseData> Endpoints()
    {
        var id = Guid.Parse("0f0e0d0c-0b0a-0908-0706-050403020100");
        yield return new TestCaseData("GET", $"/api/attachments?patientId={id}");
        yield return new TestCaseData("POST", $"/api/attachments?patientId={id}");
        yield return new TestCaseData("GET", $"/api/attachments/{id}/content");
        yield return new TestCaseData("GET", $"/api/attachments/{id}/thumbnail");
        yield return new TestCaseData("DELETE", $"/api/attachments/{id}");
    }

    [TestCaseSource(nameof(Endpoints))]
    public async Task Endpoint_SignedOut_Returns401(string method, string url)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method == "POST")
            request.Content = new ByteArrayContent(Pdf) { Headers = { ContentType = new MediaTypeHeaderValue("application/octet-stream") } };

        await (await NewClient().SendAsync(request)).ShouldHaveStatusAsync(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Delete_StaffSomeoneElsesFile_Returns403_AndAnAdminCanDeleteIt()
    {
        var owner = await CreateStaffAsync(UserRole);
        var other = await CreateStaffAsync(UserRole);
        var admin = await SignInAsAdminAsync();
        var patient = await CreatePatientAsync(owner.Client);
        var file = await UploadOkAsync(owner.Client, $"patientId={patient.Id}", Pdf, "letter.pdf");

        (await ListAsync(other.Client, $"patientId={patient.Id}")).Items.Single().CanDelete.Should().BeFalse();
        await (await other.Client.DeleteAsync($"/api/attachments/{file.Id}")).ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
        await (await admin.DeleteAsync($"/api/attachments/{file.Id}")).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        (await ListAsync(owner.Client, $"patientId={patient.Id}")).Items.Should().BeEmpty();
        await (await owner.Client.GetAsync($"/api/attachments/{file.Id}/content")).ShouldHaveStatusAsync(HttpStatusCode.NotFound);
        await (await admin.GetAsync($"/api/attachments/{file.Id}/content")).ShouldHaveStatusAsync(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task List_IncludeDeleted_ShowsAdminsTheDeletedFileButNotStaff()
    {
        var staff = await CreateStaffAsync(UserRole);
        var admin = await SignInAsAdminAsync();
        var patient = await CreatePatientAsync(staff.Client);
        var file = await UploadOkAsync(staff.Client, $"patientId={patient.Id}", Pdf, "letter.pdf");
        await (await staff.Client.DeleteAsync($"/api/attachments/{file.Id}")).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        var adminList = await ListAsync(admin, $"patientId={patient.Id}&includeDeleted=true");
        var staffList = await ListAsync(staff.Client, $"patientId={patient.Id}&includeDeleted=true");

        var deleted = adminList.Items.Should().ContainSingle().Subject;
        deleted.IsDeleted.Should().BeTrue();
        deleted.DeletedByUsername.Should().Be(staff.Username);
        deleted.CanDelete.Should().BeFalse();
        staffList.Items.Should().BeEmpty();
        (await AuditAsync(patient.Id)).Select(e => e.Action).Should().Equal(
            PatientProcedureAuditAction.AttachmentAdded, PatientProcedureAuditAction.AttachmentDeleted);
    }

    [Test]
    public async Task Stage_FilesFollowItsNotes_ItsOwnListAndLockedOnceFinished()
    {
        var admin = await SignInAsAdminAsync();
        var procedure = await CreatePatientWithProcedureAsync(admin);
        var first = procedure.Stages.OrderBy(s => s.StageOrder).First();

        var onStage = await UploadOkAsync(admin, $"stageId={first.Id}", Pdf, "consent.pdf");
        var onProcedure = await UploadOkAsync(admin, $"procedureId={procedure.Id}", Pdf, "referral.pdf");
        await (await FinishStageAsync(admin, procedure.Id, first.Id)).ShouldHaveStatusAsync(HttpStatusCode.OK);

        (await ListAsync(admin, $"stageId={first.Id}")).Items.Select(i => i.Id).Should().Equal(onStage.Id);
        (await ListAsync(admin, $"procedureId={procedure.Id}")).Items.Select(i => i.Id).Should().Equal(onProcedure.Id);
        (await ListAsync(admin, $"patientId={procedure.PatientId}")).Items.Select(i => i.Id).Should().Equal(onProcedure.Id, onStage.Id);
        var locked = await UploadAsync(admin, $"stageId={first.Id}", Pdf, "late.pdf");
        (await locked.ReadAsAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>(HttpStatusCode.Conflict)).Title.Should().Contain("finished and locked");
    }

    [Test]
    public async Task Delete_PatientOrProcedureWithFiles_Returns409()
    {
        var admin = await SignInAsAdminAsync();
        var procedure = await CreatePatientWithProcedureAsync(admin);
        var patientOnly = await CreatePatientAsync(admin);
        await UploadOkAsync(admin, $"procedureId={procedure.Id}", Pdf, "letter.pdf");
        await UploadOkAsync(admin, $"patientId={patientOnly.Id}", Pdf, "letter.pdf");
        await (await admin.PostAsJsonAsync($"{ProcedureUrl(procedure.Id)}/status",
            new ChangePatientProcedureStatusRequest { Status = "Cancelled", Reason = BlockReason })).ShouldHaveStatusAsync(HttpStatusCode.OK);

        var procedureDelete = await admin.DeleteAsync(ProcedureUrl(procedure.Id));
        var patientDelete = await admin.DeleteAsync($"/api/patients/{patientOnly.Id}");

        (await procedureDelete.ReadAsAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>(HttpStatusCode.Conflict)).Title.Should().StartWith("This procedure has 1 file attached");
        (await patientDelete.ReadAsAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>(HttpStatusCode.Conflict)).Title.Should().StartWith("This patient has 1 file attached");
    }

    [Test]
    public async Task List_UnknownPatient_Returns404()
    {
        var staff = await CreateStaffAsync(UserRole);

        await (await staff.Client.GetAsync($"/api/attachments?patientId={Guid.NewGuid()}")).ShouldHaveStatusAsync(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Api_WithoutFileStorage_RefusesUploadsWith503AndStillLists()
    {
        await using var api = new MedAppApiFactory(TestEnvironment.ConnectionString);
        var client = api.CreateApiClient();
        await (await client.PostAsJsonAsync("/api/auth/login", new MedApp.Contracts.Authentication.Requests.LoginRequest(AdminUsername, AdminPassword)))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);
        var patient = await CreatePatientAsync(client);

        var upload = await UploadAsync(client, $"patientId={patient.Id}", Pdf, "letter.pdf");

        (await upload.ReadAsAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>(HttpStatusCode.ServiceUnavailable)).Title.Should().StartWith("File storage isn't set up");
        (await ListAsync(client, $"patientId={patient.Id}")).Items.Should().BeEmpty();
    }
}
