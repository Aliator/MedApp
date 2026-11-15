using Microsoft.Extensions.Options;

namespace MedApp.API.ServerRendering;

/// <summary>
/// Serves the React app's pages: rendered by the Node server (<see cref="SsrServer"/>) while it's running, with the
/// request's cookie so the page is the signed-in user's, and otherwise the client-rendered app (index.html).
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
                await Forward(context, address, clients.CreateClient(ClientName), options.Value.RenderTimeout);
                return;
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !context.RequestAborted.IsCancellationRequested)
            {
                logger.LogWarning(exception, "Couldn't render {Path} on the server: sending the client-rendered app.", context.Request.Path);
            }
        }

        await SendIndex(context);
    }

    private static async Task Forward(HttpContext context, Uri address, HttpClient client, TimeSpan timeout)
    {
        using var request = new HttpRequestMessage(
            new HttpMethod(context.Request.Method),
            new Uri(address, context.Request.Path + context.Request.QueryString));
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

        context.Response.StatusCode = (int)response.StatusCode;
        foreach (var (name, values) in response.Headers.Concat(response.Content.Headers))
        {
            if (!HopByHopHeaders.Contains(name)) context.Response.Headers[name] = values.ToArray();
        }

        // The page streams as it renders; the timeout only covered getting it started.
        await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
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
