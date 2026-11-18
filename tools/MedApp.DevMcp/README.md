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
| Demo data | `reset_demo_data` (needs `confirm: true`): the same seeder as `tools/seed-demo-data.sh` |

### The test login: `admin` / `admin`

`ensure_test_admin` makes sure the local test login exists, with the Admin and User roles, and isn't locked out. It creates the account if it's missing, and otherwise puts the password back to `admin`. `reset_demo_data` runs it too, so the login is always there after a reset.

`admin` is far too weak for the app's password rules, so the tool sets the password hash directly instead of going through the app's commands. That's only acceptable because medapp-dev refuses to run against anything but a local database. Never create this account anywhere else.

## The demo data: one script

**All demo data is loaded by one script.** It replaces the local data with about 18 months of a synthetic community specialist centre. `docs/demo-data.md` is the guided tour: what's in it, and where to see each feature.

```bash
tools/seed-demo-data.sh --confirm --keep-users dev-parity --out demo-summary.json     # Git Bash, macOS, Linux
tools\seed-demo-data.ps1 --confirm --keep-users dev-parity --out demo-summary.json    # PowerShell
```

**What the script does,** in order:
1. builds this tool and the API in Release (so a running `medapp-dev` or AppHost, which use Debug, isn't in the way)
2. checks the database is a local one (`seed-demo --check`), before anything changes
3. applies any pending EF migrations (`dotnet ef database update`, with the `sa` password from the AppHost's user secrets)
4. runs the seeder (`seed-demo`, below): a `pre-reset-<timestamp>` snapshot, then the wipe and the replay

It takes about a minute. The summary (JSON: the generated passwords, the two-factor key and recovery codes, the records to open, the counts) goes to stdout, or to the file given by `--out`; everything else goes to stderr. Don't commit the summary.

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

**It then replays the centre's history** through the app's own rules, so everything matches what the app would have written:
- **The shared code the handlers use,** in memory: `PathwayChange` (statuses, stages, assignments, the audit), `PathwayCreation` (a pathway made from its template, and its Created entry), `StageFinishing`, `PathwayDetails` (details edits), `StageVisits` (the visit a stage asks for when it becomes current), `AppointmentChange` (booking, moving, cancelling, outcomes, notes), `AssignmentNotifications` and `AppointmentNotifications`, the monitor's own matching (`PathwayMonitor.FindMatchesAsync`, given the rules and admins as they were then), `PatientSearch.FiltersUsed`, the activity log's writer and its view window.
- **What it still writes itself:** the history's sign-in and search activity entries. `DemoActivityParityTests` (unit tests) runs the real sign-in and search commands and checks the seed writes the same actions, details and accounts.
- **The app's commands,** where only a handler is right: accounts, roles, email confirmation, two-factor, the password reset and change, notification choices, patients, monitoring rules, restriction and dispute, anonymisation, the retention "keep", exports, deleting pathways and the leaver's account, file uploads, deletes and downloads, settings, and today's sign-ins.
- **Visits are booked** into free times on the calendar of whoever is working on the stage (`ScheduleAllocator`, built from the calendars through the app's `CalendarProjection`), so nothing clashes. Every booking reserves its time first.
- **Calendar blocks and meetings obey the app's no-overlap rule** (`CalendarClashes`), checked as the app checks a new one: a day of leave is set to no day template first, a moved lunch goes only into free time, and a meeting's attendees are only those free then.
- **Templates are personal** (`DemoTemplates`): everyone's own day and week templates and default week, checked by the create commands' validators, with dated changes (a part-time week from 20 weeks ago, a template's lunch moved through `TemplateHistory`).
- **Meetings** (`DemoMeetings`) are replayed through the meeting commands' validators, the past-day guard and `MeetingNotifications` (`MeetingStory`): arranged, moved, people added or taken off, cancelled, each by an admin at its moment. Times to find are found with the app's `Availability`.
- **It runs on a service provider of its own** (`SeedServices`): its clock and activity actor are set before each call, so the handlers write history at the right moment and by the right person, and its email is dropped.

**What's in it** (the default seed `2026`; counts vary a little with the day it's run):
- **14 staff,** initial.surname usernames and invented names: `admin` (the clinical lead, a consultant physician), a service manager who's a second admin with two-factor, a consultant dermatologist, a GP with a special interest in skin, visiting gynaecology and urology consultants, two clinical nurse specialists (one part-time), a biomedical scientist (the lab), a podiatrist, a pharmacist, a booking coordinator, a healthcare assistant (locked out), and a new starter who must change their password. A leaver's account was deleted months ago. Passwords are generated and returned, with the two-factor key and recovery codes.
- **About 260 patients** (the weekly volumes of referrals decide it): mostly older adults, a few children, `@example.com` emails, 85% with an NHS number from the 999 test range. Women only on the post-menopausal bleeding pathway and men only on the prostate one, both from 50 up. Some are waiting for a referral; two are restricted, two disputed and one anonymised; imported records are due for the retention review, and two of them are kept.
- **14 stage definitions** with visit requirements (Required for clinic visits, biopsies and samples; Suggested for results, medicines reviews and follow-up; none for the lab and letters) and some SNOMED CT codes, plus a retired one. **7 pathway templates,** one archived (Skin lesion, its archived 2024 version, Inflammatory skin review, Post-menopausal bleeding, Prostate assessment, Diabetes review, Biomechanics course), each with its own weekly volume, seasons and stage times, so Insights differs for every period and template.
- **About 660 pathways** in every status, with stage notes that agree from stage to stage (one site and one outcome for each referral), blocks, cancellations, unlocks, edits, two deleted, and a full audit. **About 2,200 appointments** with their history. Calendars with everyone's own templates (the visiting consultants' hospital days, the lab's reporting days), leave and exceptions; about 220 meetings (weekly huddles and skin cancer team meetings, monthly governance and 1:1s, training, one-offs), some moved or cancelled; about 600 notifications and 12 monitoring rules; 60 days of activity log (about 15,000 entries); saved searches and pins; today's sessions.
- **Files** (about 43, two deleted: referral letters, histology reports, results, clinic and discharge letters, a lesion's site map and a flow test's trace), only when the AppHost's Azurite answers on `127.0.0.1:14331` (`DevMcp:Azurite` in `devmcp.json`, with Azurite's well-known development account, whose key is public). Otherwise the summary says files were skipped.

**The same seed gives the same data.** Times are relative to now, so it always looks current; today's diary is today's (on a weekend, Monday's). Today's sign-ins happen on working days from 07:30; the lockout happens on any day.

**The code** is in `DemoData/`. `DemoDataReset` is the way in for the script and the MCP tool, and `DemoDataSeeder` runs the steps. The story is in a few files: the staff (`DemoStaff`), the stage definitions and pathway templates (`DemoPathways`), the kinds of referral and how they turn out (`PathwayKinds`), the notes' wording (`NoteBuilder`), the weekly volumes (`VolumeCurves`), the calendar templates (`DemoTemplates`) and the meetings (`DemoMeetings`); the rest is the engine that replays it. The pure parts have unit tests in `MedApp.UnitTests/Tools/DemoData`: the helpers (`ScheduleAllocator`, `VolumeCurves`, `NoteBuilder`, `MeetingSchedule`), everyone's templates (`DemoTemplatesTests`), the calendars and meetings at a dozen moments (`DemoCalendarsTests`, `DemoMeetingsTests`), and the activity entries against the real commands (`DemoActivityParityTests`). `DemoDataSeedTests` in `MedApp.IntegrationTests` runs the whole seed on a database of its own at fixed moments (a Saturday, just after midnight the day after a clock change, and a Friday afternoon seeded twice) and checks its invariants.

## Running

`.mcp.json` starts the MCP server with `dotnet run --no-build` (build output would corrupt the stdio protocol), so **build it first**, and again after changing it or the app's Application/Infrastructure code:

```bash
dotnet build tools/MedApp.DevMcp
```

Claude Code picks up `.mcp.json` when a session starts and asks once before using a project server. Logs go to stderr, and stdout carries only the protocol.

It isn't part of `MedApp.sln`, but both test projects reference it: `MedApp.UnitTests` (its services and the demo data's pure helpers) and `MedApp.IntegrationTests` (the whole demo data set, `DemoDataSeedTests`), so CI builds it.
