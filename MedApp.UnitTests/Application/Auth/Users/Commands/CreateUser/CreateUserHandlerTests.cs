using AutoFixture;
using FluentAssertions;
using MedApp.Application.Authentication.Users.Commands.CreateUser;
using MedApp.Application.Common.Identity;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace MedApp.UnitTests.Application.Auth.Users.Commands.CreateUser;

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
    public async Task Handle_ValidCommand_CreatesThatUsernameAndReturnsTheServicesPassword()
    {
        string? createdUsername = null;

        // The service makes the password for whichever name it's given; the handler must pass on this one and
        // return the password the service made for it.
        _userService
            .Setup(s => s.CreateUserAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string username, CancellationToken _) =>
            {
                createdUsername = username;
                return CreateUserResult.Created(AuthTestConstants.ValidPassword);
            });

        var response = await _handler.Handle(
            new CreateUserCommand(AuthTestConstants.Usernames[1]),
            CancellationToken.None);

        createdUsername.Should().Be(AuthTestConstants.Usernames[1]);
        response.Succeeded.Should().BeTrue();
        response.Result.Succeeded.Should().BeTrue();
        response.GeneratedPassword.Should().Be(AuthTestConstants.ValidPassword);
    }

    [Test]
    public async Task Handle_ServiceFails_ReturnsTheFailureWithoutAPassword()
    {
        var failed = CreateUserResult.Failed(IdentityResult.Failed(new IdentityErrorDescriber().DuplicateUserName(AuthTestConstants.Usernames[0])));

        _userService
            .Setup(s => s.CreateUserAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(failed);

        var response = await _handler.Handle(
            new CreateUserCommand(AuthTestConstants.Usernames[0]),
            CancellationToken.None);

        response.Succeeded.Should().BeFalse();
        response.Result.Errors.Should().ContainSingle().Which.Code.Should().Be(nameof(IdentityErrorDescriber.DuplicateUserName));
        response.GeneratedPassword.Should().BeNull();
    }

    [Test]
    public async Task Handle_PassesCorrectArguments_ToService()
    {
        _userService
            .Setup(s => s.CreateUserAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateUserResult.Created(AuthTestConstants.ValidPassword));

        var command = new CreateUserCommand(AuthTestConstants.Usernames[0]);

        await _handler.Handle(command, CancellationToken.None);

        _userService.Verify(
            s => s.CreateUserAsync(
                AuthTestConstants.Usernames[0],
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Handle_PassesCancellationToken_ToService()
    {
        _userService
            .Setup(s => s.CreateUserAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateUserResult.Created(AuthTestConstants.ValidPassword));

        using var cts = new CancellationTokenSource();

        var command = new CreateUserCommand(AuthTestConstants.Usernames[0]);

        await _handler.Handle(command, cts.Token);

        _userService.Verify(
            s => s.CreateUserAsync(
                It.IsAny<string>(),
                cts.Token),
            Times.Once);
    }
}
