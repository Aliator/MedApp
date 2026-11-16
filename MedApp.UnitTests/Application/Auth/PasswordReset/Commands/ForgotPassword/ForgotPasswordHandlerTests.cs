using FluentAssertions;
using MedApp.Application.Authentication.Passwords.Commands.ForgotPassword;
using MedApp.Application.Common.Email;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using static MedApp.UnitTests.Common.Constants.EmailTestConstants;

namespace MedApp.UnitTests.Application.Auth.PasswordReset.Commands.ForgotPassword;

[TestFixture]
public sealed class ForgotPasswordHandlerTests
{
    private Mock<IPasswordResetService> _resets = null!;
    private Mock<IEmailSender> _sender = null!;
    private Mock<ISecurityEventLog> _events = null!;
    private List<EmailMessage> _sent = null!;
    private List<SecurityEvent> _recorded = null!;

    [SetUp]
    public void SetUp()
    {
        _resets = new Mock<IPasswordResetService>();
        _sender = new Mock<IEmailSender>();
        _events = new Mock<ISecurityEventLog>();
        _sent = [];
        _recorded = [];
        _sender.Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessage, CancellationToken>((m, _) => _sent.Add(m)).Returns(Task.CompletedTask);
        _events.Setup(e => e.RecordAsync(It.IsAny<SecurityEvent>(), It.IsAny<CancellationToken>()))
            .Callback<SecurityEvent, CancellationToken>((e, _) => _recorded.Add(e)).Returns(Task.CompletedTask);
    }

    private ForgotPasswordHandler Handler(string? baseUrl = LinkBaseUrl) =>
        new(_resets.Object, _sender.Object, Links(baseUrl), _events.Object, NullLogger<ForgotPasswordHandler>.Instance);

    [Test]
    public async Task Handle_AccountWithAConfirmedAddress_EmailsItASingleUseLink()
    {
        _resets.Setup(r => r.FindTargetsAsync(Username, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PasswordResetTarget(UserId, Username, Address, Token)]);

        await Handler().Handle(new ForgotPasswordCommand($"  {Username} "), CancellationToken.None);

        var email = _sent.Should().ContainSingle().Subject;
        email.To.Should().Be(Address);
        email.Subject.Should().Be(EmailTemplates.ResetPasswordSubject);
        email.TextBody.Should().Contain($"{LinkBaseUrl}/reset-password#user={UserId:D}&token={Uri.EscapeDataString(Token)}");
        _recorded.Should().ContainSingle(e => e.Kind == SecurityEventKind.PasswordResetRequested && e.UserId == UserId);
    }

    [Test]
    public async Task Handle_NoMatchingAccount_SendsNothingAndRecordsTheRequestWithoutWhatWasTyped()
    {
        _resets.Setup(r => r.FindTargetsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        await Handler().Handle(new ForgotPasswordCommand(OtherAddress), CancellationToken.None);

        _sent.Should().BeEmpty();
        var recorded = _recorded.Should().ContainSingle().Subject;
        recorded.Kind.Should().Be(SecurityEventKind.PasswordResetRequested);
        recorded.UserId.Should().BeNull();
        recorded.ToString().Should().NotContain(OtherAddress);
        recorded.Details!["matched"].Should().Be(false);
    }

    [Test]
    public async Task Handle_SeveralAccountsShareTheAddress_EachGetsItsOwnLink()
    {
        var other = Guid.NewGuid();
        _resets.Setup(r => r.FindTargetsAsync(Address, It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new PasswordResetTarget(UserId, Username, Address, Token),
            new PasswordResetTarget(other, "second.account", Address, "second-token"),
        ]);

        await Handler().Handle(new ForgotPasswordCommand(Address), CancellationToken.None);

        _sent.Should().HaveCount(2);
        _sent.Should().Contain(m => m.TextBody.Contains("second.account") && m.TextBody.Contains("second-token"));
    }

    [Test]
    public async Task Handle_NoLinkBaseUrl_SendsNothingButStillRecords()
    {
        _resets.Setup(r => r.FindTargetsAsync(Username, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PasswordResetTarget(UserId, Username, Address, Token)]);

        await Handler(baseUrl: null).Handle(new ForgotPasswordCommand(Username), CancellationToken.None);

        _sent.Should().BeEmpty();
        _recorded.Should().ContainSingle(e => e.UserId == UserId);
    }

    [Test]
    public async Task Handle_PassesTheCancellationTokenOn()
    {
        using var cts = new CancellationTokenSource();
        _resets.Setup(r => r.FindTargetsAsync(Username, cts.Token))
            .ReturnsAsync([new PasswordResetTarget(UserId, Username, Address, Token)]);

        await Handler().Handle(new ForgotPasswordCommand(Username), cts.Token);

        _sender.Verify(s => s.SendAsync(It.IsAny<EmailMessage>(), cts.Token), Times.Once);
    }
}
