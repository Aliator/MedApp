using AutoFixture;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Authentication.Roles.Commands.SetUserRoles;
using MedApp.Application.Common.Identity;
using MedApp.Domain.Activity;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace MedApp.UnitTests.Application.Auth.Roles.Commands.SetUserRoles;

[TestFixture]
public sealed class SetUserRolesHandlerActivityTests
{
    private IFixture _fixture = null!;
    private Mock<IIdentityRoleService> _roles = null!;
    private Mock<IIdentityReadService> _read = null!;
    private RecordingActivityLog _activity = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        _roles = new Mock<IIdentityRoleService>();
        _read = new Mock<IIdentityReadService>();
        _read.Setup(r => r.GetRolesForUserAsync("staff-user", It.IsAny<CancellationToken>())).ReturnsAsync(["User"]);
        _activity = new RecordingActivityLog();
        _fixture.Inject(_roles.Object);
        _fixture.Inject(_read.Object);
        _fixture.Inject<IActivityLog>(_activity);
        _fixture.Inject<IIdentityTransactions>(new PassThroughIdentityTransactions());
    }

    [Test]
    public async Task Handle_Changed_RecordsTheRolesBeforeAndAfter()
    {
        _roles.Setup(r => r.SetRolesAsync("staff-user", It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Success);
        using var cts = new CancellationTokenSource();

        await _fixture.Create<SetUserRolesHandler>().Handle(new SetUserRolesCommand("staff-user", ["Admin", "User"], "test.admin"), cts.Token);

        var entry = _activity.Recorded.Should().ContainSingle().Subject;
        entry.Action.Should().Be(ActivityAction.RolesChanged);
        entry.TargetUsername.Should().Be("staff-user");
        entry.Details.Should().Be("""{"from":["User"],"to":["Admin","User"]}""");
        _read.Verify(r => r.GetRolesForUserAsync("staff-user", cts.Token), Times.Once);
    }

    [Test]
    public async Task Handle_Failed_RecordsNothing()
    {
        _roles.Setup(r => r.SetRolesAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Failed(IdentityErrors.RoleNotFound));

        await _fixture.Create<SetUserRolesHandler>().Handle(new SetUserRolesCommand("staff-user", ["Nope"], "test.admin"), CancellationToken.None);

        _activity.Recorded.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_OwnRoles_RecordsNothing_AndDoesntLookThemUp()
    {
        await _fixture.Create<SetUserRolesHandler>().Handle(new SetUserRolesCommand("test.admin", ["User"], "test.admin"), CancellationToken.None);

        _activity.Recorded.Should().BeEmpty();
        _read.Verify(r => r.GetRolesForUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
