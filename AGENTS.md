# AGENTS.md

Guidance for AI coding agents working in this repository.

## Overview

MedApp is a patient and procedure management app. It has a .NET 10 Web API backend built with Clean Architecture, CQRS and MediatR, and a React frontend (`MedApp.Web`: React 19, TypeScript, Vite and Astryx). The original Blazor WebAssembly frontend is kept, deprecated, as `MedApp.Client.Deprecated`. Data is stored in SQL Server through EF Core, and ASP.NET Core Identity handles users. Authentication is a custom cookie-based session scheme.

## Commands

Run everything from the repo root. `global.json` pins the SDK to 10.0.x, and every project targets `net10.0`.

```bash
dotnet build MedApp.sln                     # build everything
dotnet test MedApp.UnitTests                # unit tests (CI's unit-tests job)
dotnet test MedApp.UnitTests --filter "FullyQualifiedName~UpdatePatient"   # run a subset
dotnet test MedApp.IntegrationTests         # the API against SQL Server in Docker (needs Docker running; about 1.5 min)

# End-to-end tests (Playwright) against the running AppHost, signed in as the local dev admin. They make their own
# synthetic data and delete what the app can delete (MedApp.E2ETests/README.md lists what stays). Install Chromium once: pwsh MedApp.E2ETests/bin/Debug/net10.0/playwright.ps1 install chromium
E2E_USERNAME=admin E2E_PASSWORD=admin dotnet test MedApp.E2ETests

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

To try the production image in Kubernetes, `deploy/kind/up.sh --demo-data kind-demo` runs MedApp on a local kind cluster at http://localhost:8080 (and https://localhost:8443), beside the AppHost and with a database of its own. It deploys with the Helm chart in `deploy/helm/medapp`, whose migrations Job runs the EF migrations bundle built into the image, then makes the first admin. The chart is locked down (network policies, the `restricted` Pod Security Standard, read-only filesystems, secrets from External Secrets), so a new container, port or connection in it needs a matching change there. Each API pod has two containers from the same image: `api`, and `pages`, the Node page server, with none of the API's secrets (see Server-side rendering under Local dev gotchas). `up.sh --argocd` has Argo CD deploy from git instead. Each merge to `main` runs the Publish workflow, which pushes the image to GHCR and records the commit, with the image's tag, on the `env/kind` branch that Argo CD follows (`deploy/argocd/README.md`). Roll back by reverting the change on `main`. Never commit to an `env/*` branch by hand except in an emergency, and know that the next deploy replaces it.

Azure (AKS, Azure SQL, Key Vault, Azure Container Registry) is in `deploy/azure`. It's written and checked, but **not deployed**. Its Bicep (`main.bicep`) is compiled and linted in CI. Deploying it costs money, so never run `deploy.sh` or `bootstrap.sh` without the user asking. Once it's set up, the Publish workflow's `azure` job deploys each merge through the `env/azure` branch; until then the job is skipped. Chart versions for both clusters are in `deploy/versions.sh`. See the `README.md` in each. CI lints the chart (the `helm` job), so after changing it run `helm lint --strict deploy/helm/medapp --set image.tag=ci`.

Before you call a change done, `dotnet build MedApp.sln` must succeed with no new warnings and `dotnet test MedApp.UnitTests` must pass. For a change to the API's behaviour, run `dotnet test MedApp.IntegrationTests` too. For a change in `MedApp.Web`, `npm run lint`, `npm run typecheck` and `npm test` must pass there too.

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
| `MedApp.AppHost` | .NET Aspire AppHost for local dev: runs the SQL Server container, the API and the React app. On publish, it builds the API's image from `MedApp.API/Dockerfile`, which holds the API, the built React app in `wwwroot`, and Node with the page server in `/app/ssr`. |
| `MedApp.ServiceDefaults` | Aspire service defaults (OpenTelemetry, health checks at `/health` and `/alive`), used by the API. The health checks are on the app's port in Development; elsewhere only on the port in `HealthChecks:Port` (8081 in Kubernetes, which nothing outside the pod reaches), and not at all without it. |
| `MedApp.UnitTests` | NUnit tests for Application handlers/validators, Infrastructure services, and the API's own services (`Api/`). |
| `MedApp.IntegrationTests` | NUnit tests of the real API in memory (`WebApplicationFactory<Program>`, run as Production) against SQL Server 2022 in Docker (Testcontainers): sign-in and sessions, authorisation, the forced password change, users, patients, procedures, notifications (with the SignalR hub), errors and the migrations. They also cover `SessionService` and `IdentityTransactions`, which unit tests can't. |
| `MedApp.E2ETests` | Playwright for .NET tests of `MedApp.Web`'s main journeys: signing in and out, patients, a procedure's stages, My Tasks, a new user's first sign-in (with the forced password change), deleting a user, the notification bell, and setting up two-factor authentication then signing in with a code. See its `README.md`. |

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
3. Add the command/query, handler and validator in `Application`. MediatR handlers and validators are registered by assembly scanning, so no DI changes are needed for them. A handler that reads or changes patient data records it in the activity log (see [Activity log and patient privacy](#activity-log-and-patient-privacy)).
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

- Lists are filtered, sorted and paged by the API: `GET api/patients/search`, `GET api/auth/users/search`, `GET api/procedures/patient-procedures/search`, `GET api/procedures/audit/search`, `GET api/activity/search` and `GET api/patients/retention-review` return a `PagedResponse<T>` (`items`, `page`, `pageSize`, `totalCount`, `totalPages`). `pageSize` defaults to 10, with a maximum of 100.
- Sort and filter in plain LINQ that EF can translate (see `PatientSearch`), so the same code runs in SQL and in unit tests. Always end the order with a unique key (`ThenBy(x => x.Id)`), or paging can repeat or skip rows.
  - The retention review is an exception too: SQL narrows it to patients last changed long enough ago, then the schedule's rules, which depend on each patient's age, sort and page it in memory (`SearchRetentionReviewHandler`).
- **The exception is a feed:** a patient's timeline (`GET api/patients/{id}/timeline`) pages by cursor (keyset) rather than by page number, so entries added meanwhile don't shift the pages. It orders by `AtUtc` then `Id`, both descending; a page's `NextCursor` encodes its last entry's pair, and the next page starts after it (`PatientTimelineSearch`, `TimelineCursor`).
- `GET api/patients` and `GET api/auth/users` return everything. Only the deprecated Blazor client uses them; prefer the search endpoints.

### Mapping

Domain entities become `Contracts` responses through **hand-written static mappers**, one per area. Don't use a mapping library (no AutoMapper or Mapster).
- **Where they live:** in the area's `Common/` folder, named `<Thing>Mapper`. Each has `ToResponse(entity)`, plus overloads for what a response needs that the entity doesn't have, for example `ToResponse(definition, usage)` or `ToResponseAsync(...)` when it looks up usernames.
- **Mappers so far:**
  - `PatientMapper`, `UserSettingsMapper`, `SessionMapper`, `NotificationMapper`, `AuditEntryMapper`, `EmailMapper`, `TwoFactorMapper`, `SecuritySettingsMapper`
  - `ProcedureMapper`, `StageDefinitionMapper`, `StageTemplateMapper`, `TimelineMapper`, `ProcedureInsightsMapper` (from totals, not entities)
  - `FhirPatientMapper` (a patient as a UK Core Patient) and `DemographicsMapper`
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

### Activity log and patient privacy

Roadmap step 7. What it records and why is in `docs/governance/` (the data inventory, the DPIA and the rights procedure).
- **The activity log** (`ActivityEntry`, written through `IActivityLog` in `MedApp.Application/Activity/Common`) is separate from the procedure audit. It records who read or changed patient data, sign-ins (and refused ones, with the account's ID when the username exists, and the two-factor step: see Auth), sign-outs, account and role changes, and the security events of email, password reset and two-factor (`ISecurityEventLog`), with the address the request came from.
  - **Reads:** a handler that returns a patient's data calls `RecordViewsAsync`. `PatientViewed` for the patient's page, for their timeline and its overview, and for a FHIR read; `PatientListed` for each patient in a list's results (patient search, procedure search, retention review, a FHIR search); `ProcedureViewed` for a procedure's page. A repeat of the same view by the same person within `Activity:ViewWindowMinutes` (5) adds nothing, so the log stays a readable size.
  - **What records nothing, on purpose:** the side nav's pin labels (`GET api/patients/labels`, `GET api/procedures/patient-procedures/labels`: names and titles of records the user already chose, at most 20); the admin-only logs themselves (procedure audit, activity log); insights (counts only); a user's own notifications. The deprecated `GET api/patients` records one `PatientsSearched` entry ("all"), not one per patient, so it appears in no patient's access report.
  - **`EndpointCoverageTests`** (integration tests) lists every GET endpoint with what it records, and fails on any it doesn't know: classify a new one there.
  - **Lookups outside MedApp:** a demographics (PDS) lookup records `DemographicsLookedUp`, with whether anyone was found, never the NHS number.
  - **Changes to a patient:** build the entry with `activityLog.Entry(...)` and pass it to the repository method (`AddAsync`, `UpdateAsync`, `DeleteAsync`, `IPatientRecordRepository.SaveAsync`), which saves both in one `SaveChangesAsync`, so no change can miss the log.
  - **Details never hold a patient's details or clinical text:** the names of the fields changed, never their values; the names of the search filters used, never the terms (an NHS number is a term like any other: `nhsNumber`, never the number). Reasons an admin types for a restriction, dispute or keep are the only free text, and anonymising replaces them; anonymising's own reason is a fixed choice, with an optional note that must hold no patient details.
  - **Who:** the actor (user and address) comes from `IActivityContext` (`HttpActivityContext` in the API, `DevToolActivityContext` in `medapp-dev`), not from the command. Procedure commands still take a `ProcedureActor`.
  - **It fails closed:** a read or sign-in whose entry can't be written fails (500), rather than going unrecorded.
  - Action numbers are stored, so never renumber `ActivityAction`. The log has no foreign keys, like the audit log.
  - `GET api/activity/search` (admins) searches it; a patient's page shows its latest entries about them as the access report.
- **Restricted and disputed records** (Articles 18 and 16): admins set them with a reason (`PUT api/patients/{id}/restriction` and `.../dispute`).
  - A restricted or anonymised record can't be changed, nor can its procedures: `PatientRules.EnsureCanChange` (409), which `GetForUpdateOrThrowAsync` applies to every procedure change.
  - **So that care can be paused safely,** a restricted record's procedures can still be blocked or cancelled with a reason: `ChangePatientProcedureStatusHandler` loads them through `GetForPauseOrThrowAsync` for those two statuses (`PatientRules.PausesWhileRestricted`). Restricting a record with procedures under way needs `ConfirmOpenProcedures` (a 400 without it); the dialog shows how many. Hazard H-22.
  - `GetForUpdateAsync` is called only by those two guards; a convention test checks it, and that every procedure command uses one.
  - A procedure's response says why it's read-only in `ReadOnlyReason` (with every `Can...` flag false, and only Blocked and Cancelled allowed while restricted), and carries the patient's flags (`PatientFlags`) for the banners on the procedure, edit and patient-procedures pages. Disputed is a banner only.
- **Patients have a row version** (`Patient.RowVersion`, a concurrency token): a save made from a copy read before someone else's save is refused with a 409 (`SaveChangesOrConflictAsync`, `PatientRules.ChangedMeanwhileMessage`) instead of writing old values back, so a stale edit can't undo a restriction or an anonymisation.
- **Everything about a patient** is loaded by `IPatientRecordRepository` (`PatientRecordRepository`) for the subject access export (`GET api/patients/{id}/export`, admins, `no-store`, itself logged) and for anonymisation. **A new table that holds patient data must be added there,** so both cover it. The export has its own types (`Exported…Response`): staff appear by username only, never with their account IDs, network addresses or notification read times, and the JSON holds exactly what the printable page shows.
- **Anonymisation** (`POST api/patients/{id}/anonymise`, admins, `PatientAnonymiser`) replaces the identifying details and free text with placeholders in place, across every table, and keeps the IDs and structure. **The result is pseudonymised, not anonymous:** the patient ID, the year of birth, dates, templates and who did what remain, so it's still personal data. It needs the patient's full name typed as confirmation and a reason from `AnonymisationReason` (with an optional note), and it's refused while a procedure is under way or the record is restricted. Deleting a patient is unchanged.
- **Retention** (`RetentionOptions`, the `Retention` section of `appsettings.json`; `RetentionSchedule`): `GET api/patients/retention-review` lists the records past their minimum period; an admin reviews, keeps (`POST api/patients/{id}/retention/keep`, with a reason and a review date up to two years ahead) or anonymises. **Nothing is ever deleted automatically.**
  - Only the adult and children periods exist yet: the longer specialty classes (mental health, maternity, oncology) don't, so customers using those set `Retention:AdultYears` to their longest class, and the page and the anonymise dialog warn.
  - The conclusion of treatment counts the procedure audit too, so a deleted procedure still counts; a 29 February birthday falls on 1 March in other years (`RetentionSchedule.AddYears`).
- **Sign-ins and the security baseline:** a sign-in throttled by `LoginThrottling` (429) is refused before the handler runs, so it's not in the activity log; the throttling logs it at Warning instead. A name no account could have (`UsernameRules.CouldExist`) is refused as unknown and logged as a refused sign-in with no account. Signing a user out everywhere is logged (`SignedOutEverywhere`), and a password change or reset is logged inside the same transaction as the change and the end of the sessions.
- **Not built yet:** purging sign-in events and other operational data, which needs a scheduled job (`docs/todo.md`).

### Insights

Admins' overview of the work (`/admin/insights`, `GET api/procedures/insights`): headline counts against the same point last week, overdue work by assignee and by priority, procedures created and completed each week, and the average time at each stage of each template.
- **Aggregated in SQL:** the LINQ is in `ProcedureInsightsQueries` (`Application/Procedures/Insights/Common`), each query ending in a `GroupBy` or `Count`; `ProcedureInsightsRepository` runs it, and the unit tests run the same LINQ in memory. `ProcedureInsightsSqlTests` checks that each query translates to SQL Server's SQL (`ToQueryString`, no database).
- **SQL-only functions** go in `InsightsFunctions`, with a C# body for the tests, mapped to SQL in `InsightsDbFunctions` (`SecondsBetween` is `DATEDIFF_BIG`, `JsonValue` is `JSON_VALUE`). They're not in the model snapshot, so they need no migration.
- **Last week's state is rebuilt backwards from now,** from the audit entries since the moment: a procedure's status then is the `"from"` of its first `StatusChanged` entry after it (`{"from":"…","to":"…"}`, as `ProcedureAudit` writes it), or its status now if nothing changed since; its due date then is the `"from"` of its first due date change after it (`DetailsUpdated`, `changes.dueDate`), or its due date now. So keep those details formats. Only recent history is read, and procedures from before the audit log need none. Completions are `StatusChanged` entries to Completed (each time, so one completed twice counts twice), and creations are `Created` entries.
- **Keep SQL Server on the index:** a correlated `TOP(1)` over a procedure's entries seeks the `(PatientProcedureId, AtUtc)` index only while its `ORDER BY` is plain columns; sorting on the details text made it scan the whole table for every procedure. Time a change to these queries on a large synthetic database (the review of #66 has the numbers).
- **Deleted procedures:** with every template, the counts from the audit log (created, completed) include procedures deleted since; with one template, they're matched to existing procedures, so deleted ones drop out, and the templates' weeks needn't add up to the total. Last week's statuses only count procedures that still exist.
- **Drill-down links** go to the procedures list with `mine=false` (so a start-on-mine setting can't narrow them) and the template by its ID (`templateId`). "Completed this week" has no link: no list shows what was completed when.
- **Weeks are UK weeks,** Monday to Sunday (`InsightsPeriod`), and "last week" is the same point in last week as now is in this one. Hourly counts from SQL are summed into weeks in C#, since the UK's offset changes are on the hour.
- **Stage times** run from a stage being reached (the stage before it first finished, or the procedure created) to it being started (waiting), then to its first finish (working). The first finish is from the audit log, because unlocking and finishing a stage again moves its own `CompletedAtUtc`.

### NHS number, FHIR and SNOMED CT

Roadmap step 9's first part. What's built and what's left is in `docs/architecture.md` (step 9) and `docs/todo.md`.
- **The NHS number** (`Patient.NhsNumber`) is optional, stored as its 10 digits with no spaces (`varchar(10)`), and unique when present (the filtered unique index `IX_Patients_NhsNumber`). It's unique across the deployment until step 8 makes it unique within an organisation.
  - `NhsNumbers` (`MedApp.Domain/Patients`) removes spaces, checks the Modulus 11 check digit, spots placeholders that pass it (`IsPlaceholder`: ten of one digit, `0123456789`, `9876543210`) and formats it; `NhsNumberRules` (`MedApp.Application/Patients/Common`) is the validation rule for every command that takes one, and refuses placeholders with their own message. The web form checks the same (`src/utils/nhsNumber.ts`). Accept it with or without spaces; show it as `XXX XXX XXXX`.
  - Another patient with the number is a 409 (`ConflictException`), also when two saves race: the repository turns the unique index's violation into the same 409. SQL Server's message for that violation holds the number, so EF Core's failed-save and failed-command events are logged at Debug (`LogFailedSavesAtDebug` in `AddDatabase`), which isn't written; keep it that way.
  - On update, null leaves it unchanged and empty removes it.
  - `GET api/patients/search` filters by it (`NhsNumber`: numbers containing the digits typed, spaces ignored).
  - The export includes it, and anonymisation removes it (`PatientAnonymiser`), though not from saved searches yet (`docs/todo.md`). **Always Encrypted for it isn't done** (`docs/todo.md`).
  - **Keep it out of new URLs** (a path or a query string): request logs and traces record URLs. Send it in a body, as the demographics lookup does. The FHIR search's query string still carries it, as FHIR defines it, and so does the patients list's filter, which stays in the URL by the product owner's decision on list filters (`docs/todo.md`).
- **The FHIR API** (`FhirController` at `api/fhir`; `MedApp.API/Fhir`, `MedApp.Application/Fhir`, DTOs in `MedApp.Contracts/Fhir`) is read-only FHIR R4 in the UK Core profiles' shape, patients only: `GET api/fhir/metadata` (a minimal CapabilityStatement, open to anyone), `GET api/fhir/Patient/{id}`, and `GET api/fhir/Patient?identifier=https://fhir.nhs.uk/Id/nhs-number|{number}` (a searchset Bundle). A value with no system matches as an NHS number only when it is one, and otherwise matches nothing, as does another system; no identifier, or an invalid number with the NHS number's system, is a 400.
  - **Flagged records:** an anonymised patient is sent as `active: false`, an `anonymous` name, the year of birth alone and no email, never its placeholders as details; a restricted or disputed one carries a `meta.tag` from MedApp's record-flag code system (`FhirPatientMapper`).
  - **Hand-built DTOs, not the Firely SDK.** Its licence (BSD-3-Clause) was fine; its size and dependencies weren't worth it for three resource shapes. Revisit when the API needs more resources (CarePlan, Task) or validation against the profiles.
  - **Return resources through `FhirResult`** (`FhirJson`): `application/fhir+json`, `Cache-Control: no-store`, and no nulls or empty arrays, which FHIR JSON never holds (leave a list null rather than empty).
  - **JSON only:** `FhirFormatFilter` answers a `_format` or `Accept` that doesn't allow JSON with a 406.
  - **Errors are OperationOutcomes, never ProblemDetails:** `FhirExceptionFilter` makes 400 `invalid` and 404 `not-found`, `FhirAuthorizationResultHandler` 401 `login` and 403 `forbidden`, `FhirFormatFilter` 406 `not-supported`, and an unknown path under `/api/fhir` is a 404 `not-supported` (`FhirJson.NotFoundAsync`, mapped before the generic `/api` 404). The forced password change's 403 stays the API's ProblemDetails.
  - **Who:** the Staff policy, with the session cookie; no OAuth clients for other systems yet.
  - **Logged as the pages are:** a read records `PatientViewed`; a search records `PatientsSearched` (filters `["nhsNumber"]`, or `["identifier"]` when it couldn't match an NHS number; the count; `via: "fhir"`) and `PatientListed` for each patient found.
  - The NHS number's verification status is always `02` ("Number present but not traced"), because MedApp doesn't trace numbers with PDS.
- **SNOMED CT codes on stage definitions** (`SnomedCode` and `SnomedTerm`, both or neither): `SnomedCt` (`MedApp.Domain/Terminology`) checks the format only (a concept's SCTID: 6 to 18 digits, no leading zero, partition `00` or `10`, a Verhoeff check digit), through `SnomedRules`. There's no terminology server, so nothing checks that the concept exists or is active, or that the admin's term matches it. They're copied onto a procedure's stages when it's created, like the name, description and instructions.
- **The demographics lookup (PDS)** is `IPersonalDemographicsService`, chosen by `Pds:Mode`: `Off` by default (`NoPersonalDemographicsService`), or `Offline` (`OfflinePersonalDemographicsService`, four synthetic 999 people). `GET api/demographics/status` says whether there's one; `POST api/demographics/lookup` (`{ nhsNumber }` in the body, never the URL) looks someone up (staff, `no-store`; 400 for an invalid number, 404 when nobody has it or there's no lookup). **The real PDS FHIR API isn't wired in:** it needs NHS England's onboarding (`docs/todo.md`).
- **Medical device decision:** all four only identify, label or move data without change, so the decision is unchanged (`docs/clinical-safety/intended-purpose-and-device-decision.md`). Anything that interprets codes or chooses them from clinical data needs a new assessment.

### Migrations

- Migrations live in `MedApp.Infrastructure/Migrations/` with `MedAppDbContextModelSnapshot.cs`. They are a single linear history starting at `InitialCreate`. Always add a new migration on top, and never edit or delete one that has been applied to a database.
- Name migrations in the same past-tense style as commits, for example `RemovedTemplateAuditColumns` or `RenamedTasksToProcedures`.
- **Always read the scaffolded migration before keeping it.** EF can't detect renames of entities or of properties used as keys, so it scaffolds a rename as `DropTable` + `CreateTable`, which deletes the data. When `migrations add` warns that "An operation was scaffolded that may result in the loss of data", rewrite `Up` and `Down` by hand. Drop the keys and foreign keys whose names change, then call `RenameTable`, `RenameColumn` and `RenameIndex`, then re-add the keys and foreign keys with the same `onDelete` behaviour. `RenamedTasksToProcedures` is the reference example.
- After editing a migration, `has-pending-model-changes` must report no changes. Otherwise the snapshot and the model disagree.
- Test a non-trivial migration up **and** down against a throwaway LocalDB database, not the dev `MedApp` database. Pass `--connection "Server=(localdb)\MSSQLLocalDB;Database=MedApp_MigrationTest;Trusted_Connection=True;TrustServerCertificate=True"` to `dotnet ef database update`, seed synthetic rows, apply the migration, roll back with `database update <PreviousMigration>`, then drop the database.
- Applying a migration to someone's local `MedApp` database changes their data, so do it only when asked, and take a `medapp-dev` `save_snapshot` first.

### Auth

- On login, a `UserSession` row is created and its ID goes into an HttpOnly cookie. `SessionAuthenticationHandler` checks the cookie on every request and builds claims, including roles and the session's own ID (`SessionIdClaim`, read with `User.GetSessionId()`).
- **Sessions last 8 hours** from sign-in (`UserSession.Lifetime`), and can be extended to 24 hours at most (`MaxLifetime`; over 20 hours, so WCAG 2.2.1 allows the hard limit). Using the app doesn't extend one; only asking does:
  - `GET api/auth/session` returns the signed-in session's `SessionResponse`: `expiresAtUtc`, `expiresInSeconds` (by the server's clock, so a device with a wrong clock still counts down right) and `canExtend`. Reading it never extends it.
  - `POST api/auth/session/extend` ("Stay signed in") moves the expiry to 8 hours from now, but never past 24 hours from sign-in and never earlier than it was, and sends the cookie again with the new expiry (the cookie expires with the session).
  - Both take the session from the cookie, are open to anyone signed in (`[Authorize]`, no role needed), are sent `no-store`, and are a 401 once the session has ended. `SessionTimeout` in `MedApp.Web` uses them for the warning before a session times out.
  - **The forced password change page has no timeout warning.** `/change-password` is outside the signed-in layout, and `PasswordChangeRequired` doesn't allow the session endpoints, deliberately: that page ends at the session's normal expiry, without a warning, and its change then fails, so its user signs in again.
  - A session isn't ended by a lockout yet (risk S5). Password changes and resets end sessions (below).
- The UI is always on the API's own origin (served from `wwwroot` in production, proxied by Vite in development). So there is no CORS, and the cookie is `SameSite=Lax`.
- **The cookie's name:** outside Development it's `__Host-medapp.session`: `Secure`, `Path=/` and no `Domain`, which browsers require of a `__Host-` cookie, so only this host over https can set it. In Development it stays `medapp.session`, not `Secure` (the Vite dev server is plain http). Only `ISessionCookieService` knows the name (`CookieName`, `TryGetSessionCookie`); read the cookie through it.
- **Sessions end when the password changes** (the handlers call `ISessionService.RevokeUserSessionsAsync`, through `UserSessions.EndSessionsAsync`, inside `IIdentityTransactions.RunAsync` with the change, so both are kept or neither is):
  - changing your own password ends your other sessions and keeps the one you changed it from;
  - an admin's password reset ends all of the user's sessions;
  - **a role change doesn't end sessions:** roles are read on every request, so it applies to the user's next request, and they stay signed in;
  - an admin can sign a user out everywhere: `POST api/auth/users/{username}/sign-out-everywhere` (204, or 404 for an unknown user), and the Users page's "Sign out everywhere" action, with a confirmation.
  - A new account action that changes how someone signs in should end their sessions the same way, in the same transaction. An open live-notifications connection isn't closed when its session ends (`docs/todo.md`).
- **Sign-in throttling** (`MedApp.API/Common/RateLimiting`), in front of Identity's lockout, which stays: `POST api/auth/login` allows 30 attempts a minute per client address (IPv6 counted by its /64) and 5 a minute per username from anywhere, right or wrong. Over either, it's a 429 `ProblemDetails` ("Too many sign-in attempts. Try again in N seconds.") with `Retry-After`, before the password is checked, and the login page shows that title. Behind Traefik, the address comes from the forwarded headers. The counts are in each API copy's memory, so two copies allow twice as many. Settings are in the `LoginThrottling` section (`LoginThrottlingOptions`).
  - **What's counted:** only a name that could be an account's (`UsernameRules.CouldExist`): at most 256 characters, of Identity's `User.AllowedUserNameCharacters` (trailing spaces ignored, as SQL Server ignores them). Any other name is unknown, the usual 401, without a lookup (`IdentityAuthenticationService`), because SQL Server's collation would match some (a byte order mark in front, a NUL after, full-width letters) to a real account. A name over 256 characters is a 400 (`LoginValidator`), and a body over 16 KB a 413 (`[RequestSizeLimit]`). The per-username key is trimmed, NFKC-normalised and upper-cased (`LoginUsernameLimiter.Key`).
  - **Each refusal is logged** at Warning, with the limit and the client's key (its address, or IPv6 /64), never the username (`LoginThrottling.LogRefusal`).
  - **Sizing:** 30 a minute per address suits a site whose staff reach MedApp from their own addresses. A hospital behind one NAT or proxy address (an HSCN gateway) shares that allowance between everyone, and at a shift change a few hundred sign-ins arrive in minutes. If staff report "Too many sign-in attempts", or the refusal warnings show one address, raise `LoginThrottling:PerClientPermitLimit` for that deployment (for example to the number of staff who start a shift together, per copy of the API); keep the per-username limit at the lockout count. The integration tests sign in far more often than people do, so `MedAppApiFactory` sets both limits out of reach, and `LoginThrottlingTests` puts the real ones back on an API of its own. The E2E suite signs in as its admin once a run, well under the limits, so CI's `e2e` job needs no setting. The per-address limit is the `login` rate limiting policy; the per-username one is the `[ThrottleLoginByUsername]` action filter, because the username is in the body.
- **Common passwords are refused** when a password is set (creating an account, changing or resetting a password), never at sign-in: `CommonPasswordValidator` (`MedApp.Infrastructure/Common/Identity`) holds the 10,000 most used passwords from the NCSC's list, embedded from `Passwords/common-passwords.txt`, compared ignoring case. Its source and licence are in `Passwords/NOTICE.txt`. Generated passwords never match it (a test makes 500).
- **Security headers** (`MedApp.API/Middleware/SecurityHeaders.cs`, first in the pipeline) go on every response: `nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`, `X-Frame-Options: DENY` and a `Permissions-Policy` that turns off device features. Every `/api` response is `Cache-Control: no-store`, with `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'`.
  - **Pages, outside Development, get a Content Security Policy with a new nonce for each response:** scripts only from the app's origin or with that nonce, styles from the origin and inline (the server writes style attributes), `connect-src 'self'` (which covers the notifications WebSocket), `object-src 'none'`, `base-uri 'none'`, `form-action 'self'`, `frame-ancestors 'none'`. `SsrPages` sends the nonce to the page server in `X-MedApp-Csp-Nonce`; `server.mjs` passes it (if it's base64) into the request's context; `createSsrRouter` gives it to the router (`ssr.nonce`), which puts it on every script it writes and in a `csp-nonce` meta tag for the browser's router. The client-rendered fallback (`index.html`) has no inline scripts, so `'self'` covers it.
  - **Never add an inline script or `eval`** to the app: the policy refuses them. A new external origin (fonts, images, an API) needs the policy changed, deliberately.
  - In Development the API sends only `frame-ancestors 'none'` to non-API paths: pages come from Vite there, and Swagger UI needs inline scripts. `SecurityHeaders:PageCspReportOnly=true` sends the page policy as report-only, an emergency switch that needs no new build.
- The threat model is `docs/security/threat-model.md`, and the ASVS 5.0 Level 2 checklist is `docs/security/asvs.md`. A change to authentication, sessions, headers or a data flow should update both.
- The roles are `Admin` and `User`. Staff (either role) use `[Authorize(Policy = AuthPolicies.Staff)]`: patients (with their timeline, which shows staff less than admins: see [Patient timeline](#patient-timeline)), the FHIR API (its CapabilityStatement is open to anyone) and the demographics lookup, and creating and working on procedures. Admin-only endpoints use `[Authorize(Roles = "Admin")]`: users and roles, stage definitions and templates (staff can read them), assigning staff, unlocking stages, deleting procedures, the procedure audit log, Insights (`GET api/procedures/insights`), the activity log, and a patient's export, restriction, dispute, anonymisation and retention review. There may be several admins; never assume one. The `SelfUserOnly` policy limits a user to their own `{username}` route.
- Procedure handlers get the signed-in user as a `ProcedureActor`, from `User.ToProcedureActor()` (`MedApp.API/Common/Authentication/ClaimsPrincipalExtensions.cs`).
- The app seeds no users; its only `HasData` seed is a default stage definition. **Locally, sign in as `admin` / `admin`.** `medapp-dev` creates that test login (`ensure_test_admin`, and on every `reset_demo_data`), bypassing the password rules. It exists only in local dev databases.
- **A deployed database gets its first admin deliberately:** `dotnet MedApp.API.dll create-first-admin` (`FirstAdminSetup`) makes the Admin and User roles and an admin with both (User lets them change their own password) from the `FirstAdmin:Username` and `FirstAdmin:Password` settings, but only while there are no accounts at all, then exits. The Helm chart's migrations Job runs it after migrating, with the credentials from a Secret.
- **The server generates every new account's first password;** there is no default password. `POST api/auth/users` takes only a username, creates the account with a generated first password that meets the `Identity` rules (`IIdentityUserService.CreateUserAsync`, the same generator as a reset), and returns it once in `CreateUserResponse`. The password is shown once, and its user must change it before doing anything else (below). The Users page shows it to the admin once (`OneTimePassword`, with a copy button), to pass on securely, and forgets it when the dialog closes.
  - **A generated password must be changed at first sign-in.** `ApplicationUser.MustChangePassword` is set by `IdentityUserService` when it creates an account or resets a password, in the same save as the password, and cleared when `UpdateUserPasswordAsync` succeeds, in the same update. A new password must differ from the current one (`UpdateUserValidator`).
  - **How it's enforced:** `SessionService` reads the flag on every request, and `SessionAuthenticationHandler` turns it into a claim (`SessionAuthenticationDefaults.MustChangePasswordClaim`). `whoami` reports it as `mustChangePassword`. While it's set, `PasswordChangeRequired` (`MedApp.API/Common/Authentication`, between authentication and authorization) refuses every `/api` request, the notifications hub included, with a 403 `ProblemDetails` (title "Password change required", `code: "password_change_required"`). It allows only what every `AccountHold` allows (`GET api/auth/whoami`, `GET api/auth/me/settings`, `GET api/auth/features`, `POST api/auth/login` and `login/two-factor` (signing in again checks afresh and replaces the session), `POST api/auth/logout`, and the anonymous `password/forgot`, `password/reset` and `email/confirm`), and `PUT api/auth/users/{username}/update-password` for their own username (compared as `SelfUserOnly` compares it, `AuthPolicies.IsOwnUsername`). Pages and static files pass: the web app's `_app` guard sends them to `/change-password`, which asks for the given password and a new one, then opens their start page. A page already open when the flag is set finds out from its next API call: the API client sends any 403 with that `code` to `/change-password` with a full page load (in the browser only).
  - **Exempt:** the first admin (`FirstAdminSetup`), whose password comes from the secret store, set by whoever deploys (on kind, `up.sh` generates it, and it can be read back from the cluster's secret store, which is acceptable for a local cluster); and every account `medapp-dev` makes or sets a password on (`create_user`, `set_password`, `ensure_test_admin`, and `reset_demo_data`'s staff), so they can sign in and work at once.
  - **`CreateUserResult`** carries a password exactly when the account was created: make it with `Created(password)` or `Failed(result)`. It and the password responses never print the password in `ToString()`, and the API sends them with `Cache-Control: no-store`.
  - **Accounts created before #53** (from the Users page or the Blazor client) were given the published password `DefaultPassword1`, and may still have it. An admin should reset those accounts' passwords, or ask their users to change them. Finding them is a to-do (`docs/todo.md`).
- Identity password and lockout rules live under the `Identity` section of `appsettings.json`.

### Email, password reset and two-factor authentication

Built on `feature/email-2fa`. Everything here is optional: an account without an email address or two-factor signs in exactly as before.
- **Sending email:** handlers call `IEmailSender.SendAsync(EmailTemplates.…)` (`MedApp.Application/Common/Email`) after their own change is saved. It's best effort: `EmailQueue` (Infrastructure) queues the message in memory and returns at once, and `EmailDispatcher` sends it over SMTP with MailKit in the background, logging failures (never the address). Nothing is retried, and a message still queued when the API stops is lost; every email today can be asked for again.
  - **Settings** (`Email` section): `LinkBaseUrl` (where links point: configured, never from the Host header), `FromAddress`, `FromName`, and `Smtp:{Host, Port, Security, Username, Password, TimeoutSeconds}`. Without a host and a from address nothing is sent; without `LinkBaseUrl` no email with a link is sent. `EmailAvailability.CanSendLinks` says whether both are set, and `GET api/auth/features` tells the web app, which hides "Forgot your password?" and email fields when it's false.
  - **Development:** the AppHost runs **Mailpit** (`axllent/mailpit`), which catches every email: open its **Inbox** link on the dashboard. kind runs it too (`mailpit.enabled`; read it with the port-forward in `deploy/kind/README.md`). Azure sends through Azure Communication Services' SMTP relay (`deploy/azure/README.md`, Email).
  - **Templates** (`EmailTemplates`): plain, accessible HTML (a language, a title, real links, no images) and the same words as text, every value HTML-encoded. **Never put clinical text in an email** (no patient, procedure or notification text: a notification's title is a procedure's title) **and never a password or code.** A new account's welcome is only the link to confirm its address.
  - **Security notices** (`ISecurityNotices`): a short email to the account's confirmed address when its password is reset by link, two-factor is turned on or off or reset by an admin, recovery codes are used or replaced, or its address changes (sent to the old one) or is removed.
- **Staff email addresses** are Identity's `Email` and `EmailConfirmed`, plus `ApplicationUser.PendingEmail`: a new address waits there until the link sent to it is opened, and the old one stays in use until then. Only a confirmed address is ever used or returned as the account's (`UserResponse.Email`; the pending one is `PendingEmail`). Several accounts may share an address. An admin can give one when creating an account (`CreateUserRequest.Email`); people set, change or remove their own in Account Settings (`GET`/`PUT`/`DELETE api/auth/me/email`); the link opens `/confirm-email`, which posts to `POST api/auth/email/confirm` (anonymous: the link proves the address). The token is Identity's change-email token, bound to the address and the security stamp, valid 24 hours.
- **Emailed links carry their token in the fragment** (`/reset-password#user=…&token=…`, `EmailLinks`), which browsers never send to a server, so tokens stay out of access logs and the Referer header; the page reads it after mounting.
- **Forgot your password?** `POST api/auth/password/forgot` with a username or an address always answers 202, whatever matched, and emails a reset link to the account with that username and to every account whose confirmed address it is (none without a confirmed address). It's throttled per client address (the `login` policy) and per username or address (`ThrottleResetRequestsAttribute`: 3 in 15 minutes, `LoginThrottling:ResetRequests…`). `POST api/auth/password/reset` takes the link's user and token and the new password: Identity's reset token from `PasswordResetTokenProvider` (30 minutes; single-use, because a reset changes the security stamp). A bad, used or expired link, or an unknown user, is the same 400 `InvalidToken`; the new password must meet the rules, avoid the common ones and differ from the current one (`SamePassword`). On success, in one transaction with the new password, every session ends; the must-change-password flag clears, any lockout ends, and the owner gets a notice. Two-factor stays on.
- **Data protection keys** (which seal every Identity token) are in the database (`DataProtectionKeys`, `PersistKeysToDbContext`), so every API copy shares them and a link made by one opens on another; on Azure `DataProtection:KeyVaultKeyUri` wraps them with a Key Vault key (the API's workload identity). The application name is fixed (`MedApp`). Integration tests keep the table across resets.
- **Two-factor authentication** (TOTP, an authenticator app) uses Identity's authenticator support for the key, the recovery codes and `TwoFactorEnabled`, but checks codes itself (`Totp`, `TwoFactorService`): against `TimeProvider`, one 30-second step either side, and each code once (the last step accepted is kept as a user token, `[MedApp]`/`AuthenticatorLastTimeStep`).
  - **Setting up** (Account Settings, Security): `POST api/auth/me/two-factor/authenticator` gives a new key, as text and as a QR code drawn on the server (`QrCodeRenderer`, Net.Codecrete.QrCodeGenerator, MIT) and sent as one SVG path, so the web app needs no QR library and the CSP needs no change; `POST …/enable` with a code turns it on, returns ten recovery codes once, and ends the account's other sessions. `POST …/recovery-codes` (with a code) replaces them; `POST …/disable` needs the password and a code (or recovery code). Wrong codes there count towards the lockout. Responses with keys or codes are `no-store`, and those records never print them.
  - **Signing in:** for an account with two-factor, `POST api/auth/login` answers **202** with a `TwoFactorChallengeResponse` and no session or cookie. The challenge (`SignInChallenge`) is 256 random bits, stored only as a SHA-256 hash, bound to the user, valid 5 minutes, allowing 5 wrong codes, and deleted when used (so only one request can finish a sign-in). `POST api/auth/login/two-factor` with the challenge and a code or a recovery code makes the session as sign-in always has. A wrong code is a 401 and counts towards Identity's lockout; with two-factor on, a right password doesn't clear the count (only a complete sign-in does), so codes can't be guessed by re-entering the password. An unusable challenge is a 401 with `code: "sign_in_expired"` (start again). A recovery code used to sign in emails the owner. The forced password change comes after the code.
  - **Admins:** `POST api/auth/users/{username}/reset-two-factor` turns someone's two-factor off and forgets their key and codes, ending every session they have (not your own: 400 `OwnTwoFactor`). The admin setting **Require two-factor authentication for admin accounts** (`/admin/settings`, `GET`/`PUT api/auth/security-settings`, one `SecuritySettings` row) can only be turned on by an admin who uses it. While it's on, an admin without two-factor is held from their next request by **`TwoFactorSetupRequired`**, an `AccountHold` beside `PasswordChangeRequired` (which comes first): a 403 with `code: "two_factor_setup_required"` for everything but setting it up (and what every hold allows), and the web app sends them to `/set-up-two-factor`; `whoami` reports `mustSetUpTwoFactor`. They can't turn it off while it's required. `SessionService` reads the setting only for an admin without two-factor.
  - **A new hold** derives from `AccountHold` (its allowlist, title and `code`), is added to the pipeline after `PasswordChangeRequired`, and gets a guard in the web app's `_app` route and `src/api/client.ts`.
- **Security events** (two-factor changes, resets and their requests, email address changes, recovery codes used, the admin requirement) go through `ISecurityEventLog` (`MedApp.Application/Common/Security`). `ActivitySecurityEventLog` writes each to the activity log (`ActivityAction` 28 to 39; `ActionFor` maps them) and to the application log (`LoggingSecurityEventLog`). An event about someone's own account is by them; one an admin made (a two-factor reset) is by the admin, with the account as its target; a reset request that matched nothing is by no account. Never put an email address, password, token or code in an event.
- **Signing in with two-factor in the activity log:** a right password records `TwoFactorChallenged` (no session yet); the second step records `SignedIn` (with `secondStep`: `authenticator` or `recoveryCode`) or `SignInFailed` (with `reason`: `InvalidCode`, `InvalidRecoveryCode`, `LockedOut`, `ChallengeExpired`, or a wrong code that locked the account), by the account the challenge was for. Never the code.

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
- reset everything to the standard synthetic demo data set (`reset_demo_data`, which gives most patients an NHS number from the 999 test range)

It only runs against a local database: by default the AppHost's SQL container, using the password from the AppHost's user secrets, so the AppHost must have run once. It protects `admin`, and destructive tools need `confirm: true`.
- **Before a destructive experiment:** take a `save_snapshot`.
- **Build it first:** `dotnet build tools/MedApp.DevMcp`, because `.mcp.json` runs it with `--no-build`.
- **What the app's layers need from the host,** the tool registers itself in `DevMcpServices` (an activity log actor, `medapp-dev`, and no live notifications). `DevMcpServicesTests` builds them with validation on, so a new service the layers need fails there, not at the tool's start.
- **Docs:** see `tools/MedApp.DevMcp/README.md`.

## Local dev gotchas

- **Docker Desktop must be running.** The dev database is SQL Server 2022 in a container started by the AppHost, with its data in the `medapp-sql-data` volume, on host port `14330` (bound to `127.0.0.1`).
  - The container is **persistent**: it keeps running after the AppHost stops, so `dotnet ef` and `medapp-dev` can still reach it. `docker stop` it to free memory; the next AppHost run starts it again.
  - Aspire generated the `sa` password on the first run and keeps it in the AppHost's user secrets (`Parameters:sql-password`). The database in the volume only accepts that `sa` password, so don't delete or change it.
- The API's `appsettings*.json` have **no connection string**: the AppHost injects `ConnectionStrings__DefaultConnection`. Running the API on its own needs that variable set.
- **An empty database** (a new volume) isn't migrated at startup. Apply the migrations with the command under [Commands](#commands), then load the demo data with `medapp-dev`'s `reset_demo_data`.
- The React dev server (`npm run dev`: Node with Vite, rendering pages on the server) proxies `/api` and `/swagger` to the API, and its own server-side API calls go through that proxy too. If `/api` calls start returning the app's HTML instead of JSON, the proxy has dropped out; restart the AppHost.
- **Server-side rendering in production** (`MedApp.API/ServerRendering`): the page server (`MedApp.Web/server/server.mjs`) is in the image at `/app/ssr/`, with its bundled entry and a copy of the client build's manifest, outside `wwwroot`, so no static file path reaches it. It runs one of two ways:
  - **In Kubernetes, in a container of its own** (the chart's `pages` container, `api.pageServer`): the same image with `node /app/ssr/server.mjs`, in the API's pod, as its own non-root user, read-only, with no capabilities and its own resource limits. It gets none of the API's secrets or environment, only `HOST=127.0.0.1`, `PORT=8082`, `HEALTH_PORT=8083` and `MEDAPP_API_URL=http://127.0.0.1:8080`, so code running in Node can't read the API's connection strings. The API's `Ssr:ServerUrl` (`http://127.0.0.1:8082`) points at it, and the API doesn't start Node itself. It runs as a service of its own: its first process only supervises, running the page server in a worker process that it starts again (after 1 s, doubling up to 30 s while it keeps failing soon after starting) whenever it exits, is killed for running out of memory, or misses its heartbeat for 15 s (a stuck event loop). So the container keeps running, and the API's pod stays in service, while pages fall back for a moment. It has only a liveness probe (`GET /alive` on 8083, answered by the supervisor, which the network policy keeps from everything but the kubelet), and deliberately no readiness or startup probe: either would take the API's pod out of service whenever the page server is. Its own log is `kubectl logs deploy/medapp-api -c pages` (the pod's default container is `api`). It has no renderer key (below), and needs none: the API never redirects requests it received on a loopback address.
  - **Anywhere else** (`docker run`, CI's `e2e` job), with no `Ssr:ServerUrl`: the API starts it with `node` on a free local port, as `Ssr:ScriptPath` (`ssr/server.mjs`, relative to the content root, `/app`). It's never restarted if it stops. Node gets only the environment variables it needs (an allowlist in `SsrServer.StartInfo`), so the API's secrets aren't in Node's environment and can't leak from it by accident, e.g. into logs. That isn't isolation: Node runs as the same user in the same container, so code running in it could still read the API's environment from `/proc`. Its own API calls also carry a key the API gave it (`SsrServer.IsFromRenderer`).
  - **Either way,** the API forwards page requests to it (only the path and query: the host is always Node's), with the CSP nonce in `X-MedApp-Csp-Nonce`, and falls back to `index.html` if Node is missing, not ready or stopped, if a render fails in any way or Node answers with a 5xx, as long as nothing has been sent yet; a page that fails part-way is cut short. Its `ssr` HttpClient has no resilience handler, so a render is never retried. `Ssr:RenderTimeout` covers only getting the page started (its status and headers), and `HttpClient`'s default 100 s timeout applies too; nothing times out a page that's still streaming, so a render that stalls part-way holds the browser's request until the browser gives up. Rendered pages hold the user's data, so they're sent with `Cache-Control: no-store`. Any path with an empty segment (`//`) is refused (`PageServerFiles`), and so, as defence in depth, are `/ssr` and `/.vite`, where the page server and the manifest used to sit in `wwwroot`. The API doesn't apply the https redirect to requests it received on a loopback address, so an https port (`ASPNETCORE_HTTPS_PORT` in `api.env`) can't break the page server's calls for a page's data. Settings are in the `Ssr` section (`SsrOptions`); a `Ssr:ServerUrl` that isn't an http address of a server alone stops startup (the page server serves no https).
- **The production image** is `MedApp.API/Dockerfile`, built from the repo root: `docker build -f MedApp.API/Dockerfile -t medapp-api .`. It builds the React app with Node and the API with the .NET SDK, then puts both on the `aspnet` runtime with Node's binary, running as a non-root user on port 8080 (TLS ends in front of it). The app goes in `/app/wwwroot`, and the page server in `/app/ssr` (`dist/ssr` from the build, with the client manifest; `dist/.vite` is left out). The Helm chart runs the page server from this same image, with its own command. The root `.dockerignore` keeps Windows `node_modules`, build output and local secrets out of the build. If you change which projects the API references, update the Dockerfile's `COPY` lines too.
- Kestrel's `https` profiles need the ASP.NET Core dev certificate. If startup fails with "the default developer certificate could not be found", run `dotnet dev-certs https`.
- Session cookies are `SameSite=Lax` everywhere, and `Secure`, with the `__Host-` name, outside Development (the Vite dev server is plain `http://localhost`). Deploying the rename signs everyone out once: browsers still send the old `medapp.session` cookie, which the API no longer reads.

## Testing conventions

- The stack is NUnit 3, FluentAssertions 8, Moq and AutoFixture with AutoMoq. FluentAssertions prints a license banner when tests run; it's expected and not an error.
- The test tree mirrors the source: `MedApp.UnitTests/Application/<Area>/Commands/<Action>/<Action>HandlerTests.cs` and `...ValidatorTests.cs`. Note that `Authentication` maps to `Auth` and `Sessions` to `Session` in the test tree.
- Fixtures are `[TestFixture] public sealed class`, with setup in `[SetUp]`. Build the system under test with `AutoFixtureFactory.Create()`, then `_fixture.Inject(mock.Object)`, then `_fixture.Create<Handler>()`.
- Test names follow `Method_Scenario_ExpectedResult`, for example `Handle_PatientDoesNotExist_ReturnsNull`.
- Put shared test data in `MedApp.UnitTests/Common/Constants/*TestConstants.cs`, and add new constants there rather than inlining magic values.
- Use synthetic data only. This is a medical app, so never put realistic patient PII in tests, seeds or fixtures.
  - **NHS numbers come only from the 999 range** (`999 xxx xxxx` with a valid check digit), which is never issued to real people. Never a number outside it, even a made-up one, as it could be someone's.
- Procedure handler tests derive from `ProcedureHandlerTestBase` (mocked repository and username lookup, fixed clock), and build procedures with `ProceduresTestConstants.CreateProcedure`.
- Handlers that write the activity log get a `RecordingActivityLog` (`_fixture.Inject<IActivityLog>(...)`), which makes entries as the real one does and keeps what's recorded, so tests can check the entries.

**Integration tests** (`MedApp.IntegrationTests`) test behaviour through HTTP that unit tests can't: real SQL translation, real Identity stores and transactions, the authentication handler and policies, the middleware.
- `TestEnvironment` (a `[SetUpFixture]`) starts SQL Server once per run, applies the migrations and starts the API. Before each test, `IntegrationTest` empties the database with Respawn (keeping the migrations history and the seeded stage definition), then makes the first admin with the deploy's own `FirstAdminSetup`.
- Do everything through the API where you can, with the helpers in `IntegrationTest` (`SignInAsAdminAsync`, `CreateStaffAsync`, `CreatePatientWithProcedureAsync`, ...). Use `RunInScopeAsync` for a service, and `ExecuteSqlAsync` only for what the API can't do, such as making a session expire.
- A test that acts as an account an admin made must first change its generated password, or every other `/api` call is a 403 `password_change_required`. `CreateStaffAsync` does when the account has a role; an account with no role can't change it (that needs the Staff policy), so it keeps its generated password and stays held. To test the Staff policy itself, give the account a role, let it choose its password, then remove the role (`AuthorisationTests.StaffEndpoint_RolesRemoved_*`). `Auth/PasswordChangeRequiredTests` tests the hold itself, with `CreateForcedUserAsync`.
- Constants are in `Common/Constants/IntegrationTestConstants.cs`.
- **Emails and the clock:** the API's `IEmailSender` is a `FakeEmailSender` (`Api.Emails`), which keeps every message, and its `TimeProvider` is a `TestClock` (`Api.Clock`) a test can move forward (a challenge's expiry, the next authenticator code); both reset before each test. `AccountSecurity` has the helpers: confirm an address from its emailed link, turn two-factor on, make the next code as an app would, and sign in in two steps. `SmtpEmailTransportTests` sends through the real transport to Mailpit in Docker.

**End-to-end tests** (`MedApp.E2ETests`) drive the React app in Chromium, against the AppHost locally and the production image in CI. They derive from `E2ETest`.
- Open pages with `OpenAsync`, which waits until React has taken over the server-rendered page; typing or clicking before then is lost.
- Find elements as people do: `GetByRole` and `GetByLabel`, with their accessible names.
- Make the data a journey needs through the API (`Api`), with unique synthetic names (`E2EData.UniqueName`), and register every clean-up with `Data.OnCleanUp`. Rely on nothing you didn't make, except the signed-in account and the default stage definition. The E2E account must be an admin.
- Give accounts a test makes a password from `E2EData.NewPassword()`, new each run, never one written in the source, and keep passwords out of messages.
- Missing `E2E_USERNAME` or `E2E_PASSWORD` fails the suite in CI (`CI=true`) and skips it locally. With `E2E_TRACE_DIR` set (only `main.yml` sets it), a failed test's Playwright trace is saved there.

## CI

`.github/workflows/main.yml` runs on PRs to `main`, with eight jobs:
- `web` checks `MedApp.Web`: `npm ci`, `npm audit --audit-level=high` (a high or critical advisory fails it), lint, type check, a check that the built theme is up to date, Vitest with coverage, and the full production build (`npm run build`: the client, the server rendering bundle and the page server).
- `image` builds the API's image from `MedApp.API/Dockerfile`, as Publish does, without pushing it, so a broken Dockerfile or bundle fails on the PR. It shares the GitHub Actions build cache with Publish. Then Trivy scans it: every finding with a fix goes to code scanning (SARIF), and a critical vulnerability with a fix fails the job. Trivy runs from its own image, pinned by digest (`TRIVY_IMAGE`), never from `aquasecurity/trivy-action`, whose tags were hijacked in March 2026. `web`, `helm`, `unit-tests`, `image`, `integration-tests` and `e2e` are all required checks on `main`.
- `dependency-review` fails a PR that adds or updates a dependency (npm, NuGet, actions) with a known high or critical advisory, and comments on the PR when it does.
- `helm` lints the Helm chart (`deploy/helm/medapp`) in its main configurations (its own SQL Server, kind's values with External Secrets and TLS, an external database, and Azure's values), renders it, and compiles and lints the Azure Bicep (`deploy/azure`). It also renders the cluster services (Traefik, cert-manager, External Secrets and Argo CD) at the versions in `deploy/versions.sh`, with every values file and `--set` value that `deploy/kind/up.sh` and `deploy/azure/bootstrap.sh` give them. After changing one of those values files, run the same `helm template` locally.
- `unit-tests` builds the dev MCP tool (`tools/MedApp.DevMcp`, which isn't in `MedApp.sln`), then runs `dotnet test MedApp.UnitTests` in Release with coverage.
- `integration-tests` runs `dotnet test MedApp.IntegrationTests` in Release, with SQL Server in the runner's Docker (Testcontainers).
- `e2e` tests the production image under the chart's restrictions (a read-only root with a writable `/tmp`, every capability dropped, no privilege escalation; one copy, without kind's Valkey backplane). It builds the image itself, reading the cache earlier runs left (it runs alongside the `image` job, so on a cold cache both build from scratch). It starts SQL Server in a step, on the runner's loopback with a random masked password, migrates with the image's `/app/efbundle`, makes the first admin with `create-first-admin` and a password generated for the run, starts the API on http://localhost:8080, then runs `MedApp.E2ETests` against it. A failed test's Playwright trace is uploaded as the `e2e-traces` artifact. The session cookie stays `Secure`: browsers keep it from `http://localhost`. Both new jobs read the repository only (`permissions: contents: read`).
- `coverage-comment` posts one sticky PR comment with both: .NET classes and React files under 80% line coverage.

What coverage counts (the aim is full coverage of everything with behaviour worth testing):
- **.NET:** handlers, validators, domain logic, Infrastructure services, and the API (controllers, middleware, authentication, server rendering). Excluded: commands, queries, DTOs, repositories, DbContext, migrations, generated code (`obj/`), the Aspire service defaults, and wiring (`ServiceExtensions`, `ApplicationBuilderExtensions`, `Program`), plus `SessionService` and `IdentityTransactions`, which need a real database (the integration tests cover them, but don't count towards coverage).
- **React:** everything in `src/` except generated code, the theme build, test helpers, the thin route files and the browser entry. `npm run coverage` (CI's web job) fails below the thresholds in `vite.config.ts`, set just under the current figures (99% lines, statements and functions, 92% branches).
- New code comes with tests that cover it. Leave a branch uncovered only when it's unreachable or needs a real browser or database, and say so in the PR.

`.github/workflows/codeql.yml` runs CodeQL on PRs to `main`, on pushes to `main` and weekly: C# (built), JavaScript and TypeScript, and the workflows themselves. Results go to the repository's code scanning alerts.

`.github/dependabot.yml` has Dependabot propose weekly updates, grouped (minor and patch together), for GitHub Actions, NuGet, npm (`MedApp.Web` and `tools/ui-parity`) and Docker. It can't read the Dockerfile's `ARG` image tags or `DOTNET_EF_VERSION`, so bump those by hand.

**Supply chain rules:**
- **Pin every third-party action to a full commit SHA,** with its tag in a comment (`uses: actions/checkout@<sha> # v4.4.0`). Resolve the SHA from the tag yourself (`gh api repos/<owner>/<repo>/git/ref/tags/<tag>`, dereferencing an annotated tag), never from a README. Dependabot keeps them current.
- **Give each job only the permissions it needs,** at the job, not the workflow.
- **NuGet audit:** every csproj treats NU1903 (high) and NU1904 (critical) as errors, for transitive packages too, so a newly published advisory fails the build until the package is updated. There's no `Directory.Build.props`, because the Dockerfile copies only the csproj files before restoring.
- **When a gate fails overnight** (an advisory published against a package with no fix yet, or a base image Microsoft hasn't rebuilt), don't switch the gate off. Accept the one known issue for a short, dated time, in the same PR as a `docs/todo.md` entry it expires with:
  - **NuGet:** `<NuGetAuditSuppress Include="https://github.com/advisories/GHSA-…" />` in an `ItemGroup` of only the affected csproj, with a comment giving the reason, the date it was added and the date to review it (at most 30 days).
  - **npm:** an `overrides` entry in `MedApp.Web/package.json` that forces a fixed version of the transitive package, if one exists. If none does, change the `web` job's audit to omit that package (`npm audit --audit-level=high --omit=dev` for a dev-only one), with a comment naming the advisory and its review date.
  - **Trivy:** a `.trivyignore` file at the repository root, one line per CVE with an expiry (`CVE-2026-12345 exp:2026-12-31`) and a comment above it with the reason, passed to both Trivy runs (`--ignorefile /out/.trivyignore`). Trivy stops ignoring it on that date, so the gate fails again if nothing has changed.

`.github/workflows/e2e.yml` runs the E2E suite by hand against an app already running somewhere (a URL you give it), with the credentials in the `E2E_USERNAME` and `E2E_PASSWORD` repository secrets (an admin account). **Those secrets don't exist yet: create them for a known deployment before using it.** Without them, the run fails rather than skips (in CI, missing credentials fail the suite; locally they skip it). It saves no traces, as a trace records the real credentials.

`.github/workflows/publish.yml` runs on every push to `main` except docs-only changes, so it runs after each merge:
1. `image` builds the API's image and pushes it to GHCR (`ghcr.io/aliator/medapp-api:<commit>`). It then makes an SPDX SBOM of the pushed image (`anchore/sbom-action`, also uploaded as the `medapp-api-sbom` artifact) and attests both the build provenance and the SBOM (`actions/attest`), pushed to GHCR beside the image. It's the only job with `id-token: write` and `attestations: write`. Check an image with `gh attestation verify oci://ghcr.io/aliator/medapp-api:<commit> --repo Aliator/MedApp`. Nothing checks the attestations before a deploy yet.
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
