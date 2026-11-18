using MedApp.UnitTests.Common.Constants;
using System.Net;
using System.Reflection;
using AutoFixture;
using FluentAssertions;
using MedApp.API.Common.Authentication;
using MedApp.API.Common.Extensions;
using MedApp.API.Common.RateLimiting;
using MedApp.API.Controllers;
using MedApp.Application.Authentication.Roles.Commands.AssignRole;
using MedApp.Application.Authentication.Roles.Commands.CreateRole;
using MedApp.Application.Authentication.Roles.Commands.DeleteRole;
using MedApp.Application.Authentication.Roles.Commands.RevokeRole;
using MedApp.Application.Authentication.Roles.Commands.SetUserRoles;
using MedApp.Application.Authentication.Roles.Queries.GetAllRoles;
using MedApp.Application.Authentication.Roles.Queries.GetUserRoles;
using MedApp.Application.Authentication.Sessions.Commands.ExtendSession;
using MedApp.Application.Authentication.Sessions.Commands.Login;
using MedApp.Application.Authentication.Sessions.Queries.GetSession;
using MedApp.Application.Authentication.Sessions.Commands.Logout;
using MedApp.Application.Authentication.Sessions.Commands.SignOutEverywhere;
using MedApp.Application.Authentication.Settings.Commands.SaveMySettings;
using MedApp.Application.Authentication.Settings.Queries.GetMySettings;
using MedApp.Application.Authentication.Users.Commands.CreateUser;
using MedApp.Application.Authentication.Users.Commands.DeleteUser;
using MedApp.Application.Authentication.Users.Commands.ResetUserPassword;
using MedApp.Application.Authentication.Users.Commands.SetStaffDetails;
using MedApp.Application.Authentication.Users.Commands.SetUserSpecialties;
using MedApp.Application.Authentication.Users.Commands.UpdateUserPassword;
using MedApp.Application.Authentication.Users.Queries.GetAllUsers;
using MedApp.Application.Authentication.Users.Queries.GetMySpecialties;
using MedApp.Application.Authentication.Users.Queries.GetStaffDetails;
using MedApp.Application.Authentication.Users.Queries.GetUserSpecialties;
using MedApp.Application.Authentication.Users.Queries.GetUserByUsername;
using MedApp.Application.Authentication.Users.Queries.SearchUsers;
using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Authentication.Requests;
using MedApp.Contracts.Authentication.Responses;
using MedApp.Contracts.Common;
using MedApp.Contracts.Specialties.Responses;
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
public sealed class AuthControllerTests
{
    private IFixture _fixture = null!;
    private Mock<ISessionCookieService> _cookies = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        _cookies = new Mock<ISessionCookieService>();
        // Reads the session cookie as the real service does outside Development (a test may set up its own answer).
        var real = MedApp.UnitTests.Api.Authentication.SessionCookieServiceTests.Service(Microsoft.Extensions.Hosting.Environments.Production);
        Guid? any;
        _cookies
            .Setup(c => c.TryGetSessionCookie(It.IsAny<HttpRequest>(), out any))
            .Returns(new TryGetSessionCookie((HttpRequest request, out Guid? sessionId) => real.TryGetSessionCookie(request, out sessionId)));
    }

    private delegate bool TryGetSessionCookie(HttpRequest request, out Guid? sessionId);

    private AuthController Controller(RecordingMediator mediator, Action<HttpContext>? configure = null, params string[] roles) =>
        new AuthController(mediator, _cookies.Object).WithRequest(SignedIn(roles), configure);

    /// <summary>Permission names as whoami lists them: sorted.</summary>
    private static string[] Names(IEnumerable<MedApp.Application.Common.Identity.Permission> permissions) =>
        permissions.Select(p => p.ToString()).Order(StringComparer.Ordinal).ToArray();

    private static IdentityResult Failed(string code = "Problem") => IdentityResult.Failed(new IdentityError { Code = code, Description = "Synthetic problem." });

    [Test]
    public async Task Login_ValidCredentials_StartsASessionAndSetsItsCookie()
    {
        var session = new LoginResult(SessionId, ExpiresAtUtc);
        var mediator = new RecordingMediator(_ => LoginOutcome.SignedIn(session));
        var controller = Controller(mediator, context =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(IpAddress);
            context.Request.Headers.UserAgent = UserAgent;
        });

        var result = await controller.Login(new LoginRequest(Username, Password));

        mediator.Request.Should().Be(new LoginCommand(Username, Password, IpAddress, UserAgent));
        _cookies.Verify(c => c.AppendSessionCookie(controller.Response, session), Times.Once);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(new LoginResponse(SessionId, ExpiresAtUtc));
    }

    [Test]
    public async Task Login_TwoFactorAccount_ReturnsTheChallengeWithoutASessionOrCookie()
    {
        var mediator = new RecordingMediator(_ => LoginOutcome.TwoFactorRequired(TwoFactorChallenge, ExpiresAtUtc));
        var controller = Controller(mediator);

        var result = await controller.Login(new LoginRequest(Username, Password));

        result.Should().BeOfType<AcceptedResult>().Which.Value.Should().Be(new TwoFactorChallengeResponse(TwoFactorChallenge, ExpiresAtUtc));
        _cookies.Verify(c => c.AppendSessionCookie(It.IsAny<HttpResponse>(), It.IsAny<LoginResult>()), Times.Never);
        controller.Response.Headers.CacheControl.ToString().Should().Be("no-store");
    }

    [Test]
    public async Task Login_EmailCodesAccount_SaysWhereTheCodeWentWithoutACookie()
    {
        var sent = new EmailCodeSentResponse("s•••@example.test", ExpiresAtUtc, 60, 3);
        var mediator = new RecordingMediator(_ =>
            LoginOutcome.TwoFactorRequired(TwoFactorChallenge, ExpiresAtUtc, MedApp.Domain.Authentication.TwoFactorMethod.Email, sent));
        var controller = Controller(mediator);

        var result = await controller.Login(new LoginRequest(Username, Password));

        result.Should().BeOfType<AcceptedResult>().Which.Value.Should()
            .Be(new TwoFactorChallengeResponse(TwoFactorChallenge, ExpiresAtUtc, "Email", sent));
        _cookies.Verify(c => c.AppendSessionCookie(It.IsAny<HttpResponse>(), It.IsAny<LoginResult>()), Times.Never);
    }

    [Test]
    public async Task CreateUser_WithAnEmail_SendsItOn()
    {
        var mediator = new RecordingMediator(_ => CreateUserResult.Created(Password));

        await Controller(mediator, null, "Admin").CreateUser(new CreateUserRequest(Username, Email));

        var command = mediator.Request.Should().BeOfType<CreateUserCommand>().Subject;
        command.Should().BeEquivalentTo(new { Username, Email });
        // The admin is recorded as who gave the address.
        command.ActorUserId.Should().NotBeNull();
        command.ActorUsername.Should().NotBeNullOrEmpty();
    }

    [Test]
    public void WhoAmI_AdminWhoMustSetUpTwoFactor_SaysSo()
    {
        var user = SignedIn("Admin");
        ((System.Security.Claims.ClaimsIdentity)user.Identity!).AddClaim(
            new System.Security.Claims.Claim(SessionAuthenticationDefaults.MustSetUpTwoFactorClaim, "true"));
        var controller = new AuthController(new RecordingMediator(), _cookies.Object).WithRequest(user);

        var result = controller.WhoAmI();

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<WhoAmIResponse>()
            .Which.MustSetUpTwoFactor.Should().BeTrue();
    }

    [Test]
    public void Login_IsThrottledPerClientAndPerUsername()
    {
        var login = typeof(AuthController).GetMethod(nameof(AuthController.Login))!;

        login.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName.Should().Be(LoginThrottling.Policy);
        login.GetCustomAttribute<ThrottleLoginByUsernameAttribute>().Should().NotBeNull();
        // A username and a password: Kestrel refuses a bigger body (413) before it's read, or counted.
        ((Microsoft.AspNetCore.Http.Metadata.IRequestSizeLimitMetadata)login.GetCustomAttribute<RequestSizeLimitAttribute>()!).MaxRequestBodySize.Should().Be(16 * 1024);
    }

    [Test]
    public async Task Logout_WithASessionCookie_EndsThatSessionAndClearsTheCookie()
    {
        var mediator = new RecordingMediator();
        Guid? sessionId = SessionId;
        _cookies.Setup(c => c.TryGetSessionCookie(It.IsAny<HttpRequest>(), out sessionId)).Returns(true);
        var controller = Controller(mediator);

        var result = await controller.Logout();

        mediator.Request.Should().Be(new LogoutCommand(SessionId));
        _cookies.Verify(c => c.DeleteSessionCookie(controller.Response), Times.Once);
        result.Should().BeOfType<NoContentResult>();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Logout_NoReadableSessionCookie_StillClearsTheCookie(bool hasCookie)
    {
        var mediator = new RecordingMediator();
        Guid? unreadable = null;
        _cookies.Setup(c => c.TryGetSessionCookie(It.IsAny<HttpRequest>(), out unreadable)).Returns(hasCookie);
        var controller = Controller(mediator);

        var result = await controller.Logout();

        mediator.Request.Should().Be(new LogoutCommand(null));
        _cookies.Verify(c => c.DeleteSessionCookie(controller.Response), Times.Once);
        result.Should().BeOfType<NoContentResult>();
    }

    [Test]
    public async Task GetSession_WithASessionCookie_ReturnsThatSessionsExpiryUncached()
    {
        var expiry = new SessionResponse(ExpiresAtUtc, 300, true);
        var mediator = new RecordingMediator(_ => expiry);
        var controller = Controller(mediator, context => context.Request.Headers.Cookie = $"{SessionAuthenticationDefaults.HostCookieName}={SessionId}");
        using var cts = new CancellationTokenSource();

        var result = await controller.GetSession(cts.Token);

        mediator.Request.Should().Be(new GetSessionQuery(SessionId));
        mediator.Tokens.Single().Should().Be(cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(expiry);
        controller.Response.Headers.CacheControl.ToString().Should().Be("no-store");
    }

    [Test]
    public async Task ExtendSession_WithASessionCookie_ExtendsItAndSendsTheCookieAgainWithTheNewExpiry()
    {
        var extended = new SessionResponse(ExpiresAtUtc, 8 * 60 * 60, true);
        var mediator = new RecordingMediator(_ => extended);
        var controller = Controller(mediator, context => context.Request.Headers.Cookie = $"{SessionAuthenticationDefaults.HostCookieName}={SessionId}");
        using var cts = new CancellationTokenSource();

        var result = await controller.ExtendSession(cts.Token);

        mediator.Request.Should().Be(new ExtendSessionCommand(SessionId));
        mediator.Tokens.Single().Should().Be(cts.Token);
        _cookies.Verify(c => c.AppendSessionCookie(controller.Response, new LoginResult(SessionId, ExpiresAtUtc)), Times.Once);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(extended);
        controller.Response.Headers.CacheControl.ToString().Should().Be("no-store");
    }

    [TestCase(null)]
    [TestCase("not-a-session-id")]
    public async Task GetSessionAndExtendSession_NoReadableSessionCookie_AreUnauthorized(string? cookie)
    {
        var mediator = new RecordingMediator();
        Action<HttpContext> configure = context =>
        {
            if (cookie is not null) context.Request.Headers.Cookie = $"{SessionAuthenticationDefaults.HostCookieName}={cookie}";
        };

        var got = await Controller(mediator, configure).GetSession(CancellationToken.None);
        var extended = await Controller(mediator, configure).ExtendSession(CancellationToken.None);

        got.Should().BeOfType<UnauthorizedResult>();
        extended.Should().BeOfType<UnauthorizedResult>();
        mediator.Sent.Should().BeEmpty();
        _cookies.Verify(c => c.AppendSessionCookie(It.IsAny<HttpResponse>(), It.IsAny<LoginResult>()), Times.Never);
    }

    [TestCase(nameof(AuthController.GetSession))]
    [TestCase(nameof(AuthController.ExtendSession))]
    public async Task GetSessionAndExtendSession_AnyoneSignedIn_IsAllowed_AndNoOneElse(string action)
    {
        (await MayCall(action, Username, "User")).Should().BeTrue();
        (await MayCall(action, Username)).Should().BeTrue();
        (await MayCall(action, Username, new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity()))).Should().BeFalse();
    }

    [Test]
    public async Task CreateUser_Succeeds_ReturnsTheUsernameAndGeneratedPassword()
    {
        var mediator = new RecordingMediator(_ => CreateUserResult.Created(Password));

        var result = await Controller(mediator).CreateUser(new CreateUserRequest(OtherUsername));

        mediator.Request.Should().BeOfType<CreateUserCommand>().Which.Should().BeEquivalentTo(new { Username = OtherUsername, Email = (string?)null });
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(new CreateUserResponse(OtherUsername, Password));
    }

    [Test]
    public async Task CreateUser_Succeeds_TellsCachesNotToStoreTheResponse()
    {
        var controller = Controller(new RecordingMediator(_ => CreateUserResult.Created(Password)));

        await controller.CreateUser(new CreateUserRequest(OtherUsername));

        controller.Response.Headers.CacheControl.ToString().Should().Be("no-store");
    }

    [Test]
    public async Task CreateUser_Fails_ReturnsBadRequestWithTheErrors()
    {
        var failed = Failed();
        var mediator = new RecordingMediator(_ => CreateUserResult.Failed(failed));

        var result = await Controller(mediator).CreateUser(new CreateUserRequest(OtherUsername));

        result.Should().BeOfType<BadRequestObjectResult>().Which.Value.Should().BeEquivalentTo(failed.Errors);
    }

    [Test]
    public async Task CreateUser_Admin_IsAllowed()
    {
        (await MayCall(nameof(AuthController.CreateUser), OtherUsername, "Admin", "User")).Should().BeTrue();
    }

    [Test]
    public async Task CreateUser_User_IsRefused()
    {
        (await MayCall(nameof(AuthController.CreateUser), OtherUsername, "User")).Should().BeFalse();
    }

    [Test]
    public async Task CreateUser_SignedInWithoutARole_IsRefused()
    {
        (await MayCall(nameof(AuthController.CreateUser), OtherUsername)).Should().BeFalse();
    }

    [Test]
    public async Task CreateUser_SignedOut_IsRefused()
    {
        (await MayCall(nameof(AuthController.CreateUser), OtherUsername, new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity()))).Should().BeFalse();
    }

    [Test]
    public async Task SearchUsers_Request_SendsTheSearchAndReturnsThePage()
    {
        var request = _fixture.Create<SearchUsersRequest>();
        var page = new PagedResponse<UserResponse>([], 1, 10, 0, 0);
        var mediator = new RecordingMediator(_ => page);
        using var cts = new CancellationTokenSource();

        var result = await Controller(mediator).SearchUsers(request, cts.Token);

        mediator.Request.Should().Be(new SearchUsersQuery(request.Page, request.PageSize, request.Sort, request.Ascending, request.Username, request.Role));
        mediator.Tokens.Single().Should().Be(cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(page);
    }

    [Test]
    public async Task GetAllUsers_ReturnsThem()
    {
        IReadOnlyList<UserResponse> users = [new UserResponse(UserId, Username, ["Admin"])];
        var mediator = new RecordingMediator(_ => users);

        var result = await Controller(mediator).GetAllUsers();

        mediator.Request.Should().BeOfType<GetAllUsersQuery>();
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(users);
    }

    [Test]
    public async Task GetUserByUsername_Found_ReturnsThem()
    {
        var user = new UserResponse(UserId, Username, ["Admin"]);
        var mediator = new RecordingMediator(_ => user);

        var result = await Controller(mediator).GetUserByUsername(Username);

        mediator.Request.Should().Be(new GetUserByUsernameQuery(Username));
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeEquivalentTo(user);
    }

    [Test]
    public async Task GetUserByUsername_NotFound_ReturnsNotFound()
    {
        var result = await Controller(new RecordingMediator()).GetUserByUsername(Username);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Test]
    public async Task UpdateUserPassword_Succeeds_ReturnsNoContent()
    {
        var mediator = new RecordingMediator(_ => IdentityResult.Success);

        var result = await Controller(mediator).UpdateUserPassword(Username, new UpdateUserPasswordRequest(Password, "New-" + Password));

        mediator.Request.Should().Be(new UpdateUserPasswordCommand(Username, Password, "New-" + Password));
        result.Should().BeOfType<NoContentResult>();
    }

    [Test]
    public async Task UpdateUserPassword_FromASession_SendsItSoItCarriesOn()
    {
        var mediator = new RecordingMediator(_ => IdentityResult.Success);
        var controller = new AuthController(mediator, _cookies.Object).WithRequest(SignedInWithSession("User"));

        await controller.UpdateUserPassword(Username, new UpdateUserPasswordRequest(Password, "New-" + Password));

        mediator.Request.Should().Be(new UpdateUserPasswordCommand(Username, Password, "New-" + Password, SessionId));
    }

    [Test]
    public async Task UpdateUserPassword_Fails_ReturnsBadRequest()
    {
        var mediator = new RecordingMediator(_ => Failed(IdentityErrors.OldPasswordIncorrectCode));

        var result = await Controller(mediator).UpdateUserPassword(Username, new UpdateUserPasswordRequest(Password, "New-" + Password));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Test]
    public async Task ResetUserPassword_Succeeds_ReturnsTheNewPassword()
    {
        var mediator = new RecordingMediator(_ => new ResetUserPasswordResponse(Password, []));

        var result = await Controller(mediator).ResetUserPassword(OtherUsername);

        mediator.Request.Should().Be(new ResetUserPasswordCommand(OtherUsername));
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(Password);
    }

    [Test]
    public async Task ResetUserPassword_Succeeds_TellsCachesNotToStoreTheResponse()
    {
        var controller = Controller(new RecordingMediator(_ => new ResetUserPasswordResponse(Password, [])));

        await controller.ResetUserPassword(OtherUsername);

        controller.Response.Headers.CacheControl.ToString().Should().Be("no-store");
    }

    [Test]
    public async Task ResetUserPassword_Fails_ReturnsBadRequestWithTheErrors()
    {
        var mediator = new RecordingMediator(_ => new ResetUserPasswordResponse(null, [IdentityErrors.UserNotFound]));

        var result = await Controller(mediator).ResetUserPassword(OtherUsername);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Test]
    public async Task SignOutEverywhere_UserFound_ReturnsNoContent()
    {
        var mediator = new RecordingMediator(_ => true);
        using var cts = new CancellationTokenSource();

        var result = await Controller(mediator).SignOutEverywhere(OtherUsername, cts.Token);

        mediator.Request.Should().Be(new SignOutEverywhereCommand(OtherUsername));
        mediator.Tokens.Single().Should().Be(cts.Token);
        result.Should().BeOfType<NoContentResult>();
    }

    [Test]
    public async Task SignOutEverywhere_UserNotFound_ReturnsNotFound()
    {
        var result = await Controller(new RecordingMediator(_ => false)).SignOutEverywhere(OtherUsername, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Test]
    public async Task SignOutEverywhere_Admin_IsAllowed()
    {
        (await MayCall(nameof(AuthController.SignOutEverywhere), OtherUsername, "Admin")).Should().BeTrue();
    }

    [Test]
    public async Task SignOutEverywhere_NotAdmin_IsRefused()
    {
        (await MayCall(nameof(AuthController.SignOutEverywhere), OtherUsername, "User")).Should().BeFalse();
        (await MayCall(nameof(AuthController.SignOutEverywhere), OtherUsername)).Should().BeFalse();
    }

    [Test]
    public async Task DeleteUser_Succeeds_SendsTheAdminsNameAndPasswordAndReturnsNoContent()
    {
        var mediator = new RecordingMediator(_ => IdentityResult.Success);

        var result = await Controller(mediator).DeleteUser(OtherUsername, new DeleteUserRequest(Password));

        mediator.Request.Should().Be(new DeleteUserCommand(OtherUsername, Username, Password));
        result.Should().BeOfType<NoContentResult>();
    }

    [Test]
    public async Task DeleteUser_UserNotFound_ReturnsNotFound()
    {
        var mediator = new RecordingMediator(_ => IdentityResult.Failed(IdentityErrors.UserNotFound));

        var result = await Controller(mediator).DeleteUser(OtherUsername, new DeleteUserRequest(Password));

        result.Should().BeOfType<NotFoundResult>();
    }

    [Test]
    public async Task DeleteUser_WrongPasswordOrNone_ReturnsBadRequest()
    {
        var mediator = new RecordingMediator(_ => IdentityResult.Failed(IdentityErrors.PasswordIncorrect));

        var result = await Controller(mediator).DeleteUser(OtherUsername, new DeleteUserRequest(null));

        mediator.Request.Should().Be(new DeleteUserCommand(OtherUsername, Username, string.Empty));
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Test]
    public async Task DeleteUser_HasUpcomingAppointments_ReturnsBadRequestWithTheError()
    {
        var error = IdentityErrors.HasUpcomingAppointments(OtherUsername, 2);
        var mediator = new RecordingMediator(_ => IdentityResult.Failed(error));

        var result = await Controller(mediator).DeleteUser(OtherUsername, new DeleteUserRequest(Password));

        result.Should().BeOfType<BadRequestObjectResult>().Which.Value.Should().BeEquivalentTo(new[] { error });
        error.Code.Should().Be(IdentityErrors.HasUpcomingAppointmentsCode);
    }

    [Test]
    public async Task GetAllRoles_ReturnsThem()
    {
        IReadOnlyList<string> roles = ["Admin", "User"];
        var mediator = new RecordingMediator(_ => roles);

        var result = await Controller(mediator).GetAllRoles();

        mediator.Request.Should().BeOfType<GetAllRolesQuery>();
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(roles);
    }

    [Test]
    public async Task GetUserRoles_ReturnsThem()
    {
        IReadOnlyList<string> roles = ["User"];
        var mediator = new RecordingMediator(_ => roles);

        var result = await Controller(mediator).GetUserRoles(OtherUsername);

        mediator.Request.Should().Be(new GetUserRolesQuery(OtherUsername));
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(roles);
    }

    [Test]
    public async Task CreateRole_Created_ReturnsNoContent_AndBadRequestWhenNot()
    {
        var created = await Controller(new RecordingMediator(_ => new IdentityRole<Guid>("Auditor"))).CreateRole(new CreateRoleRequest("Auditor"));
        var refused = await Controller(new RecordingMediator()).CreateRole(new CreateRoleRequest("Auditor"));

        created.Should().BeOfType<NoContentResult>();
        refused.Should().BeOfType<BadRequestResult>();
    }

    [Test]
    public async Task AssignRole_Assigned_ReturnsNoContent_AndBadRequestWhenNot()
    {
        var mediator = new RecordingMediator(_ => new IdentityRole<Guid>("Admin"));

        var assigned = await Controller(mediator).AssignRole(new AssignRoleRequest(OtherUsername, "Admin"));
        var refused = await Controller(new RecordingMediator()).AssignRole(new AssignRoleRequest(OtherUsername, "Admin"));

        mediator.Request.Should().Be(new AssignRoleCommand(OtherUsername, "Admin"));
        assigned.Should().BeOfType<NoContentResult>();
        refused.Should().BeOfType<BadRequestResult>();
    }

    [Test]
    public async Task RevokeRole_Revoked_ReturnsNoContent_AndBadRequestWhenNot()
    {
        var mediator = new RecordingMediator(_ => new IdentityRole<Guid>("Admin"));

        var revoked = await Controller(mediator).RevokeRole(new RevokeRoleRequest(OtherUsername, "Admin"));
        var refused = await Controller(new RecordingMediator()).RevokeRole(new RevokeRoleRequest(OtherUsername, "Admin"));

        mediator.Request.Should().Be(new RevokeRoleCommand(OtherUsername, "Admin", Username));
        revoked.Should().BeOfType<NoContentResult>();
        refused.Should().BeOfType<BadRequestResult>();
    }

    [Test]
    public async Task SetUserRoles_Succeeds_SendsTheRolesAndTheAdminAndReturnsNoContent()
    {
        var mediator = new RecordingMediator(_ => IdentityResult.Success);

        var result = await Controller(mediator).SetUserRoles(OtherUsername, new SetUserRolesRequest(["User"]));

        var sent = mediator.Request.Should().BeOfType<SetUserRolesCommand>().Subject;
        sent.Username.Should().Be(OtherUsername);
        sent.Roles.Should().Equal("User");
        sent.ActorUsername.Should().Be(Username);
        result.Should().BeOfType<NoContentResult>();
    }

    [Test]
    public async Task SetUserRoles_NoRolesGiven_SendsNone()
    {
        var mediator = new RecordingMediator(_ => IdentityResult.Success);

        await Controller(mediator).SetUserRoles(OtherUsername, new SetUserRolesRequest(null));

        mediator.Request.Should().BeOfType<SetUserRolesCommand>().Which.Roles.Should().BeEmpty();
    }

    [Test]
    public async Task SetUserRoles_UserNotFound_ReturnsNotFound_AndOtherFailuresBadRequest()
    {
        var notFound = await Controller(new RecordingMediator(_ => IdentityResult.Failed(IdentityErrors.UserNotFound)))
            .SetUserRoles(OtherUsername, new SetUserRolesRequest(["User"]));
        var ownRoles = await Controller(new RecordingMediator(_ => IdentityResult.Failed(IdentityErrors.OwnRoles)))
            .SetUserRoles(Username, new SetUserRolesRequest(["User"]));

        notFound.Should().BeOfType<NotFoundResult>();
        ownRoles.Should().BeOfType<BadRequestObjectResult>();
    }

    [Test]
    public async Task DeleteRole_Deleted_ReturnsNoContent_AndNotFoundWhenNot()
    {
        var mediator = new RecordingMediator(_ => IdentityResult.Success);

        var deleted = await Controller(mediator).DeleteRole("Auditor");
        var missing = await Controller(new RecordingMediator(_ => Failed())).DeleteRole("Auditor");

        mediator.Request.Should().Be(new DeleteRoleCommand("Auditor"));
        deleted.Should().BeOfType<NoContentResult>();
        missing.Should().BeOfType<NotFoundResult>();
    }

    [Test]
    public async Task GetUserSpecialties_ReturnsThem()
    {
        IReadOnlyList<StaffSpecialtyResponse> specialties = [new StaffSpecialtyResponse { Id = RecordId, Name = "Synthetic dermatology" }];
        var mediator = new RecordingMediator(_ => specialties);
        using var cts = new CancellationTokenSource();

        var result = await Controller(mediator).GetUserSpecialties(OtherUsername, cts.Token);

        mediator.Request.Should().Be(new GetUserSpecialtiesQuery(OtherUsername));
        mediator.Tokens.Single().Should().Be(cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(specialties);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task SetUserSpecialties_SendsTheSpecialtiesOrNoneAndReturnsThem(bool given)
    {
        IReadOnlyList<StaffSpecialtyResponse> specialties = [];
        var mediator = new RecordingMediator(_ => specialties);
        using var cts = new CancellationTokenSource();

        var result = await Controller(mediator).SetUserSpecialties(
            OtherUsername, new SetUserSpecialtiesRequest(given ? [RecordId, OtherId] : null), cts.Token);

        var sent = mediator.Request.Should().BeOfType<SetUserSpecialtiesCommand>().Subject;
        sent.Username.Should().Be(OtherUsername);
        sent.SpecialtyIds.Should().Equal(given ? [RecordId, OtherId] : []);
        mediator.Tokens.Single().Should().Be(cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(specialties);
    }

    [Test]
    public async Task GetMySpecialties_ReturnsTheSignedInUsers()
    {
        IReadOnlyList<StaffSpecialtyResponse> specialties = [];
        var mediator = new RecordingMediator(_ => specialties);
        using var cts = new CancellationTokenSource();

        var result = await Controller(mediator).GetMySpecialties(cts.Token);

        mediator.Request.Should().Be(new GetMySpecialtiesQuery(UserId));
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(specialties);
    }

    [Test]
    public async Task SpecialtyMemberships_AdminsSetThemAndAnyoneReadsTheirOwn()
    {
        foreach (var action in new[] { nameof(AuthController.GetUserSpecialties), nameof(AuthController.SetUserSpecialties) })
        {
            (await ControllerAuthorisation.MayCallAsync<AuthController>(action, SignedIn("User"))).Should().BeFalse(action);
            (await ControllerAuthorisation.MayCallAsync<AuthController>(action, SignedIn("Admin"))).Should().BeTrue(action);
        }

        (await ControllerAuthorisation.MayCallAsync<AuthController>(nameof(AuthController.GetMySpecialties), SignedIn("User"))).Should().BeTrue();
        (await ControllerAuthorisation.MayCallAsync<AuthController>(nameof(AuthController.GetMySpecialties), SignedIn())).Should().BeFalse();
    }

    [Test]
    public async Task GetMyDetails_AsksForTheSignedInUsers()
    {
        var details = new StaffDetailsResponse { FullName = "Sam Synthetic", JobTitle = "Nurse" };
        var mediator = new RecordingMediator(_ => details);
        using var cts = new CancellationTokenSource();

        var result = await Controller(mediator).GetMyDetails(cts.Token);

        mediator.Request.Should().Be(new GetStaffDetailsQuery(Username));
        mediator.Tokens.Single().Should().Be(cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(details);
    }

    [Test]
    public async Task SetMyDetails_SetsTheSignedInUsers_AndSetUserDetails_TheNamedAccounts()
    {
        var details = new StaffDetailsResponse { FullName = "Sam Synthetic" };
        var request = new StaffDetailsRequest { FullName = "Sam Synthetic", JobTitle = null };
        using var cts = new CancellationTokenSource();

        var mine = new RecordingMediator(_ => details);
        var mineResult = await Controller(mine).SetMyDetails(request, cts.Token);
        var theirs = new RecordingMediator(_ => details);
        var theirsResult = await Controller(theirs).SetUserDetails(OtherUsername, request, cts.Token);

        mine.Request.Should().Be(new SetStaffDetailsCommand(Username, "Sam Synthetic", null));
        theirs.Request.Should().Be(new SetStaffDetailsCommand(OtherUsername, "Sam Synthetic", null));
        mine.Tokens.Single().Should().Be(cts.Token);
        mineResult.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(details);
        theirsResult.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(details);
    }

    [Test]
    public async Task StaffDetails_AnyoneSetsTheirOwn_ManageUsersSetsAnyones()
    {
        foreach (var action in new[] { nameof(AuthController.GetMyDetails), nameof(AuthController.SetMyDetails) })
        {
            (await ControllerAuthorisation.MayCallAsync<AuthController>(action, SignedIn("User"))).Should().BeTrue(action);
            (await ControllerAuthorisation.MayCallAsync<AuthController>(action, SignedIn())).Should().BeFalse(action);
        }

        (await ControllerAuthorisation.MayCallAsync<AuthController>(nameof(AuthController.SetUserDetails), SignedIn("User"))).Should().BeFalse();
        (await ControllerAuthorisation.MayCallAsync<AuthController>(nameof(AuthController.SetUserDetails), SignedIn("Admin"))).Should().BeTrue();
    }

    [Test]
    public async Task GetMySettings_ReturnsTheSignedInUsersSettings()
    {
        var settings = new UserSettingsResponse("{}", ExpiresAtUtc);
        var mediator = new RecordingMediator(_ => settings);
        using var cts = new CancellationTokenSource();

        var result = await Controller(mediator).GetMySettings(cts.Token);

        mediator.Request.Should().Be(new GetMySettingsQuery(UserId));
        mediator.Tokens.Single().Should().Be(cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(settings);
    }

    [TestCase("{\"medapp.colorMode\":\"light\"}", "{\"medapp.colorMode\":\"light\"}")]
    [TestCase(null, "")]
    public async Task SaveMySettings_SavesThemForTheSignedInUser(string? settings, string expected)
    {
        var mediator = new RecordingMediator();

        var result = await Controller(mediator).SaveMySettings(new SaveSettingsRequest(settings), CancellationToken.None);

        mediator.Request.Should().Be(new SaveMySettingsCommand(UserId, expected));
        result.Should().BeOfType<NoContentResult>();
    }

    [Test]
    public void WhoAmI_SignedIn_ReturnsTheirNameAndRoles()
    {
        var result = Controller(new RecordingMediator(), roles: ["Admin", "User"]).WhoAmI();

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeEquivalentTo(new WhoAmIResponse(true, Username, ["Admin", "User"], false, Permissions: Names(TestPermissions.ForRoles("Admin", "User"))));
    }

    [Test]
    public void WhoAmI_MustChangePassword_SaysSo()
    {
        var controller = new AuthController(new RecordingMediator(), _cookies.Object).WithRequest(MustChangePassword("User"));

        var result = controller.WhoAmI();

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeEquivalentTo(new WhoAmIResponse(true, Username, ["User"], true, Permissions: Names(TestPermissions.Staff)));
    }

    [Test]
    public void WhoAmI_SignedOut_SaysSo()
    {
        var controller = new AuthController(new RecordingMediator(), _cookies.Object).WithRequest();

        var result = controller.WhoAmI();

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeEquivalentTo(new WhoAmIResponse(false, null, [], false, Permissions: []));
    }

    [Test]
    public async Task Login_NoClientAddress_SendsNone()
    {
        var mediator = new RecordingMediator(_ => LoginOutcome.SignedIn(new LoginResult(SessionId, ExpiresAtUtc)));

        await Controller(mediator).Login(new LoginRequest(Username, Password));

        mediator.Request.Should().Be(new LoginCommand(Username, Password, null, string.Empty));
    }

    [Test]
    public async Task DeleteUserAndSetUserRoles_SignedInWithoutAName_SendAnEmptyName()
    {
        var nameless = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity("Session"));
        var deleting = new RecordingMediator(_ => IdentityResult.Success);
        var setting = new RecordingMediator(_ => IdentityResult.Success);

        await new AuthController(deleting, _cookies.Object).WithRequest(nameless).DeleteUser(OtherUsername, new DeleteUserRequest(Password));
        await new AuthController(setting, _cookies.Object).WithRequest(nameless).SetUserRoles(OtherUsername, new SetUserRolesRequest(["User"]));

        deleting.Request.Should().Be(new DeleteUserCommand(OtherUsername, string.Empty, Password));
        setting.Request.Should().BeOfType<SetUserRolesCommand>().Which.ActorUsername.Should().BeEmpty();
    }

    [TestCase("Admin")]
    [TestCase("User")]
    [TestCase("Admin", "User")]
    public async Task UpdateUserPassword_StaffChangingTheirOwn_IsAllowed(params string[] roles)
    {
        (await MayUpdatePassword(Username, roles)).Should().BeTrue();
    }

    [Test]
    public async Task UpdateUserPassword_ChangingSomeoneElses_IsRefused()
    {
        (await MayUpdatePassword(OtherUsername, "Admin", "User")).Should().BeFalse();
    }

    [Test]
    public async Task UpdateUserPassword_NotStaff_IsRefused()
    {
        (await MayUpdatePassword(Username)).Should().BeFalse();
    }

    private static Task<bool> MayUpdatePassword(string routeUsername, params string[] roles) =>
        MayCall(nameof(AuthController.UpdateUserPassword), routeUsername, roles);

    private static Task<bool> MayCall(string action, string routeUsername, params string[] roles) =>
        MayCall(action, routeUsername, SignedIn(roles));

    /// <summary>Whether the action's own [Authorize] attributes, with the API's policies, let this user in.</summary>
    private static async Task<bool> MayCall(string action, string routeUsername, System.Security.Claims.ClaimsPrincipal user)
    {
        await using var services = new ServiceCollection().AddLogging().AddApi(new ConfigurationBuilder().Build()).BuildServiceProvider();
        var attributes = typeof(AuthController).GetCustomAttributes<AuthorizeAttribute>()
            .Concat(typeof(AuthController).GetMethod(action)!.GetCustomAttributes<AuthorizeAttribute>());
        var policy = await AuthorizationPolicy.CombineAsync(services.GetRequiredService<IAuthorizationPolicyProvider>(), attributes);
        var request = new DefaultHttpContext();
        request.Request.RouteValues["username"] = routeUsername;

        var result = await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, request, policy!);
        return result.Succeeded;
    }

    [Test]
    public void WhoAmI_NoIdentityAtAll_SaysSignedOut()
    {
        var controller = new AuthController(new RecordingMediator(), _cookies.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new System.Security.Claims.ClaimsPrincipal() } };

        controller.WhoAmI().Should().BeOfType<OkObjectResult>().Which.Value.Should().BeEquivalentTo(new WhoAmIResponse(false, null, [], false, Permissions: []));
    }
}
