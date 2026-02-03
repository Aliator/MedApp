using AutoFixture;
using FluentAssertions;
using MedApp.Application.Auth.Queries.GetUserRoles;
using MedApp.Application.Common.Identity;
using MedApp.UnitTests.Common.Fixtures;
using Moq;

namespace MedApp.UnitTests.Application.Auth.Queries.GetUserRoles;

[TestFixture]
public sealed class GetUserRolesHandlerTests
{
    private IFixture _fixture = null!;
    private Mock<IIdentityReadService> _identityReadService = null!;
    private GetUserRolesHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();

        _identityReadService = new Mock<IIdentityReadService>();
        _fixture.Inject(_identityReadService.Object);

        _handler = _fixture.Create<GetUserRolesHandler>();
    }

    [Test]
    public async Task Handle_ReturnsRolesForUser()
    {
        var username = "username";
        var roles = new[] { "Role", "Role2" };

        _identityReadService
            .Setup(s => s.GetRolesForUserAsync(
                username,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(roles);

        var query = new GetUserRolesQuery(username);

        var result = await _handler.Handle(
            query,
            CancellationToken.None);

        result.Should().BeEquivalentTo(roles);
    }

    [Test]
    public async Task Handle_PassesUsername_AndCancellationToken()
    {
        var username = "username";
        using var cts = new CancellationTokenSource();

        _identityReadService
            .Setup(s => s.GetRolesForUserAsync(
                username,
                cts.Token))
            .ReturnsAsync(Array.Empty<string>());

        var query = new GetUserRolesQuery(username);

        await _handler.Handle(
            query,
            cts.Token);

        _identityReadService.Verify(
            s => s.GetRolesForUserAsync(
                username,
                cts.Token),
            Times.Once);
    }
}