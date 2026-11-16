using MedApp.Application.Authentication.TwoFactor.Repositories;
using FluentAssertions;
using MedApp.Infrastructure.Common.Identity;
using MedApp.UnitTests.Common.Constants;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Moq;

namespace MedApp.UnitTests.Infrastructure.Common.Identity;

/// <summary>The passwords ResetUserPasswordAsync makes, under each of Identity's password rules.</summary>
[TestFixture]
public sealed class IdentityUserServicePasswordTests
{
    private static async Task<string> ResetPassword(PasswordOptions rules)
    {
        var userManager = new Mock<UserManager<ApplicationUser>>(
            new Mock<IUserStore<ApplicationUser>>().Object, null!, null!, null!, null!, null!, null!, null!, null!);
        var user = AuthTestConstants.CreateValidUser();
        userManager.Setup(u => u.FindByNameAsync(user.UserName!)).ReturnsAsync(user);
        userManager.Setup(u => u.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("synthetic-token");
        userManager.Setup(u => u.ResetPasswordAsync(user, "synthetic-token", It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);
        var service = new IdentityUserService(userManager.Object, Options.Create(new IdentityOptions { Password = rules }), Mock.Of<IAccountLockoutRepository>(), TimeProvider.System);

        var response = await service.ResetUserPasswordAsync(user.UserName!, CancellationToken.None);

        response.Errors.Should().BeEmpty();
        return response.GeneratedPassword!;
    }

    [Test]
    public async Task ResetUserPasswordAsync_EveryRule_MeetsThemAll()
    {
        var password = await ResetPassword(new PasswordOptions
        {
            RequiredLength = 12, RequireUppercase = true, RequireLowercase = true, RequireDigit = true, RequireNonAlphanumeric = true,
        });

        // Generated passwords are at least 16 characters, more than the 12 the rules ask for.
        password.Should().HaveLength(16);
        password.Should().MatchRegex("[A-Z]").And.MatchRegex("[a-z]").And.MatchRegex("[0-9]").And.MatchRegex("[^A-Za-z0-9]");
    }

    [Test]
    public async Task ResetUserPasswordAsync_NoRulesAndNoLength_MakesSixteenLettersAndDigits()
    {
        var password = await ResetPassword(new PasswordOptions
        {
            RequiredLength = 0, RequiredUniqueChars = 0, RequireUppercase = false, RequireLowercase = false, RequireDigit = false, RequireNonAlphanumeric = false,
        });

        password.Should().HaveLength(16).And.MatchRegex("^[A-Za-z0-9]+$");
    }

    [Test]
    public async Task ResetUserPasswordAsync_LongerRequiredLength_UsesIt()
    {
        var password = await ResetPassword(new PasswordOptions
        {
            RequiredLength = 24, RequiredUniqueChars = 1, RequireUppercase = true, RequireLowercase = true, RequireDigit = true, RequireNonAlphanumeric = true,
        });

        password.Should().HaveLength(24);
    }

    [Test]
    public async Task ResetUserPasswordAsync_MoreUniqueCharactersThanLength_GrowsToFitThemAllDistinct()
    {
        var password = await ResetPassword(new PasswordOptions
        {
            RequiredLength = 6, RequiredUniqueChars = 20, RequireUppercase = true, RequireLowercase = false, RequireDigit = true, RequireNonAlphanumeric = false,
        });

        password.Should().HaveLength(20);
        password.Distinct().Should().HaveCount(20);
    }
}
