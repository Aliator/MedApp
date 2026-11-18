using AutoFixture;
using FluentAssertions;
using FluentValidation;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Letters.Commands.ApproveLetter;
using MedApp.Application.Letters.Commands.CreateLetter;
using MedApp.Application.Letters.Commands.ReturnLetter;
using MedApp.Application.Letters.Commands.SendLetter;
using MedApp.Application.Letters.Commands.SubmitLetter;
using MedApp.Application.Letters.Commands.UpdateLetter;
using MedApp.Application.Letters.Commands.WithdrawLetter;
using MedApp.Application.Letters.Commands.WriteLetterCorrection;
using MedApp.Application.Letters.Common;
using MedApp.Application.Letters.Repositories;
using MedApp.Domain.Attachments;
using MedApp.Domain.Letters;
using MedApp.Domain.Notifications;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Referrals;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.LettersTestConstants;

namespace MedApp.UnitTests.Application.Letters.Commands;

[TestFixture]
public sealed class LetterHandlersTests : LetterHandlerTestBase
{
    private readonly CancellationTokenSource _cts = new();

    [OneTimeTearDown]
    public void TearDown() => _cts.Dispose();

    private Mock<ILetterTemplateRepository> GivenTemplate(LetterTemplate template)
    {
        var templates = new Mock<ILetterTemplateRepository>();
        templates.Setup(t => t.GetAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        Fixture.Inject(templates.Object);
        return templates;
    }

    // Starting a letter.

    [Test]
    public async Task Create_FromAPathway_CopiesTheTemplate_StartsWithThePracticeAndPatient_AndAuditsTheDraft()
    {
        var template = CreateTemplate(teamId: Pathway.TeamId, kind: LetterKind.DischargeSummary);
        GivenTemplate(template);

        var response = await Fixture.Create<CreateLetterHandler>().Handle(
            new CreateLetterCommand(template.Id, Pathway.Id, null, Author), _cts.Token);

        var letter = Added.Single();
        letter.Should().BeEquivalentTo(new
        {
            PatientId = Patient.Id, PathwayId = (Guid?)Pathway.Id, TemplateId = (Guid?)template.Id, TemplateName = template.Name,
            Kind = LetterKind.DischargeSummary, Letterhead, Body = BodySkeleton, Closing, Status = LetterStatus.Draft,
            AuthorUserId = Author.UserId, Version = 1,
        });
        letter.Recipients.Select(r => (r.Kind, r.Name)).Should().Equal(
            (LetterRecipientKind.GpPractice, Practice.Name), (LetterRecipientKind.Patient, $"{Patient.FirstName} {Patient.LastName}"));
        letter.Recipients[0].Email.Should().Be(PracticesTestConstants.SecureEmail);
        SavedAudit.Should().ContainSingle(e => e.Action == PatientPathwayAuditAction.LetterDrafted && e.PatientPathwayId == Pathway.Id);
        Letters.Verify(l => l.AddAsync(letter, It.IsAny<IEnumerable<PatientPathwayAuditEntry>>(), _cts.Token));
        response.CanEdit.Should().BeTrue();
        response.Status.Should().Be("Draft");
    }

    [Test]
    public async Task Create_ForAPatientWithAnUnlistedPractice_StartsWithIt_AndItsMergeFieldsUseIt()
    {
        Patient.PracticeId = null;
        Patient.UnlistedPracticeName = PracticesTestConstants.UnlistedName;
        Patient.UnlistedPracticeAddressLines = PracticesTestConstants.UnlistedLine;
        Patient.UnlistedPracticePostcode = PracticesTestConstants.UnlistedPostcode;
        Patient.UnlistedPracticePhone = PracticesTestConstants.UnlistedPhone;
        var template = CreateTemplate(teamId: Pathway.TeamId);
        GivenTemplate(template);

        await Fixture.Create<CreateLetterHandler>().Handle(new CreateLetterCommand(template.Id, Pathway.Id, null, Author), _cts.Token);

        var letter = Added.Single();
        letter.Recipients[0].Should().BeEquivalentTo(new
        {
            Kind = LetterRecipientKind.GpPractice, PracticeId = (Guid?)null, Name = PracticesTestConstants.UnlistedName,
            AddressLines = PracticesTestConstants.UnlistedLine, Postcode = PracticesTestConstants.UnlistedPostcode, Email = (string?)null,
        });

        var records = Fixture.Create<LetterRecords>();
        var record = new LetterRecord(letter, Patient, Pathway, null);
        var values = await records.MergeValuesAsync(record, null, new DateOnly(2026, 11, 18), _cts.Token);
        values[LetterMergeFields.PracticeName].Should().Be(PracticesTestConstants.UnlistedName);
        values[LetterMergeFields.PracticeAddress].Should().Be($"{PracticesTestConstants.UnlistedLine}\n{PracticesTestConstants.UnlistedPostcode}");
        (await records.RefreshRecipientsAsync(record, _cts.Token)).Should().BeNull();

        // Once the patient's unlisted practice is gone, the letter can't be approved to it.
        Patient.UnlistedPracticeName = null;
        (await records.RefreshRecipientsAsync(record, _cts.Token)).Should().Contain("outside the directory is no longer recorded");
    }

    [Test]
    public async Task Recipients_ThePatientsUnlistedPractice_NeedsThePatientToHaveOne()
    {
        var records = Fixture.Create<LetterRecords>();
        var record = new LetterRecord(OnPathway(), Patient, Pathway, null);

        var act = () => records.RecipientsAsync(
            record, [new LetterRecipientInput(LetterRecipientKind.GpPractice, null, null, null, null, null)], _cts.Token);

        (await act.Should().ThrowAsync<ValidationException>())
            .Which.Errors.Should().ContainSingle(e => e.ErrorMessage == LetterRecords.NoUnlistedPracticeMessage);
    }

    [Test]
    public async Task Create_ArchivedTemplate_IsAConflict()
    {
        var template = CreateTemplate(isArchived: true);
        GivenTemplate(template);

        var act = () => Fixture.Create<CreateLetterHandler>().Handle(new CreateLetterCommand(template.Id, Pathway.Id, null, Author), _cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*archived*");
    }

    [Test]
    public async Task Create_AnotherTeamsTemplate_IsRefused()
    {
        var template = CreateTemplate(teamId: Guid.NewGuid());
        GivenTemplate(template);

        var act = () => Fixture.Create<CreateLetterHandler>().Handle(new CreateLetterCommand(template.Id, Pathway.Id, null, Author), _cts.Token);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*another team*");
    }

    [Test]
    public async Task Create_RestrictedRecord_IsAConflictAskingAnAdmin()
    {
        Patient.RestrictedAtUtc = NowUtc;
        var template = CreateTemplate();
        GivenTemplate(template);

        var act = () => Fixture.Create<CreateLetterHandler>().Handle(new CreateLetterCommand(template.Id, Pathway.Id, null, Author), _cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(LetterWorkflow.RestrictedMessage);
        Added.Should().BeEmpty();
    }

    [Test]
    public async Task Create_FromAReferral_OnlyRepliesAndRequests_AddressedToTheReferrer()
    {
        var referral = ReferralsTestConstants.CreateReferral();
        referral.Patient = Patient;
        Referrals.Setup(r => r.GetByIdAsync(referral.Id, It.IsAny<CancellationToken>())).ReturnsAsync(referral);
        var clinic = CreateTemplate();
        var request = CreateTemplate(OtherTemplateName, kind: LetterKind.InformationRequest);
        var templates = GivenTemplate(clinic);
        templates.Setup(t => t.GetAsync(request.Id, It.IsAny<CancellationToken>())).ReturnsAsync(request);

        var refused = () => Fixture.Create<CreateLetterHandler>().Handle(new CreateLetterCommand(clinic.Id, null, referral.Id, Author), _cts.Token);
        await refused.Should().ThrowAsync<ValidationException>().WithMessage("*referral response or a request*");

        await Fixture.Create<CreateLetterHandler>().Handle(new CreateLetterCommand(request.Id, null, referral.Id, Author), _cts.Token);

        var letter = Added.Single();
        letter.ReferralId.Should().Be(referral.Id);
        letter.PathwayId.Should().BeNull();
        letter.Recipients.Should().ContainSingle().Which.Kind.Should().Be(LetterRecipientKind.Referrer);
        SavedAudit.Single().PatientPathwayId.Should().Be(PatientPathwayAuditEntry.NoPathway);
        SavedAudit.Single().Details.Should().Contain(referral.Id.ToString());
    }

    [Test]
    public async Task Create_ReferralEnteredInError_IsAConflict()
    {
        var referral = ReferralsTestConstants.CreateReferral();
        referral.Status = ReferralStatus.EnteredInError;
        Referrals.Setup(r => r.GetByIdAsync(referral.Id, It.IsAny<CancellationToken>())).ReturnsAsync(referral);
        var template = CreateTemplate(kind: LetterKind.ReferralResponse);
        GivenTemplate(template);

        var act = () => Fixture.Create<CreateLetterHandler>().Handle(new CreateLetterCommand(template.Id, null, referral.Id, Author), _cts.Token);

        await act.Should().ThrowAsync<ConflictException>();
    }

    // Editing.

    [Test]
    public async Task Update_TheAuthor_ReplacesTextAndRecipients_WithANewVersionButNoAuditEntry()
    {
        var letter = Given(OnPathway());

        var response = await Fixture.Create<UpdateLetterHandler>().Handle(new UpdateLetterCommand(
            letter.Id, "Head", "New body\r\n", "Close", true,
            [new LetterRecipientCommandItem("Other", null, " A Person ", "1 Street\n2 Town", " zz11zz ", "x.y@nhs.net")],
            2, Author), _cts.Token);

        letter.Body.Should().Be("New body");
        letter.LargePrint.Should().BeTrue();
        letter.Version.Should().Be(3);
        letter.Recipients.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new { Kind = LetterRecipientKind.Other, Name = "A Person", AddressLines = "1 Street\n2 Town", Postcode = "ZZ1 1ZZ", Email = "x.y@nhs.net" });
        SavedAudit.Should().BeEmpty();
        response.Version.Should().Be(3);
    }

    [Test]
    public async Task Update_SomeoneElse_IsForbidden_AndAStaleVersionAConflict()
    {
        var letter = Given(OnPathway());
        var command = new UpdateLetterCommand(letter.Id, "", "", "", false, [], 2, Approver);

        await FluentActions.Awaiting(() => Fixture.Create<UpdateLetterHandler>().Handle(command, _cts.Token))
            .Should().ThrowAsync<ForbiddenException>();
        await FluentActions.Awaiting(() => Fixture.Create<UpdateLetterHandler>().Handle(command with { Actor = Author, ExpectedVersion = 1 }, _cts.Token))
            .Should().ThrowAsync<ConflictException>().WithMessage(LetterWorkflow.ChangedMeanwhileMessage);
    }

    [Test]
    public async Task Update_NotADraft_IsAConflict()
    {
        var letter = Given(OnPathway(LetterStatus.AwaitingApproval));

        var act = () => Fixture.Create<UpdateLetterHandler>().Handle(new UpdateLetterCommand(letter.Id, "", "", "", false, [], 2, Author), _cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*awaiting approval*");
    }

    [Test]
    public async Task Update_AReferrerWithoutAReferral_IsRefused()
    {
        var letter = Given(OnPathway());

        var act = () => Fixture.Create<UpdateLetterHandler>().Handle(new UpdateLetterCommand(
            letter.Id, "", "", "", false, [new LetterRecipientCommandItem("Referrer", null, null, null, null, null)], 2, Author), _cts.Token);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*no referrer*");
    }

    // Submitting.

    [Test]
    public async Task Submit_ToAnApprover_AwaitsThem_AndTellsThem()
    {
        var letter = Given(OnPathway());
        letter.ReturnComment = "old";

        await Fixture.Create<SubmitLetterHandler>().Handle(new SubmitLetterCommand(letter.Id, Approver.UserId, 2, Author), _cts.Token);

        letter.Status.Should().Be(LetterStatus.AwaitingApproval);
        letter.ApproverUserId.Should().Be(Approver.UserId);
        letter.ReturnComment.Should().BeNull();
        SavedAudit.Single().Details.Should().Contain(Approver.Username);
        Sent.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            UserId = Approver.UserId, Kind = NotificationKind.LetterAwaitingApproval, Title = LetterNotifications.AwaitingApprovalTitle,
            SubjectId = (Guid?)letter.Id,
        });
    }

    [Test]
    public async Task Submit_ToThemselvesOrToSomeoneWhoCantApprove_IsRefused()
    {
        var letter = Given(OnPathway());

        await FluentActions.Awaiting(() => Fixture.Create<SubmitLetterHandler>().Handle(new SubmitLetterCommand(letter.Id, Author.UserId, 2, Author), _cts.Token))
            .Should().ThrowAsync<ValidationException>().WithMessage("*someone else*");
        await FluentActions.Awaiting(() => Fixture.Create<SubmitLetterHandler>().Handle(new SubmitLetterCommand(letter.Id, Sender.UserId, 2, Author), _cts.Token))
            .Should().ThrowAsync<ValidationException>().WithMessage($"*{SubmitLetterHandler.NotAnApproverMessage}*");
        letter.Status.Should().Be(LetterStatus.Draft);
    }

    [Test]
    public async Task Submit_WithNoRecipients_IsAConflict()
    {
        var letter = Given(OnPathway());
        letter.Recipients.Clear();

        var act = () => Fixture.Create<SubmitLetterHandler>().Handle(new SubmitLetterCommand(letter.Id, Approver.UserId, 2, Author), _cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(LetterWorkflow.NoRecipientsMessage);
    }

    // Approving.

    [Test]
    public async Task Approve_RendersThePdf_FilesItOnThePathway_FreezesTheLetter_AndTellsTheAuthor()
    {
        var letter = Given(OnPathway(LetterStatus.AwaitingApproval));
        Practice.AddressLines = "9 Moved Street";

        var response = await Fixture.Create<ApproveLetterHandler>().Handle(new ApproveLetterCommand(letter.Id, 2, Approver), _cts.Token);

        letter.Status.Should().Be(LetterStatus.Approved);
        letter.ApprovedAtUtc.Should().Be(NowUtc.AddTicks(1));
        var file = SavedFiles.Single();
        file.Should().BeEquivalentTo(new
        {
            PatientId = Patient.Id, PathwayId = (Guid?)Pathway.Id, ReferralId = (Guid?)null, ContentType = "application/pdf",
            SizeBytes = (long)Pdf.Length, UploadedByUserId = Approver.UserId, ScanStatus = AttachmentScanStatus.NotScanned,
        });
        file.FileName.Should().Be("Clinic letter 2026-11-18.pdf");
        letter.PdfAttachmentId.Should().Be(file.Id);
        Store.Blobs.Should().ContainKey(AttachmentStorageKeys.File(file.Id));
        SavedAudit.Select(e => e.Action).Should().Equal(PatientPathwayAuditAction.AttachmentAdded, PatientPathwayAuditAction.LetterApproved);
        letter.Recipients[0].AddressLines.Should().Be("9 Moved Street", "addresses are copied again when it's approved");
        Sent.Should().ContainSingle().Which.Kind.Should().Be(NotificationKind.LetterApproved);
        response.PdfAttachmentId.Should().Be(file.Id);

        Renderer.Verify(r => r.RenderAsync(It.Is<LetterDocument>(d =>
            d.Marking == null
            && d.Body.Contains(Patient.FirstName)
            && d.PatientIdentifiers.Contains("999 000 0018")
            && d.RecipientAddress[0] == Practice.Name
            && d.Closing.Contains("Dr Bo Approver") == false
            && d.Closing.Contains("Copies to:")), _cts.Token));
    }

    [Test]
    public async Task Approve_TheirOwnDraft_ByAClinicianWhoCanApprove_TellsNobody()
    {
        var letter = Given(OnPathway(author: ClinicianAuthor));

        await Fixture.Create<ApproveLetterHandler>().Handle(new ApproveLetterCommand(letter.Id, 2, ClinicianAuthor), _cts.Token);

        letter.Status.Should().Be(LetterStatus.Approved);
        letter.ApproverUserId.Should().Be(ClinicianAuthor.UserId);
        Sent.Should().BeEmpty();
        SavedAudit.Last().Details.Should().Contain("\"selfApproved\":true");
    }

    [Test]
    public void EnsureMayApprove_RefusesWhoeverItIsntFor()
    {
        var awaiting = OnPathway(LetterStatus.AwaitingApproval);
        var draft = OnPathway();

        FluentActions.Invoking(() => ApproveLetterHandler.EnsureMayApprove(awaiting, ClinicianAuthor)).Should().Throw<ForbiddenException>();
        FluentActions.Invoking(() => ApproveLetterHandler.EnsureMayApprove(draft, Approver)).Should().Throw<ForbiddenException>();
        FluentActions.Invoking(() => ApproveLetterHandler.EnsureMayApprove(draft, Author)).Should().Throw<ForbiddenException>()
            .WithMessage(LetterWorkflow.CantApproveOwnMessage);
        FluentActions.Invoking(() => ApproveLetterHandler.EnsureMayApprove(OnPathway(LetterStatus.Approved), Approver)).Should().Throw<ConflictException>();
        FluentActions.Invoking(() => ApproveLetterHandler.EnsureMayApprove(awaiting, Approver)).Should().NotThrow();
    }

    [Test]
    public async Task Approve_WithoutTheApproversFullName_IsAConflict()
    {
        Staff.Setup(s => s.GetAsync(Approver.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(new MedApp.Application.Common.Identity.StaffDetails(null, "Consultant"));
        var letter = Given(OnPathway(LetterStatus.AwaitingApproval));

        var act = () => Fixture.Create<ApproveLetterHandler>().Handle(new ApproveLetterCommand(letter.Id, 2, Approver), _cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(LetterWorkflow.NoFullNameMessage);
        SavedFiles.Should().BeEmpty();
    }

    [Test]
    public async Task Approve_AMergeFieldWithNoValue_IsAConflictNamingIt()
    {
        Patient.NhsNumber = null;
        var letter = Given(OnPathway(LetterStatus.AwaitingApproval));
        letter.Body = "NHS number: {{patient.nhsNumber}}";

        var act = () => Fixture.Create<ApproveLetterHandler>().Handle(new ApproveLetterCommand(letter.Id, 2, Approver), _cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*the patient's NHS number*");
        Store.Blobs.Should().BeEmpty();
    }

    [Test]
    public async Task Approve_ACharacterTheFontLacks_InThePatientsName_IsAConflict()
    {
        Patient.LastName = "Synthetiç";
        Renderer.Setup(r => r.FindUnsupportedCharacters(It.IsAny<string?>()))
            .Returns((string? text) => text?.Contains('ç') == true ? ["ç"] : []);
        var letter = Given(OnPathway(LetterStatus.AwaitingApproval));

        var act = () => Fixture.Create<ApproveLetterHandler>().Handle(new ApproveLetterCommand(letter.Id, 2, Approver), _cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*can't show ç*");
    }

    [Test]
    public async Task Approve_ARecipientWithNoAddress_OrAPracticeGone_IsAConflict()
    {
        var letter = Given(OnPathway(LetterStatus.AwaitingApproval));
        letter.Recipients.Add(new LetterRecipient { Id = Guid.NewGuid(), Position = 2, Kind = LetterRecipientKind.Other, Name = "No Address" });

        var act = () => Fixture.Create<ApproveLetterHandler>().Handle(new ApproveLetterCommand(letter.Id, 2, Approver), _cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*No Address has no address*");

        letter.Recipients.RemoveAt(2);
        letter.Recipients[0].PracticeId = Guid.NewGuid();
        await act.Should().ThrowAsync<ConflictException>().WithMessage("*no longer in the directory*");
    }

    [Test]
    public async Task Approve_SaveFails_DeletesTheStoredPdf()
    {
        var letter = Given(OnPathway(LetterStatus.AwaitingApproval));
        Letters.Setup(l => l.SaveApprovalAsync(It.IsAny<Attachment>(), It.IsAny<IEnumerable<PatientPathwayAuditEntry>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException(LetterWorkflow.ChangedMeanwhileMessage));

        var act = () => Fixture.Create<ApproveLetterHandler>().Handle(new ApproveLetterCommand(letter.Id, 2, Approver), _cts.Token);

        await act.Should().ThrowAsync<ConflictException>();
        Store.Blobs.Should().BeEmpty();
        Store.Deleted.Should().ContainSingle();
    }

    [Test]
    public async Task Approve_AReferralsLetter_FilesThePdfOnTheReferral()
    {
        var referral = ReferralsTestConstants.CreateReferral();
        referral.Patient = Patient;
        referral.PracticeId = Practice.Id;
        Referrals.Setup(r => r.GetByIdAsync(referral.Id, It.IsAny<CancellationToken>())).ReturnsAsync(referral);
        var letter = Given(new Letter
        {
            Id = Guid.NewGuid(), PatientId = Patient.Id, ReferralId = referral.Id, TemplateName = TemplateName, Kind = LetterKind.InformationRequest,
            Letterhead = Letterhead, Body = "Please send more.", Closing = "Thanks", Status = LetterStatus.Draft,
            AuthorUserId = ClinicianAuthor.UserId, Version = 1,
            Recipients = [new LetterRecipient { Id = Guid.NewGuid(), Kind = LetterRecipientKind.Referrer, PracticeId = Practice.Id, Name = "x" }],
        });

        await Fixture.Create<ApproveLetterHandler>().Handle(new ApproveLetterCommand(letter.Id, 1, ClinicianAuthor), _cts.Token);

        SavedFiles.Single().Should().BeEquivalentTo(new { PathwayId = (Guid?)null, ReferralId = (Guid?)referral.Id });
        letter.Recipients[0].Name.Should().Be($"{ReferralsTestConstants.Clinician}, {Practice.Name}");
        letter.Recipients[0].AddressLines.Should().Be(Practice.AddressLines);
    }

    // Returning, discarding, withdrawing, correcting.

    [Test]
    public async Task Return_ByTheApprover_MakesItADraftAgain_WithTheComment_AndTellsTheAuthor()
    {
        var letter = Given(OnPathway(LetterStatus.AwaitingApproval));

        await Fixture.Create<ReturnLetterHandler>().Handle(new ReturnLetterCommand(letter.Id, " Add the plan. ", 2, Approver), _cts.Token);

        letter.Status.Should().Be(LetterStatus.Draft);
        letter.ReturnComment.Should().Be("Add the plan.");
        SavedAudit.Single().Details.Should().NotContain("plan");
        Sent.Single().Should().BeEquivalentTo(new { UserId = Author.UserId, Kind = NotificationKind.LetterReturned });
        Sent.Single().Message.Should().NotContain("plan");
    }

    [Test]
    public async Task Return_BySomeoneElse_IsForbidden()
    {
        var letter = Given(OnPathway(LetterStatus.AwaitingApproval));

        var act = () => Fixture.Create<ReturnLetterHandler>().Handle(new ReturnLetterCommand(letter.Id, "No", 2, ClinicianAuthor), _cts.Token);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Test]
    public async Task Discard_TheAuthorsDraft_WithdrawsItWithNoReason()
    {
        var letter = Given(OnPathway());

        await Fixture.Create<DiscardLetterHandler>().Handle(new DiscardLetterCommand(letter.Id, 2, Author), _cts.Token);

        letter.Status.Should().Be(LetterStatus.Withdrawn);
        letter.WithdrawnReason.Should().BeNull();
        SavedAudit.Single().Details.Should().Contain("\"wasDraft\":true");
    }

    [Test]
    public async Task Discard_NotADraft_IsAConflict()
    {
        var letter = Given(OnPathway(LetterStatus.AwaitingApproval));

        var act = () => Fixture.Create<DiscardLetterHandler>().Handle(new DiscardLetterCommand(letter.Id, 2, Author), _cts.Token);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Test]
    public async Task Withdraw_ByTheApproverOrAnAdmin_KeepsThePdf_AndNamesWhoItReached()
    {
        var letter = Given(OnPathway(LetterStatus.Approved));
        letter.Recipients[0].DispatchStatus = LetterDispatchStatus.Sent;
        var pdf = letter.PdfAttachmentId;

        await Fixture.Create<WithdrawLetterHandler>().Handle(new WithdrawLetterCommand(letter.Id, " Wrong patient ", 2, AdminActor), _cts.Token);

        letter.Status.Should().Be(LetterStatus.Withdrawn);
        letter.WithdrawnReason.Should().Be("Wrong patient");
        letter.PdfAttachmentId.Should().Be(pdf);
        SavedAudit.Single().Details.Should().Contain("\"sentTo\":[\"GpPractice\"]").And.NotContain("Wrong patient");
    }

    [Test]
    public async Task Withdraw_ByTheAuthor_IsForbidden_AndADraft_AConflict()
    {
        var approved = Given(OnPathway(LetterStatus.Approved));
        var draft = Given(OnPathway());

        await FluentActions.Awaiting(() => Fixture.Create<WithdrawLetterHandler>().Handle(new WithdrawLetterCommand(approved.Id, "x", 2, Author), _cts.Token))
            .Should().ThrowAsync<ForbiddenException>();
        await FluentActions.Awaiting(() => Fixture.Create<WithdrawLetterHandler>().Handle(new WithdrawLetterCommand(draft.Id, "x", 2, Approver), _cts.Token))
            .Should().ThrowAsync<ConflictException>().WithMessage("*Only an approved letter*");
    }

    [Test]
    public async Task Withdraw_ARestrictedRecordsLetter_IsAllowed_ButNotAnAnonymisedOnes()
    {
        var letter = Given(OnPathway(LetterStatus.Approved));
        Patient.RestrictedAtUtc = NowUtc;

        await Fixture.Create<WithdrawLetterHandler>().Handle(new WithdrawLetterCommand(letter.Id, "x", 2, Approver), _cts.Token);
        letter.Status.Should().Be(LetterStatus.Withdrawn);

        var other = Given(OnPathway(LetterStatus.Approved));
        Patient.AnonymisedAtUtc = NowUtc;
        await FluentActions.Awaiting(() => Fixture.Create<WithdrawLetterHandler>().Handle(new WithdrawLetterCommand(other.Id, "x", 2, Approver), _cts.Token))
            .Should().ThrowAsync<ConflictException>();
    }

    [Test]
    public async Task Correction_OfAWithdrawnLetter_IsANewDraftWithItsTextAndRecipients()
    {
        var old = Given(OnPathway(LetterStatus.Approved));
        old.Status = LetterStatus.Withdrawn;
        old.Body = "The old text";

        var response = await Fixture.Create<WriteLetterCorrectionHandler>().Handle(new WriteLetterCorrectionCommand(old.Id, ClinicianAuthor), _cts.Token);

        var letter = Added.Single();
        letter.Should().BeEquivalentTo(new { ReplacesLetterId = (Guid?)old.Id, Body = "The old text", AuthorUserId = ClinicianAuthor.UserId, Status = LetterStatus.Draft });
        letter.Recipients.Select(r => r.Kind).Should().Equal(LetterRecipientKind.GpPractice, LetterRecipientKind.Patient);
        letter.Recipients.Select(r => r.Id).Should().NotIntersectWith(old.Recipients.Select(r => r.Id));
        response.ReplacesLetterId.Should().Be(old.Id);
    }

    [Test]
    public async Task Correction_Twice_OrOfADraft_IsAConflict()
    {
        var old = Given(OnPathway(LetterStatus.Approved));
        old.Status = LetterStatus.Withdrawn;
        Letters.Setup(l => l.GetCorrectionIdAsync(old.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Guid.NewGuid());
        var draft = Given(OnPathway());

        await FluentActions.Awaiting(() => Fixture.Create<WriteLetterCorrectionHandler>().Handle(new WriteLetterCorrectionCommand(old.Id, Author), _cts.Token))
            .Should().ThrowAsync<ConflictException>().WithMessage("*already*");
        await FluentActions.Awaiting(() => Fixture.Create<WriteLetterCorrectionHandler>().Handle(new WriteLetterCorrectionCommand(draft.Id, Author), _cts.Token))
            .Should().ThrowAsync<ConflictException>();
    }

    // Sending.

    [Test]
    public async Task Send_BySecureEmail_ClaimsTheLetter_SendsThePdf_AndMarksItSent()
    {
        var letter = Given(OnPathway(LetterStatus.Approved));
        Store.Blobs[AttachmentStorageKeys.File(letter.PdfAttachmentId!.Value)] = (Pdf, "application/pdf");
        var practice = letter.Recipients[0];

        var response = await Fixture.Create<SendLetterHandler>().Handle(new SendLetterCommand(letter.Id, practice.Id, 2, Sender), _cts.Token);

        Transport.Verify(t => t.SendAsync(It.Is<SecureLetterEmail>(e =>
            e.To == PracticesTestConstants.SecureEmail && e.Pdf.SequenceEqual(Pdf) && e.FileName == SecureLetterEmails.FileName
            && !e.TextBody.Contains(Patient.LastName) && e.Subject == "A letter from Synthetic Specialist Centre"), _cts.Token));
        practice.Should().BeEquivalentTo(new
        {
            DispatchStatus = LetterDispatchStatus.Sent, Method = (LetterDispatchMethod?)LetterDispatchMethod.SecureEmail,
            SentOn = (DateOnly?)new DateOnly(2026, 11, 18), SentByUserId = (Guid?)Sender.UserId, TransportReference = "<ref@relay>",
        });
        Letters.Verify(l => l.SaveAsync(It.Is<IEnumerable<PatientPathwayAuditEntry>>(a => !a.Any()), _cts.Token), Times.Once, "the claim");
        SavedAudit.Single().Details.Should().Contain("\"method\":\"SecureEmail\"").And.Contain("\"dispatchStatus\":\"Sent\"").And.NotContain("nhs.net");
        response.Recipients[0].DispatchStatus.Should().Be("Sent");
        response.Recipients[1].CanRecordSent.Should().BeTrue();
    }

    [Test]
    public async Task Send_TheRelayRefuses_RecordsTheFailure_AndSaysSo()
    {
        var letter = Given(OnPathway(LetterStatus.Approved));
        Store.Blobs[AttachmentStorageKeys.File(letter.PdfAttachmentId!.Value)] = (Pdf, "application/pdf");
        Transport.Setup(t => t.SendAsync(It.IsAny<SecureLetterEmail>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new LetterTransportException("refused, 550"));

        var act = () => Fixture.Create<SendLetterHandler>().Handle(new SendLetterCommand(letter.Id, letter.Recipients[0].Id, 2, Sender), _cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(SendLetterHandler.FailedMessage);
        letter.Recipients[0].DispatchStatus.Should().Be(LetterDispatchStatus.Failed);
        letter.Recipients[0].SentOn.Should().BeNull();
        SavedAudit.Single().Details.Should().Contain("\"dispatchStatus\":\"Failed\"");
    }

    [Test]
    public async Task Send_ToThePatient_OrWithTheRelayOff_OrToADomainNotAllowed_IsRefusedWithoutEmailing()
    {
        var letter = Given(OnPathway(LetterStatus.Approved));
        Store.Blobs[AttachmentStorageKeys.File(letter.PdfAttachmentId!.Value)] = (Pdf, "application/pdf");
        var handler = Fixture.Create<SendLetterHandler>();

        await FluentActions.Awaiting(() => handler.Handle(new SendLetterCommand(letter.Id, letter.Recipients[1].Id, 2, Sender), _cts.Token))
            .Should().ThrowAsync<ConflictException>().WithMessage(LetterWorkflow.PatientEmailMessage);

        letter.Recipients[0].Email = "someone@example.com";
        await FluentActions.Awaiting(() => handler.Handle(new SendLetterCommand(letter.Id, letter.Recipients[0].Id, 2, Sender), _cts.Token))
            .Should().ThrowAsync<ConflictException>().WithMessage("*nhs.net*");

        Transport.SetupGet(t => t.IsAvailable).Returns(false);
        await FluentActions.Awaiting(() => handler.Handle(new SendLetterCommand(letter.Id, letter.Recipients[0].Id, 2, Sender), _cts.Token))
            .Should().ThrowAsync<ConflictException>().WithMessage(LetterWorkflow.SecureEmailOffMessage);

        Transport.Verify(t => t.SendAsync(It.IsAny<SecureLetterEmail>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Send_AlreadySent_UnknownRecipient_OrNotApproved_AreRefused()
    {
        var letter = Given(OnPathway(LetterStatus.Approved));
        letter.Recipients[0].DispatchStatus = LetterDispatchStatus.Sent;
        var draft = Given(OnPathway());
        var handler = Fixture.Create<RecordLetterSentHandler>();

        await FluentActions.Awaiting(() => handler.Handle(new RecordLetterSentCommand(letter.Id, letter.Recipients[0].Id, "Post", Today, 2, Sender), _cts.Token))
            .Should().ThrowAsync<ConflictException>().WithMessage("*already*");
        await FluentActions.Awaiting(() => handler.Handle(new RecordLetterSentCommand(letter.Id, Guid.NewGuid(), "Post", Today, 2, Sender), _cts.Token))
            .Should().ThrowAsync<NotFoundException>();
        await FluentActions.Awaiting(() => handler.Handle(new RecordLetterSentCommand(draft.Id, draft.Recipients[0].Id, "Post", Today, 2, Sender), _cts.Token))
            .Should().ThrowAsync<ConflictException>().WithMessage("*approved*");
    }

    private static readonly DateOnly Today = new(2026, 11, 18);

    [Test]
    public async Task RecordSent_AsPosted_MarksItSentOnThatDay()
    {
        var letter = Given(OnPathway(LetterStatus.Approved));

        await Fixture.Create<RecordLetterSentHandler>().Handle(
            new RecordLetterSentCommand(letter.Id, letter.Recipients[0].Id, "Post", Today.AddDays(-1), 2, Sender), _cts.Token);

        letter.Recipients[0].Should().BeEquivalentTo(new
        {
            DispatchStatus = LetterDispatchStatus.Sent, Method = (LetterDispatchMethod?)LetterDispatchMethod.Post, SentOn = (DateOnly?)Today.AddDays(-1),
        });
        letter.IsSentToEveryone.Should().BeFalse();
    }

    [TestCase(1, "*future*")]
    [TestCase(-8, "*last 7 days*")]
    [TestCase(-2, "*before the letter was approved*")]
    public async Task RecordSent_ADateOutOfBounds_IsRefused(int days, string message)
    {
        var letter = Given(OnPathway(LetterStatus.Approved));

        var act = () => Fixture.Create<RecordLetterSentHandler>().Handle(
            new RecordLetterSentCommand(letter.Id, letter.Recipients[0].Id, "Post", Today.AddDays(days), 2, Sender), _cts.Token);

        await act.Should().ThrowAsync<ValidationException>().WithMessage(message);
    }

    [Test]
    public async Task RecordSent_HandedToAPractice_OrEmailedToThePatient_IsRefused()
    {
        var letter = Given(OnPathway(LetterStatus.Approved));
        var handler = Fixture.Create<RecordLetterSentHandler>();

        await FluentActions.Awaiting(() => handler.Handle(new RecordLetterSentCommand(letter.Id, letter.Recipients[0].Id, "HandedToPatient", Today, 2, Sender), _cts.Token))
            .Should().ThrowAsync<ValidationException>().WithMessage("*Only the patient*");
        await FluentActions.Awaiting(() => handler.Handle(new RecordLetterSentCommand(letter.Id, letter.Recipients[1].Id, "EmailOutsideMedApp", Today, 2, Sender), _cts.Token))
            .Should().ThrowAsync<ValidationException>().WithMessage($"*{LetterWorkflow.PatientEmailMessage}*");
    }

    [Test]
    public async Task RecordSent_OnARestrictedRecord_IsAConflictAskingAnAdmin()
    {
        var letter = Given(OnPathway(LetterStatus.Approved));
        Patient.RestrictedAtUtc = NowUtc;

        var act = () => Fixture.Create<RecordLetterSentHandler>().Handle(
            new RecordLetterSentCommand(letter.Id, letter.Recipients[1].Id, "HandedToPatient", Today, 2, Sender), _cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(LetterWorkflow.RestrictedMessage);
    }
}
