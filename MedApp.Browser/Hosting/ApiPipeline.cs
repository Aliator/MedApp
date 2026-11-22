using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using MedApp.API.Common.Authentication;
using MedApp.API.Middleware;
using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace MedApp.Browser.Hosting;

/// <summary>The API's JSON: MVC's defaults (camelCase names, names read ignoring case, numbers read from strings).</summary>
public static class ApiJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}

public sealed record ApiRequest(string Method, string Url, IReadOnlyDictionary<string, string> Headers, byte[] Body);

public sealed record ApiResponse(int Status, IReadOnlyDictionary<string, string> Headers, byte[] Body);

/// <summary>
/// The API's request pipeline, for one request at a time: its headers on <c>/api</c>, <see cref="ExceptionHandling"/>
/// (the API's own, unchanged), the session, the controllers' authorisation, MVC's binding and validation, the action,
/// and its result as MVC writes it. An <c>/api</c> path no action matches is a 404 with no body, as the API's.
/// </summary>
public sealed class ApiPipeline(IServiceProvider services, IReadOnlyList<ApiRoute> routes)
{
    private readonly SemaphoreSlim _oneAtATime = new(1, 1);

    public async Task<ApiResponse> SendAsync(ApiRequest request, CancellationToken ct)
    {
        await _oneAtATime.WaitAsync(ct);
        try
        {
            return await RunAsync(request, ct);
        }
        finally
        {
            _oneAtATime.Release();
        }
    }

    private async Task<ApiResponse> RunAsync(ApiRequest request, CancellationToken ct)
    {
        var uri = new Uri(new Uri("https://localhost"), request.Url);
        var http = NewRequest(request, uri);

        await using var scope = services.CreateAsyncScope();
        var context = new HttpContext(http, scope.ServiceProvider, ct);
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = context;

        // SecurityHeaders: first, on every /api response.
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Response.Headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";

        try
        {
            await new ExceptionHandling(next => ProcessAsync(next, request.Body)).InvokeAsync(context);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"{request.Method} {uri.AbsolutePath}: unhandled {e}");
            context.Response.Body.SetLength(0);
            context.Response.Headers.Remove("Content-Type");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        }

