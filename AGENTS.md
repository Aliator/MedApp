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

To try the production image in Kubernetes, `deploy/kind/up.sh --demo-data kind-demo` runs MedApp on a local kind cluster at http://localhost:8080 (and https://localhost:8443), beside the AppHost and with a database of its own. It deploys with the Helm chart in `deploy/helm/medapp`, whose migrations Job runs the EF migrations bundle built into the image, then makes the first admin. The chart is locked down (network policies, the `restricted` Pod Security Standard, read-only filesystems, secrets from External Secrets), so a new container, port or connection in it needs a matching change there. `up.sh --argocd` has Argo CD deploy from git instead. Each merge to `main` runs the Publish workflow, which pushes the image to GHCR and records the commit, with the image's tag, on the `env/kind` branch that Argo CD follows (`deploy/argocd/README.md`). Roll back by reverting the change on `main`. Never commit to an `env/*` branch by hand except in an emergency, and know that the next deploy replaces it.

Azure (AKS, Azure SQL, Key Vault, Azure Container Registry) is in `deploy/azure`. It's written and checked, but **not deployed**. Its Bicep (`main.bicep`) is compiled and linted in CI. Deploying it costs money, so never run `deploy.sh` or `bootstrap.sh` without the user asking. Once it's set up, the Publish workflow's `azure` job deploys each merge through the `env/azure` branch; until then the job is skipped. Chart versions for both clusters are in `deploy/versions.sh`. See the `README.md` in each. CI lints the chart (the `helm` job), so after changing it run `helm lint --strict deploy/helm/medapp --set image.tag=ci`.

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
| `MedApp.Web` | The UI: React 19, TypeScript, Vite and Astryx, rendered on the server by a small Node server (`server/server.mjs`) and hydrated. Not a .NET project: a solution folder in `MedApp.sln`, built with npm. |
| `MedApp.Client.Deprecated` | **Deprecated.** The original Blazor WASM UI, replaced by `MedApp.Web`. Not run, and no new work; kept building, for reference only. See its `README.md`. |
| `MedApp.AppHost` | .NET Aspire AppHost for local dev: runs the SQL Server container, the API and the React app. On publish, it builds the API's image from `MedApp.API/Dockerfile`, which holds the API, the built React app in `wwwroot` and Node to render its pages. |
| `MedApp.ServiceDefaults` | Aspire service defaults (OpenTelemetry, health checks at `/health` and `/alive`), used by the API. The health checks are on the app's port in Development; elsewhere only on the port in `HealthChecks:Port` (8081 in Kubernetes, which nothing outside the pod reaches), and not at all without it. |
| `MedApp.UnitTests` | NUnit tests for Application handlers/validators, Infrastructure services, and the API's own services (`Api/`). |
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
8. If the change alters what personal data is held, who can see it, where it goes (a recipient, a sub-processor, a region) or how long it's kept, update `docs/governance/` in the same PR: at least `data-inventory.md` and `dpia.md`, and any other document it makes untrue (see `docs/governance/README.md`).

### Validation and errors

- Validation runs in the MediatR pipeline (`ValidationBehaviour` in `Application/Common/Behaviors`). It throws `FluentValidation.ValidationException`, which `API/Middleware/ExceptionHandling.cs` turns into a 400 `ValidationProblemDetails`.
- **Preferred style for new code:** throw `NotFoundException` (becomes 404) or `ConflictException` (becomes 409) from handlers. The Procedures area follows this. A wrong username or password throws `InvalidCredentialsException` (becomes 401).
- An `/api` path that matches no endpoint is a 404. Every other unknown path gets the React app's `index.html`, so client-side routes work on reload.
- The Patients and Auth slices predate that and return `null`/`bool`/`IdentityResult`, leaving controllers to choose the status code (except that deleting a patient who has procedures throws `ConflictException`). Keep that existing style when you edit those slices, unless the task is to migrate them.
- Unhandled exceptions are not caught by the middleware, so they surface as 500s.

### Lists

