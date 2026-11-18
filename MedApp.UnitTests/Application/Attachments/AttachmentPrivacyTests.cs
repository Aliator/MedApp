using AutoFixture;
using FluentAssertions;
using MedApp.Application.Attachments.Commands.DeleteAttachment;
using MedApp.Application.Attachments.Commands.DownloadAttachment;
using MedApp.Application.Attachments.Commands.UploadAttachment;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Attachments.Queries.GetAttachments;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Common;
using MedApp.Domain.Activity;
using MedApp.Domain.Patients;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.AttachmentsTestConstants;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.UnitTests.Application.Attachments;

/// <summary>
/// Attachments and the patient's privacy controls: a restricted or anonymised record's files can't be added or
/// deleted, by anyone; listing and downloading are recorded in the activity log as reads of the patient's data.
/// </summary>
[TestFixture]
public sealed class AttachmentPrivacyTests : AttachmentHandlerTestBase
{
    private static Patient Restricted()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        patient.RestrictedAtUtc = Now.AddDays(-1);
        patient.RestrictionReason = "Synthetic restriction";
        return patient;
    }

    private static Patient Anonymised()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        patient.AnonymisedAtUtc = Now.AddDays(-1);
        return patient;
    }

    private void GivenThePatient(Patient patient) =>
        Patients.Setup(p => p.GetByIdAsync(patient.Id, It.IsAny<CancellationToken>())).ReturnsAsync(patient);

    private static UploadAttachmentCommand Upload(Guid? patientId = null, Guid? pathwayId = null, Guid? stageId = null, bool isAdmin = false) =>
        new(patientId, pathwayId, stageId, FileName, Pdf.Length, Stream(Pdf), isAdmin ? Admin : Staff, isAdmin);

    [Test]
    public async Task Upload_RestrictedPatient_ThrowsTheGuardsConflictEvenForAnAdmin()
    {
        var patient = Restricted();
        GivenThePatient(patient);

        var act = () => Fixture.Create<UploadAttachmentHandler>().Handle(Upload(patientId: patient.Id, isAdmin: true), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PatientRules.RestrictedMessage);
        Store.Blobs.Should().BeEmpty();
    }

    [Test]
    public async Task Upload_RestrictedPatientsStage_ThrowsTheGuardsConflict()
    {
        var pathway = CreatePathway(Status.InProgress, patient: Restricted());
        var stage = GivenStage(pathway, 1);

        var act = () => Fixture.Create<UploadAttachmentHandler>().Handle(Upload(stageId: stage.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PatientRules.RestrictedMessage);
    }

    [Test]
    public async Task Upload_AnonymisedPatientsPathway_ThrowsTheAnonymisedConflict()
    {
        var pathway = GivenPathway(CreatePathway(Status.Completed, patient: Anonymised()));

        var act = () => Fixture.Create<UploadAttachmentHandler>().Handle(Upload(pathwayId: pathway.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PatientRules.AnonymisedMessage);
    }

    [Test]
    public async Task Delete_RestrictedPatientsFile_ThrowsTheGuardsConflictEvenForAnAdmin()
    {
        var patient = Restricted();
        GivenThePatient(patient);
        var attachment = GivenAttachment(new MedApp.Domain.Attachments.Attachment
        {
            Id = Guid.NewGuid(), PatientId = patient.Id, FileName = FileName, ContentType = "application/pdf", UploadedByUserId = Staff.UserId,
        });

        var act = () => Fixture.Create<DeleteAttachmentHandler>().Handle(new DeleteAttachmentCommand(attachment.Id, Admin, CanCorrectRecords: true), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PatientRules.RestrictedMessage);
        attachment.IsDeleted.Should().BeFalse();
    }

    [Test]
    public async Task Delete_RestrictedPatientsPathwayFile_ThrowsTheGuardsConflict()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress, patient: Restricted()));
        var attachment = GivenAttachment(AttachmentOn(pathway));

        var act = () => Fixture.Create<DeleteAttachmentHandler>().Handle(new DeleteAttachmentCommand(attachment.Id, Staff, CanCorrectRecords: false), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PatientRules.RestrictedMessage);
    }

    [Test]
    public async Task List_RecordsAViewOfThePatientsFiles_WithThePathway()
    {
        var pathway = GivenPathway(CreatePathway());
        var query = new GetAttachmentsQuery(null, pathway.Id, null, false, Staff.UserId, false, true);
        Attachments.Setup(a => a.ListAsync(query, It.IsAny<CancellationToken>())).ReturnsAsync(([], 0));
        using var cts = new CancellationTokenSource();

        await Fixture.Create<GetAttachmentsHandler>().Handle(query, cts.Token);

        Activity.Views.Should().ContainSingle().Which.Should().BeEquivalentTo(
            (ActivityAction.AttachmentsViewed, new List<Guid> { pathway.PatientId }, (Guid?)pathway.Id));
        Activity.Tokens.Should().Contain(cts.Token);
    }

    [Test]
    public async Task List_PatientsOwnFiles_RecordsAViewWithNoPathway()
    {
        var patient = GivenPatient();
        var query = new GetAttachmentsQuery(patient.Id, null, null, false, Staff.UserId, false, true);
        Attachments.Setup(a => a.ListAsync(query, It.IsAny<CancellationToken>())).ReturnsAsync(([], 0));

        await Fixture.Create<GetAttachmentsHandler>().Handle(query, CancellationToken.None);

        Activity.Views.Single().PathwayId.Should().BeNull();
    }

    [Test]
    public async Task Download_RecordsADownloadOfThePatientsData_AndIsAllowedWhileRestricted()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress, patient: Restricted()));
        var attachment = GivenAttachment(AttachmentOn(pathway, stageOrder: 1));
        await Store.SaveAsync(AttachmentStorageKeys.File(attachment.Id), Stream(Pdf), "application/pdf", CancellationToken.None);

        await using var download = (await Fixture.Create<DownloadAttachmentHandler>()
            .Handle(new DownloadAttachmentCommand(attachment.Id, Staff), CancellationToken.None)).Content;

        Activity.Views.Should().ContainSingle().Which.Should().BeEquivalentTo(
            (ActivityAction.AttachmentDownloaded, new List<Guid> { attachment.PatientId }, (Guid?)pathway.Id));
    }
}
