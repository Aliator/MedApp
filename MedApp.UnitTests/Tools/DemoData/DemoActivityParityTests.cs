using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Authentication.Sessions.Commands.Login;
using MedApp.Application.Authentication.Sessions.Commands.LoginTwoFactor;
using MedApp.Application.Authentication.TwoFactor.Common;
using MedApp.Application.Authentication.TwoFactor.Repositories;
using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Email;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Paging;
using MedApp.Application.Common.Security;
using MedApp.Application.Patients.Queries.SearchPatients;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Referrals.Queries.GetReferralById;
using MedApp.Application.Referrals.Repositories;
using MedApp.DevMcp.DemoData;
using MedApp.Domain.Activity;
using MedApp.Domain.Authentication;
using MedApp.Domain.Patients;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.EmailTestConstants;

namespace MedApp.UnitTests.Tools.DemoData;

/// <summary>
/// The demo data writes its history's sign-ins and searches itself (<see cref="DemoActivity"/>), much faster than
/// sending each through a command. These run the real commands and check the seed writes what they write: the same
/// actions, in the same order, with the same details and by the same account.
/// </summary>
[TestFixture]
public sealed class DemoActivityParityTests
{
    private const string Browser = "Browser";
    private const string IpAddress = "192.0.2.1";

    private RecordingActivityLog _activity = null!;
    private Mock<IAuthenticationService> _authentication = null!;
    private Mock<ITwoFactorService> _twoFactor = null!;
    private Mock<ISignInChallengeRepository> _challenges = null!;
    private Mock<ISessionService> _sessions = null!;
    private FixedTimeProvider _clock = null!;

