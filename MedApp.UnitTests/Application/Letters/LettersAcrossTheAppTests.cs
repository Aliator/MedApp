using System.Text.Json;
using AutoFixture;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Attachments.Commands.DeleteAttachment;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Letters.Commands.DeleteLetterTemplate;
using MedApp.Application.Letters.Common;
using MedApp.Application.Letters.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.PatientPathways.Commands.DeletePatientPathway;
using MedApp.Application.Pathways.PatientPathways.Commands.FinishPathwayStage;
using MedApp.Application.Pathways.PatientPathwayStages.Definitions;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Pathways.Timeline.Common;
using MedApp.Application.Patients.Commands.AnonymisePatient;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Domain.Letters;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Infrastructure.Letters;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using MimeKit;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.UnitTests.Application.Letters;

/// <summary>What letters change elsewhere: the stage gate, deletions refused, privacy, the timeline and the transport.</summary>
[TestFixture]
public sealed class LettersAcrossTheAppTests : PathwayHandlerTestBase
{
    private static Letter LetterOn(PatientPathway pathway, LetterStatus status, LetterKind kind = LetterKind.DischargeSummary) =>
        LettersTestConstants.CreateLetter(pathway, PracticesTestConstants.CreatePractice(), status, kind: kind);

    [Test]
    public async Task FinishStage_ThatNeedsALetter_IsRefusedUntilItsSentToEveryone()
    {
        var pathway = GivenPathway(CreatePathway(Status.Assigned));
        pathway.StageAt(1).NeedsLetterKind = LetterKind.DischargeSummary;
        var letter = LetterOn(pathway, LetterStatus.Approved);
        Letters.Setup(l => l.GetForPathwayAsync(pathway.Id, It.IsAny<CancellationToken>())).ReturnsAsync([letter]);
        var handler = Fixture.Create<FinishPathwayStageHandler>();

        var act = () => handler.Handle(new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Staff), CancellationToken.None);
        await act.Should().ThrowAsync<ConflictException>().WithMessage("*discharge summary*not yet sent*");

