using AutoFixture;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Authentication.Users.Commands.UpdateUserPassword;
using MedApp.Application.Common.Identity;
using MedApp.Domain.Activity;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace MedApp.UnitTests.Application.Auth.Users.Commands.UpdateUserPassword;

[TestFixture]
public sealed class UpdateUserPasswordHandlerActivityTests
{
    private IFixture _fixture = null!;
    private Mock<IIdentityUserService> _users = null!;
    private RecordingActivityLog _activity = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        _users = new Mock<IIdentityUserService>();
        _activity = new RecordingActivityLog();
        _fixture.Inject(_users.Object);
        _fixture.Inject<IActivityLog>(_activity);
    }

    private static UpdateUserPasswordCommand Command() => new("staff-user", "Old-Password-1", "New-Password-1");

    [Test]
    public async Task Handle_Changed_RecordsIt()
    {
        _users.Setup(u => u.UpdateUserPasswordAsync("staff-user", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Success);

        await _fixture.Create<UpdateUserPasswordHandler>().Handle(Command(), CancellationToken.None);

        var entry = _activity.Recorded.Should().ContainSingle().Subject;
        entry.Action.Should().Be(ActivityAction.PasswordChanged);
        entry.TargetUsername.Should().Be("staff-user");
    }

    [Test]
    public async Task Handle_Failed_RecordsNothing()
    {
        _users.Setup(u => u.UpdateUserPasswordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Failed(IdentityErrors.OldPasswordIncorrect));

        await _fixture.Create<UpdateUserPasswordHandler>().Handle(Command(), CancellationToken.None);

        _activity.Recorded.Should().BeEmpty();
    }
}
