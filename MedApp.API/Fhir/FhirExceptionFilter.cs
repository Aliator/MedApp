using FluentValidation;
using MedApp.Application.Common.Exceptions;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MedApp.API.Fhir;

/// <summary>
/// Turns the FHIR API's errors into OperationOutcomes, where the rest of the API writes ProblemDetails: a validation
/// failure is a 400 (<c>invalid</c>), and an unknown resource a 404 (<c>not-found</c>). Anything else is left to the
/// usual handling.
/// </summary>
public sealed class FhirExceptionFilter : IAsyncExceptionFilter
{
    public async Task OnExceptionAsync(ExceptionContext context)
    {
        var (status, outcome) = context.Exception switch
        {
            ValidationException e => (
                StatusCodes.Status400BadRequest,
                FhirJson.Outcome(
                    "invalid",
                    string.Join(" ", e.Errors.Select(error => error.ErrorMessage).Distinct()),
                    e.Errors.Select(error => error.PropertyName).Where(name => !string.IsNullOrEmpty(name)).Distinct())),
            NotFoundException e => (StatusCodes.Status404NotFound, FhirJson.Outcome("not-found", e.Message)),
            _ => (0, null)
        };

        if (outcome is null)
            return;

        await FhirJson.WriteAsync(context.HttpContext.Response, outcome, status);
        context.ExceptionHandled = true;
    }
}
