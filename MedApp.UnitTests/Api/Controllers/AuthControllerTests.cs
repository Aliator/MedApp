using System.Net;
using System.Reflection;
using AutoFixture;
using FluentAssertions;
using MedApp.API.Common.Authentication;
using MedApp.API.Common.Extensions;
using MedApp.API.Controllers;
using MedApp.Application.Authentication.Roles.Commands.AssignRole;
using MedApp.Application.Authentication.Roles.Commands.CreateRole;
using MedApp.Application.Authentication.Roles.Commands.DeleteRole;
using MedApp.Application.Authentication.Roles.Commands.RevokeRole;
using MedApp.Application.Authentication.Roles.Commands.SetUserRoles;
using MedApp.Application.Authentication.Roles.Queries.GetAllRoles;
using MedApp.Application.Authentication.Roles.Queries.GetUserRoles;
using MedApp.Application.Authentication.Sessions.Commands.Login;
using MedApp.Application.Authentication.Sessions.Commands.Logout;
using MedApp.Application.Authentication.Settings.Commands.SaveMySettings;
using MedApp.Application.Authentication.Settings.Queries.GetMySettings;
using MedApp.Application.Authentication.Users.Commands.CreateUser;
using MedApp.Application.Authentication.Users.Commands.DeleteUser;
using MedApp.Application.Authentication.Users.Commands.ResetUserPassword;
using MedApp.Application.Authentication.Users.Commands.UpdateUserPassword;
using MedApp.Application.Authentication.Users.Queries.GetAllUsers;
using MedApp.Application.Authentication.Users.Queries.GetUserByUsername;
using MedApp.Application.Authentication.Users.Queries.SearchUsers;
using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Authentication.Requests;
using MedApp.Contracts.Authentication.Responses;
using MedApp.Contracts.Common;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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
    }

    private AuthController Controller(RecordingMediator mediator, Action<HttpContext>? configure = null, params string[] roles) =>
        new AuthController(mediator, _cookies.Object).WithRequest(SignedIn(roles), configure);

    private static IdentityResult Failed(string code = "Problem") => IdentityResult.Failed(new IdentityError { Code = code, Description = "Synthetic problem." });

    [Test]
    public async Task Login_ValidCredentials_StartsASessionAndSetsItsCookie()
    {
        var session = new LoginResult(SessionId, ExpiresAtUtc);
        var mediator = new RecordingMediator(_ => session);
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
    public async Task Logout_WithASessionCookie_EndsThatSessionAndClearsTheCookie()
    {
        var mediator = new RecordingMediator();
        var controller = Controller(mediator, context => context.Request.Headers.Cookie = $"{SessionAuthenticationDefaults.CookieName}={SessionId}");

        var result = await controller.Logout();

        mediator.Request.Should().Be(new LogoutCommand(SessionId));
        _cookies.Verify(c => c.DeleteSessionCookie(controller.Response), Times.Once);
        result.Should().BeOfType<NoContentResult>();
    }

    [TestCase(null)]
    [TestCase("not-a-session-id")]
    public async Task Logout_NoReadableSessionCookie_StillClearsTheCookie(string? cookie)
    {
        var mediator = new RecordingMediator();
        var controller = Controller(mediator, context =>
        {
            if (cookie is not null) context.Request.Headers.Cookie = $"{SessionAuthenticationDefaults.CookieName}={cookie}";
        });

        var result = await controller.Logout();

        mediator.Request.Should().Be(new LogoutCommand(null));
        _cookies.Verify(c => c.DeleteSessionCookie(controller.Response), Times.Once);
        result.Should().BeOfType<NoContentResult>();
    }

    [Test]
    public async Task CreateUser_Succeeds_ReturnsNoContent()
    {
        var mediator = new RecordingMediator(_ => IdentityResult.Success);

        var result = await Controller(mediator).CreateUser(new CreateUserRequest(OtherUsername, Password));

        mediator.Request.Should().Be(new CreateUserCommand(OtherUsername, Password));
        result.Should().BeOfType<NoContentResult>();
    }

    [Test]
    public async Task CreateUser_Fails_ReturnsBadRequestWithTheErrors()
    {
        var failed = Failed();
        var mediator = new RecordingMediator(_ => failed);

        var result = await Controller(mediator).CreateUser(new CreateUserRequest(OtherUsername, Password));

        result.Should().BeOfType<BadRequestObjectResult>().Which.Value.Should().BeEquivalentTo(failed.Errors);
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
    public async Task ResetUserPassword_Fails_ReturnsBadRequestWithTheErrors()
    {
        var mediator = new RecordingMediator(_ => new ResetUserPasswordResponse(null, [IdentityErrors.UserNotFound]));

        var result = await Controller(mediator).ResetUserPassword(OtherUsername);

        result.Should().BeOfType<BadRequestObjectResult>();
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

        mediator.Request.Should().Be(new RevokeRoleCommand(OtherUsername, "Admin"));
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

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeEquivalentTo(new WhoAmIResponse(true, Username, ["Admin", "User"]));
    }

    [Test]
    public void WhoAmI_SignedOut_SaysSo()
    {
        var controller = new AuthController(new RecordingMediator(), _cookies.Object).WithRequest();

        var result = controller.WhoAmI();

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeEquivalentTo(new WhoAmIResponse(false, null, []));
    }

    [Test]
    public async Task Login_NoClientAddress_SendsNone()
    {
        var mediator = new RecordingMediator(_ => new LoginResult(SessionId, ExpiresAtUtc));

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

    /// <summary>Whether the action's own [Authorize] attributes, with the API's policies, let this user in.</summary>
    private static async Task<bool> MayUpdatePassword(string routeUsername, params string[] roles)
    {
        await using var services = new ServiceCollection().AddLogging().AddApi(new ConfigurationBuilder().Build()).BuildServiceProvider();
        var attributes = typeof(AuthController).GetCustomAttributes<AuthorizeAttribute>()
            .Concat(typeof(AuthController).GetMethod(nameof(AuthController.UpdateUserPassword))!.GetCustomAttributes<AuthorizeAttribute>());
        var policy = await AuthorizationPolicy.CombineAsync(services.GetRequiredService<IAuthorizationPolicyProvider>(), attributes);
        var request = new DefaultHttpContext();
        request.Request.RouteValues["username"] = routeUsername;

        var result = await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(SignedIn(roles), request, policy!);
        return result.Succeeded;
    }

    [Test]
    public void WhoAmI_NoIdentityAtAll_SaysSignedOut()
    {
        var controller = new AuthController(new RecordingMediator(), _cookies.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new System.Security.Claims.ClaimsPrincipal() } };

        controller.WhoAmI().Should().BeOfType<OkObjectResult>().Which.Value.Should().BeEquivalentTo(new WhoAmIResponse(false, null, []));
    }
}
