using FluentAssertions;
using MedApp.Application.Common.Identity;
using MedApp.Infrastructure.Common.Identity;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace MedApp.UnitTests.Infrastructure.Common.Identity;

[TestFixture]
public sealed class FirstAdminSetupTests
{
    private Mock<UserManager<ApplicationUser>> _userManager = null!;
    private Mock<RoleManager<IdentityRole<Guid>>> _roleManager = null!;
    private Mock<IIdentityTransactions> _transactions = null!;
    private FirstAdminSetup _setup = null!;

    [SetUp]
    public void SetUp()
    {
        _userManager = new Mock<UserManager<ApplicationUser>>(
            new Mock<IUserStore<ApplicationUser>>().Object, null!, null!, null!, null!, null!, null!, null!, null!);
        _roleManager = new Mock<RoleManager<IdentityRole<Guid>>>(
            new Mock<IRoleStore<IdentityRole<Guid>>>().Object, null!, null!, null!, null!);

        // The transaction runs the work as given (committing or rolling back is IdentityTransactions' job).
        _transactions = new Mock<IIdentityTransactions>();
        _transactions
            .Setup(t => t.RunAsync(It.IsAny<Func<Task<IdentityResult>>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task<IdentityResult>> work, CancellationToken _) => work());

        // A new database: no roles, no accounts, and Identity accepts what it's given.
        _roleManager.Setup(r => r.RoleExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _roleManager.Setup(r => r.CreateAsync(It.IsAny<IdentityRole<Guid>>())).ReturnsAsync(IdentityResult.Success);
        _userManager.SetupGet(u => u.Users).Returns(AsyncQueryable.Of<ApplicationUser>([]));
        _userManager
            .Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        _userManager
            .Setup(u => u.AddToRolesAsync(It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(IdentityResult.Success);

        _setup = CreateSetup();
    }

    private FirstAdminSetup CreateSetup() => new(
        _userManager.Object,
        _roleManager.Object,
        _transactions.Object,
        new FixedTimeProvider(AuthTestConstants.FirstAdminCreatedAtUtc),
        NullLogger<FirstAdminSetup>.Instance);

    [TestCase(null, AuthTestConstants.ValidPassword)]
    [TestCase(" ", AuthTestConstants.ValidPassword)]
    [TestCase(AuthTestConstants.FirstAdminUsername, null)]
    [TestCase(AuthTestConstants.FirstAdminUsername, "")]
    public async Task RunAsync_UsernameOrPasswordMissing_ReturnsNotConfiguredAndChangesNothing(
        string? username,
        string? password)
    {
        var result = await _setup.RunAsync(username, password, CancellationToken.None);

        result.Outcome.Should().Be(FirstAdminOutcome.NotConfigured);
        _transactions.Verify(
            t => t.RunAsync(It.IsAny<Func<Task<IdentityResult>>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task RunAsync_NewDatabase_MakesTheRolesAndTheAdminWithBoth()
    {
        var result = await _setup.RunAsync(
            AuthTestConstants.FirstAdminUsername,
            AuthTestConstants.ValidPassword,
            CancellationToken.None);

        result.Outcome.Should().Be(FirstAdminOutcome.Created);
        result.Errors.Should().BeEmpty();

        _roleManager.Verify(r => r.CreateAsync(It.Is<IdentityRole<Guid>>(x => x.Name == "Admin")), Times.Once);
        _roleManager.Verify(r => r.CreateAsync(It.Is<IdentityRole<Guid>>(x => x.Name == "User")), Times.Once);
        _userManager.Verify(u => u.CreateAsync(
            It.Is<ApplicationUser>(x =>
                x.UserName == AuthTestConstants.FirstAdminUsername
                && x.CreatedAt == AuthTestConstants.FirstAdminCreatedAtUtc
                // Its password was chosen by whoever deploys, not generated and handed on, so it needn't change it.
                && !x.MustChangePassword),
            AuthTestConstants.ValidPassword), Times.Once);
        // User too, or the admin couldn't change their own password.
        _userManager.Verify(u => u.AddToRolesAsync(
            It.Is<ApplicationUser>(x => x.UserName == AuthTestConstants.FirstAdminUsername),
            It.Is<IEnumerable<string>>(roles => roles.SequenceEqual(new[] { "Admin", "User" }))), Times.Once);
    }

    [Test]
    public async Task RunAsync_RunsAsOneTransaction_WithTheCancellationToken()
    {
        using var cts = new CancellationTokenSource();

        await _setup.RunAsync(AuthTestConstants.FirstAdminUsername, AuthTestConstants.ValidPassword, cts.Token);

        _transactions.Verify(
            t => t.RunAsync(It.IsAny<Func<Task<IdentityResult>>>(), cts.Token),
            Times.Once);
    }

    [Test]
    public async Task RunAsync_RolesExist_DoesNotMakeThemAgain()
    {
        _roleManager.Setup(r => r.RoleExistsAsync(It.IsAny<string>())).ReturnsAsync(true);

        var result = await _setup.RunAsync(
            AuthTestConstants.FirstAdminUsername,
            AuthTestConstants.ValidPassword,
            CancellationToken.None);

        result.Outcome.Should().Be(FirstAdminOutcome.Created);
        _roleManager.Verify(r => r.CreateAsync(It.IsAny<IdentityRole<Guid>>()), Times.Never);
    }

    [Test]
    public async Task RunAsync_AccountsExist_ReturnsAccountsExistAndMakesNoAccount()
    {
        _userManager.SetupGet(u => u.Users).Returns(AsyncQueryable.Of([AuthTestConstants.CreateValidUser()]));

        var result = await _setup.RunAsync(
            AuthTestConstants.FirstAdminUsername,
            AuthTestConstants.ValidPassword,
            CancellationToken.None);

        result.Outcome.Should().Be(FirstAdminOutcome.AccountsExist);
        _userManager.Verify(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
        _userManager.Verify(u => u.AddToRolesAsync(It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Test]
    public async Task RunAsync_RoleCannotBeMade_ReturnsFailedAndMakesNoAccount()
    {
        var error = new IdentityError { Code = "RoleError", Description = "The role couldn't be made." };
        _roleManager
            .Setup(r => r.CreateAsync(It.IsAny<IdentityRole<Guid>>()))
            .ReturnsAsync(IdentityResult.Failed(error));

        var result = await _setup.RunAsync(
            AuthTestConstants.FirstAdminUsername,
            AuthTestConstants.ValidPassword,
            CancellationToken.None);

        result.Outcome.Should().Be(FirstAdminOutcome.Failed);
        result.Errors.Should().ContainSingle().Which.Should().BeSameAs(error);
        _userManager.Verify(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task RunAsync_PasswordRefused_ReturnsFailedWithTheErrorsAndAddsNoRole()
    {
        var error = new IdentityError { Code = "PasswordTooShort", Description = "Passwords must be longer." };
        _userManager
            .Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(error));

        var result = await _setup.RunAsync(
            AuthTestConstants.FirstAdminUsername,
            AuthTestConstants.Passwords[0],
            CancellationToken.None);

        result.Outcome.Should().Be(FirstAdminOutcome.Failed);
        result.Errors.Should().ContainSingle().Which.Code.Should().Be("PasswordTooShort");
        _userManager.Verify(u => u.AddToRolesAsync(It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Test]
    public async Task RunAsync_RolesCannotBeAdded_ReturnsFailedWithTheErrors()
    {
        var error = new IdentityError { Code = "RoleNotAdded", Description = "No." };
        _userManager
            .Setup(u => u.AddToRolesAsync(It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(IdentityResult.Failed(error));

        var result = await _setup.RunAsync(
            AuthTestConstants.FirstAdminUsername,
            AuthTestConstants.ValidPassword,
            CancellationToken.None);

        result.Outcome.Should().Be(FirstAdminOutcome.Failed);
        result.Errors.Should().ContainSingle().Which.Should().BeSameAs(error);
    }

    [Test]
    public async Task RunCommandAsync_Configured_MakesTheAdminAndReturnsZero()
    {
        var exitCode = await FirstAdminSetup.RunCommandAsync(
            Services(AuthTestConstants.FirstAdminUsername, AuthTestConstants.ValidPassword),
            CancellationToken.None);

        exitCode.Should().Be(0);
        _userManager.Verify(u => u.CreateAsync(
            It.Is<ApplicationUser>(x => x.UserName == AuthTestConstants.FirstAdminUsername),
            AuthTestConstants.ValidPassword), Times.Once);
    }

    [Test]
    public async Task RunCommandAsync_AccountsExist_ReturnsZero()
    {
        _userManager.SetupGet(u => u.Users).Returns(AsyncQueryable.Of([AuthTestConstants.CreateValidUser()]));

        var exitCode = await FirstAdminSetup.RunCommandAsync(
            Services(AuthTestConstants.FirstAdminUsername, AuthTestConstants.ValidPassword),
            CancellationToken.None);

        exitCode.Should().Be(0);
    }

    [Test]
    public async Task RunCommandAsync_NotConfigured_ReturnsOne()
    {
        var exitCode = await FirstAdminSetup.RunCommandAsync(Services(null, null), CancellationToken.None);

        exitCode.Should().Be(1);
    }

    [Test]
    public async Task RunCommandAsync_IdentityRefuses_ReturnsOne()
    {
        _userManager
            .Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "PasswordTooShort", Description = "No." }));

        var exitCode = await FirstAdminSetup.RunCommandAsync(
            Services(AuthTestConstants.FirstAdminUsername, AuthTestConstants.Passwords[0]),
            CancellationToken.None);

        exitCode.Should().Be(1);
    }

    /// <summary>The services the command finds: the FirstAdmin settings given, and the setup with the mocks.</summary>
    private IServiceProvider Services(string? username, string? password)
    {
        var settings = new Dictionary<string, string?>();
        if (username is not null) settings["FirstAdmin:Username"] = username;
        if (password is not null) settings["FirstAdmin:Password"] = password;

        return new ServiceCollection()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build())
            .AddScoped(_ => CreateSetup())
            .BuildServiceProvider();
    }
}
