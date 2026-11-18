using System.Globalization;
using System.Text.Json;
using FluentValidation;
using MedApp.DevMcp.DemoData;
using MedApp.DevMcp.Tools;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol;

namespace MedApp.DevMcp;

/// <summary>What <c>seed-demo</c> was asked to do.</summary>
/// <param name="Confirm">Go ahead: without it, nothing is changed.</param>
/// <param name="Check">Only say which database it would load, and whether its migrations are applied; change nothing.</param>
/// <param name="Snapshot">Save a <c>pre-reset-&lt;timestamp&gt;</c> snapshot first.</param>
/// <param name="Seed">The random seed.</param>
/// <param name="KeepUsers">Accounts to keep, besides the protected ones.</param>
/// <param name="Out">A file for the summary, instead of stdout.</param>
/// <param name="SaveSnapshot">A name to save the loaded data under afterwards (for <c>deploy/kind/up.sh --demo-data</c>), or null.</param>
/// <param name="Help">Only print the usage.</param>
public sealed record SeedDemoArguments(
    bool Confirm,
    bool Check,
    bool Snapshot,
    int Seed,
    IReadOnlyList<string> KeepUsers,
    string? Out,
    string? SaveSnapshot,
    bool Help)
{
    /// <summary>Reads the arguments after <c>seed-demo</c>. Null, with why, for anything it doesn't know.</summary>
    public static SeedDemoArguments? Parse(IReadOnlyList<string> args, out string? error)
    {
        error = null;
        var (confirm, check, snapshot, help) = (false, false, true, false);
        var seed = DemoDataReset.StandardSeed;
        var keep = new List<string>();
        string? output = null;
        string? saveAs = null;

        for (var i = 0; i < args.Count; i++)
        {
            string? Value() => i + 1 < args.Count ? args[++i] : null;

            switch (args[i])
            {
                case "--confirm":
                    confirm = true;
                    break;
                case "--check":
                    check = true;
                    break;
                case "--no-snapshot":
                    snapshot = false;
                    break;
                case "-h" or "--help":
                    help = true;
                    break;
                case "--keep-users":
                    if (Value() is not { } users)
                    {
                        error = "--keep-users needs a list of usernames, separated by commas.";
                        return null;
                    }

                    keep.AddRange(users.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    break;
                case "--seed":
                    if (!int.TryParse(Value(), NumberStyles.Integer, CultureInfo.InvariantCulture, out seed))
                    {
                        error = "--seed needs a whole number.";
                        return null;
                    }

                    break;
                case "--out":
                    if (Value() is not { Length: > 0 } path)
                    {
                        error = "--out needs a file path.";
                        return null;
                    }

                    output = path;
                    break;
                case "--save-snapshot":
                    if (Value() is not { Length: > 0 } name)
                    {
                        error = "--save-snapshot needs a name (letters, digits, - and _).";
                        return null;
                    }

                    saveAs = name;
                    break;
                default:
                    error = $"Unknown argument '{args[i]}'.";
                    return null;
            }
        }

        return new SeedDemoArguments(confirm, check, snapshot, seed, keep, output, saveAs, help);
    }
}

/// <summary>
/// <c>seed-demo</c>: the demo data from a terminal, with no MCP client. It runs what the MCP tool
/// <c>reset_demo_data</c> runs (<see cref="DemoDataReset"/>), under the same rules: a local database only, the
/// protected accounts kept, and nothing without <c>--confirm</c>. The summary (the generated passwords, the two-factor
/// key, the tour, the counts) is JSON on stdout, or in the file given by <c>--out</c>; everything else goes to stderr.
/// </summary>
public static class SeedDemoCli
{
    public const string Command = "seed-demo";

    public const int Succeeded = 0;
    public const int Failed = 1;
    public const int Refused = 2;

    public const string Usage =
        """
        Loads the demo data (docs/demo-data.md) into the local development database. It WIPES what's there.

          dotnet run --project tools/MedApp.DevMcp -c Release -- seed-demo --confirm [options]

          --confirm            Go ahead: without it, nothing is changed.
          --keep-users a,b     Accounts to keep, besides the protected ones (admin). Locally: dev-parity.
          --seed <number>      The random seed. The default, 2026, is the standard demo data set.
          --out <file>         Write the summary (JSON, with the generated passwords) to this file instead of stdout.
          --no-snapshot        Don't save a pre-reset-<timestamp> snapshot first (for a throwaway database).
          --save-snapshot <n>  Save the loaded data as a snapshot afterwards (deploy/kind/up.sh --demo-data <n> loads it).
          --check              Only say which database it would load and whether it's migrated. Changes nothing.

        It only runs against a local database: by default the AppHost's SQL container; ConnectionStrings__DefaultConnection
        names another. tools/seed-demo-data.sh (and .ps1) applies the migrations first, then runs this.
        """;

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static async Task<int> RunAsync(IReadOnlyList<string> args, TextWriter output, TextWriter log)
    {
        if (SeedDemoArguments.Parse(args, out var error) is not { } arguments)
        {
            await log.WriteLineAsync(error);
            await log.WriteLineAsync(Usage);
            return Refused;
        }

        if (arguments.Help)
        {
            await output.WriteLineAsync(Usage);
            return Succeeded;
        }

        using var cancel = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancel.Cancel();
        };

        try
        {
            var repoRoot = RepoRoot.Find();
            var configuration = new ConfigurationManager();
            DevMcpConfiguration.AddTo(configuration, repoRoot);
            // Refuses anything but a local database, before anything else happens.
            var guard = DevGuard.Check(configuration);
            var options = DevMcpConfiguration.Options(configuration, repoRoot);

            if (arguments.Check)
            {
                var pending = await DemoDataReset.PendingMigrationsAsync(configuration, cancel.Token);
                await output.WriteLineAsync(JsonSerializer.Serialize(
                    new { server = guard.Server, database = guard.Database, pendingMigrations = pending.Count }, Json));
                return Succeeded;
            }

            if (arguments.Confirm)
                await log.WriteLineAsync($"Loading the demo data into '{guard.Database}' on {guard.Server}...");
            var summary = await DemoDataReset.RunAsync(
                configuration, guard, options, arguments.Confirm, arguments.Seed, arguments.KeepUsers, arguments.Snapshot, cancel.Token);

            if (arguments.SaveSnapshot is { } name)
            {
                await DatabaseTools.SaveSnapshot(guard, options, name, cancel.Token);
                summary["snapshotAfterLoading"] = name;
            }

            var json = JsonSerializer.Serialize(summary, Json);
            if (arguments.Out is { } path)
            {
                await File.WriteAllTextAsync(path, json, cancel.Token);
                await output.WriteLineAsync(JsonSerializer.Serialize(new { summary = Path.GetFullPath(path), counts = summary["counts"] }, Json));
            }
            else
            {
                await output.WriteLineAsync(json);
            }

            return Succeeded;
        }
        catch (McpException e) when (!arguments.Confirm)
        {
            // Not confirmed: the same refusal the MCP tool gives, in the terminal's words.
            await log.WriteLineAsync(e.Message.Replace("Call again with confirm: true", "Run it again with --confirm"));
            return Refused;
        }
        catch (Exception e) when (e is McpException or InvalidOperationException or ValidationException)
        {
            await log.WriteLineAsync(e.Message);
            return Failed;
        }
        catch (OperationCanceledException)
        {
            await log.WriteLineAsync("Cancelled. The database may hold part of the demo data: run it again.");
            return Failed;
        }
    }
}
