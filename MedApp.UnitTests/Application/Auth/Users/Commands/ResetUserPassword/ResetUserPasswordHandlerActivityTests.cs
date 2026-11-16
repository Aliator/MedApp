using AutoFixture;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Authentication.Users.Commands.ResetUserPassword;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Authentication.Responses;
using MedApp.Domain.Activity;
using MedApp.UnitTests.Common.Fixtures;
using Moq;

namespace MedApp.UnitTests.Application.Auth.Users.Commands.ResetUserPassword;

[TestFixture]
public sealed class ResetUserPasswordHandlerActivityTests
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

    [Test]
    public async Task Handle_Reset_RecordsIt_WithoutThePassword()
    {
        _users.Setup(u => u.ResetUserPasswordAsync("some-user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResetUserPasswordResponse("Generated-1", []));

        await _fixture.Create<ResetUserPasswordHandler>().Handle(new ResetUserPasswordCommand("some-user"), CancellationToken.None);

        var entry = _activity.Recorded.Should().ContainSingle().Subject;
        entry.Action.Should().Be(ActivityAction.PasswordReset);
        entry.TargetUsername.Should().Be("some-user");
        entry.Details.Should().NotContain("Generated-1");
    }

    [Test]
    public async Task Handle_Failed_RecordsNothing()
    {
        _users.Setup(u => u.ResetUserPasswordAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResetUserPasswordResponse(null, [IdentityErrors.UserNotFound]));

        await _fixture.Create<ResetUserPasswordHandler>().Handle(new ResetUserPasswordCommand("some-user"), CancellationToken.None);

        _activity.Recorded.Should().BeEmpty();
    }
}
