using FluentValidation;
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
        catch (AccountLockedOutException ex)
        {
            await WriteProblem(context, StatusCodes.Status423Locked, ex.Message);
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
        string title)
    {
        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title
        };

        context.Response.StatusCode = statusCode;
        // Given to the write itself: it sets the content type, and would replace one set before it.
        await context.Response.WriteAsJsonAsync(problemDetails, options: null, contentType: ProblemJson);
    }
}