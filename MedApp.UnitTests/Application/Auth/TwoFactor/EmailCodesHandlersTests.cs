using FluentAssertions;
using FluentValidation;
using MedApp.Application.Authentication.Emails.Commands.RemoveMyEmail;
using MedApp.Application.Authentication.Emails.Commands.RemoveUserEmail;
using MedApp.Application.Authentication.Security.Repositories;
using MedApp.Application.Authentication.TwoFactor.Commands.BeginAuthenticatorSetup;
using MedApp.Application.Authentication.TwoFactor.Commands.BeginEmailCodesSetup;
using MedApp.Application.Authentication.TwoFactor.Commands.DisableTwoFactor;
using MedApp.Application.Authentication.TwoFactor.Commands.EnableEmailCodes;
using MedApp.Application.Authentication.TwoFactor.Commands.EnableTwoFactor;
using MedApp.Application.Authentication.TwoFactor.Commands.RegenerateRecoveryCodes;
using MedApp.Application.Authentication.TwoFactor.Commands.SendTwoFactorEmailCode;
using MedApp.Application.Authentication.TwoFactor.Common;
using MedApp.Application.Authentication.TwoFactor.Queries.GetMyTwoFactorStatus;
using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Email;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Security;
using MedApp.Contracts.Authentication.Responses;
using MedApp.Domain.Authentication;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Identity;
using Moq;
using static MedApp.UnitTests.Common.Constants.EmailTestConstants;

namespace MedApp.UnitTests.Application.Auth.TwoFactor;

/// <summary>
/// Turning two-factor codes by email on and off, the codes that confirm changes while they're on, moving to the app,
/// and what an address being used for codes stops.
/// </summary>
[TestFixture]
public sealed class EmailCodesHandlersTests
{
    private static readonly IReadOnlyList<string> Codes = Enumerable.Range(0, 10).Select(i => $"CODE{i}-23456").ToList();

    private Mock<ITwoFactorService> _twoFactor = null!;
    private Mock<ISecuritySettingsRepository> _settings = null!;
    private Mock<ISessionService> _sessions = null!;
    private Mock<IIdentityUserService> _users = null!;
    private Mock<IAccountEmailService> _emails = null!;
    private Mock<IEmailSender> _sender = null!;
    private Mock<IEmailCodeService> _emailCodes = null!;
    private PassThroughIdentityTransactions _transactions = null!;
    private Mock<ISecurityNotices> _notices = null!;
    private Mock<ISecurityEventLog> _events = null!;
    private Mock<IAccountAttemptLimiter> _attempts = null!;
    private FixedTimeProvider _clock = null!;

    private static readonly EmailCodeSent Sent = new(MaskedAddress, NowUtc.Add(EmailCode.Lifetime), NowUtc.AddMinutes(1), 3);

