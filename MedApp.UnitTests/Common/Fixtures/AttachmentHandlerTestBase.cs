using AutoFixture;
using MedApp.Application.Activity.Common;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Domain.Attachments;
using MedApp.Domain.Patients;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Constants;
using Moq;

namespace MedApp.UnitTests.Common.Fixtures;

/// <summary>
/// Shared setup for attachment handler tests, on top of the pathway mocks: a patient repository, an in-memory file
/// store, staging in memory, a scanner that scans nothing, and a thumbnail generator that makes none, unless a test
/// says otherwise. Audit entries the attachment repository is given are in <see cref="PathwayHandlerTestBase.SavedAudit"/>.
/// </summary>
public abstract class AttachmentHandlerTestBase : PathwayHandlerTestBase
{
    protected Mock<IPatientRepository> Patients { get; private set; } = null!;
    protected InMemoryFileStore Store { get; private set; } = null!;
    protected Mock<IMalwareScanner> Scanner { get; private set; } = null!;
    protected Mock<IThumbnailGenerator> Thumbnails { get; private set; } = null!;

    /// <summary>What the handlers record in the activity log.</summary>
    protected RecordingActivityLog Activity { get; private set; } = null!;

    /// <summary>The attachments the repository was asked to add.</summary>
    protected List<Attachment> Added { get; } = [];

    [SetUp]
    public void SetUpAttachmentMocks()
    {
        Patients = new Mock<IPatientRepository>();
        Store = new InMemoryFileStore();
        Scanner = new Mock<IMalwareScanner>();
        Thumbnails = new Mock<IThumbnailGenerator>();
        Activity = new RecordingActivityLog();
        Added.Clear();

        Scanner.Setup(s => s.ScanAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>())).ReturnsAsync(AttachmentScanStatus.NotScanned);
        Thumbnails
            .Setup(t => t.CreateAsync(It.IsAny<Stream>(), It.IsAny<AttachmentFileType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        Attachments
            .Setup(a => a.AddAsync(It.IsAny<Attachment>(), It.IsAny<PatientPathwayAuditEntry>(), It.IsAny<CancellationToken>()))
            .Callback((Attachment attachment, PatientPathwayAuditEntry audit, CancellationToken _) =>
            {
                Added.Add(attachment);
                SavedAudit.Add(audit);
            })
            .Returns(Task.CompletedTask);
        Attachments
            .Setup(a => a.SaveAsync(It.IsAny<PatientPathwayAuditEntry>(), It.IsAny<CancellationToken>()))
            .Callback((PatientPathwayAuditEntry audit, CancellationToken _) => SavedAudit.Add(audit))
            .Returns(Task.CompletedTask);
        Attachments
            .Setup(a => a.AddAuditAsync(It.IsAny<PatientPathwayAuditEntry>(), It.IsAny<CancellationToken>()))
            .Callback((PatientPathwayAuditEntry audit, CancellationToken _) => SavedAudit.Add(audit))
            .Returns(Task.CompletedTask);

        Fixture.Inject(Patients.Object);
        Fixture.Inject<IFileStore>(Store);
        // No file is a letter's PDF unless a test says so.
        var letters = new Mock<MedApp.Application.Letters.Repositories.ILetterRepository>();
        letters.Setup(l => l.IsPdfAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        Fixture.Inject(letters.Object);
        Fixture.Inject<IUploadStaging>(new MemoryUploadStaging());
        Fixture.Inject(Scanner.Object);
        Fixture.Inject(Thumbnails.Object);
        Fixture.Inject<IActivityLog>(Activity);
    }

    /// <summary>Makes the patient repository find this patient.</summary>
    protected Patient GivenPatient(Guid? id = null)
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        var withId = new Patient
        {
            Id = id ?? patient.Id,
            FirstName = patient.FirstName,
            LastName = patient.LastName,
            DateOfBirth = patient.DateOfBirth,
            Email = patient.Email,
        };
        Patients.Setup(p => p.GetByIdAsync(withId.Id, It.IsAny<CancellationToken>())).ReturnsAsync(withId);
        return withId;
    }

    /// <summary>Makes the stage lookup find the stage at <paramref name="order"/> on this pathway.</summary>
    protected PatientPathwayStage GivenStage(PatientPathway pathway, int order)
    {
        GivenPathway(pathway);
        var stage = pathway.StageAt(order);
        Attachments.Setup(a => a.GetStagePathwayIdAsync(stage.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pathway.Id);
        return stage;
    }

    /// <summary>Makes the attachment repository find this attachment, for reading and for updating.</summary>
    protected Attachment GivenAttachment(Attachment attachment)
    {
        Attachments.Setup(a => a.GetByIdAsync(attachment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(attachment);
        Attachments.Setup(a => a.GetForUpdateAsync(attachment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(attachment);
        return attachment;
    }

    private sealed class MemoryUploadStaging : IUploadStaging
    {
        public Stream Create() => new MemoryStream();
    }
}

/// <summary>A file store in memory, recording what's saved and deleted. It can be made to say it isn't set up.</summary>
public sealed class InMemoryFileStore : IFileStore
{
    public Dictionary<string, (byte[] Bytes, string ContentType)> Blobs { get; } = [];
    public List<string> Deleted { get; } = [];

    public bool IsConfigured { get; set; } = true;

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        using var copy = new MemoryStream();
        await content.CopyToAsync(copy, ct);
        Blobs.Add(key, (copy.ToArray(), contentType));
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct) =>
        Task.FromResult<Stream?>(Blobs.TryGetValue(key, out var blob) ? new MemoryStream(blob.Bytes, writable: false) : null);

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        Deleted.Add(key);
        Blobs.Remove(key);
        return Task.CompletedTask;
    }
}
