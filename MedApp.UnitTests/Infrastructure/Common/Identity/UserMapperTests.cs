using FluentAssertions;
using MedApp.Contracts.Authentication.Responses;
using MedApp.Infrastructure.Common.Identity;

namespace MedApp.UnitTests.Infrastructure.Common.Identity;

[TestFixture]
public sealed class UserMapperTests
{
    private static readonly Guid UserId = Guid.Parse("00000012-0000-0000-0000-000000000001");

    [Test]
    public void ToResponse_User_ReturnsTheirIdNameAndRoles()
    {
        var response = UserMapper.ToResponse(new ApplicationUser { Id = UserId, UserName = "test.staff" }, ["Admin", "User"]);

        response.Should().BeEquivalentTo(new UserResponse(UserId, "test.staff", ["Admin", "User"]));
    }

    [Test]
    public void ToResponse_UserWithNoName_HasAnEmptyName()
    {
        UserMapper.ToResponse(new ApplicationUser { Id = UserId }, []).Username.Should().BeEmpty();
    }

    [Test]
    public void ToResponse_ConfirmedAddressPendingOneAndTwoFactor_AreReturned()
    {
        var user = new ApplicationUser
        {
            Id = UserId, UserName = "test.staff", Email = "staff@example.test", EmailConfirmed = true,
            PendingEmail = "new@example.test", TwoFactorEnabled = true,
        };

        UserMapper.ToResponse(user, []).Should().BeEquivalentTo(
            new UserResponse(UserId, "test.staff", [], "staff@example.test", "new@example.test", true));
    }

    [Test]
    public void ToResponse_UnconfirmedAddress_IsNotTheAccounts()
    {
        var user = new ApplicationUser { Id = UserId, UserName = "test.staff", Email = "staff@example.test" };

        UserMapper.ToResponse(user, []).Email.Should().BeNull();
    }
}
