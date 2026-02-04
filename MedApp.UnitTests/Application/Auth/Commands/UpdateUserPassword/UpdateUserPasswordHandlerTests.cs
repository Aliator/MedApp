using AutoFixture;
using FluentAssertions;
using MedApp.Application.Auth.Commands.UpdateUserPassword;
using MedApp.Application.Common.Identity;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace MedApp.UnitTests.Application.Auth.Commands.UpdateUserPassword;

[TestFixture]
public sealed class UpdateUserPasswordHandlerTests
{
    private IFixture _fixture = null!;
    private Mock<IIdentityUserService> _service = null!;
    private UpdateUserPasswordHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();

        _service = new Mock<IIdentityUserService>();
        _fixture.Inject<IIdentityUserService>(_service.Object);

        _handler = _fixture.Create<UpdateUserPasswordHandler>();
    }

    [Test]
    public async Task Handle_PasswordUpdated_ReturnsSuccess()
    {
        var command = new UpdateUserPasswordCommand(
            AuthTestConstants.Usernames[0],
            AuthTestConstants.Passwords[0],
            AuthTestConstants.Passwords[1]);

        _service
            .Setup(s => s.UpdateUserPasswordAsync(
                command.Username,
                command.OldPassword,
                command.NewPassword,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
    }

    [Test]
    public async Task Handle_PasswordNotUpdated_ReturnsFailure()
    {
        var command = new UpdateUserPasswordCommand(
            AuthTestConstants.Usernames[0],
            AuthTestConstants.Passwords[0],
            AuthTestConstants.Passwords[1]);

        _service
            .Setup(s => s.UpdateUserPasswordAsync(
                command.Username,
                command.OldPassword,
                command.NewPassword,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Failed());

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
    }

    [Test]
    public async Task Handle_CallsService_WithCorrectArguments()
    {
        var command = new UpdateUserPasswordCommand(
            AuthTestConstants.Usernames[0],
            AuthTestConstants.Passwords[0],
            AuthTestConstants.Passwords[1]);

        _service
            .Setup(s => s.UpdateUserPasswordAsync(
                command.Username,
                command.OldPassword,
                command.NewPassword,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Success);

        await _handler.Handle(command, CancellationToken.None);

        _service.Verify(
            s => s.UpdateUserPasswordAsync(
                AuthTestConstants.Usernames[0],
                AuthTestConstants.Passwords[0],
                AuthTestConstants.Passwords[1],
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Handle_PassesCancellationToken()
    {
        var command = new UpdateUserPasswordCommand(
            AuthTestConstants.Usernames[0],
            AuthTestConstants.Passwords[0],
            AuthTestConstants.Passwords[1]);

        using var cts = new CancellationTokenSource();

        _service
            .Setup(s => s.UpdateUserPasswordAsync(
                command.Username,
                command.OldPassword,
                command.NewPassword,
                cts.Token))
            .ReturnsAsync(IdentityResult.Success);

        await _handler.Handle(command, cts.Token);

        _service.Verify(
            s => s.UpdateUserPasswordAsync(
                command.Username,
                command.OldPassword,
                command.NewPassword,
                cts.Token),
            Times.Once);
    }
}
