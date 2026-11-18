using FluentAssertions;
using MedApp.API.Common.RateLimiting;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MedApp.UnitTests.Api.RateLimiting;

[TestFixture]
public sealed class AccountAttemptLimiterTests
{
    private static readonly Guid Account = new("0000000f-0000-0000-0000-0000000000a1");
    private static readonly Guid OtherAccount = new("0000000f-0000-0000-0000-0000000000a2");

    private static AccountAttemptLimiter Limiter(int codes = 2, int emailChanges = 1, int emailCodes = 1) =>
        new(Options.Create(new LoginThrottlingOptions
        {
            TwoFactorCodesPerAccountPermitLimit = codes,
            TwoFactorCodeWindow = TimeSpan.FromMinutes(15),
            EmailChangesPerAccountPermitLimit = emailChanges,
            EmailChangeWindow = TimeSpan.FromMinutes(15),
            EmailCodesPerAccountPermitLimit = emailCodes,
            EmailCodeWindow = TimeSpan.FromMinutes(15),
        }));

    [Test]
    public void Attempt_EmailedCodes_HaveTheirOwnLimit()
    {
        using var limiter = Limiter(codes: 1, emailCodes: 1);

        limiter.Attempt(AccountAttempt.TwoFactorCode, Account);
        limiter.Attempt(AccountAttempt.EmailCode, Account);

        var act = () => limiter.Attempt(AccountAttempt.EmailCode, Account);
        act.Should().Throw<TooManyAttemptsException>().Which.Message.Should().StartWith("Too many codes emailed to this account.");
        new LoginThrottlingOptions().EmailCodesPerAccountPermitLimit.Should().Be(10);
    }

    [Test]
    public void Attempt_OverTheAccountsLimit_IsRefusedWithAWait()
    {
        using var limiter = Limiter();

        limiter.Attempt(AccountAttempt.TwoFactorCode, Account);
        limiter.Attempt(AccountAttempt.TwoFactorCode, Account);
        var act = () => limiter.Attempt(AccountAttempt.TwoFactorCode, Account);

        var refused = act.Should().Throw<TooManyAttemptsException>().Which;
        refused.RetryAfterSeconds.Should().BeInRange(1, 15 * 60);
        refused.Message.Should().StartWith("Too many codes tried for this account.");
    }

    [Test]
    public void Attempt_EachAccountAndKindHasItsOwnCount()
    {
        using var limiter = Limiter(codes: 1, emailChanges: 1);

        limiter.Attempt(AccountAttempt.TwoFactorCode, Account);
        limiter.Attempt(AccountAttempt.TwoFactorCode, OtherAccount);
        limiter.Attempt(AccountAttempt.EmailChange, Account);

        var act = () => limiter.Attempt(AccountAttempt.EmailChange, Account);
        act.Should().Throw<TooManyAttemptsException>().Which.Message.Should().StartWith("Too many email address changes");
    }

    [Test]
    public void Attempt_UnknownKind_Throws()
    {
        using var limiter = Limiter();

        var act = () => limiter.Attempt((AccountAttempt)99, Account);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [TestCase(1, "1 second")]
    [TestCase(30, "30 seconds")]
    [TestCase(60, "1 minute")]
    [TestCase(61, "2 minutes")]
    [TestCase(900, "15 minutes")]
    public void Wait_InWords(int seconds, string expected) =>
        AccountAttemptLimiter.Wait(seconds).Should().Be(expected);

    [Test]
    public void AddLoginThrottling_RegistersTheLimiterAndItsDefaults()
    {
        using var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(Options.Create(new IdentityOptions()))
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().Build())
            .AddLoginThrottling()
            .BuildServiceProvider();

        services.GetRequiredService<IAccountAttemptLimiter>().Should().BeOfType<AccountAttemptLimiter>();
        var options = services.GetRequiredService<IOptions<LoginThrottlingOptions>>().Value;
        options.TwoFactorCodesPerAccountPermitLimit.Should().Be(10);
        options.EmailChangesPerAccountPermitLimit.Should().Be(3);
    }

    [Test]
    public void Unlimited_NeverRefuses()
    {
        var unlimited = new UnlimitedAccountAttempts();

        var act = () => Enumerable.Range(0, 100).ToList().ForEach(_ => unlimited.Attempt(AccountAttempt.TwoFactorCode, Account));

        act.Should().NotThrow();
    }
}
