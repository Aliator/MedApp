using FluentAssertions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Teams;
using MedApp.Domain.Teams;

namespace MedApp.UnitTests.Application.Common.Teams;

/// <summary>Account types (decision 182), worked out from roles and team roles, as the web app's <c>accountTypeOf</c>.</summary>
[TestFixture]
public sealed class AccountTypesTests
{
    [Test]
    public void Of_AdminRole_IsAdmin_WhateverTheirTeams() =>
        AccountTypes.Of([AppRoles.Admin, "Clinician"], [TeamMemberRole.ClinicAdmin]).Should().Be(AccountTypes.Admin);

    [Test]
    public void Of_ClinicAdminOfAnyTeam_IsClinicAdmin() =>
        AccountTypes.Of(["Clinician"], [TeamMemberRole.Member, TeamMemberRole.ClinicAdmin]).Should().Be(AccountTypes.ClinicAdmin);

    [Test]
    public void Of_AnyoneElse_IsStaff()
    {
        AccountTypes.Of(["Lab"], [TeamMemberRole.Member]).Should().Be(AccountTypes.Staff);
        AccountTypes.Of([], []).Should().Be(AccountTypes.Staff);
    }

    [Test]
    public void Names_AreTheWebAppsTypes() =>
        new[] { AccountTypes.Admin, AccountTypes.ClinicAdmin, AccountTypes.Staff }.Should().Equal("Admin", "ClinicAdmin", "Staff");
}