- Lists are filtered, sorted and paged by the API: `GET api/patients/search`, `GET api/auth/users/search`, `GET api/procedures/patient-procedures/search` and `GET api/procedures/audit/search` return a `PagedResponse<T>` (`items`, `page`, `pageSize`, `totalCount`, `totalPages`). `pageSize` defaults to 10, with a maximum of 100.
- Sort and filter in plain LINQ that EF can translate (see `PatientSearch`), so the same code runs in SQL and in unit tests. Always end the order with a unique key (`ThenBy(x => x.Id)`), or paging can repeat or skip rows.
- **The exception is a feed:** a patient's timeline (`GET api/patients/{id}/timeline`) pages by cursor (keyset) rather than by page number, so entries added meanwhile don't shift the pages. It orders by `AtUtc` then `Id`, both descending; a page's `NextCursor` encodes its last entry's pair, and the next page starts after it (`PatientTimelineSearch`, `TimelineCursor`).
- `GET api/patients` and `GET api/auth/users` return everything. Only the deprecated Blazor client uses them; prefer the search endpoints.

### Mapping

Domain entities become `Contracts` responses through **hand-written static mappers**, one per area. Don't use a mapping library (no AutoMapper or Mapster).
- **Where they live:** in the area's `Common/` folder, named `<Thing>Mapper`. Each has `ToResponse(entity)`, plus overloads for what a response needs that the entity doesn't have, for example `ToResponse(definition, usage)` or `ToResponseAsync(...)` when it looks up usernames.
- **Mappers so far:**
  - `PatientMapper`, `UserSettingsMapper`, `SessionMapper`, `NotificationMapper`, `AuditEntryMapper`
  - `ProcedureMapper`, `StageDefinitionMapper`, `StageTemplateMapper`, `TimelineMapper`, `ProcedureInsightsMapper` (from totals, not entities)
  - `UserMapper`, in Infrastructure, because `ApplicationUser` is an Infrastructure type
- **Paged responses:** `PagedMapper.ToResponse(result, page, pageSize, Mapper.ToResponse)` (`Common/Paging`) turns a repository's `PagedResult` into the `PagedResponse` every search endpoint returns.
- **No inline mapping:** handlers and controllers don't build responses from entities themselves, and queries never return Domain entities to the API. Results that aren't entities (a login's session, a generated password, the signed-in user's claims) may be built where they're made.
- **Tests:** every mapper has its own (`<Thing>MapperTests`), including what it computes (`CanBeDeleted`, an action or kind by name, read or not).

### Data access

- Each repository method calls `SaveChangesAsync` itself, so there is no unit of work.
- Read methods use `AsNoTracking()`, and updates go through `context.X.Update(entity)`.
- **Procedures are the exception:** handlers load a tracked procedure with `GetForUpdateAsync`, change it through `ProcedureChange`, and `SaveAsync(auditEntries)` saves the change and its audit entries in one `SaveChangesAsync`. That way EF works out which stages and assignments changed, and no change can miss the audit log.
- Pass `CancellationToken` all the way through. The tests check that it is passed on.
- IDs are `Guid`s generated in handlers (`Guid.NewGuid()`). Timestamps are UTC (`CreatedAt`, `LastUpdated`), set in handlers.
- **Times are stored and sent as UTC, with a `Z`.** SQL Server's `datetime2` keeps no time zone, so `UtcDateTimeConverter` (every `DateTime` and `DateTime?`, in `MedAppDbContext.ConfigureConventions`) marks what EF reads as UTC; without it the JSON has no `Z` and browsers read the time as their local time, an hour out in British Summer Time. The web app formats times in UK time (`formatDateTime` in `MedApp.Web/src/utils/dates.ts`), and reads one without a `Z` as UTC too. Due dates and dates of birth are dates, not times, and are never shifted. `dueDateUtc` is a moment, the end of the due day (UK time) in UTC, for comparing with now; show `dueDate`.
- Handlers and validators that need "now" take a `TimeProvider` (registered in `AddApplication`); tests pass a `FixedTimeProvider`.

### Procedures

