using AutoFixture;
using FluentAssertions;
using MedApp.API.Attachments;
using MedApp.API.Controllers;
using MedApp.Application.Letters.Commands.CreateLetterTemplate;
using MedApp.Application.Letters.Commands.DeleteLetterTemplate;
using MedApp.Application.Letters.Commands.SetLetterTemplateArchived;
using MedApp.Application.Letters.Commands.UpdateLetterTemplate;
using MedApp.Application.Letters.Queries.GetLetterMergeFields;
using MedApp.Application.Letters.Queries.GetLetterTemplateById;
using MedApp.Application.Letters.Queries.GetLetterTemplates;
using MedApp.Application.Letters.Queries.PreviewLetterTemplate;
using MedApp.Contracts.Letters.Requests;
using MedApp.Contracts.Letters.Responses;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Mvc;
using static MedApp.UnitTests.Common.Constants.ApiTestConstants;
using static MedApp.UnitTests.Common.Constants.LettersTestConstants;
using Permission = MedApp.Application.Common.Identity.Permission;

namespace MedApp.UnitTests.Api.Controllers;

[TestFixture]
public sealed class LetterTemplatesControllerTests
{
    /// <summary>Holders of ManageSetup keep the templates; everyone signed in reads them.</summary>
    private static readonly string[] Changes =
    [
        nameof(LetterTemplatesController.CreateLetterTemplate),
        nameof(LetterTemplatesController.UpdateLetterTemplate),
        nameof(LetterTemplatesController.ArchiveLetterTemplate),
        nameof(LetterTemplatesController.RestoreLetterTemplate),
        nameof(LetterTemplatesController.PreviewLetterTemplate),
        nameof(LetterTemplatesController.DeleteLetterTemplate),
    ];

    private static readonly string[] Reads =
    [
        nameof(LetterTemplatesController.GetLetterTemplates),
        nameof(LetterTemplatesController.GetLetterTemplate),
        nameof(LetterTemplatesController.GetMergeFields),
    ];

