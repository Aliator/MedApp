using FluentValidation;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Common.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Middleware;

public sealed class ExceptionHandling(RequestDelegate next)
{
    private const string ProblemJson = "application/problem+json";

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException ex)
        {
            await WriteValidationProblem(
                context,
                ex.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(e => e.ErrorMessage).ToArray()),
                "One or more validation errors occurred.");
        }
        catch (NotFoundException ex)
        {
            await WriteProblem(context, StatusCodes.Status404NotFound, ex.Message);
        }
        catch (ConflictException ex)
        {
            await WriteProblem(context, StatusCodes.Status409Conflict, ex.Message);
        }
        catch (InvalidCredentialsException ex)
        {
            await WriteProblem(context, StatusCodes.Status401Unauthorized, ex.Message);
        }
        catch (InvalidTwoFactorCodeException ex)
        {
            await WriteProblem(context, StatusCodes.Status401Unauthorized, ex.Message);
        }
        catch (SignInExpiredException ex)
        {
            await WriteProblem(context, StatusCodes.Status401Unauthorized, ex.Message, SignInExpiredException.Code);
        }
        catch (AccountLockedOutException ex)
        {
            await WriteProblem(context, StatusCodes.Status423Locked, ex.Message);
        }
        catch (ForbiddenException ex)
        {
            await WriteProblem(context, StatusCodes.Status403Forbidden, ex.Message);
        }
        catch (FileTooLargeException ex)
        {
            await WriteProblem(context, StatusCodes.Status413PayloadTooLarge, ex.Message);
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge && !context.Response.HasStarted)
        {
            // Kestrel's request size limit (an upload's [RequestSizeLimit]), hit while the body was being read.
            await WriteProblem(context, StatusCodes.Status413PayloadTooLarge, new FileTooLargeException().Message);
        }
        catch (UnsupportedFileTypeException ex)
        {
            await WriteProblem(context, StatusCodes.Status415UnsupportedMediaType, ex.Message);
        }
        catch (MalwareDetectedException ex)
        {
            await WriteProblem(context, StatusCodes.Status422UnprocessableEntity, ex.Message);
        }
        catch (FileStorageUnavailableException ex)
        {
            await WriteProblem(context, StatusCodes.Status503ServiceUnavailable, ex.Message);
        }
        catch (StorageUnavailableException ex) when (!context.Response.HasStarted)
        {
            await WriteProblem(context, StatusCodes.Status503ServiceUnavailable, ex.Message);
        }
        catch (TooManyAttemptsException ex)
        {
            context.Response.Headers.RetryAfter = ex.RetryAfterSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
            await WriteProblem(context, StatusCodes.Status429TooManyRequests, ex.Message);
        }
    }

    private static async Task WriteValidationProblem(
        HttpContext context,
        IDictionary<string, string[]> errors,
        string title)
    {
        var problemDetails = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = title
        };

        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        // Given to the write itself: it sets the content type, and would replace one set before it.
        await context.Response.WriteAsJsonAsync(problemDetails, options: null, contentType: ProblemJson);
    }
    private static async Task WriteProblem(
        HttpContext context,
        int statusCode,
        string title,
        string? code = null)
    {
        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title
        };
        if (code is not null)
            problemDetails.Extensions["code"] = code;

        context.Response.StatusCode = statusCode;
        // Given to the write itself: it sets the content type, and would replace one set before it.
        await context.Response.WriteAsJsonAsync(problemDetails, options: null, contentType: ProblemJson);
    }
}