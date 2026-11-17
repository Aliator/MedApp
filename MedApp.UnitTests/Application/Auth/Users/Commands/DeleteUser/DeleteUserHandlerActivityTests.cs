using AutoFixture;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Authentication.Users.Commands.DeleteUser;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Authentication.Responses;
using MedApp.Domain.Activity;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace MedApp.UnitTests.Application.Auth.Users.Commands.DeleteUser;

[TestFixture]
public sealed class DeleteUserHandlerActivityTests
{
    private IFixture _fixture = null!;
    private Mock<IIdentityUserService> _users = null!;
    private RecordingActivityLog _activity = null!;
    private PassThroughIdentityTransactions _transactions = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        _users = new Mock<IIdentityUserService>();
        _users.Setup(u => u.VerifyPasswordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Success);
        _activity = new RecordingActivityLog();
        _fixture.Inject(_users.Object);
        _fixture.Inject<IActivityLog>(_activity);
        // The account has no upcoming appointments (a loose mock counts none), so nothing stops the delete.
        _fixture.Inject(new Mock<IAppointmentRepository>().Object);
        _transactions = new PassThroughIdentityTransactions();
        _fixture.Inject<IIdentityTransactions>(_transactions);
    }

    private static DeleteUserCommand Command() => new("old-user", "test.admin", "Synthetic-Password-1");

    [Test]
    public async Task Handle_Deleted_RecordsTheAccount()
    {
        _users.Setup(u => u.DeleteUserAsync("old-user", It.IsAny<CancellationToken>())).ReturnsAsync(IdentityResult.Success);

        await _fixture.Create<DeleteUserHandler>().Handle(Command(), CancellationToken.None);

        var entry = _activity.Recorded.Should().ContainSingle().Subject;
        entry.Action.Should().Be(ActivityAction.AccountDeleted);
        entry.TargetUsername.Should().Be("old-user");
    }

    [Test]
    public async Task Handle_Deleted_RecordsItInTheSameTransaction()
    {
        _users.Setup(u => u.DeleteUserAsync("old-user", It.IsAny<CancellationToken>())).ReturnsAsync(IdentityResult.Success);
        var log = new Mock<IActivityLog>();
        var recordedInside = new List<bool>();
        log.Setup(l => l.Entry(It.IsAny<ActivityAction>(), It.IsAny<Guid?>(), It.IsAny<object?>(), It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<ActivityActor?>()))
            .Returns(new ActivityEntry());
        log.Setup(l => l.RecordAsync(It.IsAny<ActivityEntry>(), It.IsAny<CancellationToken>()))
            .Callback(() => recordedInside.Add(_transactions.IsRunning))
            .Returns(Task.CompletedTask);
        _fixture.Inject(log.Object);

        await _fixture.Create<DeleteUserHandler>().Handle(Command(), CancellationToken.None);

        recordedInside.Should().Equal(true);
    }

    [Test]
    public async Task Handle_MeetingsChangedMeanwhile_RecordsNothing()
    {
        _users.Setup(u => u.DeleteUserAsync("old-user", It.IsAny<CancellationToken>())).ReturnsAsync(IdentityResult.Success);
        var user = new UserResponse(Guid.Parse("0000000c-0000-0000-0000-0000000000aa"), "old-user", ["User"]);
        var read = new Mock<IIdentityReadService>();
        read.Setup(r => r.GetUserAsync("old-user", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var meetings = new Mock<IMeetingRepository>();
        meetings.Setup(r => r.RemoveAttendeeFromUpcomingAsync(user.UserId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException(MeetingLimits.ChangedMeanwhile));
        _fixture.Inject(read.Object);
        _fixture.Inject(meetings.Object);

        var act = () => _fixture.Create<DeleteUserHandler>().Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _activity.Recorded.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_NotDeleted_RecordsNothing()
    {
        _users.Setup(u => u.DeleteUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Failed(IdentityErrors.UserNotFound));

        await _fixture.Create<DeleteUserHandler>().Handle(Command(), CancellationToken.None);

        _activity.Recorded.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_WrongPassword_RecordsNothing()
    {
        _users.Setup(u => u.VerifyPasswordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Failed(IdentityErrors.PasswordIncorrect));

        await _fixture.Create<DeleteUserHandler>().Handle(Command(), CancellationToken.None);

        _activity.Recorded.Should().BeEmpty();
    }
}
