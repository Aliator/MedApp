using FluentAssertions;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Attachments.Queries.GetAttachments;
using MedApp.Domain.Attachments;
using static MedApp.UnitTests.Common.Constants.AttachmentsTestConstants;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.Attachments.Queries.GetAttachments;

[TestFixture]
public sealed class AttachmentSearchTests
{
    private static readonly MedApp.Domain.Pathways.PatientPathways.PatientPathway Pathway = CreatePathway();

    private static Attachment At(Attachment attachment, int minutesAgo) => new()
    {
        Id = attachment.Id,
        PatientId = attachment.PatientId,
        PathwayId = attachment.PathwayId,
        StageId = attachment.StageId,
        FileName = attachment.FileName,
        UploadedAtUtc = Now.AddMinutes(-minutesAgo),
        DeletedAtUtc = attachment.DeletedAtUtc,
    };

    private static readonly Attachment PatientFile = At(CreateAttachment(), 50);
    private static readonly Attachment PathwayFile = At(AttachmentOn(Pathway), 40);
    private static readonly Attachment StageFile = At(AttachmentOn(Pathway, stageOrder: 2), 30);
    private static readonly Attachment DeletedFile = At(CreateAttachment(deleted: true), 20);
    private static readonly Attachment OtherPatientsFile = new() { Id = Guid.NewGuid(), PatientId = Guid.NewGuid(), UploadedAtUtc = Now };

    private static readonly IQueryable<Attachment> All =
        new[] { PatientFile, PathwayFile, StageFile, DeletedFile, OtherPatientsFile }.AsQueryable();

    private static List<AttachmentRow> Run(GetAttachmentsQuery query) =>
        AttachmentSearch.Newest(AttachmentSearch.Filter(All, query), new[] { Pathway }.AsQueryable(), Pathway.Stages.AsQueryable()).ToList();

    [Test]
    public void Patient_ListsEverythingAttachedToThemNewestFirstWithoutDeletedOnes()
    {
        var rows = Run(new GetAttachmentsQuery(PatientId, null, null, null, false, Staff.UserId, false, true));

        rows.Select(r => r.Attachment).Should().Equal(StageFile, PathwayFile, PatientFile);
        rows[0].PathwayTitle.Should().Be(PathwayTitle);
        rows[0].StageName.Should().Be(PathwayStage);
        rows[2].PathwayTitle.Should().BeNull();
    }

    [Test]
    public void Pathway_ListsOnlyItsOwnFiles()
    {
        Run(new GetAttachmentsQuery(null, Pathway.Id, null, null, false, Staff.UserId, false, true))
            .Select(r => r.Attachment).Should().Equal(PathwayFile);
    }

    [Test]
    public void Stage_ListsOnlyItsFiles()
    {
        Run(new GetAttachmentsQuery(null, null, Pathway.StageAt(2).Id, null, false, Staff.UserId, false, true))
            .Select(r => r.Attachment).Should().Equal(StageFile);
    }

    [Test]
    public void IncludeDeleted_Admin_GetsThem()
    {
        Run(new GetAttachmentsQuery(PatientId, null, null, null, true, Admin.UserId, true, true))
            .Select(r => r.Attachment).Should().Equal(DeletedFile, StageFile, PathwayFile, PatientFile);
    }

    [Test]
    public void IncludeDeleted_Staff_StillDoesNot()
    {
        Run(new GetAttachmentsQuery(PatientId, null, null, null, true, Staff.UserId, false, true))
            .Should().NotContain(r => r.Attachment == DeletedFile);
    }

    [Test]
    public void NoTarget_ListsNothing()
    {
        Run(new GetAttachmentsQuery(null, null, null, null, false, Staff.UserId, false, true)).Should().BeEmpty();
    }

    [Test]
    public void Newest_TakesAtMostTheLimit()
    {
        var many = Enumerable.Range(0, AttachmentLimits.MaxListed + 5)
            .Select(i => new Attachment { Id = Guid.NewGuid(), PatientId = PatientId, UploadedAtUtc = Now.AddMinutes(-i) })
            .AsQueryable();

        AttachmentSearch.Newest(many, Enumerable.Empty<MedApp.Domain.Pathways.PatientPathways.PatientPathway>().AsQueryable(),
                Enumerable.Empty<MedApp.Domain.Pathways.PatientPathways.PatientPathwayStage>().AsQueryable())
            .Should().HaveCount(AttachmentLimits.MaxListed);
    }
}
