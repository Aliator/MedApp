using AutoFixture;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Attachments.Repositories;
using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Specimens.Repositories;
using MedApp.Domain.Appointments;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Constants;
using Moq;

namespace MedApp.UnitTests.Common.Fixtures;

/// <summary>
/// Shared setup for pathway handler tests: a mocked repository that records the audit entries it's given (with
/// either kind of save), a mocked appointment repository with no appointments, a username lookup backed by
/// <see cref="PathwaysTestConstants.Usernames"/>, and a clock fixed at <see cref="PathwaysTestConstants.Now"/>.
/// </summary>
public abstract class PathwayHandlerTestBase
{
    private readonly List<CancellationToken> _saveTokens = [];

    protected IFixture Fixture { get; private set; } = null!;
    protected Mock<IPatientPathwayRepository> Repository { get; private set; } = null!;
    protected Mock<IAppointmentRepository> Appointments { get; private set; } = null!;
    protected Mock<IIdentityReadService> Identity { get; private set; } = null!;

    /// <summary>Attached files: none, unless a test says otherwise.</summary>
    protected Mock<IAttachmentRepository> Attachments { get; private set; } = null!;

    /// <summary>Consent records: none, unless a test says otherwise (a loose mock: no records, no scans, a count of 0).</summary>
    protected Mock<MedApp.Application.Consent.Repositories.IConsentRepository> Consents { get; private set; } = null!;

    /// <summary>Specimens: none, unless a test says otherwise.</summary>
    protected Mock<ISpecimenRepository> Specimens { get; private set; } = null!;

    /// <summary>Results: none awaiting sign-off, unless a test says otherwise.</summary>
    protected Mock<MedApp.Application.Results.Repositories.IResultRepository> Results { get; private set; } = null!;

    /// <summary>Audit entries passed to AddAsync, SaveAsync or DeleteAsync.</summary>
    protected List<PatientPathwayAuditEntry> SavedAudit { get; } = [];

    /// <summary>Pathway changes passed to AddAsync or SaveAsync, with the appointments and history they bring.</summary>
    protected List<PathwayChange> SavedChanges { get; } = [];

    /// <summary>The pathway's letters (none by default).</summary>
    protected Mock<MedApp.Application.Letters.Repositories.ILetterRepository> Letters { get; private set; } = null!;

    [SetUp]
    public void SetUpPathwayMocks()
    {
        Fixture = AutoFixtureFactory.Create();
        Repository = new Mock<IPatientPathwayRepository>();
        Appointments = new Mock<IAppointmentRepository>();
        Identity = new Mock<IIdentityReadService>();
        Attachments = new Mock<IAttachmentRepository>();
        Specimens = new Mock<ISpecimenRepository>();
        Specimens
            .Setup(s => s.GetOpenForPathwayAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        Results = new Mock<MedApp.Application.Results.Repositories.IResultRepository>();
        Results
            .Setup(r => r.GetAwaitingForPathwayAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        SavedAudit.Clear();
        SavedChanges.Clear();
        _saveTokens.Clear();

        Identity
            .Setup(i => i.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                (IReadOnlyDictionary<Guid, string>)PathwaysTestConstants.Usernames
                    .Where(kv => ids.Contains(kv.Key))
                    .ToDictionary());

        Repository
            .Setup(r => r.AddAsync(It.IsAny<PatientPathway>(), It.IsAny<IEnumerable<PatientPathwayAuditEntry>>(), It.IsAny<CancellationToken>()))
            .Callback((PatientPathway _, IEnumerable<PatientPathwayAuditEntry> audit, CancellationToken _) => SavedAudit.AddRange(audit))
            .Returns(Task.CompletedTask);
        Repository
            .Setup(r => r.AddAsync(It.IsAny<PathwayChange>(), It.IsAny<CancellationToken>()))
            .Callback((PathwayChange change, CancellationToken _) => Recorded(change))
            .Returns(Task.CompletedTask);
        Repository
            .Setup(r => r.SaveAsync(It.IsAny<IEnumerable<PatientPathwayAuditEntry>>(), It.IsAny<CancellationToken>()))
            .Callback((IEnumerable<PatientPathwayAuditEntry> audit, CancellationToken token) =>
            {
                SavedAudit.AddRange(audit);
                _saveTokens.Add(token);
            })
            .Returns(Task.CompletedTask);
        Repository
            .Setup(r => r.SaveAsync(It.IsAny<PathwayChange>(), It.IsAny<CancellationToken>()))
            .Callback((PathwayChange change, CancellationToken token) =>
            {
                Recorded(change);
                _saveTokens.Add(token);
            })
            .Returns(Task.CompletedTask);
        Repository
            .Setup(r => r.DeleteAsync(It.IsAny<PatientPathway>(), It.IsAny<IEnumerable<PatientPathwayAuditEntry>>(), It.IsAny<CancellationToken>()))
            .Callback((PatientPathway _, IEnumerable<PatientPathwayAuditEntry> audit, CancellationToken _) => SavedAudit.AddRange(audit))
            .Returns(Task.CompletedTask);

        Appointments
            .Setup(r => r.GetForStagesForUpdateAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        Appointments
            .Setup(r => r.GetRequestedForPathwayForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        Fixture.Inject(Repository.Object);
        Fixture.Inject(Appointments.Object);
        Fixture.Inject(Identity.Object);
        Fixture.Inject(Attachments.Object);
        Fixture.Inject(Specimens.Object);
        Fixture.Inject(Results.Object);
        Consents = new Mock<MedApp.Application.Consent.Repositories.IConsentRepository>();
        Consents.Setup(c => c.GetForStageAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        Consents.Setup(c => c.GetForPathwayAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        Fixture.Inject(Consents.Object);
        // No letters, unless a test says so: none stops a pathway's deletion or a stage's finish.
        Letters = new Mock<MedApp.Application.Letters.Repositories.ILetterRepository>();
        Letters.Setup(l => l.GetForPathwayAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        Letters.Setup(l => l.CountForPathwayAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
        Fixture.Inject(Letters.Object);
        Fixture.Inject<TimeProvider>(new FixedTimeProvider(PathwaysTestConstants.Now));
    }

    /// <summary>Makes the repository return this pathway, for reading and for updating.</summary>
    protected PatientPathway GivenPathway(PatientPathway pathway)
    {
        Repository.Setup(r => r.GetForUpdateAsync(pathway.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pathway);
        Repository.Setup(r => r.GetByIdAsync(pathway.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pathway);
        return pathway;
    }

    /// <summary>Makes the appointment repository return these appointments, for the stages asked about.</summary>
    protected void GivenStageAppointments(params Appointment[] appointments) =>
        Appointments
            .Setup(r => r.GetForStagesForUpdateAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> stageIds, CancellationToken _) =>
                (IReadOnlyList<Appointment>)appointments.Where(a => a.StageId is { } id && stageIds.Contains(id)).ToList());

    /// <summary>Saved once (with either kind of save), with this token if one is given.</summary>
    protected void VerifySavedOnce(CancellationToken? token = null)
    {
        Assert.That(_saveTokens, Has.Count.EqualTo(1), "The pathway should have been saved once.");
        if (token is { } expected)
            Assert.That(_saveTokens[0], Is.EqualTo(expected));
    }

    protected void VerifyNotSaved() =>
        Assert.That(_saveTokens, Is.Empty, "The pathway should not have been saved.");

    private void Recorded(PathwayChange change)
    {
        SavedAudit.AddRange(change.AuditEntries);
        SavedChanges.Add(change);
    }
}