    private IFixture _fixture = null!;
    private CancellationTokenSource _cts = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        _cts = new CancellationTokenSource();
    }

    [TearDown]
    public void TearDown() => _cts.Dispose();

    private static LetterTemplatesController Controller(RecordingMediator mediator) =>
        new LetterTemplatesController(mediator).WithRequest(SignedIn("Admin"));

    private async Task ExpectOk(object response, Func<object?, bool> request, Func<LetterTemplatesController, Task<IActionResult>> act)
    {
        var mediator = new RecordingMediator(_ => response);

        var result = await act(Controller(mediator));

        request(mediator.Request).Should().BeTrue(mediator.Request?.ToString());
        mediator.Tokens.Single().Should().Be(_cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(response);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task GetLetterTemplates_AsksForArchivedOnesOrNot(bool includeArchived)
    {
        IReadOnlyList<LetterTemplateResponse> templates = [];
        await ExpectOk(templates, r => Equals(r, new GetLetterTemplatesQuery(includeArchived)),
            c => c.GetLetterTemplates(includeArchived, _cts.Token));
    }

    [Test]
    public async Task GetLetterTemplate_ReturnsIt() =>
        await ExpectOk(_fixture.Create<LetterTemplateResponse>(), r => Equals(r, new GetLetterTemplateByIdQuery(RecordId)),
            c => c.GetLetterTemplate(RecordId, _cts.Token));

    [Test]
    public async Task GetMergeFields_ReturnsThem()
    {
        IReadOnlyList<LetterMergeFieldResponse> fields = [];
        await ExpectOk(fields, r => r is GetLetterMergeFieldsQuery, c => c.GetMergeFields(_cts.Token));
    }

    [Test]
    public async Task CreateLetterTemplate_SendsEveryDetail_AndReturnsItWithItsAddress()
    {
        var template = _fixture.Create<LetterTemplateResponse>();
        var mediator = new RecordingMediator(_ => template);
        var request = new CreateLetterTemplateRequest
        {
            Name = TemplateName, Kind = "ClinicLetter", TeamId = OtherId, Letterhead = Letterhead,
            BodySkeleton = BodySkeleton, Closing = Closing
        };

        var result = await Controller(mediator).CreateLetterTemplate(request, _cts.Token);

        mediator.Request.Should().Be(
            new CreateLetterTemplateCommand(TemplateName, "ClinicLetter", OtherId, Letterhead, BodySkeleton, Closing));
        mediator.Tokens.Single().Should().Be(_cts.Token);
        var created = result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be($"/api/letter-templates/{template.Id}");
        created.Value.Should().Be(template);
    }

    [Test]
    public async Task UpdateLetterTemplate_SendsTheDetailsAndTheVersionSeen() =>
        await ExpectOk(_fixture.Create<LetterTemplateResponse>(),
            r => Equals(r, new UpdateLetterTemplateCommand(RecordId, OtherTemplateName, "Other", null, "h", "b", "c", 4)),
            c => c.UpdateLetterTemplate(RecordId, new UpdateLetterTemplateRequest
            {
                Name = OtherTemplateName, Kind = "Other", Letterhead = "h", BodySkeleton = "b", Closing = "c", ExpectedVersion = 4
            }, _cts.Token));

    [Test]
    public async Task ArchiveAndRestoreLetterTemplate_SetItsArchivedFlagFromTheVersionSeen()
    {
        var template = _fixture.Create<LetterTemplateResponse>();
        var request = new LetterTemplateVersionRequest { ExpectedVersion = 2 };
        await ExpectOk(template, r => Equals(r, new SetLetterTemplateArchivedCommand(RecordId, true, 2)),
            c => c.ArchiveLetterTemplate(RecordId, request, _cts.Token));
        await ExpectOk(template, r => Equals(r, new SetLetterTemplateArchivedCommand(RecordId, false, 2)),
            c => c.RestoreLetterTemplate(RecordId, request, _cts.Token));
    }

    [Test]
    public async Task PreviewLetterTemplate_SendsThePdfAsADownload_NeverInline()
    {
        var mediator = new RecordingMediator(_ => new LetterPdf("letter-template-preview.pdf", [37, 80, 68, 70]));
        var controller = Controller(mediator);

        var result = await controller.PreviewLetterTemplate(new PreviewLetterTemplateRequest
        {
            Name = TemplateName, Letterhead = "h", BodySkeleton = "b", Closing = "c", LargePrint = true
        }, _cts.Token);

        mediator.Request.Should().Be(new PreviewLetterTemplateQuery(TemplateName, "h", "b", "c", true));
        mediator.Tokens.Single().Should().Be(_cts.Token);
        var file = result.Should().BeOfType<FileStreamResult>().Subject;
        file.ContentType.Should().Be("application/pdf");
        using var content = new MemoryStream();
        await file.FileStream.CopyToAsync(content);
        content.ToArray().Should().Equal(37, 80, 68, 70);
        var headers = controller.Response.Headers;
        headers.ContentDisposition.ToString().Should().StartWith("attachment; filename=\"letter-template-preview.pdf\"");
        headers.ContentSecurityPolicy.ToString().Should().Be(AttachmentResponseHeaders.ContentSecurityPolicy);
        headers.CacheControl.ToString().Should().Be("no-store");
        controller.Response.ContentLength.Should().Be(4);
    }

    [TestCase(nameof(LetterTemplatesController.CreateLetterTemplate))]
    [TestCase(nameof(LetterTemplatesController.UpdateLetterTemplate))]
    [TestCase(nameof(LetterTemplatesController.PreviewLetterTemplate))]
    public void TextSendingActions_LimitTheRequestSize(string action) =>
        typeof(LetterTemplatesController).GetMethod(action)!
            .GetCustomAttributes(typeof(RequestSizeLimitAttribute), false).Should().ContainSingle();

    [Test]
    public async Task Changes_NeedManageSetup()
    {
        foreach (var action in Changes)
        {
            (await ControllerAuthorisation.MayCallAsync<LetterTemplatesController>(action, SignedInWith(Permission.ManageSetup)))
                .Should().BeTrue(action);
            (await ControllerAuthorisation.MayCallAsync<LetterTemplatesController>(action, SignedInWith(Permission.UseApp)))
                .Should().BeFalse(action);
        }
    }

    [Test]
    public async Task Reading_NeedsUseApp()
    {
        foreach (var action in Reads)
        {
            (await ControllerAuthorisation.MayCallAsync<LetterTemplatesController>(action, SignedInWith(Permission.UseApp)))
                .Should().BeTrue(action);
            (await ControllerAuthorisation.MayCallAsync<LetterTemplatesController>(action, SignedIn())).Should().BeFalse(action);
        }
    }

    [Test]
    public async Task DeleteLetterTemplate_SendsTheCommand_AndAnswersNoContent()
    {
        var mediator = new RecordingMediator(_ => MediatR.Unit.Value);

        var result = await Controller(mediator).DeleteLetterTemplate(RecordId, _cts.Token);

        mediator.Request.Should().Be(new DeleteLetterTemplateCommand(RecordId));
        mediator.Tokens.Single().Should().Be(_cts.Token);
        result.Should().BeOfType<NoContentResult>();
    }
}
