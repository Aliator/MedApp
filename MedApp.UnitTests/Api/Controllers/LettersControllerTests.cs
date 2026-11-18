using AutoFixture;
using FluentAssertions;
using MedApp.API.Controllers;
using MedApp.Application.Letters.Commands.ApproveLetter;
using MedApp.Application.Letters.Commands.CreateLetter;
using MedApp.Application.Letters.Commands.ReturnLetter;
using MedApp.Application.Letters.Commands.SendLetter;
using MedApp.Application.Letters.Commands.SubmitLetter;
using MedApp.Application.Letters.Commands.UpdateLetter;
using MedApp.Application.Letters.Commands.WithdrawLetter;
using MedApp.Application.Letters.Commands.WriteLetterCorrection;
using MedApp.Application.Letters.Common;
using MedApp.Application.Letters.Queries.GetLetter;
using MedApp.Application.Letters.Queries.PreviewLetterTemplate;
using MedApp.Application.Letters.Queries.SearchLetters;
using MedApp.Contracts.Common;
using MedApp.Contracts.Letters.Requests;
using MedApp.Contracts.Letters.Responses;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Mvc;
using static MedApp.UnitTests.Common.Constants.ApiTestConstants;
using Permission = MedApp.Application.Common.Identity.Permission;

namespace MedApp.UnitTests.Api.Controllers;

[TestFixture]
public sealed class LettersControllerTests
{
    private static readonly Guid RecipientId = Guid.Parse("0000000e-0000-0000-0000-0000000000f1");

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

    private static LettersController Controller(RecordingMediator mediator) =>
        new LettersController(mediator).WithRequest(SignedInWith(Permission.WriteLetters, Permission.ViewPatients));

    private async Task<(RecordingMediator Mediator, IActionResult Result)> Run(object response, Func<LettersController, Task<IActionResult>> act)
    {
        var mediator = new RecordingMediator(_ => response);
        var result = await act(Controller(mediator));
        mediator.Tokens.Single().Should().Be(_cts.Token);
        return (mediator, result);
    }

    [Test]
    public async Task CreateLetter_SendsTheTemplateAndPathway_AndAnswersCreatedAtItsAddress()
    {
        var letter = _fixture.Create<LetterResponse>();
        var templateId = Guid.NewGuid();

        var (mediator, result) = await Run(letter, c => c.CreateLetter(new CreateLetterRequest { TemplateId = templateId, PathwayId = RecordId }, _cts.Token));

        mediator.Request.Should().BeOfType<CreateLetterCommand>().Which.Should().BeEquivalentTo(
            new { TemplateId = templateId, PathwayId = (Guid?)RecordId, ReferralId = (Guid?)null });
        result.Should().BeOfType<CreatedResult>().Which.Location.Should().Be($"/api/letters/{letter.Id}");
    }

    [Test]
    public async Task SearchLetters_SendsTheViewAndFilters_WithTheViewer()
    {
        var page = new PagedResponse<LetterSummaryResponse>([], 2, 5, 0, 0);

        var (mediator, result) = await Run(page, c => c.SearchLetters(
            new SearchLettersRequest { Page = 2, PageSize = 5, View = "ToSend", PatientId = RecordId }, _cts.Token));

        mediator.Request.Should().BeOfType<SearchLettersQuery>().Which.Should().BeEquivalentTo(
            new { Page = 2, PageSize = 5, View = "ToSend", PatientId = (Guid?)RecordId, PathwayId = (Guid?)null, ReferralId = (Guid?)null });
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(page);
    }

    [Test]
    public async Task CountLetters_AsksForTheViewersCount()
    {
        var count = new LetterCountResponse { ToApprove = 1, ToSend = 2 };

        var (mediator, result) = await Run(count, c => c.CountLetters(_cts.Token));

        mediator.Request.Should().BeOfType<CountLettersQuery>();
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(count);
    }

    [Test]
    public async Task GetLetter_AsksForIt()
    {
        var (mediator, _) = await Run(_fixture.Create<LetterResponse>(), c => c.GetLetter(RecordId, _cts.Token));

        mediator.Request.Should().BeOfType<GetLetterQuery>().Which.LetterId.Should().Be(RecordId);
    }

    [Test]
    public async Task UpdateLetter_SendsTheTextRecipientsAndVersion()
    {
        var request = new UpdateLetterRequest
        {
            Letterhead = "Head",
            Body = "Body",
            Closing = "Close",
            LargePrint = true,
            Recipients = [new LetterRecipientRequest { Kind = "Other", Name = "A Person", AddressLines = "1 Street", Postcode = "ZZ1 1ZZ" }],
            ExpectedVersion = 4,
        };

        var (mediator, _) = await Run(_fixture.Create<LetterResponse>(), c => c.UpdateLetter(RecordId, request, _cts.Token));

        var command = mediator.Request.Should().BeOfType<UpdateLetterCommand>().Subject;
        command.Should().BeEquivalentTo(new { LetterId = RecordId, Letterhead = "Head", Body = "Body", Closing = "Close", LargePrint = true, ExpectedVersion = 4 });
        command.Recipients.Should().Equal(new LetterRecipientCommandItem("Other", null, "A Person", "1 Street", "ZZ1 1ZZ", null));
    }

