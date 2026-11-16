using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Net.Http.Headers;

namespace MedApp.API.Fhir;

/// <summary>
/// The FHIR API answers in JSON only. A request that asks for another format, by its <c>_format</c> parameter or its
/// <c>Accept</c> header, gets a 406 (an OperationOutcome, in JSON) instead of JSON it didn't ask for. No
/// <c>Accept</c>, or one that allows JSON (<c>application/fhir+json</c>, <c>application/json</c>, or a wildcard), is
/// fine; so is <c>_format</c> of <c>json</c>, <c>application/json</c> or <c>application/fhir+json</c>.
/// </summary>
public sealed class FhirFormatFilter : IAsyncResourceFilter
{
    private static readonly HashSet<string> JsonFormats =
        new(StringComparer.OrdinalIgnoreCase) { "json", "application/json", FhirJson.ContentType };

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        if (WhyNotAcceptable(context.HttpContext.Request) is { } why)
        {
            await FhirJson.WriteAsync(context.HttpContext.Response, FhirJson.Outcome("not-supported", why), StatusCodes.Status406NotAcceptable);
            return;
        }

        await next();
    }

    /// <summary>Why the request asks for a format the API can't give, or null when JSON is fine.</summary>
    public static string? WhyNotAcceptable(HttpRequest request)
    {
        if (request.Query.TryGetValue("_format", out var formats))
        {
            var format = formats.ToString().Split(';')[0].Trim();
            return JsonFormats.Contains(format)
                ? null
                : $"MedApp's FHIR API answers in JSON only, not '{format}'.";
        }

        var accept = request.GetTypedHeaders().Accept;
        if (accept.Count == 0 || accept.Any(AllowsJson))
            return null;

        return "MedApp's FHIR API answers in JSON only (application/fhir+json).";
    }

    private static bool AllowsJson(MediaTypeHeaderValue type) =>
        type.Quality is not 0
        && (type.MatchesAllTypes
            || type.MediaType.Equals("application/*", StringComparison.OrdinalIgnoreCase)
            || type.MediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
            || type.MediaType.Equals(FhirJson.ContentType, StringComparison.OrdinalIgnoreCase));
}
