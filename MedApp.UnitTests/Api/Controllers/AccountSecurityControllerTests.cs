using System.Net;
using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using MedApp.API.Common.Authentication;
using MedApp.API.Common.Extensions;
using MedApp.API.Common.RateLimiting;
using MedApp.API.Controllers;
using MedApp.Application.Authentication.Emails.Commands.ChangeMyEmail;
using MedApp.Application.Authentication.Emails.Commands.ConfirmEmail;
using MedApp.Application.Authentication.Emails.Commands.RemoveMyEmail;
using MedApp.Application.Authentication.Emails.Queries.GetMyEmail;
using MedApp.Application.Authentication.Passwords.Commands.ForgotPassword;
using MedApp.Application.Authentication.Passwords.Commands.ResetPassword;
using MedApp.Application.Authentication.Security.Commands.SaveSecuritySettings;
using MedApp.Application.Authentication.Security.Queries.GetSecuritySettings;
using MedApp.Application.Authentication.Sessions.Commands.LoginTwoFactor;
using MedApp.Application.Authentication.Sessions.Commands.ResendSignInCode;
using MedApp.Application.Authentication.Sessions.Queries.GetAuthFeatures;
using MedApp.Application.Authentication.TwoFactor.Commands.BeginAuthenticatorSetup;
using MedApp.Application.Authentication.TwoFactor.Commands.BeginEmailCodesSetup;
using MedApp.Application.Authentication.TwoFactor.Commands.EnableEmailCodes;
using MedApp.Application.Authentication.TwoFactor.Commands.SendTwoFactorEmailCode;
using MedApp.Application.Authentication.TwoFactor.Commands.DisableTwoFactor;
using MedApp.Application.Authentication.TwoFactor.Commands.EnableTwoFactor;
using MedApp.Application.Authentication.TwoFactor.Commands.RegenerateRecoveryCodes;
using MedApp.Application.Authentication.TwoFactor.Commands.ResetUserTwoFactor;
using MedApp.Application.Authentication.TwoFactor.Queries.GetMyTwoFactorStatus;
using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Authentication.Requests;
using MedApp.Contracts.Authentication.Responses;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using static MedApp.UnitTests.Common.Constants.ApiTestConstants;

namespace MedApp.UnitTests.Api.Controllers;

[TestFixture]
public sealed class AccountSecurityControllerTests
{
    private Mock<ISessionCookieService> _cookies = null!;

    [SetUp]
    public void SetUp() => _cookies = new Mock<ISessionCookieService>();

    private AccountSecurityController Controller(RecordingMediator mediator, ClaimsPrincipal? user = null, Action<HttpContext>? configure = null) =>
        new AccountSecurityController(mediator, _cookies.Object).WithRequest(user ?? SignedInWithSession("User"), configure);

    [Test]
    public async Task LoginTwoFactor_RightCode_StartsTheSessionAsSignInDoes()
    {
        var session = new LoginResult(SessionId, ExpiresAtUtc);
        var mediator = new RecordingMediator(_ => session);
        var controller = Controller(mediator, new ClaimsPrincipal(new ClaimsIdentity()), context =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(IpAddress);
            context.Request.Headers.UserAgent = UserAgent;
        });

        var result = await controller.LoginTwoFactor(new LoginTwoFactorRequest(TwoFactorChallenge, AuthenticatorCode));

