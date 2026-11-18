# AGENTS.md

Guidance for AI coding agents working in this repository.

## Overview

MedApp is a patient and pathway management app: a .NET 10 Web API (Clean Architecture, CQRS, MediatR) and a React frontend (`MedApp.Web`: React 19, TypeScript, Vite and Astryx). The original Blazor WebAssembly frontend is kept, deprecated, as `MedApp.Client.Deprecated`. Data is in SQL Server through EF Core, ASP.NET Core Identity handles users, and authentication is a custom cookie-based session scheme.

This file is the core: what every change needs. Each area's detail is in `docs/agents/` (index: `docs/agents/README.md`), read on demand: see [Read before you change](#read-before-you-change).

**The vocabulary** (pathways, pathway templates, the stage library), the names things had before, and the labels that differ from the code's names (the `Blocked` status shows as "On hold", `DueDate` as "Target date") are in [`docs/glossary.md`](docs/glossary.md).

## Read before you change

**Before changing anything in an area, read its file.** The rules on the right are the ones costly to break and easy to miss; they apply even if you skip the file.

| Area | Read `docs/agents/…` before changing | Never miss |
|---|---|---|
| Pathways, templates, stages, notes | `pathways.md` | Change a pathway only through `PathwayChange`, loaded with `GetForUpdateOrThrowAsync`, so it's audited. Audit details hold a note's ID and version, never its text. Only a note's author edits it; only admins delete one (soft). |
| Specialties, template and pathway specialties, staff memberships | `specialties.md` | Specialties are labels: they decide nothing clinical and limit nobody's reading. One in use is archived, never deleted; every change carries `expectedVersion`. |
| Referrals, their history and letters, the waiting-time clock | `referrals.md` | Change a referral only through `ReferralChange`, which saves its `ReferralEvent`s in the same save; the history holds no free text. Every read is logged (`ReferralViewed`, `PatientListed`). A restricted or anonymised record refuses changes, but withdrawing and entered in error stay allowed while restricted. Triage needs `TriageReferrals` and membership of the referral's specialty; never prefill or suggest an outcome, template or priority. |
| Letter templates, merge fields, the PDF engine, letters | `letters.md` | Merge fields copy identity and administrative data only, never a clinical value; the body is the author's. Change a letter only through `LetterChange`; send by secure email only through `ILetterTransport` (a declared relay, allowed domains, never a patient). PDFsharp and MigraDoc stay on 6.2.x (MIT; 6.5 is AGPL). |
| Specimens, the lab's worklist, specimen types and labs | `specimens.md` | Change a specimen only through `SpecimenChange` (audit details never hold its site, notes or any detail); its number stays out of URLs (`POST api/specimens/find`); MedApp never reads or interprets a report. A specimen's report can't be deleted on its own. |
| Patient timeline | `timeline.md` | Staff never get free text from the audit log or appointment history (`TimelineMapper`'s `PublicValueFields`; `TimelineStaffCanaryTests`). |
| Notifications | `notifications.md` | A title is a fixed phrase; a message holds only usernames, enums, admin-written names and dates, never free text or the patient. |
| Calendar, appointments, meetings, visits, sites and rooms, working hours, clinic sessions, patient reminders, Did not attend | `calendar.md` | Use `WorkingHours`, `WorkingTime`, `UkTime`, `CalendarClashes`, `ClinicRules`, `AppointmentWorkflow` and `AppointmentChange`; take `ICalendarLock` (the person's, and a room's) before checking clashes. Validators use `WorkingDays` with the person, so their hours and closed days count; never assume 08:00 to 18:00 on weekdays. A message to a patient never says why (`PatientMessageTexts`); nothing changes a pathway because of a missed appointment. |
| Notepad | `notepad.md` | Kept on the server only, never in browser storage. |
| Monitoring | `monitoring.md` | Every pathway command calls `IPathwayMonitor.NotifyAsync(change)` after saving. Clinical follow-up never relies on it. |
| Attachments | `attachments.md` | Types come from the contents (`FileTypeSniffer`), never the name; SVG and HTML are refused. Files leave only through the API, as downloads. |
| Scheduled jobs, the operational purge | `jobs.md` | A job is idempotent, logs counts only, and runs under the lease (`IScheduledJobRunner`), never on its own timer. Patient records are never deleted by a job. |
| Activity log and patient privacy | `privacy.md` | See the privacy rules below. |
| Insights | `insights.md` | It rebuilds history from the audit details' formats (`{"from":…,"to":…}`): keep them. |
| NHS number, FHIR, SNOMED CT, PDS | `nhs-fhir-snomed.md` | Keep the NHS number out of new URLs. FHIR errors are OperationOutcomes, never ProblemDetails. |
| Sign-in, sessions, roles, permissions, headers | `auth.md` | Every endpoint names its permission (`[RequirePermission(Permission.X)]`), never a role name; handlers ask the caller's permissions (`PathwayActor.Permissions`), never `IsInRole`. Permissions are stored by name: renaming one needs a migration. Read the session cookie only through `ISessionCookieService`. Never add an inline script or `eval` (the CSP refuses them). An account action that changes how someone signs in ends their sessions in the same transaction. |
| Email, password reset, two-factor | `email-2fa.md` | Never clinical text, a password, an authenticator key or recovery codes in an email; never an address, password, token or code in a security event. |
| `medapp-dev` and the demo data | `dev-mcp.md` | While it runs, build and test in Release. A change to shared rules changes the demo data: keep `DemoActivityParityTests` passing. |
| Local setup, server rendering, the image | `local-dev.md` | The page server gets none of the API's secrets. A change to the API's project references updates the Dockerfile's `COPY` lines. |
| CI workflows, dependencies | `ci.md` | Pin actions to a full commit SHA; give each job only the permissions it needs; never switch a failing gate off (accept one issue for a dated time). |
| Deploying (kind, Argo CD, Azure, Publish) | `deploy.md` | The chart is locked down: a new container, port or connection needs a matching change. Never run Azure's `deploy.sh` or `bootstrap.sh` unasked. Never commit to an `env/*` branch by hand except in an emergency. |
| Integration and E2E tests | `testing.md` | An account an admin made must change its generated password before any other `/api` call (`CreateStaffAsync` does). E2E tests make their own data through the API and register every clean-up. |

**Stored enum numbers are never renumbered:** `PatientPathwayStatus`, `PatientPathwayAuditAction` (with the attachment actions), `StageSkipReason`, `NoteDeletionReason`, `NoteDeletionRequestStatus`, `NotificationKind`, `AppointmentStatus`, `AppointmentEventAction`, `VisitRequirement`, `MonitoringEvent`, `ActivityAction`, `MeetingReply`, `TwoFactorMethod`, `ScheduledJobOutcome`, `ReferralStatus`, `ReferralRejectionReason`, `ReferralEventAction`, `ClockPauseReason`, `ClockStopReason`, `LetterKind`, `ReminderChannel`, `PatientMessageKind`, `PatientMessageChannel`, `PatientMessageStatus`, `DnaFollowUpDecision`, `SpecimenStatus` and `SpecimenRejectionReason`. `LetterStatus`, `LetterRecipientKind`, `LetterDispatchStatus` and `LetterDispatchMethod` too. Add new values with new numbers.

**Privacy rules for every area** (`privacy.md`):
- **Every read and change of patient data is logged** in the activity log (`IActivityLog`: `RecordViewsAsync` for reads; a change passes its entry to the repository's save). It fails closed. Classify a new GET endpoint in `EndpointCoverageTests`.
- **Free text never reaches** staff timelines, notifications, emails or logs, and activity details never hold patient details or clinical text.
- **A new table holding patient data** is added to `PatientRecordRepository`, so the export and anonymisation cover it.
- **A restricted or anonymised record can't change** (`PatientRules.EnsureCanChange`, a 409), nor its pathways, appointments, referrals or files.
- **A change to what personal data is held,** who sees it, where it goes or how long it's kept updates `docs/governance/` in the same PR.

**Briefing a subagent:** name the area files it needs (`docs/agents/calendar.md`), rather than pasting their content.

## Keeping this file small

- **The limit:** this core file stays at or under **40,000 characters** (`wc -m AGENTS.md`), as it's loaded into every session and subagent. Claude Code's own hard limit is 150,000 characters; the file must never get near it again. CI's `changes` job fails above 40,000.
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

**Testing a change: the least that checks it.** MedApp isn't live yet, so speed comes first, deliberately: a PR's CI runs a light tier by default, `main` gets the full suite after each merge, and what that finds is fixed afterwards ([CI](#ci)). Locally and in CI, run only what checks your change:
- **Which CI tier:**
  - **A small fix** (as [Effort](#effort-for-each-stage-of-a-change) defines small): label the PR `ci:none`, and CI builds and tests nothing.
  - **A UI change:** the default light tier (lint, type check, the changed files' own Vitest tests), plus the screenshots below, looked at locally.
  - **A minor backend change:** the default light tier (the solution's build and the unit tests).
  - **Integration and E2E tests are for major changes, and the owner decides.** Before running them, locally or with the `ci:full` label, ask the owner and say why (a migration, SQL translation, sign-in, sessions or permissions, a race, the image or server rendering). Don't run them unasked.
- **Use Release** (`-c Release`): a Debug build of either test project rebuilds `medapp-dev` into the folder the running tool has locked (see `docs/agents/dev-mcp.md`).
- **Build** the projects you changed, with no new warnings (`dotnet build <project> -c Release`), and run only their unit tests (`--filter`).
- **`MedApp.Web`:** the changed files' tests (`npx vitest run <files>`), `npm run typecheck`, and `npx eslint <files>`.
- **Integration tests** are only for what unit tests can't reach (SQL translation, Identity, a migration, middleware, the demo data seed): ask the owner first (above), then run only that fixture (`--filter "FullyQualifiedName~<Fixture>"`).
- **A visible change is looked at in a browser** (nothing in CI does): with the AppHost running, capture and look at the UI parity screenshots of just the screens it touches, and run the axe audit on just those (`npm run a11y -- -g "<screen>"` in `tools/ui-parity`), as the accessibility statement promises.
- **A change to how MedApp is deployed is tried on kind** before it merges, once the owner agrees to start the cluster (it's heavy, and the only test that installs the chart): the chart (`deploy/helm`), the Dockerfile or the image's contents, `deploy/kind`, `deploy/argocd`, or a new setting, secret, port, container or connection the API needs outside Development. Run `deploy/kind/up.sh` and check the app answers.
- **Not locally**, unless the user asks or you're chasing one failing test: the full unit, web or integration suites, E2E, the whole UI parity set or every screen's axe audit.
- **When CI fails,** run only the failing tests locally.

## Solution layout

Dependencies point inward: `API → Application → Domain` and `Infrastructure → Application`. `MedApp.AppHost` sits outside and references what it runs.

| Project | Responsibility |
|---|---|
| `MedApp.Domain` | Entities and enums (`Patient`, `PatientPathway`, `PatientPathwayStage*`, `Attachment`, `Specimen`, `CalendarBlock`, `CalendarActivityType`, `ClosedDay`, `Site`, `Room`, `SiteHours`, `StaffHours`, `ClinicSession`, `ClinicPattern`, `Appointment`, `UserSession`, `Specialty`, `Referral`, ...). No dependencies. |
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
- **Preferred for new code:** throw `NotFoundException` (404) or `ConflictException` (409) from handlers, as Pathways does. A wrong username or password throws `InvalidCredentialsException` (401).
- An unmatched `/api` path is a 404; any other unknown path gets the React app's `index.html`, so client routes work on reload.
- The Patients and Auth slices predate that and return `null`/`bool`/`IdentityResult` for controllers to choose the status (except deleting a patient with pathways or appointments, which throws `ConflictException`). Keep that style there unless the task is to migrate them.
- Unhandled exceptions aren't caught by the middleware: they're 500s.

### Lists

- Lists are filtered, sorted and paged by the API: `GET api/patients/search`, `GET api/auth/users/search`, `GET api/pathways/patient-pathways/search`, `GET api/pathways/audit/search`, `GET api/activity/search`, `GET api/patients/retention-review`, `GET api/appointments/search`, `GET api/referrals/search` and `GET api/specimens/search` return a `PagedResponse<T>` (`items`, `page`, `pageSize`, `totalCount`, `totalPages`). `pageSize` defaults to 10, maximum 100. Clinic sessions (`GET api/clinics/sessions`) are a plain list, of at most 62 days.
- Sort and filter in plain LINQ that EF can translate (see `PatientSearch`), so the same code runs in SQL and unit tests. Always end the order with a unique key (`ThenBy(x => x.Id)`), or paging can repeat or skip rows.
  - Exception: the retention review is narrowed in SQL, then sorted and paged in memory by the schedule's age-dependent rules (`SearchRetentionReviewHandler`).
- **A feed pages by cursor:** a patient's timeline (`GET api/patients/{id}/timeline`) uses keyset paging, so new entries don't shift pages: `AtUtc` then `Id`, descending, with `NextCursor` encoding the last entry's pair (`PatientTimelineSearch`, `TimelineCursor`).
- `GET api/patients` and `GET api/auth/users` return everything, for the deprecated Blazor client only. Prefer the search endpoints.

### Mapping

Domain entities become `Contracts` responses through **hand-written static mappers**, one per area. Don't use a mapping library (no AutoMapper or Mapster).
- **Where:** the area's `Common/` folder, named `<Thing>Mapper`, with `ToResponse(entity)` plus overloads for what the entity lacks (e.g. `ToResponse(definition, usage)`, or `ToResponseAsync(...)` to look up usernames).
- **Mappers so far:**
  - `PatientMapper`, `UserSettingsMapper`, `SessionMapper`, `NotificationMapper` (with preferences), `AuditEntryMapper`, `AttachmentMapper`, `MonitoringRuleMapper` (names from `MonitoringRuleNames`), `EmailMapper`, `TwoFactorMapper`, `SecuritySettingsMapper`, `SpecialtyMapper`, `ReferralMapper`, `RoleMapper` (roles and the permission catalogue), `PracticeMapper` (with `ToSummary`), `StaffDetailsMapper`, `LetterTemplateMapper` (with merge fields), `SpecimenMapper` (specimens, types and labs)
  - `PathwayMapper`, `PathwayNoteMapper`, `NoteDeletionRequestMapper`, `StageDefinitionMapper`, `PathwayTemplateMapper`, `TimelineMapper`, `PathwayInsightsMapper` (from totals)
  - `AppointmentMapper`, `CalendarMapper`, `CalendarSetupMapper` (activity types, day and week templates), `ClosedDayMapper`, `MeetingMapper` (meetings and free times), `SiteMapper` (sites and rooms), `WorkingHoursMapper` (staff and site hours), `ClinicMapper` (clinic sessions and patterns), `PatientMessageMapper` (texts and emails to patients)
  - `FhirPatientMapper` (a UK Core Patient), `DemographicsMapper`
  - `UserMapper`, in Infrastructure, because `ApplicationUser` is an Infrastructure type
- **Paged responses:** `PagedMapper.ToResponse(result, page, pageSize, Mapper.ToResponse)` (`Common/Paging`) turns a repository's `PagedResult` into a `PagedResponse`.
- **No inline mapping:** handlers and controllers don't build responses from entities, and queries never return Domain entities to the API. Non-entity results (a login's session, a generated password, the user's claims) may be built where they're made.
- **Tests:** every mapper has its own (`<Thing>MapperTests`), including what it computes (`CanBeDeleted`, names of actions or kinds, read or not).

### Data access

- Each repository method calls `SaveChangesAsync` itself: no unit of work. Reads use `AsNoTracking()`; updates go through `context.X.Update(entity)`.
- **Pathways are the exception:** handlers load a tracked pathway with `GetForUpdateAsync`, change it through `PathwayChange`, and `IPatientPathwayRepository.SaveAsync(PathwayChange)` saves it, its audit entries and any appointments it requested or cancelled (with their history) in one `SaveChangesAsync`, so no change can miss the audit log.
- Pass `CancellationToken` all the way through; tests check it.
- IDs are `Guid`s generated in handlers (`Guid.NewGuid()`). Timestamps (`CreatedAt`, `LastUpdated`) are UTC, set in handlers.
- **Times are stored and sent as UTC, with a `Z`.** `datetime2` keeps no zone, so `UtcDateTimeConverter` (every `DateTime` and `DateTime?`, in `MedAppDbContext.ConfigureConventions`) marks what EF reads as UTC; otherwise browsers read it as local time, an hour out in summer. The web app shows UK time (`formatDateTime`, `MedApp.Web/src/utils/dates.ts`) and reads a time without `Z` as UTC. Target dates and dates of birth are dates, never shifted; `dueDateUtc` is the end of the due day (UK) in UTC, for comparing with now: show `dueDate`.
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

`.mcp.json` registers `medapp-dev` (`tools/MedApp.DevMcp`) and the Astryx docs MCP (`astryx`). **Use `medapp-dev` for local account and app-state chores** (accounts, roles, sessions, synthetic patients, migrations, snapshots, `reset_demo_data`), not hand-written SQL or one-off programs. Take a `save_snapshot` before a destructive experiment. **Demo data comes from one script, never by hand:** `tools/seed-demo-data.sh --confirm --keep-users dev-parity` (or `.ps1`) applies pending migrations, then runs the same seeder as `reset_demo_data`. Everything else: `docs/agents/dev-mcp.md`.

## Local dev gotchas

Docker, memory, the dev server, server-side rendering and the production image: `docs/agents/local-dev.md`. In short: Docker Desktop must be running for the AppHost; an empty database isn't migrated at startup (`tools/seed-demo-data.sh --confirm` applies the migrations, then loads the demo data); if `/api` calls return the app's HTML, restart the AppHost.

## Testing conventions

- The stack is NUnit 3, FluentAssertions 8 (its licence banner is expected), Moq and AutoFixture with AutoMoq.
- The test tree mirrors the source (`MedApp.UnitTests/Application/<Area>/Commands/<Action>/<Action>HandlerTests.cs` and `...ValidatorTests.cs`), except `Authentication` is `Auth` and `Sessions` is `Session`.
- Fixtures are `[TestFixture] public sealed class`, set up in `[SetUp]`: `AutoFixtureFactory.Create()`, `_fixture.Inject(mock.Object)`, then `_fixture.Create<Handler>()`.
- Names follow `Method_Scenario_ExpectedResult` (`Handle_PatientDoesNotExist_ReturnsNull`).
- Shared test data goes in `MedApp.UnitTests/Common/Constants/*TestConstants.cs`, not inlined magic values.
- **Synthetic data only:** never realistic patient PII in tests, seeds or fixtures. **NHS numbers come only from the 999 range** (`999 xxx xxxx`, valid check digit), never issued to real people; never one outside it, even made up, as it could be someone's.
- Pathway handler tests derive from `PathwayHandlerTestBase` (mocked repository and username lookup, fixed clock) and build pathways with `PathwaysTestConstants.CreatePathway`.
- Handlers that write the activity log get a `RecordingActivityLog` (`_fixture.Inject<IActivityLog>(...)`), which makes entries as the real one does and keeps them to check.

**Integration and end-to-end tests:** how to write them is in `docs/agents/testing.md`. Work through the API where you can, use synthetic data, and clean up what you make.

## CI

`.github/workflows/main.yml` runs on PRs to `main` in a **tier** chosen by the `changes` job from the PR's labels and diff, and in full on `main` after every merge and nightly. Detail (the tiers' path rules, each job, coverage, CodeQL, Dependabot, the supply chain rules): `docs/agents/ci.md`.
- **Tiers:** `none` (docs only, or the `ci:none` label for a small fix); **light**, the default (`web-checks` for the web app, `unit-tests` for .NET); **full** (the `ci:full` label, which the owner decides). A change under `.github/` always runs everything, `deploy/` runs `helm`, and image inputs run `image`.
- **`main`'s full run gates nothing** (Publish deploys alongside it). A failure opens or updates the issue "Full suite failing on main".
- **The six required checks** (`web`, `helm`, `unit-tests`, `image`, `integration-tests`, `e2e`) mean "what the tier asked for passed".
- **Never put a file that a build or test reads under `docs/`:** a docs-only push skips every job.
- **New code comes with tests.** Leave a branch uncovered only when it's unreachable or needs a real browser or database, and say so in the PR.

### Checking CI after a merge

Merging doesn't wait for CI ([the merge flow](#merge-it-or-ship-it-the-full-merge-flow)). After a merge, at the next natural break:
- **One call:** `gh run list --commit <merge commit> --json workflowName,status,conclusion,url` shows `main`'s CI run (the full suite) and Publish for it. A push that changes nothing deployed has no Publish run.
- **If either failed,** tell the owner what failed (`gh run view <id> --log-failed` has the step and the error) and fix it on a new branch. Reproduce only the failing tests locally.
- **No polling loops and no watcher subagents.** Don't wait on a PR's own run unless the owner asks, or the PR is labelled `ci:full`.

## Git workflow

- Branch from `main` as `feature/<name>`, and merge back through a PR to `main`.
- Commit messages are short, capitalised and past tense ("Updated patient task routing").
- Never commit `bin/`, `obj/`, `.idea/` or `*.DotSettings.user`.
- **While MedApp isn't live,** the owner wants speed and few tokens: quick reviews, merging without waiting for CI, and failures fixed afterwards. What comes back before first live use is in `docs/todo.md` (Testing).

### Effort for each stage of a change

A subagent starts cold and reads the project instructions again, so use few, and cheap ones (in Claude Code, the Agent tool's `effort` and `model`):

| Stage | Effort | Who does it |
|---|---|---|
| **Thinking:** understanding the task, reading the code, the design and the plan | The session's own effort setting | The main session. |
| **Implementation:** the code, tests and docs, to the plan | The session's own; **high** for a subagent doing design-heavy work | The main session itself, unless the change is large (many files across layers) or several independent pieces can run in parallel: then subagents with `effort: "high"`, briefed with the area files they need. The main session checks their work (a build, the tests for what changed, a read of the diff). |
| **Mechanical work:** merging `main` into a branch, version bumps, checking a run | **low** or **medium** | A subagent with `model: "sonnet"`, if a subagent at all. |
| **Review:** a quick look at the PR's diff for what would hurt | **low** | One subagent with `effort: "low"` that wasn't told the author's conclusions, as part of every merge (below). |

- **A small change** (the meaning `ci:none` uses) is a fix or tweak in a few files, with no new endpoint, table, migration or dependency, and nothing that touches sign-in, security, privacy rules, patient data, clinical safety, CI or deploying. When unsure, it isn't small.
- **Reviews are quick while MedApp isn't live** (no xhigh or high reviews, and no check that a change is small). Full reviews come back before first live use (`docs/todo.md`, Testing); until then the owner can ask for one.

### "Merge it" or "ship it": the full merge flow

**Only run this when the user explicitly asks you to merge** ("merge it" or "ship it", which mean the same). Otherwise stop at committing or an open PR. Opening a PR or seeing CI pass is not permission to merge.

**The medical device decision:** a change that adds calculations, scores, dose checks, decision support, triage or prioritising, alerts from clinical values, or medical claims must update `docs/clinical-safety/intended-purpose-and-device-decision.md` before it merges.

When the user says to merge, do every step, in order, without asking again:
1. **Commit** outstanding changes on the feature branch (branching from `main` first if you're on it), and push. New scripts need their executable bit set in git (`git update-index --chmod=+x`), as this checkout doesn't track file modes.
2. **Open a PR** to `main`, or reuse the existing one, with its CI tier's label if it isn't the default ([Testing a change](#commands)).
3. **A quick review,** by one subagent with `effort: "low"`, given the PR's diff (`git diff main...HEAD`) and description, not your conclusions. It reads the diff only, runs nothing, and takes a few minutes.
   - **It looks only for what would hurt:** data loss, a security or privacy hole, a broken build or deploy, a stored enum renumbered, a migration that drops data. Style, test depth and polish are out of scope.
   - **The document:** `docs/reviews/pr-<number>-<short-name>.md`, in the [Review document format](#review-document-format), committed and pushed to the branch so it merges with the PR.
   - **Only a critical or high finding stops the merge.** Check it yourself against the code, report it to the user with your proposed fix, and wait. Anything else is listed, and fixed afterwards.
4. **Merge** with a merge commit, without waiting for CI: `gh pr merge <n> --merge --admin --delete-branch` (repository admins bypass `main`'s required checks: ruleset "Protect", since 18 November 2026). Only a critical or high finding, or a failing local check for the change's tier, stops it. Stash any uncommitted local changes first, or the merge's local step fails after GitHub has already merged.
5. **Pull `main` locally** (`git switch main`, `git pull`), then confirm the merge commit and a clean working tree.
6. **Afterwards, check `main`'s run and the deploy** ([Checking CI after a merge](#checking-ci-after-a-merge)). Publish deploys through the environment branches (`docs/agents/deploy.md`); green only means `env/kind` moved, not that the cluster took it.
   - **If the kind cluster is running,** check Argo CD's `medapp` Application is **Synced and Healthy** at that commit (`kubectl --context kind-medapp -n argocd get application medapp`).
   - **Don't start the cluster just for this:** Argo CD catches up with `env/kind` when it next starts.
   - A change adding a secret store entry needs `deploy/kind/up.sh --argocd` run against the existing cluster before it merges (`deploy/kind/README.md`).

### Review document format

A few lines, written for an engineer: precise and technical, not simplified.
- The PR's link and branch, the commit reviewed and the date.
- What the PR does.
- The findings, or "none": each with its `file:line`, its severity (critical, high, medium or low, as graded in `docs/reviews/README.md`) and what happened to it.
- Any remaining risk.

No fixed headings are needed. A full review is something the owner asks for, in whatever shape they ask.

Personal preferences for how reviews or explanations are written belong in a contributor's own git-ignored `CLAUDE.local.md`, not in this file.
