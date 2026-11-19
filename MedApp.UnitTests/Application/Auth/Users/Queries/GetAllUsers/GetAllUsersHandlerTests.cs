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
    private Mock<MedApp.Application.Teams.Repositories.ITeamRepository> _teams = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();

        _identityReadService = new Mock<IIdentityReadService>();
        _fixture.Inject(_identityReadService.Object);
        // Nobody is a clinic admin, unless a test says so.
        _teams = new Mock<MedApp.Application.Teams.Repositories.ITeamRepository>();
        _teams.Setup(t => t.GetClinicAdminsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid>());
        _fixture.Inject(_teams.Object);

        _handler = _fixture.Create<GetAllUsersHandler>();
    }

    [Test]
    public async Task Handle_ReturnsAllUsersWithTheirAccountTypes()
    {
        UserResponse[] users =
        [
            new(Guid.NewGuid(), AuthTestConstants.Usernames[0], new[] { AuthTestConstants.Roles[0] }),
            new(Guid.NewGuid(), AuthTestConstants.Usernames[1], new[] { AuthTestConstants.Roles[1] }),
            new(Guid.NewGuid(), "clinic.admin", new[] { "Clinician" })
        ];
        _teams.Setup(t => t.GetClinicAdminsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid> { users[2].UserId });

        _identityReadService
            .Setup(s => s.GetUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        var result = await _handler.Handle(new GetAllUsersQuery(), CancellationToken.None);

        result.Should().BeEquivalentTo(
        [
            users[0] with { AccountType = AuthTestConstants.Roles[0] == "Admin" ? "Admin" : "Staff" },
            users[1] with { AccountType = AuthTestConstants.Roles[1] == "Admin" ? "Admin" : "Staff" },
            users[2] with { AccountType = "ClinicAdmin" },
        ]);
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