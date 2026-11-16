using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Security;
using MedApp.Domain.Activity;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;
using static MedApp.UnitTests.Common.Constants.EmailTestConstants;

namespace MedApp.UnitTests.Application.Common.Security;

/// <summary>Security events in the activity log: by whom, about whom, with only their small facts.</summary>
[TestFixture]
public sealed class ActivitySecurityEventLogTests
{
    private RecordingActivityLog _activity = null!;
    private ActivitySecurityEventLog _log = null!;

    [SetUp]
    public void SetUp()
    {
        _activity = new RecordingActivityLog { Actor = new ActivityActor(null, string.Empty, "192.0.2.30") };
        _log = new ActivitySecurityEventLog(_activity, new LoggingSecurityEventLog(NullLogger<LoggingSecurityEventLog>.Instance));
    }

    [Test]
    public async Task OwnAccount_IsByTheUserFromTheRequestsAddress()
    {
        _activity.Actor = new ActivityActor(UserId, Username, "192.0.2.30");
        await _log.RecordAsync(new SecurityEvent(SecurityEventKind.TwoFactorEnabled, UserId, Username), CancellationToken.None);

        var entry = _activity.Recorded.Should().ContainSingle().Subject;
        entry.Action.Should().Be(ActivityAction.TwoFactorEnabled);
        entry.UserId.Should().Be(UserId);
        entry.Username.Should().Be(Username);
        entry.IpAddress.Should().Be("192.0.2.30");
        entry.TargetUsername.Should().BeNull();
        entry.Details.Should().Be("{}");
    }

    [Test]
    public async Task NobodySignedIn_IsByNobodyWithTheAccountAsTarget()
    {
        await _log.RecordAsync(new SecurityEvent(SecurityEventKind.ResetByLink, UserId, Username), CancellationToken.None);

        var entry = _activity.Recorded.Should().ContainSingle().Subject;
        entry.Action.Should().Be(ActivityAction.PasswordResetByEmail);
        entry.UserId.Should().BeNull();
        entry.Username.Should().BeEmpty();
        entry.IpAddress.Should().Be("192.0.2.30");
        entry.TargetUsername.Should().Be(Username);
    }

    [Test]
    public async Task ByAnAdmin_IsByTheAdminWithTheAccountAsTarget()
    {
        await _log.RecordAsync(
            new SecurityEvent(SecurityEventKind.TwoFactorReset, UserId, Username, ActorId, "an.admin"),
            CancellationToken.None);

        var entry = _activity.Recorded.Should().ContainSingle().Subject;
        entry.Action.Should().Be(ActivityAction.TwoFactorReset);
        entry.UserId.Should().Be(ActorId);
        entry.Username.Should().Be("an.admin");
        entry.TargetUsername.Should().Be(Username);
    }

    [Test]
    public async Task NoAccount_IsByNobodyWithItsDetails()
    {
        await _log.RecordAsync(
            new SecurityEvent(SecurityEventKind.ResetLinkRequested, null, null,
                Details: new Dictionary<string, object?> { ["matched"] = false }),
            CancellationToken.None);

        var entry = _activity.Recorded.Should().ContainSingle().Subject;
        entry.UserId.Should().BeNull();
        entry.Username.Should().BeEmpty();
        entry.Details.Should().Be("{\"matched\":false}");
    }

    [TestCase(SecurityEventKind.ResetLinkRequested, ActivityAction.PasswordResetRequested)]
    [TestCase(SecurityEventKind.ResetByLink, ActivityAction.PasswordResetByEmail)]
    [TestCase(SecurityEventKind.AddressChangeRequested, ActivityAction.EmailChangeRequested)]
    [TestCase(SecurityEventKind.AddressConfirmed, ActivityAction.EmailConfirmed)]
    [TestCase(SecurityEventKind.AddressRemoved, ActivityAction.EmailRemoved)]
    [TestCase(SecurityEventKind.TwoFactorEnabled, ActivityAction.TwoFactorEnabled)]
    [TestCase(SecurityEventKind.TwoFactorDisabled, ActivityAction.TwoFactorDisabled)]
    [TestCase(SecurityEventKind.TwoFactorReset, ActivityAction.TwoFactorReset)]
    [TestCase(SecurityEventKind.RecoveryCodesRegenerated, ActivityAction.RecoveryCodesRegenerated)]
    [TestCase(SecurityEventKind.RecoveryCodeUsed, ActivityAction.RecoveryCodeUsed)]
    [TestCase(SecurityEventKind.TwoFactorRequirementChanged, ActivityAction.TwoFactorRequirementChanged)]
    public void ActionFor_EveryKind(SecurityEventKind kind, ActivityAction action) =>
        ActivitySecurityEventLog.ActionFor(kind).Should().Be(action);

    [Test]
    public void ActionFor_EveryKindHasOne() =>
        Enum.GetValues<SecurityEventKind>().Select(ActivitySecurityEventLog.ActionFor).Should().OnlyHaveUniqueItems();

    [Test]
    public void ActionFor_UnknownKind_Throws()
    {
        var act = () => ActivitySecurityEventLog.ActionFor((SecurityEventKind)999);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void NewActions_StartAfterTheAttachmentsBranchs()
    {
        ((int)ActivityAction.TwoFactorChallenged).Should().Be(28);
        ((int)ActivityAction.TwoFactorRequirementChanged).Should().Be(39);
    }
}
