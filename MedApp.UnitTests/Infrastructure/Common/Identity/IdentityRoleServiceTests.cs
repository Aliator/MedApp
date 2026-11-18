using FluentAssertions;
using MedApp.Application.Common.Identity;
using MedApp.Infrastructure.Common.Identity;
using MedApp.UnitTests.Common.Constants;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace MedApp.UnitTests.Infrastructure.Common.Identity;

[TestFixture]
public sealed class IdentityRoleServiceTests
{
    private Mock<UserManager<ApplicationUser>> _userManager = null!;
    private Mock<RoleManager<ApplicationRole>> _roleManager = null!;
    private Mock<IIdentityTransactions> _transactions = null!;
    private IdentityRoleService _service = null!;

    [SetUp]
    public void SetUp()
    {
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        _userManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var roleStore = new Mock<IRoleStore<ApplicationRole>>();
        _roleManager = new Mock<RoleManager<ApplicationRole>>(
            roleStore.Object, null!, null!, null!, null!);

        // The transaction runs the work as given (committing or rolling back is IdentityTransactions' job).
        _transactions = new Mock<IIdentityTransactions>();
        _transactions
            .Setup(t => t.RunAsync(It.IsAny<Func<Task<IdentityResult>>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task<IdentityResult>> work, CancellationToken _) => work());

        _service = new IdentityRoleService(_userManager.Object, _roleManager.Object, _transactions.Object, TimeProvider.System);
    }

    [Test]
    public async Task CreateRoleAsync_RoleAlreadyExists_ReturnsExistingRole()
    {
        var role = AuthTestConstants.CreateValidRole();

        _roleManager
            .Setup(r => r.FindByNameAsync(role.Name!))
            .ReturnsAsync(role);

        var result = await _service.CreateRoleAsync(
            role.Name!,
            CancellationToken.None);

        result.Should().BeSameAs(role);

        _roleManager.Verify(
            r => r.CreateAsync(It.IsAny<ApplicationRole>()),
            Times.Never);
    }

    [Test]
    public async Task CreateRoleAsync_CreateFails_ReturnsNull()
    {
        var roleName = AuthTestConstants.Roles[0];

        _roleManager
            .Setup(r => r.FindByNameAsync(roleName))
            .ReturnsAsync((ApplicationRole?)null);

        _roleManager
            .Setup(r => r.CreateAsync(It.Is<ApplicationRole>(x => x.Name == roleName)))
            .ReturnsAsync(IdentityResult.Failed());

        var result = await _service.CreateRoleAsync(
            roleName,
            CancellationToken.None);

        result.Should().BeNull();
    }

    [Test]
    public async Task CreateRoleAsync_CreateSucceeds_ReturnsCreatedRole()
    {
        var roleName = AuthTestConstants.Roles[0];

        _roleManager
            .Setup(r => r.FindByNameAsync(roleName))
            .ReturnsAsync((ApplicationRole?)null);

        _roleManager
            .Setup(r => r.CreateAsync(It.Is<ApplicationRole>(x => x.Name == roleName)))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _service.CreateRoleAsync(
            roleName,
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.Name.Should().Be(roleName);
    }

    [Test]
    public async Task AssignRoleAsync_UserNotFound_ReturnsNull()
    {
        _userManager
            .Setup(u => u.FindByNameAsync(AuthTestConstants.Usernames[0]))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _service.AssignRoleAsync(
            AuthTestConstants.Usernames[0],
            AuthTestConstants.Roles[0],
            CancellationToken.None);

        result.Should().BeNull();
    }

    [Test]
    public async Task AssignRoleAsync_RoleNotFound_ReturnsNull()
    {
        var user = AuthTestConstants.CreateValidUser();

        _userManager
            .Setup(u => u.FindByNameAsync(user.UserName!))
            .ReturnsAsync(user);

        _roleManager
            .Setup(r => r.FindByNameAsync(AuthTestConstants.Roles[0]))
            .ReturnsAsync((ApplicationRole?)null);

        var result = await _service.AssignRoleAsync(
            user.UserName!,
            AuthTestConstants.Roles[0],
            CancellationToken.None);

        result.Should().BeNull();
    }

    [Test]
    public async Task AssignRoleAsync_RoleNameNull_ReturnsNull()
    {
        var user = AuthTestConstants.CreateValidUser();
        var role = new ApplicationRole();

        _userManager
            .Setup(u => u.FindByNameAsync(user.UserName!))
            .ReturnsAsync(user);

        _roleManager
            .Setup(r => r.FindByNameAsync(AuthTestConstants.Roles[0]))
            .ReturnsAsync(role);

        var result = await _service.AssignRoleAsync(
            user.UserName!,
            AuthTestConstants.Roles[0],
            CancellationToken.None);

        result.Should().BeNull();

        _userManager.Verify(
            u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()),
            Times.Never);
    }

