using FluentAssertions;
using MedApp.Application.Letters.Commands.CreateLetter;
using MedApp.Application.Letters.Commands.ReturnLetter;
using MedApp.Application.Letters.Commands.SendLetter;
using MedApp.Application.Letters.Commands.SubmitLetter;
using MedApp.Application.Letters.Commands.UpdateLetter;
using MedApp.Application.Letters.Commands.WithdrawLetter;
using MedApp.Application.Letters.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Practices.Common;
using MedApp.Domain.Letters;
using MedApp.Domain.Notifications;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.Extensions.Options;
using static MedApp.UnitTests.Common.Constants.LettersTestConstants;

namespace MedApp.UnitTests.Application.Letters.Common;

[TestFixture]
public sealed class LetterRulesTests : LetterHandlerTestBase
{
    // The workflow's flags.

    [Test]
    public void CanApprove_TheNamedApprover_OrTheAuthorOfTheirOwnDraftIfTheyMay()
    {
        var awaiting = OnPathway(LetterStatus.AwaitingApproval);
        LetterWorkflow.CanApprove(awaiting, Approver).Should().BeTrue();
        LetterWorkflow.CanApprove(awaiting, ClinicianAuthor).Should().BeFalse();
        LetterWorkflow.CanReturn(awaiting, Approver).Should().BeTrue();
        LetterWorkflow.CanReturn(awaiting, Author).Should().BeFalse();

        LetterWorkflow.CanApprove(OnPathway(author: ClinicianAuthor), ClinicianAuthor).Should().BeTrue();
        LetterWorkflow.CanApprove(OnPathway(), Author).Should().BeFalse("they can't approve letters");
        LetterWorkflow.CanApprove(OnPathway(LetterStatus.Approved), Approver).Should().BeFalse();
    }

    [Test]
    public void CanWithdraw_TheApproverOrSomeoneWhoCorrectsRecords_OnceApproved()
    {
        var approved = OnPathway(LetterStatus.Approved);
        LetterWorkflow.CanWithdraw(approved, Approver).Should().BeTrue();
        LetterWorkflow.CanWithdraw(approved, AdminActor).Should().BeTrue();
        LetterWorkflow.CanWithdraw(approved, Author).Should().BeFalse();
        LetterWorkflow.CanWithdraw(OnPathway(), AdminActor).Should().BeFalse();
    }

    [Test]
    public void CanPreview_DraftsOnly_ForTheAuthorApproverOrAnApprover()
    {
        LetterWorkflow.CanPreview(OnPathway(), Author).Should().BeTrue();
        LetterWorkflow.CanPreview(OnPathway(), ClinicianAuthor).Should().BeTrue();
        LetterWorkflow.CanPreview(OnPathway(), Sender).Should().BeFalse();
        LetterWorkflow.CanPreview(OnPathway(LetterStatus.Approved), Author).Should().BeFalse();
    }

    [Test]
    public void CanBeCorrected_OnlyAWithdrawnApprovedLetter_Once()
    {
        var withdrawn = OnPathway(LetterStatus.Approved);
        withdrawn.Status = LetterStatus.Withdrawn;
        LetterWorkflow.CanBeCorrected(withdrawn, hasCorrection: false).Should().BeTrue();
        LetterWorkflow.CanBeCorrected(withdrawn, hasCorrection: true).Should().BeFalse();
        LetterWorkflow.CanBeCorrected(OnPathway(LetterStatus.Withdrawn), hasCorrection: false).Should().BeFalse("a discarded draft was never approved");
    }

    [Test]
    public void WhyNoSecureEmail_SaysWhy()
    {
        var practice = OnPathway(LetterStatus.Approved).Recipients[0];
        var patient = OnPathway(LetterStatus.Approved).Recipients[1];
        var domains = new LettersOptions().AllowedSecureEmailDomains;

        LetterWorkflow.WhyNoSecureEmail(practice, true, domains).Should().BeNull();
        LetterWorkflow.WhyNoSecureEmail(practice, false, domains).Should().Be(LetterWorkflow.SecureEmailOffMessage);
        LetterWorkflow.WhyNoSecureEmail(patient, true, domains).Should().Be(LetterWorkflow.PatientEmailMessage);
        practice.Email = null;
        LetterWorkflow.WhyNoSecureEmail(practice, true, domains).Should().Be(LetterWorkflow.NoSecureEmailAddressMessage);
        practice.Email = "a@example.com";
        LetterWorkflow.WhyNoSecureEmail(practice, true, domains).Should().Contain("nhs.net");
    }

