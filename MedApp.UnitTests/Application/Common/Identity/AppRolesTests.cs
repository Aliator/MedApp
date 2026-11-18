using FluentAssertions;
using MedApp.Application.Common.Identity;

namespace MedApp.UnitTests.Application.Common.Identity;

[TestFixture]
public sealed class AppRolesTests
{
    [Test]
    public void Names_AdminAndAllStaffBeforeRoles()
    {
        AppRoles.Admin.Should().Be("Admin");
        AppRoles.AllStaffBeforeRoles.Should().Be("All staff (before roles)");
    }

    [TestCase(new[] { "Admin" }, true)]
    [TestCase(new[] { "Nurse" }, true)]
    [TestCase(new[] { "Auditor" }, true)]
    [TestCase(new string[0], false)]
    public void IncludesStaff_AnyRoleAtAll(string[] roles, bool expected)
    {
        AppRoles.IncludesStaff(roles).Should().Be(expected);
    }
}