    [Test]
    public async Task AssignRoleAsync_AddToRoleFails_ReturnsNull()
    {
        var user = AuthTestConstants.CreateValidUser();
        var role = AuthTestConstants.CreateValidRole();

        _userManager
            .Setup(u => u.FindByNameAsync(user.UserName!))
            .ReturnsAsync(user);

        _roleManager
            .Setup(r => r.FindByNameAsync(role.Name!))
            .ReturnsAsync(role);

        _userManager
            .Setup(u => u.AddToRoleAsync(user, role.Name!))
            .ReturnsAsync(IdentityResult.Failed());

        var result = await _service.AssignRoleAsync(
            user.UserName!,
            role.Name!,
            CancellationToken.None);

        result.Should().BeNull();
    }

    [Test]
    public async Task AssignRoleAsync_Succeeds_ReturnsRole()
    {
        var user = AuthTestConstants.CreateValidUser();
        var role = AuthTestConstants.CreateValidRole();

        _userManager
            .Setup(u => u.FindByNameAsync(user.UserName!))
            .ReturnsAsync(user);

        _roleManager
            .Setup(r => r.FindByNameAsync(role.Name!))
            .ReturnsAsync(role);

        _userManager
            .Setup(u => u.AddToRoleAsync(user, role.Name!))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _service.AssignRoleAsync(
            user.UserName!,
            role.Name!,
            CancellationToken.None);

        result.Should().BeSameAs(role);
    }

    [Test]
    public async Task RevokeRoleAsync_UserNotFound_ReturnsNull()
    {
        _userManager
            .Setup(u => u.FindByNameAsync(AuthTestConstants.Usernames[0]))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _service.RevokeRoleAsync(
            AuthTestConstants.Usernames[0],
            AuthTestConstants.Roles[0],
            CancellationToken.None);

        result.Should().BeNull();
    }

    [Test]
    public async Task RevokeRoleAsync_RoleNotFound_ReturnsNull()
    {
        var user = AuthTestConstants.CreateValidUser();

        _userManager
            .Setup(u => u.FindByNameAsync(user.UserName!))
            .ReturnsAsync(user);

        _roleManager
            .Setup(r => r.FindByNameAsync(AuthTestConstants.Roles[0]))
            .ReturnsAsync((ApplicationRole?)null);

        var result = await _service.RevokeRoleAsync(
            user.UserName!,
            AuthTestConstants.Roles[0],
            CancellationToken.None);

        result.Should().BeNull();
    }

