# medapp-dev MCP server

A **development-only** MCP server that lets an AI agent manage local accounts and app state while the React UI (`MedApp.Web`) is built. It's registered in the repo's `.mcp.json` as `medapp-dev`.

It's also the **demo data seeder**, which runs from a terminal with no agent: [`tools/seed-demo-data.sh`](#the-demo-data-one-script) is the only way demo data gets into a database.

It reuses the app's own Application and Infrastructure layers, so the app's rules apply:
- Accounts are created and given roles through the app's MediatR commands.
- Patients go through `CreatePatient` / `DeletePatient`, with the API's FluentValidation rules.
- Passwords are hashed by the same ASP.NET Identity setup.

## Safety

- **Local database only.** It **refuses to start** unless the database is LocalDB or a localhost SQL Server.
  - By default it connects to the Aspire AppHost's SQL container, using the connection string in `devmcp.json` (`DevMcp:SqlContainer`) and the `sa` password from the AppHost's user secrets. So the AppHost must have run once, and the container must be running (it's persistent, so it usually is).
  - A `ConnectionStrings:DefaultConnection` in the API's `appsettings*.json` or an environment variable takes precedence.
- **Protected accounts.** Accounts listed in `devmcp.json` → `DevMcp:ProtectedUsers` (default `admin`) can't be deleted, have their password changed, or lose roles.
- **Destructive tools** (`delete_user`, `delete_patient`, `restore_snapshot`, `reset_demo_data`) do nothing unless called with `confirm: true`.
- **`restore_snapshot`** first saves the current database as `pre-restore-<timestamp>`.
- **Synthetic data only.** This is a medical app: the seeder and `seed_patients` invent names and use `@example.com` emails. Never enter a real person's details.
- **Demo data comes only from the script.** `seed_patients` and `create_patient` make a throwaway record or two while trying a screen. They aren't demo data: anything a demo, a screenshot or a snapshot should show belongs in the seeder (`DemoData/`), loaded by `tools/seed-demo-data.sh`. Never add demo or dummy records by hand, with SQL or through the app.
- **Passwords.** `create_user` / `set_password` return any password they generate, so the agent can sign in with it. Only use this for local dev accounts.
- **No forced password change.** The app makes an account an admin creates or resets change its generated password at first sign-in (`MustChangePassword`). Accounts this tool makes or sets a password on aren't: `create_user`, `set_password`, `ensure_test_admin`, `ensure_test_staff` and `reset_demo_data`'s staff can sign in and use the app straight away, except the demo's new starter, kept to show the forced change.
- **All or nothing.** `create_user` makes the account, sets its password and gives it its roles in one transaction, so a failure part-way leaves no account behind.

## Tools

| Area | Tools |
|---|---|
| State | `get_status`: database, migrations (applied/pending), row counts |
| Accounts | `list_users`, `list_roles`, `create_user`, `set_password`, `set_roles`, `unlock_user`, `delete_user`, `ensure_test_admin`, `ensure_test_staff` |
| Sessions | `list_sessions`, `revoke_sessions` (sign an account out everywhere) |
| Patients | `list_patients`, `create_patient`, `seed_patients`, `delete_patient` |
| Snapshots | `save_snapshot`, `list_snapshots`, `restore_snapshot` (whole-database backup/restore, files in the git-ignored `.backups/`; the AppHost bind-mounts that folder into the SQL container at `/var/opt/mssql/backups`) |
| Jobs | `run_job`: runs one of the API's scheduled jobs now (e.g. `operational-purge`), with the API's runner and lease (`docs/agents/jobs.md`); the purge is a dry run unless `Retention:Operational:DryRun` is false |
| Demo data | `reset_demo_data` (needs `confirm: true`): the same seeder as `tools/seed-demo-data.sh` |

### The test login: `admin` / `admin`

`ensure_test_admin` makes sure the local test login exists, with the Admin role alone (admins are read only and in no team, as the light demo's admin is), and isn't locked out. It creates the account if it's missing, and otherwise puts the password back to `admin`. `reset_demo_data` runs it too, so the login is always there after a reset.

`admin` is far too weak for the app's password rules, so the tool sets the password hash directly instead of going through the app's commands. That's only acceptable because medapp-dev refuses to run against anything but a local database. Never create this account anywhere else.

### Test staff: `ensure_test_staff`

Clinical work needs a member of staff in the right team. `ensure_test_staff` makes `<prefix>-clinician` (Clinician and Booking, Dermatology's clinic admin) and `<prefix>-lab` (Lab and Pathologist, in the lab team), named, through the app's commands; it makes Dermatology too if the database has none. Run again, it sets their password, unlocks them and puts their roles and team back exactly. The prefix is `dev` by default (`dev-parity` for the UI parity tool); the password is the one given or a generated one each, returned. `reset_demo_data` remakes the teams, so run it again after a reset. The E2E tests don't need it: they make their own staff account.

## The demo data: one script

**All demo data is loaded by one script.** It replaces the local data with the light demo's clinic (Dermatology and the lab), replayed through the app's handlers. `docs/demo-data.md` is the guided tour: what's in it, and where to see each feature.

```bash
tools/seed-demo-data.sh --confirm --keep-users dev-parity --out demo-summary.json     # Git Bash, macOS, Linux
tools\seed-demo-data.ps1 --confirm --keep-users dev-parity --out demo-summary.json    # PowerShell
```

**What the script does,** in order:
1. builds this tool and the API in Release (so a running `medapp-dev` or AppHost, which use Debug, isn't in the way)
2. checks the database is a local one (`seed-demo --check`), before anything changes
3. applies any pending EF migrations (`dotnet ef database update`, with the `sa` password from the AppHost's user secrets)
4. runs the seeder (`seed-demo`, below): a `pre-reset-<timestamp>` snapshot, then the wipe and the replay

It takes about a minute. The summary (JSON: the generated passwords, the records to open, the counts) goes to stdout, or to the file given by `--out`; everything else goes to stderr. Don't commit the summary.

### `seed-demo`: the seeder from a terminal

`dotnet run --project tools/MedApp.DevMcp -c Release -- seed-demo --confirm` runs the seeder without an MCP client. It's what the script runs, and it's the same code as the MCP tool `reset_demo_data` (`DemoDataReset`), under the same rules:
- **A local database only** (the guard above), and the protected accounts (`admin`) are always kept.
- **Nothing happens without `--confirm`** (exit code 2).
- **Every migration must be applied:** otherwise it refuses and names the script, which applies them.

| Option | What it does |
|---|---|
| `--confirm` | Go ahead. |
| `--keep-users a,b` | Accounts to keep, besides the protected ones. Locally: `dev-parity`, the UI parity tool's account. |
| `--seed <number>` | The random seed. The default, `2026`, is the standard demo data set. |
| `--out <file>` | Write the summary to a file instead of stdout. |
| `--no-snapshot` | Don't save the `pre-reset` snapshot (for a throwaway database, whose server has no backups folder). |
| `--save-snapshot <name>` | Save the loaded data as a snapshot afterwards, for `deploy/kind/up.sh --demo-data <name>`. |
| `--check` | Only say which database it would load and how many migrations are pending. Changes nothing. |

**Another local database** (a throwaway one for a test, say): set `ConnectionStrings__DefaultConnection` before running the script or the command. Set `ConnectionStrings__Attachments` too, or the files go to the AppHost's Azurite.

**From Claude Code,** `reset_demo_data` with `confirm: true` and `keepUsers: ["dev-parity"]` does the same, without the migrations step.

**It removes:**
- all patients, pathways, appointments, meetings, calendars (but the seeded activity types), closed days, stage definitions and templates, the audit and activity logs, sessions, notifications, monitoring rules and notification choices
- every attached file's record. The files' bytes stay in Azurite, unreferenced, so restoring the `pre-reset` snapshot still finds them.
- every account except the kept ones (`admin`, and `keepUsers`), with their settings
- the admins' sign-in rules (Admin settings, Security), back to a new database's: none

The built-in "Definition missing" stage is kept.

**It then replays the light demo's clinic** through the app's own rules, so everything matches what the app would have written:
- **Through the app's handlers:** each action is sent as the person who did it (their permissions and team walls), at the moment the story puts it, on a service provider of its own (`SeedServices`, whose clock and activity actor it moves). So the gates, walls, audit entries, notifications and grants are the app's, and clinical work is never the admin's.
- **What it writes itself:** the stage library and templates (as the admin set them up), the relay's Triage gates (no command changes them), and the activity log's sign-ins and views. `DemoActivityParityTests` (unit tests) runs the real sign-in, search and referral commands and checks the seed's copy still matches.

**What's in it:** the light demo's clinic (`docs/demo-data.md`): Dermatology and the lab; `admin` (read only) and six staff with the light demo's usernames, roles and teams; Dermatology's eleven templates; 15 patients (999-range NHS numbers, `@example.com` emails) and 12 pathways at the light demo's points; the lab's cases and the hospital's requests; two MDTs, a consult, letters, consent, calendars and clinic sessions. Times are relative to now, so it always looks current.

**The code** is in `DemoData/`. `DemoDataReset` is the way in for the script and the MCP tool, and `DemoDataSeeder` runs the steps: `DemoPeople`, `DemoSetup` (with `DemoDermatology`'s templates), `DemoPatients`, `DemoStory` (the pathways, through `DemoReplay`'s verbs), `DemoLab` (the hospital's requests), `DemoClinic` (MDTs, calendars, service alerts) and `DemoActivity`.

## Running

`.mcp.json` starts the MCP server with `dotnet run --no-build` (build output would corrupt the stdio protocol), so **build it first**, and again after changing it or the app's Application/Infrastructure code:

```bash
dotnet build tools/MedApp.DevMcp
```

Claude Code picks up `.mcp.json` when a session starts and asks once before using a project server. Logs go to stderr, and stdout carries only the protocol.

It isn't part of `MedApp.sln`, but both test projects reference it: `MedApp.UnitTests` (its services and the demo data's pure helpers) and `MedApp.IntegrationTests` (the whole demo data set, `DemoDataSeedTests`), so CI builds it.
