using AutoFixture;
using FluentAssertions;
using MedApp.Application.Authentication.Roles.Commands.SetUserRoles;
using MedApp.Application.Common.Identity;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace MedApp.UnitTests.Application.Auth.Roles.Commands.SetUserRoles;

[TestFixture]
public sealed class SetUserRolesHandlerTests
{
    private IFixture _fixture = null!;
    private Mock<IIdentityRoleService> _service = null!;
    private SetUserRolesHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();

        _service = new Mock<IIdentityRoleService>();
        _fixture.Inject(_service.Object);

        _handler = _fixture.Create<SetUserRolesHandler>();
    }

    [Test]
    public async Task Handle_AnotherUser_SetsTheirRolesAndReturnsTheResult()
    {
        var command = new SetUserRolesCommand(AuthTestConstants.Usernames[1], AuthTestConstants.Roles, AuthTestConstants.Usernames[0]);
        using var cts = new CancellationTokenSource();

        _service
            .Setup(s => s.SetRolesAsync(command.Username, command.Roles, cts.Token))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _handler.Handle(command, cts.Token);

        result.Succeeded.Should().BeTrue();
        _service.Verify(s => s.SetRolesAsync(command.Username, command.Roles, cts.Token), Times.Once);
    }

    [Test]
    public async Task Handle_OwnRoles_FailsWithoutChangingThem()
    {
        var command = new SetUserRolesCommand(
            AuthTestConstants.Usernames[0].ToUpperInvariant(),
            [AuthTestConstants.Roles[1]],
            AuthTestConstants.Usernames[0]);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == IdentityErrors.OwnRolesCode);
        _service.Verify(
            s => s.SetRolesAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
