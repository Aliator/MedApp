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
    private Mock<RoleManager<IdentityRole<Guid>>> _roleManager = null!;
    private IdentityRoleService _service = null!;

    [SetUp]
    public void SetUp()
    {
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        _userManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var roleStore = new Mock<IRoleStore<IdentityRole<Guid>>>();
        _roleManager = new Mock<RoleManager<IdentityRole<Guid>>>(
            roleStore.Object, null!, null!, null!, null!);

        _service = new IdentityRoleService(_userManager.Object, _roleManager.Object);
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
            r => r.CreateAsync(It.IsAny<IdentityRole<Guid>>()),
            Times.Never);
    }

    [Test]
    public async Task CreateRoleAsync_CreateFails_ReturnsNull()
    {
        var roleName = AuthTestConstants.Roles[0];

        _roleManager
            .Setup(r => r.FindByNameAsync(roleName))
            .ReturnsAsync((IdentityRole<Guid>?)null);

        _roleManager
            .Setup(r => r.CreateAsync(It.Is<IdentityRole<Guid>>(x => x.Name == roleName)))
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
            .ReturnsAsync((IdentityRole<Guid>?)null);

        _roleManager
            .Setup(r => r.CreateAsync(It.Is<IdentityRole<Guid>>(x => x.Name == roleName)))
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
            .ReturnsAsync((IdentityRole<Guid>?)null);

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
        var role = new IdentityRole<Guid>();

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
            .ReturnsAsync((IdentityRole<Guid>?)null);

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
        var role = new IdentityRole<Guid>();

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
            .ReturnsAsync((IdentityRole<Guid>?)null);

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
}
