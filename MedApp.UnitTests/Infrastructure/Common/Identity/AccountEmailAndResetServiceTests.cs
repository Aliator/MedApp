using FluentAssertions;
using MedApp.Application.Common.Identity;
using MedApp.Infrastructure.Common.Identity;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Moq;
using static MedApp.UnitTests.Common.Constants.EmailTestConstants;

namespace MedApp.UnitTests.Infrastructure.Common.Identity;

[TestFixture]
public sealed class AccountEmailAndResetServiceTests
{
    private Mock<UserManager<ApplicationUser>> _userManager = null!;
    private ApplicationUser _user = null!;
    private IdentityOptions _options = null!;

    [SetUp]
    public void SetUp()
    {
        _userManager = new Mock<UserManager<ApplicationUser>>(
            new Mock<IUserStore<ApplicationUser>>().Object, null!, null!, null!, null!, null!, null!, null!, null!);
        _options = new IdentityOptions();
        _options.Tokens.PasswordResetTokenProvider = PasswordResetTokenProvider.ProviderName;
        _userManager.Object.Options = _options;
        _user = new ApplicationUser { Id = UserId, UserName = Username, Email = Address, EmailConfirmed = true };
        _userManager.Setup(u => u.FindByIdAsync(UserId.ToString())).ReturnsAsync(_user);
        _userManager.Setup(u => u.UpdateAsync(_user)).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(u => u.Users).Returns(AsyncQueryable.Of(Array.Empty<ApplicationUser>()));
        _userManager.Setup(u => u.NormalizeEmail(It.IsAny<string>())).Returns<string>(e => e.ToUpperInvariant());
    }

    private AccountEmailService Emails() => new(_userManager.Object);

    private PasswordResetService Resets() => new(_userManager.Object, Options.Create(_options));

    // AccountEmailService

    [Test]
    public async Task Get_OnlyAConfirmedAddressCountsAsTheAccounts()
    {
        (await Emails().GetAsync(UserId, CancellationToken.None)).Should().Be(new AccountEmail(UserId, Username, Address, null));

        _user.EmailConfirmed = false;
        (await Emails().GetAsync(UserId, CancellationToken.None))!.Email.Should().BeNull();
        (await Emails().GetAsync(Guid.NewGuid(), CancellationToken.None)).Should().BeNull();
    }

    [Test]
    public async Task RequestChange_NewAddress_MakesItPendingAndReturnsAToken()
    {
        _userManager.Setup(u => u.GenerateChangeEmailTokenAsync(_user, OtherAddress)).ReturnsAsync(Token);

        var change = await Emails().RequestChangeAsync(UserId, OtherAddress, CancellationToken.None);

        change.Should().Be(new EmailChangeRequest(Username, OtherAddress, Token));
        _user.PendingEmail.Should().Be(OtherAddress);
        _user.Email.Should().Be(Address, "the old one stays in use until the new one is confirmed");
    }

    [Test]
    public async Task RequestChange_ConfirmedAddressAgain_DropsThePendingOneAndNeedsNoToken()
    {
        _user.PendingEmail = OtherAddress;

        var change = await Emails().RequestChangeAsync(UserId, Address.ToUpperInvariant(), CancellationToken.None);

        change!.Token.Should().BeNull();
        _user.PendingEmail.Should().BeNull();
        _userManager.Verify(u => u.UpdateAsync(_user), Times.Once);
    }