        var headers = context.Response.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        return new ApiResponse(context.Response.StatusCode, headers, context.Response.Body.ToArray());
    }

    private static HttpRequest NewRequest(ApiRequest request, Uri uri)
    {
        var headers = request.Headers;
        var http = new HttpRequest
        {
            Method = request.Method.ToUpperInvariant(),
            Scheme = uri.Scheme,
            Host = new HostString(headers.TryGetValue("Host", out var host) ? host : uri.Authority),
            Path = uri.AbsolutePath,
            QueryString = uri.Query,
            Query = ParseQuery(uri.Query),
            Body = new MemoryStream(request.Body, writable: false),
            ContentLength = request.Body.Length,
        };
        foreach (var (name, value) in headers)
            http.Headers[name] = value;
        return http;
    }

    private static Dictionary<string, StringValues> ParseQuery(string query)
    {
        var values = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var key = Decode(parts[0]);
            if (!values.TryGetValue(key, out var list))
                values[key] = list = [];
            list.Add(parts.Length > 1 ? Decode(parts[1]) : string.Empty);
        }

        return values.ToDictionary(p => p.Key, p => new StringValues(p.Value.ToArray()), StringComparer.OrdinalIgnoreCase);

        static string Decode(string text) => Uri.UnescapeDataString(text.Replace('+', ' '));
    }

    private async Task ProcessAsync(HttpContext context, byte[] body)
    {
        var path = context.Request.Path.ToString();
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.UnescapeDataString).ToList();

        ApiRoute? route = null;
        Dictionary<string, string>? values = null;
        if (segments is ["api", ..])
        {
            foreach (var candidate in routes)
            {
                if (candidate.Method != context.Request.Method || candidate.Match(segments) is not { } matched)
                    continue;
                route = candidate;
                values = matched;
                break;
            }
        }

        if (route is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        foreach (var (name, value) in values!)
            context.Request.RouteValues[name] = value;

        context.User = await AuthenticateAsync(context);
        if (!await AuthorizeAsync(context, route))
            return;

        var binder = new ActionBinder(context, route, body);
        var arguments = binder.Bind();
        if (binder.UnsupportedMediaType)
        {
            await WriteResultAsync(context, route, new StatusCodeResult(StatusCodes.Status415UnsupportedMediaType));
            return;
        }

        if (binder.Errors.Count > 0)
        {
            var problem = new ValidationProblemDetails(binder.Errors.ToDictionary(e => e.Key, e => e.Value.ToArray()))
            {
                Type = ClientErrors.TypeFor(StatusCodes.Status400BadRequest),
                Status = StatusCodes.Status400BadRequest,
                Extensions = { ["traceId"] = context.TraceIdentifier },
            };
            await WriteResultAsync(context, route, new ObjectResult(problem) { StatusCode = StatusCodes.Status400BadRequest });
            return;
        }

        var controller = (ControllerBase)ActivatorUtilities.CreateInstance(context.RequestServices, route.Controller);
        controller.ControllerContext = new ControllerContext { HttpContext = context };

        object? returned;
        try
        {
            returned = route.Action.Invoke(controller, arguments);
        }
        catch (TargetInvocationException e) when (e.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();
            throw;
        }

        if (returned is Task task)
        {
            await task;
            if (!route.Action.ReturnType.IsGenericType)
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return;
            }

            returned = task.GetType().GetProperty(nameof(Task<object>.Result))!.GetValue(task);
        }

        var result = returned switch
        {
            IActionResult actionResult => actionResult,
            IConvertToActionResult convertible => convertible.Convert(),
            _ => new ObjectResult(returned) { StatusCode = StatusCodes.Status200OK },
        };
        await WriteResultAsync(context, route, result);
    }

    /// <summary>The session's user, with the claims <c>SessionAuthenticationHandler</c> gives (no holds: never in the browser).</summary>
    private static async Task<ClaimsPrincipal> AuthenticateAsync(HttpContext context)
    {
        var cookies = context.RequestServices.GetRequiredService<ISessionCookieService>();
        if (!cookies.TryGetSessionCookie(context.Request, out var sessionId) || sessionId is null)
            return new ClaimsPrincipal(new ClaimsIdentity());

        var validation = await context.RequestServices.GetRequiredService<ISessionService>()
            .ValidateSessionAsync(sessionId.Value, context.RequestAborted);
        if (validation is null)
            return new ClaimsPrincipal(new ClaimsIdentity());

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, validation.UserId.ToString()),
            new(ClaimTypes.Name, validation.Username),
            new(SessionAuthenticationDefaults.SessionIdClaim, sessionId.Value.ToString()),
        };
        claims.AddRange(validation.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange((validation.Permissions ?? new HashSet<Permission>()).Order()
            .Select(permission => new Claim(SessionAuthenticationDefaults.PermissionClaim, permission.ToString())));
        claims.AddRange((validation.TeamIds ?? []).Select(teamId => new Claim(SessionAuthenticationDefaults.TeamClaim, teamId.ToString())));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, SessionAuthenticationDefaults.Scheme));
    }

    /// <summary>
    /// The controllers' <c>[Authorize]</c>, <c>[RequirePermission]</c> and <c>[AllowAnonymous]</c>, as the API's policies
    /// judge them: signed out is a 401 with no body; signed in without a permission, a 403 naming it (as
    /// <c>ApiAuthorizationResultHandler</c> writes it); failing another policy, a 403 with no body.
    /// </summary>
    private static async Task<bool> AuthorizeAsync(HttpContext context, ApiRoute route)
    {
        if (route.Controller.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any()
            || route.Action.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any())
            return true;

        var authorize = route.Controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Concat(route.Action.GetCustomAttributes<AuthorizeAttribute>(inherit: true))
            .ToList();
        if (authorize.Count == 0)
            return true;

        var user = context.User;
        var signedIn = user.Identity?.IsAuthenticated == true;
        var missing = authorize.OfType<RequirePermissionAttribute>()
            .Select(a => a.Permission)
            .Where(permission => !user.HasPermission(permission))
            .ToList();
        var otherFails = authorize.Any(a => a is not RequirePermissionAttribute
                                            && a.Policy == AuthPolicies.SelfUserOnly
                                            && !AuthPolicies.IsOwnUsername(context, user));

        if (signedIn && missing.Count == 0 && !otherFails)
            return true;

        if (!signedIn)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return false;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        if (missing.Count > 0)
        {
            var permission = Permissions.Describe(missing[0]);
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Permission needed",
                Detail = $"Your roles don't allow this. It needs the permission to: {permission.Description}",
                Extensions =
                {
                    ["code"] = "permission_missing",
                    ["permission"] = permission.Name,
                },
            };
            await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json");
        }

        return false;
    }

    /// <summary>Runs a result as MVC does for an <c>[ApiController]</c>.</summary>
    private async Task WriteResultAsync(HttpContext context, ApiRoute route, IActionResult result)
    {
        var response = context.Response;
        switch (result)
        {
            case StatusCodeResult status when status.StatusCode >= 400:
                // [ApiController]'s client error mapping: a bare error status gets a ProblemDetails.
                await WriteObjectAsync(context, ClientErrors.Problem(status.StatusCode, context.TraceIdentifier), status.StatusCode);
                break;

            case StatusCodeResult status:
                response.StatusCode = status.StatusCode;
                break;

            case CreatedAtActionResult created:
                var target = routes.FirstOrDefault(r => r.Controller == route.Controller && r.Action.Name == created.ActionName && r.Method == "GET")
                             ?? routes.First(r => r.Controller == route.Controller && r.Action.Name == created.ActionName);
                var values = created.RouteValues?.GetType().GetProperties()
                                 .ToDictionary(p => p.Name, p => p.GetValue(created.RouteValues), StringComparer.OrdinalIgnoreCase)
                             ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                response.Headers.Location = $"{context.Request.Scheme}://{context.Request.Host}{target.Path(values)}";
                await WriteObjectAsync(context, created.Value, created.StatusCode ?? StatusCodes.Status201Created);
                break;

            case ObjectResult objectResult:
                if (objectResult.Location is not null)
                    response.Headers.Location = objectResult.Location;
                await WriteObjectAsync(context, objectResult.Value, objectResult.StatusCode);
                break;

            case FileStreamResult file:
                response.StatusCode = StatusCodes.Status200OK;
                SetFileHeaders(response, file);
                await file.FileStream.CopyToAsync(response.Body, context.RequestAborted);
                await file.FileStream.DisposeAsync();
                break;

            case FileContentResult file:
                response.StatusCode = StatusCodes.Status200OK;
                SetFileHeaders(response, file);
                await response.Body.WriteAsync(file.FileContents, context.RequestAborted);
                break;

            case ContentResult content:
                response.StatusCode = content.StatusCode ?? StatusCodes.Status200OK;
                response.ContentType = content.ContentType ?? "text/plain; charset=utf-8";
                await response.Body.WriteAsync(Encoding.UTF8.GetBytes(content.Content ?? string.Empty), context.RequestAborted);
                break;

            default:
                throw new NotSupportedException($"The result {result.GetType().Name} isn't supported in the browser.");
        }
    }

    private static void SetFileHeaders(HttpResponse response, FileResult file)
    {
        response.ContentType = file.ContentType;
        if (file.FileDownloadName is { } name)
            response.Headers.ContentDisposition = MedApp.API.Attachments.AttachmentResponseHeaders.ContentDisposition(name);
    }

    /// <summary>MVC's output formatting: no content for a null 200, text for a string, JSON (problem JSON for a ProblemDetails) otherwise.</summary>
    private static async Task WriteObjectAsync(HttpContext context, object? value, int? statusCode)
    {
        var response = context.Response;
        if (value is ProblemDetails problem)
        {
            statusCode ??= problem.Status;
            problem.Status ??= statusCode;
        }

        response.StatusCode = statusCode ?? StatusCodes.Status200OK;
        switch (value)
        {
            case null:
                if (response.StatusCode == StatusCodes.Status200OK)
                    response.StatusCode = StatusCodes.Status204NoContent;
                break;
            case string text:
                response.ContentType = "text/plain; charset=utf-8";
                await response.Body.WriteAsync(Encoding.UTF8.GetBytes(text), context.RequestAborted);
                break;
            default:
                response.ContentType = value is ProblemDetails ? "application/problem+json; charset=utf-8" : "application/json; charset=utf-8";
                await JsonSerializer.SerializeAsync(response.Body, value, value.GetType(), ApiJson.Options, context.RequestAborted);
                break;
        }
    }
}

