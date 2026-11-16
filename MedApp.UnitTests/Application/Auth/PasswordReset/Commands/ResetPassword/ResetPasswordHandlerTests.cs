using FluentAssertions;
using MedApp.Application.Authentication.Passwords.Commands.ResetPassword;
using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Email;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Security;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Identity;
using Moq;
using static MedApp.UnitTests.Common.Constants.EmailTestConstants;

namespace MedApp.UnitTests.Application.Auth.PasswordReset.Commands.ResetPassword;

[TestFixture]
public sealed class ResetPasswordHandlerTests
{
    private Mock<IPasswordResetService> _resets = null!;
    private Mock<ISessionService> _sessions = null!;
    private PassThroughIdentityTransactions _transactions = null!;
    private Mock<ISecurityNotices> _notices = null!;
    private Mock<ISecurityEventLog> _events = null!;
    private ResetPasswordHandler _handler = null!;

    private static readonly ResetPasswordCommand Command = new(UserId, Token, NewPassword);

    [SetUp]
    public void SetUp()
    {
        _resets = new Mock<IPasswordResetService>();
        _sessions = new Mock<ISessionService>();
        _transactions = new PassThroughIdentityTransactions();
        _notices = new Mock<ISecurityNotices>();
        _events = new Mock<ISecurityEventLog>();
        _handler = new ResetPasswordHandler(_resets.Object, _sessions.Object, _transactions, _notices.Object, _events.Object);
    }

    [Test]
    public async Task Handle_ValidLink_ResetsEndsEverySessionInOneTransactionAndTellsTheOwner()
    {
        _resets.Setup(r => r.ResetAsync(UserId, Token, NewPassword, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PasswordResetResult(IdentityResult.Success, Username));

        var result = await _handler.Handle(Command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        _transactions.Runs.Should().Be(1);
        _sessions.Verify(s => s.RevokeUserSessionsAsync(UserId, null, It.IsAny<CancellationToken>()), Times.Once);
        _notices.Verify(n => n.SendAsync(UserId, SecurityNotice.ResetByLink, It.IsAny<CancellationToken>(), null, null), Times.Once);
        _events.Verify(e => e.RecordAsync(
            It.Is<SecurityEvent>(ev => ev.Kind == SecurityEventKind.ResetByLink && ev.UserId == UserId && ev.Username == Username),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_InvalidLink_EndsNoSessionAndSendsNothing()
    {
        _resets.Setup(r => r.ResetAsync(UserId, Token, NewPassword, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PasswordResetResult(IdentityResult.Failed(IdentityErrors.InvalidToken)));

        var result = await _handler.Handle(Command, CancellationToken.None);

        result.Errors.Should().ContainSingle(e => e.Code == IdentityErrors.InvalidTokenCode);
        _sessions.VerifyNoOtherCalls();
        _notices.VerifyNoOtherCalls();
        _events.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Handle_PassesTheCancellationTokenOn()
    {
        using var cts = new CancellationTokenSource();
        _resets.Setup(r => r.ResetAsync(UserId, Token, NewPassword, cts.Token))
            .ReturnsAsync(new PasswordResetResult(IdentityResult.Success, Username));

        await _handler.Handle(Command, cts.Token);

        _transactions.LastToken.Should().Be(cts.Token);
        _sessions.Verify(s => s.RevokeUserSessionsAsync(UserId, null, cts.Token), Times.Once);
    }

    [Test]
    public void Command_ToString_NeverShowsTheTokenOrPassword() =>
        Command.ToString().Should().NotContain(Token).And.NotContain(NewPassword);
}
