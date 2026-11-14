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
- **Destructive tools** (`delete_user`, `delete_patient`, `restore_snapshot`) do nothing unless called with `confirm: true`.
- **`restore_snapshot`** first saves the current database as `pre-restore-<timestamp>`.
- **Synthetic data only.** This is a medical app: `seed_patients` invents names and uses `@example.com` emails. Never enter a real person's details.
- **Passwords.** `create_user` / `set_password` return any password they generate, so the agent can sign in with it. Only use this for local dev accounts.

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

`reset_demo_data` saves a `pre-reset-<timestamp>` snapshot first.

**It removes:**
- all patients, procedures (with their audit log), stage definitions and templates, and sessions
- every account except the protected ones (`admin`)

The built-in "Definition missing" stage is kept.

**It then creates synthetic demo data:**
- 6 staff accounts, all role `User`: `admin` is the only Admin account. Their generated passwords are returned, and `set_password` gives any of them a new one.
- 60 patients, with ages skewed older and `@example.com` emails
- 10 stage definitions (referral, consultation, pre-assessment, consent, scheduling, procedure, recovery, review, results review and follow-up), plus 1 archived one
- 3 templates (surgical, diagnostic and minor procedure) that repeat stages, e.g. Consultation before and after the procedure, plus 1 archived template
- about 80 procedures across every status, with priorities, due dates (some overdue), assignees (including `admin`, so its Home task list isn't empty), stage notes, and stage progress that matches each status; a few have a stage an admin has unlocked
- a full audit trail for every procedure, written through the app's own `ProcedureChange`, so it matches what the app would record

The default seed `2026` always gives the same people and procedures. Timestamps are relative to now, so the data always looks current.

## Running

`.mcp.json` starts it with `dotnet run --no-build` (build output would corrupt the stdio protocol), so **build it first**, and again after changing it or the app's Application/Infrastructure code:

```bash
dotnet build tools/MedApp.DevMcp
```

Claude Code picks up `.mcp.json` when a session starts and asks once before using a project server. Logs go to stderr, and stdout carries only the protocol.

It isn't part of `MedApp.sln`, so CI and `dotnet test` ignore it.
