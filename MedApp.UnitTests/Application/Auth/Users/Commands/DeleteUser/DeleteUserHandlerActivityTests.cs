using AutoFixture;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Authentication.Users.Commands.DeleteUser;
using MedApp.Application.Common.Identity;
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
