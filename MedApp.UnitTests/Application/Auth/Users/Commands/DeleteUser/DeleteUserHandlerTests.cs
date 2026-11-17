using AutoFixture;
using FluentAssertions;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Authentication.Users.Commands.DeleteUser;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Authentication.Responses;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace MedApp.UnitTests.Application.Auth.Users.Commands.DeleteUser;

[TestFixture]
public sealed class DeleteUserHandlerTests
{
    private IFixture _fixture = null!;
    private Mock<IIdentityUserService> _service = null!;
    private Mock<IIdentityReadService> _reader = null!;
    private Mock<IAppointmentRepository> _appointments = null!;
    private Mock<IMeetingRepository> _meetings = null!;
    private PassThroughIdentityTransactions _transactions = null!;
    private DeleteUserHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();

        _service = new Mock<IIdentityUserService>();
        _fixture.Inject<IIdentityUserService>(_service.Object);

        // The user has no upcoming appointments unless a test says otherwise.
        _reader = new Mock<IIdentityReadService>();
        _appointments = new Mock<IAppointmentRepository>();
        _meetings = new Mock<IMeetingRepository>();
        _fixture.Inject(_reader.Object);
        _fixture.Inject(_appointments.Object);
        _fixture.Inject(_meetings.Object);
        _fixture.Inject<TimeProvider>(new FixedTimeProvider(AuthTestConstants.SessionStartedAtUtc));
        _transactions = new PassThroughIdentityTransactions();
        _fixture.Inject<IIdentityTransactions>(_transactions);

        _handler = _fixture.Create<DeleteUserHandler>();

        // The admin's password is right unless a test says otherwise.
        _service
            .Setup(s => s.VerifyPasswordAsync(AuthTestConstants.Usernames[1], AuthTestConstants.ValidPassword, It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Success);
    }

    /// <summary>Usernames[1] (an admin) deletes Usernames[0], confirming with their password.</summary>
    private static DeleteUserCommand Command() =>
        new(AuthTestConstants.Usernames[0], AuthTestConstants.Usernames[1], AuthTestConstants.ValidPassword);

