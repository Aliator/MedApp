using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using MedApp.API.Common.Authentication;
using MedApp.Application.Common.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using static MedApp.UnitTests.Common.Constants.ApiTestConstants;

namespace MedApp.UnitTests.Api.Authentication;

[TestFixture]
public sealed class PermissionPoliciesTests
{
    private static PermissionPolicies Provider(Action<AuthorizationOptions>? configure = null)
    {
        var options = new AuthorizationOptions();
        configure?.Invoke(options);
        return new PermissionPolicies(Options.Create(options));
    }

    [Test]
    public void Name_IsThePrefixAndThePermission()
    {
        PermissionPolicies.Name(Permission.ViewPatients).Should().Be("Permission:ViewPatients");
        new RequirePermissionAttribute(Permission.ManageUsers).Policy.Should().Be("Permission:ManageUsers");
    }

    [Test]
    public async Task GetPolicyAsync_APermission_NeedsASignedInUserAndThePermission()
    {
        var policy = await Provider().GetPolicyAsync(PermissionPolicies.Name(Permission.WorkPathways));

        policy!.Requirements.OfType<PermissionRequirement>().Should().ContainSingle().Which.Permission.Should().Be(Permission.WorkPathways);
        policy.Requirements.Should().ContainSingle(r => r is Microsoft.AspNetCore.Authorization.Infrastructure.DenyAnonymousAuthorizationRequirement);
    }

    [Test]
    public async Task GetPolicyAsync_AnUnknownPermission_FallsThroughToNone()
    {
        (await Provider().GetPolicyAsync("Permission:SeeEverything")).Should().BeNull();
    }

    [Test]
    public async Task GetPolicyAsync_AnotherPolicy_IsTheRegisteredOne()
    {
        var registered = new AuthorizationPolicyBuilder().RequireAssertion(_ => true).Build();

        (await Provider(o => o.AddPolicy(AuthPolicies.SelfUserOnly, registered)).GetPolicyAsync(AuthPolicies.SelfUserOnly))
            .Should().BeSameAs(registered);
    }

    [Test]
    public void PermissionRequirement_DescribesItself()
    {
        new PermissionRequirement(Permission.ViewAudit).ToString().Should().Be("Permission ViewAudit");
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Handler_PassesOnlyWithThePermissionClaim(bool has)
    {
        var requirement = new PermissionRequirement(Permission.ViewInsights);
        var user = has ? SignedInWith(Permission.ViewInsights) : SignedInWith(Permission.UseApp);
        var context = new AuthorizationHandlerContext([requirement], user, null);

        await new PermissionRequirementHandler().HandleAsync(context);

        context.HasSucceeded.Should().Be(has);
    }
}

[TestFixture]
public sealed class ApiAuthorizationResultHandlerTests
{
    private static readonly AuthorizationPolicy AnyPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

    private static DefaultHttpContext Context(string path)
    {
        var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<JsonElement> BodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return (await JsonDocument.ParseAsync(context.Response.Body)).RootElement;
    }

    [Test]
    public async Task MissingPermission_Is403_NamingItAndWhatItAllows()
    {
        var context = Context("/api/patients/search");
        var failure = AuthorizationFailure.Failed([new PermissionRequirement(Permission.ViewPatients)]);

        await new ApiAuthorizationResultHandler().HandleAsync(_ => Task.CompletedTask, context, AnyPolicy, PolicyAuthorizationResult.Forbid(failure));

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        context.Response.ContentType.Should().StartWith("application/problem+json");
        var body = await BodyAsync(context);
        body.GetProperty("code").GetString().Should().Be(ApiAuthorizationResultHandler.PermissionMissingCode);
        body.GetProperty("permission").GetString().Should().Be("ViewPatients");
        body.GetProperty("detail").GetString().Should().Contain(Permissions.Describe(Permission.ViewPatients).Description);
    }

    [Test]
    public async Task FhirPath_IsAnOperationOutcome()
    {
        var context = Context("/api/fhir/Patient");
        var failure = AuthorizationFailure.Failed([new PermissionRequirement(Permission.ViewPatients)]);

        await new ApiAuthorizationResultHandler().HandleAsync(_ => Task.CompletedTask, context, AnyPolicy, PolicyAuthorizationResult.Forbid(failure));

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        (await BodyAsync(context)).GetProperty("resourceType").GetString().Should().Be("OperationOutcome");
    }

    [Test]
    public async Task Allowed_RunsTheRequest()
    {
        var context = Context("/api/patients/search");
        var ran = false;

        await new ApiAuthorizationResultHandler().HandleAsync(_ => { ran = true; return Task.CompletedTask; }, context, AnyPolicy, PolicyAuthorizationResult.Success());

        ran.Should().BeTrue();
    }

    [Test]
    public async Task ForbiddenForAnotherReason_IsThePlain403()
    {
        var context = Context("/api/auth/users/someone/update-password");
        context.RequestServices = new ServiceCollection()
            .AddLogging()
            .AddAuthentication(o => o.DefaultScheme = "Test").AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, NoopHandler>("Test", _ => { })
            .Services.BuildServiceProvider();
        var failure = AuthorizationFailure.Failed([new Microsoft.AspNetCore.Authorization.Infrastructure.AssertionRequirement(_ => false)]);

        await new ApiAuthorizationResultHandler().HandleAsync(_ => Task.CompletedTask, context, AnyPolicy, PolicyAuthorizationResult.Forbid(failure));

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        context.Response.Body.Length.Should().Be(0);
    }

    private sealed class NoopHandler(
        IOptionsMonitor<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions> options,
        Microsoft.Extensions.Logging.ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder)
        : Microsoft.AspNetCore.Authentication.AuthenticationHandler<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<Microsoft.AspNetCore.Authentication.AuthenticateResult> HandleAuthenticateAsync() =>
            Task.FromResult(Microsoft.AspNetCore.Authentication.AuthenticateResult.NoResult());
    }
}
