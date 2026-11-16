using MedApp.Application.Authentication.TwoFactor.Common;
using MedApp.Application.Authentication.TwoFactor.Repositories;
using FluentAssertions;
using MedApp.Application.Common.Identity;
using MedApp.Infrastructure.Common.Identity;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Identity;
using Moq;
using static MedApp.UnitTests.Common.Constants.EmailTestConstants;

namespace MedApp.UnitTests.Infrastructure.Common.Identity;

/// <summary>
/// Two-factor on Identity, with the TOTP checked at a fixed time: codes from one step either side, each accepted once.
/// </summary>
[TestFixture]
public sealed class TwoFactorServiceTests
{
    private Mock<UserManager<ApplicationUser>> _userManager = null!;
    private ApplicationUser _user = null!;
    private FixedTimeProvider _clock = null!;
    private TwoFactorService _service = null!;
    private Mock<IAccountLockoutRepository> _lockouts = null!;
    private string? _lastStep;

    private static readonly byte[] KeyBytes = Base32.Decode(AuthenticatorKeyBase32);

    [SetUp]
    public void SetUp()
    {
        _userManager = new Mock<UserManager<ApplicationUser>>(
            new Mock<IUserStore<ApplicationUser>>().Object, null!, null!, null!, null!, null!, null!, null!, null!);
        _user = new ApplicationUser { Id = UserId, UserName = Username, TwoFactorEnabled = true };
        _userManager.Setup(u => u.FindByIdAsync(UserId.ToString())).ReturnsAsync(_user);
        _userManager.Setup(u => u.GetAuthenticatorKeyAsync(_user)).ReturnsAsync(AuthenticatorKeyBase32);
        _userManager.Setup(u => u.GetRolesAsync(_user)).ReturnsAsync(["User"]);
        _lastStep = null;
        _userManager
            .Setup(u => u.GetAuthenticationTokenAsync(_user, TwoFactorService.TokenProvider, TwoFactorService.LastTimeStepToken))
            .ReturnsAsync(() => _lastStep);
        _userManager
            .Setup(u => u.SetAuthenticationTokenAsync(_user, TwoFactorService.TokenProvider, TwoFactorService.LastTimeStepToken, It.IsAny<string>()))
            .Callback<ApplicationUser, string, string, string?>((_, _, _, value) => _lastStep = value)
            .ReturnsAsync(IdentityResult.Success);
        _clock = new FixedTimeProvider(NowUtc);
        _lockouts = new Mock<IAccountLockoutRepository>();
        _service = new TwoFactorService(_userManager.Object, _lockouts.Object, _clock);
    }

    private static string CodeAt(DateTime utc, int stepsAway = 0) =>
        Totp.Compute(KeyBytes, Totp.TimeStep(new DateTimeOffset(utc)) + stepsAway);

    [Test]
    public async Task Verify_CurrentCode_IsAcceptedAndItsStepRecorded()
    {
        (await _service.VerifyAuthenticatorCodeAsync(UserId, CodeAt(NowUtc), CancellationToken.None)).Should().BeTrue();

        _lastStep.Should().Be(Totp.TimeStep(new DateTimeOffset(NowUtc)).ToString());
    }

    [Test]
    public async Task Verify_SameCodeTwice_IsAcceptedOnlyOnce()
    {
        var code = CodeAt(NowUtc);

        (await _service.VerifyAuthenticatorCodeAsync(UserId, code, CancellationToken.None)).Should().BeTrue();
        (await _service.VerifyAuthenticatorCodeAsync(UserId, code, CancellationToken.None)).Should().BeFalse();
    }

    [Test]
    public async Task Verify_EarlierCodeAfterALaterOne_IsRefused()
    {
        (await _service.VerifyAuthenticatorCodeAsync(UserId, CodeAt(NowUtc, +1), CancellationToken.None)).Should().BeTrue();

        (await _service.VerifyAuthenticatorCodeAsync(UserId, CodeAt(NowUtc), CancellationToken.None)).Should().BeFalse();
    }

    [Test]
    public async Task Verify_NextStepsCode_IsAcceptedAfterThisOnes()
    {
        (await _service.VerifyAuthenticatorCodeAsync(UserId, CodeAt(NowUtc), CancellationToken.None)).Should().BeTrue();

        (await _service.VerifyAuthenticatorCodeAsync(UserId, CodeAt(NowUtc, +1), CancellationToken.None)).Should().BeTrue();
    }

