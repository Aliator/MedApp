using AutoFixture;
using FluentAssertions;
using MedApp.Application.Attachments.Commands.DeleteAttachment;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.AttachmentsTestConstants;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;

namespace MedApp.UnitTests.Application.Attachments.Commands.DeleteAttachment;

[TestFixture]
public sealed class DeleteAttachmentHandlerTests : AttachmentHandlerTestBase
{
    private DeleteAttachmentHandler Handler => Fixture.Create<DeleteAttachmentHandler>();

    [Test]
    public async Task Handle_StaffDeletesTheirOwnPatientFile_HidesItAndAuditsIt()
    {
        var attachment = GivenAttachment(CreateAttachment(uploadedBy: Staff.UserId));
        using var cts = new CancellationTokenSource();

        await Handler.Handle(new DeleteAttachmentCommand(attachment.Id, Staff, IsAdmin: false), cts.Token);

        attachment.DeletedAtUtc.Should().Be(Now);
        attachment.DeletedByUserId.Should().Be(Staff.UserId);
        attachment.DeletedByUsername.Should().Be(Staff.Username);
        SavedAudit.Should().ContainSingle(e => e.Action == PatientProcedureAuditAction.AttachmentDeleted
                                               && e.PatientProcedureId == PatientProcedureAuditEntry.NoProcedure);
        Attachments.Verify(a => a.SaveAsync(It.IsAny<PatientProcedureAuditEntry>(), cts.Token), Times.Once);
        Store.Deleted.Should().BeEmpty("the bytes are purged later, after the retention window");
    }

    [Test]
    public async Task Handle_StaffDeletesSomeoneElsesFile_ThrowsForbidden()
    {
        var attachment = GivenAttachment(CreateAttachment(uploadedBy: OtherUserId));

        var act = () => Handler.Handle(new DeleteAttachmentCommand(attachment.Id, Staff, IsAdmin: false), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        attachment.IsDeleted.Should().BeFalse();
        SavedAudit.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_AdminDeletesSomeoneElsesFile_DeletesIt()
    {
        var attachment = GivenAttachment(CreateAttachment(uploadedBy: OtherUserId));

        await Handler.Handle(new DeleteAttachmentCommand(attachment.Id, Admin, IsAdmin: true), CancellationToken.None);

        attachment.DeletedByUsername.Should().Be(Admin.Username);
    }

    [Test]
    public async Task Handle_StaffOwnFileOnAFinishedStage_ThrowsConflict()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress, finishedStages: 1));
        var attachment = GivenAttachment(AttachmentOn(procedure, stageOrder: 1));

        var act = () => Handler.Handle(new DeleteAttachmentCommand(attachment.Id, Staff, IsAdmin: false), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*finished and locked*");
        attachment.IsDeleted.Should().BeFalse();
    }

    [Test]
    public async Task Handle_AdminFileOnAFinishedStage_DeletesItAndRecordsTheStage()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.Completed, finishedStages: 3));
        var attachment = GivenAttachment(AttachmentOn(procedure, stageOrder: 1));

        await Handler.Handle(new DeleteAttachmentCommand(attachment.Id, Admin, IsAdmin: true), CancellationToken.None);

        attachment.IsDeleted.Should().BeTrue();
        SavedAudit.Single().StageId.Should().Be(procedure.StageAt(1).Id);
        SavedAudit.Single().PatientProcedureId.Should().Be(procedure.Id);
    }

    [Test]
    public async Task Handle_FileOfAProcedureThatHasGone_IsTreatedAsThePatients()
    {
        var attachment = GivenAttachment(CreateAttachment(procedureId: Guid.NewGuid()));

        await Handler.Handle(new DeleteAttachmentCommand(attachment.Id, Staff, IsAdmin: false), CancellationToken.None);

        SavedAudit.Single().PatientProcedureId.Should().Be(PatientProcedureAuditEntry.NoProcedure);
    }

    [Test]
    public async Task Handle_AlreadyDeleted_ThrowsNotFound()
    {
        var attachment = GivenAttachment(CreateAttachment(deleted: true));

        var act = () => Handler.Handle(new DeleteAttachmentCommand(attachment.Id, Admin, IsAdmin: true), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_Unknown_ThrowsNotFound()
    {
        var act = () => Handler.Handle(new DeleteAttachmentCommand(Guid.NewGuid(), Admin, IsAdmin: true), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
