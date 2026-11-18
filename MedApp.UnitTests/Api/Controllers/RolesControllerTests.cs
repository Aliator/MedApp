using AutoFixture;
using FluentAssertions;
using MedApp.API.Controllers;
using MedApp.Application.Authentication.Roles.Commands.CreateRole;
using MedApp.Application.Authentication.Roles.Commands.DeleteRole;
using MedApp.Application.Authentication.Roles.Commands.SetRoleArchived;
using MedApp.Application.Authentication.Roles.Commands.UpdateRole;
using MedApp.Application.Authentication.Roles.Queries.GetPermissions;
using MedApp.Application.Authentication.Roles.Queries.GetRoleById;
using MedApp.Application.Authentication.Roles.Queries.GetRoles;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Roles.Requests;
using MedApp.Contracts.Roles.Responses;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Mvc;
using static MedApp.UnitTests.Common.Constants.ApiTestConstants;

namespace MedApp.UnitTests.Api.Controllers;

[TestFixture]
public sealed class RolesControllerTests
{
    private const string RoleName = "Synthetic visiting role";
    private const string Description = "Synthetic description.";
    private const string Version = "synthetic-version";

    private IFixture _fixture = null!;
    private CancellationTokenSource _cts = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        _cts = new CancellationTokenSource();
    }

    [TearDown]
    public void TearDown() => _cts.Dispose();

    private static RolesController Controller(RecordingMediator mediator) =>
        new RolesController(mediator).WithRequest(SignedIn("Admin"));

    private async Task ExpectOk(object response, object request, Func<RolesController, Task<IActionResult>> act)
    {
        var mediator = new RecordingMediator(_ => response);

        var result = await act(Controller(mediator));

        mediator.Request.Should().Be(request);
        mediator.Tokens.Single().Should().Be(_cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(response);
    }

    [Test]
    public async Task GetPermissions_ReturnsTheCatalogue()
    {
        IReadOnlyList<PermissionResponse> permissions = [];
        await ExpectOk(permissions, new GetPermissionsQuery(), c => c.GetPermissions(_cts.Token));
    }

    [Test]
    public async Task GetRoles_ReturnsThem()
    {
        IReadOnlyList<RoleResponse> roles = [];
        await ExpectOk(roles, new GetRolesQuery(), c => c.GetRoles(_cts.Token));
    }

    [Test]
    public async Task GetRole_ReturnsIt() =>
        await ExpectOk(_fixture.Create<RoleResponse>(), new GetRoleByIdQuery(RecordId), c => c.GetRole(RecordId, _cts.Token));

    [Test]
    public async Task CreateRole_SendsItAsTheSignedInAdmin_AndReturnsItWithItsAddress()
    {
        var role = _fixture.Create<RoleResponse>();
        var mediator = new RecordingMediator(_ => role);
        IReadOnlyList<string> permissions = ["ViewPatients"];

        var result = await Controller(mediator).CreateRole(
            new CreateRoleRequest { Name = RoleName, Description = Description, Permissions = permissions }, _cts.Token);

        mediator.Request.Should().Be(new CreateRoleCommand(RoleName, Description, permissions, UserId, Username));
        mediator.Tokens.Single().Should().Be(_cts.Token);
        var created = result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be($"/api/roles/{role.Id}");
        created.Value.Should().Be(role);
    }

    [Test]
    public async Task CreateRole_NoPermissionsGiven_SendsNone()
    {
        var mediator = new RecordingMediator(_ => _fixture.Create<RoleResponse>());

        await Controller(mediator).CreateRole(new CreateRoleRequest { Name = RoleName }, _cts.Token);

        mediator.Request.Should().BeOfType<CreateRoleCommand>().Which.Permissions.Should().BeEmpty();
    }

    [Test]
    public async Task UpdateRole_SendsTheChangeAndTheVersionSeen()
    {
        IReadOnlyList<string> permissions = ["ViewPatients"];
        await ExpectOk(_fixture.Create<RoleResponse>(),
            new UpdateRoleCommand(RecordId, RoleName, Description, permissions, Version, UserId, Username),
            c => c.UpdateRole(RecordId,
                new UpdateRoleRequest { Name = RoleName, Description = Description, Permissions = permissions, ExpectedVersion = Version },
                _cts.Token));
    }

    [Test]
    public async Task UpdateRole_NoPermissionsGiven_SendsNone()
    {
        var mediator = new RecordingMediator(_ => _fixture.Create<RoleResponse>());

        await Controller(mediator).UpdateRole(RecordId, new UpdateRoleRequest { Name = RoleName, ExpectedVersion = Version }, _cts.Token);

        mediator.Request.Should().BeOfType<UpdateRoleCommand>().Which.Permissions.Should().BeEmpty();
    }

    [Test]
    public async Task ArchiveAndRestoreRole_SetItsArchivedFlagFromTheVersionSeen()
    {
        var role = _fixture.Create<RoleResponse>();
        var request = new RoleVersionRequest { ExpectedVersion = Version };
        await ExpectOk(role, new SetRoleArchivedCommand(RecordId, true, Version, UserId, Username), c => c.ArchiveRole(RecordId, request, _cts.Token));
        await ExpectOk(role, new SetRoleArchivedCommand(RecordId, false, Version, UserId, Username), c => c.RestoreRole(RecordId, request, _cts.Token));
    }

    [Test]
    public async Task DeleteRole_ReturnsNoContent()
    {
        var mediator = new RecordingMediator();

        var result = await Controller(mediator).DeleteRole(RecordId, _cts.Token);

        mediator.Request.Should().Be(new DeleteRoleCommand(RecordId, UserId, Username));
        mediator.Tokens.Single().Should().Be(_cts.Token);
        result.Should().BeOfType<NoContentResult>();
    }

    [Test]
    public async Task EveryAction_NeedsManageRoles_ButTheListManageUsers()
    {
        foreach (var action in ControllerAuthorisation.Actions<RolesController>())
        {
            var needs = action == nameof(RolesController.GetRoles) ? Permission.ManageUsers : Permission.ManageRoles;
            var others = Enum.GetValues<Permission>().Where(p => p != needs).ToArray();

            (await ControllerAuthorisation.MayCallAsync<RolesController>(action, SignedInWith(needs))).Should().BeTrue(action);
            (await ControllerAuthorisation.MayCallAsync<RolesController>(action, SignedInWith(others))).Should().BeFalse(action);
            (await ControllerAuthorisation.MayCallAsync<RolesController>(action, SignedIn(AppRoles.AllStaffBeforeRoles))).Should().BeFalse(action);
            (await ControllerAuthorisation.MayCallAsync<RolesController>(action, SignedIn(AppRoles.Admin))).Should().BeTrue(action);
        }
    }
}
