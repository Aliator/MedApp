using System.Text.Json;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using MedApp.API.Controllers;
using MedApp.API.Fhir;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Fhir.Common;
using MedApp.Application.Fhir.Queries.GetFhirPatient;
using MedApp.Application.Fhir.Queries.SearchFhirPatients;
using MedApp.Contracts.Fhir;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using static MedApp.UnitTests.Common.Constants.ApiTestConstants;

namespace MedApp.UnitTests.Api.Fhir;

/// <summary>The FHIR API's controller and how it writes its resources and errors.</summary>
[TestFixture]
public sealed class FhirApiTests
{
    private static DefaultHttpContext NewContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.RequestServices = new ServiceCollection().BuildServiceProvider();
        return context;
    }

    private static JsonElement Body(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return JsonDocument.Parse(context.Response.Body).RootElement;
    }

    [Test]
    public async Task ReadPatient_SendsTheIdAndReturnsTheResource()
    {
        var resource = new FhirPatient { Id = RecordId.ToString() };
        var mediator = new RecordingMediator(_ => resource);
        using var cts = new CancellationTokenSource();

        var result = await new FhirController(mediator).WithRequest(SignedIn("User")).ReadPatient("abc", cts.Token);

        mediator.Request.Should().Be(new GetFhirPatientQuery("abc"));
        mediator.Tokens.Should().Equal(cts.Token);
        result.Should().BeOfType<FhirResult<FhirPatient>>().Which.Resource.Should().BeSameAs(resource);
    }

    [Test]
    public async Task SearchPatients_SendsTheIdentifierAndTheApisAddress()
    {
        var bundle = new FhirBundle();
        var mediator = new RecordingMediator(_ => bundle);
        var controller = new FhirController(mediator).WithRequest(SignedIn("User"), context =>
        {
            context.Request.Scheme = "https";
            context.Request.Host = new HostString("medapp.example");
        });

        var result = await controller.SearchPatients("x|1", CancellationToken.None);

        mediator.Request.Should().Be(new SearchFhirPatientsQuery("x|1", "https://medapp.example/api/fhir"));
        result.Should().BeOfType<FhirResult<FhirBundle>>().Which.Resource.Should().BeSameAs(bundle);
    }

    [Test]
    public async Task FhirResult_WritesFhirJsonWithoutNulls_NeverCached()
    {
        var context = NewContext();
        var patient = new FhirPatient { Id = "1", BirthDate = "1980-05-17" };

        await new FhirResult<FhirPatient>(patient).ExecuteResultAsync(new ActionContext(context, new RouteData(), new ActionDescriptor()));

        context.Response.StatusCode.Should().Be(200);
        context.Response.ContentType.Should().StartWith("application/fhir+json");
        context.Response.Headers.CacheControl.ToString().Should().Be("no-store");
        var json = Body(context);
        json.EnumerateObject().Select(p => p.Name).Should().Equal("resourceType", "id", "birthDate");
        json.GetProperty("resourceType").GetString().Should().Be("Patient");
    }

    [Test]
    public void FhirJson_Outcome_IsOneErrorIssue_WithoutAnEmptyExpression()
    {
        var outcome = FhirJson.Outcome("not-found", "Gone.");
        var withWhere = FhirJson.Outcome("invalid", "Bad.", ["identifier"]);

        outcome.ResourceType.Should().Be("OperationOutcome");
        outcome.Issue.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Severity = "error",
            Code = "not-found",
            Diagnostics = "Gone.",
            Expression = (List<string>?)null
        });
        withWhere.Issue[0].Expression.Should().Equal("identifier");
    }

    private static async Task<(ExceptionContext Context, HttpContext Http)> Filter(Exception exception)
    {
        var http = NewContext();
        var context = new ExceptionContext(
            new ActionContext(http, new RouteData(), new ActionDescriptor()), [])
        {
            Exception = exception
        };
        await new FhirExceptionFilter().OnExceptionAsync(context);
        return (context, http);
    }

    [Test]
    public async Task ExceptionFilter_ValidationFailure_Is400Invalid()
    {
        var (context, http) = await Filter(new ValidationException([
            new ValidationFailure("identifier", "Bad identifier."),
            new ValidationFailure("identifier", "Bad identifier.")
        ]));

        context.ExceptionHandled.Should().BeTrue();
        http.Response.StatusCode.Should().Be(400);
        var issue = Body(http).GetProperty("issue")[0];
        issue.GetProperty("code").GetString().Should().Be("invalid");
        issue.GetProperty("diagnostics").GetString().Should().Be("Bad identifier.");
        issue.GetProperty("expression")[0].GetString().Should().Be("identifier");
    }

    [Test]
    public async Task ExceptionFilter_NotFound_Is404NotFound()
    {
        var (context, http) = await Filter(new NotFoundException("Patient 'x' was not found."));

        context.ExceptionHandled.Should().BeTrue();
        http.Response.StatusCode.Should().Be(404);
        http.Response.ContentType.Should().StartWith("application/fhir+json");
        Body(http).GetProperty("issue")[0].GetProperty("code").GetString().Should().Be("not-found");
    }

    [Test]
    public async Task ExceptionFilter_AnythingElse_IsLeftAlone()
    {
        var (context, http) = await Filter(new InvalidOperationException());

        context.ExceptionHandled.Should().BeFalse();
        http.Response.Body.Length.Should().Be(0);
    }

    private static async Task<HttpContext> Authorise(string path, PolicyAuthorizationResult result, Action? next = null)
    {
        var http = NewContext();
        http.RequestServices = new ServiceCollection().AddLogging().AddAuthentication().Services.BuildServiceProvider();
        http.Request.Path = path;
        await new FhirAuthorizationResultHandler().HandleAsync(
            _ =>
            {
                next?.Invoke();
                return Task.CompletedTask;
            },
            http,
            new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build(),
            result);
        return http;
    }

    [Test]
    public async Task AuthorisationHandler_FhirNotSignedIn_Is401Login()
    {
        var http = await Authorise("/api/fhir/Patient", PolicyAuthorizationResult.Challenge());

        http.Response.StatusCode.Should().Be(401);
        Body(http).GetProperty("issue")[0].GetProperty("code").GetString().Should().Be("login");
    }

    [Test]
    public async Task AuthorisationHandler_FhirWithoutTheRole_Is403Forbidden()
    {
        var http = await Authorise("/API/FHIR/Patient/1", PolicyAuthorizationResult.Forbid());

        http.Response.StatusCode.Should().Be(403);
        Body(http).GetProperty("issue")[0].GetProperty("code").GetString().Should().Be("forbidden");
    }

    [Test]
    public async Task AuthorisationHandler_Allowed_GoesOn()
    {
        var called = false;

        await Authorise("/api/fhir/Patient", PolicyAuthorizationResult.Success(), () => called = true);

        called.Should().BeTrue();
    }

    [Test]
    public void IsFhir_OnlyTheFhirApi()
    {
        FhirAuthorizationResultHandler.IsFhir("/api/fhir").Should().BeTrue();
        FhirAuthorizationResultHandler.IsFhir("/api/fhir/Patient/1").Should().BeTrue();
        FhirAuthorizationResultHandler.IsFhir("/api/fhirish").Should().BeFalse();
        FhirAuthorizationResultHandler.IsFhir("/api/patients").Should().BeFalse();
    }

    [Test]
    public void Instant_IsUtcToTheMillisecondWithAZ() =>
        FhirPatientMapper.Instant(ActivityTestConstants.Now).Should().Be("2026-11-16T09:30:00.000Z");
}
