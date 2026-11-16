using AutoFixture;
using FluentAssertions;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Procedures.Repositories;
using MedApp.Application.Procedures.Timeline.Common;
using MedApp.Application.Procedures.Timeline.Queries.GetPatientTimeline;
using MedApp.Domain.Attachments;
using MedApp.Domain.Patients;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using static MedApp.UnitTests.Common.Constants.TimelineTestConstants;
using Action = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureAuditAction;

namespace MedApp.UnitTests.Application.Procedures.Timeline;

/// <summary>Files on the patient timeline: what's listed, and what staff and admins see of each.</summary>
[TestFixture]
public sealed class AttachmentTimelineTests
{
    private const string SecretName = "synthetic-free-text-name.pdf";

    private static readonly PatientProcedure Procedure = CreateTimelineProcedure();

    private static PatientProcedureAuditEntry FileEntry(Action action, DateTime atUtc, PatientProcedure? procedure, PatientProcedureStage? stage = null)
    {
        var attachment = new Attachment
        {
            Id = Guid.NewGuid(),
            PatientId = PatientId,
            ProcedureId = procedure?.Id,
            StageId = stage?.Id,
            FileName = SecretName,
            ContentType = "application/pdf",
            SizeBytes = 10,
        };
        return AttachmentAudit.Entry(attachment, new AttachmentTarget(PatientId, procedure, stage), Staff, action, atUtc);
    }

    private static readonly PatientProcedureAuditEntry PatientFileAdded = FileEntry(Action.AttachmentAdded, Now.AddHours(-3), null);
    private static readonly PatientProcedureAuditEntry ProcedureFileAdded = FileEntry(Action.AttachmentAdded, Now.AddHours(-2), Procedure);
    private static readonly PatientProcedureAuditEntry ProcedureFileDownloaded = FileEntry(Action.AttachmentDownloaded, Now.AddHours(-1), Procedure);
    private static readonly PatientProcedureAuditEntry Created = CreateEntry(Action.Created, Now.AddDays(-1));

    private static List<PatientTimelineRow> Run(GetPatientTimelineQuery query) =>
        PatientTimelineSearch.Page(
            new[] { PatientFileAdded, ProcedureFileAdded, ProcedureFileDownloaded, Created }.AsQueryable(),
            new[] { Procedure }.AsQueryable(),
            query).ToList();

    [Test]
    public void Page_Staff_SeeFilesOnThePatientAndProceduresButNeverDownloads()
    {
        Run(CreateTimelineQuery()).Select(r => r.Entry).Should().Equal(ProcedureFileAdded, PatientFileAdded, Created);
    }

    [Test]
    public void Page_Admin_NeverSeeDownloadsEither()
    {
        Run(CreateTimelineQuery(isAdmin: true)).Select(r => r.Entry).Should().NotContain(ProcedureFileDownloaded);
    }

    [Test]
    public void Page_FilesKind_ReturnsOnlyFiles()
    {
        Run(CreateTimelineQuery() with { Kinds = ["Files"] }).Select(r => r.Entry).Should().Equal(ProcedureFileAdded, PatientFileAdded);
    }

    [Test]
    public void Page_PatientFile_HasNoProcedureTitleAndIsAboutThePatient()
    {
        var row = Run(CreateTimelineQuery()).Single(r => r.Entry == PatientFileAdded);

        row.CurrentProcedureTitle.Should().BeNull();
        row.IsAboutPatient.Should().BeTrue();
    }

    [Test]
    public void ToResponse_Staff_GetTheFileButNotItsName()
    {
        var response = TimelineMapper.ToResponse(new PatientTimelineRow(ProcedureFileAdded, CurrentTitle), isAdmin: false);

        response.Kind.Should().Be("Files");
        response.Action.Should().Be("AttachmentAdded");
        response.AttachedTo.Should().Be("Procedure");
        response.AttachmentId.Should().NotBeNull();
        response.FileName.Should().BeNull();
        response.ProcedureTitle.Should().Be(CurrentTitle);
    }

    [Test]
    public void ToResponse_Admin_GetItsName()
    {
        TimelineMapper.ToResponse(new PatientTimelineRow(ProcedureFileAdded, CurrentTitle), isAdmin: true)
            .FileName.Should().Be(SecretName);
    }

    [Test]
    public void ToResponse_PatientFile_IsNotADeletedProcedure()
    {
        var response = TimelineMapper.ToResponse(new PatientTimelineRow(PatientFileAdded, null), isAdmin: false);

        response.IsProcedureDeleted.Should().BeFalse();
        response.AttachedTo.Should().Be("Patient");
        response.ProcedureId.Should().Be(PatientProcedureAuditEntry.NoProcedure);
        response.ProcedureTitle.Should().BeEmpty();
    }

    [Test]
    public void ToResponse_UnreadableAttachmentId_IsNull()
    {
        var entry = CreateEntry(Action.AttachmentDeleted, Now, details: "{\"attachmentId\":\"not-a-guid\",\"attachedTo\":\"Stage\"}");

        var response = TimelineMapper.ToResponse(new PatientTimelineRow(entry, CurrentTitle), isAdmin: true);

        response.AttachmentId.Should().BeNull();
        response.AttachedTo.Should().Be("Stage");
        response.FileName.Should().BeNull();
    }

    [Test]
    public async Task Handle_Staff_KeepsPatientFilesThoughTheyHaveNoProcedureTitle()
    {
        var fixture = AutoFixtureFactory.Create();
        var patients = new Mock<IPatientRepository>();
        patients.Setup(r => r.GetByIdAsync(PatientId, It.IsAny<CancellationToken>())).ReturnsAsync(new Patient { Id = PatientId });
        var audit = new Mock<IProcedureAuditRepository>();
        var deletedProcedures = CreateEntry(Action.Created, Now.AddDays(-2), procedureId: DeletedProcedureId);
        audit.Setup(r => r.GetPatientTimelineAsync(It.IsAny<GetPatientTimelineQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PatientTimelineRow(PatientFileAdded, null), new PatientTimelineRow(deletedProcedures, null)]);
        fixture.Inject(patients.Object);
        fixture.Inject(audit.Object);

        var page = await fixture.Create<GetPatientTimelineHandler>().Handle(CreateTimelineQuery(), CancellationToken.None);

        page.Items.Select(i => i.Id).Should().Equal(PatientFileAdded.Id);
    }
}
