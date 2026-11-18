using FluentAssertions;
using MedApp.API.Controllers;
using MedApp.Application.Attachments.Commands.DeleteAttachment;
using MedApp.Application.Attachments.Commands.DownloadAttachment;
using MedApp.Application.Attachments.Commands.UploadAttachment;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Attachments.Queries.GetAttachments;
using MedApp.Application.Attachments.Queries.GetAttachmentThumbnail;
using MedApp.Application.Procedures.Common;
using MedApp.Contracts.Attachments.Requests;
using MedApp.Contracts.Attachments.Responses;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Mvc;
using static MedApp.UnitTests.Common.Constants.ApiTestConstants;

namespace MedApp.UnitTests.Api.Controllers;

[TestFixture]
public sealed class AttachmentsControllerTests
{
    private static readonly ProcedureActor Actor = new(UserId, Username);

    [TestCase("User", false)]
    [TestCase("Admin", true)]
    public async Task GetAttachments_SendsTheTargetTheViewerAndWhetherTheyAreAnAdmin(string role, bool isAdmin)
    {
        var list = new AttachmentListResponse();
        var mediator = new RecordingMediator(_ => list);
        using var cts = new CancellationTokenSource();
        var request = new GetAttachmentsRequest { PatientId = RecordId, IncludeDeleted = true };

        var result = await new AttachmentsController(mediator).WithRequest(SignedIn(role)).GetAttachments(request, cts.Token);

        mediator.Request.Should().Be(new GetAttachmentsQuery(RecordId, null, null, true, UserId, isAdmin));
        mediator.Tokens.Single().Should().Be(cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(list);
    }

    [Test]
    public async Task Upload_SendsTheBodyItsLengthAndTheDecodedName_AndAnswersCreated()
    {
        var created = new AttachmentResponse { Id = OtherId };
        var mediator = new RecordingMediator(_ => created);
        using var body = new MemoryStream([1, 2, 3]);
        var controller = new AttachmentsController(mediator).WithRequest(SignedIn("User"), context =>
        {
            context.Request.Body = body;
            context.Request.ContentLength = 3;
            context.Request.ContentType = "application/octet-stream";
        });

        var result = await controller.Upload(new AttachmentTargetRequest { StageId = RecordId }, "r%C3%A9sum%C3%A9.pdf", CancellationToken.None);

        mediator.Request.Should().Be(new UploadAttachmentCommand(null, null, RecordId, "résumé.pdf", 3, body, Actor, false));
        var createdResult = result.Should().BeOfType<CreatedResult>().Subject;
        createdResult.Location.Should().Be($"/api/attachments/{OtherId}/content");
        createdResult.Value.Should().Be(created);
    }

    [TestCase(null)]
    [TestCase("application/json")]
    [TestCase("multipart/form-data; boundary=x")]
    [TestCase("not a type")]
    public async Task Upload_AnyOtherBodyType_ThrowsUnsupportedWithoutSendingIt(string? contentType)
    {
        var mediator = new RecordingMediator();
        var controller = new AttachmentsController(mediator).WithRequest(SignedIn("User"), context => context.Request.ContentType = contentType);

        var act = () => controller.Upload(new AttachmentTargetRequest { PatientId = RecordId }, "a.pdf", CancellationToken.None);

        await act.Should().ThrowAsync<UnsupportedFileTypeException>().WithMessage("Send the file's bytes as application/octet-stream.");
        mediator.Sent.Should().BeEmpty();
    }

    [Test]
    public async Task Download_SendsTheFileWithTheSafeHeaders()
    {
        var content = new MemoryStream([1]);
        var mediator = new RecordingMediator(_ => new AttachmentDownload("scan.pdf", "application/pdf", 1, content));
        var controller = new AttachmentsController(mediator).WithRequest(SignedIn("User"));

        var result = await controller.Download(RecordId, CancellationToken.None);

        mediator.Request.Should().Be(new DownloadAttachmentCommand(RecordId, Actor));
        var file = result.Should().BeOfType<FileStreamResult>().Subject;
        file.FileStream.Should().BeSameAs(content);
        file.ContentType.Should().Be("application/pdf");
        file.FileDownloadName.Should().BeEmpty("the controller writes Content-Disposition itself");
        controller.Response.Headers.ContentDisposition.ToString().Should().StartWith("attachment;");
        controller.Response.Headers.ContentSecurityPolicy.ToString().Should().Be("sandbox; default-src 'none'");
    }

    [Test]
    public async Task Thumbnail_SendsItWithTheSafeHeaders()
    {
        var mediator = new RecordingMediator(_ => new AttachmentDownload("thumbnail.png", "image/png", null, new MemoryStream()));
        var controller = new AttachmentsController(mediator).WithRequest(SignedIn("User"));

        var result = await controller.Thumbnail(RecordId, CancellationToken.None);

        mediator.Request.Should().Be(new GetAttachmentThumbnailQuery(RecordId));
        result.Should().BeOfType<FileStreamResult>().Which.ContentType.Should().Be("image/png");
        controller.Response.Headers.XContentTypeOptions.ToString().Should().Be("nosniff");
    }

    [TestCase("User", false)]
    [TestCase("Admin", true)]
    public async Task Delete_SendsWhoAndWhetherTheyAreAnAdmin_AndAnswersNoContent(string role, bool isAdmin)
    {
        var mediator = new RecordingMediator();
        using var cts = new CancellationTokenSource();

        var result = await new AttachmentsController(mediator).WithRequest(SignedIn(role)).Delete(RecordId, cts.Token);

        mediator.Request.Should().Be(new DeleteAttachmentCommand(RecordId, Actor with { IsAdmin = isAdmin }, isAdmin));
        mediator.Tokens.Single().Should().Be(cts.Token);
        result.Should().BeOfType<NoContentResult>();
    }
}
