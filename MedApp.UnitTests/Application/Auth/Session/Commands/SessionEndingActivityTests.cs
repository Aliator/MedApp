using AutoFixture;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Authentication.Sessions.Commands.SignOutEverywhere;
using MedApp.Application.Authentication.Users.Commands.ResetUserPassword;
using MedApp.Application.Authentication.Users.Commands.UpdateUserPassword;
using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Authentication.Responses;
using MedApp.Domain.Activity;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Identity;
using Moq;
using static MedApp.UnitTests.Common.Constants.AuthTestConstants;

namespace MedApp.UnitTests.Application.Auth.Session.Commands;

/// <summary>
/// The activity log for the account changes that end sessions: signing out everywhere is recorded as an account action,
/// and a password change or reset is recorded inside the same transaction as the change and the end of the sessions,
/// so none of them is kept without the others.
/// </summary>
[TestFixture]
public sealed class SessionEndingActivityTests
{
    private IFixture _fixture = null!;
    private Mock<IIdentityUserService> _users = null!;
    private Mock<IIdentityReadService> _read = null!;
    private RecordingTransactions _transactions = null!;
    private TransactionAwareActivityLog _activity = null!;

    private string Target => Usernames[1];

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        _users = new Mock<IIdentityUserService>();
        _read = new Mock<IIdentityReadService>();
        _fixture.Inject(_users.Object);
        _fixture.Inject(_read.Object);
        _fixture.Inject(new Mock<ISessionService>().Object);
        _transactions = new RecordingTransactions();
        _fixture.Inject<IIdentityTransactions>(_transactions);
        _activity = new TransactionAwareActivityLog(_transactions);
        _fixture.Inject<IActivityLog>(_activity);

        _read.Setup(r => r.GetUserAsync(Target, It.IsAny<CancellationToken>())).ReturnsAsync(SessionUser("User"));
    }

    [Test]
    public async Task SignOutEverywhere_UserFound_RecordsItAgainstTheirAccount()
    {
        await _fixture.Create<SignOutEverywhereHandler>().Handle(new SignOutEverywhereCommand(Target), CancellationToken.None);

        var entry = _activity.Recorded.Should().ContainSingle().Subject;
        entry.Action.Should().Be(ActivityAction.SignedOutEverywhere);
        entry.TargetUsername.Should().Be(Target);
    }

    [Test]
    public async Task SignOutEverywhere_NoSuchUser_RecordsNothing()
    {
        _read.Setup(r => r.GetUserAsync(Target, It.IsAny<CancellationToken>())).ReturnsAsync((UserResponse?)null);

        await _fixture.Create<SignOutEverywhereHandler>().Handle(new SignOutEverywhereCommand(Target), CancellationToken.None);

        _activity.Recorded.Should().BeEmpty();
    }

    [Test]
    public async Task ResetPassword_RecordsTheResetInsideTheTransaction()
    {
        _users.Setup(u => u.ResetUserPasswordAsync(Target, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResetUserPasswordResponse("Generated-Synthetic-1", []));

        await _fixture.Create<ResetUserPasswordHandler>().Handle(new ResetUserPasswordCommand(Target), CancellationToken.None);

        _activity.Recorded.Should().ContainSingle(e => e.Action == ActivityAction.PasswordReset);
        _activity.RecordedInsideTransaction.Should().Equal(true);
    }

    [Test]
    public async Task ChangePassword_RecordsTheChangeInsideTheTransaction()
    {
        _users.Setup(u => u.UpdateUserPasswordAsync(Target, "Old-Synthetic-1", "New-Synthetic-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Success);

        await _fixture.Create<UpdateUserPasswordHandler>()
            .Handle(new UpdateUserPasswordCommand(Target, "Old-Synthetic-1", "New-Synthetic-1", SessionId), CancellationToken.None);

        _activity.Recorded.Should().ContainSingle(e => e.Action == ActivityAction.PasswordChanged);
        _activity.RecordedInsideTransaction.Should().Equal(true);
    }

    /// <summary>Runs the work as a transaction would, and says whether one is running.</summary>
    private sealed class RecordingTransactions : IIdentityTransactions
    {
        public bool IsRunning { get; private set; }

        public async Task<IdentityResult> RunAsync(Func<Task<IdentityResult>> work, CancellationToken ct)
        {
            IsRunning = true;
            try
            {
                return await work();
            }
            finally
            {
                IsRunning = false;
            }
        }
    }

    /// <summary>A recording activity log that also notes whether each entry was recorded inside the transaction.</summary>
    private sealed class TransactionAwareActivityLog(RecordingTransactions transactions) : IActivityLog
    {
        private readonly RecordingActivityLog _inner = new();

        public List<bool> RecordedInsideTransaction { get; } = [];

        public List<ActivityEntry> Recorded => _inner.Recorded;

        public ActivityActor Actor => _inner.Actor;

        public ActivityEntry Entry(ActivityAction action, Guid? patientId = null, object? details = null, string? targetUsername = null,
            Guid? pathwayId = null, ActivityActor? actor = null) =>
            _inner.Entry(action, patientId, details, targetUsername, pathwayId, actor);

        public Task RecordAsync(ActivityEntry entry, CancellationToken ct)
        {
            RecordedInsideTransaction.Add(transactions.IsRunning);
            return _inner.RecordAsync(entry, ct);
        }

        public Task RecordViewsAsync(ActivityAction action, IEnumerable<Guid> patientIds, CancellationToken ct, Guid? pathwayId = null) =>
            _inner.RecordViewsAsync(action, patientIds, ct, pathwayId);

        public Task RecordViewAsync(ActivityAction action, Guid patientId, object details, CancellationToken ct) =>
            _inner.RecordViewAsync(action, patientId, details, ct);
    }
}
