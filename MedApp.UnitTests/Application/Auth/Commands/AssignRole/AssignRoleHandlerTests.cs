using AutoFixture;
using FluentAssertions;
using MedApp.Application.Auth.Commands.AssignRole;
using MedApp.Application.Common.Identity;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace MedApp.UnitTests.Application.Auth.Commands.AssignRole;

[TestFixture]
public sealed class AssignRoleHandlerTests
{
    private IFixture _fixture = null!;
    private Mock<IIdentityRoleService> _service = null!;
    private AssignRoleHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();

        _service = new Mock<IIdentityRoleService>();
        _fixture.Inject<IIdentityRoleService>(_service.Object);

        _handler = _fixture.Create<AssignRoleHandler>();
    }

    [Test]
    public async Task Handle_RoleAssigned_ReturnsRole()
    {
        var role = new IdentityRole<Guid>
        {
            Id = Guid.NewGuid(),
            Name = "Role"
        };

        var command = new AssignRoleCommand(AuthTestConstants.Roles[0], role.Name);

        _service
            .Setup(s => s.AssignRoleAsync(
                command.Username,
                command.Role,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().BeSameAs(role);
    }


    [Test]
    public async Task Handle_RoleNotAssigned_ReturnsNull()
    {
        var command = new AssignRoleCommand(AuthTestConstants.Usernames[0], AuthTestConstants.Roles[0]);

        _service
            .Setup(s => s.AssignRoleAsync(
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
        var command = new AssignRoleCommand(AuthTestConstants.Usernames[0], AuthTestConstants.Roles[0]);

        _service
            .Setup(s => s.AssignRoleAsync(
                command.Username,
                command.Role,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdentityRole<Guid>?)null);

        await _handler.Handle(command, CancellationToken.None);

        _service.Verify(
            s => s.AssignRoleAsync(
                AuthTestConstants.Usernames[0],
                AuthTestConstants.Roles[0],
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Handle_PassesCancellationToken()
    {
        var command = new AssignRoleCommand(AuthTestConstants.Usernames[0], AuthTestConstants.Roles[0]);

        using var cts = new CancellationTokenSource();

        _service
            .Setup(s => s.AssignRoleAsync(
                command.Username,
                command.Role,
                cts.Token))
            .ReturnsAsync((IdentityRole<Guid>?)null);

        await _handler.Handle(command, cts.Token);

        _service.Verify(
            s => s.AssignRoleAsync(
                command.Username,
                command.Role,
                cts.Token),
            Times.Once);
    }
}
