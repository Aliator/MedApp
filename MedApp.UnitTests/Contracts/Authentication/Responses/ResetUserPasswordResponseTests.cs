using FluentAssertions;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Authentication.Responses;
using MedApp.UnitTests.Common.Constants;

namespace MedApp.UnitTests.Contracts.Authentication.Responses;

[TestFixture]
public sealed class ResetUserPasswordResponseTests
{
    [Test]
    public void ToString_PasswordReset_HidesThePassword()
    {
        var text = new ResetUserPasswordResponse(AuthTestConstants.ValidPassword, []).ToString();

        text.Should().NotContain(AuthTestConstants.ValidPassword);
        text.Should().Contain("GeneratedPassword = ***");
    }

    [Test]
    public void ToString_ResetFailed_SaysThereIsNoPassword()
    {
        var text = new ResetUserPasswordResponse(null, [IdentityErrors.UserNotFound]).ToString();

        text.Should().Contain("GeneratedPassword = null");
    }
}
