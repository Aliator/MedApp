using AutoFixture;
using FluentAssertions;
using MedApp.Application.Auth.Roles.Commands.DeleteRole;
using MedApp.Application.Common.Identity;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace MedApp.UnitTests.Application.Auth.Roles.Commands.DeleteRole;

[TestFixture]
public sealed class DeleteRoleHandlerTests
{
    private IFixture _fixture = null!;
    private Mock<IIdentityRoleService> _service = null!;
    private DeleteRoleHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();

        _service = new Mock<IIdentityRoleService>();
        _fixture.Inject<IIdentityRoleService>(_service.Object);

        _handler = _fixture.Create<DeleteRoleHandler>();
    }

    [Test]
    public async Task Handle_RoleDeleted_ReturnsSuccess()
    {
        var command = new DeleteRoleCommand(AuthTestConstants.Roles[0]);

        var identityResult = IdentityResult.Success;

        _service
            .Setup(s => s.DeleteRoleAsync(
                command.RoleName,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(identityResult);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().BeSameAs(identityResult);
    }

    [Test]
    public async Task Handle_RoleNotDeleted_ReturnsFailure()
    {
        var command = new DeleteRoleCommand(AuthTestConstants.Roles[0]);

        var identityResult = IdentityResult.Failed();

        _service
            .Setup(s => s.DeleteRoleAsync(
                command.RoleName,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(identityResult);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
    }

    [Test]
    public async Task Handle_CallsService_WithCorrectArguments()
    {
        var command = new DeleteRoleCommand(AuthTestConstants.Roles[0]);

        _service
            .Setup(s => s.DeleteRoleAsync(
                command.RoleName,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Success);

        await _handler.Handle(command, CancellationToken.None);

        _service.Verify(
            s => s.DeleteRoleAsync(
                command.RoleName,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Handle_PassesCancellationToken()
    {
        var command = new DeleteRoleCommand(AuthTestConstants.Roles[0]);

        using var cts = new CancellationTokenSource();

        _service
            .Setup(s => s.DeleteRoleAsync(
                command.RoleName,
                cts.Token))
            .ReturnsAsync(IdentityResult.Success);

        await _handler.Handle(command, cts.Token);

        _service.Verify(
            s => s.DeleteRoleAsync(
                command.RoleName,
                cts.Token),
            Times.Once);
    }
}
