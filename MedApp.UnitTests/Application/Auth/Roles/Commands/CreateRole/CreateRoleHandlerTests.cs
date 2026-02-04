using AutoFixture;
using FluentAssertions;
using MedApp.Application.Auth.Roles.Commands.CreateRole;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace MedApp.UnitTests.Application.Auth.Roles.Commands.CreateRole;

[TestFixture]
public sealed class CreateRoleHandlerTests
{
    private IFixture _fixture = null!;
    private Mock<RoleManager<IdentityRole<Guid>>> _roleManager = null!;
    private CreateRoleHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();

        var store = new Mock<IRoleStore<IdentityRole<Guid>>>();

        _roleManager = new Mock<RoleManager<IdentityRole<Guid>>>(
            store.Object,
            null!,
            null!,
            null!,
            null!);

        _fixture.Inject(_roleManager.Object);

        _handler = _fixture.Create<CreateRoleHandler>();
    }

    [Test]
    public async Task Handle_RoleAlreadyExists_ReturnsExistingRole()
    {
        var role = new IdentityRole<Guid>(AuthTestConstants.Roles[0]);

        _roleManager
            .Setup(r => r.FindByNameAsync(AuthTestConstants.Roles[0]))
            .ReturnsAsync(role);

        var command = new CreateRoleCommand(AuthTestConstants.Roles[0]);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().BeSameAs(role);

        _roleManager.Verify(
            r => r.CreateAsync(It.IsAny<IdentityRole<Guid>>()),
            Times.Never);
    }

    [Test]
    public async Task Handle_RoleDoesNotExist_CreateSucceeds_ReturnsNewRole()
    {
        _roleManager
            .Setup(r => r.FindByNameAsync(AuthTestConstants.Roles[0]))
            .ReturnsAsync((IdentityRole<Guid>?)null);

        _roleManager
            .Setup(r => r.CreateAsync(It.IsAny<IdentityRole<Guid>>()))
            .ReturnsAsync(IdentityResult.Success);

        var command = new CreateRoleCommand(AuthTestConstants.Roles[0]);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Name.Should().Be(AuthTestConstants.Roles[0]);
    }

    [Test]
    public async Task Handle_RoleDoesNotExist_CreateFails_ReturnsNull()
    {
        _roleManager
            .Setup(r => r.FindByNameAsync(AuthTestConstants.Roles[0]))
            .ReturnsAsync((IdentityRole<Guid>?)null);

        _roleManager
            .Setup(r => r.CreateAsync(It.IsAny<IdentityRole<Guid>>()))
            .ReturnsAsync(IdentityResult.Failed());

        var command = new CreateRoleCommand(AuthTestConstants.Roles[0]);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().BeNull();
    }

    [Test]
    public async Task Handle_PassesCancellationToken_ToCreateAsync()
    {
        _roleManager
            .Setup(r => r.FindByNameAsync(AuthTestConstants.Roles[0]))
            .ReturnsAsync((IdentityRole<Guid>?)null);

        using var cts = new CancellationTokenSource();

        _roleManager
            .Setup(r => r.CreateAsync(It.IsAny<IdentityRole<Guid>>()))
            .ReturnsAsync(IdentityResult.Success);

        var command = new CreateRoleCommand(AuthTestConstants.Roles[0]);

        await _handler.Handle(command, cts.Token);

        _roleManager.Verify(
            r => r.CreateAsync(It.IsAny<IdentityRole<Guid>>()),
            Times.Once);
    }
}
