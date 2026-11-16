using System.Text.Json;
using System.Text.Json.Serialization;
using MedApp.Contracts.Fhir;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Fhir;

/// <summary>
/// How the FHIR API writes its resources: FHIR R4 JSON (camelCase, no nulls, since FHIR JSON never holds one), sent as
/// <c>application/fhir+json</c>, and never cached, as it holds patient data.
/// </summary>
public static class FhirJson
{
    public const string ContentType = "application/fhir+json";

    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Writes a resource as the response, with its status code.</summary>
    public static async Task WriteAsync<T>(HttpResponse response, T resource, int statusCode)
        where T : FhirResource
    {
        response.StatusCode = statusCode;
        response.Headers.CacheControl = "no-store";
        await response.WriteAsJsonAsync(resource, Options, $"{ContentType}; charset=utf-8");
    }

    /// <summary>An OperationOutcome with one error issue.</summary>
    public static FhirOperationOutcome Outcome(string code, string diagnostics, IEnumerable<string>? expression = null) => new()
    {
        Issue =
        [
            new FhirOperationOutcomeIssue
            {
                Severity = "error",
                Code = code,
                Diagnostics = diagnostics,
                Expression = expression?.ToList() is { Count: > 0 } list ? list : null
            }
        ]
    };
}

/// <summary>A FHIR resource as an action's result (<see cref="FhirJson"/>).</summary>
public sealed class FhirResult<T>(T resource, int statusCode = StatusCodes.Status200OK) : IActionResult
    where T : FhirResource
{
    public T Resource { get; } = resource;

    public int StatusCode { get; } = statusCode;

    public Task ExecuteResultAsync(ActionContext context) =>
        FhirJson.WriteAsync(context.HttpContext.Response, Resource, StatusCode);
}