    [Test]
    public void Describe_AndKindInMessage_PutThemInWords()
    {
        LetterWorkflow.Describe(LetterStatus.AwaitingApproval).Should().Be("awaiting approval");
        LetterWorkflow.Describe(LetterStatus.Draft).Should().Be("a draft");
        LetterWorkflow.Describe(LetterStatus.Withdrawn).Should().Be("withdrawn");
        LetterWorkflow.KindInMessage(LetterKind.DischargeSummary).Should().Be("discharge summary");
        LetterWorkflow.KindInMessage(LetterKind.InformationRequest).Should().Be("request for more information");
    }

    // The audit.

    [Test]
    public void Change_EachStep_AuditsIdsKindsAndMethods_NeverText()
    {
        var letter = OnPathway();
        letter.Body = "secret clinical words";
        var change = new LetterChange(letter, Pathway, Author, NowUtc);

        change.Draft();
        change.Submit(Approver.UserId, Approver.Username);
        change.Return("secret comment");
        change.Submit(Approver.UserId, Approver.Username);
        change.Approve(Guid.NewGuid());
        change.Send(letter.Recipients[0], LetterDispatchMethod.Post, LetterDispatchStatus.Sent, new DateOnly(2026, 11, 18));
        change.Withdraw("secret reason");

        change.AuditEntries.Select(e => e.Action).Should().Equal(
            PatientPathwayAuditAction.LetterDrafted, PatientPathwayAuditAction.LetterSubmitted, PatientPathwayAuditAction.LetterReturned,
            PatientPathwayAuditAction.LetterSubmitted, PatientPathwayAuditAction.LetterApproved, PatientPathwayAuditAction.LetterSent,
            PatientPathwayAuditAction.LetterWithdrawn);
        change.AuditEntries.Should().AllSatisfy(e =>
        {
            e.Details.Should().NotContain("secret").And.NotContain(Practice.Name).And.Contain(letter.Id.ToString());
            e.PatientPathwayId.Should().Be(Pathway.Id);
            e.PathwayTitle.Should().Be(Pathway.Title);
            e.Username.Should().Be(Author.Username);
        });
        change.AuditEntries.Select(e => e.AtUtc).Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
        letter.Version.Should().Be(7);
        letter.LastUpdated.Should().Be(NowUtc);
    }

    // The notifications.

    [Test]
    public void Notifications_FixedTitles_UsernamesAndTheKindOnly()
    {
        var letter = OnPathway(LetterStatus.AwaitingApproval, kind: LetterKind.AppointmentLetter);

        var awaiting = LetterNotifications.AwaitingApproval(letter, Author, NowUtc);
        awaiting.Should().BeEquivalentTo(new
        {
            UserId = Approver.UserId, Kind = NotificationKind.LetterAwaitingApproval, Title = "Letter to approve",
            Message = "a.writer asked you to approve an appointment letter.", SubjectId = (Guid?)letter.Id, PathwayId = (Guid?)Pathway.Id,
        });
        LetterNotifications.Approved(letter, Approver, NowUtc)!.Message.Should().Be("b.approver approved your appointment letter.");
        LetterNotifications.Approved(letter, Author, NowUtc).Should().BeNull("they approved it themselves");
        LetterNotifications.Returned(letter, Approver, NowUtc).Should().BeEquivalentTo(new
        {
            UserId = Author.UserId, Title = "Letter returned", Message = "b.approver returned your appointment letter with a comment.",
        });
        LetterNotifications.AwaitingApproval(OnPathway(LetterStatus.AwaitingApproval), Author, NowUtc).Message.Should().Contain("a clinic letter");
        Notification.HasFixedTitle(NotificationKind.LetterReturned).Should().BeTrue();
    }

    // The document.

    [Test]
    public void Rendering_AddressesTheFirstRecipient_ListsTheCopies_AndNamesWhatsMissing()
    {
        var letter = OnPathway();
        letter.Closing = "Yours,\n{{approver.name}}";
        var values = new Dictionary<string, string?>
        {
            [LetterMergeFields.PatientName] = "Sam Example",
            [LetterMergeFields.PatientDateOfBirth] = "1 January 1970",
            [LetterMergeFields.PatientNhsNumber] = "999 000 0018",
        };

        var filled = LetterRendering.Fill(letter, values, LetterRendering.DraftMarking);

        filled.Document.RecipientAddress.Should().Equal(Practice.Name, PracticesTestConstants.AddressLine1, PracticesTestConstants.AddressLine2, PracticesTestConstants.Postcode);
        filled.Document.Closing.Should().Equal("Yours,", "", "Copies to:", $"{Patient.FirstName} {Patient.LastName}");
        filled.Document.Body.Should().Contain("Sam Example");
        filled.Document.PatientIdentifiers.Should().Be("Sam Example · Born 1 January 1970 · NHS number 999 000 0018");
        filled.Document.Title.Should().Be("Clinic letter");
        filled.Document.Marking.Should().Be(LetterRendering.DraftMarking);
        filled.Missing.Should().Equal(LetterMergeFields.ApproverName);
        LetterRendering.MissingMessage(filled.Missing).Should().Contain("the approver's name").And.Contain("has no value");
        LetterRendering.MissingMessage(["practice.name", "patient.address"]).Should().Contain("the GP practice's name, the patient's address").And.Contain("have");
        LetterRendering.AllText(filled.Document).Should().Contain(Practice.Name).And.Contain("Copies to:");
        LetterRendering.FileName(letter, new DateOnly(2026, 11, 18)).Should().Be("Clinic letter 2026-11-18.pdf");
    }

