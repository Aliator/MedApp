using AutoFixture;
using FluentAssertions;
using MedApp.Application.Auth.Roles.Commands.RevokeRole;
using MedApp.Application.Common.Identity;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace MedApp.UnitTests.Application.Auth.Roles.Commands.RevokeRole;

[TestFixture]
public sealed class RevokeRoleHandlerTests
{
    private IFixture _fixture = null!;
    private Mock<IIdentityRoleService> _service = null!;
    private RevokeRoleHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();

        _service = new Mock<IIdentityRoleService>();
        _fixture.Inject<IIdentityRoleService>(_service.Object);

        _handler = _fixture.Create<RevokeRoleHandler>();
    }

    [Test]
    public async Task Handle_RoleRevoked_ReturnsRole()
    {
        var role = new IdentityRole<Guid>
        {
            Id = Guid.NewGuid(),
            Name = AuthTestConstants.Roles[0]
        };

        var command = new RevokeRoleCommand(AuthTestConstants.Usernames[0], role.Name);

        _service
            .Setup(s => s.RevokeRoleAsync(
                command.Username,
                command.Role,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().BeSameAs(role);
    }

    [Test]
    public async Task Handle_RoleNotRevoked_ReturnsNull()
    {
        var command = new RevokeRoleCommand(AuthTestConstants.Usernames[0], AuthTestConstants.Roles[0]);

        _service
            .Setup(s => s.RevokeRoleAsync(
                command.Username,
                command.Role,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdentityRole<Guid>?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().BeNull();
    }

    [Test]
    public async Task Handle_CallsService_WithCorrectArguments()
    {
        var command = new RevokeRoleCommand(AuthTestConstants.Usernames[0], AuthTestConstants.Roles[0]);

        _service
            .Setup(s => s.RevokeRoleAsync(
                command.Username,
                command.Role,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdentityRole<Guid>?)null);

        await _handler.Handle(command, CancellationToken.None);

        _service.Verify(
            s => s.RevokeRoleAsync(
                AuthTestConstants.Usernames[0],
                AuthTestConstants.Roles[0],
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Handle_PassesCancellationToken()
    {
        var command = new RevokeRoleCommand(AuthTestConstants.Usernames[0], AuthTestConstants.Roles[0]);

        using var cts = new CancellationTokenSource();

        _service
            .Setup(s => s.RevokeRoleAsync(
                command.Username,
                command.Role,
                cts.Token))
            .ReturnsAsync((IdentityRole<Guid>?)null);

        await _handler.Handle(command, cts.Token);

        _service.Verify(
            s => s.RevokeRoleAsync(
                command.Username,
                command.Role,
                cts.Token),
            Times.Once);
    }
}
