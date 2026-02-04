using AutoFixture;
using FluentAssertions;
using MedApp.Application.Auth.Commands.CreateUser;
using MedApp.Application.Common.Identity;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace MedApp.UnitTests.Application.Auth.Commands.CreateUser;

[TestFixture]
public sealed class CreateUserHandlerTests
{
    private IFixture _fixture = null!;
    private Mock<IIdentityUserService> _userService = null!;
    private CreateUserHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();

        _userService = new Mock<IIdentityUserService>();
        _fixture.Inject(_userService.Object);

        _handler = _fixture.Create<CreateUserHandler>();
    }

    [Test]
    public async Task Handle_ValidCommand_ReturnsIdentityResult()
    {
        var result = IdentityResult.Success;

        _userService
            .Setup(s => s.CreateUserAsync(
                AuthTestConstants.Usernames[0],
                AuthTestConstants.Passwords[0],
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        var command = new CreateUserCommand(
            AuthTestConstants.Usernames[0],
            AuthTestConstants.Passwords[0]);

        var response = await _handler.Handle(
            command,
            CancellationToken.None);

        response.Should().Be(result);
    }

    [Test]
    public async Task Handle_PassesCorrectArguments_ToService()
    {
        _userService
            .Setup(s => s.CreateUserAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Success);

        var command = new CreateUserCommand(
            AuthTestConstants.Usernames[0],
            AuthTestConstants.Passwords[0]);

        await _handler.Handle(command, CancellationToken.None);

        _userService.Verify(
            s => s.CreateUserAsync(
                AuthTestConstants.Usernames[0],
                AuthTestConstants.Passwords[0],
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Handle_PassesCancellationToken_ToService()
    {
        _userService
            .Setup(s => s.CreateUserAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Success);

        using var cts = new CancellationTokenSource();

        var command = new CreateUserCommand(
            AuthTestConstants.Usernames[0],
            AuthTestConstants.Passwords[0]);

        await _handler.Handle(command, cts.Token);

        _userService.Verify(
            s => s.CreateUserAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                cts.Token),
            Times.Once);
    }
}
