namespace MedApp.API.ServerRendering;

/// <summary>
/// Server-side rendering (the "Ssr" configuration section): the Node server that renders the React app's pages,
/// which the API starts and forwards page requests to. Without it (no Node, or no built server in wwwroot), pages
/// are rendered in the browser, as before.
/// </summary>
public sealed class SsrOptions
{
    public const string Section = "Ssr";

    /// <summary>Whether to start the Node server at all. On by default; it's only started if it's been built.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>The Node executable: "node" (on the PATH) unless set.</summary>
    public string NodePath { get; set; } = "node";

    /// <summary>The server's script, relative to the web root (where publishing puts the built app).</summary>
    public string ScriptPath { get; set; } = "ssr/server.mjs";

    /// <summary>How long a page may take to render before the browser gets the client-rendered app instead.</summary>
    public TimeSpan RenderTimeout { get; set; } = TimeSpan.FromSeconds(15);
}
