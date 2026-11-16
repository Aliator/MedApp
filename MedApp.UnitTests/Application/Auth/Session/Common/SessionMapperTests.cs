using FluentAssertions;
using MedApp.Application.Authentication.Sessions.Common;
using MedApp.Contracts.Authentication.Responses;
using MedApp.Domain.Authentication;
using MedApp.UnitTests.Common.Constants;

namespace MedApp.UnitTests.Application.Auth.Session.Common;

[TestFixture]
public sealed class SessionMapperTests
{
    [Test]
    public void ToResponse_ActiveSession_ReturnsWhenItEndsTheSecondsLeftAndThatItCanBeExtended()
    {
        var session = AuthTestConstants.CreateSession();

        SessionMapper.ToResponse(session, AuthTestConstants.SessionNowUtc)
            .Should().Be(new SessionResponse(session.ExpiresAtUtc, 300, true));
    }

    [Test]
    public void ToResponse_ExpiryReadBackWithoutAKind_MarksItUtc()
    {
        // As EF Core reads a datetime2 back from SQL Server: the same moment, with no kind.
        var started = DateTime.SpecifyKind(AuthTestConstants.SessionStartedAtUtc, DateTimeKind.Unspecified);
        var session = new UserSession(AuthTestConstants.SettingsUserId, started, started.AddHours(8), null, null);

        var response = SessionMapper.ToResponse(session, AuthTestConstants.SessionNowUtc);

        response.ExpiresAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        response.ExpiresAtUtc.Should().Be(AuthTestConstants.SessionStartedAtUtc.AddHours(8));
        response.ExpiresInSeconds.Should().Be(300);
        new DateTimeOffset(response.ExpiresAtUtc).Offset.Should().Be(TimeSpan.Zero);
    }

    [Test]
    public void ToResponse_PartSecondsLeft_CountsWholeSecondsOnly()
    {
        var session = AuthTestConstants.CreateSession();

        SessionMapper.ToResponse(session, session.ExpiresAtUtc.AddMilliseconds(-1500))
            .ExpiresInSeconds.Should().Be(1);
    }

    [Test]
    public void ToResponse_Ended_HasNoSecondsLeftAndCantBeExtended()
    {
        var session = AuthTestConstants.CreateSession();

        SessionMapper.ToResponse(session, session.ExpiresAtUtc.AddMinutes(1))
            .Should().Be(new SessionResponse(session.ExpiresAtUtc, 0, false));
    }

    [Test]
    public void ToResponse_AtTheLongestASessionLasts_CantBeExtended()
    {
        var session = AuthTestConstants.CreateSession();
        var late = AuthTestConstants.SessionStartedAtUtc.AddHours(20);
        session.Extend(AuthTestConstants.SessionNowUtc);
        session.Extend(AuthTestConstants.SessionStartedAtUtc.AddHours(15));
        session.Extend(late);

        SessionMapper.ToResponse(session, late)
            .Should().Be(new SessionResponse(AuthTestConstants.SessionStartedAtUtc.AddHours(24), 4 * 60 * 60, false));
    }
}
