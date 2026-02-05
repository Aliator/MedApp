using AutoFixture;
using FluentAssertions;
using MedApp.Application.Auth.Roles.Commands.CreateRole;
using MedApp.Application.Common.Identity;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace MedApp.UnitTests.Application.Auth.Roles.Commands.CreateRole;

[TestFixture]
public sealed class CreateRoleHandlerTests
{
    private IFixture _fixture = null!;
    private Mock<IIdentityRoleService> _service = null!;
    private CreateRoleHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();

        _service = new Mock<IIdentityRoleService>();
        _fixture.Inject<IIdentityRoleService>(_service.Object);

        _handler = _fixture.Create<CreateRoleHandler>();
    }

    [Test]
    public async Task Handle_CreateSucceeds_ReturnsRole()
    {
        var role = new IdentityRole<Guid>
        {
            Id = Guid.NewGuid(),
            Name = AuthTestConstants.Roles[0]
        };

        var command = new CreateRoleCommand(role.Name);

        _service
            .Setup(s => s.CreateRoleAsync(
                command.Name,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().BeSameAs(role);
    }

    [Test]
    public async Task Handle_CreateFails_ReturnsNull()
    {
        var command = new CreateRoleCommand(AuthTestConstants.Roles[0]);

        _service
            .Setup(s => s.CreateRoleAsync(
                command.Name,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdentityRole<Guid>?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().BeNull();
    }

    [Test]
    public async Task Handle_CallsService_WithCorrectArguments()
    {
        var command = new CreateRoleCommand(AuthTestConstants.Roles[0]);

        _service
            .Setup(s => s.CreateRoleAsync(
                command.Name,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdentityRole<Guid>?)null);

        await _handler.Handle(command, CancellationToken.None);

        _service.Verify(
            s => s.CreateRoleAsync(
                AuthTestConstants.Roles[0],
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Handle_PassesCancellationToken()
    {
        var command = new CreateRoleCommand(AuthTestConstants.Roles[0]);

        using var cts = new CancellationTokenSource();

        _service
            .Setup(s => s.CreateRoleAsync(
                command.Name,
                cts.Token))
            .ReturnsAsync((IdentityRole<Guid>?)null);

        await _handler.Handle(command, cts.Token);

        _service.Verify(
            s => s.CreateRoleAsync(
                command.Name,
                cts.Token),
            Times.Once);
    }
}
