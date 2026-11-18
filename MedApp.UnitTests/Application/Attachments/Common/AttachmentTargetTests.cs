using System.Text.Json;
using FluentAssertions;
using MedApp.Application.Attachments.Common;
using MedApp.Domain.Pathways.PatientPathways;
using static MedApp.UnitTests.Common.Constants.AttachmentsTestConstants;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

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
    public void WhyFilesCantChange_Pathway_FollowsItsDetails(Status status, bool canChange)
    {
        var pathway = CreatePathway(status);

        var reason = new AttachmentTarget(pathway.PatientId, pathway, null).WhyFilesCantChange();

        if (canChange)
            reason.Should().BeNull();
        else
            reason.Should().StartWith("Files can't be added to or removed from a pathway while it's");
    }

    [Test]
    public void WhyFilesCantChange_CurrentStage_IsNull()
    {
        var pathway = CreatePathway(Status.InProgress, finishedStages: 1);

        new AttachmentTarget(pathway.PatientId, pathway, pathway.StageAt(2)).WhyFilesCantChange().Should().BeNull();
    }

    [Test]
    public void WhyFilesCantChange_FinishedStage_SaysItIsLocked()
    {
        var pathway = CreatePathway(Status.InProgress, finishedStages: 1);

        new AttachmentTarget(pathway.PatientId, pathway, pathway.StageAt(1)).WhyFilesCantChange()
            .Should().Contain("finished and locked");
    }

    [Test]
    public void WhyFilesCantChange_LaterStage_SaysToFinishTheCurrentOne()
    {
        var pathway = CreatePathway(Status.InProgress);

        new AttachmentTarget(pathway.PatientId, pathway, pathway.StageAt(3)).WhyFilesCantChange()
            .Should().StartWith("Stages are worked in order");
    }

    [Test]
    public void Kind_FollowsWhatIsSet()
    {
        var pathway = CreatePathway();

        new AttachmentTarget(PatientId, null, null).Kind.Should().Be(AttachmentTargetKind.Patient);
        new AttachmentTarget(PatientId, pathway, null).Kind.Should().Be(AttachmentTargetKind.Pathway);
        new AttachmentTarget(PatientId, pathway, pathway.StageAt(1)).Kind.Should().Be(AttachmentTargetKind.Stage);
    }

    [Test]
    public void Entry_PatientFile_HasNoPathwayAndRecordsTheFile()
    {
        var attachment = CreateAttachment();

        var entry = AttachmentAudit.Entry(
            attachment, new AttachmentTarget(PatientId, null, null), Staff, PatientPathwayAuditAction.AttachmentAdded, Now);

        entry.PatientPathwayId.Should().Be(PatientPathwayAuditEntry.NoPathway);
        entry.PathwayTitle.Should().BeEmpty();
        entry.PatientId.Should().Be(PatientId);
        entry.StageId.Should().BeNull();
        entry.Action.Should().Be(PatientPathwayAuditAction.AttachmentAdded);
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
    public void Entry_StageFile_RecordsThePathwayAndStage()
    {
        var pathway = CreatePathway();
        var attachment = AttachmentOn(pathway, stageOrder: 1);

        var entry = AttachmentAudit.Entry(
            attachment, new AttachmentTarget(pathway.PatientId, pathway, pathway.StageAt(1)), Admin,
            PatientPathwayAuditAction.AttachmentDeleted, Now);

        entry.PatientPathwayId.Should().Be(pathway.Id);
        entry.PathwayTitle.Should().Be(PathwayTitle);
        entry.StageId.Should().Be(pathway.StageAt(1).Id);
        entry.StageName.Should().Be(ConsultationStage);
        JsonDocument.Parse(entry.Details).RootElement.GetProperty("attachedTo").GetString().Should().Be("Stage");
    }
}
