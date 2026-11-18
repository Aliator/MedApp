# AGENTS.md

Guidance for AI coding agents working in this repository.

## Overview

MedApp is a patient and procedure management app: a .NET 10 Web API (Clean Architecture, CQRS, MediatR) and a React frontend (`MedApp.Web`: React 19, TypeScript, Vite and Astryx). The original Blazor WebAssembly frontend is kept, deprecated, as `MedApp.Client.Deprecated`. Data is in SQL Server through EF Core, ASP.NET Core Identity handles users, and authentication is a custom cookie-based session scheme.

This file is the core: what every change needs. Each area's detail is in `docs/agents/` (index: `docs/agents/README.md`), read on demand: see [Read before you change](#read-before-you-change).

## Read before you change

**Before changing anything in an area, read its file.** The rules on the right are the ones costly to break and easy to miss; they apply even if you skip the file.

| Area | Read `docs/agents/…` before changing | Never miss |
|---|---|---|
| Procedures, templates, stages, notes | `procedures.md` | Change a procedure only through `ProcedureChange`, loaded with `GetForUpdateOrThrowAsync`, so it's audited. Audit details hold a note's ID and version, never its text. Only a note's author edits it; only admins delete one (soft). |
| Patient timeline | `timeline.md` | Staff never get free text from the audit log or appointment history (`TimelineMapper`'s `PublicValueFields`; `TimelineStaffCanaryTests`). |
| Notifications | `notifications.md` | A title is a fixed phrase; a message holds only usernames, enums, admin-written names and dates, never free text or the patient. |
| Calendar, appointments, meetings, visits | `calendar.md` | Use `WorkingHours`, `UkTime`, `CalendarClashes`, `AppointmentWorkflow` and `AppointmentChange`; take `ICalendarLock` before checking clashes. |
| Notepad | `notepad.md` | Kept on the server only, never in browser storage. |
| Monitoring | `monitoring.md` | Every procedure command calls `IProcedureMonitor.NotifyAsync(change)` after saving. Clinical follow-up never relies on it. |
| Attachments | `attachments.md` | Types come from the contents (`FileTypeSniffer`), never the name; SVG and HTML are refused. Files leave only through the API, as downloads. |
| Activity log and patient privacy | `privacy.md` | See the privacy rules below. |
| Insights | `insights.md` | It rebuilds history from the audit details' formats (`{"from":…,"to":…}`): keep them. |
| NHS number, FHIR, SNOMED CT, PDS | `nhs-fhir-snomed.md` | Keep the NHS number out of new URLs. FHIR errors are OperationOutcomes, never ProblemDetails. |
| Sign-in, sessions, roles, headers | `auth.md` | Read the session cookie only through `ISessionCookieService`. Never add an inline script or `eval` (the CSP refuses them). An account action that changes how someone signs in ends their sessions in the same transaction. |
| Email, password reset, two-factor | `email-2fa.md` | Never clinical text, a password, an authenticator key or recovery codes in an email; never an address, password, token or code in a security event. |
| `medapp-dev` and the demo data | `dev-mcp.md` | While it runs, build and test in Release. A change to shared rules changes the demo data: keep `DemoActivityParityTests` passing. |
| Local setup, server rendering, the image | `local-dev.md` | The page server gets none of the API's secrets. A change to the API's project references updates the Dockerfile's `COPY` lines. |
| CI workflows, dependencies | `ci.md` | Pin actions to a full commit SHA; give each job only the permissions it needs; never switch a failing gate off (accept one issue for a dated time). |
| Deploying (kind, Argo CD, Azure, Publish) | `deploy.md` | The chart is locked down: a new container, port or connection needs a matching change. Never run Azure's `deploy.sh` or `bootstrap.sh` unasked. Never commit to an `env/*` branch by hand except in an emergency. |
| Integration and E2E tests | `testing.md` | An account an admin made must change its generated password before any other `/api` call (`CreateStaffAsync` does). E2E tests make their own data through the API and register every clean-up. |

**Stored enum numbers are never renumbered:** `PatientProcedureStatus`, `PatientProcedureAuditAction` (with the attachment actions), `NoteDeletionReason`, `NoteDeletionRequestStatus`, `NotificationKind`, `AppointmentStatus`, `AppointmentEventAction`, `VisitRequirement`, `MonitoringEvent`, `ActivityAction` and `TwoFactorMethod`. Add new values with new numbers.

**Privacy rules for every area** (`privacy.md`):
- **Every read and change of patient data is logged** in the activity log (`IActivityLog`: `RecordViewsAsync` for reads; a change passes its entry to the repository's save). It fails closed. Classify a new GET endpoint in `EndpointCoverageTests`.
- **Free text never reaches** staff timelines, notifications, emails or logs, and activity details never hold patient details or clinical text.
- **A new table holding patient data** is added to `PatientRecordRepository`, so the export and anonymisation cover it.
- **A restricted or anonymised record can't change** (`PatientRules.EnsureCanChange`, a 409), nor its procedures, appointments or files.
- **A change to what personal data is held,** who sees it, where it goes or how long it's kept updates `docs/governance/` in the same PR.

**Briefing a subagent:** name the area files it needs (`docs/agents/calendar.md`), rather than pasting their content.

## Keeping this file small

- **The limit:** this core file stays at or under **40,000 characters** (`wc -m AGENTS.md`), as it's loaded into every session and subagent. Claude Code's own hard limit is 150,000 characters; the file must never get near it again. CI's `web` job fails above 40,000.
- **When to check:** whenever a change edits this file, before committing it.
- **Over the limit:** move detail into its area file, or condense, in the same PR if that's small; otherwise open a separate PR (`feature/condense-agents-md`) without waiting to be asked. It isn't merged without the owner's "merge it", like any PR.
- **Area files** (`docs/agents/*.md`) have no hard limit, but new detail should be as short as the rules need; depth belongs in the rest of `docs/`.
- **How to condense or move:** keep every heading's text (sections link to their anchors), every rule and its force, stored enum numbers, names of classes, files, endpoints and settings, limits, commands and links. Cut repetition (say it once, point to it), narrative, examples and long rationale. Compare with `python .github/scripts/check-agents-md.py <old AGENTS.md> AGENTS.md docs/agents/*.md`: no heading, link or backticked name may be lost unless dropped on purpose.

## Commands

Run everything from the repo root. `global.json` pins the SDK to 10.0.x; every project targets `net10.0`.

```bash
dotnet build MedApp.sln                     # build everything
dotnet test MedApp.UnitTests -c Release     # unit tests (Release, as medapp-dev locks Debug)
dotnet test MedApp.UnitTests -c Release --filter "FullyQualifiedName~UpdatePatient"   # a subset
dotnet test MedApp.IntegrationTests         # the API against SQL Server in Docker (about 1.5 min)

# E2E (Playwright) against the running AppHost as the dev admin; they make and delete their own synthetic data (README lists what stays)
# Install Chromium once: pwsh MedApp.E2ETests/bin/Debug/net10.0/playwright.ps1 install chromium
E2E_USERNAME=admin E2E_PASSWORD=admin dotnet test MedApp.E2ETests

dotnet run --project MedApp.AppHost         # everything (needs Docker): SQL Server, API, React app, Aspire dashboard
#   React app: http://localhost:5173 (commands in MedApp.Web/README.md); API + Swagger: https://localhost:7038/swagger
#   Dashboard: the login URL the command prints

# EF Core migrations (dotnet-ef 10.x is a global tool)
dotnet ef migrations add <Name> --project MedApp.Infrastructure --startup-project MedApp.API
dotnet ef migrations has-pending-model-changes --project MedApp.Infrastructure --startup-project MedApp.API   # model and snapshot in sync?

# Applying them to the dev database (the AppHost's SQL container); the sa password is in the AppHost's user secrets
PW=$(dotnet user-secrets list --project MedApp.AppHost | sed -n 's/^Parameters:sql-password = //p')
dotnet ef database update --project MedApp.Infrastructure --startup-project MedApp.API --connection "Server=127.0.0.1,14330;Database=MedApp;User ID=sa;Password=$PW;TrustServerCertificate=True"
```

See [Migrations](#migrations) before adding or applying one. Kubernetes (`deploy/kind/up.sh`), Argo CD and Azure: `docs/agents/deploy.md`. After changing the chart run `helm lint --strict deploy/helm/medapp --set image.tag=ci`.

**Testing a change: only what it touches.** CI runs every suite on each PR (about 9 minutes), and a PR merges only when green. Locally:
- **Use Release** (`-c Release`): a Debug build of either test project rebuilds `medapp-dev` into the folder the running tool has locked (see `docs/agents/dev-mcp.md`).
- **Build** the projects you changed, with no new warnings (`dotnet build <project> -c Release`), and run only their unit tests (`--filter`).
- **`MedApp.Web`:** the changed files' tests (`npx vitest run <files>`), `npm run typecheck`, and `npx eslint <files>`.
- **Integration tests:** only for what unit tests can't reach (SQL translation, Identity, a migration, middleware, the demo data seed), and only that fixture (`--filter "FullyQualifiedName~<Fixture>"`).
- **A visible change is looked at in a browser** (nothing in CI does): with the AppHost running, capture and look at the UI parity screenshots of just the screens it touches, and run the axe audit on just those (`npm run a11y -- -g "<screen>"` in `tools/ui-parity`), as the accessibility statement promises.
- **A change to how MedApp is deployed is tried on kind** before it merges: the chart (`deploy/helm`), the Dockerfile or the image's contents, `deploy/kind`, `deploy/argocd`, or a new setting, secret, port, container or connection the API needs outside Development. Run `deploy/kind/up.sh` and check the app answers; CI never installs the chart.
- **Not locally**, unless the user asks, a rule above calls for it, or you're chasing one failing test: the full unit, web or integration suites, E2E, the whole UI parity set or every screen's axe audit, kind.
- **When CI fails,** run only the failing tests locally.

## Solution layout

Dependencies point inward: `API → Application → Domain` and `Infrastructure → Application`. `MedApp.AppHost` sits outside and references what it runs.

| Project | Responsibility |
|---|---|
| `MedApp.Domain` | Entities and enums (`Patient`, `PatientProcedure`, `PatientProcedureStage*`, `Attachment`, `CalendarBlock`, `CalendarActivityType`, `Appointment`, `UserSession`, ...). No dependencies. |
| `MedApp.Contracts` | Request/response DTOs, generated as TypeScript types for the React app from the Swagger document. |
| `MedApp.Application` | Commands, queries, handlers, validators (MediatR + FluentValidation); repository and service **interfaces**; app exceptions. |
| `MedApp.Infrastructure` | `MedAppDbContext`, EF configurations, repositories, Identity and session services, migrations. |
| `MedApp.API` | Thin controllers, the session auth handler, exception middleware; serves the built React app from `wwwroot` in production. |
| `MedApp.Web` | The UI, rendered on the server by a Node server (`server/server.mjs`) and hydrated; a solution folder in `MedApp.sln`, built with npm. |
| `MedApp.Client.Deprecated` | **Deprecated** Blazor WASM UI: not run, no new work, kept building (its `README.md`). |
| `MedApp.AppHost` | .NET Aspire AppHost for local dev (SQL Server, Azurite, the API, the React app); on publish, builds the image from `MedApp.API/Dockerfile`. |
| `MedApp.ServiceDefaults` | OpenTelemetry and health checks (`/health`, `/alive`: the app's port in Development, else only `HealthChecks:Port`, 8081 in Kubernetes, and absent without it). |
| `MedApp.UnitTests` | NUnit tests of handlers, validators, Infrastructure services and the API's own services (`Api/`). |
| `MedApp.IntegrationTests` | The real API in memory (`WebApplicationFactory<Program>`, as Production) against SQL Server 2022 and Azurite (Testcontainers); also `SessionService` and `IdentityTransactions`. |
| `MedApp.E2ETests` | Playwright for .NET journeys through `MedApp.Web` (its `README.md`). |

The top-level `Application/`, `Domain/`, `Infrastructure/`, `UnitTests/`, `IntegrationTests/` and `E2ETests/` folders hold only stale `bin/`/`obj/` output. Ignore them and never add code there.

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
1. The entity in `Domain`, and its `IEntityTypeConfiguration` in `Infrastructure/Data/Configurations/<Area>/` (picked up by `ApplyConfigurationsFromAssembly`).
2. DTOs in `Contracts/<Area>/{Requests,Responses}/`.
3. The command/query, handler and validator in `Application` (registered by assembly scanning). A handler that reads or changes patient data records it in the activity log (see `docs/agents/privacy.md`).
4. A new repository: interface in `Application/<Area>/Repositories/`, implementation in `Infrastructure/Data/Repositories/`, registered in `AddRepositories()` (`MedApp.Infrastructure/Common/Extensions/ServiceExtensions.cs`).
5. The controller action maps the request into a command, calls `mediator.Send(...)` and returns the result. Keep business logic out of controllers.
6. A schema change needs an EF migration, reviewed (see [Migrations](#migrations)). Migrations are **not** applied at startup.
7. Unit tests for the handler and the validator.
8. If it alters what personal data is held, who sees it, where it goes (a recipient, a sub-processor, a region) or how long it's kept, update `docs/governance/` in the same PR: at least `data-inventory.md` and `dpia.md`, and any document it makes untrue (`docs/governance/README.md`).

### Validation and errors

- Validation runs in the MediatR pipeline (`ValidationBehaviour`, `Application/Common/Behaviors`) and throws `FluentValidation.ValidationException`, which `API/Middleware/ExceptionHandling.cs` turns into a 400 `ValidationProblemDetails`.
- **Preferred for new code:** throw `NotFoundException` (404) or `ConflictException` (409) from handlers, as Procedures does. A wrong username or password throws `InvalidCredentialsException` (401).
- An unmatched `/api` path is a 404; any other unknown path gets the React app's `index.html`, so client routes work on reload.
- The Patients and Auth slices predate that and return `null`/`bool`/`IdentityResult` for controllers to choose the status (except deleting a patient with procedures or appointments, which throws `ConflictException`). Keep that style there unless the task is to migrate them.
- Unhandled exceptions aren't caught by the middleware: they're 500s.

### Lists

- Lists are filtered, sorted and paged by the API: `GET api/patients/search`, `GET api/auth/users/search`, `GET api/procedures/patient-procedures/search`, `GET api/procedures/audit/search`, `GET api/activity/search`, `GET api/patients/retention-review` and `GET api/appointments/search` return a `PagedResponse<T>` (`items`, `page`, `pageSize`, `totalCount`, `totalPages`). `pageSize` defaults to 10, maximum 100.
- Sort and filter in plain LINQ that EF can translate (see `PatientSearch`), so the same code runs in SQL and unit tests. Always end the order with a unique key (`ThenBy(x => x.Id)`), or paging can repeat or skip rows.
  - Exception: the retention review is narrowed in SQL, then sorted and paged in memory by the schedule's age-dependent rules (`SearchRetentionReviewHandler`).
- **A feed pages by cursor:** a patient's timeline (`GET api/patients/{id}/timeline`) uses keyset paging, so new entries don't shift pages: `AtUtc` then `Id`, descending, with `NextCursor` encoding the last entry's pair (`PatientTimelineSearch`, `TimelineCursor`).
- `GET api/patients` and `GET api/auth/users` return everything, for the deprecated Blazor client only. Prefer the search endpoints.

### Mapping

Domain entities become `Contracts` responses through **hand-written static mappers**, one per area. Don't use a mapping library (no AutoMapper or Mapster).
- **Where:** the area's `Common/` folder, named `<Thing>Mapper`, with `ToResponse(entity)` plus overloads for what the entity lacks (e.g. `ToResponse(definition, usage)`, or `ToResponseAsync(...)` to look up usernames).
- **Mappers so far:**
  - `PatientMapper`, `UserSettingsMapper`, `SessionMapper`, `NotificationMapper` (with preferences), `AuditEntryMapper`, `AttachmentMapper`, `MonitoringRuleMapper` (names from `MonitoringRuleNames`), `EmailMapper`, `TwoFactorMapper`, `SecuritySettingsMapper`
  - `ProcedureMapper`, `ProcedureNoteMapper`, `NoteDeletionRequestMapper`, `StageDefinitionMapper`, `StageTemplateMapper`, `TimelineMapper`, `ProcedureInsightsMapper` (from totals)
  - `AppointmentMapper`, `CalendarMapper`, `CalendarSetupMapper` (activity types, day and week templates), `MeetingMapper` (meetings and free times)
  - `FhirPatientMapper` (a UK Core Patient), `DemographicsMapper`
  - `UserMapper`, in Infrastructure, because `ApplicationUser` is an Infrastructure type
- **Paged responses:** `PagedMapper.ToResponse(result, page, pageSize, Mapper.ToResponse)` (`Common/Paging`) turns a repository's `PagedResult` into a `PagedResponse`.
- **No inline mapping:** handlers and controllers don't build responses from entities, and queries never return Domain entities to the API. Non-entity results (a login's session, a generated password, the user's claims) may be built where they're made.
- **Tests:** every mapper has its own (`<Thing>MapperTests`), including what it computes (`CanBeDeleted`, names of actions or kinds, read or not).

### Data access

- Each repository method calls `SaveChangesAsync` itself: no unit of work. Reads use `AsNoTracking()`; updates go through `context.X.Update(entity)`.
- **Procedures are the exception:** handlers load a tracked procedure with `GetForUpdateAsync`, change it through `ProcedureChange`, and `IPatientProcedureRepository.SaveAsync(ProcedureChange)` saves it, its audit entries and any appointments it requested or cancelled (with their history) in one `SaveChangesAsync`, so no change can miss the audit log.
- Pass `CancellationToken` all the way through; tests check it.
- IDs are `Guid`s generated in handlers (`Guid.NewGuid()`). Timestamps (`CreatedAt`, `LastUpdated`) are UTC, set in handlers.
- **Times are stored and sent as UTC, with a `Z`.** `datetime2` keeps no zone, so `UtcDateTimeConverter` (every `DateTime` and `DateTime?`, in `MedAppDbContext.ConfigureConventions`) marks what EF reads as UTC; otherwise browsers read it as local time, an hour out in summer. The web app shows UK time (`formatDateTime`, `MedApp.Web/src/utils/dates.ts`) and reads a time without `Z` as UTC. Due dates and dates of birth are dates, never shifted; `dueDateUtc` is the end of the due day (UK) in UTC, for comparing with now: show `dueDate`.
- "Now" comes from a `TimeProvider` (registered in `AddApplication`); tests pass a `FixedTimeProvider`.

### Migrations

- Migrations live in `MedApp.Infrastructure/Migrations/` with `MedAppDbContextModelSnapshot.cs`: one linear history from `InitialCreate`. Always add new ones on top; never edit or delete one that has been applied to a database.
- Name them in the past-tense commit style, e.g. `RemovedTemplateAuditColumns`, `RenamedTasksToProcedures`.
- **Always read the scaffolded migration before keeping it.** EF can't detect renames of entities or of properties used as keys, so it scaffolds them as `DropTable` + `CreateTable`, deleting the data. When `migrations add` warns "An operation was scaffolded that may result in the loss of data", rewrite `Up` and `Down` by hand: drop the keys and foreign keys whose names change, `RenameTable`, `RenameColumn` and `RenameIndex`, then re-add the keys and foreign keys with the same `onDelete`. `RenamedTasksToProcedures` is the reference.
- **A migration test reads only the columns that exist at its step:** project (`.Select(n => new { n.Id, ... })`) or use SQL, never whole entities (`ToListAsync()` on a `DbSet`), as a later migration's columns would be asked for ("Invalid column name"). Counting (`CountAsync`, `AnyAsync`) is fine.
- After editing a migration, `has-pending-model-changes` must report no changes, or the snapshot and model disagree.
- Test a non-trivial migration up **and** down on a throwaway LocalDB database, never the dev `MedApp` one: `dotnet ef database update --connection "Server=(localdb)\MSSQLLocalDB;Database=MedApp_MigrationTest;Trusted_Connection=True;TrustServerCertificate=True"`, seed synthetic rows, apply, roll back with `database update <PreviousMigration>`, then drop the database.
- Apply a migration to someone's local `MedApp` database only when asked, after a `medapp-dev` `save_snapshot`: it changes their data.

## Frontend conventions (MedApp.Web)

All UI work goes in `MedApp.Web`; its conventions are in `MedApp.Web/AGENTS.md` (loaded in that folder), layout and scripts in `MedApp.Web/README.md`. In short:
- UI is only Astryx components, or composites in `src/components/`; icons come from `AppIcon`.
- Pages are in `src/pages/`, with thin route files in `src/routes/`. List pages keep page, sort and filters in the URL.
- Data goes through TanStack Query and the typed `api` client (`src/api/`).
- Every component and page has a Vitest test.
- After an API DTO or endpoint change, run `npm run gen:api` with the AppHost running, and commit `src/api/schema.d.ts`.

### The deprecated Blazor client

`MedApp.Client.Deprecated`, the original Blazor UI, is kept for reference only (plan Q5b). The AppHost doesn't run it and the API doesn't allow its origin. Make no changes beyond keeping it compiling. Its `RootNamespace` and `AssemblyName` are still `MedApp.Client`.

### UI parity screenshots

`tools/ui-parity/` is the Playwright tool that compared Blazor and React screens during the migration (`tools/ui-parity/report.html`); a development aid, not E2E tests.
- **Capture only the screens your change affects,** by name from `screens.ts`, and look at their `react.png`: with the AppHost running, in `tools/ui-parity`, `npm run parity:react -- -g "patient-view|patients-list"`. For a shared component (`Page`, `DataTable`, the side nav), two or three screens that use it. **The whole set** (`npm run parity:react`, about a minute, and costly to read) only for a change touching many screens (layout, theme) or when asked; it isn't part of merging.
- The Blazor screenshots on disk are the last pre-cutover baseline; recapturing needs `MedApp.Client.Deprecated` run by hand and its origin allowed again.
- **New screens or states** go in `tools/ui-parity/screens.ts`. Never make a screen confirm a destructive action; only open the dialog.
- **Credentials** live in the git-ignored `tools/ui-parity/.env`; never commit them. Setup: `tools/ui-parity/README.md`.

### Dev MCP server (`medapp-dev`)

`.mcp.json` registers `medapp-dev` (`tools/MedApp.DevMcp`) and the Astryx docs MCP (`astryx`). **Use `medapp-dev` for local account and app-state chores** (accounts, roles, sessions, synthetic patients, migrations, snapshots, `reset_demo_data`), not hand-written SQL or one-off programs. Take a `save_snapshot` before a destructive experiment. Everything else, including the demo data: `docs/agents/dev-mcp.md`.

## Local dev gotchas

Docker, memory, the dev server, server-side rendering and the production image: `docs/agents/local-dev.md`. In short: Docker Desktop must be running for the AppHost; an empty database isn't migrated at startup (apply the migrations, then `reset_demo_data`); if `/api` calls return the app's HTML, restart the AppHost.

## Testing conventions

- The stack is NUnit 3, FluentAssertions 8 (its licence banner is expected), Moq and AutoFixture with AutoMoq.
- The test tree mirrors the source (`MedApp.UnitTests/Application/<Area>/Commands/<Action>/<Action>HandlerTests.cs` and `...ValidatorTests.cs`), except `Authentication` is `Auth` and `Sessions` is `Session`.
- Fixtures are `[TestFixture] public sealed class`, set up in `[SetUp]`: `AutoFixtureFactory.Create()`, `_fixture.Inject(mock.Object)`, then `_fixture.Create<Handler>()`.
- Names follow `Method_Scenario_ExpectedResult` (`Handle_PatientDoesNotExist_ReturnsNull`).
- Shared test data goes in `MedApp.UnitTests/Common/Constants/*TestConstants.cs`, not inlined magic values.
- **Synthetic data only:** never realistic patient PII in tests, seeds or fixtures. **NHS numbers come only from the 999 range** (`999 xxx xxxx`, valid check digit), never issued to real people; never one outside it, even made up, as it could be someone's.
- Procedure handler tests derive from `ProcedureHandlerTestBase` (mocked repository and username lookup, fixed clock) and build procedures with `ProceduresTestConstants.CreateProcedure`.
- Handlers that write the activity log get a `RecordingActivityLog` (`_fixture.Inject<IActivityLog>(...)`), which makes entries as the real one does and keeps them to check.

**Integration and end-to-end tests:** how to write them is in `docs/agents/testing.md`. Work through the API where you can, use synthetic data, and clean up what you make.

## CI

`.github/workflows/main.yml` runs on PRs to `main`. `web`, `helm`, `unit-tests`, `image`, `integration-tests` and `e2e` are required checks on `main`. Job detail, coverage, CodeQL, Dependabot and the supply chain rules: `docs/agents/ci.md`.
- **A push of only `docs/`** after a passing run skips the build and tests (the `changes` job). **Never put a file that a build or test reads under `docs/`.**
- **New code comes with tests.** Leave a branch uncovered only when it's unreachable or needs a real browser or database, and say so in the PR. `coverage-comment` lists .NET classes and React files under 80% line coverage.
- **Each merge to `main` deploys** (the Publish workflow, `docs/agents/deploy.md`): after a merge, its run is the deploy, checked as you check CI.

### Checking CI on PRs you opened or pushed to

After opening a PR, or pushing to one you opened, check its CI result before calling the work done. Don't stop at "pushed".

**Don't block the user while CI runs.** Hand steps 1–3 to a background subagent (Claude Code's `Agent` tool, which notifies you when done), tell the user it's watching CI, and carry on with what they ask next. Give it the branch, the pushed commit SHA and this section. The subagent's rules:
- **Read-only:** no edits, commits or pushes. It reports; it doesn't fix.
- **Waiting:** poll about every 30 seconds with the harness's wait mechanism (a background command or monitor; foreground `sleep` may be blocked). A run usually takes about 9 minutes, under one for a docs-only push after a passing run. After 15 minutes, report it as still pending, with its URL.
- **One PR each:** a watcher waits for one PR's run, never several, so a ready PR isn't held up by another's CI.
- **Report:** the run URL and result; for a failure, the failing step, the error, the cause, whether it's already broken on `main`, and a proposed fix as an exact diff.

When it reports, do step 4 yourself without interrupting the user's current task: mention the result at the next natural break, or at once if it failed.

1. **Find the run for your commit.** The repo is public, so the GitHub REST API needs no auth; use `gh` if it's installed and logged in.
   - `GET https://api.github.com/repos/Aliator/MedApp/actions/runs?branch=<branch>`. Trust only a run whose `head_sha` is the commit you pushed. A new run can take a minute to appear, and until then the API returns the previous one: say the check is pending and look again later. Never report the old run's result as the new one.
   - Then read its jobs (`/actions/runs/<run_id>/jobs`) for each step's `conclusion`.
2. **If it passed**, say so in one line, with any coverage problems from the sticky PR comment (handlers, validators or React files under 80%).
3. **If it failed**, find the cause before reporting:
   - Note the failing step. A failure in "Set up job" means the workflow is broken (bad action ref, runner image), not the code.
   - Read the annotations (`GET /repos/Aliator/MedApp/check-runs/<job_id>/annotations`); full logs need auth (`gh run view <run_id> --log-failed`).
   - Reproduce only the failing tests locally, as CI runs them: `--configuration Release` with a `--filter`, or the `web` job's npm script on the failing files.
   - Check whether it's yours or already broken on `main` (say, when your branch doesn't change the workflow).
4. **Ask the user before fixing:** the failing step and error, the cause, whether your change caused it, and the fix as an exact diff. Wait for approval before committing or pushing it, unless they've already told you to fix it. After pushing a fix, check the new run (step 1).

## Git workflow

- Branch from `main` as `feature/<name>`, and merge back through a PR to `main`.
- Commit messages are short, capitalised and past tense ("Updated patient task routing").
- Never commit `bin/`, `obj/`, `.idea/` or `*.DotSettings.user`.

### Effort for each stage of a change

The user wants each change made in three stages, each at its own reasoning effort (Claude Code's Agent tool `effort`):

| Stage | Effort | Who does it |
|---|---|---|
| **Thinking:** understanding the task, reading the code, the design and the plan | **xhigh** | The main session (its effort setting is `xhigh`). |
| **Implementation:** the code, tests and docs, to the plan | **high** | A subagent with `effort: "high"`, given the plan, the files and the conventions. The main session checks its work (a build, the tests for what changed, a read of the diff) before calling it done. |
| **Review:** a fresh, adversarial look at the whole PR | **xhigh** (**high** for a small change) | A subagent with `effort: "xhigh"` that wasn't told the author's conclusions, as part of every merge (below). |

**Small changes are lighter:**
- **What counts:** a fix or tweak in a few files, with no new endpoint, table, migration or dependency, and nothing touching sign-in, security, privacy rules, patient data, clinical safety, CI or deploying. When unsure, it isn't small.
- **Implementation:** the main session makes it itself; a subagent's cold start costs more than the change.
- **Review:** at **high** (`effort: "high"`), not xhigh, which was the longest stage of recent small changes (a quarter to a third of the time from asking to deployed).
- Small, mechanical edits (a typo, a one-line config change) need no stages until they're shipped.

### "Merge it" or "ship it": the full merge flow

**Only run this when the user explicitly asks you to merge** ("merge it" or "ship it", which mean the same). Otherwise stop at committing or an open PR. Opening a PR or seeing CI pass is not permission to merge.

**The medical device decision:** a change that adds calculations, scores, dose checks, decision support, triage or prioritising, alerts from clinical values, or medical claims must update `docs/clinical-safety/intended-purpose-and-device-decision.md` before it merges.

When the user says to merge, do every step, in order, without asking again:
1. **Commit** outstanding changes on the feature branch (branching from `main` first if you're on it), and push. New scripts need their executable bit set in git (`git update-index --chmod=+x`), as this checkout doesn't track file modes.
2. **Open a PR** to `main`, or reuse the existing one.
3. **Review the PR at xhigh** (high for a small change: see [Effort](#effort-for-each-stage-of-a-change)) with a subagent (`effort: "xhigh"` or `"high"`), written up as a review document:
   - **A small change's reviewer checks it's small:** a review at high starts by saying whether the change meets the Effort rule's definition of small; if not, it's reviewed again at xhigh.
   - **What to review:** the PR's whole diff against `main` (`git diff main...HEAD`), its description and the code around the changes. Give the reviewer the PR and the files, not your conclusions, so its view isn't anchored on yours.
   - **What to look for:** correctness, security, data loss, operational and deploy risks, judgement (was this the right design?), docs matching code, whether `docs/governance/` is still true (a change to what personal data is held, who sees it, where it goes or how long it's kept must update it in the same PR), and whether the tests really test the change. Every finding needs file:line evidence, a concrete failure scenario, a severity (critical, high, medium, low), a confidence and a suggested fix.
   - **How the reviewer checks:** by reading the code, and for anything it doubts, the narrowest test (a filtered unit test run, or Vitest on named files). It starts no Docker, AppHost, kind cluster or browser, and runs no full suite: CI does, and step 4 waits for it.
   - **The document:** `docs/reviews/pr-<number>-<short-name>.md`, committed and pushed to the branch so it merges with the PR, in the [Review document format](#review-document-format).
   - **If it finds anything high or critical, stop.** Don't merge. Check those findings yourself against the code, report them to the user with your proposed fixes, and wait. Medium and low findings are recorded and don't block; mention them when you report the merge.
4. **Wait for the workflow** run whose `head_sha` is the last pushed commit (with the review document) to complete ([Checking CI](#checking-ci-on-prs-you-opened-or-pushed-to)). Here, waiting is the point. A push of only the review document after a passing run skips the build and tests (`changes`) and takes under a minute; review fixes or a merge from `main` run everything.
   - **If it fails, stop.** Don't merge. Report it and propose a fix, as in step 4 of [Checking CI](#checking-ci-on-prs-you-opened-or-pushed-to).
5. **Merge** with a merge commit (`gh pr merge <n> --merge`), matching the "Merge pull request #N" history. Stash any uncommitted local changes first, or the merge's local step fails after GitHub has already merged.
6. **Delete the branch** on GitHub and locally (`--delete-branch` does both).
7. **Pull `main` locally** (`git switch main`, `git pull`), then confirm the merge commit and a clean working tree.
8. **Check the deploy:** the merge runs Publish, which deploys through the environment branches (`deploy/argocd/README.md`). Check its run for the merge commit succeeded, as with CI. Green only means `env/kind` moved, not that the cluster took it.
   - **If the kind cluster is running,** check Argo CD's `medapp` Application is **Synced and Healthy** at that commit (`kubectl --context kind-medapp -n argocd get application medapp`).
   - **Don't start the cluster just for this:** Argo CD catches up with `env/kind` when it next starts, and a deploy change was already tried on kind before merging ([Commands](#commands), Testing a change).
   - A change adding a secret store entry needs `deploy/kind/up.sh --argocd` run against the existing cluster before it merges (`deploy/kind/README.md`).

### Review document format

Write each review so someone who didn't make the change can follow it quickly: short sections, bullets with bold lead-ins, short sentences, and any non-obvious term explained where it first appears. Keep the depth in the findings and the detail at the end.

Start with the PR's link and branch, the commit reviewed and the `main` compared with, the date, and whether any finding is high or critical. Then these sections, with these names, in this order (a review may add sections, but not rename or drop these):
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
