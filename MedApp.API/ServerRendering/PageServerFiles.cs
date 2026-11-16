namespace MedApp.API.ServerRendering;

/// <summary>
/// The paths browsers never get, which no page or file of the app has:
/// <list type="bullet">
/// <item>
/// Any path with an empty segment (<c>//</c>). To a URL resolver, a path starting with <c>//</c> names another host,
/// and it would get past the checks below.
/// </item>
/// <item>
/// <c>/ssr</c> and <c>/.vite</c>, as defence in depth. The page server and the client build's manifest used to sit in
/// wwwroot there; the image now keeps them in <c>/app/ssr/</c>, outside the web root, where no static file path
/// reaches them on any host (this string check alone could be got round on Windows). It still covers a web root
/// filled by hand from a whole <c>dist/</c> folder.
/// </item>
/// </list>
/// </summary>
public static class PageServerFiles
{
    public static bool IsBlocked(PathString path) =>
        path.StartsWithSegments("/ssr")
        || path.StartsWithSegments("/.vite")
        || (path.Value?.Contains("//", StringComparison.Ordinal) ?? false);
}
