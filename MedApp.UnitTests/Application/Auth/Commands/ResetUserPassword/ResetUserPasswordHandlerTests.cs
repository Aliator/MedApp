using AutoFixture;
using FluentAssertions;
using MedApp.Application.Auth.Commands.ResetUserPassword;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Auth.Responses;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace MedApp.UnitTests.Application.Auth.Commands.ResetUserPassword;

[TestFixture]
public sealed class ResetUserPasswordHandlerTests
{
    private IFixture _fixture = null!;
    private Mock<IIdentityUserService> _service = null!;
    private ResetUserPasswordHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();

        _service = new Mock<IIdentityUserService>();
        _fixture.Inject<IIdentityUserService>(_service.Object);

        _handler = _fixture.Create<ResetUserPasswordHandler>();
    }

    [Test]
    public async Task Handle_PasswordReset_ReturnsGeneratedPassword()
    {
        var response = new ResetUserPasswordResponse(
            AuthTestConstants.Passwords[1],
            Array.Empty<IdentityError>());

        var command = new ResetUserPasswordCommand(
            AuthTestConstants.Usernames[0]);

        _service
            .Setup(s => s.ResetUserPasswordAsync(
                command.Username,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.GeneratedPassword.Should().Be(response.GeneratedPassword);
        result.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_PasswordNotReset_ReturnsErrors()
    {
        var errors = new[]
        {
            new IdentityError
            {
                Code = IdentityErrors.UserNotFoundCode,
                Description = "User not found."
            }
        };

        var response = new ResetUserPasswordResponse(
            null,
            errors);

        var command = new ResetUserPasswordCommand(
            AuthTestConstants.Usernames[0]);

        _service
            .Setup(s => s.ResetUserPasswordAsync(
                command.Username,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.GeneratedPassword.Should().BeNull();
        result.Errors.Should().ContainSingle();
    }

    [Test]
    public async Task Handle_CallsService_WithCorrectArguments()
    {
        var response = new ResetUserPasswordResponse(
            AuthTestConstants.Passwords[1],
            Array.Empty<IdentityError>());

        var command = new ResetUserPasswordCommand(
            AuthTestConstants.Usernames[0]);

        _service
            .Setup(s => s.ResetUserPasswordAsync(
                command.Username,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        await _handler.Handle(command, CancellationToken.None);

        _service.Verify(
            s => s.ResetUserPasswordAsync(
                AuthTestConstants.Usernames[0],
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Handle_PassesCancellationToken()
    {
        var response = new ResetUserPasswordResponse(
            AuthTestConstants.Passwords[1],
            Array.Empty<IdentityError>());

        var command = new ResetUserPasswordCommand(
            AuthTestConstants.Usernames[0]);

        using var cts = new CancellationTokenSource();

        _service
            .Setup(s => s.ResetUserPasswordAsync(
                command.Username,
                cts.Token))
            .ReturnsAsync(response);

        await _handler.Handle(command, cts.Token);

        _service.Verify(
            s => s.ResetUserPasswordAsync(
                command.Username,
                cts.Token),
            Times.Once);
    }
}
