using AutoFixture;
using FluentAssertions;
using MedApp.Application.Authentication.Users.Queries.GetUserByUsername;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Authentication.Responses;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;

namespace MedApp.UnitTests.Application.Auth.Users.Queries.GetUserByUsername;

[TestFixture]
public sealed class GetUserByUsernameHandlerTests
{
    private IFixture _fixture = null!;
    private Mock<IIdentityReadService> _service = null!;
    private GetUserByUsernameHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();

        _service = new Mock<IIdentityReadService>();
        _fixture.Inject<IIdentityReadService>(_service.Object);

        _handler = _fixture.Create<GetUserByUsernameHandler>();
    }

    [Test]
    public async Task Handle_UserExists_ReturnsUserDetails()
    {
        var userId = Guid.NewGuid();

        var user = new UserResponse(
            userId,
            AuthTestConstants.Usernames[0],
            AuthTestConstants.Roles);

        var query = new GetUserByUsernameQuery(
            AuthTestConstants.Usernames[0]);

        _service
            .Setup(s => s.GetUserAsync(
                query.Username,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().BeSameAs(user);
    }

    [Test]
    public async Task Handle_UserDoesNotExist_ReturnsNull()
    {
        var query = new GetUserByUsernameQuery(
            AuthTestConstants.Usernames[0]);

        _service
            .Setup(s => s.GetUserAsync(
                query.Username,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserResponse?)null);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().BeNull();
    }

    [Test]
    public async Task Handle_CallsService_WithCorrectArguments()
    {
        var query = new GetUserByUsernameQuery(
            AuthTestConstants.Usernames[0]);

        _service
            .Setup(s => s.GetUserAsync(
                query.Username,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserResponse?)null);

        await _handler.Handle(query, CancellationToken.None);

        _service.Verify(
            s => s.GetUserAsync(
                AuthTestConstants.Usernames[0],
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Handle_PassesCancellationToken()
    {
        var query = new GetUserByUsernameQuery(
            AuthTestConstants.Usernames[0]);

        using var cts = new CancellationTokenSource();

        _service
            .Setup(s => s.GetUserAsync(
                query.Username,
                cts.Token))
            .ReturnsAsync((UserResponse?)null);

        await _handler.Handle(query, cts.Token);

        _service.Verify(
            s => s.GetUserAsync(
                query.Username,
                cts.Token),
            Times.Once);
    }
}
