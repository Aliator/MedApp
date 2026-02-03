using AutoFixture;
using FluentAssertions;
using MedApp.Application.Auth.Commands.Login;
using MedApp.Application.Common.Authentication;
using MedApp.UnitTests.Common.Fixtures;
using Moq;

namespace MedApp.UnitTests.Application.Auth.Commands.Login;

[TestFixture]
public sealed class LoginHandlerTests
{
    private IFixture _fixture = null!;
    private Mock<IAuthenticationService> _authService = null!;
    private Mock<IJwtTokenGenerator> _tokenGenerator = null!;
    private LoginHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();

        _authService = new Mock<IAuthenticationService>();
        _tokenGenerator = new Mock<IJwtTokenGenerator>();

        _fixture.Inject(_authService.Object);
        _fixture.Inject(_tokenGenerator.Object);

        _handler = _fixture.Create<LoginHandler>();
    }

    [Test]
    public async Task Handle_ValidCredentials_ReturnsGeneratedToken()
    {
        var userId = Guid.NewGuid();
        var username = "username";
        var roles = new[] { "Admin", "User" };
        var token = "jwt-token";

        _authService
            .Setup(s => s.ValidateCredentialsAsync(
                username,
                "password"))
            .ReturnsAsync((userId, username, roles));

        _tokenGenerator
            .Setup(g => g.GenerateToken(
                userId,
                username,
                roles))
            .Returns(token);

        var command = new LoginCommand(
            username,
            "password");

        var result = await _handler.Handle(
            command,
            CancellationToken.None);

        result.Should().Be(token);
    }

    [Test]
    public async Task Handle_UsesValuesReturnedFromAuthenticationService()
    {
        var userId = Guid.NewGuid();
        var username = "username";
        var roles = new[] { "User" };

        _authService
            .Setup(s => s.ValidateCredentialsAsync(
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync((userId, username, roles));

        _tokenGenerator
            .Setup(g => g.GenerateToken(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>()))
            .Returns("token");

        var command = new LoginCommand(
            "user",
            "password");

        await _handler.Handle(command, CancellationToken.None);

        _tokenGenerator.Verify(
            g => g.GenerateToken(
                userId,
                username,
                roles),
            Times.Once);
    }

    [Test]
    public async Task Handle_ValidatesCredentials_Once()
    {
        _authService
            .Setup(s => s.ValidateCredentialsAsync(
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync((Guid.NewGuid(), "username", Array.Empty<string>()));

        _tokenGenerator
            .Setup(g => g.GenerateToken(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>()))
            .Returns("token");

        var command = new LoginCommand(
            "username",
            "password");

        await _handler.Handle(command, CancellationToken.None);

        _authService.Verify(
            s => s.ValidateCredentialsAsync(
                "username",
                "password"),
            Times.Once);
    }
}
