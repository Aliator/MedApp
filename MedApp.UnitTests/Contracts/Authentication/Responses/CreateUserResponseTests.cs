using FluentAssertions;
using MedApp.Contracts.Authentication.Responses;
using MedApp.UnitTests.Common.Constants;

namespace MedApp.UnitTests.Contracts.Authentication.Responses;

[TestFixture]
public sealed class CreateUserResponseTests
{
    [Test]
    public void ToString_Always_ShowsTheUsernameButHidesThePassword()
    {
        var text = new CreateUserResponse(AuthTestConstants.Usernames[0], AuthTestConstants.ValidPassword).ToString();

        text.Should().NotContain(AuthTestConstants.ValidPassword);
        text.Should().Contain($"Username = {AuthTestConstants.Usernames[0]}");
        text.Should().Contain("Password = ***");
    }
}