        mediator.Request.Should().Be(new LoginTwoFactorCommand(TwoFactorChallenge, AuthenticatorCode, null, IpAddress, UserAgent));
        _cookies.Verify(c => c.AppendSessionCookie(controller.Response, session), Times.Once);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(new LoginResponse(SessionId, ExpiresAtUtc));
    }

    [Test]
    public async Task ResendSignInCode_SendsTheChallengeOnAndSetsNoCookie()
    {
        var sent = new EmailCodeSentResponse("s•••@example.test", ExpiresAtUtc, 60, 2);
        var mediator = new RecordingMediator(_ => sent);

        var result = await Controller(mediator, new ClaimsPrincipal(new ClaimsIdentity()))
            .ResendSignInCode(new ResendSignInCodeRequest(TwoFactorChallenge), CancellationToken.None);

        mediator.Request.Should().Be(new ResendSignInCodeCommand(TwoFactorChallenge));
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(sent);
        _cookies.VerifyNoOtherCalls();
    }

    [Test]
    public async Task EmailCodes_EachActionIsForTheSignedInUserAndSession()
    {
        var sent = new EmailCodeSentResponse("s•••@example.test", ExpiresAtUtc, 60, 3);
        var mediator = new RecordingMediator(request => request switch
        {
            EnableEmailCodesCommand or EnableTwoFactorCommand => new RecoveryCodesResponse(["A"]),
            ChangeMyEmailCommand => new EmailResponse(Email, null),
            _ => sent,
        });
        var controller = Controller(mediator);

        (await controller.BeginEmailCodesSetup(new EmailCodesSetupRequest(Password), CancellationToken.None))
            .Should().BeOfType<AcceptedResult>().Which.Value.Should().Be(sent);
        (await controller.EnableEmailCodes(new EmailCodeRequest(AuthenticatorCode), CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await controller.SendTwoFactorEmailCode(new SendEmailCodeRequest(Password), CancellationToken.None)).Should().BeOfType<AcceptedResult>();
        (await controller.EnableTwoFactor(new TwoFactorCodeRequest(Password, AuthenticatorCode, "ABCDE-23456"), CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await controller.ChangeMyEmail(new ChangeEmailRequest(Email, Password, "654321"), CancellationToken.None)).Should().BeOfType<AcceptedResult>();

        mediator.Sent.Should().Equal(
            new BeginEmailCodesSetupCommand(UserId, Password),
            new EnableEmailCodesCommand(UserId, AuthenticatorCode, SessionId),
            new SendTwoFactorEmailCodeCommand(UserId, Password),
            new EnableTwoFactorCommand(UserId, Password, AuthenticatorCode, SessionId, "ABCDE-23456"),
            new ChangeMyEmailCommand(UserId, Email, Password, "654321"));
        new EmailCodesSetupRequest(Password).ToString().Should().NotContain(Password);
        new SendEmailCodeRequest(Password).ToString().Should().NotContain(Password);
        new TwoFactorCodeRequest(Password, AuthenticatorCode, "ABCDE-23456").ToString().Should().NotContain("ABCDE-23456");
        new ChangeEmailRequest(Email, Password, "654321").ToString().Should().NotContain("654321");
        new EmailCodeRequest(AuthenticatorCode).ToString().Should().NotContain(AuthenticatorCode);
    }

    [Test]
    public void ResendSignInCode_IsOpenThrottledAndSmall()
    {
        var method = typeof(AccountSecurityController).GetMethod(nameof(AccountSecurityController.ResendSignInCode))!;

        method.GetCustomAttributes<AuthorizeAttribute>().Should().BeEmpty();
        method.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName.Should().Be(LoginThrottling.Policy);
        method.GetCustomAttribute<RequestSizeLimitAttribute>().Should().NotBeNull();
    }

    [Test]
    public async Task Features_ComeFromTheQuery()
    {
        var mediator = new RecordingMediator(_ => new AuthFeaturesResponse(true));

        var result = await Controller(mediator).GetFeatures(CancellationToken.None);

        mediator.Request.Should().BeOfType<GetAuthFeaturesQuery>();
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(new AuthFeaturesResponse(true));
    }

    [Test]
    public async Task ForgotPassword_IsAlwaysAccepted()
    {
        var mediator = new RecordingMediator();

        var result = await Controller(mediator).ForgotPassword(new ForgotPasswordRequest(Email), CancellationToken.None);

        mediator.Request.Should().Be(new ForgotPasswordCommand(Email));
        result.Should().BeOfType<AcceptedResult>();
    }

    [Test]
    public async Task ResetPassword_SucceedsOrReturnsIdentitysErrors()
    {
        var userId = Guid.NewGuid();
        var ok = await Controller(new RecordingMediator(_ => IdentityResult.Success))
            .ResetPassword(new ResetPasswordRequest(userId, "token", Password), CancellationToken.None);
        var bad = await Controller(new RecordingMediator(_ => IdentityResult.Failed(IdentityErrors.InvalidToken)))
            .ResetPassword(new ResetPasswordRequest(userId, "token", Password), CancellationToken.None);

        ok.Should().BeOfType<NoContentResult>();
        bad.Should().BeOfType<BadRequestObjectResult>().Which.Value.Should().BeAssignableTo<IEnumerable<IdentityError>>()
            .Which.Should().ContainSingle(e => e.Code == IdentityErrors.InvalidTokenCode);
    }

    [Test]
    public async Task ConfirmEmail_SucceedsOrReturnsIdentitysErrors()
    {
        var userId = Guid.NewGuid();
        var mediator = new RecordingMediator(_ => IdentityResult.Success);

        (await Controller(mediator).ConfirmEmail(new ConfirmEmailRequest(userId, "token"), CancellationToken.None)).Should().BeOfType<NoContentResult>();
        mediator.Request.Should().Be(new ConfirmEmailCommand(userId, "token", UserId));
        (await Controller(new RecordingMediator(_ => IdentityResult.Failed(IdentityErrors.InvalidToken)))
            .ConfirmEmail(new ConfirmEmailRequest(userId, "token"), CancellationToken.None)).Should().BeOfType<BadRequestObjectResult>();
    }

    [Test]
    public async Task MyEmail_GetChangeAndRemove_AreForTheSignedInUser()
    {
        var response = new EmailResponse(Email, null);
        var mediator = new RecordingMediator(_ => response);
        var controller = Controller(mediator);

        (await controller.GetMyEmail(CancellationToken.None)).Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(response);
        (await controller.ChangeMyEmail(new ChangeEmailRequest(Email, Password), CancellationToken.None)).Should().BeOfType<AcceptedResult>()
            .Which.Value.Should().Be(response);
        (await controller.RemoveMyEmail(new RemoveEmailRequest(Password), CancellationToken.None)).Should().BeOfType<NoContentResult>();

        mediator.Sent.Should().Equal(new GetMyEmailQuery(UserId), new ChangeMyEmailCommand(UserId, Email, Password), new RemoveMyEmailCommand(UserId, Password));
    }

    [Test]
    public async Task MyTwoFactor_EachActionIsForTheSignedInUserAndSession()
    {
        var mediator = new RecordingMediator(request => request switch
        {
            GetMyTwoFactorStatusQuery => new TwoFactorStatusResponse(true, 10, false),
            BeginAuthenticatorSetupCommand => new AuthenticatorSetupResponse("abcd", "otpauth://x", "M0,0", 29),
            _ => new RecoveryCodesResponse(["A"]),
        });
        var controller = Controller(mediator);

        (await controller.GetMyTwoFactor(CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await controller.BeginAuthenticatorSetup(CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await controller.EnableTwoFactor(new TwoFactorCodeRequest(Password, AuthenticatorCode), CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await controller.DisableTwoFactor(new DisableTwoFactorRequest(Password, AuthenticatorCode), CancellationToken.None)).Should().BeOfType<NoContentResult>();
        (await controller.RegenerateRecoveryCodes(new TwoFactorCodeRequest(Password, AuthenticatorCode), CancellationToken.None)).Should().BeOfType<OkObjectResult>();

        mediator.Sent.Should().Equal(
            new GetMyTwoFactorStatusQuery(UserId),
            new BeginAuthenticatorSetupCommand(UserId),
            new EnableTwoFactorCommand(UserId, Password, AuthenticatorCode, SessionId),
            new DisableTwoFactorCommand(UserId, Password, AuthenticatorCode, SessionId),
            new RegenerateRecoveryCodesCommand(UserId, Password, AuthenticatorCode));
    }

    [Test]
    public async Task RemoveUserEmail_IsByTheAdminAndUnknownIsNotFound()
    {
        var mediator = new RecordingMediator(_ => IdentityResult.Success);

        (await Controller(mediator, SignedIn("Admin")).RemoveUserEmail(OtherUsername, CancellationToken.None)).Should().BeOfType<NoContentResult>();
        mediator.Request.Should().Be(new MedApp.Application.Authentication.Emails.Commands.RemoveUserEmail.RemoveUserEmailCommand(OtherUsername, UserId, Username));
        (await Controller(new RecordingMediator(_ => IdentityResult.Failed(IdentityErrors.UserNotFound)), SignedIn("Admin"))
            .RemoveUserEmail(OtherUsername, CancellationToken.None)).Should().BeOfType<NotFoundResult>();
    }

    [Test]
    public void Requests_NeverPrintThePasswordOrCode()
    {
        new ChangeEmailRequest(Email, Password).ToString().Should().NotContain(Password);
        new RemoveEmailRequest(Password).ToString().Should().NotContain(Password);
        new TwoFactorCodeRequest(Password, AuthenticatorCode).ToString().Should().NotContain(Password).And.NotContain(AuthenticatorCode);
    }

    [TestCase(true, typeof(NoContentResult))]
    public async Task ResetUserTwoFactor_Succeeds(bool _, Type expected)
    {
        var mediator = new RecordingMediator(_ => IdentityResult.Success);

        var result = await Controller(mediator, SignedIn("Admin")).ResetUserTwoFactor(OtherUsername, CancellationToken.None);

        result.Should().BeOfType(expected);
        mediator.Request.Should().Be(new ResetUserTwoFactorCommand(OtherUsername, UserId, Username));
    }

    [Test]
    public async Task ResetUserTwoFactor_UnknownUserIsNotFoundAndOwnIsBadRequest()
    {
        (await Controller(new RecordingMediator(_ => IdentityResult.Failed(IdentityErrors.UserNotFound)), SignedIn("Admin"))
            .ResetUserTwoFactor(OtherUsername, CancellationToken.None)).Should().BeOfType<NotFoundResult>();
        (await Controller(new RecordingMediator(_ => IdentityResult.Failed(IdentityErrors.OwnTwoFactor)), SignedIn("Admin"))
            .ResetUserTwoFactor(Username, CancellationToken.None)).Should().BeOfType<BadRequestObjectResult>();
    }

    [Test]
    public async Task SecuritySettings_GetAndSave()
    {
        var response = new SecuritySettingsResponse(true, 0, ExpiresAtUtc, Username, []);
        var mediator = new RecordingMediator(_ => response);
        var controller = Controller(mediator, SignedIn("Admin"));

        (await controller.GetSecuritySettings(CancellationToken.None)).Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(response);
        (await controller.SaveSecuritySettings(new SaveSecuritySettingsRequest(true), CancellationToken.None)).Should().BeOfType<OkObjectResult>();

        mediator.Sent.Should().Equal(new GetSecuritySettingsQuery(), new SaveSecuritySettingsCommand(true, UserId, Username));
    }

    [TestCase(nameof(AccountSecurityController.LoginTwoFactor))]
    [TestCase(nameof(AccountSecurityController.ForgotPassword))]
    [TestCase(nameof(AccountSecurityController.ResetPassword))]
    public void EndpointsUsedBeforeSigningIn_AreAnonymousAndThrottledPerAddress(string action)
    {
        var method = typeof(AccountSecurityController).GetMethod(action)!;

        method.GetCustomAttributes<AuthorizeAttribute>().Should().BeEmpty();
        method.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName.Should().Be(LoginThrottling.Policy);
        method.GetCustomAttribute<RequestSizeLimitAttribute>().Should().NotBeNull();
    }

    [Test]
    public void ConfirmEmail_NeedsTheAccountSignedIn()
    {
        var method = typeof(AccountSecurityController).GetMethod(nameof(AccountSecurityController.ConfirmEmail))!;

        method.GetCustomAttributes<AuthorizeAttribute>().Should().ContainSingle();
        method.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName.Should().Be(LoginThrottling.Policy);
    }

    [TestCase(nameof(AccountSecurityController.EnableTwoFactor))]
    [TestCase(nameof(AccountSecurityController.DisableTwoFactor))]
    [TestCase(nameof(AccountSecurityController.RegenerateRecoveryCodes))]
    [TestCase(nameof(AccountSecurityController.BeginEmailCodesSetup))]
    [TestCase(nameof(AccountSecurityController.EnableEmailCodes))]
    [TestCase(nameof(AccountSecurityController.SendTwoFactorEmailCode))]
    public void EndpointsThatCheckACode_AreThrottledPerAddress(string action) =>
        typeof(AccountSecurityController).GetMethod(action)!.GetCustomAttribute<EnableRateLimitingAttribute>()!
            .PolicyName.Should().Be(LoginThrottling.Policy);

    [Test]
    public void ForgotPassword_IsAlsoThrottledPerIdentifier() =>
        typeof(AccountSecurityController).GetMethod(nameof(AccountSecurityController.ForgotPassword))!
            .GetCustomAttribute<ThrottleResetRequestsAttribute>().Should().NotBeNull();

    [TestCase(nameof(AccountSecurityController.GetMyEmail), "User", true)]
    [TestCase(nameof(AccountSecurityController.ChangeMyEmail), "Admin", true)]
    [TestCase(nameof(AccountSecurityController.GetMyTwoFactor), "User", true)]
    [TestCase(nameof(AccountSecurityController.EnableTwoFactor), "User", true)]
    [TestCase(nameof(AccountSecurityController.DisableTwoFactor), null, false)]
    [TestCase(nameof(AccountSecurityController.BeginEmailCodesSetup), "User", true)]
    [TestCase(nameof(AccountSecurityController.EnableEmailCodes), null, false)]
    [TestCase(nameof(AccountSecurityController.SendTwoFactorEmailCode), "Admin", true)]
    [TestCase(nameof(AccountSecurityController.ResetUserTwoFactor), "User", false)]
    [TestCase(nameof(AccountSecurityController.ResetUserTwoFactor), "Admin", true)]
    [TestCase(nameof(AccountSecurityController.GetSecuritySettings), "User", false)]
    [TestCase(nameof(AccountSecurityController.SaveSecuritySettings), "User", false)]
    [TestCase(nameof(AccountSecurityController.SaveSecuritySettings), "Admin", true)]
    public async Task Authorisation_StaffForTheirOwnAccountAndAdminsForEveryoneElse(string action, string? role, bool allowed)
    {
        var user = role is null ? SignedIn() : SignedIn(role);

        (await MayCall(action, user)).Should().Be(allowed);
    }

    private static async Task<bool> MayCall(string action, ClaimsPrincipal user)
    {
        await using var services = new ServiceCollection().AddLogging().AddApi(new ConfigurationBuilder().Build()).BuildServiceProvider();
        var attributes = typeof(AccountSecurityController).GetCustomAttributes<AuthorizeAttribute>()
            .Concat(typeof(AccountSecurityController).GetMethod(action)!.GetCustomAttributes<AuthorizeAttribute>());
        var policy = await AuthorizationPolicy.CombineAsync(services.GetRequiredService<IAuthorizationPolicyProvider>(), attributes);
        var result = await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, new DefaultHttpContext(), policy!);
        return result.Succeeded;
    }
}
