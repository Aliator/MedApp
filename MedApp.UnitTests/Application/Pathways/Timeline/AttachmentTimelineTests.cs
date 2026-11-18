using AutoFixture;
using FluentAssertions;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Pathways.Timeline.Common;
using MedApp.Application.Pathways.Timeline.Queries.GetPatientTimeline;
using MedApp.Domain.Attachments;
using MedApp.Domain.Patients;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using static MedApp.UnitTests.Common.Constants.TimelineTestConstants;
using Action = MedApp.Domain.Pathways.PatientPathways.PatientPathwayAuditAction;

namespace MedApp.UnitTests.Application.Pathways.Timeline;

/// <summary>Files on the patient timeline: what's listed, and what staff and admins see of each.</summary>
[TestFixture]
public sealed class AttachmentTimelineTests
{
    private const string SecretName = "synthetic-free-text-name.pdf";

    private static readonly PatientPathway Pathway = CreateTimelinePathway();

    private static PatientPathwayAuditEntry FileEntry(Action action, DateTime atUtc, PatientPathway? pathway, PatientPathwayStage? stage = null)
    {
        var attachment = new Attachment
        {
            Id = Guid.NewGuid(),
            PatientId = PatientId,
            PathwayId = pathway?.Id,
            StageId = stage?.Id,
            FileName = SecretName,
            ContentType = "application/pdf",
            SizeBytes = 10,
        };
        return AttachmentAudit.Entry(attachment, new AttachmentTarget(PatientId, pathway, stage), Staff, action, atUtc);
    }

    private static readonly PatientPathwayAuditEntry PatientFileAdded = FileEntry(Action.AttachmentAdded, Now.AddHours(-3), null);
    private static readonly PatientPathwayAuditEntry PathwayFileAdded = FileEntry(Action.AttachmentAdded, Now.AddHours(-2), Pathway);
    private static readonly PatientPathwayAuditEntry PathwayFileDownloaded = FileEntry(Action.AttachmentDownloaded, Now.AddHours(-1), Pathway);
    private static readonly PatientPathwayAuditEntry Created = CreateEntry(Action.Created, Now.AddDays(-1));

    private static List<PatientTimelineRow> Run(GetPatientTimelineQuery query) =>
        PatientTimelineSearch.Page(
            new[] { PatientFileAdded, PathwayFileAdded, PathwayFileDownloaded, Created }.AsQueryable(),
            new[] { Pathway }.AsQueryable(),
            query).ToList();

    [Test]
    public void Page_Staff_SeeFilesOnThePatientAndPathwaysButNeverDownloads()
    {
        Run(CreateTimelineQuery()).Select(r => r.Entry).Should().Equal(PathwayFileAdded, PatientFileAdded, Created);
    }

    [Test]
    public void Page_Admin_NeverSeeDownloadsEither()
    {
        Run(CreateTimelineQuery(isAdmin: true)).Select(r => r.Entry).Should().NotContain(PathwayFileDownloaded);
    }

    [Test]
    public void Page_FilesKind_ReturnsOnlyFiles()
    {
        Run(CreateTimelineQuery() with { Kinds = ["Files"] }).Select(r => r.Entry).Should().Equal(PathwayFileAdded, PatientFileAdded);
    }

    [Test]
    public void Page_PatientFile_HasNoPathwayTitleAndIsAboutThePatient()
    {
        var row = Run(CreateTimelineQuery()).Single(r => r.Entry == PatientFileAdded);

        row.CurrentPathwayTitle.Should().BeNull();
        row.IsAboutPatient.Should().BeTrue();
    }

    [Test]
    public void ToResponse_Staff_GetTheFileButNotItsName()
    {
        var response = TimelineMapper.ToResponse(new PatientTimelineRow(PathwayFileAdded, CurrentTitle), isAdmin: false);

        response.Kind.Should().Be("Files");
        response.Action.Should().Be("AttachmentAdded");
        response.AttachedTo.Should().Be("Pathway");
        response.AttachmentId.Should().NotBeNull();
        response.FileName.Should().BeNull();
        response.PathwayTitle.Should().Be(CurrentTitle);
    }

    [Test]
    public void ToResponse_Admin_GetItsName()
    {
        TimelineMapper.ToResponse(new PatientTimelineRow(PathwayFileAdded, CurrentTitle), isAdmin: true)
            .FileName.Should().Be(SecretName);
    }

    [Test]
    public void ToResponse_PatientFile_IsNotADeletedPathway()
    {
        var response = TimelineMapper.ToResponse(new PatientTimelineRow(PatientFileAdded, null), isAdmin: false);

        response.IsPathwayDeleted.Should().BeFalse();
        response.AttachedTo.Should().Be("Patient");
        response.PathwayId.Should().Be(PatientPathwayAuditEntry.NoPathway);
        response.PathwayTitle.Should().BeEmpty();
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
    public async Task Handle_Staff_KeepsPatientFilesThoughTheyHaveNoPathwayTitle()
    {
        var fixture = AutoFixtureFactory.Create();
        var patients = new Mock<IPatientRepository>();
        patients.Setup(r => r.GetByIdAsync(PatientId, It.IsAny<CancellationToken>())).ReturnsAsync(new Patient { Id = PatientId });
        var audit = new Mock<IPathwayAuditRepository>();
        var deletedPathways = CreateEntry(Action.Created, Now.AddDays(-2), pathwayId: DeletedPathwayId);
        audit.Setup(r => r.GetPatientTimelineAsync(It.IsAny<GetPatientTimelineQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PatientTimelineRow(PatientFileAdded, null), new PatientTimelineRow(deletedPathways, null)]);
        fixture.Inject(patients.Object);
        fixture.Inject(audit.Object);
        fixture.Inject(EmptyReferrals.Repository());

        var page = await fixture.Create<GetPatientTimelineHandler>().Handle(CreateTimelineQuery(), CancellationToken.None);

        page.Items.Select(i => i.Id).Should().Equal(PatientFileAdded.Id);
    }
}
