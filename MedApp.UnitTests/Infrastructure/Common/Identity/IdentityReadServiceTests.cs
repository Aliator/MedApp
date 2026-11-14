using FluentAssertions;
using MedApp.Infrastructure.Common.Identity;
using MedApp.UnitTests.Common.Constants;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace MedApp.UnitTests.Infrastructure.Common.Identity;

[TestFixture]
public sealed class IdentityReadServiceTests
{
    private Mock<UserManager<ApplicationUser>> _userManager = null!;
    private Mock<RoleManager<IdentityRole<Guid>>> _roleManager = null!;
    private IdentityReadService _service = null!;

    [SetUp]
    public void SetUp()
    {
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        _userManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var roleStore = new Mock<IRoleStore<IdentityRole<Guid>>>();
        _roleManager = new Mock<RoleManager<IdentityRole<Guid>>>(
            roleStore.Object, null!, null!, null!, null!);

        _service = new IdentityReadService(_userManager.Object, _roleManager.Object);
    }

    [Test]
    public async Task GetUsernamesAsync_ReturnsAllUsernames()
    {
        var users = AuthTestConstants.Usernames
            .Select(u => new ApplicationUser { UserName = u })
            .AsQueryable();

        _userManager.SetupGet(u => u.Users).Returns(users);

        var result = await _service.GetUsernamesAsync(CancellationToken.None);

        result.Should().BeEquivalentTo(AuthTestConstants.Usernames);
    }

    [Test]
    public async Task GetRoleNamesAsync_ReturnsAllRoleNames()
    {
        var roles = AuthTestConstants.Roles
            .Select(r => new IdentityRole<Guid>(r))
            .AsQueryable();

        _roleManager.SetupGet(r => r.Roles).Returns(roles);

        var result = await _service.GetRoleNamesAsync(CancellationToken.None);

        result.Should().BeEquivalentTo(AuthTestConstants.Roles);
    }

    [Test]
    public async Task GetRolesForUserAsync_UserDoesNotExist_ReturnsEmpty()
    {
        _userManager
            .Setup(u => u.FindByNameAsync(AuthTestConstants.Usernames[0]))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _service.GetRolesForUserAsync(AuthTestConstants.Usernames[0], CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Test]
    public async Task GetRolesForUserAsync_UserExists_ReturnsRoles()
    {
        var user = AuthTestConstants.CreateValidUser();

        _userManager
            .Setup(u => u.FindByNameAsync(user.UserName!))
            .ReturnsAsync(user);

        _userManager
            .Setup(u => u.GetRolesAsync(user))
            .ReturnsAsync(AuthTestConstants.Roles.ToList());

        var result = await _service.GetRolesForUserAsync(user.UserName!, CancellationToken.None);

        result.Should().BeEquivalentTo(AuthTestConstants.Roles);
    }
    
    [Test]
    public async Task GetUserAsync_UserDoesNotExist_ReturnsNull()
    {
        _userManager
            .Setup(u => u.FindByNameAsync(AuthTestConstants.Usernames[0]))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _service.GetUserAsync(
            AuthTestConstants.Usernames[0],
            CancellationToken.None);

        result.Should().BeNull();
    }

    [Test]
    public async Task GetUserAsync_UserExists_ReturnsUserDetails()
    {
        var user = AuthTestConstants.CreateValidUser();

        _userManager
            .Setup(u => u.FindByNameAsync(user.UserName!))
            .ReturnsAsync(user);

        _userManager
            .Setup(u => u.GetRolesAsync(user))
            .ReturnsAsync(AuthTestConstants.Roles.ToList());

        var result = await _service.GetUserAsync(
            user.UserName!,
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.Username.Should().Be(user.UserName);
        result.Roles.Should().BeEquivalentTo(AuthTestConstants.Roles);
    }

    private List<ApplicationUser> SetUpSearchUsers(params string[] usernames)
    {
        var users = usernames.Select(AuthTestConstants.CreateUser).ToList();
        _userManager.SetupGet(u => u.Users).Returns(users.AsQueryable());
        _userManager.Setup(u => u.NormalizeName(It.IsAny<string>())).Returns<string>(name => name.ToUpperInvariant());
        _userManager.Setup(u => u.GetRolesAsync(It.IsAny<ApplicationUser>())).ReturnsAsync([AuthTestConstants.Roles[1]]);
        return users;
    }

    [Test]
    public async Task SearchUsersAsync_NoFilters_ReturnsFirstPageSortedByUsernameWithRoles()
    {
        SetUpSearchUsers("charlie", "Alpha", "bravo");

        var result = await _service.SearchUsersAsync(AuthTestConstants.CreateSearchUsersQuery(), CancellationToken.None);

        result.TotalCount.Should().Be(3);
        result.Items.Select(u => u.Username).Should().Equal("Alpha", "bravo", "charlie");
        result.Items.Should().OnlyContain(u => u.Roles.SequenceEqual(new[] { AuthTestConstants.Roles[1] }));
    }

    [Test]
    public async Task SearchUsersAsync_Descending_ReversesTheOrder()
    {
        SetUpSearchUsers("charlie", "Alpha", "bravo");
        var query = AuthTestConstants.CreateSearchUsersQuery() with { Ascending = false };

        var result = await _service.SearchUsersAsync(query, CancellationToken.None);

        result.Items.Select(u => u.Username).Should().Equal("charlie", "bravo", "Alpha");
    }

    [Test]
    public async Task SearchUsersAsync_SecondPage_SkipsTheFirst()
    {
        SetUpSearchUsers("a1", "a2", "a3", "a4", "a5");
        var query = AuthTestConstants.CreateSearchUsersQuery() with { Page = 2, PageSize = 2 };

        var result = await _service.SearchUsersAsync(query, CancellationToken.None);

        result.TotalCount.Should().Be(5);
        result.Items.Select(u => u.Username).Should().Equal("a3", "a4");
    }

    [Test]
    public async Task SearchUsersAsync_Username_MatchesPartIgnoringCase()
    {
        SetUpSearchUsers("a.hartwell", "k.ashford", "HARTLEY");
        var query = AuthTestConstants.CreateSearchUsersQuery() with { Username = " hart " };

        var result = await _service.SearchUsersAsync(query, CancellationToken.None);

        result.TotalCount.Should().Be(2);
        result.Items.Select(u => u.Username).Should().Equal("a.hartwell", "HARTLEY");
    }

    [Test]
    public async Task SearchUsersAsync_Role_ReturnsOnlyUsersInIt()
    {
        var users = SetUpSearchUsers("alpha", "bravo", "charlie");
        _userManager
            .Setup(u => u.GetUsersInRoleAsync(AuthTestConstants.Roles[0]))
            .ReturnsAsync([users[0], users[2]]);
        var query = AuthTestConstants.CreateSearchUsersQuery() with { Role = AuthTestConstants.Roles[0] };

        var result = await _service.SearchUsersAsync(query, CancellationToken.None);

        result.TotalCount.Should().Be(2);
        result.Items.Select(u => u.Username).Should().Equal("alpha", "charlie");
    }

    [Test]
    public async Task SearchUsersAsync_Cancelled_Throws()
    {
        SetUpSearchUsers("alpha");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => _service.SearchUsersAsync(AuthTestConstants.CreateSearchUsersQuery(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
