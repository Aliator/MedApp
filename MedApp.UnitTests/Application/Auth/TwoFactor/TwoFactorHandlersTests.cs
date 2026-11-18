using FluentAssertions;
using FluentValidation;
using MedApp.Application.Authentication.Security.Repositories;
using MedApp.Application.Authentication.TwoFactor.Commands.BeginAuthenticatorSetup;
using MedApp.Application.Authentication.TwoFactor.Commands.DisableTwoFactor;
using MedApp.Application.Authentication.TwoFactor.Commands.EnableTwoFactor;
using MedApp.Application.Authentication.TwoFactor.Commands.RegenerateRecoveryCodes;
using MedApp.Application.Authentication.TwoFactor.Commands.ResetUserTwoFactor;
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

[TestFixture]
public sealed class TwoFactorHandlersTests
{
    private Mock<ITwoFactorService> _twoFactor = null!;
    private Mock<ISecuritySettingsRepository> _settings = null!;
    private Mock<ISessionService> _sessions = null!;
    private Mock<IIdentityUserService> _users = null!;
    private Mock<IIdentityReadService> _read = null!;
    private PassThroughIdentityTransactions _transactions = null!;
    private Mock<ISecurityNotices> _notices = null!;
    private Mock<ISecurityEventLog> _events = null!;
    private Mock<IAccountAttemptLimiter> _attempts = null!;
    private Mock<IEmailCodeService> _emailCodes = null!;
    private Mock<IAccountEmailService> _emails = null!;
    private Mock<IEmailSender> _sender = null!;

    private static readonly IReadOnlyList<string> Codes = Enumerable.Range(0, 10).Select(i => $"CODE{i}-23456").ToList();

