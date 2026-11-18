using AutoFixture;
using FluentAssertions;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Authentication.Roles.Commands.SetUserRoles;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
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
    private Mock<IMeetingRepository> _meetings = null!;
    private PassThroughIdentityTransactions _transactions = null!;
    private SetUserRolesHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        // Not the last admin, unless a test says so.
        _fixture.Inject(new Mock<ILastAdminGuard>().Object);

        _service = new Mock<IIdentityRoleService>();
        _read = new Mock<IIdentityReadService>();
        _appointments = new Mock<IAppointmentRepository>();
        _meetings = new Mock<IMeetingRepository>();
        _fixture.Inject(_meetings.Object);
        _read.Setup(r => r.GetUserAsync(AuthTestConstants.Usernames[1], It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthTestConstants.SessionUser("User"));
        _service.Setup(s => s.SetRolesAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Success);
        _fixture.Inject(_service.Object);
        _fixture.Inject(_read.Object);
        _fixture.Inject(_appointments.Object);
        _fixture.Inject<TimeProvider>(new FixedTimeProvider(CalendarTestConstants.Now));
        _transactions = new PassThroughIdentityTransactions();
        _fixture.Inject<IIdentityTransactions>(_transactions);

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
    public async Task Handle_EveryStaffRoleTaken_ChangesTheRolesAndTheirMeetingsInOneTransaction()
    {
        var inside = new List<string>();
        _service.Setup(s => s.SetRolesAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .Callback(() => inside.Add($"roles {_transactions.IsRunning}"))
            .ReturnsAsync(IdentityResult.Success);
        _meetings.Setup(r => r.RemoveAttendeeFromUpcomingAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback(() => inside.Add($"meetings {_transactions.IsRunning}"))
            .ReturnsAsync(1);

        await _handler.Handle(new SetUserRolesCommand(AuthTestConstants.Usernames[1], [], AuthTestConstants.Usernames[0]), CancellationToken.None);

        inside.Should().Equal("roles True", "meetings True");
        _transactions.Runs.Should().Be(1);
    }

    [Test]
    public async Task Handle_MeetingsChangedMeanwhile_ThrowsFromInsideTheTransaction()
    {
        // Another admin saved one of the meetings at that moment: the 409 leaves the transaction, which rolls the role
        // change back.
        _meetings.Setup(r => r.RemoveAttendeeFromUpcomingAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback(() => _transactions.IsRunning.Should().BeTrue())
            .ThrowsAsync(new ConflictException(MeetingLimits.ChangedMeanwhile));

        var act = () => _handler.Handle(new SetUserRolesCommand(AuthTestConstants.Usernames[1], [], AuthTestConstants.Usernames[0]), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(MeetingLimits.ChangedMeanwhile);
    }

    [Test]
    public async Task Handle_TakingAdminFromTheLastActiveAdmin_FailsWithLastAdmin_InsideTheTransaction()
    {
        var target = AuthTestConstants.SessionUser("Admin");
        _read.Setup(r => r.GetUserAsync(AuthTestConstants.Usernames[1], It.IsAny<CancellationToken>())).ReturnsAsync(target);
        var guard = new Mock<ILastAdminGuard>();
        guard.Setup(g => g.WouldLeaveNoAdminAsync(target.UserId, It.IsAny<CancellationToken>()))
            .Callback(() => _transactions.IsRunning.Should().BeTrue())
            .ReturnsAsync(true);
        _fixture.Inject(guard.Object);

        var result = await _fixture.Create<SetUserRolesHandler>().Handle(
            new SetUserRolesCommand(AuthTestConstants.Usernames[1], ["Clinician"], AuthTestConstants.Usernames[0]), CancellationToken.None);

        result.Errors.Should().ContainSingle().Which.Code.Should().Be(IdentityErrors.LastAdminCode);
        _service.Verify(s => s.SetRolesAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_KeepingAdmin_DoesNotAskTheGuard()
    {
        var guard = new Mock<ILastAdminGuard>();
        _fixture.Inject(guard.Object);

        await _fixture.Create<SetUserRolesHandler>().Handle(
            new SetUserRolesCommand(AuthTestConstants.Usernames[1], ["admin", "Clinician"], AuthTestConstants.Usernames[0]), CancellationToken.None);

        guard.Verify(g => g.WouldLeaveNoAdminAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
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
        _meetings.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Handle_NoStaffRoleLeft_TakesThemOffTheirMeetingsStillToCome()
    {
        using var cts = new CancellationTokenSource();

        var result = await _handler.Handle(new SetUserRolesCommand(AuthTestConstants.Usernames[1], [], AuthTestConstants.Usernames[0]), cts.Token);

        result.Succeeded.Should().BeTrue();
        _meetings.Verify(
            m => m.RemoveAttendeeFromUpcomingAsync(AuthTestConstants.SessionUserId, CalendarTestConstants.Now, cts.Token),
            Times.Once);
        _meetings.VerifyNoOtherCalls();
    }

    [TestCase("User")]
    [TestCase("Admin")]
    public async Task Handle_AStaffRoleLeft_LeavesTheirMeetings(string role)
    {
        await _handler.Handle(new SetUserRolesCommand(AuthTestConstants.Usernames[1], [role], AuthTestConstants.Usernames[0]), CancellationToken.None);

        _meetings.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Handle_NoStaffRoleLeftButTheChangeFails_LeavesTheirMeetings()
    {
        _service.Setup(s => s.SetRolesAsync(AuthTestConstants.Usernames[1], It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Failed());

        await _handler.Handle(new SetUserRolesCommand(AuthTestConstants.Usernames[1], [], AuthTestConstants.Usernames[0]), CancellationToken.None);

        _meetings.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Handle_NoStaffRoleLeftWithUpcomingAppointments_LeavesTheirMeetings()
    {
        _appointments.Setup(a => a.CountUpcomingForStaffAsync(AuthTestConstants.SessionUserId, CalendarTestConstants.Now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        await _handler.Handle(new SetUserRolesCommand(AuthTestConstants.Usernames[1], [], AuthTestConstants.Usernames[0]), CancellationToken.None);

        _meetings.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Handle_OwnRoles_LeavesTheirMeetings()
    {
        await _handler.Handle(new SetUserRolesCommand(AuthTestConstants.Usernames[0], [], AuthTestConstants.Usernames[0]), CancellationToken.None);

        _meetings.VerifyNoOtherCalls();
    }
}
