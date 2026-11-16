using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace MedApp.API.Middleware;

/// <summary>
/// The browser security headers every response gets, first in the pipeline so redirects, static files, errors and
/// pages all carry them:
/// <list type="bullet">
/// <item><c>X-Content-Type-Options: nosniff</c>, <c>Referrer-Policy: strict-origin-when-cross-origin</c>,
/// <c>X-Frame-Options: DENY</c> and a <c>Permissions-Policy</c> that turns off the device features the app never uses.</item>
/// <item><c>/api</c>: <c>Cache-Control: no-store</c> (every answer is the signed-in user's data) and a Content Security
/// Policy that allows nothing (<see cref="ApiPolicy"/>), since no API response is ever a page.</item>
/// <item>Pages, outside Development: a Content Security Policy with a new nonce for each request
/// (<see cref="PagePolicy"/>). The nonce goes to the page server (<see cref="ServerRendering.SsrPages"/>), which puts it on
/// the inline scripts the router writes to hydrate the page. In Development, pages come from Vite's dev server (which
/// needs inline scripts of its own), and the API serves only Swagger UI, so the API sends only
/// <c>frame-ancestors 'none'</c> there.</item>
/// </list>
/// </summary>
public sealed class SecurityHeaders(RequestDelegate next, IWebHostEnvironment environment, IOptions<SecurityHeadersOptions> options)
{
    /// <summary>Where the request's nonce is kept (<see cref="HttpContext.Items"/>), for <see cref="GetNonce"/>.</summary>
    private static readonly object NonceKey = new();

    /// <summary>No API response is a page: it may load, run or frame nothing.</summary>
    public const string ApiPolicy = "default-src 'none'; frame-ancestors 'none'";

    /// <summary>What Development's responses get: no framing (Swagger UI needs its inline scripts).</summary>
    public const string DevelopmentPolicy = "frame-ancestors 'none'";

    /// <summary>
    /// Device features the app never uses, off for it and anything it embeds. Only features browsers recognise, so
    /// they don't log warnings about the header.
    /// </summary>
    public const string PermissionsPolicy =
        "accelerometer=(), autoplay=(), camera=(), display-capture=(), encrypted-media=(), geolocation=(), " +
        "gyroscope=(), hid=(), magnetometer=(), microphone=(), midi=(), payment=(), picture-in-picture=(), " +
        "screen-wake-lock=(), serial=(), usb=(), xr-spatial-tracking=()";

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers.XFrameOptions = "DENY";
        headers["Permissions-Policy"] = PermissionsPolicy;

        if (context.Request.Path.StartsWithSegments("/api"))
        {
            // Every API answer is the signed-in user's data (or a generated password): neither the browser nor
            // anything between keeps it. An endpoint may still set a stricter value itself.
            headers.CacheControl = "no-store";
            headers.ContentSecurityPolicy = ApiPolicy;
        }
        else if (environment.IsDevelopment())
        {
            headers.ContentSecurityPolicy = DevelopmentPolicy;
        }
        else
        {
            var nonce = NewNonce();
            context.Items[NonceKey] = nonce;
            if (options.Value.PageCspReportOnly)
                headers.ContentSecurityPolicyReportOnly = PagePolicy(nonce);
            else
                headers.ContentSecurityPolicy = PagePolicy(nonce);
        }

        await next(context);
    }

    /// <summary>The nonce this request's page policy allows inline scripts with; null if it has none.</summary>
    public static string? GetNonce(HttpContext context) => context.Items[NonceKey] as string;

    /// <summary>
    /// The pages' policy. Scripts only from the app's own origin (its built bundles) or inline with this request's
    /// nonce (the router's hydration data, and React's streaming scripts). Styles from its own origin, and inline:
    /// the server writes style attributes (the page's text size, and Astryx's components), which a nonce can't cover.
    /// Images and fonts from its own origin and <c>data:</c> URLs (Astryx's stylesheets carry small images inline).
    /// Connections only to its own origin, which includes the live notifications' WebSocket (<c>wss:</c> to the same
    /// host). No plugins, no <c>&lt;base&gt;</c>, forms only to itself, and no framing by anyone.
    /// </summary>
    public static string PagePolicy(string nonce) =>
        "default-src 'self'; " +
        $"script-src 'self' 'nonce-{nonce}'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: blob:; " +
        "font-src 'self' data:; " +
        "connect-src 'self'; " +
        "object-src 'none'; " +
        "base-uri 'none'; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'";

    /// <summary>128 random bits, base64: unguessable, and new for every response.</summary>
    private static string NewNonce() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
}