    // The stage gate.

    [Test]
    public void StageGate_NeedsALetterOfItsKind_ApprovedAndSentToEveryone()
    {
        var stage = Pathway.StageAt(3);
        StageGates.WhyLetterMissing(stage, []).Should().BeNull("the stage needs no letter");

        stage.NeedsLetterKind = LetterKind.DischargeSummary;
        StageGates.WhyLetterMissing(stage, []).Should().Contain("a discharge summary").And.Contain("None has been written");

        var draft = OnPathway(kind: LetterKind.DischargeSummary);
        StageGates.WhyLetterMissing(stage, [draft]).Should().Contain("still a draft");
        var awaiting = OnPathway(LetterStatus.AwaitingApproval, kind: LetterKind.DischargeSummary);
        StageGates.WhyLetterMissing(stage, [awaiting]).Should().Contain("awaiting approval");
        var approved = OnPathway(LetterStatus.Approved, kind: LetterKind.DischargeSummary);
        StageGates.WhyLetterMissing(stage, [approved]).Should().Contain("not yet sent to every recipient");
        StageGates.WhyLetterMissing(stage, [OnPathway(LetterStatus.Approved)]).Should().Contain("None has been written", "a clinic letter isn't one");

        approved.Recipients.ForEach(r => r.DispatchStatus = LetterDispatchStatus.Sent);
        StageGates.WhyLetterMissing(stage, [draft, approved]).Should().BeNull();
        approved.Status = LetterStatus.Withdrawn;
        StageGates.WhyLetterMissing(stage, [approved]).Should().NotBeNull("a withdrawn letter doesn't count");

        stage.NeedsLetterKind = LetterKind.AppointmentLetter;
        StageGates.WhyLetterMissing(stage, []).Should().Contain("an appointment letter");
    }

    [TestCase(null, true, null)]
    [TestCase("None", true, null)]
    [TestCase("DischargeSummary", true, LetterKind.DischargeSummary)]
    [TestCase("Everything", false, null)]
    public void StageLetterKinds_ParseNamesOrNone(string? value, bool valid, LetterKind? kind)
    {
        StageLetterKinds.IsValid(value).Should().Be(valid);
        if (valid)
            StageLetterKinds.Parse(value).Should().Be(kind);
    }

    // Validators.

