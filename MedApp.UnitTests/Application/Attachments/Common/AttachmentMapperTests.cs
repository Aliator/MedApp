using FluentAssertions;
using MedApp.Application.Attachments.Common;
using MedApp.Domain.Attachments;
using static MedApp.UnitTests.Common.Constants.AttachmentsTestConstants;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.Attachments.Common;

[TestFixture]
public sealed class AttachmentMapperTests
{
    [Test]
    public void ToResponse_StageFile_MapsEveryField()
    {
        var pathway = CreatePathway();
        var attachment = AttachmentOn(pathway, stageOrder: 2);

        var response = AttachmentMapper.ToResponse(new AttachmentRow(attachment, PathwayTitle, PathwayStage), Staff.UserId, canCorrectRecords: false, canAttachFiles: true);

        response.Should().BeEquivalentTo(new
        {
            attachment.Id,
            attachment.PatientId,
            AttachedTo = "Stage",
            PathwayId = pathway.Id,
            PathwayTitle,
            StageId = pathway.StageAt(2).Id,
            StageName = PathwayStage,
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
    public void ToResponse_PatientFile_HasNoPathwayOrStage()
    {
        var response = AttachmentMapper.ToResponse(new AttachmentRow(CreateAttachment(), "stray", "stray"), Staff.UserId, false, true);

        response.AttachedTo.Should().Be("Patient");
        response.PathwayId.Should().BeNull();
        response.PathwayTitle.Should().BeNull();
        response.StageName.Should().BeNull();
    }

    [Test]
    public void ToResponse_PathwayFile_HasItsTitleButNoStage()
    {
        var pathway = CreatePathway();
        var target = new AttachmentTarget(pathway.PatientId, pathway, null);

        var response = AttachmentMapper.ToResponse(AttachmentOn(pathway), target, Staff.UserId, false, true);

        response.AttachedTo.Should().Be("Pathway");
        response.PathwayTitle.Should().Be(PathwayTitle);
        response.StageId.Should().BeNull();
    }

    [Test]
    public void ToResponse_DeletedFile_HasNoThumbnailAndCantBeDeleted()
    {
        var attachment = CreateAttachment(deleted: true, hasThumbnail: true);

        var response = AttachmentMapper.ToResponse(new AttachmentRow(attachment, null, null), Admin.UserId, canCorrectRecords: true, canAttachFiles: true);

        response.IsDeleted.Should().BeTrue();
        response.DeletedByUsername.Should().Be(Admin.Username);
        response.DeletedAtUtc.Should().Be(attachment.DeletedAtUtc);
        response.HasThumbnail.Should().BeFalse();
        response.CanDelete.Should().BeFalse();
    }

    [Test]
    public void ToResponse_UnknownContentType_UsesItAsTheTypeName()
    {
        var response = AttachmentMapper.ToResponse(new AttachmentRow(CreateAttachment(contentType: "image/gif"), null, null), Staff.UserId, false, true);

        response.TypeName.Should().Be("image/gif");
    }

    [Test]
    public void ToResponse_ScanStatus_ByName()
    {
        var attachment = new Attachment { ScanStatus = AttachmentScanStatus.Clean, ContentType = "text/plain; charset=utf-8" };

        var response = AttachmentMapper.ToResponse(new AttachmentRow(attachment, null, null), Staff.UserId, false, true);

        response.ScanStatus.Should().Be("Clean");
        response.TypeName.Should().Be("Text");
    }

    [TestCase(true, false, true, true)]
    [TestCase(false, false, true, false)]
    [TestCase(false, true, true, true)]
    [TestCase(true, false, false, false)]
    [TestCase(false, true, false, false)]
    public void CanDelete_OwnFileOrCorrectRecords_WhileTheyCanAttachFiles(
        bool own, bool canCorrectRecords, bool canAttachFiles, bool expected)
    {
        var attachment = CreateAttachment(uploadedBy: own ? Staff.UserId : OtherUserId);

        AttachmentMapper.CanDelete(attachment, Staff.UserId, canCorrectRecords, canAttachFiles).Should().Be(expected);
    }

    [Test]
    public void ToListResponse_ViewerWithoutAttachFiles_CantDeleteTheirOwnFiles()
    {
        var rows = new[] { new AttachmentRow(CreateAttachment(uploadedBy: Staff.UserId), null, null) };

        var response = AttachmentMapper.ToListResponse(rows, 1, Staff.UserId, canCorrectRecords: false, canAttachFiles: false);

        response.Items.Single().CanDelete.Should().BeFalse();
    }

    [Test]
    public void ToListResponse_CarriesTheItemsTotalAndLimits()
    {
        var rows = new[] { new AttachmentRow(CreateAttachment(), null, null), new AttachmentRow(CreateAttachment(), null, null) };

        var response = AttachmentMapper.ToListResponse(rows, 250, Staff.UserId, false, true);

        response.Items.Should().HaveCount(2);
        response.TotalCount.Should().Be(250);
        response.MaxFileBytes.Should().Be(20 * 1024 * 1024);
        response.AllowedExtensions.Should().Equal(".pdf", ".png", ".jpg", ".jpeg", ".heic", ".heif", ".docx", ".txt");
    }
}
