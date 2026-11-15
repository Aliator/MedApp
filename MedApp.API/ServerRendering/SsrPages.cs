using Microsoft.Extensions.Options;

namespace MedApp.API.ServerRendering;

/// <summary>
/// Serves the React app's pages: rendered by the Node server (<see cref="SsrServer"/>) while it's running, with the
/// request's cookie so the page is the signed-in user's, and otherwise the client-rendered app (index.html). Any
/// failure to render, or an error page from the Node server (5xx), gets the client-rendered app too, as long as
/// nothing has been sent yet.
/// </summary>
public static class SsrPages
{
    /// <summary>The named HttpClient that calls the Node server.</summary>
    public const string ClientName = "ssr";

    /// <summary>Request headers passed on to the Node server: what a page depends on.</summary>
    private static readonly string[] ForwardedRequestHeaders = ["Cookie", "Accept", "Accept-Language", "User-Agent"];

    /// <summary>Response headers not copied back: the connection's own, which Kestrel sets.</summary>
    private static readonly HashSet<string> HopByHopHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Connection", "Keep-Alive", "Transfer-Encoding", "Upgrade", "Proxy-Connection", "TE", "Trailer",
    };

    /// <summary>The fallback endpoint: every path that isn't the API's or a file.</summary>
    public static async Task Handle(
        HttpContext context,
        ISsrServer ssr,
        IHttpClientFactory clients,
        IOptions<SsrOptions> options,
        ILogger<SsrServer> logger)
    {
        var address = ssr.Address;
        if (address is not null && (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)))
        {
            try
            {
                if (await Forward(context, address, clients.CreateClient(ClientName), options.Value.RenderTimeout, logger))
                    return;
            }
            // Anything but the browser going away (then there's no one to send a page to).
            catch (Exception exception) when (!context.RequestAborted.IsCancellationRequested)
            {
                if (context.Response.HasStarted)
                {
                    // Part of the page has gone, so the browser can't be given another: end it, cut short.
                    logger.LogWarning(exception, "Rendering {Path} on the server failed part-way: ending the response.", context.Request.Path);
                    context.Abort();
                    return;
                }

                logger.LogWarning(exception, "Couldn't render {Path} on the server: sending the client-rendered app.", context.Request.Path);
                // Drop the Node server's status and headers, if they were copied before it failed.
                context.Response.Clear();
            }
        }

        await SendIndex(context);
    }

    /// <summary>
    /// Sends the Node server's page for the request. Returns false, having sent nothing, when the page should be the
    /// client-rendered app instead: the Node server answered with an error (5xx), or the request didn't make a URL on
    /// the Node server.
    /// </summary>
    private static async Task<bool> Forward(HttpContext context, Uri address, HttpClient client, TimeSpan timeout, ILogger logger)
    {
        // Only the path and query come from the request, so the host is always the Node server's: a path like
        // //evil.example/x is a path here, not another host.
        var target = new UriBuilder(address.Scheme, address.Host, address.Port)
        {
            Path = context.Request.Path.ToUriComponent(),
            Query = context.Request.QueryString.ToUriComponent(),
        }.Uri;
        if (target.Scheme != address.Scheme
            || !string.Equals(target.Host, address.Host, StringComparison.OrdinalIgnoreCase)
            || target.Port != address.Port)
        {
            logger.LogWarning("Not rendering {Path} on the server: it doesn't make a URL on the Node server.", context.Request.Path);
            return false;
        }

        using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), target);
        foreach (var name in ForwardedRequestHeaders)
        {
            if (context.Request.Headers.TryGetValue(name, out var value))
                request.Headers.TryAddWithoutValidation(name, value.ToArray());
        }
        // The page's links and redirects are for the address the browser used.
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", context.Request.Scheme);
        request.Headers.TryAddWithoutValidation("X-Forwarded-Host", context.Request.Host.Value);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        timeoutSource.CancelAfter(timeout);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutSource.Token);

        // The Node server's own error page isn't the app's: the client-rendered app may still work.
        if ((int)response.StatusCode >= StatusCodes.Status500InternalServerError)
        {
            logger.LogWarning("Couldn't render {Path} on the server ({Status}): sending the client-rendered app.", context.Request.Path, (int)response.StatusCode);
            return false;
        }

        // Redirects and not-found pages pass on as they are.
        context.Response.StatusCode = (int)response.StatusCode;
        foreach (var (name, values) in response.Headers.Concat(response.Content.Headers))
        {
            if (!HopByHopHeaders.Contains(name)) context.Response.Headers[name] = values.ToArray();
        }
        // The page holds the signed-in user's data (patients), so neither the browser nor anything between keeps it.
        context.Response.Headers.CacheControl = "no-store";

        // The page streams as it renders; the timeout only covered getting it started.
        await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
        return true;
    }

    private static async Task SendIndex(HttpContext context)
    {
        var index = context.RequestServices.GetRequiredService<IWebHostEnvironment>().WebRootFileProvider.GetFileInfo("index.html");
        if (!index.Exists)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(index, context.RequestAborted);
    }
}
