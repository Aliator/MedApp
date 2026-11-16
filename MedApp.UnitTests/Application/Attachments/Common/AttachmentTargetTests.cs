using System.Text.Json;
using FluentAssertions;
using MedApp.Application.Attachments.Common;
using MedApp.Domain.Procedures.PatientProcedures;
using static MedApp.UnitTests.Common.Constants.AttachmentsTestConstants;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;

namespace MedApp.UnitTests.Application.Attachments.Common;

[TestFixture]
public sealed class AttachmentTargetTests
{
    [Test]
    public void WhyFilesCantChange_Patient_IsAlwaysNull()
    {
        new AttachmentTarget(PatientId, null, null).WhyFilesCantChange().Should().BeNull();
    }

    [TestCase(Status.Unassigned, true)]
    [TestCase(Status.InProgress, true)]
    [TestCase(Status.Blocked, true)]
    [TestCase(Status.Completed, true)]
    [TestCase(Status.Cancelled, false)]
    [TestCase(Status.Archived, false)]
    public void WhyFilesCantChange_Procedure_FollowsItsDetails(Status status, bool canChange)
    {
        var procedure = CreateProcedure(status);

        var reason = new AttachmentTarget(procedure.PatientId, procedure, null).WhyFilesCantChange();

        if (canChange)
            reason.Should().BeNull();
        else
            reason.Should().StartWith("Files can't be added to or removed from a procedure while it's");
    }

    [Test]
    public void WhyFilesCantChange_CurrentStage_IsNull()
    {
        var procedure = CreateProcedure(Status.InProgress, finishedStages: 1);

        new AttachmentTarget(procedure.PatientId, procedure, procedure.StageAt(2)).WhyFilesCantChange().Should().BeNull();
    }

    [Test]
    public void WhyFilesCantChange_FinishedStage_SaysItIsLocked()
    {
        var procedure = CreateProcedure(Status.InProgress, finishedStages: 1);

        new AttachmentTarget(procedure.PatientId, procedure, procedure.StageAt(1)).WhyFilesCantChange()
            .Should().Contain("finished and locked");
    }

    [Test]
    public void WhyFilesCantChange_LaterStage_SaysToFinishTheCurrentOne()
    {
        var procedure = CreateProcedure(Status.InProgress);

        new AttachmentTarget(procedure.PatientId, procedure, procedure.StageAt(3)).WhyFilesCantChange()
            .Should().StartWith("Stages are worked in order");
    }

    [Test]
    public void Kind_FollowsWhatIsSet()
    {
        var procedure = CreateProcedure();

        new AttachmentTarget(PatientId, null, null).Kind.Should().Be(AttachmentTargetKind.Patient);
        new AttachmentTarget(PatientId, procedure, null).Kind.Should().Be(AttachmentTargetKind.Procedure);
        new AttachmentTarget(PatientId, procedure, procedure.StageAt(1)).Kind.Should().Be(AttachmentTargetKind.Stage);
    }

    [Test]
    public void Entry_PatientFile_HasNoProcedureAndRecordsTheFile()
    {
        var attachment = CreateAttachment();

        var entry = AttachmentAudit.Entry(
            attachment, new AttachmentTarget(PatientId, null, null), Staff, PatientProcedureAuditAction.AttachmentAdded, Now);

        entry.PatientProcedureId.Should().Be(PatientProcedureAuditEntry.NoProcedure);
        entry.ProcedureTitle.Should().BeEmpty();
        entry.PatientId.Should().Be(PatientId);
        entry.StageId.Should().BeNull();
        entry.Action.Should().Be(PatientProcedureAuditAction.AttachmentAdded);
        entry.UserId.Should().Be(Staff.UserId);
        entry.Username.Should().Be(Staff.Username);
        entry.AtUtc.Should().Be(Now);

        var details = JsonDocument.Parse(entry.Details).RootElement;
        details.GetProperty("attachmentId").GetGuid().Should().Be(attachment.Id);
        details.GetProperty("fileName").GetString().Should().Be(FileName);
        details.GetProperty("contentType").GetString().Should().Be("application/pdf");
        details.GetProperty("size").GetInt64().Should().Be(1234);
        details.GetProperty("attachedTo").GetString().Should().Be("Patient");
    }

    [Test]
    public void Entry_StageFile_RecordsTheProcedureAndStage()
    {
        var procedure = CreateProcedure();
        var attachment = AttachmentOn(procedure, stageOrder: 1);

        var entry = AttachmentAudit.Entry(
            attachment, new AttachmentTarget(procedure.PatientId, procedure, procedure.StageAt(1)), Admin,
            PatientProcedureAuditAction.AttachmentDeleted, Now);

        entry.PatientProcedureId.Should().Be(procedure.Id);
        entry.ProcedureTitle.Should().Be(ProcedureTitle);
        entry.StageId.Should().Be(procedure.StageAt(1).Id);
        entry.StageName.Should().Be(ConsultationStage);
        JsonDocument.Parse(entry.Details).RootElement.GetProperty("attachedTo").GetString().Should().Be("Stage");
    }
}