    [Test]
    public async Task Handle_WrongPassword_ReturnsTheFailureWithoutDeleting()
    {
        var wrong = IdentityResult.Failed(IdentityErrors.PasswordIncorrect);
        _service
            .Setup(s => s.VerifyPasswordAsync(AuthTestConstants.Usernames[1], AuthTestConstants.ValidPassword, It.IsAny<CancellationToken>()))
            .ReturnsAsync(wrong);

        var result = await _handler.Handle(Command(), CancellationToken.None);

        result.Should().BeSameAs(wrong);
        _service.Verify(s => s.DeleteUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_UserDeleted_ReturnsSuccess()
    {
        var command = Command();

        var identityResult = IdentityResult.Success;

        _service
            .Setup(s => s.DeleteUserAsync(
                command.Username,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(identityResult);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().BeSameAs(identityResult);
    }

    [Test]
    public async Task Handle_UserNotDeleted_ReturnsFailure()
    {
        var command = Command();

        var identityResult = IdentityResult.Failed();

        _service
            .Setup(s => s.DeleteUserAsync(
                command.Username,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(identityResult);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
    }

    [Test]
    public async Task Handle_CallsService_WithCorrectArguments()
    {
        var command = Command();

        _service
            .Setup(s => s.DeleteUserAsync(
                command.Username,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Success);

        await _handler.Handle(command, CancellationToken.None);

        _service.Verify(
            s => s.DeleteUserAsync(
                command.Username,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Handle_PassesCancellationToken()
    {
        var command = Command();

        using var cts = new CancellationTokenSource();

        _service
            .Setup(s => s.DeleteUserAsync(
                command.Username,
                cts.Token))
            .ReturnsAsync(IdentityResult.Success);

        await _handler.Handle(command, cts.Token);

        _service.Verify(
            s => s.DeleteUserAsync(
                command.Username,
                cts.Token),
            Times.Once);
    }

    /// <summary>The user being deleted exists, with this many upcoming appointments.</summary>
    private void GivenUpcomingAppointments(int count)
    {
        _reader
            .Setup(r => r.GetUserAsync(AuthTestConstants.Usernames[0], It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserResponse(AuthTestConstants.SettingsUserId, AuthTestConstants.Usernames[0], [AuthTestConstants.Roles[1]]));
        _appointments
            .Setup(r => r.CountUpcomingForStaffAsync(AuthTestConstants.SettingsUserId, AuthTestConstants.SessionStartedAtUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(count);
    }

    [TestCase(1, "username has 1 upcoming appointment. Move or cancel them first.")]
    [TestCase(2, "username has 2 upcoming appointments. Move or cancel them first.")]
    public async Task Handle_UserHasUpcomingAppointments_ReturnsTheFailureWithoutDeleting(int count, string description)
    {
        GivenUpcomingAppointments(count);

        var result = await _handler.Handle(Command(), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        var error = result.Errors.Should().ContainSingle().Subject;
        error.Code.Should().Be(IdentityErrors.HasUpcomingAppointmentsCode);
        error.Description.Should().Be(description);
        _service.Verify(s => s.DeleteUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_UserHasNoUpcomingAppointments_DeletesThem()
    {
        GivenUpcomingAppointments(0);
        _service
            .Setup(s => s.DeleteUserAsync(AuthTestConstants.Usernames[0], It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _handler.Handle(Command(), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        _service.Verify(s => s.DeleteUserAsync(AuthTestConstants.Usernames[0], It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_WrongPassword_DoesNotLookForAppointments()
    {
        _service
            .Setup(s => s.VerifyPasswordAsync(AuthTestConstants.Usernames[1], AuthTestConstants.ValidPassword, It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Failed(IdentityErrors.PasswordIncorrect));

        await _handler.Handle(Command(), CancellationToken.None);

        _appointments.Verify(
            r => r.CountUpcomingForStaffAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task Handle_UserWithAppointments_PassesTheCancellationTokenToTheLookups()
    {
        GivenUpcomingAppointments(1);
        using var cts = new CancellationTokenSource();

        await _handler.Handle(Command(), cts.Token);

        _reader.Verify(r => r.GetUserAsync(AuthTestConstants.Usernames[0], cts.Token), Times.Once);
        _appointments.Verify(
            r => r.CountUpcomingForStaffAsync(AuthTestConstants.SettingsUserId, AuthTestConstants.SessionStartedAtUtc, cts.Token),
            Times.Once);
    }

    [Test]
    public async Task Handle_Deleted_TakesThemOffTheirMeetingsStillToCome()
    {
        GivenUpcomingAppointments(0);
        using var cts = new CancellationTokenSource();
        _service.Setup(s => s.DeleteUserAsync(AuthTestConstants.Usernames[0], cts.Token)).ReturnsAsync(IdentityResult.Success);

        await _handler.Handle(Command(), cts.Token);

        _meetings.Verify(
            r => r.RemoveAttendeeFromUpcomingAsync(AuthTestConstants.SettingsUserId, AuthTestConstants.SessionStartedAtUtc, cts.Token),
            Times.Once);
        _meetings.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Handle_Deleted_DeletesAndTakesThemOffMeetingsInOneTransaction()
    {
        GivenUpcomingAppointments(0);
        using var cts = new CancellationTokenSource();
        var inside = new List<string>();
        _service.Setup(s => s.DeleteUserAsync(AuthTestConstants.Usernames[0], cts.Token))
            .Callback(() => inside.Add($"delete {_transactions.IsRunning}"))
            .ReturnsAsync(IdentityResult.Success);
        _meetings.Setup(r => r.RemoveAttendeeFromUpcomingAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback(() => inside.Add($"meetings {_transactions.IsRunning}"))
            .ReturnsAsync(1);

        await _handler.Handle(Command(), cts.Token);

        inside.Should().Equal("delete True", "meetings True");
        _transactions.Runs.Should().Be(1);
        _transactions.LastToken.Should().Be(cts.Token);
    }

    [Test]
    public async Task Handle_MeetingsChangedMeanwhile_ThrowsFromInsideTheTransaction()
    {
        // Another admin saved one of the meetings at that moment: the 409 leaves the transaction, which rolls the
        // deletion back.
        GivenUpcomingAppointments(0);
        _service.Setup(s => s.DeleteUserAsync(AuthTestConstants.Usernames[0], It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Success);
        var conflict = new ConflictException(MeetingLimits.ChangedMeanwhile);
        _meetings.Setup(r => r.RemoveAttendeeFromUpcomingAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback(() => _transactions.IsRunning.Should().BeTrue())
            .ThrowsAsync(conflict);

        var act = () => _handler.Handle(Command(), CancellationToken.None);

        (await act.Should().ThrowAsync<ConflictException>()).Which.Should().BeSameAs(conflict);
        _transactions.Runs.Should().Be(1);
    }

    [Test]
    public async Task Handle_WrongPassword_StartsNoTransaction()
    {
        _service
            .Setup(s => s.VerifyPasswordAsync(AuthTestConstants.Usernames[1], AuthTestConstants.ValidPassword, It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Failed(IdentityErrors.PasswordIncorrect));

        await _handler.Handle(Command(), CancellationToken.None);

        _transactions.Runs.Should().Be(0);
    }

    [Test]
    public async Task Handle_NotDeleted_LeavesTheirMeetings()
    {
        GivenUpcomingAppointments(0);
        _service.Setup(s => s.DeleteUserAsync(AuthTestConstants.Usernames[0], It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Failed());

        await _handler.Handle(Command(), CancellationToken.None);

        _meetings.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Handle_UpcomingAppointmentsStopIt_LeavesTheirMeetings()
    {
        GivenUpcomingAppointments(1);

        await _handler.Handle(Command(), CancellationToken.None);

        _meetings.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Handle_WrongPassword_LeavesTheirMeetings()
    {
        GivenUpcomingAppointments(0);
        _service
            .Setup(s => s.VerifyPasswordAsync(AuthTestConstants.Usernames[1], AuthTestConstants.ValidPassword, It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Failed(IdentityErrors.PasswordIncorrect));

        await _handler.Handle(Command(), CancellationToken.None);

        _meetings.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Handle_DeletedButTheAccountWasntFound_HasNobodyToTakeOffMeetings()
    {
        // The identity service found no account to check (GetUserAsync returns null) but reports the delete as done.
        _service.Setup(s => s.DeleteUserAsync(AuthTestConstants.Usernames[0], It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _handler.Handle(Command(), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        _meetings.VerifyNoOtherCalls();
    }
}
