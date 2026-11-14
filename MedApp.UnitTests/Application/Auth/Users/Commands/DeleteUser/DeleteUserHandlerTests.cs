using AutoFixture;
using FluentAssertions;
using MedApp.Application.Authentication.Users.Commands.DeleteUser;
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

        // The admin's password is right unless a test says otherwise.
        _service
            .Setup(s => s.VerifyPasswordAsync(AuthTestConstants.Usernames[1], AuthTestConstants.ValidPassword, It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Success);
    }

    /// <summary>Usernames[1] (an admin) deletes Usernames[0], confirming with their password.</summary>
    private static DeleteUserCommand Command() =>
        new(AuthTestConstants.Usernames[0], AuthTestConstants.Usernames[1], AuthTestConstants.ValidPassword);

    [Test]
    public async Task Handle_WrongPassword_ReturnsTheFailureWithoutDeleting()
    {
        var wrong = IdentityResult.Failed(IdentityErrors.PasswordIncorrect);
        _service
            .Setup(s => s.VerifyPasswordAsync(AuthTestConstants.Usernames[1], AuthTestConstants.ValidPassword, It.IsAny<CancellationToken>()))
            .ReturnsAsync(wrong);

        var result = await _handler.Handle(Command(), CancellationToken.None);

        result.Should().BeSameAs(wrong);
        _service.Verify(s => s.DeleteUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_UserDeleted_ReturnsSuccess()
    {
        var command = Command();

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
        var command = Command();

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
        var command = Command();

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
        var command = Command();

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