    [Test]
    public void CreateValidator_ExactlyOnePathwayOrReferral()
    {
        var validator = new CreateLetterValidator();
        validator.Validate(new CreateLetterCommand(Guid.NewGuid(), Guid.NewGuid(), null, Author)).IsValid.Should().BeTrue();
        validator.Validate(new CreateLetterCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Author)).IsValid.Should().BeFalse();
        validator.Validate(new CreateLetterCommand(Guid.Empty, null, null, Author)).Errors.Should().HaveCount(2);
    }

    [Test]
    public void UpdateValidator_ChecksTextRecipientsAndVersion()
    {
        var renderer = RendererMissing("ç").Object;
        var validator = new UpdateLetterValidator(renderer, Microsoft.Extensions.Options.Options.Create(new LettersOptions()));
        UpdateLetterCommand Command(params LetterRecipientCommandItem[] recipients) =>
            new(Guid.NewGuid(), "Head", "Body {{patient.name}}", "Close", false, recipients, 1, Author);

        validator.Validate(Command(new LetterRecipientCommandItem("Patient", null, null, null, null, null))).IsValid.Should().BeTrue();

        validator.Validate(Command() with { Body = new string('x', Letter.MaxBodyLength + 1) }).Errors.Should().ContainSingle(e => e.PropertyName == "Body");
        validator.Validate(Command() with { Body = "{{patient.nme}}" }).Errors.Should().ContainSingle(e => e.ErrorMessage.Contains("isn't a merge field"));
        validator.Validate(Command() with { Closing = "Façade" }).Errors.Should().ContainSingle(e => e.ErrorMessage.Contains("can't show"));
        validator.Validate(Command() with { ExpectedVersion = 0 }).IsValid.Should().BeFalse();

        validator.Validate(Command(new LetterRecipientCommandItem("Nobody", null, null, null, null, null))).IsValid.Should().BeFalse();
        validator.Validate(Command(new LetterRecipientCommandItem("GpPractice", null, null, null, null, null)))
            .Errors.Should().ContainSingle(e => e.ErrorMessage.Contains("directory"));
        validator.Validate(Command(new LetterRecipientCommandItem("Other", null, null, null, null, null))).Errors.Should().HaveCount(2);
        validator.Validate(Command(new LetterRecipientCommandItem("Other", null, "A Person", "1\n2\n3\n4", "nope", "x@example.com")))
            .Errors.Select(e => e.ErrorMessage).Should().Contain(m => m.Contains("at most 3 lines"))
            .And.Contain(m => m.Contains("UK postcode")).And.Contain(m => m.Contains("nhs.net"));
        validator.Validate(Command(new LetterRecipientCommandItem("Other", null, "Françoise", "1 Street", null, null)))
            .Errors.Should().ContainSingle(e => e.ErrorMessage.Contains("can't show ç"));
        validator.Validate(Command(Enumerable.Range(0, Letter.MaxRecipients + 1)
                .Select(_ => new LetterRecipientCommandItem("Other", null, "A", "1 Street", null, null)).ToArray()))
            .Errors.Should().Contain(e => e.ErrorMessage.Contains("at most 10 recipients"));
        validator.Validate(Command(
                new LetterRecipientCommandItem("Patient", null, null, null, null, null),
                new LetterRecipientCommandItem("Patient", null, null, null, null, null)))
            .Errors.Should().Contain(e => e.ErrorMessage.Contains("only once"));
    }

    [Test]
    public void OtherValidators_CheckTheirFields()
    {
        new SubmitLetterValidator().Validate(new SubmitLetterCommand(Guid.NewGuid(), Guid.Empty, 1, Author)).IsValid.Should().BeFalse();
        new ReturnLetterValidator().Validate(new ReturnLetterCommand(Guid.NewGuid(), " ", 1, Approver)).IsValid.Should().BeFalse();
        new ReturnLetterValidator().Validate(new ReturnLetterCommand(Guid.NewGuid(), new string('x', 501), 1, Approver)).IsValid.Should().BeFalse();
        new WithdrawLetterValidator().Validate(new WithdrawLetterCommand(Guid.NewGuid(), "Wrong", 1, Approver)).IsValid.Should().BeTrue();
        new DiscardLetterValidator().Validate(new DiscardLetterCommand(Guid.NewGuid(), 0, Author)).IsValid.Should().BeFalse();
        new SendLetterValidator().Validate(new SendLetterCommand(Guid.NewGuid(), Guid.Empty, 1, Sender)).IsValid.Should().BeFalse();
        var recordSent = new RecordLetterSentValidator();
        recordSent.Validate(new RecordLetterSentCommand(Guid.NewGuid(), Guid.NewGuid(), "Post", new DateOnly(2026, 11, 18), 1, Sender)).IsValid.Should().BeTrue();
        recordSent.Validate(new RecordLetterSentCommand(Guid.NewGuid(), Guid.NewGuid(), "SecureEmail", new DateOnly(2026, 11, 18), 1, Sender)).IsValid.Should().BeFalse();
        recordSent.Validate(new RecordLetterSentCommand(Guid.NewGuid(), Guid.NewGuid(), "Pigeon", new DateOnly(2026, 11, 18), 1, Sender)).IsValid.Should().BeFalse();
    }

    // Secure email's words.

    [Test]
    public void SecureLetterEmails_HoldNoPatientOrClinicalText()
    {
        var email = SecureLetterEmails.For("practice@nhs.net", "Synthetic Specialist Centre", Pdf);

        email.Subject.Should().Be("A letter from Synthetic Specialist Centre");
        email.TextBody.Should().StartWith("A letter from Synthetic Specialist Centre. It's attached.");
        email.FileName.Should().Be("letter.pdf");
        new LetterSecureEmailOptions().CentreName.Should().Be("the clinic");
        new LetterSecureEmailOptions { Host = "h", FromAddress = "f@nhs.net" }.IsAvailable.Should().BeFalse("the relay isn't declared");
        new LetterSecureEmailOptions { Host = "h", FromAddress = "f@nhs.net", RelayMeetsDcb1596 = true }.IsAvailable.Should().BeTrue();
        new LetterTransportException("connection").Message.Should().NotContain("@");
    }
}
