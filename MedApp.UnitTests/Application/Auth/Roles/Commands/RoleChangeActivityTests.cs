using AutoFixture;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Authentication.Roles.Commands.AssignRole;
using MedApp.Application.Authentication.Roles.Commands.CreateRole;
using MedApp.Application.Authentication.Roles.Commands.DeleteRole;
using MedApp.Application.Authentication.Roles.Commands.RevokeRole;
using MedApp.Application.Common.Identity;
using MedApp.Domain.Activity;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace MedApp.UnitTests.Application.Auth.Roles.Commands;

/// <summary>What assigning, revoking, creating and deleting roles record in the activity log.</summary>
[TestFixture]
public sealed class RoleChangeActivityTests
{
    private IFixture _fixture = null!;
    private Mock<IIdentityRoleService> _roles = null!;
    private RecordingActivityLog _activity = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        // Not the last admin, unless a test says so.
        _fixture.Inject(new Mock<ILastAdminGuard>().Object);
        _fixture.Inject<IIdentityTransactions>(new PassThroughIdentityTransactions());
        _roles = new Mock<IIdentityRoleService>();
        _activity = new RecordingActivityLog();
        _fixture.Inject(_roles.Object);
        _fixture.Inject<IActivityLog>(_activity);
    }

    private static IdentityRole<Guid> Role(string name) => new(name) { Id = Guid.NewGuid() };

    [Test]
    public async Task AssignRole_Assigned_RecordsTheRoleAdded()
    {
        _roles.Setup(r => r.AssignRoleAsync("staff-user", "Admin", It.IsAny<CancellationToken>())).ReturnsAsync(Role("Admin"));

        await _fixture.Create<AssignRoleHandler>().Handle(new AssignRoleCommand("staff-user", "Admin"), CancellationToken.None);

        var entry = _activity.Recorded.Should().ContainSingle().Subject;
        entry.Action.Should().Be(ActivityAction.RolesChanged);
        entry.TargetUsername.Should().Be("staff-user");
        entry.Details.Should().Be("""{"added":"Admin"}""");
    }

    [Test]
    public async Task AssignRole_Failed_RecordsNothing()
    {
        _roles.Setup(r => r.AssignRoleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((IdentityRole<Guid>?)null);

        await _fixture.Create<AssignRoleHandler>().Handle(new AssignRoleCommand("staff-user", "Admin"), CancellationToken.None);

        _activity.Recorded.Should().BeEmpty();
    }

    [Test]
    public async Task RevokeRole_Revoked_RecordsTheRoleRemoved()
    {
        _roles.Setup(r => r.RevokeRoleAsync("staff-user", "Admin", It.IsAny<CancellationToken>())).ReturnsAsync(Role("Admin"));

        await _fixture.Create<RevokeRoleHandler>().Handle(new RevokeRoleCommand("staff-user", "Admin"), CancellationToken.None);

        _activity.Recorded.Should().ContainSingle().Which.Details.Should().Be("""{"removed":"Admin"}""");
    }

    [Test]
    public async Task RevokeRole_Failed_RecordsNothing()
    {
        _roles.Setup(r => r.RevokeRoleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((IdentityRole<Guid>?)null);

        await _fixture.Create<RevokeRoleHandler>().Handle(new RevokeRoleCommand("staff-user", "Admin"), CancellationToken.None);

        _activity.Recorded.Should().BeEmpty();
    }

    [Test]
    public async Task CreateRole_Created_RecordsIt()
    {
        _roles.Setup(r => r.CreateRoleAsync("Auditor", It.IsAny<CancellationToken>())).ReturnsAsync(Role("Auditor"));

        await _fixture.Create<CreateRoleHandler>().Handle(new CreateRoleCommand("Auditor"), CancellationToken.None);

        var entry = _activity.Recorded.Should().ContainSingle().Subject;
        entry.Action.Should().Be(ActivityAction.RoleCreated);
        entry.Details.Should().Be("""{"role":"Auditor"}""");
    }

    [Test]
    public async Task CreateRole_Failed_RecordsNothing()
    {
        _roles.Setup(r => r.CreateRoleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((IdentityRole<Guid>?)null);

        await _fixture.Create<CreateRoleHandler>().Handle(new CreateRoleCommand("Auditor"), CancellationToken.None);

        _activity.Recorded.Should().BeEmpty();
    }

    [Test]
    public async Task DeleteRole_Deleted_RecordsIt()
    {
        _roles.Setup(r => r.DeleteRoleAsync("Auditor", It.IsAny<CancellationToken>())).ReturnsAsync(IdentityResult.Success);

        await _fixture.Create<DeleteRoleHandler>().Handle(new DeleteRoleCommand("Auditor"), CancellationToken.None);

        var entry = _activity.Recorded.Should().ContainSingle().Subject;
        entry.Action.Should().Be(ActivityAction.RoleDeleted);
        entry.Details.Should().Be("""{"role":"Auditor"}""");
    }

    [Test]
    public async Task DeleteRole_Failed_RecordsNothing()
    {
        _roles.Setup(r => r.DeleteRoleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Failed(IdentityErrors.RoleNotFound));

        await _fixture.Create<DeleteRoleHandler>().Handle(new DeleteRoleCommand("Auditor"), CancellationToken.None);

        _activity.Recorded.Should().BeEmpty();
    }
}
