using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MedApp.DevMcp.DemoData;

/// <summary>
/// A clock the seed moves: the app's handlers read "now" from <see cref="TimeProvider"/>, so setting it before a call
/// replays that call at a moment in the demo's history, as if it had happened then.
/// </summary>
public sealed class SeedClock(DateTime utcNow) : TimeProvider
{
    public DateTime UtcNow { get; set; } = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);

    public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(UtcNow, DateTimeKind.Utc));
}

/// <summary>The activity log's actor while seeding: whoever the seed is acting as, set before each call.</summary>
public sealed class SeedActivityContext : IActivityContext
{
    public ActivityActor Actor { get; set; } = new(null, DevToolActivityContext.Username, null);
}

/// <summary>
/// The app's services as the seed uses them: the same as the tool's (<see cref="DevMcpServices.AddDevMcpCore"/>), but
/// with a clock and an activity actor the seed controls, so handlers it calls write history at the right moment and
/// by the right person. A provider of its own, apart from the tool's, so nothing else sees the moved clock.
/// </summary>
public sealed class SeedServices : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    public SeedServices(IConfiguration configuration, DateTime nowUtc)
    {
        Clock = new SeedClock(nowUtc);
        Context = new SeedActivityContext();

        var overlay = new ConfigurationBuilder()
            .AddConfiguration(configuration)
            // Without a base for links, the app makes no confirmation link and records no address request. Nothing is
            // sent: email goes to DiscardedEmail.
            .AddInMemoryCollection(configuration["Email:LinkBaseUrl"] is { Length: > 0 }
                ? []
                : [new KeyValuePair<string, string?>("Email:LinkBaseUrl", "http://localhost:5173")])
            .Build();

        var services = new ServiceCollection()
            .AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning))
            .AddSingleton<IConfiguration>(overlay)
            .AddDevMcpCore(overlay);

        // Registered last, so these win over the tool's own.
        services.AddSingleton<TimeProvider>(Clock);
        services.AddSingleton<IActivityContext>(Context);
        services.AddSingleton<IEmailSender, DiscardedEmail>();
        _provider = services.BuildServiceProvider();
    }

    public SeedClock Clock { get; }

    public SeedActivityContext Context { get; }

    /// <summary>Acts as someone, at a moment: what the next handler call records as who and when.</summary>
    public void At(DateTime utc, ActivityActor actor)
    {
        Clock.UtcNow = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        Context.Actor = actor;
    }

    /// <summary>A scope of the app's services, as one request would have.</summary>
    public AsyncServiceScope Scope() => _provider.CreateAsyncScope();

    /// <summary>Runs work in a scope of its own (a request), returning its result.</summary>
    public async Task<T> Run<T>(Func<IServiceProvider, Task<T>> work)
    {
        await using var scope = Scope();
        return await work(scope.ServiceProvider);
    }

    public async Task Run(Func<IServiceProvider, Task> work)
    {
        await using var scope = Scope();
        await work(scope.ServiceProvider);
    }

    public ValueTask DisposeAsync() => _provider.DisposeAsync();
}

/// <summary>
/// The seed's email: the app's account commands send confirmation links and security notices as they would for real,
/// and refuse an address when no email can be sent, so this says it can, and drops every message. Nothing leaves.
/// </summary>
public sealed class DiscardedEmail : IEmailSender
{
    public bool CanSend => true;

    public Task SendAsync(EmailMessage message, CancellationToken ct) => Task.CompletedTask;
}
