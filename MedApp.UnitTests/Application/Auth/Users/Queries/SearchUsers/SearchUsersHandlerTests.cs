using AutoFixture;
using FluentAssertions;
using MedApp.Application.Authentication.Users.Queries.SearchUsers;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Paging;
using MedApp.Contracts.Authentication.Responses;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;

namespace MedApp.UnitTests.Application.Auth.Users.Queries.SearchUsers;

[TestFixture]
public sealed class SearchUsersHandlerTests
{
    private IFixture _fixture = null!;
    private Mock<IIdentityReadService> _identityReadService = null!;
    private SearchUsersHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();

        _identityReadService = new Mock<IIdentityReadService>();
        _fixture.Inject(_identityReadService.Object);

        _handler = _fixture.Create<SearchUsersHandler>();
    }

    [Test]
    public async Task Handle_PageOfUsers_ReturnsThemWithTotals()
    {
        var users = AuthTestConstants.Usernames
            .Select(u => new UserResponse(Guid.NewGuid(), u, AuthTestConstants.Roles))
            .ToList();
        var query = AuthTestConstants.CreateSearchUsersQuery();
        _identityReadService
            .Setup(s => s.SearchUsersAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<UserResponse>(users, 23));

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Items.Should().BeEquivalentTo(users);
        result.Page.Should().Be(1);
        result.TotalCount.Should().Be(23);
        result.TotalPages.Should().Be(3);
    }

    [Test]
    public async Task Handle_PassesQueryAndCancellationTokenToService()
    {
        var query = AuthTestConstants.CreateSearchUsersQuery();
        using var cts = new CancellationTokenSource();
        _identityReadService
            .Setup(s => s.SearchUsersAsync(It.IsAny<SearchUsersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<UserResponse>([], 0));

        await _handler.Handle(query, cts.Token);

        _identityReadService.Verify(s => s.SearchUsersAsync(query, cts.Token), Times.Once);
    }
}