    [SetUp]
    public void SetUp()
    {
        _twoFactor = new Mock<ITwoFactorService>();
        _settings = new Mock<ISecuritySettingsRepository>();
        _settings.Setup(s => s.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new SecuritySettings());
        _sessions = new Mock<ISessionService>();
        _users = new Mock<IIdentityUserService>();
        _users.Setup(u => u.VerifyPasswordAsync(Username, CurrentPassword, It.IsAny<CancellationToken>())).ReturnsAsync(IdentityResult.Success);
        _emails = new Mock<IAccountEmailService>();
        _emails.Setup(e => e.GetAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(Account());
        _sender = new Mock<IEmailSender>();
        _sender.SetupGet(s => s.CanSend).Returns(true);
        _emailCodes = new Mock<IEmailCodeService>();
        _transactions = new PassThroughIdentityTransactions();
        _notices = new Mock<ISecurityNotices>();
        _events = new Mock<ISecurityEventLog>();
        _attempts = new Mock<IAccountAttemptLimiter>();
        _clock = new FixedTimeProvider(NowUtc);
    }

    private void GivenState(TwoFactorState state)
    {
        _twoFactor.Setup(t => t.GetStateAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(state);
        _twoFactor.Setup(t => t.GetMethodAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(state.UsesEmailCodes ? TwoFactorMethod.Email : state.Enabled ? TwoFactorMethod.Authenticator : TwoFactorMethod.None);
    }

    private static TwoFactorState Email(bool isAdmin = false) => State(isAdmin: isAdmin, method: TwoFactorMethod.Email);

    // The status

    [Test]
    public async Task Status_EmailCodes_SaysSo()
    {
        GivenState(Email());

        var status = await new GetMyTwoFactorStatusHandler(_twoFactor.Object, _settings.Object, _emails.Object, _sender.Object)
            .Handle(new GetMyTwoFactorStatusQuery(UserId), CancellationToken.None);

        status.Should().Be(new TwoFactorStatusResponse(true, 10, false, "Email", true, null));
    }

    [TestCase(true, null, EmailCodesAvailability.NoConfirmedAddress)]
    [TestCase(false, Address, EmailCodesAvailability.EmailUnavailable)]
    public async Task Status_WithoutAnAddressOrEmail_SaysWhyNot(bool canSend, string? address, string reason)
    {
        GivenState(State(enabled: false));
        _sender.SetupGet(s => s.CanSend).Returns(canSend);
        _emails.Setup(e => e.GetAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(Account(address, pending: OtherAddress));

        var status = await new GetMyTwoFactorStatusHandler(_twoFactor.Object, _settings.Object, _emails.Object, _sender.Object)
            .Handle(new GetMyTwoFactorStatusQuery(UserId), CancellationToken.None);

        status.CanUseEmailCodes.Should().BeFalse();
        status.EmailCodesUnavailableReason.Should().Be(reason);
        status.Method.Should().Be("None");
    }

    [Test]
    public void Availability_ExplainsEachReason()
    {
        EmailCodesAvailability.Explain(EmailCodesAvailability.EmailUnavailable).Should().Contain("can't send email");
        EmailCodesAvailability.Explain(EmailCodesAvailability.NoConfirmedAddress).Should().Contain("Confirm an email address");
    }

    [Test]
    public void Mapper_SentCode_GivesTheWaitByTheServersClock()
    {
        TwoFactorMapper.ToResponse(Sent, NowUtc.AddSeconds(20.5)).Should()
            .Be(new EmailCodeSentResponse(MaskedAddress, Sent.ExpiresAtUtc, 40, 3));
        TwoFactorMapper.ToResponse(Sent, NowUtc.AddMinutes(2)).ResendInSeconds.Should().Be(0);
        TwoFactorMapper.ToResponse(Sent with { ResendsLeft = 0 }, NowUtc).ResendInSeconds.Should().Be(0);
    }

    // BeginEmailCodesSetup

    private BeginEmailCodesSetupHandler BeginHandler() =>
        new(_twoFactor.Object, _users.Object, _emails.Object, _sender.Object, _emailCodes.Object, _clock);

    [Test]
    public async Task Begin_RightPassword_EmailsACodeToTurnItOn()
    {
        GivenState(State(enabled: false));
        _emailCodes.Setup(c => c.SendAsync(UserId, EmailCodePurpose.TurnOn, null, It.IsAny<CancellationToken>(), It.IsAny<DateTime?>())).ReturnsAsync(Sent);

        var result = await BeginHandler().Handle(new BeginEmailCodesSetupCommand(UserId, CurrentPassword), CancellationToken.None);

        result.Should().Be(new EmailCodeSentResponse(MaskedAddress, Sent.ExpiresAtUtc, 60, 3));
        new BeginEmailCodesSetupCommand(UserId, CurrentPassword).ToString().Should().NotContain(CurrentPassword);
    }

    [TestCase(TwoFactorMethod.Authenticator)]
    [TestCase(TwoFactorMethod.Email)]
    public async Task Begin_AlreadyOn_IsAConflict(TwoFactorMethod method)
    {
        GivenState(State(method: method));

        var act = () => BeginHandler().Handle(new BeginEmailCodesSetupCommand(UserId, CurrentPassword), CancellationToken.None);

        (await act.Should().ThrowAsync<ConflictException>()).Which.Message.Should().Contain("Turn it off first");
        _emailCodes.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Begin_NoConfirmedAddress_IsAConflictBeforeThePassword()
    {
        GivenState(State(enabled: false));
        _emails.Setup(e => e.GetAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(Account(null, pending: OtherAddress));

        var act = () => BeginHandler().Handle(new BeginEmailCodesSetupCommand(UserId, CurrentPassword), CancellationToken.None);

        (await act.Should().ThrowAsync<ConflictException>()).Which.Message.Should().Contain("Confirm an email address");
        _users.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Begin_EmailCantBeSent_IsAConflict()
    {
        GivenState(State(enabled: false));
        _sender.SetupGet(s => s.CanSend).Returns(false);

        var act = () => BeginHandler().Handle(new BeginEmailCodesSetupCommand(UserId, CurrentPassword), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Test]
    public async Task Begin_WrongPassword_IsAValidationErrorAndSendsNothing()
    {
        GivenState(State(enabled: false));
        _users.Setup(u => u.VerifyPasswordAsync(Username, CurrentPassword, It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Failed(IdentityErrors.PasswordIncorrect));

        var act = () => BeginHandler().Handle(new BeginEmailCodesSetupCommand(UserId, CurrentPassword), CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().ContainSingle(e => e.PropertyName == "Password");
        _emailCodes.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Begin_AddressGoneMeanwhile_IsAConflict()
    {
        GivenState(State(enabled: false));

        var act = () => BeginHandler().Handle(new BeginEmailCodesSetupCommand(UserId, CurrentPassword), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Test]
    public async Task Begin_NoAccount_IsNotFound()
    {
        var act = () => BeginHandler().Handle(new BeginEmailCodesSetupCommand(UserId, CurrentPassword), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public void BeginValidator_NeedsThePassword()
    {
        new BeginEmailCodesSetupValidator().Validate(new BeginEmailCodesSetupCommand(UserId, CurrentPassword)).IsValid.Should().BeTrue();
        new BeginEmailCodesSetupValidator().Validate(new BeginEmailCodesSetupCommand(UserId, "")).IsValid.Should().BeFalse();
    }

    // EnableEmailCodes

    private EnableEmailCodesHandler EnableHandler() =>
        new(_twoFactor.Object, _emailCodes.Object, _emails.Object, _sender.Object, _sessions.Object, _transactions, _notices.Object, _events.Object, _attempts.Object);

    private void RightCode() =>
        _emailCodes.Setup(c => c.CheckAsync(UserId, EmailCodePurpose.TurnOn, null, EmailedCode, It.IsAny<CancellationToken>())).ReturnsAsync(EmailCodeCheck.Right);

    [Test]
    public async Task Enable_RightCode_TurnsEmailCodesOnEndsOtherSessionsAndReturnsTheRecoveryCodes()
    {
        GivenState(State(enabled: false));
        RightCode();
        _twoFactor.Setup(t => t.EnableAsync(UserId, TwoFactorMethod.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecoveryCodesResult(IdentityResult.Success, Codes));

        var result = await EnableHandler().Handle(new EnableEmailCodesCommand(UserId, EmailedCode, SessionId), CancellationToken.None);

        result.RecoveryCodes.Should().Equal(Codes);
        _transactions.Runs.Should().Be(1);
        _attempts.Verify(a => a.Attempt(AccountAttempt.TwoFactorCode, UserId), Times.Once);
        _sessions.Verify(s => s.RevokeUserSessionsAsync(UserId, SessionId, It.IsAny<CancellationToken>()), Times.Once);
        _events.Verify(e => e.RecordAsync(
            It.Is<SecurityEvent>(ev => ev.Kind == SecurityEventKind.EmailCodesEnabled && ev.Details == null), It.IsAny<CancellationToken>()), Times.Once);
        _notices.Verify(n => n.SendAsync(UserId, SecurityNotice.EmailCodesEnabled, It.IsAny<CancellationToken>(), null, null), Times.Once);
        _notices.Verify(n => n.SendToOtherAdminsAsync(It.IsAny<SecurityNotice>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Enable_AnAdmin_TellsTheOtherAdmins()
    {
        GivenState(State(enabled: false, isAdmin: true));
        RightCode();
        _twoFactor.Setup(t => t.EnableAsync(UserId, TwoFactorMethod.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecoveryCodesResult(IdentityResult.Success, Codes));

        await EnableHandler().Handle(new EnableEmailCodesCommand(UserId, EmailedCode, SessionId), CancellationToken.None);

        _notices.Verify(n => n.SendToOtherAdminsAsync(SecurityNotice.AdminTwoFactorEnabled, UserId, Username, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Enable_WrongCode_IsAValidationErrorOnCodeAndChangesNothing()
    {
        GivenState(State(enabled: false));

        var act = () => EnableHandler().Handle(new EnableEmailCodesCommand(UserId, EmailedCode, SessionId), CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Should()
            .ContainSingle(e => e.PropertyName == "Code" && e.ErrorMessage == TwoFactorCodes.WrongEmailCode);
        _twoFactor.Verify(t => t.EnableAsync(It.IsAny<Guid>(), It.IsAny<TwoFactorMethod>(), It.IsAny<CancellationToken>()), Times.Never);
        // As with setting up the app: the code proves the mailbox, and doesn't count towards the lockout.
        _twoFactor.Verify(t => t.RecordFailedCodeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Enable_OverTheAccountsLimit_ChecksNoCode()
    {
        GivenState(State(enabled: false));
        _attempts.Setup(a => a.Attempt(AccountAttempt.TwoFactorCode, UserId)).Throws(new TooManyAttemptsException("Too many.", 60));

        var act = () => EnableHandler().Handle(new EnableEmailCodesCommand(UserId, EmailedCode, SessionId), CancellationToken.None);

        await act.Should().ThrowAsync<TooManyAttemptsException>();
        _emailCodes.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Enable_AlreadyOn_IsAConflict()
    {
        GivenState(State());

        var act = () => EnableHandler().Handle(new EnableEmailCodesCommand(UserId, EmailedCode, SessionId), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Test]
    public async Task Enable_IdentityRefuses_IsAConflictAndEndsNoSession()
    {
        GivenState(State(enabled: false));
        RightCode();
        _twoFactor.Setup(t => t.EnableAsync(UserId, TwoFactorMethod.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecoveryCodesResult(IdentityResult.Failed(new IdentityError { Description = "No." }), []));

        var act = () => EnableHandler().Handle(new EnableEmailCodesCommand(UserId, EmailedCode, SessionId), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _sessions.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Enable_NoAccount_IsNotFound()
    {
        var act = () => EnableHandler().Handle(new EnableEmailCodesCommand(UserId, EmailedCode, SessionId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [TestCase("654321", true)]
    [TestCase("654 321", true)]
    [TestCase("65432", false)]
    [TestCase("ABCDE-23456", false)]
    [TestCase("", false)]
    public void EnableValidator_WantsSixDigits(string code, bool valid)
    {
        new EnableEmailCodesValidator().Validate(new EnableEmailCodesCommand(UserId, code, null)).IsValid.Should().Be(valid);
        new EnableEmailCodesCommand(UserId, EmailedCode, null).ToString().Should().NotContain(EmailedCode);
    }

    // SendTwoFactorEmailCode

    private SendTwoFactorEmailCodeHandler SendHandler() => new(_twoFactor.Object, _users.Object, _emailCodes.Object, _clock);

    [Test]
    public async Task Send_UsingEmailCodes_EmailsACodeToConfirmAChange()
    {
        GivenState(Email());
        _emailCodes.Setup(c => c.SendAsync(UserId, EmailCodePurpose.Confirm, null, It.IsAny<CancellationToken>(), It.IsAny<DateTime?>())).ReturnsAsync(Sent);

        var result = await SendHandler().Handle(new SendTwoFactorEmailCodeCommand(UserId, CurrentPassword), CancellationToken.None);

        result.SentTo.Should().Be(MaskedAddress);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Send_NotUsingEmailCodes_IsAConflict(bool appOn)
    {
        GivenState(State(enabled: appOn));

        var act = () => SendHandler().Handle(new SendTwoFactorEmailCodeCommand(UserId, CurrentPassword), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _emailCodes.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Send_NoAddressToSendTo_IsAConflict()
    {
        GivenState(Email());

        var act = () => SendHandler().Handle(new SendTwoFactorEmailCodeCommand(UserId, CurrentPassword), CancellationToken.None);

        (await act.Should().ThrowAsync<ConflictException>()).Which.Message.Should().Contain("recovery code");
    }

    [Test]
    public async Task Send_NoAccount_IsNotFound()
    {
        var act = () => SendHandler().Handle(new SendTwoFactorEmailCodeCommand(UserId, CurrentPassword), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // Turning it off, and new recovery codes, with an emailed code

    private DisableTwoFactorHandler DisableHandler() =>
        new(_twoFactor.Object, _users.Object, _settings.Object, _sessions.Object, _transactions, _notices.Object, _events.Object, _attempts.Object, _emailCodes.Object);

    [Test]
    public async Task Disable_EmailCodes_ChecksTheEmailedCodeAndRecordsEmailCodesOff()
    {
        GivenState(Email());
        _emailCodes.Setup(c => c.CheckAsync(UserId, EmailCodePurpose.Confirm, null, EmailedCode, It.IsAny<CancellationToken>())).ReturnsAsync(EmailCodeCheck.Right);
        _twoFactor.Setup(t => t.DisableAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(IdentityResult.Success);

        await DisableHandler().Handle(new DisableTwoFactorCommand(UserId, CurrentPassword, EmailedCode, SessionId), CancellationToken.None);

        _twoFactor.Verify(t => t.VerifyAuthenticatorCodeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _events.Verify(e => e.RecordAsync(It.Is<SecurityEvent>(ev => ev.Kind == SecurityEventKind.EmailCodesDisabled), It.IsAny<CancellationToken>()), Times.Once);
        _notices.Verify(n => n.SendAsync(UserId, SecurityNotice.TwoFactorDisabled, It.IsAny<CancellationToken>(), null, null), Times.Once);
    }

    [Test]
    public async Task Disable_EmailCodes_WithARecoveryCode_UsesItUp()
    {
        GivenState(Email());
        _twoFactor.Setup(t => t.RedeemRecoveryCodeAsync(UserId, RecoveryCode, It.IsAny<CancellationToken>())).ReturnsAsync(new RecoveryCodeRedemption(true, 9));
        _twoFactor.Setup(t => t.DisableAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(IdentityResult.Success);

        await DisableHandler().Handle(new DisableTwoFactorCommand(UserId, CurrentPassword, RecoveryCode, SessionId), CancellationToken.None);

        _twoFactor.Verify(t => t.DisableAsync(UserId, It.IsAny<CancellationToken>()), Times.Once);
        _emailCodes.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Disable_EmailCodes_WrongCode_CountsAsAWrongAuthenticatorCodeDoes()
    {
        GivenState(Email());

        var act = () => DisableHandler().Handle(new DisableTwoFactorCommand(UserId, CurrentPassword, EmailedCode, SessionId), CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Should()
            .ContainSingle(e => e.PropertyName == "Code" && e.ErrorMessage == TwoFactorCodes.WrongEmailCode);
        _twoFactor.Verify(t => t.RecordFailedCodeAsync(UserId, It.IsAny<CancellationToken>()), Times.Once);
        _attempts.Verify(a => a.Attempt(AccountAttempt.TwoFactorCode, UserId), Times.Once);
    }

    [Test]
    public async Task Disable_EmailCodes_WhileAdminsMustUseTheApp_IsAConflict()
    {
        GivenState(Email(isAdmin: true));
        _settings.Setup(s => s.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new SecuritySettings { RequireTwoFactorForAdmins = true });

        var act = () => DisableHandler().Handle(new DisableTwoFactorCommand(UserId, CurrentPassword, EmailedCode, SessionId), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Test]
    public async Task Regenerate_EmailCodes_ChecksTheEmailedCode()
    {
        GivenState(Email());
        _emailCodes.Setup(c => c.CheckAsync(UserId, EmailCodePurpose.Confirm, null, EmailedCode, It.IsAny<CancellationToken>())).ReturnsAsync(EmailCodeCheck.Right);
        _twoFactor.Setup(t => t.RegenerateRecoveryCodesAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecoveryCodesResult(IdentityResult.Success, Codes));

        var result = await new RegenerateRecoveryCodesHandler(_twoFactor.Object, _users.Object, _notices.Object, _events.Object, _attempts.Object, _emailCodes.Object)
            .Handle(new RegenerateRecoveryCodesCommand(UserId, CurrentPassword, EmailedCode), CancellationToken.None);

        result.RecoveryCodes.Should().Equal(Codes);
    }

    [Test]
    public void WrongCodeFor_EachMethod() =>
        TwoFactorCodes.WrongCodeFor(TwoFactorMethod.Email).Should().Be(TwoFactorCodes.WrongEmailCode)
            .And.NotBe(TwoFactorCodes.WrongCodeFor(TwoFactorMethod.Authenticator));

    // Moving from email codes to the app (what the admin requirement needs)

    [Test]
    public async Task BeginAuthenticator_WhileUsingEmailCodes_GivesAKey()
    {
        GivenState(Email(isAdmin: true));
        _twoFactor.Setup(t => t.ResetAuthenticatorKeyAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticatorKey(Username, AuthenticatorKeyBase32));
        var qr = new Mock<IQrCodeRenderer>();
        qr.Setup(q => q.Render(It.IsAny<string>())).Returns(new QrCodeImage("M0,0h1v1h-1z", 33));

        var result = await new BeginAuthenticatorSetupHandler(_twoFactor.Object, qr.Object)
            .Handle(new BeginAuthenticatorSetupCommand(UserId), CancellationToken.None);

        result.QrCodeSize.Should().Be(33);
    }

    private EnableTwoFactorHandler EnableAppHandler() =>
        new(_twoFactor.Object, _users.Object, _sessions.Object, _transactions, _notices.Object, _events.Object, _emailCodes.Object, _attempts.Object);

    private const long AppCodeStep = 59_000_000;

    // A right code from the new app: matched, then recorded as used with the change.
    private void GivenTheAppsCode()
    {
        _twoFactor.Setup(t => t.MatchAuthenticatorCodeAsync(UserId, AuthenticatorCode, It.IsAny<CancellationToken>())).ReturnsAsync(AppCodeStep);
        _twoFactor.Setup(t => t.RecordAuthenticatorStepAsync(UserId, AppCodeStep, It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    private void SwitchingAdmin()
    {
        GivenState(Email(isAdmin: true));
        GivenTheAppsCode();
        _twoFactor.Setup(t => t.EnableAsync(UserId, TwoFactorMethod.Authenticator, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecoveryCodesResult(IdentityResult.Success, Codes));
    }

    [Test]
    public async Task EnableAuthenticator_WhileUsingEmailCodes_WithAnEmailedCode_SwitchesAndTellsEveryone()
    {
        SwitchingAdmin();
        _emailCodes.Setup(c => c.CheckAsync(UserId, EmailCodePurpose.Confirm, null, EmailedCode, It.IsAny<CancellationToken>())).ReturnsAsync(EmailCodeCheck.Right);

        var result = await EnableAppHandler()
            .Handle(new EnableTwoFactorCommand(UserId, CurrentPassword, AuthenticatorCode, SessionId, EmailedCode), CancellationToken.None);

        result.RecoveryCodes.Should().Equal(Codes);
        _events.Verify(e => e.RecordAsync(
            It.Is<SecurityEvent>(ev => ev.Kind == SecurityEventKind.TwoFactorEnabled && (string)ev.Details!["switchedFrom"]! == "email"),
            It.IsAny<CancellationToken>()), Times.Once);
        _notices.Verify(n => n.SendAsync(UserId, SecurityNotice.TwoFactorSwitchedToApp, It.IsAny<CancellationToken>(), null, null), Times.Once);
        // The review of #118, finding 2: other admins hear of a switch, as they do when two-factor goes off.
        _notices.Verify(n => n.SendToOtherAdminsAsync(SecurityNotice.AdminTwoFactorSwitchedToApp, UserId, Username, It.IsAny<CancellationToken>()), Times.Once);
        new EnableTwoFactorCommand(UserId, CurrentPassword, AuthenticatorCode, SessionId, EmailedCode).ToString().Should().NotContain(EmailedCode);
    }

    [Test]
    public async Task EnableAuthenticator_WhileUsingEmailCodes_WithARecoveryCode_Switches()
    {
        SwitchingAdmin();
        _twoFactor.Setup(t => t.RedeemRecoveryCodeAsync(UserId, RecoveryCode, It.IsAny<CancellationToken>())).ReturnsAsync(new RecoveryCodeRedemption(true, 9));

        await EnableAppHandler()
            .Handle(new EnableTwoFactorCommand(UserId, CurrentPassword, AuthenticatorCode, SessionId, RecoveryCode), CancellationToken.None);

        _twoFactor.Verify(t => t.EnableAsync(UserId, TwoFactorMethod.Authenticator, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task EnableAuthenticator_WhileUsingEmailCodes_WithoutTheCurrentFactor_IsRefused()
    {
        // The review of #118, finding 2: a session and the password alone used to swap the second factor.
        SwitchingAdmin();

        var act = () => EnableAppHandler()
            .Handle(new EnableTwoFactorCommand(UserId, CurrentPassword, AuthenticatorCode, SessionId), CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Should()
            .ContainSingle(e => e.PropertyName == "CurrentCode" && e.ErrorMessage == EnableTwoFactorHandler.CurrentCodeNeeded);
        _twoFactor.Verify(t => t.EnableAsync(It.IsAny<Guid>(), It.IsAny<TwoFactorMethod>(), It.IsAny<CancellationToken>()), Times.Never);
        // The new app's code isn't spent on a request that can't succeed.
        _twoFactor.Verify(t => t.MatchAuthenticatorCodeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task EnableAuthenticator_WhileUsingEmailCodes_WrongCurrentCode_CountsTowardsTheLockout()
    {
        SwitchingAdmin();

        var act = () => EnableAppHandler()
            .Handle(new EnableTwoFactorCommand(UserId, CurrentPassword, AuthenticatorCode, SessionId, EmailedCode), CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().ContainSingle(e => e.PropertyName == "CurrentCode");
        _twoFactor.Verify(t => t.RecordFailedCodeAsync(UserId, It.IsAny<CancellationToken>()), Times.Once);
        _twoFactor.Verify(t => t.EnableAsync(It.IsAny<Guid>(), It.IsAny<TwoFactorMethod>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task EnableAuthenticator_WhileUsingEmailCodes_WrongCurrentCodeThenRight_TheSameAppCodeStillWorks()
    {
        // The follow-up review of #118, finding 10: a mistyped emailed code used to spend the new app's code, so the
        // retry within its 30 seconds was refused as a replay. Here the app's code is used once, as the service does.
        SwitchingAdmin();
        long? used = null;
        _twoFactor.Setup(t => t.MatchAuthenticatorCodeAsync(UserId, AuthenticatorCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => used >= AppCodeStep ? null : AppCodeStep);
        _twoFactor.Setup(t => t.RecordAuthenticatorStepAsync(UserId, AppCodeStep, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                if (used >= AppCodeStep)
                    return false;
                used = AppCodeStep;
                return true;
            });
        _twoFactor.Setup(t => t.VerifyAuthenticatorCodeAsync(UserId, AuthenticatorCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                if (used >= AppCodeStep)
                    return false;
                used = AppCodeStep;
                return true;
            });
        _emailCodes.SetupSequence(c => c.CheckAsync(UserId, EmailCodePurpose.Confirm, null, EmailedCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmailCodeCheck.Wrong)
            .ReturnsAsync(EmailCodeCheck.Right);
        var command = new EnableTwoFactorCommand(UserId, CurrentPassword, AuthenticatorCode, SessionId, EmailedCode);

        var mistyped = () => EnableAppHandler().Handle(command, CancellationToken.None);
        (await mistyped.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().ContainSingle(e => e.PropertyName == "CurrentCode");
        used.Should().BeNull();

        var result = await EnableAppHandler().Handle(command, CancellationToken.None);

        result.RecoveryCodes.Should().Equal(Codes);
        used.Should().Be(AppCodeStep);
        _twoFactor.Verify(t => t.EnableAsync(UserId, TwoFactorMethod.Authenticator, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task EnableAuthenticator_WhileUsingEmailCodes_WrongAppCode_UsesNoRecoveryCode()
    {
        // Why the app's code is still checked first: a wrong one mustn't spend the current factor.
        GivenState(Email());

        var act = () => EnableAppHandler()
            .Handle(new EnableTwoFactorCommand(UserId, CurrentPassword, AuthenticatorCode, SessionId, RecoveryCode), CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().ContainSingle(e => e.PropertyName == "Code");
        _twoFactor.Verify(t => t.RedeemRecoveryCodeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task EnableAuthenticator_NotUsingEmailCodes_NeedsNoCurrentCode()
    {
        GivenState(State(enabled: false));
        GivenTheAppsCode();
        _twoFactor.Setup(t => t.EnableAsync(UserId, TwoFactorMethod.Authenticator, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecoveryCodesResult(IdentityResult.Success, Codes));

        await EnableAppHandler().Handle(new EnableTwoFactorCommand(UserId, CurrentPassword, AuthenticatorCode, SessionId), CancellationToken.None);

        _notices.Verify(n => n.SendAsync(UserId, SecurityNotice.TwoFactorEnabled, It.IsAny<CancellationToken>(), null, null), Times.Once);
        _emailCodes.VerifyNoOtherCalls();
    }

    // Codes that can't work, and code cleaning (the review of #118, findings 1 and 4)

    [TestCase("654321\n")]
    [TestCase("654321\r\n")]
    [TestCase(" 654 321 ")]
    [TestCase("\t654321")]
    public async Task Disable_EmailedCodeWithALineBreak_IsAcceptedAsAtSignIn(string typed)
    {
        GivenState(Email());
        _emailCodes.Setup(c => c.CheckAsync(UserId, EmailCodePurpose.Confirm, null, typed, It.IsAny<CancellationToken>())).ReturnsAsync(EmailCodeCheck.Right);
        _twoFactor.Setup(t => t.DisableAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(IdentityResult.Success);

        await DisableHandler().Handle(new DisableTwoFactorCommand(UserId, CurrentPassword, typed, SessionId), CancellationToken.None);

        _twoFactor.Verify(t => t.DisableAsync(UserId, It.IsAny<CancellationToken>()), Times.Once);
        _twoFactor.Verify(t => t.RedeemRecoveryCodeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Disable_EmailedCodeThatCantWorkAnyMore_SaysAskForANewOne_WithoutCountingIt()
    {
        GivenState(Email());
        _emailCodes.Setup(c => c.CheckAsync(UserId, EmailCodePurpose.Confirm, null, EmailedCode, It.IsAny<CancellationToken>())).ReturnsAsync(EmailCodeCheck.NotLive);

        var act = () => DisableHandler().Handle(new DisableTwoFactorCommand(UserId, CurrentPassword, EmailedCode, SessionId), CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Should()
            .ContainSingle(e => e.PropertyName == "Code" && e.ErrorMessage == TwoFactorCodes.EmailCodeNotLive);
        _twoFactor.Verify(t => t.RecordFailedCodeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestCase("123456\n", true)]
    [TestCase("123456\r\n", true)]
    [TestCase("\t123 456 ", true)]
    [TestCase("1234567", false)]
    [TestCase(null, false)]
    public void IsAuthenticatorCode_CleansAsEmailCodesDo(string? code, bool expected)
    {
        TwoFactorCodes.IsAuthenticatorCode(code).Should().Be(expected);
        EmailCodes.IsWellFormed(code).Should().Be(expected);
    }

    [Test]
    public async Task EnableEmail_CodeThatCantWorkAnyMore_SaysAskForANewOne()
    {
        GivenState(State(enabled: false));
        _emailCodes.Setup(c => c.CheckAsync(UserId, EmailCodePurpose.TurnOn, null, EmailedCode, It.IsAny<CancellationToken>())).ReturnsAsync(EmailCodeCheck.NotLive);

        var act = () => EnableHandler().Handle(new EnableEmailCodesCommand(UserId, EmailedCode, SessionId), CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Should()
            .ContainSingle(e => e.ErrorMessage == TwoFactorCodes.EmailCodeNotLive);
    }

    [Test]
    public async Task EnableEmail_AddressRemovedSinceTheCodeWasSent_IsAConflict()
    {
        // The review of #118, finding 6.
        GivenState(State(enabled: false));
        _emails.Setup(e => e.GetAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(Account(null));

        var act = () => EnableHandler().Handle(new EnableEmailCodesCommand(UserId, EmailedCode, SessionId), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _emailCodes.VerifyNoOtherCalls();
        _twoFactor.Verify(t => t.EnableAsync(It.IsAny<Guid>(), It.IsAny<TwoFactorMethod>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // Moving the address while it's the second factor (the review of #118, finding 2)

    private MedApp.Application.Authentication.Emails.Commands.ChangeMyEmail.ChangeMyEmailHandler ChangeHandler() =>
        new(_emails.Object, _users.Object, _twoFactor.Object, Availability(canSend: true), _sender.Object, Links(), _notices.Object, _events.Object,
            _attempts.Object, _emailCodes.Object);

    private void GivenAChange() =>
        _emails.Setup(e => e.RequestChangeAsync(UserId, OtherAddress, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailChangeRequest(Username, OtherAddress, "token"));

    [Test]
    public async Task ChangeMyEmail_UsingEmailCodes_WithoutACode_IsRefused()
    {
        GivenState(Email());
        GivenAChange();

        var act = () => ChangeHandler().Handle(
            new MedApp.Application.Authentication.Emails.Commands.ChangeMyEmail.ChangeMyEmailCommand(UserId, OtherAddress, CurrentPassword),
            CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().ContainSingle(e => e.PropertyName == "Code");
        _emails.Verify(e => e.RequestChangeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ChangeMyEmail_UsingEmailCodes_WithAnEmailedCode_AsksForTheNewAddress()
    {
        GivenState(Email());
        GivenAChange();
        _emailCodes.Setup(c => c.CheckAsync(UserId, EmailCodePurpose.Confirm, null, EmailedCode, It.IsAny<CancellationToken>())).ReturnsAsync(EmailCodeCheck.Right);

        await ChangeHandler().Handle(
            new MedApp.Application.Authentication.Emails.Commands.ChangeMyEmail.ChangeMyEmailCommand(UserId, OtherAddress, CurrentPassword, EmailedCode),
            CancellationToken.None);

        _emails.Verify(e => e.RequestChangeAsync(UserId, OtherAddress, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ChangeMyEmail_UsingEmailCodes_WrongCode_CountsTowardsTheLockout()
    {
        GivenState(Email());
        GivenAChange();

        var act = () => ChangeHandler().Handle(
            new MedApp.Application.Authentication.Emails.Commands.ChangeMyEmail.ChangeMyEmailCommand(UserId, OtherAddress, CurrentPassword, EmailedCode),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        _twoFactor.Verify(t => t.RecordFailedCodeAsync(UserId, It.IsAny<CancellationToken>()), Times.Once);
        _emails.Verify(e => e.RequestChangeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ChangeMyEmail_WithTheApp_NeedsNoCode()
    {
        GivenState(State());
        GivenAChange();

        await ChangeHandler().Handle(
            new MedApp.Application.Authentication.Emails.Commands.ChangeMyEmail.ChangeMyEmailCommand(UserId, OtherAddress, CurrentPassword),
            CancellationToken.None);

        _emailCodes.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Send_WrongPassword_SendsNothing()
    {
        GivenState(Email());
        _users.Setup(u => u.VerifyPasswordAsync(Username, CurrentPassword, It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Failed(IdentityErrors.PasswordIncorrect));

        var act = () => SendHandler().Handle(new SendTwoFactorEmailCodeCommand(UserId, CurrentPassword), CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().ContainSingle(e => e.PropertyName == "Password");
        _emailCodes.VerifyNoOtherCalls();
        new SendTwoFactorEmailCodeValidator().Validate(new SendTwoFactorEmailCodeCommand(UserId, "")).IsValid.Should().BeFalse();
        new SendTwoFactorEmailCodeCommand(UserId, CurrentPassword).ToString().Should().NotContain(CurrentPassword);
    }

    // The address while it's used for codes

    [Test]
    public async Task RemoveMyEmail_WhileUsedForCodes_IsAConflictBeforeThePassword()
    {
        GivenState(Email());

        var act = () => new RemoveMyEmailHandler(_emails.Object, _users.Object, _twoFactor.Object, _notices.Object, _events.Object)
            .Handle(new RemoveMyEmailCommand(UserId, CurrentPassword), CancellationToken.None);

        (await act.Should().ThrowAsync<ConflictException>()).Which.Message.Should().Be(RemoveMyEmailHandler.SignsInWithEmailCodes);
        _users.VerifyNoOtherCalls();
        _emails.Verify(e => e.RemoveAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task RemoveUserEmail_WhileUsedForCodes_IsAConflict()
    {
        GivenState(Email());
        var read = new Mock<IIdentityReadService>();
        read.Setup(r => r.GetUserAsync(Username, It.IsAny<CancellationToken>())).ReturnsAsync(new UserResponse(UserId, Username, []));

        var act = () => new RemoveUserEmailHandler(read.Object, _emails.Object, _twoFactor.Object, _notices.Object, _events.Object)
            .Handle(new RemoveUserEmailCommand(Username, ActorId, "an.admin"), CancellationToken.None);

        (await act.Should().ThrowAsync<ConflictException>()).Which.Message.Should().Be(RemoveUserEmailHandler.SignsInWithEmailCodes);
        _emails.Verify(e => e.RemoveAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
