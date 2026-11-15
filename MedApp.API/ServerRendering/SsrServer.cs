using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Options;

namespace MedApp.API.ServerRendering;

/// <summary>The page-rendering server, as page requests and the API's https redirect see it.</summary>
public interface ISsrServer
{
    /// <summary>Where the Node server is, once it's running; null until then, or if it isn't.</summary>
    Uri? Address { get; }

    /// <summary>Whether a request is the Node server's own call to the API: from this machine, with its key.</summary>
    bool IsFromRenderer(HttpContext context);
}

/// <summary>
/// Starts the Node server that renders pages (MedApp.Web's <c>server/server.mjs</c>, published to
/// <c>wwwroot/ssr/</c>) once the API is listening, on a free port on this machine only, and stops it with the API.
/// It tells the server where the API is (to fetch a page's data) and gives it a key for those calls (see
/// <see cref="IsFromRenderer"/>). If the server can't start or stops, pages fall back to client-side rendering.
/// </summary>
public sealed class SsrServer(
    IOptions<SsrOptions> options,
    IWebHostEnvironment environment,
    IServer server,
    IHostApplicationLifetime lifetime,
    ILogger<SsrServer> logger) : BackgroundService, ISsrServer
{
    /// <summary>The header the Node server sends its key in.</summary>
    public const string KeyHeader = "X-MedApp-Ssr-Key";

    private readonly string _key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    /// <summary>The key the Node server sends with its calls (<see cref="KeyHeader"/>).</summary>
    public string Key => _key;
    private Process? _process;
    private volatile Uri? _address;

    public Uri? Address => _address;

    public bool IsFromRenderer(HttpContext context) =>
        context.Connection.RemoteIpAddress is { } ip
        && IPAddress.IsLoopback(ip)
        && context.Request.Headers.TryGetValue(KeyHeader, out var key)
        && CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(key.ToString()),
            System.Text.Encoding.UTF8.GetBytes(_key));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        var script = Path.Combine(environment.WebRootPath ?? string.Empty, settings.ScriptPath);
        if (!settings.Enabled || environment.WebRootPath is null || !File.Exists(script))
        {
            logger.LogInformation("Server-side rendering is off ({Script} not found, or Ssr:Enabled is false): pages render in the browser.", script);
            return;
        }

        // The API's own address, once it's listening.
        await WhenStarted(stoppingToken);
        var apiUrl = ApiAddressFor(server.Features.Get<IServerAddressesFeature>()?.Addresses ?? []);
        if (apiUrl is null)
        {
            logger.LogWarning("Server-side rendering is off: the API has no address the Node server could reach.");
            return;
        }

        var port = FreePort();
        var start = new ProcessStartInfo(settings.NodePath, [script])
        {
            WorkingDirectory = Path.GetDirectoryName(script)!,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            Environment =
            {
                ["PORT"] = port.ToString(),
                ["HOST"] = "127.0.0.1",
                ["MEDAPP_API_URL"] = apiUrl,
                ["MEDAPP_SSR_KEY"] = _key,
                ["NODE_ENV"] = "production",
                // The API's address may be https with a certificate only this machine trusts (development's).
                ["NODE_OPTIONS"] = "--use-system-ca",
            },
        };

        try
        {
            _process = Process.Start(start) ?? throw new InvalidOperationException("Node didn't start.");
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Server-side rendering is off: couldn't start {Node}.", settings.NodePath);
            return;
        }

        _process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            logger.LogInformation("SSR: {Line}", e.Data);
            if (e.Data.StartsWith("MedApp pages:", StringComparison.Ordinal))
                _address = new Uri($"http://127.0.0.1:{port}/");
        };
        _process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null) logger.LogWarning("SSR: {Line}", e.Data);
        };
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        try
        {
            await _process.WaitForExitAsync(stoppingToken);
            logger.LogWarning("The server-side rendering server stopped (exit code {Code}): pages render in the browser.", _process.ExitCode);
        }
        catch (OperationCanceledException)
        {
            // The API is stopping.
        }
        finally
        {
            _address = null;
            if (!_process.HasExited) _process.Kill(entireProcessTree: true);
        }
    }

    /// <summary>
    /// The address the Node server should call the API on, from the API's own: http if it has one (no certificate
    /// to trust), on this machine (a wildcard host becomes 127.0.0.1). Null if it has none.
    /// </summary>
    public static string? ApiAddressFor(IEnumerable<string> addresses)
    {
        var uris = addresses
            .Select(a => a.Replace("://*", "://127.0.0.1").Replace("://+", "://127.0.0.1").Replace("://[::]", "://127.0.0.1").Replace("://0.0.0.0", "://127.0.0.1"))
            .Select(a => Uri.TryCreate(a, UriKind.Absolute, out var uri) ? uri : null)
            .OfType<Uri>()
            .ToList();
        var chosen = uris.FirstOrDefault(u => u.Scheme == Uri.UriSchemeHttp) ?? uris.FirstOrDefault();
        return chosen?.GetLeftPart(UriPartial.Authority);
    }

    private Task WhenStarted(CancellationToken stoppingToken)
    {
        var started = new TaskCompletionSource();
        lifetime.ApplicationStarted.Register(() => started.TrySetResult());
        stoppingToken.Register(() => started.TrySetCanceled());
        return started.Task;
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
