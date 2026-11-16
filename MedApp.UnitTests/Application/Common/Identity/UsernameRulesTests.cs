using FluentAssertions;
using MedApp.Application.Common.Identity;
using MedApp.UnitTests.Common.Constants;
using Microsoft.AspNetCore.Identity;

namespace MedApp.UnitTests.Application.Common.Identity;

[TestFixture]
public sealed class UsernameRulesTests
{
    private static readonly string Allowed = new UserOptions().AllowedUserNameCharacters;

    [TestCase("username")]
    [TestCase("first.last-2@example.test")]
    [TestCase("username ")]
    public void CouldExist_NamesAnAccountCouldHave_IsTrue(string username)
    {
        UsernameRules.CouldExist(username, Allowed).Should().BeTrue();
    }

    [TestCaseSource(typeof(AuthTestConstants), nameof(AuthTestConstants.UsernameVariants))]
    public void CouldExist_CharactersNoAccountHas_IsFalse(string username)
    {
        UsernameRules.CouldExist(username, Allowed).Should().BeFalse();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase(" username")]
    public void CouldExist_NothingOrALeadingSpace_IsFalse(string? username)
    {
        UsernameRules.CouldExist(username, Allowed).Should().BeFalse();
    }

    [Test]
    public void CouldExist_Length_IsAtMost256()
    {
        UsernameRules.CouldExist(new string('a', 256), Allowed).Should().BeTrue();
        UsernameRules.CouldExist(AuthTestConstants.TooLongUsername, Allowed).Should().BeFalse();
    }

    [TestCase(null)]
    [TestCase("")]
    public void CouldExist_NoCharacterRule_AllowsAnyCharacters(string? allowed)
    {
        UsernameRules.CouldExist("\uFEFFusername", allowed).Should().BeTrue();
        UsernameRules.CouldExist(AuthTestConstants.TooLongUsername, allowed).Should().BeFalse();
    }
}
