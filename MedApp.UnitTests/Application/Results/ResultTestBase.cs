using MedApp.Application.Common.Identity;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Results.Repositories;
using MedApp.Domain.Attachments;
using MedApp.Domain.Notifications;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Results;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;

namespace MedApp.UnitTests.Application.Results;

/// <summary>
/// Shared mocks for the results tests: a repository that records what it's asked to save, a username lookup, permission
/// holders (<see cref="ResultsTestConstants.Clinician"/> and <see cref="ResultsTestConstants.OtherClinician"/> sign off
/// results; nobody else), a notification sender that keeps what it's given, and a fixed clock.
/// </summary>
public abstract class ResultTestBase
{
    protected Mock<IResultRepository> Repository { get; private set; } = null!;
    protected Mock<IIdentityReadService> Identity { get; private set; } = null!;
    protected Mock<IPermissionHolders> Holders { get; private set; } = null!;
    protected Mock<INotificationSender> Notifications { get; private set; } = null!;
    protected FixedTimeProvider Clock { get; } = new(PathwaysTestConstants.Now);

    protected List<Result> Added { get; } = [];
    protected List<PatientPathwayAuditEntry> SavedAudit { get; } = [];
    protected List<Notification> Sent { get; } = [];
    protected List<CancellationToken> Tokens { get; } = [];
    protected CancellationTokenSource Cts { get; private set; } = null!;

    protected static readonly Guid[] SignOffHolders =
        [ResultsTestConstants.Clinician.UserId, ResultsTestConstants.OtherClinician.UserId];

    [SetUp]
    public void SetUpResultMocks()
    {
        Cts = new CancellationTokenSource();
        Added.Clear();
        SavedAudit.Clear();
        Sent.Clear();
        Tokens.Clear();

        Repository = new Mock<IResultRepository>();
        Repository
            .Setup(r => r.AddAsync(It.IsAny<Result>(), It.IsAny<IEnumerable<PatientPathwayAuditEntry>>(), It.IsAny<CancellationToken>()))
            .Callback((Result result, IEnumerable<PatientPathwayAuditEntry> audit, CancellationToken ct) =>
            {
                Added.Add(result);
                SavedAudit.AddRange(audit);
                Tokens.Add(ct);
                Repository.Setup(r => r.GetByIdAsync(result.Id, It.IsAny<CancellationToken>())).ReturnsAsync(result);
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

        Identity = new Mock<IIdentityReadService>();
        Identity
            .Setup(i => i.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                (IReadOnlyDictionary<Guid, string>)PathwaysTestConstants.Usernames
                    .Where(kv => ids.Contains(kv.Key))
                    .ToDictionary());

        Holders = new Mock<IPermissionHolders>();
        Holders
            .Setup(h => h.WhoHoldsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), Permission.SignOffResults, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids, Permission _, CancellationToken _) =>
                (IReadOnlySet<Guid>)ids.Where(SignOffHolders.Contains).ToHashSet());
        Holders
            .Setup(h => h.GetUserIdsAsync(Permission.SignOffResults, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SignOffHolders);

        Notifications = new Mock<INotificationSender>();
        Notifications
            .Setup(n => n.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback((Notification notification, CancellationToken _) => Sent.Add(notification))
            .Returns(Task.CompletedTask);
    }

    [TearDown]
    public void TearDownResultMocks() => Cts.Dispose();

    /// <summary>Makes the repository return this result (and its report file), for reading and for updating.</summary>
    protected Result Given(Result result, Attachment? report = null)
    {
        Repository.Setup(r => r.GetForUpdateAsync(result.Id, It.IsAny<CancellationToken>())).ReturnsAsync(result);
        Repository.Setup(r => r.GetByIdAsync(result.Id, It.IsAny<CancellationToken>())).ReturnsAsync(result);
        report ??= ResultsTestConstants.ReportOf(result);
        Repository.Setup(r => r.GetReportAsync(report.Id, It.IsAny<CancellationToken>())).ReturnsAsync(report);
        Repository.Setup(r => r.GetReportForUpdateAsync(report.Id, It.IsAny<CancellationToken>())).ReturnsAsync(report);
        return result;
    }
}
