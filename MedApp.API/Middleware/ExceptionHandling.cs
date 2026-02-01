using FluentValidation;
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
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(problemDetails);
    }

}