using FluentAssertions;
using MedApp.Infrastructure.Common.Identity;
using MedApp.UnitTests.Common.Constants;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using MedApp.Infrastructure.Common.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace MedApp.UnitTests.Infrastructure.Common.Identity;

[TestFixture]
public sealed class CommonPasswordValidatorTests
{
    private static Task<IdentityResult> Validate(string? password) =>
        new CommonPasswordValidator().ValidateAsync(null!, AuthTestConstants.CreateValidUser(), password);

    [Test]
    public void List_IsEmbedded_WithItsTenThousandPasswords()
    {
        // 10,000 lines, less one blank and the ones that differ only in case.
        CommonPasswordValidator.Count.Should().BeInRange(9_900, 9_999);
    }

    [TestCase("123456")]
    [TestCase("password")]
    [TestCase("Password1")]
    [TestCase("PASSWORD1")]
    [TestCase("qwerty123")]
    [TestCase("iloveyou")]
    public async Task Validate_CommonPassword_IsRefused(string password)
    {
        var result = await Validate(password);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Code.Should().Be(CommonPasswordValidator.ErrorCode);
    }

    [TestCase(AuthTestConstants.ValidPassword)]
    [TestCase("Synthetic-Password-1")]
    [TestCase("correct horse battery staple ward 7")]
    public async Task Validate_UncommonPassword_IsAllowed(string password)
    {
        (await Validate(password)).Succeeded.Should().BeTrue();
    }

    [TestCase(null)]
    [TestCase("")]
    public async Task Validate_NoPassword_IsLeftToTheOtherRules(string? password)
    {
        (await Validate(password)).Succeeded.Should().BeTrue();
    }

    [Test]
    public async Task GeneratedPasswords_AreNeverRefused()
    {
        var userManager = new Mock<UserManager<ApplicationUser>>(
            new Mock<IUserStore<ApplicationUser>>().Object, null!, null!, null!, null!, null!, null!, null!, null!);
        var user = AuthTestConstants.CreateValidUser();
        var generated = new List<string>();
        userManager.Setup(u => u.FindByNameAsync(user.UserName!)).ReturnsAsync(user);
        userManager.Setup(u => u.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("synthetic-token");
        userManager.Setup(u => u.ResetPasswordAsync(user, "synthetic-token", Capture.In(generated))).ReturnsAsync(IdentityResult.Success);
        var service = new IdentityUserService(userManager.Object, Options.Create(new IdentityOptions()));

        for (var i = 0; i < 500; i++)
            await service.ResetUserPasswordAsync(user.UserName!, CancellationToken.None);

        generated.Should().HaveCount(500).And.OnlyContain(password => !CommonPasswordValidator.IsCommon(password));
    }
}

[TestFixture]
public sealed class CommonPasswordValidatorRegistrationTests
{
    [Test]
    public void AddInfrastructure_RegistersItWithIdentitysOtherPasswordRules()
    {
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
        using var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection()
            .AddLogging()
            .AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(configuration)
            .AddInfrastructure(configuration)
            .BuildServiceProvider();

        services.GetServices<IPasswordValidator<ApplicationUser>>()
            .Should().Contain(v => v is CommonPasswordValidator)
            .And.Contain(v => v is PasswordValidator<ApplicationUser>);
    }
}
