using FluentAssertions;
using MedApp.Application.Common.Identity;

namespace MedApp.UnitTests.Application.Common.Identity;

[TestFixture]
public sealed class AppRolesTests
{
    [Test]
    public void Staff_IsAdminAndUser()
    {
        AppRoles.Staff.Should().Equal(AppRoles.Admin, AppRoles.User);
    }

    [TestCase(new[] { "Admin" }, true)]
    [TestCase(new[] { "User" }, true)]
    [TestCase(new[] { "user" }, true)]
    [TestCase(new[] { "Auditor", "ADMIN" }, true)]
    [TestCase(new[] { "Auditor" }, false)]
    [TestCase(new string[0], false)]
    public void IncludesStaff_Roles_TrueWithAdminOrUserInAnyCase(string[] roles, bool expected)
    {
        AppRoles.IncludesStaff(roles).Should().Be(expected);
    }
}