The concept and decisions are in `docs/procedures-ui-plan.md`. In short: admins build **templates**, ordered lists of **stage definitions** in which a stage may repeat. Staff create a patient's **procedure** from a template, which copies its stages (name, description, instructions) onto the procedure. Stages are worked strictly in order: notes, then finish, which locks the stage until an admin unlocks it.
- **The rules live in `MedApp.Application/Procedures/Common`:** `ProcedureWorkflow` (statuses, which stage is current, what can be edited, overdue), and `ProcedureChange` (applies a change, moves the status automatically and writes the audit entries). Use them; don't set `Status` or stage flags in a handler by hand.
- **Every change to a procedure is audited** (`PatientProcedureAuditEntry`, no foreign keys so the history outlives deletes). New procedure commands must go through `ProcedureChange` so they're audited too.
- **Due dates are whole days,** due by the end of the day UK time and stored as that moment in UTC (`DueDates`). The API takes and returns `DueDate` as a date.
- **Definitions and templates in use are archived, not deleted;** deleting one that's in use is a 409. Patients with procedures can't be deleted (409).
- Status numbers are stored, so never renumber `PatientProcedureStatus` or `PatientProcedureAuditAction`.

### Patient timeline

The patient page shows a **timeline**: everything that happened to the patient's procedures, in one feed, built from the procedure audit log (`MedApp.Application/Procedures/Timeline`).
- **Endpoints, for staff:** `GET api/patients/{id}/timeline` (a page of entries, newest first, with `Kinds` and `ProcedureIds` filters and a `Before` cursor) and `GET api/patients/{id}/timeline/overview` (the open and blocked procedures, soonest due first and overdue flagged, and the procedures to filter by). Both are 404 for an unknown patient.
- **Kinds** (`PatientTimelineKind`) group the audit actions for filtering: Created, Stages (started, finished, unlocked), Notes, Assignments, Blocks (to Blocked, or back from it: cancelling a blocked procedure is under Status), Status, DueDates (a details edit that includes the due date), Details and Deleted. `PatientTimelineKinds` works them out the same way in memory and in SQL, reading markers in the entry's JSON details. They aren't stored, so they can be regrouped freely; a change to how `ProcedureAudit` writes details must keep its tests passing.
- **What each person sees is decided on the server** (`GetPatientTimelineQuery.IsAdmin`, from the caller's role), because the audit log itself is admin-only and keeps old values:
  - **Staff** see entries only for procedures that still exist. Each shows the kind, who, when and the procedure (a link), and structured values, old and new: due dates, priorities and statuses, the template, who was assigned and unassigned (past assignees too), and the username recorded at the time (a deleted account's too, where the procedure page says "Deleted user"). **Never** free text from the log: no notes text (stage or procedure), no old or new titles, no block or cancel reasons; for those they get only which field changed.
  - **Admins** see everything, including entries for deleted procedures (labelled as deleted, each with the title recorded in that entry) and the free-text values.
  - Keep this when adding a field to `PatientTimelineEntryResponse`: free text from the audit details goes to admins only (`TimelineMapper`'s `PublicValueFields` is the allowlist for edited fields). `TimelineStaffCanaryTests` records every action with one string as all its free text and checks the whole staff response never holds it; add any new action or free-text field to it.
  - The governance pack (`docs/governance/`) states this rule; change both together.
- An entry carries the procedure's title now, or, once it's deleted, the title recorded in that entry (so a procedure renamed before it was deleted shows under both titles).
- **Order:** newest first by `AtUtc`, then `Id`. The entries one change writes (a stage started, its notes saved, the status change that followed) are each a tick (100 ns) after the one before (`ProcedureChange.Audit`), so they sort in the order they happened; entries written before that share a time and fall back to ID order.

### Notifications

- A handler tells someone about something with `INotificationSender.SendAsync(new Notification { … })` (`MedApp.Application/Notifications/Common`), after its own change is saved. It stores the notification, then pushes it live to every page that user has open.
- **Live delivery is SignalR:** `NotificationsHub` at `/api/notifications/hub`, addressed by user ID (the session's `NameIdentifier`), through `SignalRNotificationPublisher` in the API. Pushing is best effort: a failure is logged, and the stored notification shows on the next load.
- **Several copies of the API share a backplane:** with `ConnectionStrings:SignalRBackplane` set, SignalR passes messages through that Redis-compatible server (`SignalRBackplane`), so a notification reaches a browser on any copy. In Kubernetes it's Valkey; in development it's unset, and there's one copy.
  - Browsers connect by WebSocket only, without negotiating first (`LiveNotifications`), because with several copies the negotiate request and the WebSocket could reach different ones.
- `GET api/notifications` returns the newest and the unread count. `POST api/notifications/read` marks them all read, and `POST api/notifications/{id}/read` marks one.
- Being assigned a procedure is the only kind so far (not when you assign yourself). Kind numbers are stored, so never renumber `NotificationKind`. Notifications have no foreign keys, like the audit log.

### Insights

Admins' overview of the work (`/admin/insights`, `GET api/procedures/insights`): headline counts against the same point last week, overdue work by assignee and by priority, procedures created and completed each week, and the average time at each stage of each template.
- **Aggregated in SQL:** the LINQ is in `ProcedureInsightsQueries` (`Application/Procedures/Insights/Common`), each query ending in a `GroupBy` or `Count`; `ProcedureInsightsRepository` runs it, and the unit tests run the same LINQ in memory. `ProcedureInsightsSqlTests` checks that each query translates to SQL Server's SQL (`ToQueryString`, no database).
- **SQL-only functions** go in `InsightsFunctions`, with a C# body for the tests, mapped to SQL in `InsightsDbFunctions` (`SecondsBetween` is `DATEDIFF_BIG`). They're not in the model snapshot, so they need no migration.
- **Past statuses come from the audit log:** a procedure's status at a moment is the `"to"` of its last `StatusChanged` entry before it (`{"from":"…","to":"…"}`, as `ProcedureAudit` writes it), or Unassigned. So keep that details format. Completions are `StatusChanged` entries to Completed, and creations are `Created` entries.
- **Weeks are UK weeks,** Monday to Sunday (`InsightsPeriod`), and "last week" is the same point in last week as now is in this one. Hourly counts from SQL are summed into weeks in C#, since the UK's offset changes are on the hour.
- **Stage times** run from a stage being reached (the stage before it first finished, or the procedure created) to it being started (waiting), then to its first finish (working). The first finish is from the audit log, because unlocking and finishing a stage again moves its own `CompletedAtUtc`.

### Migrations

- Migrations live in `MedApp.Infrastructure/Migrations/` with `MedAppDbContextModelSnapshot.cs`. They are a single linear history starting at `InitialCreate`. Always add a new migration on top, and never edit or delete one that has been applied to a database.
- Name migrations in the same past-tense style as commits, for example `RemovedTemplateAuditColumns` or `RenamedTasksToProcedures`.
- **Always read the scaffolded migration before keeping it.** EF can't detect renames of entities or of properties used as keys, so it scaffolds a rename as `DropTable` + `CreateTable`, which deletes the data. When `migrations add` warns that "An operation was scaffolded that may result in the loss of data", rewrite `Up` and `Down` by hand. Drop the keys and foreign keys whose names change, then call `RenameTable`, `RenameColumn` and `RenameIndex`, then re-add the keys and foreign keys with the same `onDelete` behaviour. `RenamedTasksToProcedures` is the reference example.
- After editing a migration, `has-pending-model-changes` must report no changes. Otherwise the snapshot and the model disagree.
- Test a non-trivial migration up **and** down against a throwaway LocalDB database, not the dev `MedApp` database. Pass `--connection "Server=(localdb)\MSSQLLocalDB;Database=MedApp_MigrationTest;Trusted_Connection=True;TrustServerCertificate=True"` to `dotnet ef database update`, seed synthetic rows, apply the migration, roll back with `database update <PreviousMigration>`, then drop the database.
- Applying a migration to someone's local `MedApp` database changes their data, so do it only when asked, and take a `medapp-dev` `save_snapshot` first.

### Auth

- On login, a `UserSession` row is created and its ID goes into an HttpOnly cookie. `SessionAuthenticationHandler` checks the cookie on every request and builds claims, including roles.
- **Sessions last 8 hours** from sign-in (`UserSession.Lifetime`), and can be extended to 24 hours at most (`MaxLifetime`; over 20 hours, so WCAG 2.2.1 allows the hard limit). Using the app doesn't extend one; only asking does:
  - `GET api/auth/session` returns the signed-in session's `SessionResponse`: `expiresAtUtc`, `expiresInSeconds` (by the server's clock, so a device with a wrong clock still counts down right) and `canExtend`. Reading it never extends it.
  - `POST api/auth/session/extend` ("Stay signed in") moves the expiry to 8 hours from now, but never past 24 hours from sign-in and never earlier than it was, and sends the cookie again with the new expiry (the cookie expires with the session).
  - Both take the session from the cookie, are open to anyone signed in (`[Authorize]`, no role needed), are sent `no-store`, and are a 401 once the session has ended. `SessionTimeout` in `MedApp.Web` uses them for the warning before a session times out.
  - **The forced password change page has no timeout warning.** `/change-password` is outside the signed-in layout, and `PasswordChangeRequired` doesn't allow the session endpoints, deliberately: that page ends at the session's normal expiry, without a warning, and its change then fails, so its user signs in again.
  - A session isn't yet ended by a lockout, a password change, a reset or a role change (risk S5); the security baseline work is adding that.
- The UI is always on the API's own origin (served from `wwwroot` in production, proxied by Vite in development). So there is no CORS, and the cookie is `SameSite=Lax`, and `Secure` outside Development.
- The roles are `Admin` and `User`. Staff (either role) use `[Authorize(Policy = AuthPolicies.Staff)]`: patients (with their timeline, which shows staff less than admins: see [Patient timeline](#patient-timeline)), and creating and working on procedures. Admin-only endpoints use `[Authorize(Roles = "Admin")]`: users and roles, stage definitions and templates (staff can read them), assigning staff, unlocking stages, deleting procedures, the procedure audit log, and Insights (`GET api/procedures/insights`). There may be several admins; never assume one. The `SelfUserOnly` policy limits a user to their own `{username}` route.
- Procedure handlers get the signed-in user as a `ProcedureActor`, from `User.ToProcedureActor()` (`MedApp.API/Common/Authentication/ClaimsPrincipalExtensions.cs`).
- The app seeds no users; its only `HasData` seed is a default stage definition. **Locally, sign in as `admin` / `admin`.** `medapp-dev` creates that test login (`ensure_test_admin`, and on every `reset_demo_data`), bypassing the password rules. It exists only in local dev databases.
- **A deployed database gets its first admin deliberately:** `dotnet MedApp.API.dll create-first-admin` (`FirstAdminSetup`) makes the Admin and User roles and an admin with both (User lets them change their own password) from the `FirstAdmin:Username` and `FirstAdmin:Password` settings, but only while there are no accounts at all, then exits. The Helm chart's migrations Job runs it after migrating, with the credentials from a Secret.
- **The server generates every new account's first password;** there is no default password. `POST api/auth/users` takes only a username, creates the account with a generated first password that meets the `Identity` rules (`IIdentityUserService.CreateUserAsync`, the same generator as a reset), and returns it once in `CreateUserResponse`. The password is shown once, and its user must change it before doing anything else (below). The Users page shows it to the admin once (`OneTimePassword`, with a copy button), to pass on securely, and forgets it when the dialog closes.
  - **A generated password must be changed at first sign-in.** `ApplicationUser.MustChangePassword` is set by `IdentityUserService` when it creates an account or resets a password, in the same save as the password, and cleared when `UpdateUserPasswordAsync` succeeds, in the same update. A new password must differ from the current one (`UpdateUserValidator`).
  - **How it's enforced:** `SessionService` reads the flag on every request, and `SessionAuthenticationHandler` turns it into a claim (`SessionAuthenticationDefaults.MustChangePasswordClaim`). `whoami` reports it as `mustChangePassword`. While it's set, `PasswordChangeRequired` (`MedApp.API/Common/Authentication`, between authentication and authorization) refuses every `/api` request, the notifications hub included, with a 403 `ProblemDetails` (title "Password change required", `code: "password_change_required"`). It allows only `GET api/auth/whoami`, `POST api/auth/login` (signing in again checks the password afresh and replaces the session), `POST api/auth/logout`, `GET api/auth/me/settings`, and `PUT api/auth/users/{username}/update-password` for their own username (compared as `SelfUserOnly` compares it, `AuthPolicies.IsOwnUsername`). Pages and static files pass: the web app's `_app` guard sends them to `/change-password`, which asks for the given password and a new one, then opens their start page. A page already open when the flag is set finds out from its next API call: the API client sends any 403 with that `code` to `/change-password` with a full page load (in the browser only).
  - **Exempt:** the first admin (`FirstAdminSetup`), whose password comes from the secret store, set by whoever deploys (on kind, `up.sh` generates it, and it can be read back from the cluster's secret store, which is acceptable for a local cluster); and every account `medapp-dev` makes or sets a password on (`create_user`, `set_password`, `ensure_test_admin`, and `reset_demo_data`'s staff), so they can sign in and work at once.
  - **`CreateUserResult`** carries a password exactly when the account was created: make it with `Created(password)` or `Failed(result)`. It and the password responses never print the password in `ToString()`, and the API sends them with `Cache-Control: no-store`.
  - **Accounts created before #53** (from the Users page or the Blazor client) were given the published password `DefaultPassword1`, and may still have it. An admin should reset those accounts' passwords, or ask their users to change them. Finding them is a to-do (`docs/todo.md`).
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
  - Aspire generated the `sa` password on the first run and keeps it in the AppHost's user secrets (`Parameters:sql-password`). The database in the volume only accepts that `sa` password, so don't delete or change it.
- The API's `appsettings*.json` have **no connection string**: the AppHost injects `ConnectionStrings__DefaultConnection`. Running the API on its own needs that variable set.
- **An empty database** (a new volume) isn't migrated at startup. Apply the migrations with the command under [Commands](#commands), then load the demo data with `medapp-dev`'s `reset_demo_data`.
- The React dev server (`npm run dev`: Node with Vite, rendering pages on the server) proxies `/api` and `/swagger` to the API, and its own server-side API calls go through that proxy too. If `/api` calls start returning the app's HTML instead of JSON, the proxy has dropped out; restart the AppHost.
- **Server-side rendering in production** (`MedApp.API/ServerRendering`): the API starts the page server from `wwwroot/ssr/server.mjs` with `node`, on a free local port. It forwards page requests there (only the path and query: the host is always Node's) and falls back to `index.html` if Node is missing or stops, if a render fails in any way or Node answers with a 5xx, as long as nothing has been sent yet; a page that fails part-way is cut short. Its `ssr` HttpClient has no resilience handler, so a render is never retried. `Ssr:RenderTimeout` covers only getting the page started (its status and headers), and `HttpClient`'s default 100 s timeout applies too; nothing times out a page that's still streaming, so a render that stalls part-way holds the browser's request until the browser gives up. Rendered pages hold the user's data, so they're sent with `Cache-Control: no-store`. Node gets only the environment variables it needs (an allowlist in `SsrServer.StartInfo`), so the API's secrets (its connection strings) aren't in Node's environment and can't leak from it by accident, e.g. into logs. That isn't isolation: Node runs as the same user in the same container, so code running in Node could still read the API's environment from `/proc`. Real separation would need the page server in a sidecar container. The page server's own API calls carry a key the API gave it, so they skip the https redirect on this machine. `wwwroot/ssr`, `wwwroot/.vite` and any path with an empty segment (`//`) are refused (`PageServerFiles`). That holds on Linux, where the image runs; on a Windows host, backslash, trailing-dot and 8.3 short-name forms of those paths (`/ssr\server.mjs`, `/ssr./server.mjs`, `/VITE~1/manifest.json`) can still reach the files through static files. The robust fix, moving the page server out of `wwwroot`, is on the to-do list (`docs/todo.md`). Settings are in the `Ssr` section (`SsrOptions`).
- **The production image** is `MedApp.API/Dockerfile`, built from the repo root: `docker build -f MedApp.API/Dockerfile -t medapp-api .`. It builds the React app with Node and the API with the .NET SDK, then puts both on the `aspnet` runtime with Node's binary, running as a non-root user on port 8080 (TLS ends in front of it). The root `.dockerignore` keeps Windows `node_modules`, build output and local secrets out of the build. If you change which projects the API references, update the Dockerfile's `COPY` lines too.
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

`.github/workflows/main.yml` runs on PRs to `main`, with five jobs:
- `web` checks `MedApp.Web`: `npm ci`, lint, type check, a check that the built theme is up to date, Vitest with coverage, and the full production build (`npm run build`: the client, the server rendering bundle and the page server).
- `image` builds the API's image from `MedApp.API/Dockerfile`, as Publish does, without pushing it, so a broken Dockerfile or bundle fails on the PR. It shares the GitHub Actions build cache with Publish. `web`, `helm`, `unit-tests` and `image` are all required checks on `main`.
- `helm` lints the Helm chart (`deploy/helm/medapp`) in its main configurations (its own SQL Server, kind's values with External Secrets and TLS, an external database, and Azure's values), renders it, and compiles and lints the Azure Bicep (`deploy/azure`). It also renders the cluster services (Traefik, cert-manager, External Secrets and Argo CD) at the versions in `deploy/versions.sh`, with every values file and `--set` value that `deploy/kind/up.sh` and `deploy/azure/bootstrap.sh` give them. After changing one of those values files, run the same `helm template` locally.
- `unit-tests` builds the dev MCP tool (`tools/MedApp.DevMcp`, which isn't in `MedApp.sln`), then runs `dotnet test MedApp.UnitTests` in Release with coverage.
- `coverage-comment` posts one sticky PR comment with both: .NET classes and React files under 80% line coverage.

What coverage counts (the aim is full coverage of everything with behaviour worth testing):
- **.NET:** handlers, validators, domain logic, Infrastructure services, and the API (controllers, middleware, authentication, server rendering). Excluded: commands, queries, DTOs, repositories, DbContext, migrations, generated code (`obj/`), the Aspire service defaults, and wiring (`ServiceExtensions`, `ApplicationBuilderExtensions`, `Program`), plus `SessionService` and `IdentityTransactions`, which need a real database.
- **React:** everything in `src/` except generated code, the theme build, test helpers, the thin route files and the browser entry. `npm run coverage` (CI's web job) fails below the thresholds in `vite.config.ts`, set just under the current figures (99% lines, statements and functions, 92% branches).
- New code comes with tests that cover it. Leave a branch uncovered only when it's unreachable or needs a real browser or database, and say so in the PR.

`.github/workflows/e2e.yml` runs the (empty) E2E suite, only when started by hand.

`.github/workflows/publish.yml` runs on every push to `main` except docs-only changes, so it runs after each merge:
1. `image` builds the API's image and pushes it to GHCR (`ghcr.io/aliator/medapp-api:<commit>`).
2. `deploy-kind` records that commit on the `env/kind` branch, with the tag in `deploy/argocd/kind-image.yaml`: a commit whose files are exactly `main`'s plus that file, except `.github/workflows`, which keeps the branch's own (`.github/scripts/deploy-to-env.sh`). Argo CD deploys that branch (`deploy/argocd/README.md`). `azure` does the same for `env/azure`, once Azure is set up. The two deploy jobs need only `image`, so one failing doesn't stop the other. Started by hand on another branch, it builds and pushes the image but deploys nothing. A re-run of an older run doesn't move a branch back.

When you've merged, its run is the deploy. Check it the same way as CI.

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

### Effort for each stage of a change

The user wants each change made in three stages, each at its own reasoning effort (in Claude Code, the Agent tool's `effort`):

| Stage | Effort | Who does it |
|---|---|---|
| **Thinking:** understanding the task, reading the code, the design and the plan | **xhigh** | The main session (its effort setting is `xhigh`). |
| **Implementation:** writing the code, tests and docs to the plan | **high** | A subagent with `effort: "high"`, given the plan, the files and the conventions. The main session checks its work (build, tests, a read of the diff) before calling it done. |
| **Review:** a fresh, adversarial look at the whole PR | **xhigh** | A subagent with `effort: "xhigh"` that wasn't told the author's conclusions, as part of every merge (below). |

Small, mechanical edits (a typo, a one-line config change) can be done directly in the main session; the stages are for real changes.

### "Merge it" or "ship it": the full merge flow

**Only run this when the user explicitly asks you to merge**, for example "merge it" or "ship it" (they mean the same). Without that, stop at committing or at an open PR, as usual. Opening a PR or seeing CI pass is not permission to merge.

**The medical device decision:** a change that adds calculations, scores, dose checks, decision support, triage or prioritising, alerts from clinical values, or medical claims must update `docs/clinical-safety/intended-purpose-and-device-decision.md` before it merges.

When the user says to merge, do every step, in order, without stopping to ask again:
1. **Commit** any outstanding changes on the feature branch (branch from `main` first if you're on it), and push. If the branch changed the UI, run the full set of UI parity screenshots first (see [UI parity screenshots](#ui-parity-screenshots)). New scripts need their executable bit set in git (`git update-index --chmod=+x`), since this checkout doesn't track file modes.
2. **Open a PR** to `main` if there isn't one already. If there is one, reuse it.
3. **Review the PR at xhigh**, with a subagent (`effort: "xhigh"`), and write it up as a review document:
   - **What to review:** the PR's whole diff against `main` (`git diff main...HEAD`), its description, and the code around the changes. Give the reviewer the PR and the files, not your own conclusions, so its view isn't anchored on yours.
   - **What to look for:** correctness, security, data loss, operational and deploy risks, judgement (was this the right design?), consistency between docs and code, whether the governance documents in `docs/governance/` are still true (a change to what personal data is held, who can see it, where it goes or how long it's kept must update them in the same PR), and whether the tests really test the change. Every finding needs file:line evidence, a concrete failure scenario, a severity (critical, high, medium, low), a confidence, and a suggested fix.
   - **The document:** `docs/reviews/pr-<number>-<short-name>.md`. Commit it to the branch and push, so it's merged with the PR. Write it in the format below (see [Review document format](#review-document-format)).
   - **If it finds anything high or critical, stop.** Don't merge. Check those findings yourself against the code, then report them to the user with the fixes you propose, and wait. Medium and low findings are recorded in the document and don't block. Mention them when you report the merge.
4. **Wait for the workflow** run whose `head_sha` is the last pushed commit (the one with the review document) to complete (see [Checking CI](#checking-ci-on-prs-you-opened-or-pushed-to)). In this flow, waiting is the point, so wait for it rather than moving on.
   - **If it fails, stop.** Don't merge. Report it and propose a fix, as in step 4 of the CI section.
5. **Merge** the PR with a merge commit (`gh pr merge <n> --merge`), matching the existing "Merge pull request #N" history. If the local checkout has uncommitted changes, stash them first, or the merge's local step fails after the merge on GitHub has already happened.
6. **Delete the branch**, both on GitHub and locally (`--delete-branch` handles both).
7. **Pull `main` locally:** `git switch main` then `git pull`, so the working copy is up to date and ready for the next change. Then confirm the merge commit and that the working tree is clean.
8. **Check the deploy:** each merge runs the Publish workflow, which deploys through the environment branches (`deploy/argocd/README.md`). Check that its run for the merge commit succeeded, as with CI.

### Review document format

Write each review so someone who didn't make the change can follow it quickly: short sections, bullets with bold lead-ins, short sentences, and any term that isn't obvious explained the first time it appears. Keep the depth in the findings and the detail at the end.

Start with the PR's link and branch, the commit reviewed and the `main` it was compared with, the date, and whether any finding is high or critical. Then use these sections, with these names, in this order (a review may add sections, but not rename or drop these):
1. **Summary:** what the PR does, in one or two sentences.
2. **What changed, and why:** each change, what it does here, and why it was needed.
3. **Design choices:** for each real choice, the main alternative and the trade-off, including what the chosen option is worse at.
4. **How it was checked:** tests, builds, screenshots and anything run by hand, with the results.
5. **Findings,** most severe first. Each has:
   - a severity (critical, high, medium or low, as graded in `docs/reviews/README.md`) and a confidence
   - what could go wrong, as a concrete scenario
   - the evidence (`file:line`)
   - the fix, and what happened to it (fixed in the PR, documented, or left as a to-do)

   With no findings, say so.
6. **Remaining risks:** what's still weak or left to do after this PR.
7. **Detail** (optional): diffs, longer reasoning, and anything else only an engineer needs.

Personal preferences for how reviews or explanations are written belong in a contributor's own git-ignored `CLAUDE.local.md`, not in this file.