/// <summary>MVC's <c>ApiBehaviorOptions.ClientErrorMapping</c>: each error status's RFC 9110 link and title.</summary>
public static class ClientErrors
{
    private static readonly Dictionary<int, (string Section, string Title)> Mapping = new()
    {
        [400] = ("15.5.1", "Bad Request"),
        [401] = ("15.5.2", "Unauthorized"),
        [403] = ("15.5.4", "Forbidden"),
        [404] = ("15.5.5", "Not Found"),
        [405] = ("15.5.6", "Method Not Allowed"),
        [406] = ("15.5.7", "Not Acceptable"),
        [408] = ("15.5.9", "Request Timeout"),
        [409] = ("15.5.10", "Conflict"),
        [412] = ("15.5.13", "Precondition Failed"),
        [415] = ("15.5.16", "Unsupported Media Type"),
        [422] = ("15.5.21", "Unprocessable Entity"),
        [426] = ("15.5.22", "Upgrade Required"),
        [500] = ("15.6.1", "An error occurred while processing your request."),
    };

    public static string? TypeFor(int status) =>
        Mapping.TryGetValue(status, out var entry) ? $"https://tools.ietf.org/html/rfc9110#section-{entry.Section}" : null;

    public static ProblemDetails Problem(int status, string traceId) => new()
    {
        Type = TypeFor(status),
        Title = Mapping.TryGetValue(status, out var entry) ? entry.Title : null,
        Status = status,
        Extensions = { ["traceId"] = traceId },
    };
}
