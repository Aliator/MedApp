using System.ComponentModel;
using MedApp.DevMcp.DemoData;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Server;

namespace MedApp.DevMcp.Tools;

/// <summary>
/// Wipes the local database's data and fills it with the demo centre (<see cref="DemoDataSeeder"/>). This is only the
/// tool's contract, which it shares with the terminal command (<see cref="DemoDataReset"/>): confirmation, the
/// pre-reset snapshot and the kept accounts. Same seed, same data (timestamps are relative to "now", so the data always
/// looks current).
/// </summary>
[McpServerToolType]
public sealed class DemoDataTools
{
    [McpServerTool(Name = "reset_demo_data", Destructive = true)]
    [Description("WIPES all patients, procedures, appointments, calendars, attached files' records (not their bytes in Azurite, so the pre-reset snapshot keeps its files), stage definitions and templates, the audit and activity logs, sessions, notifications, monitoring rules, notification choices, the admins' sign-in rules (back to none), and every account except the kept ones (admin, plus keepUsers; pass [\"dev-parity\"] locally to keep the UI parity tool's account). Saves a 'pre-reset-<timestamp>' snapshot first. Needs confirm: true, and a database with every migration applied. It runs the same code as tools/seed-demo-data.sh (the terminal's way in, which applies migrations first). Then it makes sure admin / admin exists (the clinical lead, Admin and User) and replays about 18 months of a synthetic community specialist centre (referral-led outpatient clinics with their own lab) through the app's own rules: 12 staff besides admin (initial.surname, generated passwords returned; a second admin with two-factor, whose authenticator key and recovery codes are returned; a new starter who must change their password; a locked-out account; a deleted leaver), about 260 patients (unique names, 999-range NHS numbers, example.com emails; restricted, disputed, anonymised and retention-review records), 15 stage definitions (one retired) with visit requirements and some SNOMED CT codes, 7 pathway templates (one archived) with distinct weekly volumes, about 650 to 670 procedures in every status with notes, visits and a full audit trail, calendars with templates, leave and exceptions, about 2,100 to 2,300 appointments with their history, notifications and monitoring rules, 60 days of activity log, today's sessions (on working days from 07:30), saved searches and pins, and (when the AppHost's Azurite answers on 127.0.0.1:14331) attached files. Takes 30 seconds to a minute. Its summary lists the passwords, the records to open (tour) and the counts. docs/demo-data.md is the guided tour.")]
    public static async Task<object> ResetDemoData(
        IConfiguration configuration,
        DevGuard guard,
        DevMcpOptions options,
        [Description("Must be true: this deletes data.")] bool confirm = false,
        [Description("Random seed; the default 2026 gives the standard demo data set.")] int seed = DemoDataReset.StandardSeed,
        [Description("Extra accounts to keep, besides the protected ones. Default: none. Locally, pass [\"dev-parity\"].")] string[]? keepUsers = null,
        CancellationToken ct = default) =>
        // The same code the terminal command runs: confirmation, migrations applied, the snapshot, then the seed.
        await DemoDataReset.RunAsync(configuration, guard, options, confirm, seed, keepUsers, snapshot: true, ct);
}
