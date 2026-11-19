using AutoFixture;
using FluentAssertions;
using MedApp.Application.Authentication.Roles.Commands.AssignRole;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Teams.Common;
using MedApp.Application.Teams.Repositories;
using MedApp.Domain.Teams;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace MedApp.UnitTests.Application.Auth.Roles.Commands.AssignRole;

[TestFixture]
public sealed class AssignRoleHandlerTests
{
    private IFixture _fixture = null!;
    private Mock<IIdentityRoleService> _service = null!;
    private AssignRoleHandler _handler = null!;
    private Mock<ITeamRepository> _teams = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();

        _service = new Mock<IIdentityRoleService>();
        _fixture.Inject<IIdentityRoleService>(_service.Object);
        // In no team, unless a test says so.
        _teams = new Mock<ITeamRepository>();
        _fixture.Inject(_teams.Object);

        _handler = _fixture.Create<AssignRoleHandler>();
    }

    [Test]
    public async Task Handle_AdminForSomeoneInATeam_ThrowsConflictWithoutAssigning()
    {
        var user = new MedApp.Contracts.Authentication.Responses.UserResponse(Guid.NewGuid(), AuthTestConstants.Usernames[0], []);
        var reader = new Mock<IIdentityReadService>();
        reader.Setup(r => r.GetUserAsync(user.Username, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _fixture.Inject(reader.Object);
        _teams.Setup(t => t.GetForUserAsync(user.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Team { Id = Guid.NewGuid(), Name = "Synthetic clinic" }]);

        var act = () => _fixture.Create<AssignRoleHandler>().Handle(new AssignRoleCommand(user.Username, AppRoles.Admin), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(TeamRules.TeamMemberIsNoAdmin(user.Username));
        _service.Verify(s => s.AssignRoleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_RoleAssigned_ReturnsRole()
    {
        var role = new IdentityRole<Guid>
        {
            Id = Guid.NewGuid(),
            Name = AuthTestConstants.Roles[0]
        };

        var command = new AssignRoleCommand(AuthTestConstants.Usernames[0], role.Name);

        _service
            .Setup(s => s.AssignRoleAsync(
                command.Username,
                command.Role,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().BeSameAs(role);
    }


    [Test]
    public async Task Handle_RoleNotAssigned_ReturnsNull()
    {
        var command = new AssignRoleCommand(AuthTestConstants.Usernames[0], AuthTestConstants.Roles[0]);

        _service
            .Setup(s => s.AssignRoleAsync(
                command.Username,
                command.Role,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdentityRole<Guid>?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().BeNull();
    }

    [Test]
    public async Task Handle_CallsService_WithCorrectArguments()
    {
        var command = new AssignRoleCommand(AuthTestConstants.Usernames[0], AuthTestConstants.Roles[0]);

        _service
            .Setup(s => s.AssignRoleAsync(
                command.Username,
                command.Role,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdentityRole<Guid>?)null);

        await _handler.Handle(command, CancellationToken.None);

        _service.Verify(
            s => s.AssignRoleAsync(
                AuthTestConstants.Usernames[0],
                AuthTestConstants.Roles[0],
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Handle_PassesCancellationToken()
    {
        var command = new AssignRoleCommand(AuthTestConstants.Usernames[0], AuthTestConstants.Roles[0]);

        using var cts = new CancellationTokenSource();

        _service
            .Setup(s => s.AssignRoleAsync(
                command.Username,
                command.Role,
                cts.Token))
            .ReturnsAsync((IdentityRole<Guid>?)null);

        await _handler.Handle(command, cts.Token);

        _service.Verify(
            s => s.AssignRoleAsync(
                command.Username,
                command.Role,
                cts.Token),
            Times.Once);
    }
}
