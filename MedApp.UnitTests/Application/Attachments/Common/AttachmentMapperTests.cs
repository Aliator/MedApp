using FluentAssertions;
using MedApp.Application.Attachments.Common;
using MedApp.Domain.Attachments;
using static MedApp.UnitTests.Common.Constants.AttachmentsTestConstants;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;

namespace MedApp.UnitTests.Application.Attachments.Common;

[TestFixture]
public sealed class AttachmentMapperTests
{
    [Test]
    public void ToResponse_StageFile_MapsEveryField()
    {
        var procedure = CreateProcedure();
        var attachment = AttachmentOn(procedure, stageOrder: 2);

        var response = AttachmentMapper.ToResponse(new AttachmentRow(attachment, ProcedureTitle, ProcedureStage), Staff.UserId, isAdmin: false);

        response.Should().BeEquivalentTo(new
        {
            attachment.Id,
            attachment.PatientId,
            AttachedTo = "Stage",
            ProcedureId = procedure.Id,
            ProcedureTitle,
            StageId = procedure.StageAt(2).Id,
            StageName = ProcedureStage,
            attachment.FileName,
            attachment.ContentType,
            TypeName = "PDF",
            attachment.SizeBytes,
            attachment.Sha256,
            ScanStatus = "NotScanned",
            HasThumbnail = false,
            attachment.UploadedByUsername,
            attachment.UploadedAtUtc,
            CanDelete = true,
            IsDeleted = false,
            DeletedAtUtc = (DateTime?)null,
            DeletedByUsername = (string?)null,
        });
    }

    [Test]
    public void ToResponse_PatientFile_HasNoProcedureOrStage()
    {
        var response = AttachmentMapper.ToResponse(new AttachmentRow(CreateAttachment(), "stray", "stray"), Staff.UserId, false);

        response.AttachedTo.Should().Be("Patient");
        response.ProcedureId.Should().BeNull();
        response.ProcedureTitle.Should().BeNull();
        response.StageName.Should().BeNull();
    }

    [Test]
    public void ToResponse_ProcedureFile_HasItsTitleButNoStage()
    {
        var procedure = CreateProcedure();
        var target = new AttachmentTarget(procedure.PatientId, procedure, null);

        var response = AttachmentMapper.ToResponse(AttachmentOn(procedure), target, Staff.UserId, false);

        response.AttachedTo.Should().Be("Procedure");
        response.ProcedureTitle.Should().Be(ProcedureTitle);
        response.StageId.Should().BeNull();
    }

    [Test]
    public void ToResponse_DeletedFile_HasNoThumbnailAndCantBeDeleted()
    {
        var attachment = CreateAttachment(deleted: true, hasThumbnail: true);

        var response = AttachmentMapper.ToResponse(new AttachmentRow(attachment, null, null), Admin.UserId, isAdmin: true);

        response.IsDeleted.Should().BeTrue();
        response.DeletedByUsername.Should().Be(Admin.Username);
        response.DeletedAtUtc.Should().Be(attachment.DeletedAtUtc);
        response.HasThumbnail.Should().BeFalse();
        response.CanDelete.Should().BeFalse();
    }

    [Test]
    public void ToResponse_UnknownContentType_UsesItAsTheTypeName()
    {
        var response = AttachmentMapper.ToResponse(new AttachmentRow(CreateAttachment(contentType: "image/gif"), null, null), Staff.UserId, false);

        response.TypeName.Should().Be("image/gif");
    }

    [Test]
    public void ToResponse_ScanStatus_ByName()
    {
        var attachment = new Attachment { ScanStatus = AttachmentScanStatus.Clean, ContentType = "text/plain; charset=utf-8" };

        var response = AttachmentMapper.ToResponse(new AttachmentRow(attachment, null, null), Staff.UserId, false);

        response.ScanStatus.Should().Be("Clean");
        response.TypeName.Should().Be("Text");
    }

    [TestCase(true, false, true)]
    [TestCase(false, false, false)]
    [TestCase(false, true, true)]
    public void CanDelete_OwnFileOrAdmin(bool own, bool isAdmin, bool expected)
    {
        var attachment = CreateAttachment(uploadedBy: own ? Staff.UserId : OtherUserId);

        AttachmentMapper.CanDelete(attachment, Staff.UserId, isAdmin).Should().Be(expected);
    }

    [Test]
    public void ToListResponse_CarriesTheItemsTotalAndLimits()
    {
        var rows = new[] { new AttachmentRow(CreateAttachment(), null, null), new AttachmentRow(CreateAttachment(), null, null) };

        var response = AttachmentMapper.ToListResponse(rows, 250, Staff.UserId, false);

        response.Items.Should().HaveCount(2);
        response.TotalCount.Should().Be(250);
        response.MaxFileBytes.Should().Be(20 * 1024 * 1024);
        response.AllowedExtensions.Should().Equal(".pdf", ".png", ".jpg", ".jpeg", ".heic", ".heif", ".docx", ".txt");
    }
}
