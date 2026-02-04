using AutoFixture;
using FluentAssertions;
using MedApp.Application.Auth.Users.Commands.DeleteUser;
using MedApp.Application.Common.Identity;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace MedApp.UnitTests.Application.Auth.Users.Commands.DeleteUser;

[TestFixture]
public sealed class DeleteUserHandlerTests
{
    private IFixture _fixture = null!;
    private Mock<IIdentityUserService> _service = null!;
    private DeleteUserHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();

        _service = new Mock<IIdentityUserService>();
        _fixture.Inject<IIdentityUserService>(_service.Object);

        _handler = _fixture.Create<DeleteUserHandler>();
    }

    [Test]
    public async Task Handle_UserDeleted_ReturnsSuccess()
    {
        var command = new DeleteUserCommand(AuthTestConstants.Usernames[0]);

        var identityResult = IdentityResult.Success;

        _service
            .Setup(s => s.DeleteUserAsync(
                command.Username,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(identityResult);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().BeSameAs(identityResult);
    }

    [Test]
    public async Task Handle_UserNotDeleted_ReturnsFailure()
    {
        var command = new DeleteUserCommand(AuthTestConstants.Usernames[0]);

        var identityResult = IdentityResult.Failed();

        _service
            .Setup(s => s.DeleteUserAsync(
                command.Username,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(identityResult);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
    }

    [Test]
    public async Task Handle_CallsService_WithCorrectArguments()
    {
        var command = new DeleteUserCommand(AuthTestConstants.Usernames[0]);

        _service
            .Setup(s => s.DeleteUserAsync(
                command.Username,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Success);

        await _handler.Handle(command, CancellationToken.None);

        _service.Verify(
            s => s.DeleteUserAsync(
                command.Username,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Handle_PassesCancellationToken()
    {
        var command = new DeleteUserCommand(AuthTestConstants.Usernames[0]);

        using var cts = new CancellationTokenSource();

        _service
            .Setup(s => s.DeleteUserAsync(
                command.Username,
                cts.Token))
            .ReturnsAsync(IdentityResult.Success);

        await _handler.Handle(command, cts.Token);

        _service.Verify(
            s => s.DeleteUserAsync(
                command.Username,
                cts.Token),
            Times.Once);
    }
}
