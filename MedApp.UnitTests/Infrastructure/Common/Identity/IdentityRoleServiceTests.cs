using FluentAssertions;
using MedApp.Infrastructure.Common.Identity;
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
    public async Task AssignRoleAsync_UserNotFound_ReturnsNull()
    {
        _userManager
            .Setup(u => u.FindByNameAsync("alice"))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _service.AssignRoleAsync("alice", "Admin", CancellationToken.None);

        result.Should().BeNull();
    }

    [Test]
    public async Task AssignRoleAsync_RoleNotFound_ReturnsNull()
    {
        var user = new ApplicationUser { UserName = "alice" };

        _userManager
            .Setup(u => u.FindByNameAsync("alice"))
            .ReturnsAsync(user);

        _roleManager
            .Setup(r => r.FindByNameAsync("Admin"))
            .ReturnsAsync((IdentityRole<Guid>?)null);

        var result = await _service.AssignRoleAsync("alice", "Admin", CancellationToken.None);

        result.Should().BeNull();
    }

    [Test]
    public async Task AssignRoleAsync_AddToRoleFails_ReturnsNull()
    {
        var user = new ApplicationUser { UserName = "alice" };
        var role = new IdentityRole<Guid>("Admin");

        _userManager
            .Setup(u => u.FindByNameAsync("alice"))
            .ReturnsAsync(user);

        _roleManager
            .Setup(r => r.FindByNameAsync("Admin"))
            .ReturnsAsync(role);

        _userManager
            .Setup(u => u.AddToRoleAsync(user, "Admin"))
            .ReturnsAsync(IdentityResult.Failed());

        var result = await _service.AssignRoleAsync("alice", "Admin", CancellationToken.None);

        result.Should().BeNull();
    }

    [Test]
    public async Task AssignRoleAsync_Succeeds_ReturnsRole()
    {
        var user = new ApplicationUser { UserName = "alice" };
        var role = new IdentityRole<Guid>("Admin");

        _userManager
            .Setup(u => u.FindByNameAsync("alice"))
            .ReturnsAsync(user);

        _roleManager
            .Setup(r => r.FindByNameAsync("Admin"))
            .ReturnsAsync(role);

        _userManager
            .Setup(u => u.AddToRoleAsync(user, "Admin"))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _service.AssignRoleAsync("alice", "Admin", CancellationToken.None);

        result.Should().BeSameAs(role);
    }
}