    [Test]
    public async Task Verify_CodeFromAMinuteAndAHalfAgo_IsRefused() =>
        (await _service.VerifyAuthenticatorCodeAsync(UserId, CodeAt(NowUtc.AddSeconds(-90)), CancellationToken.None)).Should().BeFalse();

    [Test]
    public async Task Verify_ConcurrentUseLosesTheSave_IsRefused()
    {
        _userManager
            .Setup(u => u.SetAuthenticationTokenAsync(_user, TwoFactorService.TokenProvider, TwoFactorService.LastTimeStepToken, It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityErrorDescriber().ConcurrencyFailure()));

        (await _service.VerifyAuthenticatorCodeAsync(UserId, CodeAt(NowUtc), CancellationToken.None)).Should().BeFalse();
    }

    [Test]
    public async Task Verify_NoKeyOrNoUserOrBadKey_IsRefused()
    {
        _userManager.Setup(u => u.GetAuthenticatorKeyAsync(_user)).ReturnsAsync((string?)null);
        (await _service.VerifyAuthenticatorCodeAsync(UserId, CodeAt(NowUtc), CancellationToken.None)).Should().BeFalse();

        _userManager.Setup(u => u.GetAuthenticatorKeyAsync(_user)).ReturnsAsync("not base32!");
        (await _service.VerifyAuthenticatorCodeAsync(UserId, CodeAt(NowUtc), CancellationToken.None)).Should().BeFalse();

        (await _service.VerifyAuthenticatorCodeAsync(Guid.NewGuid(), CodeAt(NowUtc), CancellationToken.None)).Should().BeFalse();
    }

    [Test]
    public async Task GetState_SaysWhetherAdminAndHowManyCodesAreLeft()
    {
        _userManager.Setup(u => u.GetRolesAsync(_user)).ReturnsAsync(["admin"]);
        _userManager.Setup(u => u.CountRecoveryCodesAsync(_user)).ReturnsAsync(8);

        var state = await _service.GetStateAsync(UserId, CancellationToken.None);

        state.Should().Be(new TwoFactorState(Username, true, 8, true));
        (await _service.GetStateAsync(Guid.NewGuid(), CancellationToken.None)).Should().BeNull();
    }

