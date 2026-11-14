# AGENTS.md

Guidance for AI coding agents working in this repository.

## Overview

MedApp is a patient and procedure management app. It has a .NET 10 Web API backend built with Clean Architecture, CQRS and MediatR, and a React frontend (`MedApp.Web`: React 19, TypeScript, Vite and Astryx). The original Blazor WebAssembly frontend is kept, deprecated, as `MedApp.Client.Deprecated`. Data is stored in SQL Server through EF Core, and ASP.NET Core Identity handles users. Authentication is a custom cookie-based session scheme.

## Commands

Run everything from the repo root. `global.json` pins the SDK to 10.0.x, and every project targets `net10.0`.

```bash
dotnet build MedApp.sln                     # build everything
dotnet test MedApp.UnitTests                # the only project with tests (CI runs this)
dotnet test MedApp.UnitTests --filter "FullyQualifiedName~UpdatePatient"   # run a subset

dotnet run --project MedApp.AppHost         # everything (needs Docker running): SQL Server container, API, React app, Aspire dashboard
#   React app:      http://localhost:5173   (MedApp.Web; its commands are in MedApp.Web/README.md)
#   API + Swagger:  https://localhost:7038/swagger
#   Dashboard:      the login URL the command prints

# EF Core migrations (dotnet-ef 10.x is installed as a global tool)
dotnet ef migrations add <Name> --project MedApp.Infrastructure --startup-project MedApp.API
dotnet ef migrations has-pending-model-changes --project MedApp.Infrastructure --startup-project MedApp.API   # model and snapshot in sync?

# Applying migrations to the dev database (the AppHost's SQL container) needs its connection string.
# The sa password is in the AppHost's user secrets.
PW=$(dotnet user-secrets list --project MedApp.AppHost | sed -n 's/^Parameters:sql-password = //p')
dotnet ef database update --project MedApp.Infrastructure --startup-project MedApp.API --connection "Server=127.0.0.1,14330;Database=MedApp;User ID=sa;Password=$PW;TrustServerCertificate=True"
```

