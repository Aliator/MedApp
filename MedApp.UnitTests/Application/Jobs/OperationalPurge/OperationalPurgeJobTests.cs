using FluentAssertions;
using MedApp.Application.Jobs.OperationalPurge;
using MedApp.Application.Notepads.Common;
using MedApp.Domain.Activity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using static MedApp.UnitTests.Common.Constants.JobsTestConstants;

namespace MedApp.UnitTests.Application.Jobs.OperationalPurge;

[TestFixture]
public sealed class OperationalPurgeJobTests
{
    private Mock<IOperationalDataRepository> _repository = null!;
    private OperationalRetentionOptions _options = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = new Mock<IOperationalDataRepository>();
        _options = new OperationalRetentionOptions();
    }

    private OperationalPurgeJob Job => new(
        _repository.Object,
        Options.Create(_options),
        Options.Create(new NotepadOptions { KeepDays = 30 }),
        NullLogger<OperationalPurgeJob>.Instance);

    [Test]
    public void Defaults_AreTheRetentionSchedulesPeriods_AsADryRun()
    {
        _options.DryRun.Should().BeTrue();
        _options.SessionMonths.Should().Be(12);
        _options.SignInEventMonths.Should().Be(12);
        _options.NotificationMonths.Should().Be(6);
        _options.ExpiredCodeDays.Should().Be(1);
        _options.IsValid.Should().BeTrue();
    }

    [TestCase(0, 12, 6, 1)]
    [TestCase(12, 0, 6, 1)]
    [TestCase(12, 12, 0, 1)]
    [TestCase(12, 12, 6, 0)]
    public void IsValid_APeriodUnderOne_IsFalse(int sessions, int signIns, int notifications, int codes)
    {
        new OperationalRetentionOptions
        {
            SessionMonths = sessions,
            SignInEventMonths = signIns,
            NotificationMonths = notifications,
            ExpiredCodeDays = codes,
        }.IsValid.Should().BeFalse();
    }

    [Test]
    public void Schedule_IsDailyAtHalfPastFour()
    {
        Job.Name.Should().Be("operational-purge");
        Job.Schedule.Describe().Should().Be("Daily at 04:30 (UK time)");
    }

    [Test]
    public void Description_DryRun_SaysSo()
    {
        Job.Description.Should().StartWith("Dry run").And.Contain("Never patient records");
        _options.DryRun = false;
        Job.Description.Should().StartWith("Deletes sessions after 12 months");
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task RunAsync_PurgesEachKindAtItsCutoff_AndReturnsTheTotal(bool dryRun)
    {
        _options.DryRun = dryRun;
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        _repository.Setup(r => r.PurgeSessionsAsync(Now.AddMonths(-12), dryRun, ct)).ReturnsAsync(1);
        _repository.Setup(r => r.PurgeActivityAsync(OperationalPurgeJob.SignInAndSecurityActions, Now.AddMonths(-12), dryRun, ct)).ReturnsAsync(2);
        _repository.Setup(r => r.PurgeNotificationsAsync(Now.AddMonths(-6), dryRun, ct)).ReturnsAsync(3);
        _repository.Setup(r => r.PurgeNotepadsAsync(Now.AddDays(-30), dryRun, ct)).ReturnsAsync(4);
        _repository.Setup(r => r.PurgeSignInChallengesAsync(Now.AddDays(-1), dryRun, ct)).ReturnsAsync(5);
        _repository.Setup(r => r.PurgeEmailCodesAsync(Now.AddDays(-1), dryRun, ct)).ReturnsAsync(6);

        var total = await Job.RunAsync(Now, ct);

        total.Should().Be(21);
        _repository.VerifyAll();
    }

    [Test]
    public async Task RunAsync_LongerPeriods_UsesThem()
    {
        _options.SessionMonths = 24;
        _options.NotificationMonths = 3;

        await Job.RunAsync(Now, CancellationToken.None);

        _repository.Verify(r => r.PurgeSessionsAsync(Now.AddMonths(-24), true, It.IsAny<CancellationToken>()));
        _repository.Verify(r => r.PurgeNotificationsAsync(Now.AddMonths(-3), true, It.IsAny<CancellationToken>()));
    }

    [Test]
    public void SignInAndSecurityActions_AreSignInsSignOutsAndSecurityEvents_NeverPatientOrAccountEntries()
    {
        OperationalPurgeJob.SignInAndSecurityActions.Should().BeEquivalentTo(
        [
            ActivityAction.SignedIn, ActivityAction.SignInFailed, ActivityAction.TwoFactorChallenged,
            ActivityAction.SignedOut, ActivityAction.SignedOutEverywhere,
            ActivityAction.TwoFactorEnabled, ActivityAction.TwoFactorDisabled, ActivityAction.TwoFactorReset,
            ActivityAction.RecoveryCodesRegenerated, ActivityAction.RecoveryCodeUsed,
            ActivityAction.PasswordResetRequested, ActivityAction.PasswordResetByEmail,
            ActivityAction.EmailChangeRequested, ActivityAction.EmailConfirmed, ActivityAction.EmailRemoved,
            ActivityAction.TwoFactorRequirementChanged, ActivityAction.EmailCodesEnabled, ActivityAction.EmailCodesDisabled,
        ]);
        OperationalPurgeJob.SignInAndSecurityActions.Should().NotContain(
        [
            ActivityAction.PatientViewed, ActivityAction.PatientListed, ActivityAction.PatientsSearched,
            ActivityAction.PathwayViewed, ActivityAction.AttachmentDownloaded, ActivityAction.DemographicsLookedUp,
            ActivityAction.AccountCreated, ActivityAction.AccountDeleted, ActivityAction.RolesChanged,
            ActivityAction.PasswordReset, ActivityAction.PasswordChanged,
        ]);
    }
}
