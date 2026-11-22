using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

// The parts of ASP.NET Core MVC the API's controllers use: attributes, results and ControllerBase. The browser's
// router (MedApp.Browser.Hosting) reads the attributes and runs the results as MVC does.
namespace Microsoft.AspNetCore.Mvc
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public sealed class RouteAttribute(string template) : Attribute
    {
        public string Template { get; } = template;
        public string? Name { get; set; }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public abstract class HttpMethodAttribute(string method, string? template) : Attribute
    {
        public string Method { get; } = method;
        public string? Template { get; } = template;
        public string? Name { get; set; }
    }

    public sealed class HttpGetAttribute(string? template = null) : HttpMethodAttribute("GET", template);
    public sealed class HttpPostAttribute(string? template = null) : HttpMethodAttribute("POST", template);
    public sealed class HttpPutAttribute(string? template = null) : HttpMethodAttribute("PUT", template);
    public sealed class HttpPatchAttribute(string? template = null) : HttpMethodAttribute("PATCH", template);
    public sealed class HttpDeleteAttribute(string? template = null) : HttpMethodAttribute("DELETE", template);

    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
    public sealed class FromBodyAttribute : Attribute
    {
        public Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior EmptyBodyBehavior { get; set; }
    }

    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
    public sealed class FromQueryAttribute : Attribute
    {
        public string? Name { get; set; }
    }

    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
    public sealed class FromRouteAttribute : Attribute
    {
        public string? Name { get; set; }
    }

    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
    public sealed class FromHeaderAttribute : Attribute
    {
        public string? Name { get; set; }
    }

    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class FromServicesAttribute : Attribute;

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly)]
    public sealed class ApiControllerAttribute : Attribute;

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public sealed class ProducesResponseTypeAttribute : Attribute
    {
        public ProducesResponseTypeAttribute(int statusCode) => StatusCode = statusCode;

        public ProducesResponseTypeAttribute(Type type, int statusCode)
        {
            Type = type;
            StatusCode = statusCode;
        }

        public ProducesResponseTypeAttribute(Type type, int statusCode, string contentType, params string[] additionalContentTypes)
            : this(type, statusCode)
        {
        }

        public Type? Type { get; }
        public int StatusCode { get; }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public sealed class ProducesResponseTypeAttribute<T>(int statusCode) : Attribute
    {
        public int StatusCode { get; } = statusCode;
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class TagsAttribute(params string[] tags) : Attribute
    {
        public IReadOnlyList<string> Tags { get; } = tags;
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class RequestSizeLimitAttribute(long bytes) : Attribute
    {
        public long Bytes { get; } = bytes;
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class DisableRequestSizeLimitAttribute : Attribute;

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class TypeFilterAttribute(Type type) : Attribute
    {
        public Type ImplementationType { get; } = type;
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public sealed class TypeFilterAttribute<T>() : TypeFilterAttribute(typeof(T));

    namespace Filters
    {
        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
        public abstract class ActionFilterAttribute : Attribute;
    }

    namespace ModelBinding
    {
        public enum EmptyBodyBehavior
        {
            Default,
            Allow,
            Disallow,
        }

        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Parameter | AttributeTargets.Property)]
        public sealed class BindNeverAttribute : Attribute;
    }

    public interface IActionResult;

    /// <summary>A result the router runs: sets the status, headers and body of the response.</summary>
    public abstract class ActionResult : IActionResult;

    public interface IConvertToActionResult
    {
        IActionResult Convert();
    }

    public sealed class ActionResult<TValue> : IConvertToActionResult
    {
        public ActionResult(TValue value) => Value = value;

        public ActionResult(ActionResult result) => Result = result;

        public ActionResult? Result { get; }
        public TValue? Value { get; }

        public IActionResult Convert() => Result ?? new ObjectResult(Value) { StatusCode = StatusCodes.Status200OK };

        public static implicit operator ActionResult<TValue>(TValue value) => new(value);
        public static implicit operator ActionResult<TValue>(ActionResult result) => new(result);
    }

    public class StatusCodeResult(int statusCode) : ActionResult
    {
        public int StatusCode { get; } = statusCode;
    }

    public class ObjectResult(object? value) : ActionResult
    {
        public object? Value { get; set; } = value;
        public int? StatusCode { get; set; }
        public string? Location { get; set; }
    }

    public sealed class OkResult() : StatusCodeResult(StatusCodes.Status200OK);
    public sealed class NoContentResult() : StatusCodeResult(StatusCodes.Status204NoContent);
    public sealed class NotFoundResult() : StatusCodeResult(StatusCodes.Status404NotFound);
    public sealed class BadRequestResult() : StatusCodeResult(StatusCodes.Status400BadRequest);
    public sealed class UnauthorizedResult() : StatusCodeResult(StatusCodes.Status401Unauthorized);
    public sealed class ConflictResult() : StatusCodeResult(StatusCodes.Status409Conflict);
    public sealed class AcceptedResult() : ObjectResult(null);

    public sealed class CreatedAtActionResult(string? actionName, object? routeValues, object? value) : ObjectResult(value)
    {
        public string? ActionName { get; } = actionName;
        public object? RouteValues { get; } = routeValues;
    }

    public class FileResult(string contentType) : ActionResult
    {
        public string ContentType { get; } = contentType;
        public string? FileDownloadName { get; set; }
        public bool EnableRangeProcessing { get; set; }
    }

    public sealed class FileStreamResult(Stream fileStream, string contentType) : FileResult(contentType)
    {
        public Stream FileStream { get; } = fileStream;
    }

    public sealed class FileContentResult(byte[] fileContents, string contentType) : FileResult(contentType)
    {
        public byte[] FileContents { get; } = fileContents;
    }

    public sealed class ContentResult : ActionResult
    {
        public string? Content { get; set; }
        public string? ContentType { get; set; }
        public int? StatusCode { get; set; }
    }

    /// <summary>The request a controller handles: set by the router before the action runs.</summary>
    public sealed class ControllerContext
    {
        public HttpContext HttpContext { get; set; } = null!;
    }

    public abstract class ControllerBase
    {
        public ControllerContext ControllerContext { get; set; } = new();
        public HttpContext HttpContext => ControllerContext.HttpContext;
        public HttpRequest Request => HttpContext.Request;
        public HttpResponse Response => HttpContext.Response;
        public ClaimsPrincipal User => HttpContext.User;

        public virtual OkResult Ok() => new();
        public virtual ObjectResult Ok(object? value) => new(value) { StatusCode = StatusCodes.Status200OK };
        public virtual NoContentResult NoContent() => new();
        public virtual NotFoundResult NotFound() => new();
        public virtual ObjectResult NotFound(object? value) => new(value) { StatusCode = StatusCodes.Status404NotFound };
        public virtual BadRequestResult BadRequest() => new();
        public virtual ObjectResult BadRequest(object? error) => new(error) { StatusCode = StatusCodes.Status400BadRequest };
        public virtual UnauthorizedResult Unauthorized() => new();
        public virtual ObjectResult Unauthorized(object? value) => new(value) { StatusCode = StatusCodes.Status401Unauthorized };
        public virtual ConflictResult Conflict() => new();
        public virtual ObjectResult Conflict(object? error) => new(error) { StatusCode = StatusCodes.Status409Conflict };
        public virtual AcceptedResult Accepted() => new() { StatusCode = StatusCodes.Status202Accepted };
        public virtual ObjectResult Accepted(object? value) => new(value) { StatusCode = StatusCodes.Status202Accepted };
        public virtual ObjectResult Accepted(string? uri, object? value) => new(value) { StatusCode = StatusCodes.Status202Accepted, Location = uri };
        public virtual StatusCodeResult StatusCode(int statusCode) => new(statusCode);
        public virtual ObjectResult StatusCode(int statusCode, object? value) => new(value) { StatusCode = statusCode };

        public virtual ObjectResult Created(string? uri, object? value) =>
            new(value) { StatusCode = StatusCodes.Status201Created, Location = uri };

        public virtual ObjectResult Created() => new(null) { StatusCode = StatusCodes.Status201Created };

        public virtual CreatedAtActionResult CreatedAtAction(string? actionName, object? routeValues, object? value) =>
            new(actionName, routeValues, value) { StatusCode = StatusCodes.Status201Created };

        public virtual FileContentResult File(byte[] fileContents, string contentType, string? fileDownloadName = null) =>
            new(fileContents, contentType) { FileDownloadName = fileDownloadName };

        public virtual FileStreamResult File(Stream fileStream, string contentType, string? fileDownloadName = null) =>
            new(fileStream, contentType) { FileDownloadName = fileDownloadName };
    }

    /// <summary>As ASP.NET Core's (RFC 9457), serialised the same way: unset members left out, extensions flattened.</summary>
    public class ProblemDetails
    {
        [JsonPropertyName("type"), JsonPropertyOrder(-5), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Type { get; set; }

        [JsonPropertyName("title"), JsonPropertyOrder(-4), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Title { get; set; }

        [JsonPropertyName("status"), JsonPropertyOrder(-3), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Status { get; set; }

        [JsonPropertyName("detail"), JsonPropertyOrder(-2), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Detail { get; set; }

        [JsonPropertyName("instance"), JsonPropertyOrder(-1), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Instance { get; set; }

        [JsonExtensionData]
        public IDictionary<string, object?> Extensions { get; set; } = new Dictionary<string, object?>(StringComparer.Ordinal);
    }

    public class ValidationProblemDetails : ProblemDetails
    {
        public ValidationProblemDetails() : this(new Dictionary<string, string[]>())
        {
        }

        public ValidationProblemDetails(IDictionary<string, string[]> errors)
        {
            Title = "One or more validation errors occurred.";
            Errors = new Dictionary<string, string[]>(errors, StringComparer.Ordinal);
        }

        [JsonPropertyName("errors"), JsonPropertyOrder(0)]
        public IDictionary<string, string[]> Errors { get; set; }
    }
}

namespace Microsoft.AspNetCore.Authorization
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public class AuthorizeAttribute : Attribute
    {
        public AuthorizeAttribute()
        {
        }

        public AuthorizeAttribute(string policy) => Policy = policy;

        public string? Policy { get; set; }
        public string? Roles { get; set; }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
    public sealed class AllowAnonymousAttribute : Attribute;
}

namespace Microsoft.AspNetCore.RateLimiting
{
    /// <summary>Sign-in throttling has nothing to protect in the browser: the attribute is read and ignored.</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class EnableRateLimitingAttribute(string policyName) : Attribute
    {
        public string PolicyName { get; } = policyName;
    }
}

namespace Microsoft.Net.Http.Headers
{
    /// <summary>The part of ASP.NET Core's media type parser uploads use: the type, its subtype and parameters.</summary>
    public sealed class MediaTypeHeaderValue
    {
        private MediaTypeHeaderValue(string mediaType, string? charset)
        {
            MediaType = mediaType;
            Charset = charset;
        }

        public Microsoft.Extensions.Primitives.StringSegment MediaType { get; }
        public Microsoft.Extensions.Primitives.StringSegment Charset { get; }

        public static bool TryParse(string? input, out MediaTypeHeaderValue parsed)
        {
            parsed = null!;
            if (string.IsNullOrWhiteSpace(input))
                return false;

            var parts = input.Split(';');
            var mediaType = parts[0].Trim();
            var slash = mediaType.IndexOf('/');
            if (slash <= 0 || slash == mediaType.Length - 1 || mediaType.Contains(' '))
                return false;

            string? charset = null;
            foreach (var parameter in parts.Skip(1))
            {
                var pair = parameter.Split('=', 2);
                if (pair.Length == 2 && pair[0].Trim().Equals("charset", StringComparison.OrdinalIgnoreCase))
                    charset = pair[1].Trim().Trim('"');
            }

            parsed = new MediaTypeHeaderValue(mediaType, charset);
            return true;
        }

        public override string ToString() => Charset.HasValue ? $"{MediaType}; charset={Charset}" : MediaType.ToString();
    }
}
