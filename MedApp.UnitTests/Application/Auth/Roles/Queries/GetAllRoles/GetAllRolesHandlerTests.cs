using AutoFixture;
using FluentAssertions;
using MedApp.Application.Auth.Roles.Queries.GetAllRoles;
using MedApp.Application.Common.Identity;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;

namespace MedApp.UnitTests.Application.Auth.Roles.Queries.GetAllRoles;

[TestFixture]
public sealed class GetAllRolesHandlerTests
{
    private IFixture _fixture = null!;
    private Mock<IIdentityReadService> _identityReadService = null!;
    private GetAllRolesHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();

        _identityReadService = new Mock<IIdentityReadService>();
        _fixture.Inject(_identityReadService.Object);

        _handler = _fixture.Create<GetAllRolesHandler>();
    }

    [Test]
    public async Task Handle_ReturnsAllRoleNames()
    {
        _identityReadService
            .Setup(s => s.GetRoleNamesAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthTestConstants.Roles);

        var result = await _handler.Handle(
            new GetAllRolesQuery(),
            CancellationToken.None);

        result.Should().BeEquivalentTo(AuthTestConstants.Roles);
    }

    [Test]
    public async Task Handle_PassesCancellationToken()
    {
        using var cts = new CancellationTokenSource();

        _identityReadService
            .Setup(s => s.GetRoleNamesAsync(
                cts.Token))
            .ReturnsAsync(Array.Empty<string>());

        await _handler.Handle(
            new GetAllRolesQuery(),
            cts.Token);

        _identityReadService.Verify(
            s => s.GetRoleNamesAsync(cts.Token),
            Times.Once);
    }
}