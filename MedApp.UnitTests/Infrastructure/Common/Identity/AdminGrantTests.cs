using FluentAssertions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Security;
using MedApp.Infrastructure.Common.Identity;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace MedApp.UnitTests.Infrastructure.Common.Identity;

[TestFixture]
public sealed class AdminGrantTests
{
    private static readonly DateTime Now = new(2026, 11, 18, 9, 0, 0, DateTimeKind.Utc);

    private Mock<UserManager<ApplicationUser>> _users = null!;
    private Mock<RoleManager<ApplicationRole>> _roles = null!;
    private Mock<ISecurityEventLog> _events = null!;
    private Mock<IIdentityTransactions> _transactions = null!;
    private ApplicationUser _user = null!;

    [SetUp]
    public void SetUp()
    {
        _users = new Mock<UserManager<ApplicationUser>>(
            new Mock<IUserStore<ApplicationUser>>().Object, null!, null!, null!, null!, null!, null!, null!, null!);
        _roles = new Mock<RoleManager<ApplicationRole>>(
            new Mock<IRoleStore<ApplicationRole>>().Object, null!, null!, null!, null!);
        _events = new Mock<ISecurityEventLog>();
        _transactions = new Mock<IIdentityTransactions>();
        _transactions
            .Setup(t => t.RunAsync(It.IsAny<Func<Task<IdentityResult>>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task<IdentityResult>> work, CancellationToken _) => work());

        _user = AuthTestConstants.CreateValidUser();
        _users.Setup(u => u.FindByNameAsync(_user.UserName!)).ReturnsAsync(_user);
        _users.Setup(u => u.IsInRoleAsync(_user, AppRoles.Admin)).ReturnsAsync(false);
        _users.Setup(u => u.AddToRoleAsync(_user, AppRoles.Admin)).ReturnsAsync(IdentityResult.Success);
        _roles.Setup(r => r.RoleExistsAsync(AppRoles.Admin)).ReturnsAsync(true);
    }

    private AdminGrant Grant() => new(
        _users.Object, _roles.Object, _transactions.Object, _events.Object, new FixedTimeProvider(Now), NullLogger<AdminGrant>.Instance);

    [Test]
    public async Task RunAsync_AnAccount_GivesItAdmin_AndRecordsASecurityEvent()
    {
        using var cts = new CancellationTokenSource();

        var outcome = await Grant().RunAsync(_user.UserName, cts.Token);

        outcome.Should().Be(AdminGrantOutcome.Granted);
        _users.Verify(u => u.AddToRoleAsync(_user, AppRoles.Admin), Times.Once);
        _transactions.Verify(t => t.RunAsync(It.IsAny<Func<Task<IdentityResult>>>(), cts.Token), Times.Once);
        _events.Verify(e => e.RecordAsync(
            It.Is<SecurityEvent>(x =>
                x.Kind == SecurityEventKind.AdminGrantedByCommand
                && x.UserId == _user.Id
                && x.Username == _user.UserName
                && x.ActorUserId == null
                && (string)x.Details!["added"]! == AppRoles.Admin
                && (string)x.Details!["via"]! == AdminGrant.Command),
            cts.Token), Times.Once);
    }

    [Test]
    public async Task RunAsync_NoAdminRole_MakesTheBuiltInOneFirst()
    {
        _roles.Setup(r => r.RoleExistsAsync(AppRoles.Admin)).ReturnsAsync(false);
        _roles.Setup(r => r.CreateAsync(It.IsAny<ApplicationRole>())).ReturnsAsync(IdentityResult.Success);

        (await Grant().RunAsync(_user.UserName, CancellationToken.None)).Should().Be(AdminGrantOutcome.Granted);

        _roles.Verify(r => r.CreateAsync(It.Is<ApplicationRole>(x => x.Name == AppRoles.Admin && x.IsBuiltIn && x.Id == StandardRoles.Admin.Id)), Times.Once);
    }

    [Test]
    public async Task RunAsync_AlreadyAnAdmin_ChangesAndRecordsNothing()
    {
        _users.Setup(u => u.IsInRoleAsync(_user, AppRoles.Admin)).ReturnsAsync(true);

        (await Grant().RunAsync(_user.UserName, CancellationToken.None)).Should().Be(AdminGrantOutcome.AlreadyAdmin);

        _users.Verify(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
        _events.Verify(e => e.RecordAsync(It.IsAny<SecurityEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task RunAsync_UnknownAccount_IsUserNotFound()
    {
        (await Grant().RunAsync("nobody", CancellationToken.None)).Should().Be(AdminGrantOutcome.UserNotFound);

        _events.Verify(e => e.RecordAsync(It.IsAny<SecurityEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestCase(null)]
    [TestCase(" ")]
    public async Task RunAsync_NoUsername_ChangesNothing(string? username)
    {
        (await Grant().RunAsync(username, CancellationToken.None)).Should().Be(AdminGrantOutcome.NoUsername);

        _transactions.Verify(t => t.RunAsync(It.IsAny<Func<Task<IdentityResult>>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task RunAsync_IdentityRefuses_IsFailed()
    {
        _users.Setup(u => u.AddToRoleAsync(_user, AppRoles.Admin)).ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "No", Description = "No." }));

        (await Grant().RunAsync(_user.UserName, CancellationToken.None)).Should().Be(AdminGrantOutcome.Failed);

        _events.Verify(e => e.RecordAsync(It.IsAny<SecurityEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestCase(true, 0)]
    [TestCase(false, 1)]
    public async Task RunCommandAsync_ExitCode(bool accountExists, int exitCode)
    {
        var services = new ServiceCollection().AddScoped(_ => Grant()).BuildServiceProvider();

        (await AdminGrant.RunCommandAsync(services, accountExists ? _user.UserName! : "nobody", CancellationToken.None)).Should().Be(exitCode);
    }
}