    [Test]
    public async Task GetState_Off_HasNoCodesLeft()
    {
        _user.TwoFactorEnabled = false;

        (await _service.GetStateAsync(UserId, CancellationToken.None))!.RecoveryCodesLeft.Should().Be(0);
        _userManager.Verify(u => u.CountRecoveryCodesAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Test]
    public async Task IsEnabled_ReadsTheFlag()
    {
        (await _service.IsEnabledAsync(UserId, CancellationToken.None)).Should().BeTrue();
        (await _service.IsEnabledAsync(Guid.NewGuid(), CancellationToken.None)).Should().BeFalse();
    }

    [Test]
    public async Task ResetKey_WhileOff_GivesANewKeyAndForgetsTheLastStep()
    {
        _user.TwoFactorEnabled = false;
        _userManager.Setup(u => u.ResetAuthenticatorKeyAsync(_user)).ReturnsAsync(IdentityResult.Success);

        var key = await _service.ResetAuthenticatorKeyAsync(UserId, CancellationToken.None);

        key.Should().Be(new AuthenticatorKey(Username, AuthenticatorKeyBase32));
        _userManager.Verify(u => u.RemoveAuthenticationTokenAsync(_user, TwoFactorService.TokenProvider, TwoFactorService.LastTimeStepToken), Times.Once);
    }

    [Test]
    public async Task ResetKey_WhileOnOrForNobody_GivesNone()
    {
        (await _service.ResetAuthenticatorKeyAsync(UserId, CancellationToken.None)).Should().BeNull();
        (await _service.ResetAuthenticatorKeyAsync(Guid.NewGuid(), CancellationToken.None)).Should().BeNull();
        _userManager.Verify(u => u.ResetAuthenticatorKeyAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Test]
    public async Task ResetKey_IdentityRefusesOrHasNoKey_GivesNone()
    {
        _user.TwoFactorEnabled = false;
        _userManager.Setup(u => u.ResetAuthenticatorKeyAsync(_user)).ReturnsAsync(IdentityResult.Failed());
        (await _service.ResetAuthenticatorKeyAsync(UserId, CancellationToken.None)).Should().BeNull();

        _userManager.Setup(u => u.ResetAuthenticatorKeyAsync(_user)).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(u => u.GetAuthenticatorKeyAsync(_user)).ReturnsAsync((string?)null);
        (await _service.ResetAuthenticatorKeyAsync(UserId, CancellationToken.None)).Should().BeNull();
    }

    [Test]
    public async Task RedeemRecoveryCode_NormalisesWhatWasTypedAndCountsWhatsLeft()
    {
        _userManager.Setup(u => u.RedeemTwoFactorRecoveryCodeAsync(_user, RecoveryCode)).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(u => u.CountRecoveryCodesAsync(_user)).ReturnsAsync(9);

        var redeemed = await _service.RedeemRecoveryCodeAsync(UserId, " abcde-2 3456 ", CancellationToken.None);

        redeemed.Should().Be(new RecoveryCodeRedemption(true, 9));
    }

    [Test]
    public async Task RedeemRecoveryCode_WrongEmptyOffOrNobody_Fails()
    {
        _userManager.Setup(u => u.RedeemTwoFactorRecoveryCodeAsync(_user, It.IsAny<string>())).ReturnsAsync(IdentityResult.Failed());

        (await _service.RedeemRecoveryCodeAsync(UserId, RecoveryCode, CancellationToken.None)).Succeeded.Should().BeFalse();
        (await _service.RedeemRecoveryCodeAsync(UserId, "  ", CancellationToken.None)).Succeeded.Should().BeFalse();
        (await _service.RedeemRecoveryCodeAsync(Guid.NewGuid(), RecoveryCode, CancellationToken.None)).Succeeded.Should().BeFalse();
        _user.TwoFactorEnabled = false;
        (await _service.RedeemRecoveryCodeAsync(UserId, RecoveryCode, CancellationToken.None)).Succeeded.Should().BeFalse();
    }

    [Test]
    public async Task Enable_MakesTenCodesThenTurnsItOn()
    {
        var codes = Enumerable.Range(0, 10).Select(i => $"CODE{i}").ToList();
        _userManager.Setup(u => u.GenerateNewTwoFactorRecoveryCodesAsync(_user, TwoFactorService.RecoveryCodeCount)).ReturnsAsync(codes);
        _userManager.Setup(u => u.SetTwoFactorEnabledAsync(_user, true)).ReturnsAsync(IdentityResult.Success);

        var result = await _service.EnableAsync(UserId, CancellationToken.None);

        result.Result.Succeeded.Should().BeTrue();
        result.Codes.Should().Equal(codes);
    }

    [Test]
    public async Task Enable_Failures_ReturnNoCodes()
    {
        (await _service.EnableAsync(Guid.NewGuid(), CancellationToken.None)).Result.Succeeded.Should().BeFalse();

        _userManager.Setup(u => u.GenerateNewTwoFactorRecoveryCodesAsync(_user, It.IsAny<int>())).ReturnsAsync((IEnumerable<string>?)null);
        (await _service.EnableAsync(UserId, CancellationToken.None)).Result.Succeeded.Should().BeFalse();

        _userManager.Setup(u => u.GenerateNewTwoFactorRecoveryCodesAsync(_user, It.IsAny<int>())).ReturnsAsync(["A"]);
        _userManager.Setup(u => u.SetTwoFactorEnabledAsync(_user, true)).ReturnsAsync(IdentityResult.Failed());
        var refused = await _service.EnableAsync(UserId, CancellationToken.None);
        refused.Result.Succeeded.Should().BeFalse();
        refused.Codes.Should().BeEmpty();
    }

    [Test]
    public async Task Regenerate_ReturnsNewCodesOrFails()
    {
        _userManager.Setup(u => u.GenerateNewTwoFactorRecoveryCodesAsync(_user, TwoFactorService.RecoveryCodeCount)).ReturnsAsync(["A", "B"]);
        (await _service.RegenerateRecoveryCodesAsync(UserId, CancellationToken.None)).Codes.Should().Equal("A", "B");

        _userManager.Setup(u => u.GenerateNewTwoFactorRecoveryCodesAsync(_user, It.IsAny<int>())).ReturnsAsync((IEnumerable<string>?)null);
        (await _service.RegenerateRecoveryCodesAsync(UserId, CancellationToken.None)).Result.Succeeded.Should().BeFalse();
        (await _service.RegenerateRecoveryCodesAsync(Guid.NewGuid(), CancellationToken.None)).Result.Succeeded.Should().BeFalse();
    }

    [Test]
    public async Task Disable_TurnsItOffAndForgetsTheKeyAndCodes()
    {
        _userManager.Setup(u => u.SetTwoFactorEnabledAsync(_user, false)).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(u => u.ResetAuthenticatorKeyAsync(_user)).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(u => u.RemoveAuthenticationTokenAsync(_user, It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);

        var result = await _service.DisableAsync(UserId, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        _userManager.Verify(u => u.ResetAuthenticatorKeyAsync(_user), Times.Once);
        _userManager.Verify(u => u.RemoveAuthenticationTokenAsync(_user, "[AspNetUserStore]", "RecoveryCodes"), Times.Once);
        _userManager.Verify(u => u.RemoveAuthenticationTokenAsync(_user, TwoFactorService.TokenProvider, TwoFactorService.LastTimeStepToken), Times.Once);
    }

    [Test]
    public async Task Disable_StopsAtTheFirstFailure()
    {
        (await _service.DisableAsync(Guid.NewGuid(), CancellationToken.None)).Errors.Should().ContainSingle(e => e.Code == IdentityErrors.UserNotFoundCode);

        _userManager.Setup(u => u.SetTwoFactorEnabledAsync(_user, false)).ReturnsAsync(IdentityResult.Failed());
        (await _service.DisableAsync(UserId, CancellationToken.None)).Succeeded.Should().BeFalse();
        _userManager.Verify(u => u.ResetAuthenticatorKeyAsync(It.IsAny<ApplicationUser>()), Times.Never);

        _userManager.Setup(u => u.SetTwoFactorEnabledAsync(_user, false)).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(u => u.ResetAuthenticatorKeyAsync(_user)).ReturnsAsync(IdentityResult.Failed());
        (await _service.DisableAsync(UserId, CancellationToken.None)).Succeeded.Should().BeFalse();

        _userManager.Setup(u => u.ResetAuthenticatorKeyAsync(_user)).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(u => u.RemoveAuthenticationTokenAsync(_user, "[AspNetUserStore]", "RecoveryCodes")).ReturnsAsync(IdentityResult.Failed());
        (await _service.DisableAsync(UserId, CancellationToken.None)).Succeeded.Should().BeFalse();
    }

    [Test]
    public async Task Lockout_IsReadCountedAndCleared()
    {
        _userManager.Setup(u => u.IsLockedOutAsync(_user)).ReturnsAsync(false);
        (await _service.LockoutMinutesLeftAsync(UserId, CancellationToken.None)).Should().BeNull();
        (await _service.RecordFailedCodeAsync(UserId, CancellationToken.None)).Should().BeNull();
        _lockouts.Verify(l => l.RecordFailedCodeAsync(
            UserId, _userManager.Object.Options.Lockout.MaxFailedAccessAttempts, _userManager.Object.Options.Lockout.DefaultLockoutTimeSpan,
            new DateTimeOffset(NowUtc), It.IsAny<CancellationToken>()), Times.Once);
        // Never Identity's read-then-write count, which loses counts to requests that arrive together.
        _userManager.Verify(u => u.AccessFailedAsync(It.IsAny<ApplicationUser>()), Times.Never);

        _userManager.Setup(u => u.IsLockedOutAsync(_user)).ReturnsAsync(true);
        _userManager.Setup(u => u.GetLockoutEndDateAsync(_user)).ReturnsAsync(new DateTimeOffset(NowUtc.AddSeconds(90)));
        (await _service.LockoutMinutesLeftAsync(UserId, CancellationToken.None)).Should().Be(2);
        _lockouts.Setup(l => l.RecordFailedCodeAsync(UserId, It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TimeSpan.FromSeconds(90));
        (await _service.RecordFailedCodeAsync(UserId, CancellationToken.None)).Should().Be(2);

        _userManager.Setup(u => u.GetLockoutEndDateAsync(_user)).ReturnsAsync((DateTimeOffset?)null);
        (await _service.LockoutMinutesLeftAsync(UserId, CancellationToken.None)).Should().Be(1);

        await _service.RecordSignInAsync(UserId, CancellationToken.None);
        _lockouts.Verify(l => l.ResetAsync(UserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Lockout_NoUser_DoesNothing()
    {
        var nobody = Guid.NewGuid();

        (await _service.LockoutMinutesLeftAsync(nobody, CancellationToken.None)).Should().BeNull();
        (await _service.RecordFailedCodeAsync(nobody, CancellationToken.None)).Should().BeNull();
    }

    [Test]
    public async Task AdminsWithoutTwoFactor_NamesOnlyThoseWithItOff()
    {
        _userManager.Setup(u => u.GetUsersInRoleAsync(FirstAdminSetup.AdminRole)).ReturnsAsync(
        [
            new ApplicationUser { UserName = "zed", TwoFactorEnabled = false },
            new ApplicationUser { UserName = "on", TwoFactorEnabled = true },
            new ApplicationUser { UserName = "amy", TwoFactorEnabled = false },
        ]);

        (await _service.GetAdminsWithoutTwoFactorAsync(CancellationToken.None)).Should().Equal("amy", "zed");
    }

    [Test]
    public async Task AdminContacts_OnlyConfirmedAddresses()
    {
        var withAddress = new ApplicationUser { Id = Guid.NewGuid(), UserName = "a", Email = Address, EmailConfirmed = true };
        _userManager.Setup(u => u.GetUsersInRoleAsync(FirstAdminSetup.AdminRole)).ReturnsAsync(
        [
            withAddress,
            new ApplicationUser { UserName = "b", Email = OtherAddress, EmailConfirmed = false },
            new ApplicationUser { UserName = "c" },
        ]);

        var contacts = await _service.GetAdminContactsAsync(CancellationToken.None);

        contacts.Should().ContainSingle().Which.Should().Be(new AccountContact(withAddress.Id, "a", Address));
        contacts[0].ToString().Should().NotContain(Address);
    }

    [Test]
    public async Task FailedSave_IsForgottenSoALaterSaveCantThrow()
    {
        _userManager
            .Setup(u => u.SetAuthenticationTokenAsync(_user, TwoFactorService.TokenProvider, TwoFactorService.LastTimeStepToken, It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "ConcurrencyFailure" }));

        (await _service.VerifyAuthenticatorCodeAsync(UserId, CodeAt(NowUtc), CancellationToken.None)).Should().BeFalse();

        _lockouts.Verify(l => l.Forget(UserId), Times.Once);
    }

    [Test]
    public async Task FailedRedeem_IsForgottenAndTheUserReadAgain()
    {
        _userManager.Setup(u => u.RedeemTwoFactorRecoveryCodeAsync(_user, "WRONG")).ReturnsAsync(IdentityResult.Failed());
        _userManager.Setup(u => u.CountRecoveryCodesAsync(_user)).ReturnsAsync(4);

        (await _service.RedeemRecoveryCodeAsync(UserId, "wrong", CancellationToken.None)).Should().Be(new RecoveryCodeRedemption(false, 4));

        _lockouts.Verify(l => l.Forget(UserId), Times.Once);
        _userManager.Verify(u => u.FindByIdAsync(UserId.ToString()), Times.Exactly(2));
    }

    [TestCase(0, 15)]
    [TestCase(1, 30)]
    [TestCase(3, 120)]
    [TestCase(20, 1440)]
    public void CodeLockouts_DoubleEachTimeUpToADay(int previous, int minutes) =>
        CodeLockouts.Duration(TimeSpan.FromMinutes(15), previous).Should().Be(TimeSpan.FromMinutes(minutes));

    [TestCase(" abcde-23456 ", "ABCDE-23456")]
    [TestCase("abcde 23456", "ABCDE23456")]
    public void NormaliseRecoveryCode_UpperCaseWithoutSpaces(string typed, string expected) =>
        TwoFactorService.NormaliseRecoveryCode(typed).Should().Be(expected);
}