    [Test]
    public async Task RequestChange_ConfirmedAddressAgainWithNothingPending_SavesNothing()
    {
        await Emails().RequestChangeAsync(UserId, Address, CancellationToken.None);

        _userManager.Verify(u => u.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Test]
    public async Task RequestChange_NoAccountOrSaveFails_GivesNone()
    {
        (await Emails().RequestChangeAsync(Guid.NewGuid(), OtherAddress, CancellationToken.None)).Should().BeNull();

        _userManager.Setup(u => u.UpdateAsync(_user)).ReturnsAsync(IdentityResult.Failed());
        (await Emails().RequestChangeAsync(UserId, OtherAddress, CancellationToken.None)).Should().BeNull();
    }

    [Test]
    public async Task Confirm_ValidToken_UsesThePendingAddressAndReturnsTheOldOne()
    {
        _user.PendingEmail = OtherAddress;
        string? pendingWhenSaved = "unset";
        _userManager.Setup(u => u.ChangeEmailAsync(_user, OtherAddress, Token))
            .Callback(() => pendingWhenSaved = _user.PendingEmail)
            .ReturnsAsync(IdentityResult.Success);

        var confirmation = await Emails().ConfirmAsync(UserId, Token, CancellationToken.None);

        confirmation.Should().Be(new EmailConfirmation(IdentityResult.Success, Username, Address));
        pendingWhenSaved.Should().BeNull("the pending address is cleared in the same save");
    }

    [Test]
    public async Task Confirm_FirstAddress_HasNoOldOne()
    {
        _user.Email = null;
        _user.EmailConfirmed = false;
        _user.PendingEmail = OtherAddress;
        _userManager.Setup(u => u.ChangeEmailAsync(_user, OtherAddress, Token)).ReturnsAsync(IdentityResult.Success);

        (await Emails().ConfirmAsync(UserId, Token, CancellationToken.None)).PreviousEmail.Should().BeNull();
    }

    [Test]
    public async Task Confirm_BadTokenNothingPendingOrNobody_IsInvalidToken()
    {
        (await Emails().ConfirmAsync(UserId, Token, CancellationToken.None)).Result.Errors.Should().ContainSingle(e => e.Code == IdentityErrors.InvalidTokenCode);
        (await Emails().ConfirmAsync(Guid.NewGuid(), Token, CancellationToken.None)).Result.Succeeded.Should().BeFalse();

        _user.PendingEmail = OtherAddress;
        _userManager.Setup(u => u.ChangeEmailAsync(_user, OtherAddress, Token)).ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "InvalidToken" }));
        (await Emails().ConfirmAsync(UserId, Token, CancellationToken.None)).Result.Errors.Should().ContainSingle(e => e.Code == IdentityErrors.InvalidTokenCode);
        _user.PendingEmail.Should().Be(OtherAddress);
    }

    [Test]
    public async Task Remove_ClearsBothAndReturnsWhatThereWas()
    {
        _user.PendingEmail = OtherAddress;
        _userManager.Setup(u => u.SetEmailAsync(_user, null)).ReturnsAsync(IdentityResult.Success);

        var removed = await Emails().RemoveAsync(UserId, CancellationToken.None);

        removed.Should().Be(new AccountEmail(UserId, Username, Address, OtherAddress));
        _user.PendingEmail.Should().BeNull();
        _userManager.Verify(u => u.SetEmailAsync(_user, null), Times.Once);
    }

    [Test]
    public async Task Remove_NothingThere_SavesNothing()
    {
        _user.Email = null;

        await Emails().RemoveAsync(UserId, CancellationToken.None);

        _userManager.Verify(u => u.SetEmailAsync(It.IsAny<ApplicationUser>(), It.IsAny<string?>()), Times.Never);
        (await Emails().RemoveAsync(Guid.NewGuid(), CancellationToken.None)).Should().BeNull();
    }

    // PasswordResetService

    [Test]
    public async Task FindTargets_ByUsername_WithAConfirmedAddress()
    {
        _userManager.Setup(u => u.FindByNameAsync(Username)).ReturnsAsync(_user);
        _userManager.Setup(u => u.GeneratePasswordResetTokenAsync(_user)).ReturnsAsync(Token);

        var targets = await Resets().FindTargetsAsync(Username, CancellationToken.None);

        targets.Should().ContainSingle().Which.Should().Be(new PasswordResetTarget(UserId, Username, Address, Token));
    }

    [Test]
    public async Task FindTargets_ByUsernameWithoutAConfirmedAddress_FindsNone()
    {
        _user.EmailConfirmed = false;
        _userManager.Setup(u => u.FindByNameAsync(Username)).ReturnsAsync(_user);

        (await Resets().FindTargetsAsync(Username, CancellationToken.None)).Should().BeEmpty();
        _userManager.Verify(u => u.GeneratePasswordResetTokenAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Test]
    public async Task FindTargets_ByAddress_EveryConfirmedAccountWithIt()
    {
        var second = new ApplicationUser { Id = Guid.NewGuid(), UserName = "second", Email = Address, NormalizedEmail = Address.ToUpperInvariant(), EmailConfirmed = true };
        var unconfirmed = new ApplicationUser { Id = Guid.NewGuid(), UserName = "third", Email = Address, NormalizedEmail = Address.ToUpperInvariant() };
        _user.NormalizedEmail = Address.ToUpperInvariant();
        _userManager.Setup(u => u.Users).Returns(AsyncQueryable.Of([_user, second, unconfirmed]));
        _userManager.Setup(u => u.GeneratePasswordResetTokenAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(Token);

        var targets = await Resets().FindTargetsAsync(Address, CancellationToken.None);

        targets.Select(t => t.Username).Should().BeEquivalentTo(Username, "second");
    }

    [Test]
    public async Task FindTargets_NamesNoAccountCouldHave_AreNotLookedUp()
    {
        _options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyz.";

        var targets = await Resets().FindTargetsAsync("﻿" + Username, CancellationToken.None);

        targets.Should().BeEmpty();
        _userManager.Verify(u => u.FindByNameAsync(It.IsAny<string>()), Times.Never);
    }

    [TestCase(Address, true)]
    [TestCase("no-at-sign", false)]
    [TestCase("ｓynthetic@example.test", false)]
    [TestCase("with space@example.test", false)]
    public void CouldBeEmail_OnlyPrintableAsciiWithAnAt(string value, bool expected) =>
        PasswordResetService.CouldBeEmail(value).Should().Be(expected);

    [Test]
    public async Task Reset_ValidToken_SetsThePasswordClearsTheFlagAndEndsTheLockout()
    {
        _user.MustChangePassword = true;
        bool? flagWhenSaved = null;
        _userManager.Setup(u => u.VerifyUserTokenAsync(_user, PasswordResetTokenProvider.ProviderName, UserManager<ApplicationUser>.ResetPasswordTokenPurpose, Token)).ReturnsAsync(true);
        _userManager.Setup(u => u.ResetPasswordAsync(_user, Token, NewPassword))
            .Callback(() => flagWhenSaved = _user.MustChangePassword)
            .ReturnsAsync(IdentityResult.Success);

        var result = await Resets().ResetAsync(UserId, Token, NewPassword, CancellationToken.None);

        result.Should().Be(new PasswordResetResult(IdentityResult.Success, Username));
        flagWhenSaved.Should().BeFalse();
        _userManager.Verify(u => u.SetLockoutEndDateAsync(_user, null), Times.Once);
        _userManager.Verify(u => u.ResetAccessFailedCountAsync(_user), Times.Once);
    }

    [Test]
    public async Task Reset_InvalidTokenOrNobody_IsInvalidTokenWithoutCheckingThePassword()
    {
        (await Resets().ResetAsync(Guid.NewGuid(), Token, NewPassword, CancellationToken.None)).Result.Errors
            .Should().ContainSingle(e => e.Code == IdentityErrors.InvalidTokenCode);
        (await Resets().ResetAsync(UserId, Token, NewPassword, CancellationToken.None)).Result.Errors
            .Should().ContainSingle(e => e.Code == IdentityErrors.InvalidTokenCode);

        _userManager.Verify(u => u.CheckPasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task Reset_TheCurrentPassword_IsRefused()
    {
        _userManager.Setup(u => u.VerifyUserTokenAsync(_user, It.IsAny<string>(), It.IsAny<string>(), Token)).ReturnsAsync(true);
        _userManager.Setup(u => u.CheckPasswordAsync(_user, NewPassword)).ReturnsAsync(true);

        var result = await Resets().ResetAsync(UserId, Token, NewPassword, CancellationToken.None);

        result.Result.Errors.Should().ContainSingle(e => e.Code == IdentityErrors.SamePasswordCode);
        _userManager.Verify(u => u.ResetPasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task Reset_PasswordRulesRefuse_KeepsTheFlagAndReturnsTheirErrors()
    {
        _user.MustChangePassword = true;
        _userManager.Setup(u => u.VerifyUserTokenAsync(_user, It.IsAny<string>(), It.IsAny<string>(), Token)).ReturnsAsync(true);
        _userManager.Setup(u => u.ResetPasswordAsync(_user, Token, NewPassword))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "PasswordTooShort" }));

        var result = await Resets().ResetAsync(UserId, Token, NewPassword, CancellationToken.None);

        result.Result.Errors.Should().ContainSingle(e => e.Code == "PasswordTooShort");
        _user.MustChangePassword.Should().BeTrue();
        _userManager.Verify(u => u.SetLockoutEndDateAsync(It.IsAny<ApplicationUser>(), It.IsAny<DateTimeOffset?>()), Times.Never);
    }

    [Test]
    public async Task Reset_TokenUsedMeanwhile_IsInvalidToken()
    {
        _userManager.Setup(u => u.VerifyUserTokenAsync(_user, It.IsAny<string>(), It.IsAny<string>(), Token)).ReturnsAsync(true);
        _userManager.Setup(u => u.ResetPasswordAsync(_user, Token, NewPassword))
            .ReturnsAsync(IdentityResult.Failed(new IdentityErrorDescriber().InvalidToken()));

        var result = await Resets().ResetAsync(UserId, Token, NewPassword, CancellationToken.None);

        result.Result.Errors.Should().ContainSingle(e => e.Code == IdentityErrors.InvalidTokenCode && e.Description == IdentityErrors.InvalidToken.Description);
    }

    [Test]
    public void TokenProvider_LastsThirtyMinutesUnderItsOwnName()
    {
        var options = new PasswordResetTokenProviderOptions();

        options.TokenLifespan.Should().Be(TimeSpan.FromMinutes(30));
        options.Name.Should().Be(PasswordResetTokenProvider.ProviderName);
    }
}
