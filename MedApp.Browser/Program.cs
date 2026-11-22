using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using System.Text.Json;
using MedApp.Browser.Hosting;

[assembly: System.Runtime.Versioning.SupportedOSPlatform("browser")]

Console.WriteLine("MedApp backend loaded");

/// <summary>
/// What the page calls (<c>getAssemblyExports</c>): <see cref="StartAsync"/> once, then <see cref="FetchAsync"/> for
/// each <c>/api</c> request, as <c>fetch</c> would send it to the API.
/// </summary>
public static partial class MedAppBackend
{
    private const string DatabasePath = "/medapp/medapp.db";

    private static BrowserBackend? _backend;

    /// <summary>
    /// Starts the backend on a database: the file's bytes the page gives (the demo database, or the visitor's saved
    /// copy of it), or, given none, a new empty one. Returns how long it took, as JSON: <c>totalMs</c>, and each
    /// part's milliseconds.
    /// </summary>
    [JSExport]
    public static async Task<string> StartAsync(byte[] database)
    {
        var clock = Stopwatch.StartNew();
        if (_backend is null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
            if (database.Length > 0)
                await File.WriteAllBytesAsync(DatabasePath, database);
            _backend = await BrowserBackend.StartAsync(DatabasePath, CancellationToken.None);
        }

        return JsonSerializer.Serialize(new Dictionary<string, long>(_backend.StartTimings) { ["totalMs"] = clock.ElapsedMilliseconds });
    }

    /// <summary>
    /// One request: its method, URL (from <c>/api</c>, or absolute), headers as a JSON object of strings, and body
    /// (empty for none). Returns the response as JSON: <c>status</c>, <c>headers</c>, <c>body</c> as text for text
    /// and JSON, otherwise <c>bodyBase64</c>, and <c>changed</c>, whether the database changed (reads change it too:
    /// the activity log), so the page saves it (<see cref="ExportDatabase"/>).
    /// </summary>
    [JSExport]
    public static async Task<string> FetchAsync(string method, string url, string headersJson, byte[] body)
    {
        var backend = _backend ?? throw new InvalidOperationException("The backend hasn't been started (StartAsync).");
        var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(string.IsNullOrEmpty(headersJson) ? "{}" : headersJson)!;
        var response = await backend.Pipeline.SendAsync(
            new ApiRequest(method, url, new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase), body),
            CancellationToken.None);

        var isText = response.Headers.TryGetValue("Content-Type", out var type)
                     && (type.Contains("json", StringComparison.OrdinalIgnoreCase) || type.StartsWith("text/", StringComparison.OrdinalIgnoreCase));
        return JsonSerializer.Serialize(new
        {
            status = response.Status,
            headers = response.Headers,
            body = isText ? Encoding.UTF8.GetString(response.Body) : null,
            bodyBase64 = isText || response.Body.Length == 0 ? null : Convert.ToBase64String(response.Body),
            changed = backend.DataChanged(),
        });
    }

    /// <summary>The database file's bytes, for the page to save: called between requests, never while one is served.</summary>
    [JSExport]
    public static byte[] ExportDatabase() =>
        (_backend ?? throw new InvalidOperationException("The backend hasn't been started (StartAsync).")).Export();
}
