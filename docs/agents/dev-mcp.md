# Dev MCP server (`medapp-dev`)

The `medapp-dev` tool and the demo data: read before using it beyond its tool descriptions, or changing it or the seed.

`.mcp.json` registers `medapp-dev` (`tools/MedApp.DevMcp`) and the Astryx docs MCP (`astryx`).

**Use `medapp-dev` for local account and app-state chores,** not hand-written SQL or one-off programs: dev accounts (create, reset, unlock, roles, sign out), synthetic patients, migrations and row counts, snapshots of the whole local database, and the demo data (`reset_demo_data`). It only runs against a local database (by default the AppHost's SQL container, with the password from the AppHost's user secrets, so the AppHost must have run once). It protects `admin`; destructive tools need `confirm: true`.
- **Before a destructive experiment:** take a `save_snapshot`.
- **Build it first:** `dotnet build tools/MedApp.DevMcp` (`.mcp.json` runs it with `--no-build`).
- **While it runs, `dotnet build MedApp.sln` fails** ("file is locked by: MedApp.DevMcp"): the test projects reference the tool, which isn't in the solution, so a solution build rebuilds it as Debug into the running tool's folder. Build and test in Release (`dotnet build MedApp.UnitTests -c Release`). After a change to the app's rules or the seed, stop it in `/mcp`, run `dotnet build tools/MedApp.DevMcp`, and start it again.
- **`reset_demo_data`** (`tools/MedApp.DevMcp/DemoData`) wipes the data and replays 18 months of a synthetic clinic (staff, about 260 patients, 650 procedures and 2,500 appointments, calendars, notifications, activity and, when Azurite answers, files). History goes through the handlers' shared code (`ProcedureChange`, `ProcedureCreation`, `StageFinishing`, `ProcedureDetails`, `StageVisits`, `AppointmentChange`, `AssignmentNotifications`, `AppointmentNotifications`, the monitor's `FindMatchesAsync`, `PatientSearch.FiltersUsed`, the activity log's writer), or a handler's commands where only that is right, with a clock the seed moves. It returns the generated passwords, two-factor key and recovery codes. Locally, pass `keepUsers: ["dev-parity"]`. What's in it: `docs/demo-data.md`.
  - **When the app's rules change,** shared code changes the demo data with it. But the seed writes its sign-in and search activity entries itself: `DemoActivityParityTests` (unit) fails if the real commands write something else, so change the seed's copy with them.
  - **Its tests:** `DemoDataSeedTests` (integration) runs the seed at awkward fixed moments (a weekend, after a clock change, twice in a row) and checks it obeys the app's rules (visits, clashes, nothing out of time, repeatable counts); `DemoCalendarsTests` checks calendars at a dozen moments.
- **Host services** the app's layers need are registered in `DevMcpServices` (activity log actor `medapp-dev`, no live notifications). `DevMcpServicesTests` builds them with validation on, so a newly needed service fails there, not at start.
- **Docs:** `tools/MedApp.DevMcp/README.md`.
