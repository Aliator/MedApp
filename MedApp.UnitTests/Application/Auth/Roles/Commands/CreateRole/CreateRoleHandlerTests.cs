using FluentAssertions;
using MedApp.Application.Auth.Roles.Commands.CreateRole;
using MedApp.Application.Common.Identity;
using Microsoft.AspNetCore.Identity;
using Moq;
using NUnit.Framework;

namespace MedApp.UnitTests.Application.Auth.Roles.Commands.CreateRole;

public sealed class CreateRoleHandlerTests
{
    [Test]
    public async Task Handle_PassesCancellationToken_ToService()
    {
        var ct = new CancellationTokenSource().Token;
        var roleService = new Mock<IIdentityRoleService>(MockBehavior.Strict);

        roleService
            .Setup(s => s.CreateRoleAsync("Admin", ct))
            .ReturnsAsync(new IdentityRole<Guid>("Admin"));

        var handler = new CreateRoleHandler(roleService.Object);

        await handler.Handle(new CreateRoleCommand("Admin"), ct);

        roleService.Verify(s => s.CreateRoleAsync("Admin", ct), Times.Once);
    }

    [Test]
    public async Task Handle_RoleAlreadyExists_ReturnsExistingRole()
    {
        var ct = CancellationToken.None;
        var roleService = new Mock<IIdentityRoleService>(MockBehavior.Strict);

        var existing = new IdentityRole<Guid>("Admin");

        roleService
            .Setup(s => s.CreateRoleAsync("Admin", ct))
            .ReturnsAsync(existing);

        var handler = new CreateRoleHandler(roleService.Object);

        var result = await handler.Handle(new CreateRoleCommand("Admin"), ct);

        result.Should().BeSameAs(existing);
        result!.Name.Should().Be("Admin");
    }

    [Test]
    public async Task Handle_CreateFails_ReturnsNull()
    {
        var ct = CancellationToken.None;
        var roleService = new Mock<IIdentityRoleService>(MockBehavior.Strict);

        roleService
            .Setup(s => s.CreateRoleAsync("Admin", ct))
            .ReturnsAsync((IdentityRole<Guid>?)null);

        var handler = new CreateRoleHandler(roleService.Object);

        var result = await handler.Handle(new CreateRoleCommand("Admin"), ct);

        result.Should().BeNull();
    }

    [Test]
    public async Task Handle_CreateSucceeds_ReturnsRole()
    {
        var ct = CancellationToken.None;
        var roleService = new Mock<IIdentityRoleService>(MockBehavior.Strict);

        var created = new IdentityRole<Guid>("Admin");

        roleService
            .Setup(s => s.CreateRoleAsync("Admin", ct))
            .ReturnsAsync(created);

        var handler = new CreateRoleHandler(roleService.Object);

        var result = await handler.Handle(new CreateRoleCommand("Admin"), ct);

        result.Should().BeSameAs(created);
        result!.Name.Should().Be("Admin");
    }
}
