using FluentAssertions;
using MedApp.Application.Common.Identity;
using MedApp.UnitTests.Common.Constants;
using Microsoft.AspNetCore.Identity;

namespace MedApp.UnitTests.Application.Common.Identity;

[TestFixture]
public sealed class CreateUserResultTests
{
    private static IdentityResult Duplicate() =>
        IdentityResult.Failed(new IdentityErrorDescriber().DuplicateUserName(AuthTestConstants.Usernames[0]));

    [Test]
    public void Created_WithAPassword_SucceedsAndCarriesIt()
    {
        var result = CreateUserResult.Created(AuthTestConstants.ValidPassword);

        result.Succeeded.Should().BeTrue();
        result.Result.Succeeded.Should().BeTrue();
        result.GeneratedPassword.Should().Be(AuthTestConstants.ValidPassword);
    }

    [TestCase(null)]
    [TestCase("")]
    public void Created_WithoutAPassword_Throws(string? password)
    {
        var create = () => CreateUserResult.Created(password!);

        create.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Failed_WithAFailure_FailsWithoutAPassword()
    {
        var failed = Duplicate();

        var result = CreateUserResult.Failed(failed);

        result.Succeeded.Should().BeFalse();
        result.Result.Should().Be(failed);
        result.GeneratedPassword.Should().BeNull();
    }

    [Test]
    public void Failed_WithASuccess_Throws()
    {
        var fail = () => CreateUserResult.Failed(IdentityResult.Success);

        fail.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Failed_WithNoResult_Throws()
    {
        var fail = () => CreateUserResult.Failed(null!);

        fail.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void ToString_Created_HidesThePassword()
    {
        var text = CreateUserResult.Created(AuthTestConstants.ValidPassword).ToString();

        text.Should().NotContain(AuthTestConstants.ValidPassword);
        text.Should().Contain("GeneratedPassword = ***");
    }

    [Test]
    public void ToString_Failed_ShowsTheFailureAndNoPassword()
    {
        var text = CreateUserResult.Failed(Duplicate()).ToString();

        text.Should().Contain(nameof(IdentityErrorDescriber.DuplicateUserName));
        text.Should().Contain("GeneratedPassword = null");
    }
}