    [SetUp]
    public void SetUp()
    {
        _activity = new RecordingActivityLog();
        _authentication = new Mock<IAuthenticationService>();
        _twoFactor = new Mock<ITwoFactorService>();
        _challenges = new Mock<ISignInChallengeRepository>();
        _sessions = new Mock<ISessionService>();
        _clock = new FixedTimeProvider(NowUtc);

        _authentication.Setup(a => a.ValidateCredentialsAsync(Username, CurrentPassword)).ReturnsAsync((UserId, Username, ["User"]));
        _sessions.Setup(s => s.CreateSessionAsync(UserId, IpAddress, Browser, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoginResult(SessionId, NowUtc.AddHours(8)));
    }

    private LoginHandler Login() =>
        new(_authentication.Object, _sessions.Object, _activity, _twoFactor.Object, _challenges.Object, Mock.Of<IEmailCodeService>(), _clock);

    /// <summary>What the seed writes, in the form the log stores it: action, details (as JSON) and by whom.</summary>
    private static IEnumerable<(ActivityAction Action, string Details, Guid? UserId)> AsWritten(
        IEnumerable<(ActivityAction Action, object? Details)> entries, Guid? userId) =>
        entries.Select(e => (e.Action, ActivityLog.SerializeDetails(e.Details), userId));

    private IEnumerable<(ActivityAction Action, string Details, Guid? UserId)> Written() =>
        _activity.Recorded.Select(e => (e.Action, e.Details, e.UserId));

    [Test]
    public async Task SignIn_WithoutTwoFactor_WritesWhatTheSeedWrites()
    {
        await Login().Handle(new LoginCommand(Username, CurrentPassword, IpAddress, Browser), CancellationToken.None);

        Written().Should().Equal(AsWritten(DemoActivity.SignInEntries(twoFactor: false), UserId));
    }

    [Test]
    public async Task SignIn_WithTwoFactor_WritesWhatTheSeedWrites()
    {
        _twoFactor.Setup(t => t.GetMethodAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(TwoFactorMethod.Authenticator);
        var outcome = await Login().Handle(new LoginCommand(Username, CurrentPassword, IpAddress, Browser), CancellationToken.None);

        // The second step, with the challenge the first gave and a right authenticator code.
        var challenge = new SignInChallenge(UserId, SignInChallengeTokens.Hash(outcome.Challenge!), NowUtc);
        _challenges.Setup(c => c.FindByTokenHashAsync(challenge.TokenHash, It.IsAny<CancellationToken>())).ReturnsAsync(challenge);
        _challenges.Setup(c => c.TryReserveAttemptAsync(challenge.Id, NowUtc, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _challenges.Setup(c => c.ConsumeAsync(challenge.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _twoFactor.Setup(t => t.VerifyAuthenticatorCodeAsync(UserId, AuthenticatorCode, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var read = new Mock<IIdentityReadService>();
        read.Setup(r => r.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [UserId] = Username });
        var secondStep = new LoginTwoFactorHandler(_challenges.Object, _twoFactor.Object, _sessions.Object, read.Object, _activity,
            Mock.Of<ISecurityNotices>(), Mock.Of<ISecurityEventLog>(), Mock.Of<IAccountAttemptLimiter>(), Mock.Of<IEmailCodeService>(), _clock);
        await secondStep.Handle(new LoginTwoFactorCommand(outcome.Challenge!, AuthenticatorCode, null, IpAddress, Browser), CancellationToken.None);

        Written().Should().Equal(AsWritten(DemoActivity.SignInEntries(twoFactor: true), UserId));
    }

    [TestCase(true, TestName = "WrongPassword_ForAnAccount_WritesWhatTheSeedWrites")]
    [TestCase(false, TestName = "WrongPassword_ForANameNoAccountHas_WritesWhatTheSeedWrites")]
    public async Task WrongPassword_WritesWhatTheSeedWrites(bool accountExists)
    {
        Guid? userId = accountExists ? UserId : null;
        _authentication.Setup(a => a.ValidateCredentialsAsync(Username, "wrong")).ThrowsAsync(new InvalidCredentialsException(userId));

        var act = () => Login().Handle(new LoginCommand(Username, "wrong", IpAddress, Browser), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
        Written().Should().Equal(AsWritten([DemoActivity.RefusedSignIn], userId));
        _activity.Recorded.Single().Username.Should().Be(Username, "a refused sign-in records the name typed, as the seed's typos do");
    }

    [TestCaseSource(typeof(DemoActivity), nameof(DemoActivity.Searches))]
    public async Task Search_WritesWhatTheSeedWrites(SearchPatientsQuery query)
    {
        var found = new[] { PatientsTestConstants.CreateValidPatient(), PatientsTestConstants.CreateValidPatient() };
        var repository = new Mock<IPatientRepository>();
        repository.Setup(r => r.SearchAsync(query, It.IsAny<CancellationToken>())).ReturnsAsync(new PagedResult<Patient>(found, found.Length));

        await new SearchPatientsHandler(repository.Object, _activity, Mock.Of<MedApp.Application.Teams.Repositories.ITeamRepository>()).Handle(query, CancellationToken.None);

        var searched = _activity.Recorded.Should().ContainSingle("every search the seed makes uses a filter, so it's recorded").Subject;
        (searched.Action, searched.Details).Should().Be(
            (ActivityAction.PatientsSearched, ActivityLog.SerializeDetails(DemoActivity.SearchDetails(PatientSearch.FiltersUsed(query), found.Length))));
        var listed = _activity.Views.Should().ContainSingle().Subject;
        listed.Action.Should().Be(ActivityAction.PatientListed, "the seed lists each patient a search found");
        listed.PatientIds.Should().Equal(found.Select(p => p.Id));
        listed.PathwayId.Should().BeNull();
    }

    /// <summary>Opening a referral records what the seed writes for each referral someone registered or triaged.</summary>
    [Test]
    public async Task ReferralViewed_WritesWhatTheSeedWrites()
    {
        var referral = ReferralsTestConstants.CreateReferral();
        var repository = new Mock<IReferralRepository>();
        repository.Setup(r => r.GetByIdAsync(referral.Id, It.IsAny<CancellationToken>())).ReturnsAsync(referral);
        repository.Setup(r => r.GetHistoryAsync(referral.Id, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var registrar = ReferralsTestConstants.Registrar with
        {
            Scope = GrantsTeamBoundary.MemberOf(ReferralsTestConstants.Registrar.UserId, referral.TeamId)
        };

        await new GetReferralByIdHandler(repository.Object, new GrantsTeamBoundary(_activity).Boundary, _activity, _clock)
            .Handle(new GetReferralByIdQuery(referral.Id, registrar), CancellationToken.None);

        _activity.SingleViews.Should().Equal(
            (ActivityAction.ReferralViewed, referral.PatientId, ActivityLog.SerializeDetails(DemoActivity.ReferralViewDetails(referral.Id))));
    }
}
