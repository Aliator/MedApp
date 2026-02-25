using AutoFixture;
using FluentAssertions;
using MedApp.Application.Authentication.Users.Queries.GetAllUsers;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Authentication.Responses;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;

namespace MedApp.UnitTests.Application.Auth.Users.Queries.GetAllUsers;

[TestFixture]
public sealed class GetAllUsersHandlerTests
{
    private IFixture _fixture = null!;
    private Mock<IIdentityReadService> _identityReadService = null!;
    private GetAllUsersHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();

        _identityReadService = new Mock<IIdentityReadService>();
        _fixture.Inject(_identityReadService.Object);

        _handler = _fixture.Create<GetAllUsersHandler>();
    }

    [Test]
    public async Task Handle_ReturnsAllUsers()
    {
        UserResponse[] users =
        [
            new(Guid.NewGuid(), AuthTestConstants.Usernames[0], new[] { AuthTestConstants.Roles[0] }),
            new(Guid.NewGuid(), AuthTestConstants.Usernames[1], new[] { AuthTestConstants.Roles[1] })
        ];

        _identityReadService
            .Setup(s => s.GetUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        var result = await _handler.Handle(new GetAllUsersQuery(), CancellationToken.None);

        result.Should().BeEquivalentTo(users);
    }

    [Test]
    public async Task Handle_CallsServiceOnce()
    {
        _identityReadService
            .Setup(s => s.GetUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserResponse>());

        await _handler.Handle(new GetAllUsersQuery(), CancellationToken.None);

        _identityReadService.Verify(
            s => s.GetUsersAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Handle_PassesCancellationToken_ToService()
    {
        using var cts = new CancellationTokenSource();

        _identityReadService
            .Setup(s => s.GetUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserResponse>());

        await _handler.Handle(new GetAllUsersQuery(), cts.Token);

        _identityReadService.Verify(
            s => s.GetUsersAsync(cts.Token),
            Times.Once);
    }
}