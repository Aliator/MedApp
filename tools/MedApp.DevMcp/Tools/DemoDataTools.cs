using System.ComponentModel;
using MedApp.DevMcp.DemoData;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Server;

namespace MedApp.DevMcp.Tools;

/// <summary>
/// Wipes the local database's data and fills it with the demo clinic (<see cref="DemoDataSeeder"/>). This is only the
/// tool's contract: confirmation, the pre-reset snapshot and the kept accounts. Same seed, same data (timestamps are
/// relative to "now", so the data always looks current).
/// </summary>
[McpServerToolType]
public sealed class DemoDataTools
{
    [McpServerTool(Name = "reset_demo_data", Destructive = true)]
    [Description("WIPES all patients, procedures, appointments, calendars, attached files' records (not their bytes in Azurite, so the pre-reset snapshot keeps its files), stage definitions and templates, the audit and activity logs, sessions, notifications, monitoring rules, notification choices, and every account except the kept ones (admin, plus keepUsers; pass [\"dev-parity\"] locally to keep the UI parity tool's account). Saves a 'pre-reset-<timestamp>' snapshot first. Needs confirm: true. Then it makes sure admin / admin exists (the clinical lead, Admin and User) and replays about 18 months of a synthetic clinic through the app's own rules: 11 staff besides admin (initial.surname, generated passwords returned; a second admin with two-factor, whose authenticator key and recovery codes are returned; a new starter who must change their password; a locked-out account; a deleted leaver), about 280 patients (999-range NHS numbers, example.com emails; restricted, disputed, anonymised and retention-review records), 14 stage definitions with visit requirements and some SNOMED CT codes, 6 templates (one archived) with distinct weekly volumes, about 650 procedures in every status with notes, visits and a full audit trail, calendars with templates, leave and exceptions, about 2,500 appointments with their history, notifications and monitoring rules, 60 days of activity log, today's sessions, saved searches and pins, and (when the AppHost's Azurite answers on 127.0.0.1:14331) attached files. Takes about 30 seconds. Its summary lists the passwords, the records to open (tour) and the counts. docs/demo-data.md is the guided tour.")]
    public static async Task<object> ResetDemoData(
        IConfiguration configuration,
        DevGuard guard,
        DevMcpOptions options,
        [Description("Must be true: this deletes data.")] bool confirm = false,
        [Description("Random seed; the default 2026 gives the standard demo data set.")] int seed = 2026,
        [Description("Extra accounts to keep, besides the protected ones. Default: none. Locally, pass [\"dev-parity\"].")] string[]? keepUsers = null,
        CancellationToken ct = default)
    {
        Guards.RequireConfirm(confirm, "Resetting the database to demo data");

        var snapshot = $"pre-reset-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
        await DatabaseTools.SaveSnapshot(guard, options, snapshot, ct);

        var summary = await DemoDataSeeder.RunAsync(
            configuration,
            new DemoSeedOptions(seed, DateTime.UtcNow, options.ProtectedUsers.Concat(keepUsers ?? []).ToArray()),
            ct);
        summary["snapshotBeforeReset"] = snapshot;
        return summary;
    }
}
