using AutoFixture;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Authentication.Users.Commands.CreateUser;
using MedApp.Application.Common.Identity;
using MedApp.Domain.Activity;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace MedApp.UnitTests.Application.Auth.Users.Commands.CreateUser;

[TestFixture]
public sealed class CreateUserHandlerActivityTests
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
    public async Task Handle_Created_RecordsTheAccount()
    {
        _users.Setup(u => u.CreateUserAsync("new-user", It.IsAny<CancellationToken>())).ReturnsAsync(CreateUserResult.Created("Generated-1"));
        using var cts = new CancellationTokenSource();

        await _fixture.Create<CreateUserHandler>().Handle(new CreateUserCommand("new-user"), cts.Token);

        var entry = _activity.Recorded.Should().ContainSingle().Subject;
        entry.Action.Should().Be(ActivityAction.AccountCreated);
        entry.TargetUsername.Should().Be("new-user");
        entry.Details.Should().NotContain("Generated-1");
        _activity.Tokens.Should().Equal(cts.Token);
    }

    [Test]
    public async Task Handle_Failed_RecordsNothing()
    {
        _users.Setup(u => u.CreateUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateUserResult.Failed(IdentityResult.Failed(new IdentityError { Code = "Duplicate" })));

        await _fixture.Create<CreateUserHandler>().Handle(new CreateUserCommand("new-user"), CancellationToken.None);

        _activity.Recorded.Should().BeEmpty();
    }
}