        letter.Recipients.ForEach(r => r.DispatchStatus = LetterDispatchStatus.Sent);
        var response = await handler.Handle(new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Staff), CancellationToken.None);
        response.Stages[0].State.Should().Be("Completed");
        response.Stages[0].NeedsLetterKind.Should().Be("DischargeSummary");
    }

    [Test]
    public async Task DeletePathway_WithLetters_IsAConflict()
    {
        var pathway = GivenPathway(CreatePathway(Status.Cancelled));
        Letters.Setup(l => l.CountForPathwayAsync(pathway.Id, It.IsAny<CancellationToken>())).ReturnsAsync(2);

        var act = () => Fixture.Create<DeletePatientPathwayHandler>().Handle(new DeletePatientPathwayCommand(pathway.Id, Admin), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*2 letters*");
    }

    [Test]
    public async Task DeleteAttachment_ALettersPdf_IsAConflictEvenForAnAdmin()
    {
        var attachment = AttachmentsTestConstants.CreateAttachment(uploadedBy: Admin.UserId);
        var attachments = new Mock<MedApp.Application.Attachments.Repositories.IAttachmentRepository>();
        attachments.Setup(a => a.GetForUpdateAsync(attachment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(attachment);
        Letters.Setup(l => l.IsPdfAsync(attachment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        Fixture.Inject(attachments.Object);

        var act = () => Fixture.Create<DeleteAttachmentHandler>().Handle(
            new DeleteAttachmentCommand(attachment.Id, Admin, CanCorrectRecords: true), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(DeleteAttachmentHandler.LetterPdfMessage);
        attachment.IsDeleted.Should().BeFalse();
    }

    [Test]
    public async Task DeleteTemplate_Unused_DeletesIt_AndOneWithLetters_IsAConflict()
    {
        var template = LettersTestConstants.CreateTemplate();
        var templates = new Mock<ILetterTemplateRepository>();
        templates.Setup(t => t.GetAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        var handler = new DeleteLetterTemplateHandler(templates.Object);
        using var cts = new CancellationTokenSource();

        await handler.Handle(new DeleteLetterTemplateCommand(template.Id), cts.Token);
        templates.Verify(t => t.DeleteAsync(template.Id, cts.Token));

        templates.Setup(t => t.IsInUseAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        await FluentActions.Awaiting(() => handler.Handle(new DeleteLetterTemplateCommand(template.Id), cts.Token))
            .Should().ThrowAsync<ConflictException>().WithMessage("*Archive it instead*");
        await FluentActions.Awaiting(() => handler.Handle(new DeleteLetterTemplateCommand(Guid.NewGuid()), cts.Token))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public void StageDefinition_GivesItsLetterKind()
    {
        var definition = CreateDefinition("Discharge letter to GP");
        definition.NeedsLetterKind = LetterKind.DischargeSummary;

        StageDefinitionMapper.ToResponse(definition, StageDefinitionUsage.None).NeedsLetterKind.Should().Be("DischargeSummary");
        definition.NeedsLetterKind = null;
        StageDefinitionMapper.ToResponse(definition, StageDefinitionUsage.None).NeedsLetterKind.Should().BeNull();
    }

    [Test]
    public void PathwayCreation_CopiesTheStagesLetterKind()
    {
        var definition = CreateDefinition("Discharge letter to GP");
        definition.NeedsLetterKind = LetterKind.DischargeSummary;
        var template = CreateTemplate(TemplateName, false, definition);

        var pathway = PathwayCreation.FromTemplate(template, Guid.NewGuid(), null, Today.AddDays(7), PatientPathwayPriority.Medium, Now);

        pathway.Stages.Single().NeedsLetterKind.Should().Be(LetterKind.DischargeSummary);
    }

    [Test]
    public void Anonymiser_ReplacesLettersText_AndPeoplesNamesAndAddresses_KeepingTheDirectorysPractice()
    {
        var pathway = CreatePathway(Status.Completed, finishedStages: 3);
        var letter = LetterOn(pathway, LetterStatus.Withdrawn);
        letter.ReturnComment = "comment";
        letter.WithdrawnReason = "reason";
        var record = PrivacyTestConstants.CreateRecord(null, pathway) with { Letters = [letter] };

        PatientAnonymiser.Apply(record, Now, new ActivityActor(Admin.UserId, Admin.Username, null));

        letter.Should().BeEquivalentTo(new
        {
            Body = PatientAnonymiser.RemovedText, Closing = PatientAnonymiser.RemovedText,
            ReturnComment = PatientAnonymiser.RemovedText, WithdrawnReason = PatientAnonymiser.RemovedText,
            Letterhead = LettersTestConstants.Letterhead, Kind = LetterKind.DischargeSummary,
        });
        letter.Recipients[0].Name.Should().Be(PracticesTestConstants.PracticeName);
        letter.Recipients[0].AddressLines.Should().NotBeNull();
        letter.Recipients[1].Should().BeEquivalentTo(new { Name = PatientAnonymiser.RemovedText, AddressLines = (string?)null, Postcode = (string?)null });
    }

    [TestCase(LetterStatus.Draft)]
    [TestCase(LetterStatus.AwaitingApproval)]
    [TestCase(LetterStatus.Approved)]
    public async Task Anonymise_WithALetterNotSentToEveryone_IsRefused(LetterStatus status)
    {
        var pathway = CreatePathway(Status.Completed, finishedStages: 3);
        var record = PrivacyTestConstants.CreateRecord(null, pathway) with { Letters = [LetterOn(pathway, status)] };
        var repository = new Mock<IPatientRecordRepository>();
        repository.Setup(r => r.GetAsync(record.Patient.Id, true, It.IsAny<CancellationToken>())).ReturnsAsync(record);
        Fixture.Inject(repository.Object);
        Fixture.Inject<IActivityLog>(new RecordingActivityLog());

        var act = () => Fixture.Create<AnonymisePatientHandler>().Handle(
            new AnonymisePatientCommand(record.Patient.Id, "retentionPeriodEnded", null, "x"), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*1 letter not yet sent to everyone*");
    }

    [Test]
    public void Export_GivesLettersWithTheirTextAndRecipients_StaffByUsername()
    {
        var pathway = CreatePathway(Status.Completed, finishedStages: 3);
        var letter = LetterOn(pathway, LetterStatus.Approved);
        letter.Recipients[0].DispatchStatus = LetterDispatchStatus.Sent;
        letter.Recipients[0].Method = LetterDispatchMethod.Post;
        letter.Recipients[0].SentByUserId = LettersTestConstants.Sender.UserId;
        var record = PrivacyTestConstants.CreateRecord(null, pathway) with { Letters = [letter] };

        PatientExportMapper.UserIds(record).Should().Contain([LettersTestConstants.Author.UserId, LettersTestConstants.Approver.UserId]);
        var export = PatientExportMapper.ToResponse(record, LettersTestConstants.Usernames, Now, "exporter");

        var exported = export.Letters.Single();
        exported.Should().BeEquivalentTo(new
        {
            letter.Id, Kind = "DischargeSummary", Status = "Approved", Body = LettersTestConstants.LetterBody,
            Author = LettersTestConstants.Author.Username, Approver = LettersTestConstants.Approver.Username,
        });
        exported.Recipients[0].Should().BeEquivalentTo(new { Kind = "GpPractice", DispatchStatus = "Sent", Method = "Post", SentBy = LettersTestConstants.Sender.Username });
        JsonSerializer.Serialize(exported).Should().NotContain(LettersTestConstants.Author.UserId.ToString());
    }

    [Test]
    public void Timeline_ALetterEntry_GivesStaffItsKindAndMethod_AndAdminsTheReason()
    {
        var pathway = CreatePathway(Status.InProgress);
        var letter = LetterOn(pathway, LetterStatus.Approved);
        letter.WithdrawnReason = "Synthetic reason";
        var change = new LetterChange(letter, pathway, Staff, Now);
        change.Send(letter.Recipients[1], LetterDispatchMethod.HandedToPatient, LetterDispatchStatus.Sent, Today);
        change.Withdraw("Synthetic reason");

        var sent = TimelineMapper.ToResponse(new PatientTimelineRow(change.AuditEntries[0], pathway.Title), isAdmin: false);
        var withdrawnForStaff = TimelineMapper.ToResponse(new PatientTimelineRow(change.AuditEntries[1], pathway.Title), isAdmin: false);
        var withdrawnForAdmin = TimelineMapper.ToResponse(new PatientTimelineRow(change.AuditEntries[1], pathway.Title) { Letter = letter }, isAdmin: true);

        sent.Should().BeEquivalentTo(new
        {
            Kind = "Letters", Action = "LetterSent", LetterId = (Guid?)letter.Id, LetterKind = "DischargeSummary",
            LetterTemplateName = LettersTestConstants.TemplateName, RecipientKind = "Patient", DispatchMethod = "HandedToPatient", DispatchStatus = "Sent",
        });
        withdrawnForStaff.Reason.Should().BeNull();
        withdrawnForAdmin.Reason.Should().Be("Synthetic reason");
        PatientTimelineKinds.Of(change.AuditEntries[0]).Should().Be(PatientTimelineKind.Letters);
        PatientTimelineKinds.WhereKindIn(change.AuditEntries.AsQueryable(), new HashSet<PatientTimelineKind> { PatientTimelineKind.Letters })
            .Should().HaveCount(2);
        TimelineMapper.GuidIn("not json", "letterId").Should().BeNull();
    }

    [Test]
    public void Transport_BuildsAMessageFromTheCentre_WithThePdfAttached()
    {
        var settings = new MedApp.Application.Practices.Common.LetterSecureEmailOptions
        {
            Host = "relay.invalid", FromAddress = "centre@nhs.net", FromName = "Synthetic Specialist Centre", RelayMeetsDcb1596 = true,
        };
        var email = SecureLetterEmails.For("practice@nhs.net", settings.CentreName, "%PDF"u8.ToArray());

        var message = SecureEmailLetterTransport.BuildMessage(email, settings);

        message.From.Mailboxes.Single().Address.Should().Be("centre@nhs.net");
        message.To.Mailboxes.Single().Address.Should().Be("practice@nhs.net");
        message.Subject.Should().Be("A letter from Synthetic Specialist Centre");
        message.MessageId.Should().NotBeNullOrEmpty();
        message.Attachments.OfType<MimePart>().Single().FileName.Should().Be("letter.pdf");
    }

    [Test]
    public async Task Transport_WhenItIsntSetUp_IsUnavailable_AndRefusesToSend()
    {
        var transport = new SecureEmailLetterTransport(Microsoft.Extensions.Options.Options.Create(new MedApp.Application.Practices.Common.LettersOptions()));

        transport.IsAvailable.Should().BeFalse();
        await FluentActions.Awaiting(() => transport.SendAsync(SecureLetterEmails.For("a@nhs.net", "x", []), CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async Task Transport_ARelayThatCantBeReached_IsALetterTransportFailure()
    {
        var transport = new SecureEmailLetterTransport(Microsoft.Extensions.Options.Options.Create(new MedApp.Application.Practices.Common.LettersOptions
        {
            SecureEmail = new() { Host = "127.0.0.1", Port = 1, Security = "None", FromAddress = "c@nhs.net", RelayMeetsDcb1596 = true, TimeoutSeconds = 2 },
        }));

        await FluentActions.Awaiting(() => transport.SendAsync(SecureLetterEmails.For("a@nhs.net", "x", "%PDF"u8.ToArray()), CancellationToken.None))
            .Should().ThrowAsync<LetterTransportException>().Where(e => e.Category == "connection");
    }
}
