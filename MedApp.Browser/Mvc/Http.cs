using System.Net;
using System.Security.Claims;
using Microsoft.Extensions.Primitives;

// The parts of ASP.NET Core's HTTP abstractions the API's controllers use, for one request served in the browser.
namespace Microsoft.AspNetCore.Http;

public static class StatusCodes
{
    public const int Status200OK = 200;
    public const int Status201Created = 201;
    public const int Status202Accepted = 202;
    public const int Status204NoContent = 204;
    public const int Status302Found = 302;
    public const int Status304NotModified = 304;
    public const int Status400BadRequest = 400;
    public const int Status401Unauthorized = 401;
    public const int Status403Forbidden = 403;
    public const int Status404NotFound = 404;
    public const int Status405MethodNotAllowed = 405;
    public const int Status406NotAcceptable = 406;
    public const int Status409Conflict = 409;
    public const int Status410Gone = 410;
    public const int Status412PreconditionFailed = 412;
    public const int Status413PayloadTooLarge = 413;
    public const int Status415UnsupportedMediaType = 415;
    public const int Status422UnprocessableEntity = 422;
    public const int Status423Locked = 423;
    public const int Status429TooManyRequests = 429;
    public const int Status500InternalServerError = 500;
    public const int Status501NotImplemented = 501;
    public const int Status502BadGateway = 502;
    public const int Status503ServiceUnavailable = 503;
}

public sealed class HeaderDictionary : Dictionary<string, StringValues>
{
    public HeaderDictionary() : base(StringComparer.OrdinalIgnoreCase)
    {
    }

    public new StringValues this[string key]
    {
        get => TryGetValue(key, out var value) ? value : StringValues.Empty;
        set
        {
            if (StringValues.IsNullOrEmpty(value))
                Remove(key);
            else
                base[key] = value;
        }
    }

    public StringValues Accept { get => this["Accept"]; set => this["Accept"] = value; }
    public StringValues CacheControl { get => this["Cache-Control"]; set => this["Cache-Control"] = value; }
    public StringValues ContentDisposition { get => this["Content-Disposition"]; set => this["Content-Disposition"] = value; }
    public StringValues ContentSecurityPolicy { get => this["Content-Security-Policy"]; set => this["Content-Security-Policy"] = value; }
    public StringValues ContentType { get => this["Content-Type"]; set => this["Content-Type"] = value; }
    public StringValues Location { get => this["Location"]; set => this["Location"] = value; }
    public StringValues RetryAfter { get => this["Retry-After"]; set => this["Retry-After"] = value; }
    public StringValues UserAgent { get => this["User-Agent"]; set => this["User-Agent"] = value; }
    public StringValues XContentTypeOptions { get => this["X-Content-Type-Options"]; set => this["X-Content-Type-Options"] = value; }
    public StringValues XFrameOptions { get => this["X-Frame-Options"]; set => this["X-Frame-Options"] = value; }
}

public readonly record struct HostString(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct PathString(string? Value)
{
    public bool HasValue => !string.IsNullOrEmpty(Value);

    public bool StartsWithSegments(PathString other, StringComparison comparison)
    {
        var value = Value ?? string.Empty;
        var start = other.Value ?? string.Empty;
        return value.StartsWith(start, comparison) && (value.Length == start.Length || value[start.Length] == '/');
    }

    public override string ToString() => Value ?? string.Empty;

    public static implicit operator PathString(string? value) => new(value);
    public static implicit operator string(PathString path) => path.ToString();
}

public sealed class ConnectionInfo
{
    public IPAddress? RemoteIpAddress { get; set; }
}

public sealed class HttpRequest
{
    public string Method { get; init; } = "GET";
    public string Scheme { get; init; } = "https";
    public HostString Host { get; init; } = new("localhost");
    public PathString PathBase { get; init; } = new(string.Empty);
    public PathString Path { get; init; }
    public string QueryString { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, StringValues> Query { get; init; } = new Dictionary<string, StringValues>();
    public HeaderDictionary Headers { get; } = new();
    public Dictionary<string, object?> RouteValues { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Stream Body { get; set; } = Stream.Null;

    public string? ContentType => Headers.ContentType.Count > 0 ? Headers.ContentType.ToString() : null;

    public long? ContentLength { get; init; }
}

public sealed class HttpResponse
{
    public int StatusCode { get; set; } = StatusCodes.Status200OK;
    public HeaderDictionary Headers { get; } = new();
    public long? ContentLength { get; set; }
    public MemoryStream Body { get; } = new();
    public bool HasStarted => Body.Length > 0;

    public string? ContentType
    {
        get => Headers.ContentType.Count > 0 ? Headers.ContentType.ToString() : null;
        set => Headers.ContentType = value;
    }
}

public delegate Task RequestDelegate(HttpContext context);

/// <summary>Kestrel's refusal of a request it can't read (a body over its size limit).</summary>
public sealed class BadHttpRequestException(string message, int statusCode) : IOException(message)
{
    public int StatusCode { get; } = statusCode;
}

public static class HttpResponseJsonExtensions
{
    /// <summary>As ASP.NET Core's: the API's JSON options when none are given, and its content type unless one is.</summary>
    public static async Task WriteAsJsonAsync<TValue>(
        this HttpResponse response,
        TValue value,
        System.Text.Json.JsonSerializerOptions? options,
        string? contentType,
        CancellationToken cancellationToken = default)
    {
        response.ContentType = contentType ?? "application/json; charset=utf-8";
        await System.Text.Json.JsonSerializer.SerializeAsync(
            response.Body, value, options ?? MedApp.Browser.Hosting.ApiJson.Options, cancellationToken);
    }
}

public sealed class HttpContext
{
    public HttpContext(HttpRequest request, IServiceProvider services, CancellationToken aborted)
    {
        Request = request;
        RequestServices = services;
        RequestAborted = aborted;
    }

    public HttpRequest Request { get; }
    public HttpResponse Response { get; } = new();
    public ClaimsPrincipal User { get; set; } = new(new ClaimsIdentity());
    public ConnectionInfo Connection { get; } = new();
    public IServiceProvider RequestServices { get; }
    public CancellationToken RequestAborted { get; }
    public string TraceIdentifier { get; } = $"00-{Guid.NewGuid():N}-{Guid.NewGuid().ToString("N")[..16]}-00";
}

public interface IHttpContextAccessor
{
    HttpContext? HttpContext { get; set; }
}

public sealed class HttpContextAccessor : IHttpContextAccessor
{
    public HttpContext? HttpContext { get; set; }
}
