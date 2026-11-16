# medapp-dev MCP server

A **development-only** MCP server that lets an AI agent manage local accounts and app state while the React UI (`MedApp.Web`) is built. It's registered in the repo's `.mcp.json` as `medapp-dev`.

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
- **Synthetic data only.** This is a medical app: `seed_patients` invents names and uses `@example.com` emails. Never enter a real person's details.
- **Passwords.** `create_user` / `set_password` return any password they generate, so the agent can sign in with it. Only use this for local dev accounts.
- **No forced password change.** The app makes an account an admin creates or resets change its generated password at first sign-in (`MustChangePassword`). Accounts this tool makes or sets a password on aren't: `create_user`, `set_password`, `ensure_test_admin` and `reset_demo_data`'s staff can sign in and use the app straight away, except the demo's new starter, kept to show the forced change.
- **All or nothing.** `create_user` makes the account, sets its password and gives it its roles in one transaction, so a failure part-way leaves no account behind.

## Tools

| Area | Tools |
|---|---|
| State | `get_status`: database, migrations (applied/pending), row counts |
| Accounts | `list_users`, `list_roles`, `create_user`, `set_password`, `set_roles`, `unlock_user`, `delete_user`, `ensure_test_admin` |
| Sessions | `list_sessions`, `revoke_sessions` (sign an account out everywhere) |
| Patients | `list_patients`, `create_patient`, `seed_patients`, `delete_patient` |
| Snapshots | `save_snapshot`, `list_snapshots`, `restore_snapshot` (whole-database backup/restore, files in the git-ignored `.backups/`; the AppHost bind-mounts that folder into the SQL container at `/var/opt/mssql/backups`) |
| Demo data | `reset_demo_data` (needs `confirm: true`) |

### The test login: `admin` / `admin`

`ensure_test_admin` makes sure the local test login exists, with the Admin and User roles, and isn't locked out. It creates the account if it's missing, and otherwise puts the password back to `admin`. `reset_demo_data` runs it too, so the login is always there after a reset.

`admin` is far too weak for the app's password rules, so the tool sets the password hash directly instead of going through the app's commands. That's only acceptable because medapp-dev refuses to run against anything but a local database. Never create this account anywhere else.

### `reset_demo_data`: the demo clinic

One call replaces the local data with about 18 months of a synthetic clinic. `docs/demo-data.md` is the guided tour: what's in it, and where to see each feature.

**Run it like this locally:** `confirm: true` and `keepUsers: ["dev-parity"]`, so the UI parity tool's account survives. It saves a `pre-reset-<timestamp>` snapshot first. It takes about 30 seconds.

**It removes:**
- all patients, procedures, appointments, calendars (but the seeded activity types), stage definitions and templates, the audit and activity logs, sessions, notifications, monitoring rules and notification choices
- every attached file's record. The files' bytes stay in Azurite, unreferenced, so restoring the `pre-reset` snapshot still finds them.
- every account except the kept ones (`admin`, and `keepUsers`), with their settings

The built-in "Definition missing" stage is kept.

**It then replays the clinic's history** through the app's own rules, so everything matches what the app would have written:
- **The domain helpers the handlers use,** in memory: `ProcedureChange` (statuses, stages, assignments, the audit), `StageVisits` (the visit a stage asks for when it becomes current), `AppointmentChange` (booking, moving, cancelling, outcomes, notes), the assignment and appointment notifications, the monitor's rule matching, and the activity log's writer.
- **The app's commands,** where only a handler is right: accounts, roles, email confirmation, two-factor, the password reset and change, notification choices, patients, monitoring rules, restriction and dispute, anonymisation, the retention "keep", exports, deleting procedures and the leaver's account, file uploads, deletes and downloads, settings, and today's sign-ins.
- **Visits are booked** into free times on the calendar of whoever is working on the stage (`ScheduleAllocator`, built from the calendars through the app's `CalendarProjection`), so nothing clashes.
- **It runs on a service provider of its own** (`SeedServices`): its clock and activity actor are set before each call, so the handlers write history at the right moment and by the right person, and its email is dropped.

**What's in it** (the default seed `2026`; counts vary a little with the day it's run):
- **12 staff,** initial.surname usernames and invented names: `admin` (the clinical lead), a service manager who's a second admin with two-factor, two surgeons, an endoscopist, three nurses (one part-time), a physiotherapist, a booking coordinator, a healthcare assistant (locked out), and a new starter who must change their password. A leaver's account was deleted months ago. Passwords are generated and returned, with the two-factor key and recovery codes.
- **About 280 patients:** ages skewed older, a few children, `@example.com` emails, 85% with an NHS number from the 999 test range. Some are waiting for a referral; two are restricted, two disputed and one anonymised; imported records are due for the retention review, and two of them are kept.
- **14 stage definitions** with visit requirements and some SNOMED CT codes, plus a retired one. **6 templates,** one archived, each with its own weekly volume and stage times, so Insights differs for every period and template.
- **About 650 procedures** in every status, with stage notes, blocks, cancellations, unlocks, edits, two deleted, and a full audit. **About 2,500 appointments** with their history. Calendars with templates, leave and exceptions; notifications and monitoring rules; 60 days of activity log; saved searches and pins; today's sessions.
- **Files** (about 36, two deleted), only when the AppHost's Azurite answers on `127.0.0.1:14331` (`DevMcp:Azurite` in `devmcp.json`, with Azurite's well-known development account, whose key is public). Otherwise the summary says files were skipped.

**The same seed gives the same data.** Times are relative to now, so it always looks current; today's diary is today's (on a weekend, Monday's).

**The code** is in `DemoData/`. `DemoDataSeeder` runs the steps. The pure helpers (`ScheduleAllocator`, `VolumeCurves`, `NoteBuilder`) have unit tests in `MedApp.UnitTests/Tools/DemoData`, and `DemoDataSeedTests` in `MedApp.IntegrationTests` runs the whole seed on a database of its own and checks its invariants.

## Running

`.mcp.json` starts it with `dotnet run --no-build` (build output would corrupt the stdio protocol), so **build it first**, and again after changing it or the app's Application/Infrastructure code:

```bash
dotnet build tools/MedApp.DevMcp
```

Claude Code picks up `.mcp.json` when a session starts and asks once before using a project server. Logs go to stderr, and stdout carries only the protocol.

It isn't part of `MedApp.sln`, but both test projects reference it: `MedApp.UnitTests` (its services and the demo data's pure helpers) and `MedApp.IntegrationTests` (the whole demo data set, `DemoDataSeedTests`), so CI builds it.
