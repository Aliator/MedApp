using FluentAssertions;
using MedApp.Domain.Authentication;

namespace MedApp.UnitTests.Domain.Authentication;

[TestFixture]
public sealed class UserSessionTests
{
    private static readonly DateTime Created = new(2026, 6, 15, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Expires = Created.AddHours(8);

    private static UserSession Session() => new(Guid.Parse("00000010-0000-0000-0000-000000000001"), Created, Expires, "192.0.2.10", "SyntheticBrowser/1.0");

    [Test]
    public void New_KeepsWhoStartedItAndWhen()
    {
        var session = Session();

        session.Id.Should().NotBeEmpty();
        session.UserId.Should().Be(Guid.Parse("00000010-0000-0000-0000-000000000001"));
        session.CreatedAtUtc.Should().Be(Created);
        session.ExpiresAtUtc.Should().Be(Expires);
        session.CreatedByIp.Should().Be("192.0.2.10");
        session.UserAgent.Should().Be("SyntheticBrowser/1.0");
        session.RevokedAtUtc.Should().BeNull();
    }

    [Test]
    public void IsActive_UntilItExpires()
    {
        var session = Session();

        session.IsActive(Expires.AddSeconds(-1)).Should().BeTrue();
        session.IsActive(Expires).Should().BeFalse();
    }

    [Test]
    public void Revoke_EndsItAtOnce_AndKeepsTheFirstRevocation()
    {
        var session = Session();

        session.Revoke(Created.AddHours(1));
        session.Revoke(Created.AddHours(2));

        session.IsActive(Created.AddHours(1)).Should().BeFalse();
        session.RevokedAtUtc.Should().Be(Created.AddHours(1));
    }

    [Test]
    public void Lifetimes_AreEightHoursExtendableToTwentyFour()
    {
        UserSession.Lifetime.Should().Be(TimeSpan.FromHours(8));
        UserSession.MaxLifetime.Should().Be(TimeSpan.FromHours(24));
        Session().LatestExpiryUtc.Should().Be(Created.AddHours(24));
    }

    [Test]
    public void Extend_GivesAFullLifetimeFromNow()
    {
        var session = Session();
        var now = Expires.AddMinutes(-5);

        session.Extend(now);

        session.ExpiresAtUtc.Should().Be(now.AddHours(8));
    }

    [Test]
    public void Extend_NearTheLongestASessionLasts_StopsThere()
    {
        var session = Session();
        session.Extend(Expires.AddMinutes(-5));
        session.Extend(Created.AddHours(15));
        var later = Created.AddHours(20);

        session.Extend(later);

        session.ExpiresAtUtc.Should().Be(Created.AddHours(24));
        session.CanExtend(later).Should().BeFalse();
    }

    [Test]
    public void Extend_NeverShortensIt()
    {
        var session = Session();
        session.Extend(Created.AddHours(7));

        // A full lifetime from an earlier moment would end before it now does.
        session.Extend(Created.AddHours(1));

        session.ExpiresAtUtc.Should().Be(Created.AddHours(15));
    }

    [Test]
    public void Extend_AnEndedSession_LeavesItEnded()
    {
        var expired = Session();
        var revoked = Session();
        revoked.Revoke(Created.AddHours(1));

        expired.Extend(Expires.AddMinutes(1));
        revoked.Extend(Created.AddHours(2));

        expired.ExpiresAtUtc.Should().Be(Expires);
        expired.IsActive(Expires.AddMinutes(1)).Should().BeFalse();
        revoked.ExpiresAtUtc.Should().Be(Expires);
        revoked.IsActive(Created.AddHours(2)).Should().BeFalse();
    }

    [Test]
    public void CanExtend_OnlyWhileActiveAndShortOfTheLongest()
    {
        var session = Session();
        var revoked = Session();
        revoked.Revoke(Created.AddHours(1));

        session.CanExtend(Created.AddHours(1)).Should().BeTrue();
        session.CanExtend(Expires).Should().BeFalse();
        revoked.CanExtend(Created.AddHours(2)).Should().BeFalse();
    }
}

[TestFixture]
public sealed class UserSettingsTests
{
    [Test]
    public void Replace_TakesTheNewSettingsAndTime()
    {
        var userId = Guid.Parse("00000010-0000-0000-0000-000000000002");
        var settings = new UserSettings(userId, "{}", new DateTime(2026, 6, 15, 9, 0, 0, DateTimeKind.Utc));

        settings.Replace("{\"medapp.colorMode\":\"light\"}", new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc));

        settings.UserId.Should().Be(userId);
        settings.Json.Should().Be("{\"medapp.colorMode\":\"light\"}");
        settings.UpdatedAtUtc.Should().Be(new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc));
    }
}