    [Test]
    public async Task RevokeRoleAsync_RoleNameNull_ReturnsNull()
    {
        var user = AuthTestConstants.CreateValidUser();
        var role = new ApplicationRole();

        _userManager
            .Setup(u => u.FindByNameAsync(user.UserName!))
            .ReturnsAsync(user);

        _roleManager
            .Setup(r => r.FindByNameAsync(AuthTestConstants.Roles[0]))
            .ReturnsAsync(role);

        var result = await _service.RevokeRoleAsync(
            user.UserName!,
            AuthTestConstants.Roles[0],
            CancellationToken.None);

        result.Should().BeNull();

        _userManager.Verify(
            u => u.RemoveFromRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()),
            Times.Never);
    }

    [Test]
    public async Task RevokeRoleAsync_RemoveFromRoleFails_ReturnsNull()
    {
        var user = AuthTestConstants.CreateValidUser();
        var role = AuthTestConstants.CreateValidRole();

        _userManager
            .Setup(u => u.FindByNameAsync(user.UserName!))
            .ReturnsAsync(user);

        _roleManager
            .Setup(r => r.FindByNameAsync(role.Name!))
            .ReturnsAsync(role);

        _userManager
            .Setup(u => u.RemoveFromRoleAsync(user, role.Name!))
            .ReturnsAsync(IdentityResult.Failed());

        var result = await _service.RevokeRoleAsync(
            user.UserName!,
            role.Name!,
            CancellationToken.None);

        result.Should().BeNull();
    }

    [Test]
    public async Task RevokeRoleAsync_Succeeds_ReturnsRole()
    {
        var user = AuthTestConstants.CreateValidUser();
        var role = AuthTestConstants.CreateValidRole();

        _userManager
            .Setup(u => u.FindByNameAsync(user.UserName!))
            .ReturnsAsync(user);

        _roleManager
            .Setup(r => r.FindByNameAsync(role.Name!))
            .ReturnsAsync(role);

        _userManager
            .Setup(u => u.RemoveFromRoleAsync(user, role.Name!))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _service.RevokeRoleAsync(
            user.UserName!,
            role.Name!,
            CancellationToken.None);

        result.Should().BeSameAs(role);
    }

    [Test]
    public async Task DeleteRoleAsync_RoleNotFound_ReturnsFailedResult()
    {
        _roleManager
            .Setup(r => r.FindByNameAsync(AuthTestConstants.Roles[0]))
            .ReturnsAsync((ApplicationRole?)null);

        var result = await _service.DeleteRoleAsync(
            AuthTestConstants.Roles[0],
            CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == IdentityErrors.RoleNotFoundCode);
    }

    [Test]
    public async Task DeleteRoleAsync_RoleExists_DeletesRole()
    {
        var role = AuthTestConstants.CreateValidRole();

        _roleManager
            .Setup(r => r.FindByNameAsync(role.Name!))
            .ReturnsAsync(role);

        _roleManager
            .Setup(r => r.DeleteAsync(role))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _service.DeleteRoleAsync(
            role.Name!,
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();

        _roleManager.Verify(
            r => r.DeleteAsync(role),
            Times.Once);
    }

    private void SetUpRoles(params string[] names)
    {
        foreach (var name in names)
            _roleManager
                .Setup(r => r.FindByNameAsync(It.Is<string>(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase))))
                .ReturnsAsync(new ApplicationRole(name));
    }

    [Test]
    public async Task SetRolesAsync_UserMissing_FailsWithUserNotFound()
    {
        _userManager.Setup(u => u.FindByNameAsync(AuthTestConstants.Usernames[0])).ReturnsAsync((ApplicationUser?)null);

        var result = await _service.SetRolesAsync(AuthTestConstants.Usernames[0], AuthTestConstants.Roles, CancellationToken.None);

        result.Errors.Should().ContainSingle(e => e.Code == IdentityErrors.UserNotFoundCode);
    }

    [Test]
    public async Task SetRolesAsync_RoleMissing_FailsWithoutChangingAnything()
    {
        var user = AuthTestConstants.CreateValidUser();
        _userManager.Setup(u => u.FindByNameAsync(user.UserName!)).ReturnsAsync(user);
        SetUpRoles(AuthTestConstants.Roles[0]);

        var result = await _service.SetRolesAsync(user.UserName!, [AuthTestConstants.Roles[0], "Nonexistent"], CancellationToken.None);

        // A failed result rolls the transaction back, though nothing was changed.
        result.Errors.Should().ContainSingle(e => e.Code == IdentityErrors.RoleNotFoundCode);
        VerifyNoRolesChanged();
    }

    private void VerifyNoRolesChanged()
    {
        _userManager.Verify(u => u.RemoveFromRolesAsync(It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<string>>()), Times.Never);
        _userManager.Verify(u => u.AddToRolesAsync(It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Test]
    public async Task SetRolesAsync_SameRoles_SucceedsWithoutChangingAnything()
    {
        var user = AuthTestConstants.CreateValidUser();
        _userManager.Setup(u => u.FindByNameAsync(user.UserName!)).ReturnsAsync(user);
        _userManager.Setup(u => u.GetRolesAsync(user)).ReturnsAsync([AuthTestConstants.Roles[1]]);
        SetUpRoles(AuthTestConstants.Roles[1]);

        var result = await _service.SetRolesAsync(user.UserName!, [AuthTestConstants.Roles[1].ToLowerInvariant()], CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        VerifyNoRolesChanged();
    }

    [Test]
    public async Task SetRolesAsync_TransactionRetried_LoadsTheUserAndRolesAgainForTheRetry()
    {
        // A retry (after a transient error) runs the work again from the start, with what it loaded before forgotten.
        // The first attempt's user must not be reused: Identity has already given it a new concurrency stamp.
        var firstAttempt = AuthTestConstants.CreateValidUser();
        var retry = AuthTestConstants.CreateValidUser();
        _transactions
            .Setup(t => t.RunAsync(It.IsAny<Func<Task<IdentityResult>>>(), It.IsAny<CancellationToken>()))
            .Returns(async (Func<Task<IdentityResult>> work, CancellationToken _) =>
            {
                await work();
                return await work();
            });
        _userManager.SetupSequence(u => u.FindByNameAsync(firstAttempt.UserName!)).ReturnsAsync(firstAttempt).ReturnsAsync(retry);
        _userManager.Setup(u => u.GetRolesAsync(firstAttempt)).ReturnsAsync([AuthTestConstants.Roles[1]]);
        _userManager.Setup(u => u.GetRolesAsync(retry)).ReturnsAsync([AuthTestConstants.Roles[1]]);
        _userManager.Setup(u => u.RemoveFromRolesAsync(It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<string>>())).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(u => u.AddToRolesAsync(It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<string>>())).ReturnsAsync(IdentityResult.Success);
        SetUpRoles(AuthTestConstants.Roles[0]);

        var result = await _service.SetRolesAsync(firstAttempt.UserName!, [AuthTestConstants.Roles[0]], CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        _userManager.Verify(u => u.FindByNameAsync(firstAttempt.UserName!), Times.Exactly(2));
        _roleManager.Verify(r => r.FindByNameAsync(AuthTestConstants.Roles[0]), Times.Exactly(2));
        _userManager.Verify(u => u.GetRolesAsync(retry), Times.Once);
        _userManager.Verify(u => u.RemoveFromRolesAsync(retry, new[] {AuthTestConstants.Roles[1]}), Times.Once);
        _userManager.Verify(u => u.AddToRolesAsync(retry, new[] {AuthTestConstants.Roles[0]}), Times.Once);
    }

    [Test]
    public async Task SetRolesAsync_ChangedRoles_RemovesAndAddsInOneTransaction()
    {
        var user = AuthTestConstants.CreateValidUser();
        using var cts = new CancellationTokenSource();
        _userManager.Setup(u => u.FindByNameAsync(user.UserName!)).ReturnsAsync(user);
        _userManager.Setup(u => u.GetRolesAsync(user)).ReturnsAsync([AuthTestConstants.Roles[1]]);
        _userManager.Setup(u => u.RemoveFromRolesAsync(user, It.IsAny<IEnumerable<string>>())).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(u => u.AddToRolesAsync(user, It.IsAny<IEnumerable<string>>())).ReturnsAsync(IdentityResult.Success);
        SetUpRoles(AuthTestConstants.Roles[0]);

        var result = await _service.SetRolesAsync(user.UserName!, [AuthTestConstants.Roles[0]], cts.Token);

        result.Succeeded.Should().BeTrue();
        _transactions.Verify(t => t.RunAsync(It.IsAny<Func<Task<IdentityResult>>>(), cts.Token), Times.Once);
        _userManager.Verify(u => u.RemoveFromRolesAsync(user, new[] {AuthTestConstants.Roles[1]}), Times.Once);
        _userManager.Verify(u => u.AddToRolesAsync(user, new[] {AuthTestConstants.Roles[0]}), Times.Once);
    }

    [Test]
    public async Task SetRolesAsync_RolesOnlyRemoved_RemovesThemWithoutAdding()
    {
        var user = AuthTestConstants.CreateValidUser();
        _userManager.Setup(u => u.FindByNameAsync(user.UserName!)).ReturnsAsync(user);
        _userManager.Setup(u => u.GetRolesAsync(user)).ReturnsAsync([AuthTestConstants.Roles[0], AuthTestConstants.Roles[1]]);
        _userManager.Setup(u => u.RemoveFromRolesAsync(user, It.IsAny<IEnumerable<string>>())).ReturnsAsync(IdentityResult.Success);
        SetUpRoles(AuthTestConstants.Roles[0]);

        var result = await _service.SetRolesAsync(user.UserName!, [AuthTestConstants.Roles[0]], CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        _userManager.Verify(u => u.RemoveFromRolesAsync(user, new[] {AuthTestConstants.Roles[1]}), Times.Once);
        _userManager.Verify(u => u.AddToRolesAsync(It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Test]
    public async Task SetRolesAsync_RemoveFails_ReturnsTheFailureWithoutAdding()
    {
        var user = AuthTestConstants.CreateValidUser();
        _userManager.Setup(u => u.FindByNameAsync(user.UserName!)).ReturnsAsync(user);
        _userManager.Setup(u => u.GetRolesAsync(user)).ReturnsAsync([AuthTestConstants.Roles[1]]);
        _userManager
            .Setup(u => u.RemoveFromRolesAsync(user, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError {Code = "Failed"}));
        SetUpRoles(AuthTestConstants.Roles[0]);

        var result = await _service.SetRolesAsync(user.UserName!, [AuthTestConstants.Roles[0]], CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        _userManager.Verify(u => u.AddToRolesAsync(It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Test]
    public async Task SetRolesAsync_AddingAnArchivedRole_FailsWithoutChangingAnything()
    {
        var user = AuthTestConstants.CreateValidUser();
        _userManager.Setup(u => u.FindByNameAsync(user.UserName!)).ReturnsAsync(user);
        _userManager.Setup(u => u.GetRolesAsync(user)).ReturnsAsync([]);
        _roleManager.Setup(r => r.FindByNameAsync("Lab")).ReturnsAsync(new ApplicationRole("Lab") { IsArchived = true });

        var result = await _service.SetRolesAsync(user.UserName!, ["Lab"], CancellationToken.None);

        result.Errors.Should().ContainSingle().Which.Code.Should().Be(IdentityErrors.RoleArchivedCode);
        _userManager.Verify(u => u.AddToRolesAsync(It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Test]
    public async Task SetRolesAsync_KeepingAnArchivedRoleTheyHold_Succeeds()
    {
        var user = AuthTestConstants.CreateValidUser();
        _userManager.Setup(u => u.FindByNameAsync(user.UserName!)).ReturnsAsync(user);
        _userManager.Setup(u => u.GetRolesAsync(user)).ReturnsAsync(["Lab"]);
        _roleManager.Setup(r => r.FindByNameAsync("Lab")).ReturnsAsync(new ApplicationRole("Lab") { IsArchived = true });

        var result = await _service.SetRolesAsync(user.UserName!, ["Lab"], CancellationToken.None);

        result.Succeeded.Should().BeTrue();
    }

    [Test]
    public async Task DeleteRoleAsync_TheBuiltInAdminRole_FailsWithoutDeleting()
    {
        _roleManager.Setup(r => r.FindByNameAsync("Admin")).ReturnsAsync(new ApplicationRole("Admin") { IsBuiltIn = true });

        var result = await _service.DeleteRoleAsync("Admin", CancellationToken.None);

        result.Errors.Should().ContainSingle().Which.Code.Should().Be(IdentityErrors.BuiltInRoleCode);
        _roleManager.Verify(r => r.DeleteAsync(It.IsAny<ApplicationRole>()), Times.Never);
    }

    [Test]
    public async Task CreateRoleAsync_New_SetsWhenItWasMade()
    {
        var now = new DateTime(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc);
        _roleManager.Setup(r => r.FindByNameAsync("Visitor")).ReturnsAsync((ApplicationRole?)null);
        _roleManager.Setup(r => r.CreateAsync(It.IsAny<ApplicationRole>())).ReturnsAsync(IdentityResult.Success);
        var service = new IdentityRoleService(_userManager.Object, _roleManager.Object, _transactions.Object, new MedApp.UnitTests.Common.Fixtures.FixedTimeProvider(now));

        var role = (ApplicationRole?)await service.CreateRoleAsync("Visitor", CancellationToken.None);

        role!.CreatedAt.Should().Be(now);
        role.LastUpdated.Should().Be(now);
        role.IsBuiltIn.Should().BeFalse();
    }
}
