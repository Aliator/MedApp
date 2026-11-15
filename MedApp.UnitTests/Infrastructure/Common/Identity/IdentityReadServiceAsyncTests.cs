using FluentAssertions;
using MedApp.Contracts.Authentication.Responses;
using MedApp.Infrastructure.Common.Identity;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace MedApp.UnitTests.Infrastructure.Common.Identity;

/// <summary>IdentityReadService's queries that run asynchronously against the users table.</summary>
[TestFixture]
public sealed class IdentityReadServiceAsyncTests
{
    private static readonly ApplicationUser First = new() { Id = Guid.Parse("0000000f-0000-0000-0000-000000000001"), UserName = "test.first" };
    private static readonly ApplicationUser Second = new() { Id = Guid.Parse("0000000f-0000-0000-0000-000000000002"), UserName = "test.second" };

    private Mock<UserManager<ApplicationUser>> _userManager = null!;
    private IdentityReadService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _userManager = new Mock<UserManager<ApplicationUser>>(
            new Mock<IUserStore<ApplicationUser>>().Object, null!, null!, null!, null!, null!, null!, null!, null!);
        _userManager.SetupGet(u => u.Users).Returns(AsyncQueryable.Of([First, Second]));
        var roleManager = new Mock<RoleManager<IdentityRole<Guid>>>(
            new Mock<IRoleStore<IdentityRole<Guid>>>().Object, null!, null!, null!, null!);
        _service = new IdentityReadService(_userManager.Object, roleManager.Object);
    }

    [Test]
    public async Task GetUsernamesByIdAsync_SomeIds_ReturnsTheNamesOfThoseThatExist()
    {
        var result = await _service.GetUsernamesByIdAsync([First.Id, First.Id, Guid.Parse("0000000f-0000-0000-0000-000000000009")], CancellationToken.None);

        result.Should().BeEquivalentTo(new Dictionary<Guid, string> { [First.Id] = First.UserName! });
    }

    [Test]
    public async Task GetUsernamesByIdAsync_NoIds_ReturnsNothing()
    {
        var result = await _service.GetUsernamesByIdAsync([], CancellationToken.None);

        result.Should().BeEmpty();
        _userManager.VerifyGet(u => u.Users, Times.Never);
    }

    [Test]
    public async Task GetUsersAsync_ReturnsEachUserWithTheirRoles_SkippingAnyGoneSinceTheList()
    {
        _userManager.Setup(u => u.FindByIdAsync(First.Id.ToString())).ReturnsAsync(First);
        _userManager.Setup(u => u.FindByIdAsync(Second.Id.ToString())).ReturnsAsync((ApplicationUser?)null);
        _userManager.Setup(u => u.GetRolesAsync(First)).ReturnsAsync(["Admin", "User"]);

        var result = await _service.GetUsersAsync(CancellationToken.None);

        result.Should().BeEquivalentTo(new[] { new UserResponse(First.Id, First.UserName!, ["Admin", "User"]) });
    }
}
