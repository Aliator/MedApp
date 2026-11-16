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
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;

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

    private static UploadAttachmentCommand Upload(Guid? patientId = null, Guid? procedureId = null, Guid? stageId = null, bool isAdmin = false) =>
        new(patientId, procedureId, stageId, FileName, Pdf.Length, Stream(Pdf), isAdmin ? Admin : Staff, isAdmin);

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
        var procedure = CreateProcedure(Status.InProgress, patient: Restricted());
        var stage = GivenStage(procedure, 1);

        var act = () => Fixture.Create<UploadAttachmentHandler>().Handle(Upload(stageId: stage.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PatientRules.RestrictedMessage);
    }

    [Test]
    public async Task Upload_AnonymisedPatientsProcedure_ThrowsTheAnonymisedConflict()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.Completed, patient: Anonymised()));

        var act = () => Fixture.Create<UploadAttachmentHandler>().Handle(Upload(procedureId: procedure.Id), CancellationToken.None);

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

        var act = () => Fixture.Create<DeleteAttachmentHandler>().Handle(new DeleteAttachmentCommand(attachment.Id, Admin, IsAdmin: true), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PatientRules.RestrictedMessage);
        attachment.IsDeleted.Should().BeFalse();
    }

    [Test]
    public async Task Delete_RestrictedPatientsProcedureFile_ThrowsTheGuardsConflict()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress, patient: Restricted()));
        var attachment = GivenAttachment(AttachmentOn(procedure));

        var act = () => Fixture.Create<DeleteAttachmentHandler>().Handle(new DeleteAttachmentCommand(attachment.Id, Staff, IsAdmin: false), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PatientRules.RestrictedMessage);
    }

    [Test]
    public async Task List_RecordsAViewOfThePatientsFiles_WithTheProcedure()
    {
        var procedure = GivenProcedure(CreateProcedure());
        var query = new GetAttachmentsQuery(null, procedure.Id, null, false, Staff.UserId, false);
        Attachments.Setup(a => a.ListAsync(query, It.IsAny<CancellationToken>())).ReturnsAsync(([], 0));
        using var cts = new CancellationTokenSource();

        await Fixture.Create<GetAttachmentsHandler>().Handle(query, cts.Token);

        Activity.Views.Should().ContainSingle().Which.Should().BeEquivalentTo(
            (ActivityAction.AttachmentsViewed, new List<Guid> { procedure.PatientId }, (Guid?)procedure.Id));
        Activity.Tokens.Should().Contain(cts.Token);
    }

    [Test]
    public async Task List_PatientsOwnFiles_RecordsAViewWithNoProcedure()
    {
        var patient = GivenPatient();
        var query = new GetAttachmentsQuery(patient.Id, null, null, false, Staff.UserId, false);
        Attachments.Setup(a => a.ListAsync(query, It.IsAny<CancellationToken>())).ReturnsAsync(([], 0));

        await Fixture.Create<GetAttachmentsHandler>().Handle(query, CancellationToken.None);

        Activity.Views.Single().ProcedureId.Should().BeNull();
    }

    [Test]
    public async Task Download_RecordsADownloadOfThePatientsData_AndIsAllowedWhileRestricted()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress, patient: Restricted()));
        var attachment = GivenAttachment(AttachmentOn(procedure, stageOrder: 1));
        await Store.SaveAsync(AttachmentStorageKeys.File(attachment.Id), Stream(Pdf), "application/pdf", CancellationToken.None);

        await using var download = (await Fixture.Create<DownloadAttachmentHandler>()
            .Handle(new DownloadAttachmentCommand(attachment.Id, Staff), CancellationToken.None)).Content;

        Activity.Views.Should().ContainSingle().Which.Should().BeEquivalentTo(
            (ActivityAction.AttachmentDownloaded, new List<Guid> { attachment.PatientId }, (Guid?)procedure.Id));
    }
}