    [SetUp]
    public void SetUp()
    {
        _twoFactor = new Mock<ITwoFactorService>();
        _settings = new Mock<ISecuritySettingsRepository>();
        _settings.Setup(s => s.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new SecuritySettings());
        _sessions = new Mock<ISessionService>();
        _users = new Mock<IIdentityUserService>();
        _read = new Mock<IIdentityReadService>();
        _transactions = new PassThroughIdentityTransactions();
        _notices = new Mock<ISecurityNotices>();
        _events = new Mock<ISecurityEventLog>();
        _attempts = new Mock<IAccountAttemptLimiter>();
        _emailCodes = new Mock<IEmailCodeService>();
        _emails = new Mock<IAccountEmailService>();
        _emails.Setup(e => e.GetAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(Account());
        _sender = new Mock<IEmailSender>();
        _sender.SetupGet(s => s.CanSend).Returns(true);
        // The right password, unless a test says otherwise.
        _users.Setup(u => u.VerifyPasswordAsync(Username, CurrentPassword, It.IsAny<CancellationToken>())).ReturnsAsync(IdentityResult.Success);
    }

    private void GivenState(TwoFactorState state) =>
        _twoFactor.Setup(t => t.GetStateAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(state);

    private const long AppCodeStep = 59_000_000;

    // A right code from the new app: matched, then recorded as used with the change.
    private void GivenTheAppsCode(bool recorded = true)
    {
        _twoFactor.Setup(t => t.MatchAuthenticatorCodeAsync(UserId, AuthenticatorCode, It.IsAny<CancellationToken>())).ReturnsAsync(AppCodeStep);
        _twoFactor.Setup(t => t.RecordAuthenticatorStepAsync(UserId, AppCodeStep, It.IsAny<CancellationToken>())).ReturnsAsync(recorded);
    }

    private void AdminsRequired() =>
        _settings.Setup(s => s.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new SecuritySettings { RequireTwoFactorForAdmins = true });

    // GetMyTwoFactorStatus

    private GetMyTwoFactorStatusHandler StatusHandler() => new(_twoFactor.Object, _settings.Object, _emails.Object, _sender.Object);

    [TestCase(false, false, false)]
    [TestCase(true, false, false)]
    [TestCase(true, true, true)]
    public async Task GetStatus_SaysWhetherItsRequired(bool isAdmin, bool adminsRequired, bool required)
    {
        GivenState(State(enabled: true, isAdmin: isAdmin, codesLeft: 7));
        if (adminsRequired)
            AdminsRequired();

        var result = await StatusHandler().Handle(new GetMyTwoFactorStatusQuery(UserId), CancellationToken.None);

        result.Should().Be(new TwoFactorStatusResponse(true, 7, required, "Authenticator", true, null));
    }

    [Test]
    public async Task GetStatus_NoAccount_IsNotFound()
    {
        var act = () => StatusHandler().Handle(new GetMyTwoFactorStatusQuery(UserId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // BeginAuthenticatorSetup

    [Test]
    public async Task BeginSetup_GivesANewKeyAsTextAndAQrCodeOfItsAddress()
    {
        GivenState(State(enabled: false));
        _twoFactor.Setup(t => t.ResetAuthenticatorKeyAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticatorKey(Username, AuthenticatorKeyBase32));
        var qr = new Mock<IQrCodeRenderer>();
        var expectedUri = AuthenticatorUri.Build(Username, AuthenticatorKeyBase32);
        qr.Setup(q => q.Render(expectedUri)).Returns(new QrCodeImage("M0,0h1v1h-1z", 33));

        var result = await new BeginAuthenticatorSetupHandler(_twoFactor.Object, qr.Object)
            .Handle(new BeginAuthenticatorSetupCommand(UserId), CancellationToken.None);

        result.Should().Be(new AuthenticatorSetupResponse(AuthenticatorUri.FormatKey(AuthenticatorKeyBase32), expectedUri, "M0,0h1v1h-1z", 33));
    }

    [Test]
    public async Task BeginSetup_AlreadyOn_IsAConflictAndKeepsTheKey()
    {
        GivenState(State(enabled: true));

        var act = () => new BeginAuthenticatorSetupHandler(_twoFactor.Object, Mock.Of<IQrCodeRenderer>())
            .Handle(new BeginAuthenticatorSetupCommand(UserId), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _twoFactor.Verify(t => t.ResetAuthenticatorKeyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task BeginSetup_NoAccount_IsNotFound()
    {
        var act = () => new BeginAuthenticatorSetupHandler(_twoFactor.Object, Mock.Of<IQrCodeRenderer>())
            .Handle(new BeginAuthenticatorSetupCommand(UserId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task BeginSetup_KeyCouldNotBeMade_IsNotFound()
    {
        GivenState(State(enabled: false));

        var act = () => new BeginAuthenticatorSetupHandler(_twoFactor.Object, Mock.Of<IQrCodeRenderer>())
            .Handle(new BeginAuthenticatorSetupCommand(UserId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // EnableTwoFactor

    private EnableTwoFactorHandler EnableHandler() =>
        new(_twoFactor.Object, _users.Object, _sessions.Object, _transactions, _notices.Object, _events.Object, _emailCodes.Object, _attempts.Object);

    [Test]
    public async Task Enable_RightCode_TurnsItOnEndsOtherSessionsAndReturnsTheCodesOnce()
    {
        GivenState(State(enabled: false));
        GivenTheAppsCode();
        _twoFactor.Setup(t => t.EnableAsync(UserId, TwoFactorMethod.Authenticator, It.IsAny<CancellationToken>())).ReturnsAsync(new RecoveryCodesResult(IdentityResult.Success, Codes));

        var result = await EnableHandler().Handle(new EnableTwoFactorCommand(UserId, CurrentPassword, AuthenticatorCode, SessionId), CancellationToken.None);

        result.RecoveryCodes.Should().Equal(Codes);
        _transactions.Runs.Should().Be(1);
        _sessions.Verify(s => s.RevokeUserSessionsAsync(UserId, SessionId, It.IsAny<CancellationToken>()), Times.Once);
        _notices.Verify(n => n.SendAsync(UserId, SecurityNotice.TwoFactorEnabled, It.IsAny<CancellationToken>(), null, null), Times.Once);
        _events.Verify(e => e.RecordAsync(It.Is<SecurityEvent>(ev => ev.Kind == SecurityEventKind.TwoFactorEnabled), It.IsAny<CancellationToken>()), Times.Once);
        result.ToString().Should().NotContain(Codes[0]);
    }

    [Test]
    public async Task Enable_WrongCode_IsAValidationErrorOnCodeAndChangesNothing()
    {
        GivenState(State(enabled: false));

        var act = () => EnableHandler().Handle(new EnableTwoFactorCommand(UserId, CurrentPassword, AuthenticatorCode, SessionId), CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().ContainSingle(e => e.PropertyName == "Code");
        _twoFactor.Verify(t => t.EnableAsync(It.IsAny<Guid>(), It.IsAny<TwoFactorMethod>(), It.IsAny<CancellationToken>()), Times.Never);
        _twoFactor.Verify(t => t.RecordFailedCodeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Enable_CodeAcceptedMeanwhile_IsAValidationErrorOnCodeAndChangesNothing()
    {
        // Another request used the same code between its match and the change.
        GivenState(State(enabled: false));
        GivenTheAppsCode(recorded: false);

        var act = () => EnableHandler().Handle(new EnableTwoFactorCommand(UserId, CurrentPassword, AuthenticatorCode, SessionId), CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Should()
            .ContainSingle(e => e.PropertyName == "Code" && e.ErrorMessage == TwoFactorCodes.WrongCode);
        _twoFactor.Verify(t => t.EnableAsync(It.IsAny<Guid>(), It.IsAny<TwoFactorMethod>(), It.IsAny<CancellationToken>()), Times.Never);
        _sessions.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Enable_AlreadyOn_IsAConflict()
    {
        GivenState(State(enabled: true));

        var act = () => EnableHandler().Handle(new EnableTwoFactorCommand(UserId, CurrentPassword, AuthenticatorCode, SessionId), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Test]
    public async Task Enable_IdentityRefuses_IsAConflictAndEndsNoSession()
    {
        GivenState(State(enabled: false));
        GivenTheAppsCode();
        _twoFactor.Setup(t => t.EnableAsync(UserId, TwoFactorMethod.Authenticator, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecoveryCodesResult(IdentityResult.Failed(new IdentityError { Description = "No." }), []));

        var act = () => EnableHandler().Handle(new EnableTwoFactorCommand(UserId, CurrentPassword, AuthenticatorCode, SessionId), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _sessions.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Enable_NoAccount_IsNotFound()
    {
        var act = () => EnableHandler().Handle(new EnableTwoFactorCommand(UserId, CurrentPassword, AuthenticatorCode, SessionId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [TestCase("123456", true)]
    [TestCase("123 456", true)]
    [TestCase("12345", false)]
    [TestCase("ABCDE-23456", false)]
    [TestCase("", false)]
    public void EnableValidator_WantsSixDigits(string code, bool valid) =>
        new EnableTwoFactorValidator().Validate(new EnableTwoFactorCommand(UserId, CurrentPassword, code, null)).IsValid.Should().Be(valid);

    // DisableTwoFactor

    private DisableTwoFactorHandler DisableHandler() =>
        new(_twoFactor.Object, _users.Object, _settings.Object, _sessions.Object, _transactions, _notices.Object, _events.Object, _attempts.Object, _emailCodes.Object);

    private static DisableTwoFactorCommand Disable(string code = AuthenticatorCode) => new(UserId, CurrentPassword, code, SessionId);

    [Test]
    public async Task Disable_RightPasswordAndCode_TurnsItOffEndsOtherSessionsAndTellsTheOwner()
    {
        GivenState(State());
        _users.Setup(u => u.VerifyPasswordAsync(Username, CurrentPassword, It.IsAny<CancellationToken>())).ReturnsAsync(IdentityResult.Success);
        _twoFactor.Setup(t => t.VerifyAuthenticatorCodeAsync(UserId, AuthenticatorCode, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _twoFactor.Setup(t => t.DisableAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(IdentityResult.Success);

        await DisableHandler().Handle(Disable(), CancellationToken.None);

        _sessions.Verify(s => s.RevokeUserSessionsAsync(UserId, SessionId, It.IsAny<CancellationToken>()), Times.Once);
        _notices.Verify(n => n.SendAsync(UserId, SecurityNotice.TwoFactorDisabled, It.IsAny<CancellationToken>(), null, null), Times.Once);
        _events.Verify(e => e.RecordAsync(It.Is<SecurityEvent>(ev => ev.Kind == SecurityEventKind.TwoFactorDisabled), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Disable_WithARecoveryCode_UsesItUp()
    {
        GivenState(State());
        _users.Setup(u => u.VerifyPasswordAsync(Username, CurrentPassword, It.IsAny<CancellationToken>())).ReturnsAsync(IdentityResult.Success);
        _twoFactor.Setup(t => t.RedeemRecoveryCodeAsync(UserId, RecoveryCode, It.IsAny<CancellationToken>())).ReturnsAsync(new RecoveryCodeRedemption(true, 9));
        _twoFactor.Setup(t => t.DisableAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(IdentityResult.Success);

        await DisableHandler().Handle(Disable(RecoveryCode), CancellationToken.None);

        _twoFactor.Verify(t => t.DisableAsync(UserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Disable_Required_IsAConflict()
    {
        GivenState(State(isAdmin: true));
        AdminsRequired();

        var act = () => DisableHandler().Handle(Disable(), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _users.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Disable_AlreadyOff_IsAConflict()
    {
        GivenState(State(enabled: false));

        var act = () => DisableHandler().Handle(Disable(), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Test]
    public async Task Disable_WrongPassword_IsAValidationErrorOnPassword()
    {
        GivenState(State());
        _users.Setup(u => u.VerifyPasswordAsync(Username, CurrentPassword, It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Failed(IdentityErrors.PasswordIncorrect));

        var act = () => DisableHandler().Handle(Disable(), CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().ContainSingle(e => e.PropertyName == "Password");
        _twoFactor.Verify(t => t.DisableAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Disable_PasswordLocksTheAccount_IsLockedOut()
    {
        GivenState(State());
        _users.Setup(u => u.VerifyPasswordAsync(Username, CurrentPassword, It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Failed(IdentityErrors.LockedOut));
        _twoFactor.Setup(t => t.LockoutMinutesLeftAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(5);

        var act = () => DisableHandler().Handle(Disable(), CancellationToken.None);

        await act.Should().ThrowAsync<AccountLockedOutException>();
    }

    [Test]
    public async Task Disable_WrongCode_CountsTowardsTheLockoutAndIsAValidationErrorOnCode()
    {
        GivenState(State());
        _users.Setup(u => u.VerifyPasswordAsync(Username, CurrentPassword, It.IsAny<CancellationToken>())).ReturnsAsync(IdentityResult.Success);

        var act = () => DisableHandler().Handle(Disable(), CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().ContainSingle(e => e.PropertyName == "Code");
        _twoFactor.Verify(t => t.RecordFailedCodeAsync(UserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Disable_IdentityRefuses_IsAConflictAndEndsNoSession()
    {
        GivenState(State());
        _users.Setup(u => u.VerifyPasswordAsync(Username, CurrentPassword, It.IsAny<CancellationToken>())).ReturnsAsync(IdentityResult.Success);
        _twoFactor.Setup(t => t.VerifyAuthenticatorCodeAsync(UserId, AuthenticatorCode, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _twoFactor.Setup(t => t.DisableAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(IdentityResult.Failed());

        var act = () => DisableHandler().Handle(Disable(), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _sessions.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Disable_NoAccount_IsNotFound()
    {
        var act = () => DisableHandler().Handle(Disable(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public void DisableValidator_NeedsPasswordAndCode()
    {
        var validator = new DisableTwoFactorValidator();

        validator.Validate(Disable()).IsValid.Should().BeTrue();
        validator.Validate(new DisableTwoFactorCommand(UserId, "", AuthenticatorCode, null)).IsValid.Should().BeFalse();
        validator.Validate(new DisableTwoFactorCommand(UserId, CurrentPassword, "", null)).IsValid.Should().BeFalse();
        validator.Validate(new DisableTwoFactorCommand(UserId, CurrentPassword, new string('1', 100), null)).IsValid.Should().BeFalse();
        Disable().ToString().Should().NotContain(CurrentPassword).And.NotContain(AuthenticatorCode);
    }

    // RegenerateRecoveryCodes

    private RegenerateRecoveryCodesHandler RegenerateHandler() => new(_twoFactor.Object, _users.Object, _notices.Object, _events.Object, _attempts.Object, _emailCodes.Object);

    [Test]
    public async Task Regenerate_RightCode_ReturnsNewCodesAndTellsTheOwner()
    {
        GivenState(State());
        _twoFactor.Setup(t => t.VerifyAuthenticatorCodeAsync(UserId, AuthenticatorCode, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _twoFactor.Setup(t => t.RegenerateRecoveryCodesAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecoveryCodesResult(IdentityResult.Success, Codes));

        var result = await RegenerateHandler().Handle(new RegenerateRecoveryCodesCommand(UserId, CurrentPassword, AuthenticatorCode), CancellationToken.None);

        result.RecoveryCodes.Should().Equal(Codes);
        _notices.Verify(n => n.SendAsync(UserId, SecurityNotice.RecoveryCodesRegenerated, It.IsAny<CancellationToken>(), null, null), Times.Once);
        _events.Verify(e => e.RecordAsync(It.Is<SecurityEvent>(ev => ev.Kind == SecurityEventKind.RecoveryCodesRegenerated), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Regenerate_RecoveryCodeInstead_IsRefusedWithoutUsingItUp()
    {
        GivenState(State());

        var act = () => RegenerateHandler().Handle(new RegenerateRecoveryCodesCommand(UserId, CurrentPassword, RecoveryCode), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        _twoFactor.Verify(t => t.RedeemRecoveryCodeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Regenerate_Off_IsAConflict()
    {
        GivenState(State(enabled: false));

        var act = () => RegenerateHandler().Handle(new RegenerateRecoveryCodesCommand(UserId, CurrentPassword, AuthenticatorCode), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Test]
    public async Task Regenerate_Locked_IsLockedOut()
    {
        GivenState(State());
        _twoFactor.Setup(t => t.LockoutMinutesLeftAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(3);

        var act = () => RegenerateHandler().Handle(new RegenerateRecoveryCodesCommand(UserId, CurrentPassword, AuthenticatorCode), CancellationToken.None);

        await act.Should().ThrowAsync<AccountLockedOutException>();
    }

    [Test]
    public async Task Regenerate_WrongCodeThatLocks_IsLockedOut()
    {
        GivenState(State());
        _twoFactor.Setup(t => t.RecordFailedCodeAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(5);

        var act = () => RegenerateHandler().Handle(new RegenerateRecoveryCodesCommand(UserId, CurrentPassword, AuthenticatorCode), CancellationToken.None);

        await act.Should().ThrowAsync<AccountLockedOutException>();
    }

    [Test]
    public async Task Regenerate_IdentityRefuses_IsAConflict()
    {
        GivenState(State());
        _twoFactor.Setup(t => t.VerifyAuthenticatorCodeAsync(UserId, AuthenticatorCode, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _twoFactor.Setup(t => t.RegenerateRecoveryCodesAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecoveryCodesResult(IdentityResult.Failed(), []));

        var act = () => RegenerateHandler().Handle(new RegenerateRecoveryCodesCommand(UserId, CurrentPassword, AuthenticatorCode), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Test]
    public async Task Regenerate_NoAccount_IsNotFound()
    {
        var act = () => RegenerateHandler().Handle(new RegenerateRecoveryCodesCommand(UserId, CurrentPassword, AuthenticatorCode), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public void RegenerateValidator_WantsSixDigits()
    {
        var validator = new RegenerateRecoveryCodesValidator();

        validator.Validate(new RegenerateRecoveryCodesCommand(UserId, CurrentPassword, AuthenticatorCode)).IsValid.Should().BeTrue();
        validator.Validate(new RegenerateRecoveryCodesCommand(UserId, CurrentPassword, RecoveryCode)).IsValid.Should().BeFalse();
        new RegenerateRecoveryCodesCommand(UserId, CurrentPassword, AuthenticatorCode).ToString().Should().NotContain(AuthenticatorCode);
        new EnableTwoFactorCommand(UserId, CurrentPassword, AuthenticatorCode, null).ToString().Should().NotContain(AuthenticatorCode);
    }

    // ResetUserTwoFactor

    private ResetUserTwoFactorHandler ResetHandler() =>
        new(_read.Object, _twoFactor.Object, _sessions.Object, _transactions, _notices.Object, _events.Object);

    private void GivenUser() =>
        _read.Setup(r => r.GetUserAsync(Username, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserResponse(UserId, Username, ["User"], TwoFactorEnabled: true));

    [Test]
    public async Task Reset_TurnsItOffEndsEverySessionRecordsTheAdminAndTellsTheUser()
    {
        GivenUser();
        _twoFactor.Setup(t => t.DisableAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(IdentityResult.Success);

        var result = await ResetHandler().Handle(new ResetUserTwoFactorCommand(Username, ActorId, "an.admin"), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        _transactions.Runs.Should().Be(1);
        _sessions.Verify(s => s.RevokeUserSessionsAsync(UserId, null, It.IsAny<CancellationToken>()), Times.Once);
        _notices.Verify(n => n.SendAsync(UserId, SecurityNotice.TwoFactorReset, It.IsAny<CancellationToken>(), null, null), Times.Once);
        _events.Verify(e => e.RecordAsync(
            It.Is<SecurityEvent>(ev => ev.Kind == SecurityEventKind.TwoFactorReset && ev.UserId == UserId && ev.ActorUserId == ActorId && ev.ActorUsername == "an.admin"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Reset_UnknownUser_IsUserNotFound()
    {
        var result = await ResetHandler().Handle(new ResetUserTwoFactorCommand(Username, ActorId, "an.admin"), CancellationToken.None);

        result.Errors.Should().ContainSingle(e => e.Code == IdentityErrors.UserNotFoundCode);
    }

    [Test]
    public async Task Reset_OwnAccount_IsRefused()
    {
        GivenUser();

        var result = await ResetHandler().Handle(new ResetUserTwoFactorCommand(Username, UserId, Username), CancellationToken.None);

        result.Errors.Should().ContainSingle(e => e.Code == IdentityErrors.OwnTwoFactorCode);
        _twoFactor.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Reset_IdentityRefuses_ReturnsItsErrorsAndEndsNoSession()
    {
        GivenUser();
        _twoFactor.Setup(t => t.DisableAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "Concurrency" }));

        var result = await ResetHandler().Handle(new ResetUserTwoFactorCommand(Username, ActorId, "an.admin"), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        _sessions.VerifyNoOtherCalls();
        _notices.VerifyNoOtherCalls();
    }

    [Test]
    public void ResetValidator_NeedsAUsername()
    {
        new ResetUserTwoFactorValidator().Validate(new ResetUserTwoFactorCommand(Username, ActorId, "a")).IsValid.Should().BeTrue();
        new ResetUserTwoFactorValidator().Validate(new ResetUserTwoFactorCommand("", ActorId, "a")).IsValid.Should().BeFalse();
    }

    // Common

    [TestCase("123456", true)]
    [TestCase(" 123 456 ", true)]
    [TestCase("1234567", false)]
    [TestCase("12345a", false)]
    [TestCase("ABCDE-23456", false)]
    public void IsAuthenticatorCode_SixDigitsOnly(string code, bool expected) =>
        TwoFactorCodes.IsAuthenticatorCode(code).Should().Be(expected);

    [Test]
    public void Mapper_StatusOff_HasNoCodesLeft() =>
        TwoFactorMapper.ToResponse(State(enabled: false, codesLeft: 4), required: true, EmailCodesAvailability.NoConfirmedAddress)
            .Should().Be(new TwoFactorStatusResponse(false, 0, true, "None", false, EmailCodesAvailability.NoConfirmedAddress));

    [Test]
    public void Mapper_RecoveryCodes_AreReturnedInOrder() =>
        TwoFactorMapper.ToResponse(new RecoveryCodesResult(IdentityResult.Success, Codes)).RecoveryCodes.Should().Equal(Codes);

    // The review of #86: a session alone can't change how someone signs in, codes are limited per account, and admins
    // hear about each other's two-factor.

    [Test]
    public async Task Enable_WrongPassword_IsAValidationErrorOnPasswordAndChecksNoCode()
    {
        GivenState(State(enabled: false));
        _users.Setup(u => u.VerifyPasswordAsync(Username, CurrentPassword, It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Failed(IdentityErrors.PasswordIncorrect));

        var act = () => EnableHandler().Handle(new EnableTwoFactorCommand(UserId, CurrentPassword, AuthenticatorCode, SessionId), CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().ContainSingle(e => e.PropertyName == "Password");
        _twoFactor.Verify(t => t.MatchAuthenticatorCodeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Enable_AnAdmin_TellsTheOtherAdmins()
    {
        GivenState(State(enabled: false, isAdmin: true));
        GivenTheAppsCode();
        _twoFactor.Setup(t => t.EnableAsync(UserId, TwoFactorMethod.Authenticator, It.IsAny<CancellationToken>())).ReturnsAsync(new RecoveryCodesResult(IdentityResult.Success, Codes));

        await EnableHandler().Handle(new EnableTwoFactorCommand(UserId, CurrentPassword, AuthenticatorCode, SessionId), CancellationToken.None);

        _notices.Verify(n => n.SendToOtherAdminsAsync(SecurityNotice.AdminTwoFactorEnabled, UserId, Username, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Disable_AnAdmin_TellsTheOtherAdmins()
    {
        GivenState(State(isAdmin: true));
        _twoFactor.Setup(t => t.VerifyAuthenticatorCodeAsync(UserId, AuthenticatorCode, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _twoFactor.Setup(t => t.DisableAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(IdentityResult.Success);

        await DisableHandler().Handle(Disable(), CancellationToken.None);

        _notices.Verify(n => n.SendToOtherAdminsAsync(SecurityNotice.AdminTwoFactorRemoved, UserId, Username, It.IsAny<CancellationToken>()), Times.Once);
        _attempts.Verify(a => a.Attempt(AccountAttempt.TwoFactorCode, UserId), Times.Once);
    }

    [Test]
    public async Task Reset_AnAdmin_TellsTheOtherAdmins()
    {
        _read.Setup(r => r.GetUserAsync(Username, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserResponse(UserId, Username, ["Admin", "User"], TwoFactorEnabled: true));
        _twoFactor.Setup(t => t.DisableAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(IdentityResult.Success);

        await ResetHandler().Handle(new ResetUserTwoFactorCommand(Username, ActorId, "an.admin"), CancellationToken.None);

        _notices.Verify(n => n.SendToOtherAdminsAsync(SecurityNotice.AdminTwoFactorRemoved, UserId, Username, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Regenerate_WrongPassword_ChecksNoCode()
    {
        GivenState(State());
        _users.Setup(u => u.VerifyPasswordAsync(Username, CurrentPassword, It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Failed(IdentityErrors.PasswordIncorrect));

        var act = () => RegenerateHandler().Handle(new RegenerateRecoveryCodesCommand(UserId, CurrentPassword, AuthenticatorCode), CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().ContainSingle(e => e.PropertyName == "Password");
        _attempts.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Regenerate_OverTheAccountsLimit_ChecksNoCode()
    {
        GivenState(State());
        _attempts.Setup(a => a.Attempt(AccountAttempt.TwoFactorCode, UserId)).Throws(new TooManyAttemptsException("Too many.", 600));

        var act = () => RegenerateHandler().Handle(new RegenerateRecoveryCodesCommand(UserId, CurrentPassword, AuthenticatorCode), CancellationToken.None);

        (await act.Should().ThrowAsync<TooManyAttemptsException>()).Which.RetryAfterSeconds.Should().Be(600);
        _twoFactor.Verify(t => t.VerifyAuthenticatorCodeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Regenerate_WrongCodeThatLocks_TellsTheHolder()
    {
        GivenState(State());
        _twoFactor.Setup(t => t.RecordFailedCodeAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(10);

        var act = () => RegenerateHandler().Handle(new RegenerateRecoveryCodesCommand(UserId, CurrentPassword, AuthenticatorCode), CancellationToken.None);

        (await act.Should().ThrowAsync<AccountLockedOutException>()).Which.MinutesLeft.Should().Be(10);
        _notices.Verify(n => n.SendAsync(UserId, SecurityNotice.TwoFactorCodesLockedOut, It.IsAny<CancellationToken>(), null, null), Times.Once);
    }

    [Test]
    public void Validators_NeedThePassword()
    {
        new EnableTwoFactorValidator().Validate(new EnableTwoFactorCommand(UserId, "", AuthenticatorCode, null)).IsValid.Should().BeFalse();
        new RegenerateRecoveryCodesValidator().Validate(new RegenerateRecoveryCodesCommand(UserId, "", AuthenticatorCode)).IsValid.Should().BeFalse();
        new EnableTwoFactorCommand(UserId, CurrentPassword, AuthenticatorCode, null).ToString().Should().NotContain(CurrentPassword);
        new RegenerateRecoveryCodesCommand(UserId, CurrentPassword, AuthenticatorCode).ToString().Should().NotContain(CurrentPassword);
    }
}
