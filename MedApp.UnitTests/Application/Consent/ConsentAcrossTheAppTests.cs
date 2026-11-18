using AutoFixture;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Attachments.Commands.DeleteAttachment;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Consent.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.PatientPathways.Commands.DeletePatientPathway;
using MedApp.Application.Pathways.PatientPathways.Commands.FinishPathwayStage;
using MedApp.Application.Pathways.PatientPathwayStages.Definitions;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Pathways.Timeline.Common;
using MedApp.Application.Patients.Common;
using MedApp.Domain.Consent;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.UnitTests.Application.Consent;

/// <summary>What consent records change elsewhere: the stage gate, deletions refused, the stage flag, privacy and the timeline.</summary>
[TestFixture]
public sealed class ConsentAcrossTheAppTests : PathwayHandlerTestBase
{
    [Test]
    public async Task FinishStage_ThatNeedsConsent_IsRefusedUntilAValidRecordExists()
    {
        var pathway = GivenPathway(CreatePathway(Status.Assigned));
        pathway.StageAt(1).NeedsConsent = true;
        var handler = Fixture.Create<FinishPathwayStageHandler>();

        var act = () => handler.Handle(new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Staff), CancellationToken.None);
        await act.Should().ThrowAsync<ConflictException>().WithMessage(StageGates.ConsentMissingMessage);

        Consents.Setup(c => c.GetForStageAsync(pathway.StageAt(1).Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([ConsentTestConstants.CreateRecord(pathway)]);
        var response = await handler.Handle(new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Staff), CancellationToken.None);
        response.Stages[0].State.Should().Be("Completed");
        response.Stages[0].NeedsConsent.Should().BeTrue();
    }

    [Test]
    public async Task DeletePathway_WithConsentRecords_IsAConflict()
    {
        var pathway = GivenPathway(CreatePathway(Status.Cancelled));
        Consents.Setup(c => c.CountForPathwayAsync(pathway.Id, It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var act = () => Fixture.Create<DeletePatientPathwayHandler>().Handle(new DeletePatientPathwayCommand(pathway.Id, Admin), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*1 consent record,*");
    }

    [Test]
    public async Task DeleteAttachment_AConsentFormsScan_IsAConflictEvenForAnAdmin()
    {
        var attachment = AttachmentsTestConstants.CreateAttachment(uploadedBy: Admin.UserId);
        var attachments = new Mock<MedApp.Application.Attachments.Repositories.IAttachmentRepository>();
        attachments.Setup(a => a.GetForUpdateAsync(attachment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(attachment);
        var letters = new Mock<MedApp.Application.Letters.Repositories.ILetterRepository>();
        letters.Setup(l => l.IsPdfAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        Specimens.Setup(s => s.IsReportAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        Consents.Setup(c => c.IsScanAsync(attachment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        Fixture.Inject(attachments.Object);
        Fixture.Inject(letters.Object);

        var act = () => Fixture.Create<DeleteAttachmentHandler>().Handle(
            new DeleteAttachmentCommand(attachment.Id, Admin, CanCorrectRecords: true), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(ConsentWorkflow.ScanFileMessage);
        attachment.IsDeleted.Should().BeFalse();
    }

    [Test]
    public void StageDefinition_AndPathwayCreation_CarryNeedsConsent()
    {
        var definition = CreateDefinition("Biopsy");
        definition.NeedsConsent = true;
        StageDefinitionMapper.ToResponse(definition, StageDefinitionUsage.None).NeedsConsent.Should().BeTrue();

        var template = CreateTemplate(TemplateName, false, definition);
        var pathway = PathwayCreation.FromTemplate(template, Guid.NewGuid(), null, Today.AddDays(7), PatientPathwayPriority.Medium, Now);
        pathway.Stages.Single().NeedsConsent.Should().BeTrue();
    }

    [Test]
    public void PathwayResponse_CanRecordConsent_OnlyForHoldersOfRecordConsent()
    {
        var pathway = CreatePathway(Status.InProgress);
        var holder = PathwayMapper.ToResponse(pathway, new Dictionary<Guid, string>(), Now, viewerPermissions: ConsentTestConstants.Recorder.Permissions);
        var admin = PathwayMapper.ToResponse(pathway, new Dictionary<Guid, string>(), Now, viewerPermissions: Permissions.Admin);

        holder.Stages.Should().OnlyContain(s => s.CanRecordConsent);
        admin.Stages.Should().OnlyContain(s => !s.CanRecordConsent, "recording consent is clinical: Admin doesn't hold it");
    }

    [Test]
    public void Anonymiser_ReplacesTheDetail_KeepingTheFormAndDates()
    {
        var pathway = CreatePathway(Status.Completed, finishedStages: 3);
        var consent = ConsentTestConstants.CreateRecord(pathway, ConsentStatus.Withdrawn);
        var record = PrivacyTestConstants.CreateRecord(null, pathway) with { ConsentRecords = [consent] };

        PatientAnonymiser.Apply(record, Now, new ActivityActor(Admin.UserId, Admin.Username, null));

        consent.Should().BeEquivalentTo(new
        {
            StatusDetail = PatientAnonymiser.RemovedText, FormName = ConsentTestConstants.FormName, SignedOn = Today,
            Status = ConsentStatus.Withdrawn,
        });
    }

    [Test]
    public void Export_ListsConsentRecords_WithStaffByUsername()
    {
        var pathway = CreatePathway(Status.Completed, finishedStages: 3);
        var consent = ConsentTestConstants.CreateRecord(pathway, ConsentStatus.Withdrawn);
        var record = PrivacyTestConstants.CreateRecord(null, pathway) with { ConsentRecords = [consent] };

        var export = PatientExportMapper.ToResponse(record, new Dictionary<Guid, string> { [Staff.UserId] = Staff.Username }, Now, "exporter");

        export.ConsentRecords.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            consent.Id, FormName = ConsentTestConstants.FormName, SignedBy = "Patient", Status = "Withdrawn",
            RecordedBy = Staff.Username, StatusChangedBy = Staff.Username, StatusDetail = "Synthetic detail.",
        });
        PatientExportMapper.UserIds(record).Should().Contain(Staff.UserId);
    }

    [Test]
    public void TimelineKinds_ConsentActions_AreTheConsentKind_InMemoryAndInLinq()
    {
        var entries = new[] { PatientPathwayAuditAction.ConsentRecorded, PatientPathwayAuditAction.ConsentWithdrawn, PatientPathwayAuditAction.ConsentEnteredInError, PatientPathwayAuditAction.Created }
            .Select(action => new PatientPathwayAuditEntry { Id = Guid.NewGuid(), Action = action, Details = "{}" })
            .ToList();

        entries.Take(3).Should().OnlyContain(e => PatientTimelineKinds.Of(e) == PatientTimelineKind.Consent);
        entries.AsQueryable().WhereKindIn(new HashSet<PatientTimelineKind> { PatientTimelineKind.Consent })
            .Should().HaveCount(3);
    }
}
