using FluentAssertions;
using MedApp.Application.Authentication.Roles.Common;
using MedApp.Application.Common.Identity;
using static MedApp.UnitTests.Common.Constants.RolesTestConstants;

namespace MedApp.UnitTests.Application.Auth.Roles.Common;

[TestFixture]
public sealed class RoleMapperTests
{
    [Test]
    public void ToResponse_EditableRole_GivesWhatItGrants_WithoutUseAppOrNamesItCannotHave()
    {
        var role = Role("WorkPathways", "UseApp", "ManageUsers", "RetiredPermission", "ViewPatients");

        var response = RoleMapper.ToResponse(role, 3);

        response.Id.Should().Be(RoleId);
        response.Name.Should().Be(RoleName);
        response.Description.Should().Be(Description);
        response.Permissions.Should().Equal("ViewPatients", "WorkPathways");
        response.HolderCount.Should().Be(3);
        response.Version.Should().Be(RoleVersion);
        response.CanEdit.Should().BeTrue();
        response.CanDelete.Should().BeFalse();
        response.IsBuiltIn.Should().BeFalse();
        response.Holders.Should().BeNull();
        response.CreatedAt.Should().Be(CreatedAt);
        response.LastUpdated.Should().Be(CreatedAt);
    }

    [Test]
    public void ToResponse_NobodyHoldsIt_CanBeDeleted_AndListsItsHoldersWhenGiven()
    {
        var response = RoleMapper.ToResponse(Role() with { IsArchived = true }, 0, []);

        response.CanDelete.Should().BeTrue();
        response.IsArchived.Should().BeTrue();
        response.Holders.Should().BeEmpty();
    }

    [Test]
    public void ToResponse_TheBuiltInAdminRole_GivesItsFixedSet_AndCanNeitherBeEditedNorDeleted()
    {
        var response = RoleMapper.ToResponse(AdminRole(), 0, [new RoleHolder(HolderId, HolderUsername)]);

        response.IsBuiltIn.Should().BeTrue();
        response.CanEdit.Should().BeFalse();
        response.CanDelete.Should().BeFalse();
        response.Permissions.Should().BeEquivalentTo(Permissions.Admin.Where(p => p != Permission.UseApp).Select(p => p.ToString()));
        response.Permissions.Should().NotContain("TriageReferrals");
        response.Holders!.Single().Username.Should().Be(HolderUsername);
    }

    [Test]
    public void ToResponses_AdminFirst_ThenByNameIgnoringCase()
    {
        var responses = RoleMapper.ToResponses(
            [Role() with { Id = Guid.NewGuid(), Name = "beta" }, Role() with { Name = "Alpha" }, AdminRole()],
            new Dictionary<Guid, int> { [RoleId] = 2 });

        responses.Select(r => r.Name).Should().Equal("Admin", "Alpha", "beta");
        responses.Select(r => r.HolderCount).Should().Equal(0, 2, 0);
    }

    [Test]
    public void ToResponse_Permission_GivesItsNameGroupDescriptionAndWhetherAdminOnly()
    {
        var response = RoleMapper.ToResponse(Permissions.Describe(Permission.ArrangeMeetings));

        response.Name.Should().Be("ArrangeMeetings");
        response.Group.Should().Be("AppointmentsAndCalendars");
        response.Description.Should().Be(Permissions.Describe(Permission.ArrangeMeetings).Description);
        response.IsAdminOnly.Should().BeFalse();
        RoleMapper.ToResponse(Permissions.Describe(Permission.ViewAudit)).IsAdminOnly.Should().BeTrue();
    }
}
