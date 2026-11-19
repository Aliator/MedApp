using MedApp.Application.Common.Identity;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Specimens.Common;
using MedApp.Application.Specimens.Repositories;
using MedApp.Domain.Attachments;
using MedApp.Domain.Notifications;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Specimens;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.Extensions.Options;
using Moq;

namespace MedApp.UnitTests.Application.Specimens;

/// <summary>
/// Shared mocks for the specimen tests: a repository that records what it's asked to save (and knows the setup lists
/// of <see cref="SpecimensTestConstants"/>), a username lookup, a QR renderer, permission holders, a notification
/// sender that keeps what it's given, and a clock fixed at <see cref="PathwaysTestConstants.Now"/>.
/// </summary>
public abstract class SpecimenTestBase
{
    protected Mock<ISpecimenRepository> Repository { get; private set; } = null!;
    protected Mock<IPatientPathwayRepository> Pathways { get; private set; } = null!;
    protected Mock<IIdentityReadService> Identity { get; private set; } = null!;
    protected Mock<IQrCodeRenderer> QrCodes { get; private set; } = null!;
    protected Mock<IPermissionHolders> Holders { get; private set; } = null!;
    protected Mock<INotificationSender> Notifications { get; private set; } = null!;

    /// <summary>Teams: the lab is <see cref="Lab"/>.</summary>
    protected Mock<MedApp.Application.Teams.Repositories.ITeamRepository> Teams { get; private set; } = null!;

    /// <summary>Access grants: none yet, unless a test says so.</summary>
    protected Mock<MedApp.Application.Teams.Repositories.IPathwayAccessRepository> Access { get; private set; } = null!;

    /// <summary>The lab team.</summary>
    protected static readonly MedApp.Domain.Teams.Team Lab = new()
    {
        Id = MedApp.Domain.Teams.Team.LabId,
        Name = "Synthetic lab",
        Kind = MedApp.Domain.Teams.TeamKind.Lab,
    };
    protected FixedTimeProvider Clock { get; } = new(PathwaysTestConstants.Now);
    protected IOptions<SpecimenOptions> Options { get; } = Microsoft.Extensions.Options.Options.Create(new SpecimenOptions());

    protected List<Specimen> Added { get; } = [];
    protected List<PatientPathwayAuditEntry> SavedAudit { get; } = [];

    /// <summary>Access grants passed to AddAsync (the lab's, once passed the pathway).</summary>
    protected List<MedApp.Domain.Teams.PathwayAccess> SavedGrants { get; } = [];
    protected List<Attachment> SavedReports { get; } = [];
    protected List<MedApp.Domain.Results.Result> SavedResults { get; } = [];
    protected List<Notification> Sent { get; } = [];
    protected List<CancellationToken> Tokens { get; } = [];
    protected CancellationTokenSource Cts { get; private set; } = null!;

    protected SpecimenType Biopsy { get; private set; } = null!;
    protected MedApp.Domain.Lab.LabTest Histology { get; private set; } = null!;

    /// <summary>The team walls at one record: seen (the tests' actors are the system's, which sees every team).</summary>
    protected Mock<MedApp.Application.Common.Teams.ITeamBoundary> Boundary { get; private set; } = null!;

    /// <summary>Lab cases: none open, unless a test says so; what's tracked is kept.</summary>
    protected Mock<MedApp.Application.Lab.Repositories.ILabCaseRepository> LabCases { get; private set; } = null!;

    protected List<MedApp.Application.Lab.Common.LabCaseChange> TrackedCases { get; } = [];

    protected MedApp.Application.Lab.Common.LabReceipt Receipt => new(LabCases.Object, Repository.Object, Options);