    [Test]
    public async Task PreviewLetter_SendsThePdfAsADownload()
    {
        var pdf = new LetterPdf("letter-preview.pdf", [0x25, 0x50, 0x44, 0x46]);
        var mediator = new RecordingMediator(_ => pdf);
        var controller = Controller(mediator);

        var result = await controller.PreviewLetter(RecordId, _cts.Token);

        mediator.Request.Should().BeOfType<PreviewLetterQuery>().Which.LetterId.Should().Be(RecordId);
        result.Should().BeOfType<FileStreamResult>().Which.ContentType.Should().Be(LetterPdf.ContentType);
        controller.Response.Headers.ContentDisposition.ToString().Should().StartWith("attachment");
    }

    [Test]
    public async Task WorkflowActions_SendTheirCommands()
    {
        var response = _fixture.Create<LetterResponse>();
        var approver = Guid.NewGuid();

        (await Run(response, c => c.SubmitLetter(RecordId, new SubmitLetterRequest { ApproverUserId = approver, ExpectedVersion = 2 }, _cts.Token)))
            .Mediator.Request.Should().BeOfType<SubmitLetterCommand>().Which.Should().BeEquivalentTo(new { LetterId = RecordId, ApproverUserId = approver, ExpectedVersion = 2 });
        (await Run(response, c => c.ApproveLetter(RecordId, new LetterVersionRequest { ExpectedVersion = 3 }, _cts.Token)))
            .Mediator.Request.Should().BeOfType<ApproveLetterCommand>().Which.ExpectedVersion.Should().Be(3);
        (await Run(response, c => c.ReturnLetter(RecordId, new ReturnLetterRequest { Comment = "Fix it", ExpectedVersion = 3 }, _cts.Token)))
            .Mediator.Request.Should().BeOfType<ReturnLetterCommand>().Which.Comment.Should().Be("Fix it");
        (await Run(response, c => c.DiscardLetter(RecordId, new LetterVersionRequest { ExpectedVersion = 1 }, _cts.Token)))
            .Mediator.Request.Should().BeOfType<DiscardLetterCommand>().Which.ExpectedVersion.Should().Be(1);
        (await Run(response, c => c.WithdrawLetter(RecordId, new WithdrawLetterRequest { Reason = "Wrong", ExpectedVersion = 5 }, _cts.Token)))
            .Mediator.Request.Should().BeOfType<WithdrawLetterCommand>().Which.Reason.Should().Be("Wrong");
        (await Run(response, c => c.SendLetter(RecordId, RecipientId, new LetterVersionRequest { ExpectedVersion = 6 }, _cts.Token)))
            .Mediator.Request.Should().BeOfType<SendLetterCommand>().Which.Should().BeEquivalentTo(new { LetterId = RecordId, RecipientId, ExpectedVersion = 6 });
        (await Run(response, c => c.RecordSent(RecordId, RecipientId,
                new RecordLetterSentRequest { Method = "Post", SentOn = new DateOnly(2026, 11, 18), ExpectedVersion = 6 }, _cts.Token)))
            .Mediator.Request.Should().BeOfType<RecordLetterSentCommand>().Which.Should().BeEquivalentTo(
                new { LetterId = RecordId, RecipientId, Method = "Post", SentOn = new DateOnly(2026, 11, 18), ExpectedVersion = 6 });
    }

    [Test]
    public async Task WriteCorrection_AnswersCreatedAtTheNewLetter()
    {
        var letter = _fixture.Create<LetterResponse>();

        var (mediator, result) = await Run(letter, c => c.WriteCorrection(RecordId, _cts.Token));

        mediator.Request.Should().BeOfType<WriteLetterCorrectionCommand>().Which.LetterId.Should().Be(RecordId);
        result.Should().BeOfType<CreatedResult>().Which.Location.Should().Be($"/api/letters/{letter.Id}");
    }

    [TestCase(nameof(LettersController.CreateLetter), Permission.WriteLetters)]
    [TestCase(nameof(LettersController.UpdateLetter), Permission.WriteLetters)]
    [TestCase(nameof(LettersController.SubmitLetter), Permission.WriteLetters)]
    [TestCase(nameof(LettersController.DiscardLetter), Permission.WriteLetters)]
    [TestCase(nameof(LettersController.WriteCorrection), Permission.WriteLetters)]
    [TestCase(nameof(LettersController.ApproveLetter), Permission.ApproveLetters)]
    [TestCase(nameof(LettersController.ReturnLetter), Permission.ApproveLetters)]
    [TestCase(nameof(LettersController.SendLetter), Permission.SendLetters)]
    [TestCase(nameof(LettersController.RecordSent), Permission.SendLetters)]
    [TestCase(nameof(LettersController.SearchLetters), Permission.ViewPatients)]
    [TestCase(nameof(LettersController.CountLetters), Permission.ViewPatients)]
    [TestCase(nameof(LettersController.GetLetter), Permission.ViewPatients)]
    [TestCase(nameof(LettersController.PreviewLetter), Permission.ViewPatients)]
    [TestCase(nameof(LettersController.WithdrawLetter), Permission.ViewPatients)]
    public async Task EachAction_NeedsItsPermission(string action, Permission permission)
    {
        (await ControllerAuthorisation.MayCallAsync<LettersController>(action, SignedInWith(permission))).Should().BeTrue(action);
        (await ControllerAuthorisation.MayCallAsync<LettersController>(action, SignedInWith(Permission.UseApp))).Should().BeFalse(action);
    }

    [Test]
    public void EveryAction_IsCoveredAbove() =>
        ControllerAuthorisation.Actions<LettersController>().Should().HaveCount(14);
}
