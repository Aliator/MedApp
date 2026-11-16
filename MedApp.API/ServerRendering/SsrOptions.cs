namespace MedApp.API.ServerRendering;

/// <summary>
/// Server-side rendering (the "Ssr" configuration section): the Node server that renders the React app's pages, which
/// the API forwards page requests to. It's either a page server running on its own (<see cref="ServerUrl"/>: in
/// Kubernetes, a container beside the API's, in the same pod), or one the API starts itself, inside its own
/// container. With neither (no Node, or no built server), pages are rendered in the browser.
/// </summary>
public sealed class SsrOptions
{
    public const string Section = "Ssr";

    /// <summary>The message startup fails with when <see cref="ServerUrl"/> is set but isn't a server's address.</summary>
    public const string InvalidServerUrl =
        "Ssr:ServerUrl must be an http or https address with no path, query or user name, such as http://127.0.0.1:8082.";

    /// <summary>Whether to render pages on the server at all. On by default.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The address of a page server that runs on its own, such as <c>http://127.0.0.1:8082</c> (the Helm chart's
    /// pages container, in the API's pod). When it's set, the API forwards page requests there and doesn't start
    /// Node itself. Only a scheme (http or https), a host and a port: page requests keep their own path and query.
    /// </summary>
    public string? ServerUrl { get; set; }

    /// <summary>The Node executable, when the API starts the page server itself: "node" (on the PATH) unless set.</summary>
    public string NodePath { get; set; } = "node";

    /// <summary>
    /// The page server's script, when the API starts it itself, relative to the content root. The image puts it in
    /// <c>/app/ssr/</c>, outside the web root (<c>/app/wwwroot</c>), so no static file path can reach it.
    /// </summary>
    public string ScriptPath { get; set; } = "ssr/server.mjs";

    /// <summary>How long a page may take to render before the browser gets the client-rendered app instead.</summary>
    public TimeSpan RenderTimeout { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// <see cref="ServerUrl"/> as the page server's address (ending in <c>/</c>), or null when it's empty or isn't an
    /// http or https address of a server alone. A path, query, fragment or user name would be dropped from every page
    /// request without a word, so they're refused (<see cref="HasValidServerUrl"/> fails startup).
    /// </summary>
    public Uri? ExternalServer()
    {
        if (string.IsNullOrWhiteSpace(ServerUrl)
            || !Uri.TryCreate(ServerUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || uri.AbsolutePath != "/"
            || uri.Query.Length > 0
            || uri.Fragment.Length > 0
            || uri.UserInfo.Length > 0)
        {
            return null;
        }
        return new Uri(uri.GetLeftPart(UriPartial.Authority) + "/");
    }

    /// <summary>Whether <see cref="ServerUrl"/> is empty, or a server's address (<see cref="ExternalServer"/>).</summary>
    public bool HasValidServerUrl() => string.IsNullOrWhiteSpace(ServerUrl) || ExternalServer() is not null;
}
