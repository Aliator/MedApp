using System.Text.Json;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using MedApp.API.Middleware;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Common.Exceptions;
using Microsoft.AspNetCore.Http;

namespace MedApp.UnitTests.Api.Middleware;

[TestFixture]
public sealed class ExceptionHandlingTests
{
    private static async Task<(HttpContext Context, JsonElement Body)> Run(Exception? thrown)
    {
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var middleware = new ExceptionHandling(_ => thrown is null ? Task.CompletedTask : Task.FromException(thrown));

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var body = context.Response.Body.Length > 0 ? await JsonDocument.ParseAsync(context.Response.Body) : null;
        return (context, body?.RootElement ?? default);
    }

    [Test]
    public async Task InvokeAsync_NothingThrown_LeavesTheResponseAlone()
    {
        var (context, _) = await Run(null);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.Body.Length.Should().Be(0);
    }

    [Test]
    public async Task InvokeAsync_ValidationFails_Returns400WithEachFieldsErrors()
    {
        var failures = new[]
        {
            new ValidationFailure("Email", "Email is required."),
            new ValidationFailure("Email", "Email must be valid."),
            new ValidationFailure("FirstName", "First name is required."),
        };

        var (context, body) = await Run(new ValidationException(failures));

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        context.Response.ContentType.Should().Be("application/problem+json");
        body.GetProperty("title").GetString().Should().Be("One or more validation errors occurred.");
        body.GetProperty("errors").GetProperty("Email").EnumerateArray().Select(e => e.GetString())
            .Should().Equal("Email is required.", "Email must be valid.");
        body.GetProperty("errors").GetProperty("FirstName").GetArrayLength().Should().Be(1);
    }

    private static IEnumerable<TestCaseData> KnownProblems()
    {
        yield return new TestCaseData(new NotFoundException("Synthetic problem."), StatusCodes.Status404NotFound);
        yield return new TestCaseData(new ConflictException("Synthetic problem."), StatusCodes.Status409Conflict);
        yield return new TestCaseData(new InvalidCredentialsException(), StatusCodes.Status401Unauthorized);
        yield return new TestCaseData(new AccountLockedOutException(5), StatusCodes.Status423Locked);
        yield return new TestCaseData(new ForbiddenException("Synthetic problem."), StatusCodes.Status403Forbidden);
        yield return new TestCaseData(new FileTooLargeException(), StatusCodes.Status413PayloadTooLarge);
        yield return new TestCaseData(new UnsupportedFileTypeException("Synthetic problem."), StatusCodes.Status415UnsupportedMediaType);
        yield return new TestCaseData(new MalwareDetectedException(), StatusCodes.Status422UnprocessableEntity);
        yield return new TestCaseData(new FileStorageUnavailableException(), StatusCodes.Status503ServiceUnavailable);
    }

    [Test]
    public async Task InvokeAsync_KestrelsRequestSizeLimit_Returns413SayingTheFileIsTooLarge()
    {
        var (context, body) = await Run(new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge));

        context.Response.StatusCode.Should().Be(StatusCodes.Status413PayloadTooLarge);
        body.GetProperty("title").GetString().Should().Be(new FileTooLargeException().Message);
    }

    [Test]
    public async Task InvokeAsync_OtherBadRequests_LetThemThrough()
    {
        var act = () => Run(new BadHttpRequestException("Bad chunk.", StatusCodes.Status400BadRequest));

        await act.Should().ThrowAsync<BadHttpRequestException>();
    }

    [TestCaseSource(nameof(KnownProblems))]
    public async Task InvokeAsync_KnownProblem_ReturnsItsStatusWithItsMessage(Exception exception, int status)
    {
        var (context, body) = await Run(exception);

        context.Response.StatusCode.Should().Be(status);
        context.Response.ContentType.Should().Be("application/problem+json");
        body.GetProperty("status").GetInt32().Should().Be(status);
        body.GetProperty("title").GetString().Should().Be(exception.Message);
    }

    [Test]
    public async Task InvokeAsync_UnexpectedException_LetsItThrough()
    {
        var act = () => Run(new InvalidOperationException("Unexpected"));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
