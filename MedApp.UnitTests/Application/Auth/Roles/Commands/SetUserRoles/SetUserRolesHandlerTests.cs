using AutoFixture;
using FluentAssertions;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Authentication.Roles.Commands.SetUserRoles;
using MedApp.Application.Common.Identity;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace MedApp.UnitTests.Application.Auth.Roles.Commands.SetUserRoles;

[TestFixture]
public sealed class SetUserRolesHandlerTests
{
    private IFixture _fixture = null!;
    private Mock<IIdentityRoleService> _service = null!;
    private Mock<IIdentityReadService> _read = null!;
    private Mock<IAppointmentRepository> _appointments = null!;
    private SetUserRolesHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();

        _service = new Mock<IIdentityRoleService>();
        _read = new Mock<IIdentityReadService>();
        _appointments = new Mock<IAppointmentRepository>();
        _read.Setup(r => r.GetUserAsync(AuthTestConstants.Usernames[1], It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthTestConstants.SessionUser("User"));
        _service.Setup(s => s.SetRolesAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Success);
        _fixture.Inject(_service.Object);
        _fixture.Inject(_read.Object);
        _fixture.Inject(_appointments.Object);
        _fixture.Inject<TimeProvider>(new FixedTimeProvider(CalendarTestConstants.Now));

        _handler = _fixture.Create<SetUserRolesHandler>();
    }

    [Test]
    public async Task Handle_AnotherUser_SetsTheirRolesAndReturnsTheResult()
    {
        var command = new SetUserRolesCommand(AuthTestConstants.Usernames[1], AuthTestConstants.Roles, AuthTestConstants.Usernames[0]);
        using var cts = new CancellationTokenSource();

        _service
            .Setup(s => s.SetRolesAsync(command.Username, command.Roles, cts.Token))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _handler.Handle(command, cts.Token);

        result.Succeeded.Should().BeTrue();
        _service.Verify(s => s.SetRolesAsync(command.Username, command.Roles, cts.Token), Times.Once);
    }

    [Test]
    public async Task Handle_OwnRoles_FailsWithoutChangingThem()
    {
        var command = new SetUserRolesCommand(
            AuthTestConstants.Usernames[0].ToUpperInvariant(),
            [AuthTestConstants.Roles[1]],
            AuthTestConstants.Usernames[0]);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == IdentityErrors.OwnRolesCode);
        _service.Verify(
            s => s.SetRolesAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestCase(1, "1 upcoming appointment")]
    [TestCase(2, "2 upcoming appointments")]
    public async Task Handle_NoStaffRoleLeftWithUpcomingAppointments_FailsWithoutChangingThem(int count, string counted)
    {
        using var cts = new CancellationTokenSource();
        _appointments.Setup(a => a.CountUpcomingForStaffAsync(AuthTestConstants.SessionUserId, CalendarTestConstants.Now, cts.Token))
            .ReturnsAsync(count);
        var command = new SetUserRolesCommand(AuthTestConstants.Usernames[1], [], AuthTestConstants.Usernames[0]);

        var result = await _handler.Handle(command, cts.Token);

        result.Succeeded.Should().BeFalse();
        var error = result.Errors.Should().ContainSingle().Subject;
        error.Code.Should().Be(IdentityErrors.HasUpcomingAppointmentsCode);
        error.Description.Should().Be($"{AuthTestConstants.Usernames[1]} has {counted}. Move or cancel them first.");
        _read.Verify(r => r.GetUserAsync(AuthTestConstants.Usernames[1], cts.Token), Times.Once);
        _service.Verify(
            s => s.SetRolesAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task Handle_NoStaffRoleLeftWithNoUpcomingAppointments_SetsThem()
    {
        var command = new SetUserRolesCommand(AuthTestConstants.Usernames[1], [], AuthTestConstants.Usernames[0]);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        _appointments.Verify(
            a => a.CountUpcomingForStaffAsync(AuthTestConstants.SessionUserId, CalendarTestConstants.Now, It.IsAny<CancellationToken>()),
            Times.Once);
        _service.Verify(s => s.SetRolesAsync(command.Username, command.Roles, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestCase("User")]
    [TestCase("admin")]
    public async Task Handle_AStaffRoleLeft_DoesNotLookAtAppointments(string role)
    {
        _appointments.Setup(a => a.CountUpcomingForStaffAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var result = await _handler.Handle(
            new SetUserRolesCommand(AuthTestConstants.Usernames[1], [role], AuthTestConstants.Usernames[0]),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        _appointments.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Handle_NoStaffRoleForAnUnknownUser_LeavesTheAnswerToTheRoleService()
    {
        _service.Setup(s => s.SetRolesAsync("nobody", It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Failed(IdentityErrors.UserNotFound));

        var result = await _handler.Handle(new SetUserRolesCommand("nobody", [], AuthTestConstants.Usernames[0]), CancellationToken.None);

        result.Errors.Should().ContainSingle().Which.Code.Should().Be(IdentityErrors.UserNotFoundCode);
        _appointments.VerifyNoOtherCalls();
    }
}
