using FluentAssertions;
using MedApp.Application.Authentication.Roles.Common;
using MedApp.Application.Authentication.Roles.Queries.GetPermissions;
using MedApp.Application.Authentication.Roles.Queries.GetRoleById;
using MedApp.Application.Authentication.Roles.Queries.GetRoles;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.UnitTests.Common.Fixtures;
using static MedApp.UnitTests.Common.Constants.RolesTestConstants;

namespace MedApp.UnitTests.Application.Auth.Roles.Queries;

/// <summary>GetRoles, GetRoleById and GetPermissions.</summary>
[TestFixture]
public sealed class RoleQueriesHandlerTests : RoleHandlerTestBase
{
    [Test]
    public async Task GetRoles_AdminFirstThenByName_WithHolderCounts()
    {
        Roles.Add(Role() with { Id = Guid.NewGuid(), Name = "zeta team" });
        Roles.Add(Role(), new RoleHolder(HolderId, HolderUsername));
        Roles.Add(AdminRole(), new RoleHolder(ActorId, ActorUsername), new RoleHolder(HolderId, HolderUsername));
        using var cts = new CancellationTokenSource();

        var roles = await new GetRolesHandler(Roles).Handle(new GetRolesQuery(), cts.Token);

        roles.Select(r => r.Name).Should().Equal("Admin", RoleName, "zeta team");
        roles.Select(r => r.HolderCount).Should().Equal(2, 1, 0);
        roles.Should().OnlyContain(r => r.Holders == null);
        Roles.Tokens.Should().OnlyContain(t => t == cts.Token);
    }

    [Test]
    public async Task GetRoleById_ReturnsItWithWhoHoldsIt()
    {
        Roles.Add(Role(), new RoleHolder(HolderId, HolderUsername));
        using var cts = new CancellationTokenSource();

        var role = await new GetRoleByIdHandler(Roles).Handle(new GetRoleByIdQuery(RoleId), cts.Token);

        role.Name.Should().Be(RoleName);
        role.HolderCount.Should().Be(1);
        role.Holders!.Single().UserId.Should().Be(HolderId);
        role.CanDelete.Should().BeFalse();
        Roles.Tokens.Should().OnlyContain(t => t == cts.Token);
    }

    [Test]
    public async Task GetRoleById_Unknown_IsNotFound()
    {
        var act = () => new GetRoleByIdHandler(Roles).Handle(new GetRoleByIdQuery(RoleId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task GetPermissions_ReturnsTheWholeCatalogue()
    {
        var permissions = await new GetPermissionsHandler().Handle(new GetPermissionsQuery(), CancellationToken.None);

        permissions.Select(p => p.Name).Should().Equal(Permissions.Catalogue.Select(p => p.Name));
        permissions.Single(p => p.Name == "ManageRoles").IsAdminOnly.Should().BeTrue();
        permissions.Single(p => p.Name == "UseApp").Group.Should().Be("Base");
    }
}