See [Migrations](#migrations) before adding or applying one.

Before you call a change done, `dotnet build MedApp.sln` must succeed with no new warnings and `dotnet test MedApp.UnitTests` must pass. For a change in `MedApp.Web`, `npm run lint`, `npm run typecheck` and `npm test` must pass there too.

## Solution layout

Dependencies point inward: `API → Application → Domain` and `Infrastructure → Application`. `MedApp.AppHost` sits outside that and references what it runs.

| Project | Responsibility |
|---|---|
| `MedApp.Domain` | Entities and enums (`Patient`, `PatientProcedure`, `PatientProcedureStage*`, `UserSession`). No dependencies. |
| `MedApp.Contracts` | Request/response DTOs. The React app gets them as TypeScript types generated from the API's Swagger document. |
| `MedApp.Application` | Commands, queries, handlers and validators (MediatR + FluentValidation). Also repository and service **interfaces** and app exceptions. |
| `MedApp.Infrastructure` | `MedAppDbContext`, EF configurations, repository implementations, Identity and session services, and migrations. |
| `MedApp.API` | Thin controllers, the session auth handler and exception middleware. In production it also serves the built React app from `wwwroot`. |
| `MedApp.Web` | The UI: React 19, TypeScript, Vite and Astryx. Not a .NET project: a solution folder in `MedApp.sln`, built with npm. |
| `MedApp.Client.Deprecated` | **Deprecated.** The original Blazor WASM UI, replaced by `MedApp.Web`. Not run, and no new work; kept building, for reference only. See its `README.md`. |
| `MedApp.AppHost` | .NET Aspire AppHost for local dev: runs the SQL Server container, the API and the React app. On publish, it builds the React app into the API's `wwwroot`. |
| `MedApp.ServiceDefaults` | Aspire service defaults (OpenTelemetry, health checks at `/health` and `/alive` in Development), used by the API. |
| `MedApp.UnitTests` | NUnit tests for Application handlers/validators and Infrastructure services. |
| `MedApp.IntegrationTests` | Empty project shell, with no tests yet. |
| `MedApp.E2ETests` | Playwright for .NET setup (base fixture, sign-in) for end-to-end tests of `MedApp.Web`. No tests yet; see its `README.md`. |

The top-level `Application/`, `Domain/`, `Infrastructure/`, `UnitTests/`, `IntegrationTests/` and `E2ETests/` folders hold only stale `bin/`/`obj/` output from an old layout. Ignore them and never add code there.

## Backend conventions

### Feature slices (vertical, one folder per use case)

```
MedApp.Application/<Area>/Commands/<Action><Entity>/
    <Action><Entity>Command.cs     # sealed record : IRequest<TResponse>
    <Action><Entity>Handler.cs     # sealed class, primary-constructor DI, IRequestHandler<,>
    <Action><Entity>Validator.cs   # sealed class : AbstractValidator<TCommand> (when input needs checking)
MedApp.Application/<Area>/Queries/<Get...>/
    <Get...>Query.cs / <Get...>Handler.cs
```

To add a feature end to end:
1. Add or update the entity in `Domain`. Add or update its `IEntityTypeConfiguration` in `Infrastructure/Data/Configurations/<Area>/`, which `ApplyConfigurationsFromAssembly` picks up automatically.
2. Add request/response DTOs in `Contracts/<Area>/{Requests,Responses}/`.
3. Add the command/query, handler and validator in `Application`. MediatR handlers and validators are registered by assembly scanning, so no DI changes are needed for them.
4. For a new repository, put the interface in `Application/<Area>/Repositories/` and the implementation in `Infrastructure/Data/Repositories/`. Register it in `AddRepositories()` in `MedApp.Infrastructure/Common/Extensions/ServiceExtensions.cs`.
5. In the controller action, map the request into a command, call `mediator.Send(...)` and return the result. Keep business logic out of controllers.
6. If the schema changed, add an EF migration and review it (see [Migrations](#migrations)). Migrations are **not** applied at startup.
7. Add unit tests for the handler and the validator.

### Validation and errors

- Validation runs in the MediatR pipeline (`ValidationBehaviour` in `Application/Common/Behaviors`). It throws `FluentValidation.ValidationException`, which `API/Middleware/ExceptionHandling.cs` turns into a 400 `ValidationProblemDetails`.
- **Preferred style for new code:** throw `NotFoundException` (becomes 404) or `ConflictException` (becomes 409) from handlers. The Procedures area follows this. A wrong username or password throws `InvalidCredentialsException` (becomes 401).
- An `/api` path that matches no endpoint is a 404. Every other unknown path gets the React app's `index.html`, so client-side routes work on reload.
- The Patients and Auth slices predate that and return `null`/`bool`/`IdentityResult`, leaving controllers to choose the status code (except that deleting a patient who has procedures throws `ConflictException`). Keep that existing style when you edit those slices, unless the task is to migrate them.
- Unhandled exceptions are not caught by the middleware, so they surface as 500s.

### Lists

- Lists are filtered, sorted and paged by the API: `GET api/patients/search`, `GET api/auth/users/search`, `GET api/procedures/patient-procedures/search` and `GET api/procedures/audit/search` return a `PagedResponse<T>` (`items`, `page`, `pageSize`, `totalCount`, `totalPages`). `pageSize` defaults to 10, with a maximum of 100.
- Sort and filter in plain LINQ that EF can translate (see `PatientSearch`), so the same code runs in SQL and in unit tests. Always end the order with a unique key (`ThenBy(x => x.Id)`), or paging can repeat or skip rows.
- `GET api/patients` and `GET api/auth/users` return everything. Only the deprecated Blazor client uses them; prefer the search endpoints.

### Mapping

Handlers map Domain entities to `Contracts` responses by hand, with inline `new XResponse(...)` / object initialisers. There is no mapping library on `main`.

### Data access

- Each repository method calls `SaveChangesAsync` itself, so there is no unit of work.
- Read methods use `AsNoTracking()`, and updates go through `context.X.Update(entity)`.
- **Procedures are the exception:** handlers load a tracked procedure with `GetForUpdateAsync`, change it through `ProcedureChange`, and `SaveAsync(auditEntries)` saves the change and its audit entries in one `SaveChangesAsync`. That way EF works out which stages and assignments changed, and no change can miss the audit log.
- Pass `CancellationToken` all the way through. The tests check that it is passed on.
- IDs are `Guid`s generated in handlers (`Guid.NewGuid()`). Timestamps are UTC (`CreatedAt`, `LastUpdated`), set in handlers.
- Handlers and validators that need "now" take a `TimeProvider` (registered in `AddApplication`); tests pass a `FixedTimeProvider`.

### Procedures

The concept and decisions are in `docs/procedures-ui-plan.md`. In short: admins build **templates**, ordered lists of **stage definitions** in which a stage may repeat. Staff create a patient's **procedure** from a template, which copies its stages (name, description, instructions) onto the procedure. Stages are worked strictly in order: notes, then finish, which locks the stage until an admin unlocks it.
- **The rules live in `MedApp.Application/Procedures/Common`:** `ProcedureWorkflow` (statuses, which stage is current, what can be edited, overdue), and `ProcedureChange` (applies a change, moves the status automatically and writes the audit entries). Use them; don't set `Status` or stage flags in a handler by hand.
- **Every change to a procedure is audited** (`PatientProcedureAuditEntry`, no foreign keys so the history outlives deletes). New procedure commands must go through `ProcedureChange` so they're audited too.
- **Due dates are whole days,** due by the end of the day UK time and stored as that moment in UTC (`DueDates`). The API takes and returns `DueDate` as a date.
- **Definitions and templates in use are archived, not deleted;** deleting one that's in use is a 409. Patients with procedures can't be deleted (409).
- Status numbers are stored, so never renumber `PatientProcedureStatus` or `PatientProcedureAuditAction`.

### Migrations

- Migrations live in `MedApp.Infrastructure/Migrations/` with `MedAppDbContextModelSnapshot.cs`. They are a single linear history starting at `InitialCreate`. Always add a new migration on top, and never edit or delete one that has been applied to a database.
- Name migrations in the same past-tense style as commits, for example `RemovedTemplateAuditColumns` or `RenamedTasksToProcedures`.
- **Always read the scaffolded migration before keeping it.** EF can't detect renames of entities or of properties used as keys, so it scaffolds a rename as `DropTable` + `CreateTable`, which deletes the data. When `migrations add` warns that "An operation was scaffolded that may result in the loss of data", rewrite `Up` and `Down` by hand. Drop the keys and foreign keys whose names change, then call `RenameTable`, `RenameColumn` and `RenameIndex`, then re-add the keys and foreign keys with the same `onDelete` behaviour. `RenamedTasksToProcedures` is the reference example.
- After editing a migration, `has-pending-model-changes` must report no changes. Otherwise the snapshot and the model disagree.
- Test a non-trivial migration up **and** down against a throwaway LocalDB database, not the dev `MedApp` database. Pass `--connection "Server=(localdb)\MSSQLLocalDB;Database=MedApp_MigrationTest;Trusted_Connection=True;TrustServerCertificate=True"` to `dotnet ef database update`, seed synthetic rows, apply the migration, roll back with `database update <PreviousMigration>`, then drop the database.
- Applying a migration to someone's local `MedApp` database changes their data, so do it only when asked, and take a `medapp-dev` `save_snapshot` first.

### Auth

- On login, a `UserSession` row is created and its ID goes into an HttpOnly cookie. `SessionAuthenticationHandler` checks the cookie on every request and builds claims, including roles.
- The UI is always on the API's own origin (served from `wwwroot` in production, proxied by Vite in development). So there is no CORS, and the cookie is `SameSite=Lax`, and `Secure` outside Development.
- The roles are `Admin` and `User`. Staff (either role) use `[Authorize(Policy = AuthPolicies.Staff)]`: patients, and creating and working on procedures. Admin-only endpoints use `[Authorize(Roles = "Admin")]`: users and roles, stage definitions and templates (staff can read them), assigning staff, unlocking stages, deleting procedures, and the procedure audit log. There may be several admins; never assume one. The `SelfUserOnly` policy limits a user to their own `{username}` route.
- Procedure handlers get the signed-in user as a `ProcedureActor`, from `User.ToProcedureActor()` (`MedApp.API/Common/Authentication/ClaimsPrincipalExtensions.cs`).
- The app seeds no users; its only `HasData` seed is a default stage definition. **Locally, sign in as `admin` / `admin`.** `medapp-dev` creates that test login (`ensure_test_admin`, and on every `reset_demo_data`), bypassing the password rules. It exists only in local dev databases.
- Identity password and lockout rules live under the `Identity` section of `appsettings.json`.

## Frontend conventions (MedApp.Web)

All UI work goes in `MedApp.Web`. Its conventions are in `MedApp.Web/AGENTS.md`, which Claude Code loads when working in that folder, and its layout and scripts are in `MedApp.Web/README.md`. In short:
- UI is built only from Astryx components, or composites of them in `src/components/`; icons come from `AppIcon`.
- Pages live in `src/pages/`, with thin route files in `src/routes/`. List pages keep their page, sort and filters in the URL.
- Data goes through TanStack Query and the typed `api` client (`src/api/`).
- Every component and page has a Vitest test.
- After an API DTO or endpoint change, run `npm run gen:api` with the AppHost running, and commit `src/api/schema.d.ts`.

### The deprecated Blazor client

`MedApp.Client.Deprecated` is the original Blazor UI, kept in the solution for reference only (plan Q5b):
- The AppHost doesn't run it, and the API no longer allows its origin.
- Make no changes beyond keeping it compiling.
- Its `RootNamespace` and `AssemblyName` are still `MedApp.Client`.

### UI parity screenshots

`tools/ui-parity/` is the Playwright tool that compared the Blazor and React apps screen by screen during the migration. It writes `tools/ui-parity/report.html`. It's a development aid, not E2E tests.
- **Don't capture every screen after every change.** A full run takes about a minute, and reading the images costs tokens, so keep it targeted:
  - **While working,** capture only the screens your change affects, by name (from `screens.ts`), and look at their `react.png`. With the AppHost running, in `tools/ui-parity`: `npm run parity:react -- -g "patient-view|patients-list"`. Sign-in still runs first; one screen takes a few seconds. For a shared component (`Page`, `DataTable`, the side nav), pick two or three screens that use it rather than all of them.
  - **When merging** (step 1 of the merge flow), run the whole set once (`npm run parity:react`) and check that every screen still passes. Then look only at the screens the branch changed.
- The Blazor screenshots on disk are the last baseline from before cutover. Capturing Blazor again means running `MedApp.Client.Deprecated` by hand and allowing its origin in the API again.
- **New screens or states** go in `tools/ui-parity/screens.ts`. Never make a screen confirm a destructive action; only open the dialog.
- **Credentials** live in the git-ignored `tools/ui-parity/.env`; never commit them. Setup and run steps are in `tools/ui-parity/README.md`.

### Dev MCP server (`medapp-dev`)

`.mcp.json` registers `medapp-dev` (`tools/MedApp.DevMcp`) and the Astryx docs MCP (`astryx`).

**Use `medapp-dev` for local account and app-state chores,** instead of hand-written SQL or one-off programs:
- create, reset or unlock dev accounts, and set roles
- sign accounts out
- seed synthetic patients
- check migrations and row counts
- snapshot and restore the whole local database
- reset everything to the standard synthetic demo data set (`reset_demo_data`)

It only runs against a local database: by default the AppHost's SQL container, using the password from the AppHost's user secrets, so the AppHost must have run once. It protects `admin`, and destructive tools need `confirm: true`.
- **Before a destructive experiment:** take a `save_snapshot`.
- **Build it first:** `dotnet build tools/MedApp.DevMcp`, because `.mcp.json` runs it with `--no-build`.
- **Docs:** see `tools/MedApp.DevMcp/README.md`.

## Local dev gotchas

- **Docker Desktop must be running.** The dev database is SQL Server 2022 in a container started by the AppHost, with its data in the `medapp-sql-data` volume, on host port `14330` (bound to `127.0.0.1`).
  - The container is **persistent**: it keeps running after the AppHost stops, so `dotnet ef` and `medapp-dev` can still reach it. `docker stop` it to free memory; the next AppHost run starts it again.
  - Aspire generated the `sa` password on the first run and keeps it in the AppHost's user secrets (`Parameters:sql-password`). The volume only opens with that password, so don't delete or change it.
- The API's `appsettings*.json` have **no connection string**: the AppHost injects `ConnectionStrings__DefaultConnection`. Running the API on its own needs that variable set.
- **An empty database** (a new volume) isn't migrated at startup. Apply the migrations with the command under [Commands](#commands), then load the demo data with `medapp-dev`'s `reset_demo_data`.
- The React dev server proxies `/api` and `/swagger` to the API. If `/api` calls start returning the app's HTML instead of JSON, the proxy has dropped out; restart the AppHost.
- Kestrel's `https` profiles need the ASP.NET Core dev certificate. If startup fails with "the default developer certificate could not be found", run `dotnet dev-certs https`.
- Session cookies are `SameSite=Lax` everywhere, and `Secure` outside Development (the Vite dev server is plain `http://localhost`).

## Testing conventions

- The stack is NUnit 3, FluentAssertions 8, Moq and AutoFixture with AutoMoq. FluentAssertions prints a license banner when tests run; it's expected and not an error.
- The test tree mirrors the source: `MedApp.UnitTests/Application/<Area>/Commands/<Action>/<Action>HandlerTests.cs` and `...ValidatorTests.cs`. Note that `Authentication` maps to `Auth` and `Sessions` to `Session` in the test tree.
- Fixtures are `[TestFixture] public sealed class`, with setup in `[SetUp]`. Build the system under test with `AutoFixtureFactory.Create()`, then `_fixture.Inject(mock.Object)`, then `_fixture.Create<Handler>()`.
- Test names follow `Method_Scenario_ExpectedResult`, for example `Handle_PatientDoesNotExist_ReturnsNull`.
- Put shared test data in `MedApp.UnitTests/Common/Constants/*TestConstants.cs`, and add new constants there rather than inlining magic values.
- Use synthetic data only. This is a medical app, so never put realistic patient PII in tests, seeds or fixtures.
- Procedure handler tests derive from `ProcedureHandlerTestBase` (mocked repository and username lookup, fixed clock), and build procedures with `ProceduresTestConstants.CreateProcedure`.

## CI

`.github/workflows/main.yml` runs on PRs to `main`, with three jobs:
- `web` checks `MedApp.Web`: `npm ci`, lint, type check, a check that the built theme is up to date, Vitest with coverage, and `vite build`.
- `unit-tests` runs `dotnet test MedApp.UnitTests` in Release with coverage.
- `coverage-comment` posts one sticky PR comment with both: .NET classes and React files under 80% line coverage.

What coverage counts:
- **.NET:** commands, queries, DTOs, repositories, DbContext, migrations and `ServiceExtensions` are excluded, so handlers and validators are what count.
- **React:** generated code, the theme build, test helpers and the thin route files are excluded.

`.github/workflows/e2e.yml` runs the (empty) E2E suite, only when started by hand.

### Checking CI on PRs you opened or pushed to

After you open a PR, or push to the branch of one you opened, check its CI result before you call the work done. Don't stop at "pushed".

**Don't block the user while CI runs.** Hand steps 1–3 to a background subagent (in Claude Code, the `Agent` tool; it notifies you when it's done). Tell the user it's watching CI, and carry on with whatever they ask next. Give the subagent the branch, the pushed commit SHA and this section to follow. Rules for the subagent:
- **Read-only:** no edits, commits or pushes. Its job is to report, not to fix.
- **Waiting:** poll about every 30 seconds using the harness's wait mechanism (a background command or monitor; foreground `sleep` may be blocked). The workflow usually takes 2–3 minutes. Give up after 15 minutes and report the run as still pending, with its URL.
- **Report:** return the run URL, the result, and for a failure the failing step, the error, the cause, whether it's already broken on `main`, and a proposed fix as an exact diff.

When the subagent reports back, do step 4 yourself, and don't interrupt the user's current task to do it. Mention the result at the next natural break, or right away if it failed.

1. **Find the run for your commit.** The repo is public, so the GitHub REST API works without auth. Use `gh` instead if it's installed and logged in (it may not be).
   - Runs: `GET https://api.github.com/repos/Aliator/MedApp/actions/runs?branch=<branch>`. Only trust a run whose `head_sha` matches the commit you just pushed. A new run can take a minute or so to appear, and until then the API returns the previous run. If you don't see a matching run, say the check is still pending and check again later. Don't report the old run's result as the new one.
   - Then read its jobs (`/actions/runs/<run_id>/jobs`) to see each step's `conclusion`.
2. **If it passed**, say so in one line, and mention any coverage problems from the sticky PR comment, such as handlers, validators or React files under 80%.
3. **If it failed**, find the cause before reporting.
   - Note which step failed. A failure in "Set up job" means the workflow itself is broken (bad action ref, runner image), not the code.
   - Read the annotations with `GET /repos/Aliator/MedApp/check-runs/<job_id>/annotations`. Full job logs need auth, so read them through `gh run view <run_id> --log-failed` when `gh` is available.
   - Reproduce the failure locally the way CI runs it: `dotnet test MedApp.UnitTests --configuration Release`, or the `web` job's npm scripts in `MedApp.Web`.
   - Check whether the failure is yours or already broken on `main`, for example when the workflow file is unchanged by your branch.
4. **Prompt the user before fixing.** Tell them which step failed and the error, the cause, whether your change caused it or it was already broken, and the fix you propose with the exact diff. Wait for them to approve before you commit or push it, unless they've already told you to fix it. After pushing a fix, check the new run again (step 1).

## Git workflow

- Branch from `main` as `feature/<name>`, and merge back through a PR to `main`.
- Commit messages are short, capitalised and past tense, for example "Updated patient task routing", "Added get all methods for tasks".
- Never commit `bin/`, `obj/`, `.idea/` or `*.DotSettings.user`.

### "Merge it": the full merge flow

**Only run this when the user explicitly asks you to merge.** Without that, stop at committing or at an open PR, as usual. Opening a PR or seeing CI pass is not permission to merge.

When the user says to merge, do every step, in order, without stopping to ask again:
1. **Commit** any outstanding changes on the feature branch (branch from `main` first if you're on it), and push. If the branch changed the UI, run the full set of UI parity screenshots first (see [UI parity screenshots](#ui-parity-screenshots)).
2. **Open a PR** to `main` if there isn't one already. If there is one, reuse it.
3. **Wait for the workflow** run whose `head_sha` is the pushed commit to complete (see [Checking CI](#checking-ci-on-prs-you-opened-or-pushed-to)). In this flow, waiting is the point, so wait for it rather than moving on.
   - **If it fails, stop.** Don't merge. Report it and propose a fix, as in step 4 of the CI section.
4. **Merge** the PR with a merge commit (`gh pr merge <n> --merge`), matching the existing "Merge pull request #N" history.
5. **Delete the branch**, both on GitHub and locally (`--delete-branch` handles both).
6. **Pull `main` locally:** `git switch main` then `git pull`, so the working copy is up to date and ready for the next change. Then confirm the merge commit and that the working tree is clean.
