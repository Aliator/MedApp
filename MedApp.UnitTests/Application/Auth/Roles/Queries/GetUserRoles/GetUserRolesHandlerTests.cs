using AutoFixture;
using FluentAssertions;
using MedApp.Application.Authentication.Roles.Queries.GetUserRoles;
using MedApp.Application.Common.Identity;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;

namespace MedApp.UnitTests.Application.Auth.Roles.Queries.GetUserRoles;

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
        _identityReadService
            .Setup(s => s.GetRolesForUserAsync(
                AuthTestConstants.Usernames[0],
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthTestConstants.Roles);

        var query = new GetUserRolesQuery(AuthTestConstants.Usernames[0]);

        var result = await _handler.Handle(
            query,
            CancellationToken.None);

        result.Should().BeEquivalentTo(AuthTestConstants.Roles);
    }

    [Test]
    public async Task Handle_PassesUsername_AndCancellationToken()
    {
        using var cts = new CancellationTokenSource();

        _identityReadService
            .Setup(s => s.GetRolesForUserAsync(
                AuthTestConstants.Usernames[0],
                cts.Token))
            .ReturnsAsync(Array.Empty<string>());

        var query = new GetUserRolesQuery(AuthTestConstants.Usernames[0]);

        await _handler.Handle(
            query,
            cts.Token);

        _identityReadService.Verify(
            s => s.GetRolesForUserAsync(
                AuthTestConstants.Usernames[0],
                cts.Token),
            Times.Once);
    }
}