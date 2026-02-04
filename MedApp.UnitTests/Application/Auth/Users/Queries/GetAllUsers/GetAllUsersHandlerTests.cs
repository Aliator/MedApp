using AutoFixture;
using FluentAssertions;
using MedApp.Application.Auth.Users.Queries.GetAllUsers;
using MedApp.Application.Common.Identity;
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
    public async Task Handle_ReturnsAllUsernames()
    {
        _identityReadService
            .Setup(s => s.GetUsernamesAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthTestConstants.Usernames);

        var result = await _handler.Handle(
            new GetAllUsersQuery(),
            CancellationToken.None);

        result.Should().BeEquivalentTo(AuthTestConstants.Usernames);
    }

    [Test]
    public async Task Handle_PassesCancellationToken()
    {
        using var cts = new CancellationTokenSource();

        _identityReadService
            .Setup(s => s.GetUsernamesAsync(
                cts.Token))
            .ReturnsAsync(Array.Empty<string>());

        await _handler.Handle(
            new GetAllUsersQuery(),
            cts.Token);

        _identityReadService.Verify(
            s => s.GetUsernamesAsync(cts.Token),
            Times.Once);
    }
}