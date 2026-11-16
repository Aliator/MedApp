using FluentAssertions;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Attachments.Queries.GetAttachments;
using MedApp.Domain.Attachments;
using static MedApp.UnitTests.Common.Constants.AttachmentsTestConstants;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;

namespace MedApp.UnitTests.Application.Attachments.Queries.GetAttachments;

[TestFixture]
public sealed class AttachmentSearchTests
{
    private static readonly MedApp.Domain.Procedures.PatientProcedures.PatientProcedure Procedure = CreateProcedure();

    private static Attachment At(Attachment attachment, int minutesAgo) => new()
    {
        Id = attachment.Id,
        PatientId = attachment.PatientId,
        ProcedureId = attachment.ProcedureId,
        StageId = attachment.StageId,
        FileName = attachment.FileName,
        UploadedAtUtc = Now.AddMinutes(-minutesAgo),
        DeletedAtUtc = attachment.DeletedAtUtc,
    };

    private static readonly Attachment PatientFile = At(CreateAttachment(), 50);
    private static readonly Attachment ProcedureFile = At(AttachmentOn(Procedure), 40);
    private static readonly Attachment StageFile = At(AttachmentOn(Procedure, stageOrder: 2), 30);
    private static readonly Attachment DeletedFile = At(CreateAttachment(deleted: true), 20);
    private static readonly Attachment OtherPatientsFile = new() { Id = Guid.NewGuid(), PatientId = Guid.NewGuid(), UploadedAtUtc = Now };

    private static readonly IQueryable<Attachment> All =
        new[] { PatientFile, ProcedureFile, StageFile, DeletedFile, OtherPatientsFile }.AsQueryable();

    private static List<AttachmentRow> Run(GetAttachmentsQuery query) =>
        AttachmentSearch.Newest(AttachmentSearch.Filter(All, query), new[] { Procedure }.AsQueryable(), Procedure.Stages.AsQueryable()).ToList();

    [Test]
    public void Patient_ListsEverythingAttachedToThemNewestFirstWithoutDeletedOnes()
    {
        var rows = Run(new GetAttachmentsQuery(PatientId, null, null, false, Staff.UserId, false));

        rows.Select(r => r.Attachment).Should().Equal(StageFile, ProcedureFile, PatientFile);
        rows[0].ProcedureTitle.Should().Be(ProcedureTitle);
        rows[0].StageName.Should().Be(ProcedureStage);
        rows[2].ProcedureTitle.Should().BeNull();
    }

    [Test]
    public void Procedure_ListsOnlyItsOwnFiles()
    {
        Run(new GetAttachmentsQuery(null, Procedure.Id, null, false, Staff.UserId, false))
            .Select(r => r.Attachment).Should().Equal(ProcedureFile);
    }

    [Test]
    public void Stage_ListsOnlyItsFiles()
    {
        Run(new GetAttachmentsQuery(null, null, Procedure.StageAt(2).Id, false, Staff.UserId, false))
            .Select(r => r.Attachment).Should().Equal(StageFile);
    }

    [Test]
    public void IncludeDeleted_Admin_GetsThem()
    {
        Run(new GetAttachmentsQuery(PatientId, null, null, true, Admin.UserId, true))
            .Select(r => r.Attachment).Should().Equal(DeletedFile, StageFile, ProcedureFile, PatientFile);
    }

    [Test]
    public void IncludeDeleted_Staff_StillDoesNot()
    {
        Run(new GetAttachmentsQuery(PatientId, null, null, true, Staff.UserId, false))
            .Should().NotContain(r => r.Attachment == DeletedFile);
    }

    [Test]
    public void NoTarget_ListsNothing()
    {
        Run(new GetAttachmentsQuery(null, null, null, false, Staff.UserId, false)).Should().BeEmpty();
    }

    [Test]
    public void Newest_TakesAtMostTheLimit()
    {
        var many = Enumerable.Range(0, AttachmentLimits.MaxListed + 5)
            .Select(i => new Attachment { Id = Guid.NewGuid(), PatientId = PatientId, UploadedAtUtc = Now.AddMinutes(-i) })
            .AsQueryable();

        AttachmentSearch.Newest(many, Enumerable.Empty<MedApp.Domain.Procedures.PatientProcedures.PatientProcedure>().AsQueryable(),
                Enumerable.Empty<MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStage>().AsQueryable())
            .Should().HaveCount(AttachmentLimits.MaxListed);
    }
}
