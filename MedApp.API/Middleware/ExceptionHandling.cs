using FluentValidation;
using MedApp.Application.Common.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Middleware;

public sealed class ExceptionHandling(RequestDelegate next)
{
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
        catch (IdentityOperationException ex)
        {
            await WriteValidationProblem(
                context,
                ex.Errors,
                "One or more identity errors occurred.");
        }
    }

    private static async Task WriteValidationProblem(
        HttpContext context,
        IDictionary<string, string[]> errors,
        string title)
    {
        var allErrors = errors
            .SelectMany(e => e.Value)
            .ToList();

        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.ContentType = "application/problem+json";

        if (allErrors.Count == 1)
        {
            await context.Response.WriteAsJsonAsync(new
            {
                title,
                status = StatusCodes.Status400BadRequest,
                error = allErrors[0]
            });

            return;
        }

        var problemDetails = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = title
        };

        await context.Response.WriteAsJsonAsync(problemDetails);
    }
}