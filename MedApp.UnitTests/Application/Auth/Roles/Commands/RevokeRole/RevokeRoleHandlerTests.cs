using AutoFixture;
using FluentAssertions;
using MedApp.Application.Authentication.Roles.Commands.RevokeRole;
using MedApp.Application.Common.Identity;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace MedApp.UnitTests.Application.Auth.Roles.Commands.RevokeRole;

[TestFixture]
public sealed class RevokeRoleHandlerTests
{
    private IFixture _fixture = null!;
    private Mock<IIdentityRoleService> _service = null!;
    private RevokeRoleHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        // Not the last admin, unless a test says so.
        _fixture.Inject(new Mock<ILastAdminGuard>().Object);
        _fixture.Inject<IIdentityTransactions>(new PassThroughIdentityTransactions());

        _service = new Mock<IIdentityRoleService>();
        _fixture.Inject<IIdentityRoleService>(_service.Object);

        _handler = _fixture.Create<RevokeRoleHandler>();
    }

    [Test]
    public async Task Handle_OwnRole_IsRefusedWithoutRevoking()
    {
        var result = await _handler.Handle(
            new RevokeRoleCommand(AuthTestConstants.Usernames[0], "Admin", AuthTestConstants.Usernames[0].ToUpperInvariant()),
            CancellationToken.None);

        result.Should().BeNull();
        _service.Verify(s => s.RevokeRoleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_AdminFromTheLastActiveAdmin_IsRefusedWithoutRevoking()
    {
        var user = new MedApp.Contracts.Authentication.Responses.UserResponse(Guid.NewGuid(), AuthTestConstants.Usernames[0], ["Admin"]);
        var read = new Mock<IIdentityReadService>();
        read.Setup(r => r.GetUserAsync(AuthTestConstants.Usernames[0], It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var guard = new Mock<ILastAdminGuard>();
        guard.Setup(g => g.WouldLeaveNoAdminAsync(user.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _fixture.Inject(read.Object);
        _fixture.Inject(guard.Object);

        var result = await _fixture.Create<RevokeRoleHandler>().Handle(
            new RevokeRoleCommand(AuthTestConstants.Usernames[0], "admin", AuthTestConstants.Usernames[1]), CancellationToken.None);

        result.Should().BeNull();
        _service.Verify(s => s.RevokeRoleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_Admin_ChecksTheGuardAndRevokesInsideOneTransaction()
    {
        var transactions = new PassThroughIdentityTransactions();
        var user = new MedApp.Contracts.Authentication.Responses.UserResponse(Guid.NewGuid(), AuthTestConstants.Usernames[0], ["Admin"]);
        var read = new Mock<IIdentityReadService>();
        read.Setup(r => r.GetUserAsync(AuthTestConstants.Usernames[0], It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var inside = new List<string>();
        var guard = new Mock<ILastAdminGuard>();
        guard.Setup(g => g.WouldLeaveNoAdminAsync(user.UserId, It.IsAny<CancellationToken>()))
            .Callback(() => inside.Add($"guard {transactions.IsRunning}"))
            .ReturnsAsync(false);
        _service.Setup(s => s.RevokeRoleAsync(AuthTestConstants.Usernames[0], "Admin", It.IsAny<CancellationToken>()))
            .Callback(() => inside.Add($"revoke {transactions.IsRunning}"))
            .ReturnsAsync(new IdentityRole<Guid>("Admin"));
        _fixture.Inject<IIdentityTransactions>(transactions);
        _fixture.Inject(read.Object);
        _fixture.Inject(guard.Object);

        var result = await _fixture.Create<RevokeRoleHandler>().Handle(
            new RevokeRoleCommand(AuthTestConstants.Usernames[0], "Admin", AuthTestConstants.Usernames[1]), CancellationToken.None);

        result.Should().NotBeNull();
        inside.Should().Equal("guard True", "revoke True");
        transactions.Runs.Should().Be(1);
    }

    [Test]
    public async Task Handle_RoleRevoked_ReturnsRole()
    {
        var role = new IdentityRole<Guid>
        {
            Id = Guid.NewGuid(),
            Name = AuthTestConstants.Roles[0]
        };

        var command = new RevokeRoleCommand(AuthTestConstants.Usernames[0], role.Name);

        _service
            .Setup(s => s.RevokeRoleAsync(
                command.Username,
                command.Role,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().BeSameAs(role);
    }

    [Test]
    public async Task Handle_RoleNotRevoked_ReturnsNull()
    {
        var command = new RevokeRoleCommand(AuthTestConstants.Usernames[0], AuthTestConstants.Roles[0]);

        _service
            .Setup(s => s.RevokeRoleAsync(
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
        var command = new RevokeRoleCommand(AuthTestConstants.Usernames[0], AuthTestConstants.Roles[0]);

        _service
            .Setup(s => s.RevokeRoleAsync(
                command.Username,
                command.Role,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdentityRole<Guid>?)null);

        await _handler.Handle(command, CancellationToken.None);

        _service.Verify(
            s => s.RevokeRoleAsync(
                AuthTestConstants.Usernames[0],
                AuthTestConstants.Roles[0],
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Handle_PassesCancellationToken()
    {
        var command = new RevokeRoleCommand(AuthTestConstants.Usernames[0], AuthTestConstants.Roles[0]);

        using var cts = new CancellationTokenSource();

        _service
            .Setup(s => s.RevokeRoleAsync(
                command.Username,
                command.Role,
                cts.Token))
            .ReturnsAsync((IdentityRole<Guid>?)null);

        await _handler.Handle(command, cts.Token);

        _service.Verify(
            s => s.RevokeRoleAsync(
                command.Username,
                command.Role,
                cts.Token),
            Times.Once);
    }
}
