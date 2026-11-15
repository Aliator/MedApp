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
}