    [SetUp]
    public void SetUpSpecimenMocks()
    {
        Cts = new CancellationTokenSource();
        Added.Clear();
        SavedAudit.Clear();
        SavedGrants.Clear();
        Teams = new Mock<MedApp.Application.Teams.Repositories.ITeamRepository>();
        Teams.Setup(t => t.GetLabAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Lab);
        Access = new Mock<MedApp.Application.Teams.Repositories.IPathwayAccessRepository>();
        SavedReports.Clear();
        SavedResults.Clear();
        Sent.Clear();
        Tokens.Clear();
        Biopsy = SpecimensTestConstants.Biopsy();
        Histology = SpecimensTestConstants.Histology();
        TrackedCases.Clear();
        Boundary = new Mock<MedApp.Application.Common.Teams.ITeamBoundary>();
        LabCases = new Mock<MedApp.Application.Lab.Repositories.ILabCaseRepository>();
        LabCases.Setup(l => l.GetOpenForStagesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        LabCases.Setup(l => l.GetTestAsync(Histology.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Histology);
        LabCases.Setup(l => l.Track(It.IsAny<MedApp.Application.Lab.Common.LabCaseChange>(), It.IsAny<bool>()))
            .Callback((MedApp.Application.Lab.Common.LabCaseChange change, bool _) =>
            {
                TrackedCases.Add(change);
                SavedAudit.AddRange(change.AuditEntries);
            });
        LabCases.Setup(l => l.Track(It.IsAny<MedApp.Domain.Results.Result?>(), It.IsAny<IEnumerable<PatientPathwayAuditEntry>>()))
            .Callback((MedApp.Domain.Results.Result? result, IEnumerable<PatientPathwayAuditEntry> audit) =>
            {
                if (result is not null)
                    SavedResults.Add(result);
                SavedAudit.AddRange(audit);
            });
        LabCases.Setup(l => l.SaveAsync(It.IsAny<CancellationToken>()))
            .Callback((CancellationToken ct) => Tokens.Add(ct))
            .Returns(Task.CompletedTask);

        Repository = new Mock<ISpecimenRepository>();
        Repository.Setup(r => r.NextNumberAsync(It.IsAny<CancellationToken>())).ReturnsAsync(123);
        Repository.Setup(r => r.GetTypeAsync(Biopsy.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Biopsy);
        Repository.Setup(r => r.GetLabTestAsync(Histology.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Histology);
        Repository.Setup(r => r.GetReceivedWithoutCaseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        Repository.Setup(r => r.GetForStageAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        Repository
            .Setup(r => r.AddAsync(It.IsAny<Specimen>(), It.IsAny<IEnumerable<PatientPathwayAuditEntry>>(), It.IsAny<CancellationToken>()))
            .Callback((Specimen specimen, IEnumerable<PatientPathwayAuditEntry> audit, CancellationToken ct) =>
            {
                Added.Add(specimen);
                SavedAudit.AddRange(audit);
                Tokens.Add(ct);
            })
            .Returns(Task.CompletedTask);
        Repository
            .Setup(r => r.AddAsync(
                It.IsAny<Specimen>(), It.IsAny<IEnumerable<PatientPathwayAuditEntry>>(),
                It.IsAny<IEnumerable<MedApp.Domain.Teams.PathwayAccess>>(), It.IsAny<CancellationToken>()))
            .Callback((Specimen specimen, IEnumerable<PatientPathwayAuditEntry> audit,
                IEnumerable<MedApp.Domain.Teams.PathwayAccess> grants, CancellationToken ct) =>
            {
                Added.Add(specimen);
                SavedAudit.AddRange(audit);
                SavedGrants.AddRange(grants);
                Tokens.Add(ct);
            })
            .Returns(Task.CompletedTask);
        Repository
            .Setup(r => r.SaveAsync(It.IsAny<IEnumerable<PatientPathwayAuditEntry>>(), It.IsAny<CancellationToken>()))
            .Callback((IEnumerable<PatientPathwayAuditEntry> audit, CancellationToken ct) =>
            {
                SavedAudit.AddRange(audit);
                Tokens.Add(ct);
            })
            .Returns(Task.CompletedTask);
        Repository
            .Setup(r => r.SaveReportAsync(It.IsAny<Attachment>(), It.IsAny<MedApp.Domain.Results.Result>(), It.IsAny<IEnumerable<PatientPathwayAuditEntry>>(), It.IsAny<CancellationToken>()))
            .Callback((Attachment report, MedApp.Domain.Results.Result result, IEnumerable<PatientPathwayAuditEntry> audit, CancellationToken ct) =>
            {
                SavedReports.Add(report);
                SavedResults.Add(result);
                SavedAudit.AddRange(audit);
                Tokens.Add(ct);
            })
            .Returns(Task.CompletedTask);

        Pathways = new Mock<IPatientPathwayRepository>();

        Identity = new Mock<IIdentityReadService>();
        Identity
            .Setup(i => i.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                (IReadOnlyDictionary<Guid, string>)PathwaysTestConstants.Usernames
                    .Where(kv => ids.Contains(kv.Key))
                    .ToDictionary());

        QrCodes = new Mock<IQrCodeRenderer>();
        QrCodes.Setup(q => q.Render(It.IsAny<string>())).Returns(new QrCodeImage("M0 0h1v1H0z", 29));

        // Staff and Admin can sign off results (so can ask for a specimen); nobody else.
        Holders = new Mock<IPermissionHolders>();
        Holders
            .Setup(h => h.WhoHoldsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), SpecimenChoices.RequestingClinicianPermission, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids, Permission _, CancellationToken _) =>
                (IReadOnlySet<Guid>)ids.Where(id => id == PathwaysTestConstants.Staff.UserId || id == PathwaysTestConstants.Admin.UserId).ToHashSet());

        Notifications = new Mock<INotificationSender>();
        Notifications
            .Setup(n => n.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback((Notification notification, CancellationToken _) => Sent.Add(notification))
            .Returns(Task.CompletedTask);
    }

    [TearDown]
    public void TearDownSpecimenMocks() => Cts.Dispose();

    /// <summary>Makes the repository return this specimen, for reading and for updating.</summary>
    protected Specimen Given(Specimen specimen)
    {
        Repository.Setup(r => r.GetForUpdateAsync(specimen.Id, It.IsAny<CancellationToken>())).ReturnsAsync(specimen);
        Repository.Setup(r => r.GetByIdAsync(specimen.Id, It.IsAny<CancellationToken>())).ReturnsAsync(specimen);
        Repository.Setup(r => r.GetByNumberAsync(specimen.Number, It.IsAny<CancellationToken>())).ReturnsAsync(specimen);
        if (specimen.Pathway is { } pathway)
            Pathways.Setup(p => p.GetForUpdateAsync(pathway.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pathway);
        return specimen;
    }

    protected PatientPathway GivenPathway(PatientPathway pathway)
    {
        Pathways.Setup(p => p.GetByIdAsync(pathway.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pathway);
        return pathway;
    }
}
