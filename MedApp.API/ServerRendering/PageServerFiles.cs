namespace MedApp.API.ServerRendering;

/// <summary>
/// The paths browsers never get: the page-rendering server and the build's manifest, which sit in wwwroot beside the
/// app, and any path with an empty segment (<c>//</c>), which no page or file of the app has. A path starting with
/// <c>//</c> would otherwise get past the <c>/ssr</c> check, and to a URL resolver it names another host.
/// </summary>
public static class PageServerFiles
{
    public static bool IsBlocked(PathString path) =>
        path.StartsWithSegments("/ssr")
        || path.StartsWithSegments("/.vite")
        || (path.Value?.Contains("//", StringComparison.Ordinal) ?? false);
}
