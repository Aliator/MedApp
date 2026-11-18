# AGENTS.md

Guidance for AI coding agents working in this repository.

## Overview

MedApp is a patient and procedure management app: a .NET 10 Web API (Clean Architecture, CQRS, MediatR) and a React frontend (`MedApp.Web`: React 19, TypeScript, Vite and Astryx). The original Blazor WebAssembly frontend is kept, deprecated, as `MedApp.Client.Deprecated`. Data is in SQL Server through EF Core, ASP.NET Core Identity handles users, and authentication is a custom cookie-based session scheme.

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

See [Migrations](#migrations) before adding or applying one.

**Kubernetes (kind):** `deploy/kind/up.sh --demo-data kind-demo` runs the production image on a local kind cluster at http://localhost:8080 (and https://localhost:8443), beside the AppHost, with its own database, using the Helm chart in `deploy/helm/medapp`. Its migrations Job runs the image's EF migrations bundle, then makes the first admin. It also runs Azurite (the Azure Storage emulator, port 10000) for attached files, unless `attachments.azurite.enabled` is off.
- **The chart is locked down** (network policies, the `restricted` Pod Security Standard, read-only filesystems, secrets from External Secrets), so a new container, port or connection needs a matching change there.
- Each API pod runs `api` and `pages` (the Node page server, without the API's secrets) from one image (see Server-side rendering under Local dev gotchas).
- `up.sh --argocd` has Argo CD deploy from git. Each merge to `main` runs the Publish workflow, which pushes the image to GHCR and records the commit and image tag on the `env/kind` branch Argo CD follows (`deploy/argocd/README.md`). Roll back by reverting on `main`. Never commit to an `env/*` branch by hand except in an emergency; the next deploy replaces it.

**Azure** (AKS, Azure SQL, Key Vault, Azure Container Registry, a storage account for files) is in `deploy/azure`: written and checked (CI compiles and lints `main.bicep`), but **not deployed**. It costs money, so never run `deploy.sh` or `bootstrap.sh` without the user asking. Once set up, Publish's `azure` job deploys each merge through `env/azure`; until then it's skipped. Chart versions for both clusters are in `deploy/versions.sh`; see each folder's `README.md`. After changing the chart run `helm lint --strict deploy/helm/medapp --set image.tag=ci` (CI's `helm` job lints it).

**Testing a change: only what it touches.** CI runs every suite on each PR (about 9 minutes), and a PR merges only when green. Locally:
- **Use Release** (`-c Release`): a Debug build of either test project rebuilds `medapp-dev` into the folder the running tool has locked (see [Dev MCP server](#dev-mcp-server-medapp-dev)).
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
| `MedApp.Domain` | Entities and enums (`Patient`, `PatientProcedure`, `PatientProcedureStage*`, `Attachment`, calendars (`CalendarBlock`, `CalendarActivityType`, day and week templates), `Appointment`, `UserSession`). No dependencies. |
| `MedApp.Contracts` | Request/response DTOs. The React app gets them as TypeScript types generated from the API's Swagger document. |
| `MedApp.Application` | Commands, queries, handlers, validators (MediatR + FluentValidation); repository and service **interfaces**; app exceptions. |
| `MedApp.Infrastructure` | `MedAppDbContext`, EF configurations, repositories, Identity and session services, migrations. |
| `MedApp.API` | Thin controllers, the session auth handler, exception middleware; in production it serves the built React app from `wwwroot`. |
| `MedApp.Web` | The UI (React 19, TypeScript, Vite, Astryx), rendered on the server by a Node server (`server/server.mjs`) and hydrated. A solution folder in `MedApp.sln`, built with npm. |
| `MedApp.Client.Deprecated` | **Deprecated** Blazor WASM UI. Not run, no new work; kept building for reference. See its `README.md`. |
| `MedApp.AppHost` | .NET Aspire AppHost for local dev: SQL Server and Azurite containers, the API and the React app. On publish it builds the image from `MedApp.API/Dockerfile` (API, React app in `wwwroot`, Node and the page server in `/app/ssr`). |
| `MedApp.ServiceDefaults` | Aspire defaults (OpenTelemetry, health checks at `/health` and `/alive`). Health checks are on the app's port in Development; elsewhere only on `HealthChecks:Port` (8081 in Kubernetes, unreachable from outside the pod), and absent without it. |
| `MedApp.UnitTests` | NUnit tests for Application handlers/validators, Infrastructure services and the API's own services (`Api/`). |
| `MedApp.IntegrationTests` | NUnit tests of the real API in memory (`WebApplicationFactory<Program>`, as Production) against SQL Server 2022 and Azurite in Docker (Testcontainers), across every area, including racing bookings and templates across a clock change. Also `SessionService` and `IdentityTransactions`, which unit tests can't cover. |
| `MedApp.E2ETests` | Playwright for .NET tests of `MedApp.Web`'s main journeys. See its `README.md`. |

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
3. The command/query, handler and validator in `Application` (registered by assembly scanning). A handler that reads or changes patient data records it in the activity log (see [Activity log and patient privacy](#activity-log-and-patient-privacy)).
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

### Procedures

Concept and decisions: `docs/procedures-ui-plan.md`. Admins build **templates**, ordered lists of **stage definitions** (a stage may repeat). Staff create a patient's **procedure** from one, copying its stages (name, description, instructions). Stages are worked strictly in order: notes, then finish, which locks the stage until an admin unlocks it.
- **The rules live in `MedApp.Application/Procedures/Common`:** `ProcedureWorkflow` (statuses, current stage, what's editable, overdue) and `ProcedureChange` (applies a change, moves the status, writes the audit entries). Use them; don't set `Status` or stage flags by hand.
- **Every change is audited** (`PatientProcedureAuditEntry`, no foreign keys so history outlives deletes). New procedure commands must go through `ProcedureChange`.
- **Due dates are whole days,** due by the end of the day UK time, stored as that moment in UTC (`DueDates`); the API takes and returns `DueDate` as a date.
- **Definitions and templates in use are archived, not deleted** (deleting one is a 409). Patients with procedures or appointments can't be deleted (409).
- **A stage can involve a visit** (`VisitRequirement`, copied onto the stage): see [Calendar and appointments](#calendar-and-appointments).
- **A procedure has several notes** (`PatientProcedureNote`, Markdown, 4,000 characters), newest first, each with author, written and last edited times. `PATCH` refuses notes (400, `UpdatePatientProcedureValidator.NotesMovedMessage`, so an old page is told, not silently ignored), and the Edit Procedure page has none. A new procedure's notes become its first note, by its creator (`ProcedureCreation.Record`).
  - **Endpoints, for staff:** `POST .../patient-procedures/{id}/notes` `{text}` (201, `Location` the procedure) and `PUT .../notes/{noteId}` `{text, expectedVersion}`. Notes come only in the procedure's response (`NoteList`, each with `CanEdit`, plus `CanAddNotes`), so reading them is its `ProcedureViewed`. **`Notes` stays a string** (deprecated: the first note's or the old column's text); never change an existing response field's type.
  - **When:** whenever details can be edited (`ProcedureWorkflow.CanChangeNotes`), so Blocked and **Completed** too; not Cancelled or Archived (409). Restricted or anonymised records: the usual `GetForUpdateOrThrowAsync` 409.
  - **Who:** staff add; **only the author edits** (403 otherwise, admins included: what someone wrote stays theirs). **Only admins delete;** staff ask. A note migrated with no known creator has no author ("Unknown") and can't be edited.
  - **Versions:** every add and edit writes a `PatientProcedureNoteVersion` (text, who, when; no foreign keys, patient and procedure copied in). An edit from a stale `expectedVersion` is a 409 (`ProcedureNotes.ChangedMeanwhileMessage`); the row version catches simultaneous saves.
  - **Audit:** `NoteAdded = 13` and `NoteEdited = 14`, through `ProcedureChange.AddNote`/`EditNote` (moving `LastUpdated`; the monitor is told, though no rule watches them). **Details hold the note's ID and version, never its text** (DPIA risk 4).
  - **Deleting is soft, by admins, with a reason** (`POST .../notes/{noteId}/delete` `{reason, detail?, expectedVersion}`, 204; `ProcedureChange.DeleteNote`), in any procedure status (it's a correction), never while restricted or anonymised.
    - **Reason:** `NoteDeletionReason`, stored by number, so never renumber it: `WrongPatient = 0`, `WrittenInError = 1`, `Duplicate = 2`, `Other = 3` (words in `NoteDeletionReasons`). Detail: up to 500 characters, required for Other.
    - **Effect:** sets `DeletedAtUtc`, `DeletedByUserId`, `DeletionReason`, `DeletionDetail`; hidden from the procedure page (`ProcedureNoteMapper.ToResponses`, and the `Notes` string), but the row and versions stay for history, the timeline and the export. Editing or deleting it again is a 409 (`ProcedureNotes.DeletedMessage`).
    - **The version read:** every delete carries `expectedVersion`; a note edited since is a 409 (`ProcedureNotes.EditedSinceOpenedMessage`). Keeping a note needs none.
    - **The author is told** (`AuthorsNoteDeleted`, see [Notifications](#notifications)), so they can add it to the right record.
    - **The old field:** deleting a procedure's oldest note (the one #110's migration copied `PatientProcedures.Notes` into) also clears that column (audit `oldNotesFieldCleared`).
    - **Audit:** `NoteDeleted = 15`: note ID, latest version, `deletionReason` by name, detail, `requestId` if any; **never the text**. Anonymising replaces the detail (`detail` is a `PatientAnonymiser` free-text key).
    - **Per-note flags:** `CanDelete` (admins, while the record can change), `CanRequestDeletion` (others, while it can change and nothing is waiting) and `DeletionRequestId` (the waiting request). `ProcedureActor.IsAdmin` (from the session's role) carries the viewer's role.
  - **Staff request a deletion** (`POST .../notes/{noteId}/deletion-requests` `{reason, detail?}`, 201 with the note; `RequestNoteDeletionHandler`): any note, any status, not while restricted or anonymised.
    - **The request:** `ProcedureNoteDeletionRequest` (`MedApp.Application/Procedures/NoteDeletionRequests`; no foreign keys): note, procedure, patient, requester (ID and username then), reason, detail, status, timestamps and decider. `NoteDeletionRequestStatus` is stored by number, so never renumber it: `Pending = 0`, `Approved = 1`, `Declined = 2`, `Closed = 3` (no decision).
    - **One waiting per note:** `PendingDeletionRequestId` on the note; asking again is a 409 (`ProcedureNotes.AlreadyRequestedMessage`). The note's row version and the filtered unique index `IX_ProcedureNoteDeletionRequests_NoteId_Pending` stop races (`NoteDeletionRequestRepository` makes either the same 409).
    - **Saved together:** request and note share the DbContext (`INoteDeletionRequestRepository`), one `SaveChangesAsync`.
    - Every admin but the requester is told (`NoteDeletionNotifications`).
  - **An admin decides** (`GET api/procedures/note-deletion-requests/{id}`, `POST .../{id}/decision` `{deleteNote, expectedVersion?, reason?, detail?}`, admins). The GET gives the note's text, the patient (as `PatientBanner`), the procedure's title, request and outcome, `CanDecide` and `WhyCantDecide`, and records `ProcedureViewed` (`PatientViewed` once the procedure is deleted). **Delete note** needs `expectedVersion` and uses the admin's reason and detail or else the requester's; **Keep note** declines. The requester is told. Already decided (`ProcedureNotes.AlreadyDecidedMessage`), or a restricted or anonymised record, is a 409; the row version catches two admins.
    - **A direct delete approves** a waiting request (telling its requester).
    - **Deleting a procedure closes** its waiting requests (`Closed`, same save, `DeletePatientProcedureHandler`), untold.
    - **Anonymising** replaces request and deletion details and closes waiting requests; an older API copy doesn't (the chart's README, `docs/todo.md`).
    - **Privacy:** the export lists deleted notes and every request (`ExportedNoteDeletionRequestResponse`, loaded in `PatientRecordRepository`); `WrongPatient` deletions are flagged `IsAboutAnotherPatient`. Hazard H-38.
  - **The old `Notes` column** stays (`PatientProcedure.LegacyNotes`, for the deprecated string) so older API copies work during a rollout (the chart's two-release rule). Migration `AddedProcedureNotes` copied it into a first note, by the creator, or "Unknown" if anyone else had edited it (`DetailsUpdated` with `changes.notes`). Anonymising clears it. Dropping it is a to-do, after copying text only it holds (`docs/todo.md`).
- Status numbers are stored, so never renumber `PatientProcedureStatus`, `PatientProcedureAuditAction` or `NoteDeletionRequestStatus`.

### Patient timeline

The patient page's **timeline** is one feed of what happened to the patient's procedures and appointments, from the procedure audit log and appointment history (`AppointmentEvent`) (`MedApp.Application/Procedures/Timeline`).
- **Endpoints, for staff:** `GET api/patients/{id}/timeline` (newest first, `Kinds` and `ProcedureIds` filters, `Before` cursor) and `GET api/patients/{id}/timeline/overview` (open and blocked procedures, soonest due first, overdue flagged, and the procedures to filter by). Both 404 for an unknown patient.
- **Kinds** (`PatientTimelineKind`): Created, Stages, Notes (stage and procedure notes), Assignments, Blocks (cancelling a blocked procedure is Status), Status, DueDates, Details, Deleted, Files (attached or deleted, never downloads) and Appointments (every history entry). `PatientTimelineKinds` computes them alike in memory and SQL, from markers in the JSON details. Not stored, so they can be regrouped; a change to `ProcedureAudit`'s details must keep its tests passing.
- **Two feeds, merged** by time with the same filters, so one cursor pages through both. An appointment entry's `ProcedureId` and `ProcedureTitle` are null when it isn't for a procedure.
- **What each person sees is decided on the server** (`GetPatientTimelineQuery.IsAdmin`, from the role), because the audit log is admin-only and keeps old values:
  - **Staff** see entries only for existing procedures: kind, who (the username then, a deleted account's too), when, the procedure (a link) and structured old and new values (due dates, priorities, statuses, template, assignees). For appointments: status, time, member of staff, stage; nothing for a deleted procedure's. **Never free text:** no note text, titles, block or cancel reasons, file names, appointment notes, or the reason for finishing without a visit; only which field changed (a procedure note's is `note`, with no values).
  - A file on the patient themselves has procedure ID `PatientProcedureAuditEntry.NoProcedure` (all zeros) and an empty title.
  - **Admins** see everything: deleted procedures' entries (labelled, with the title then), file names, appointment notes and reasons, finishing-without-visit reasons, deletion reasons and details, and note text from the versions (before an edit, and a deleted note's) (`GetPatientTimelineHandler`, `PatientTimelineRow.NoteText`).
  - **Adding a field to `PatientTimelineEntryResponse`:** audit free text goes to admins only (`TimelineMapper`'s `PublicValueFields` allowlists edited fields). `TimelineStaffCanaryTests` records every action with one string as all free text and checks no staff response holds it; add any new action or free-text field there.
  - `docs/governance/` states this rule; change both together.
- An entry shows the procedure's current title, or once deleted the title recorded then (so a rename before deletion shows under both).
- **Order:** `AtUtc` then `Id`, newest first. One change's entries are each a tick (100 ns) apart (`ProcedureChange.Audit`), so they sort in order; older entries sharing a time fall back to ID.

### Notifications

- Handlers call `INotificationSender.SendAsync(new Notification { … })` (`MedApp.Application/Notifications/Common`) after their own save. It stores the notification, then pushes it to every page the user has open.
- **Live delivery is SignalR:** `NotificationsHub` at `/api/notifications/hub`, by user ID (the session's `NameIdentifier`), through `SignalRNotificationPublisher`. Best effort: a failure is logged, and the notification shows on the next load.
- **API copies share a backplane:** `ConnectionStrings:SignalRBackplane`, a Redis-compatible server (`SignalRBackplane`): Valkey in Kubernetes, unset in development. Browsers use WebSocket only, without negotiating (`LiveNotifications`), as the two requests could reach different copies.
- `GET api/notifications` returns the newest and the unread count; `POST api/notifications/read` marks all, `POST api/notifications/{id}/read` one.
- **Kinds** (`NotificationKind`) are stored by number, so never renumber them: 0 assigned a procedure (not by yourself); `AppointmentBooked = 1`, `AppointmentRescheduled = 2`, `AppointmentCancelled = 3` (see [Calendar and appointments](#calendar-and-appointments)); `MeetingInvited = 4`, `MeetingChanged = 5`, `MeetingCancelled = 6`; 7 to 9 kept for calendar kinds; 10 to 17 monitoring, one per `MonitoringEvent`; 18 to 21 note deletion (`Notification.IsNoteDeletion`). No foreign keys; they carry `AppointmentId`, a meeting's day (`CalendarDate`, linking to that Calendar day) or `NoteDeletionRequestId`.
  - **Note deletion** (`NoteDeletionNotifications`): `NoteDeletionRequested = 18` to every admin but the requester (with `ProcedureId` and `NoteDeletionRequestId`, opening the request's dialog); `NoteDeletionApproved = 19` and `NoteDeletionDeclined = 20` to the requester unless they decided; `AuthorsNoteDeleted = 21` to the author unless they deleted it, asked for it (they get 19) or aren't known. Fixed titles (`Notification.HasFixedTitle`, so anonymising leaves them), usernames only: never the note, reason or patient.
- **Preferences** (`NotificationPreferences`, Account Settings, `GET`/`PUT api/notifications/preferences`): only whether to be told of being assigned (appointment kinds can't be switched off yet). `NotificationSender` checks them for optional kinds (`NotificationPreferences.IsOptional`); no row means everything. Kept on the server because it reads them.
- **Notification text never carries free text or the patient.** Titles are fixed phrases; messages use usernames, enums, admin-written names (templates, stages, meeting titles) and dates. (The older assignment notification still uses the procedure's title: DPIA risk 3.)
  - **Meeting titles count as admin-written:** only admins arrange meetings, and the Title field says no patient details. Notifications quote them and outlive the meeting, beyond export and anonymisation (DPIA risk 25). Meeting notes are never in a notification.

### Calendar and appointments

The concept and decisions are in `docs/calendar-plan.md`. Each member of staff's **calendar** holds **appointments** with patients and **blocks** of time for activities; **day and week templates** fill each week with its usual blocks; a stage can **involve a visit**, and the app then asks for an appointment before it's finished.
- **The rules live in two `Common` folders.** Use them; don't set an appointment's `Status`, or work out UK times, slots or clashes, in a handler by hand.
  - `MedApp.Application/Calendar/Common`: `WorkingHours` (the working day, slot length, `WhyOutside(startUtc, endUtc)` for validators), `UkTime` (UK dates and clock times to and from UTC, and dates for messages), `TemplateHistory` (replacing dated rows from today), `CalendarProjection` (a person's days, with template blocks and exceptions, from the repository's `CalendarData`), `CalendarClashes` (what a time would overlap), `CalendarMapper` and `CalendarSetupMapper`.
  - `MedApp.Application/Appointments/Common`: `AppointmentWorkflow` (statuses and what's allowed from each), `AppointmentChange` (applies a change and writes its history) and `AppointmentMapper`.
- **The working day** is 08:00 to 18:00 UK time (Europe/London), Monday to Friday, in 15-minute rows or 5-minute ones (the Calendar page's Rows menu, `rows=5` in its URL, each person's last choice kept). Nothing can be booked or blocked at weekends. An appointment or block must start and end on the same weekday, inside the working day, on a 5-minute boundary, and start before it ends; anything else is a 400. Bank holidays aren't known yet.
- **Times are UTC** ([Data access](#data-access)). Template times are UK clock times (`TimeOnly`), turned into UTC for each date by `UkTime`. Each day in the calendar response has `workStartUtc` and `workEndUtc`, so the browser steps through slots in UTC and never does time zone arithmetic. A UK working day never spans a clock change.
- **Activity types** (`CalendarActivityType`, admin-managed) are what time can be blocked for: a name and a colour from a fixed palette of Astryx `Token` colours (`red`, `orange`, `yellow`, `green`, `teal`, `cyan`, `blue`, `purple`, `pink`, `gray`). Seeded: Lunch, Admin time, Training, Meeting, Annual leave and Travel. Once used, one is archived, not deleted: new blocks can't choose it, existing ones keep it.
- **Blocks** (`CalendarBlock`): a member of staff, an activity type, a start and end, and an optional note (200 characters). Blocks are about staff, never patients: a note must not hold patient details, and the UI says so.
- **No overlaps.** A new or moved appointment, block or meeting may not overlap an appointment (Booked, Attended or Did not attend), a block (stored or from a template) or a meeting on that person's calendar: a 409 naming the clash (for a meeting, who has what), and the UI offers only free slots. `Availability` (shared free time, suggested meeting times) uses the same busy time (`CalendarClashes.BusyTimes`), so the server accepts every suggestion.
  - **Races for one slot** are serialised by a database lock per member of staff (`ICalendarLock`, SQL Server's `sp_getapplock`). Any command that adds or moves something on a calendar takes it before checking for clashes. It protects a calendar, not an appointment (booking one appointment with two people takes two locks), so appointments have their own protection (below).
  - **A later template change can overlap what's there:** both show, the appointment on top, the block flagged as overlapping. The app never moves or removes either.
- **Templates are applied when a calendar is read, never generated** (`CalendarProjection`), so an edit changes every day it applies to from then on. Nothing runs in the background.
  - **A day template** (`DayTemplate`) is named, with blocks (`DayTemplateBlock`: activity type, start and end time, optional note). **A week template** (`WeekTemplate`) gives each weekday a day template or none (`WeekTemplateDay`). Each member of staff may have a **default week** (`StaffWeekTemplate`).
  - **Templates are personal** (`OwnerUserId`): week templates, day overrides and the default week use only the owner's own (404 for someone else's); names are unique per owner. Each person's page is `/calendar/templates`.
  - **History is kept by dating rows, not copying them.** Day template blocks, week template days and default weeks have `ValidFrom` and `ValidTo` (UK dates). An edit closes changed rows on yesterday and adds new ones from today; unchanged rows stay; a row added and changed again today is replaced (`TemplateHistory`). So a past day shows what applied then, and a change shows from today, including today's past blocks.
  - **Changing one day's template block** stores an exception for that date (`CalendarBlockException`) hiding it, plus, for an edit, a stored block with the new values. A later template edit keeps exceptions on blocks it didn't change.
  - **Apply Template** (the Calendar page, on any working days of the week shown) stores a day override (`CalendarDayOverride`) per day: that date uses the chosen day template, or none, instead of the week's. "The default week's" removes it (the dialog treats the API's 404 for a day with no override as done). Blocks added by hand stay.
  - **Past days don't change:** blocks, exceptions and overrides before today (UK) can't be added, edited or removed (409). Past appointments can still have their outcome recorded.
  - **Templates in use are archived, not deleted,** like stage definitions: a day template once a week template or day override has referred to it, a week template once it's been anyone's default. Archived ones can't be chosen again but keep applying.
- **Who changes what** (also in [Auth](#auth)): any staff member sees every calendar and books, reschedules, cancels and records the outcome of any appointment, for anyone. A calendar's blocks, day overrides and default week, and a person's day and week templates: the owner or an admin (`CalendarGuards.EnsureCanChangeTemplatesOf`, 403 otherwise). Activity types, meetings, the team view and suggested times: admins. Staff read them all.
- **A meeting** (`Meeting`, `MeetingAttendee`), arranged by an admin: a title, notes about staff (never patients), a time inside one working day, not on a past day, and at least one attendee, as busy time on each attendee's calendar.
  - Adding or changing one holds every attendee's lock (on a change, also those going before). It has a row version, and a change must carry the `lastUpdated` the caller saw (`expectedLastUpdated`): a 409 if it changed since, so a stale dialog or drag can't undo another admin's change.
  - Only attendees a change newly adds must be staff; one already going whose account has gone is dropped on save, untold. Attendees are told (`MeetingNotifications`), never of their own change. Cancelling deletes it. An account deleted, or losing every staff role, is taken off upcoming meetings (`IMeetingRepository.RemoveAttendeeFromUpcomingAsync`); a meeting left with nobody goes.
- **An appointment** (`Appointment`) is for a patient, optionally one of their procedures and its stage, with a member of staff, a start and end, a status, optional notes (2,000 characters of free text about the patient, treated like procedure notes), and who requested, booked and recorded its outcome, and when.
  - **Statuses** (`AppointmentStatus`): `Requested = 0` (shown as "Requires appointment": no staff or time yet; listed at `/calendar/requires-appointment`, reached from the Calendar page's "Requires Appointment (N)" link, not the side nav), `Booked = 1`, `Attended = 2`, `DidNotAttend = 3`, `Cancelled = 4`.
  - **Changes** (`AppointmentWorkflow`): Requested to Booked, and Booked to Booked (rescheduling), only in the future and clash-free. Booked to Attended or Did not attend once it has started; those two can be corrected either way. Requested or Booked to Cancelled, with a reason (500 characters). Cancelled is final: a patient who didn't attend gets a new appointment. Nothing can be booked in the past, so a walk-in can't be recorded afterwards.
  - **One open appointment per stage** (Requested or Booked); another is a 409 pointing to it (`AppointmentWorkflow.StageHasOpenVisit`). No limit without a stage. `CreateAppointmentHandler` checks it, and the filtered unique index `IX_Appointments_StageId_Open` stops two at once: the repository turns its refusal into `StageHasOpenAppointmentException`, the handler into the same 409. A procedure change whose automatic visit loses that race is a 409 to try again (`PatientProcedureRepository`).
  - **Two saves at once:** `Appointment.RowVersion` makes the later a 409 (`AppointmentWorkflow.ChangedMeanwhileMessage`, or `ProcedureAppointmentChangedMeanwhileMessage` from a procedure change), never a silent overwrite. Every appointment save goes through `SaveChangesOrConflictAsync` with that message.
  - **Booking says what it expects:** `POST api/appointments/{id}/book` takes `expectedStatus`, the status the caller saw (`Requested` to book, `Booked` to move); any other is a 409 (`BookedMeanwhileMessage` when someone booked it first), so a stale "book" can't move someone else's booking. The web app sends its copy's status.
  - **Every change is recorded** in its history (`AppointmentEvent`, action `AppointmentEventAction`): requested, booked, rescheduled (with the old time and member of staff), attended, did not attend, cancelled (with the reason) and notes changed (old and new text). No foreign keys, like the procedure audit log. New appointment commands must go through `AppointmentChange`.
  - Status, action and requirement numbers are stored, so never renumber `AppointmentStatus`, `AppointmentEventAction` or `VisitRequirement`.
- **Stage visits:**
  - **`VisitRequirement`** on stage definitions: `None = 0` (the default), `Suggested = 1` or `Required = 2`, set by admins, copied onto each stage when a procedure is created.
  - **The Requested appointment:** when a stage with a visit becomes current, `ProcedureChange.RequestVisit` creates a Requested appointment for it (requested by the app), unless it already has a Requested, Booked or Attended one. None while the procedure is blocked or closed.
  - **Required:** finishing needs a linked Attended appointment, checked on the server (`ProcedureWorkflow.WhyStageNeedsVisit`); staff get a 409.
  - **The admin bypass:** an admin may finish a Required stage without a visit, after confirming with a reason (the finish endpoint's optional body, `FinishProcedureStageRequest { withoutVisit: true, reason }`; with no body it works as before). The `StageFinished` audit entry records `withoutVisit`, the requirement and the reason.
  - **Suggested:** finishing without an Attended appointment asks for confirmation in the UI only; the server doesn't refuse, and `StageFinished` records `withoutVisit` and the requirement.
  - Finishing a stage again after an admin unlocked it doesn't ask for a visit again.
  - **Tidying up:** finishing a stage cancels its Requested appointments, and cancelling a procedure all of its Requested ones (`ProcedureChange.CancelRequestedVisit`), each with a reason. The app never cancels a Booked one: someone may need to tell the patient, so the procedure page lists them.
  - All of this is saved with the procedure change, in one `SaveChangesAsync`.
- **Notifications** ([Notifications](#notifications)) go to the member of staff, only when someone else made the change: `AppointmentBooked` (booked with you, or moved to you), `AppointmentRescheduled` (moved to another time) and `AppointmentCancelled` (cancelled, or moved to someone else). Fixed titles; the message says who and when, never the patient or the notes. `Notification.AppointmentId` opens it on the Calendar page.
- **Deleting:** a patient with appointments can't be deleted (409). Deleting a procedure (only once cancelled or archived) detaches its appointments (procedure and stage set to none) in the same save; their history keeps it. An account with upcoming Booked appointments can't be deleted, or lose every staff role (`SetUserRolesHandler`), until they're moved or cancelled: both ask `WhyCantStopBeingStaffAsync` (`AppointmentRepositoryExtensions`) and refuse with `HasUpcomingAppointments`.
- **Privacy** ([Activity log and patient privacy](#activity-log-and-patient-privacy)):
  - **Restriction:** changes load through `GetForUpdateOrThrowAsync` (`AppointmentRepositoryExtensions`, `PatientRules.EnsureCanChange`), and adding one checks the patient the same way. Cancelling uses `GetForCancelOrThrowAsync` (`PatientRules.EnsureCanPause`): allowed for a restricted patient, never an anonymised one.
  - **Reads:** opening an appointment records `PatientViewed`; the calendar and appointment search record `PatientListed` for the patients shown. `GET api/calendar?withoutAppointments=true` (Apply Template's days) records nothing.
  - **Export and anonymisation:** `PatientRecordRepository` loads a patient's appointments, their history and the notifications about them. `PatientAnonymiser` replaces appointments' notes and cancellation reasons, and the history's notes, reasons and procedure titles. Appointment notifications stay as they are (`Notification.HasFixedTitle`).
  - **Not monitored:** appointments aren't `MonitoringEvent`s, nor is the admin bypass yet (`docs/todo.md`).
- **Endpoints** are under `api/calendar` and `api/appointments` (the plan's section 4).

### Notepad

Each member of staff has one notepad (`Notepad`, keyed by account ID): Markdown jottings beside every page of the web app (`MedApp.Application/Notepads`).
- **Endpoints:** `GET`/`PUT api/me/notepad` (`{text, expectedVersion}`), for anyone signed in (`[Authorize]`, no role), always the caller's own; the response carries `version` (0 for none) and `keepDays`. A save from another version is a 409 (`NotepadRules.ChangedMeanwhileMessage`); the row version and the key catch two saves at once (also 409).
- **Kept on the server, never in the browser,** because it may hold patient details. The web app keeps only whether it's open (`medapp.notepadOpen`).
- **Retention:** deleted when next read or saved once it has gone `Notepad:KeepDays` (30, at least 1, checked at startup) without a change, so one in use is never cleared (ageing its text is a to-do). A save that finds it expired is a 409 (`NotepadRules.ClearedMessage`), so old text isn't saved again. No scheduled job yet (`docs/todo.md`). Deleted with the account (cascade).
- **Not in the activity log:** it's the user's own, not linked to a patient (`EndpointCoverageTests` says so), so a patient's export and anonymisation can't find it. The DPIA accepts that (risk 26), with the 30 days and **Add note to procedure** as the controls.
- **Add note to procedure** (web only) adds its text, or the selected part (as Markdown, from the editor's selection), as a note on an open procedure (`POST .../notes`), after a confirmation showing the patient as the banner does (hazard H-37). "Remove it from the notepad" removes exactly that range by position, never by searching for its words, and says so when it can't.

### Monitoring

Admins' rules for being told what staff do to procedures (`/admin/monitoring`, `api/monitoring/rules`, admin-only), e.g. "Notify me when **sam** finishes **Bloods** on a **High or Urgent** priority procedure".
- **A rule** (`MonitoringRule`) belongs to one admin, who alone sees and changes it (another's is a 404). It names a person or anyone, an event (`MonitoringEvent`, stored by number: procedure created, stage finished, procedure completed, blocked or cancelled, someone assigned, a stage unlocked, a due date changed) and optional conditions: a minimum priority, a template, and for the two stage events a stage definition. It can be switched off. At most 50 each (`MonitoringRuleInput.MaxRulesPerAdmin`; count then insert, so two adds at 49 can make 51, accepted). No foreign keys: a rule naming something deleted stays, shows it as deleted and matches nothing.
  - **Switching** (`PUT api/monitoring/rules/{id}/enabled`) changes nothing else, so always works. An edit (`PUT .../{id}`) checks only the person, template and stage it newly names (`MonitoringRuleInput.CheckAsync`).
- **The person** is who did it, except for ProcedureAssigned: the one assigned. **For StageUnlocked the person must be an admin** (400 otherwise).
- **How it fires:** every procedure command calls `IProcedureMonitor.NotifyAsync(change)` after saving its `ProcedureChange`. `ProcedureMonitor` reads what it did from its audit entries (`MonitoredActivity`, ignoring automatic status changes, notes and starts); loads the switched-on rules for those events (one query, only when a rule could match); matches them (`MonitoringRuleMatcher`: the template after the change, the higher of the priority before and after); leaves out the admin who made the change and, for an assignment, the assignee; skips (doesn't delete) rules of owners no longer admins (`GetAdminsAsync`, only when a rule matched); and sends each remaining admin one notification per change however many rules match (`MonitoringNotifications`).
- **Best effort, after the save:** not in the change's transaction (no outbox). A failure is logged and the change stands; only the request's own cancellation is rethrown. A failed insert is detached from the DbContext (`NotificationRepository.AddAsync`) so a later save doesn't retry it. Clinical follow-up must never rely on monitoring (hazard H-29).
- **Privacy:** a patient's export leaves monitoring notifications out (`Notification.IsMonitoring`, in `PatientExportMapper`): they're about staff. Anonymisation leaves monitoring and appointment notifications as they are (`Notification.HasFixedTitle`): fixed titles, no free text or patient details, and new messages must keep it so (the canary in `PatientPrivacyTests` checks).
- **Tests:** `ProcedureCommandsMonitoringTests` checks every procedure command calls the monitor after its save, with the request's token; `MonitoringNotificationsTests` checks every event's message.
- **A new event:** add it to `MonitoringEvent` (a new number), `MonitoredActivity`, `MonitoringNotifications` (its own `NotificationKind`, inside `Notification.IsMonitoring`'s range), the web app's `MONITORING_EVENTS` and `ruleText`, and the governance documents listing the events (`dpia.md` risk 24, the staff privacy notice, hazard H-29).
- **`medapp-dev`'s `reset_demo_data`** deletes every rule, notification choice and notification.

### Attachments

Staff attach files to a **patient**, **procedure** or **stage** (`MedApp.Application/Attachments`, `api/attachments`). Bytes are in blob storage behind `IFileStore`; metadata (`Attachment`: name, sniffed type, size, SHA-256, scan state, who, when, target) in SQL.
- **Endpoints, all for staff:** `GET api/attachments?patientId=` (everything for the patient), `?procedureId=` (not its stages') or `?stageId=`, exactly one, newest first, at most 200 (`AttachmentLimits.MaxListed`; `TotalCount` gives the total); `POST api/attachments?…` (upload); `GET api/attachments/{id}/content` (download) and `/thumbnail`; `DELETE api/attachments/{id}`.
- **Uploading:** raw bytes as `application/octet-stream` (anything else is a 415, so another site can't send one without CORS, which nothing allows), the name percent-encoded in `X-File-Name`; not multipart. At most 20 MB: `[RequestSizeLimit]` on that action only, and the handler counts bytes too (Kestrel's limit is untouched).
  - The body streams into a temporary file (`IUploadStaging`: `/tmp`, readable only by the API's user) while hashed, never all in memory. On Linux its name is deleted once opened; on Windows on close, and `StaleUploadSweeper` clears leftovers at start-up.
  - **The target is checked before the body is read and again after** (`CheckTargetAsync`), as it may be restricted, anonymised, deleted or finished meanwhile (closing the window fully would need the save to re-read the patient's flags). Then: type (`FileTypeSniffer`), malware scan, blob, thumbnail, then the record and audit entry in one save. If anything after storing the blob fails, the blob is deleted.
  - **Types come from the contents, never the name or the browser** (`FileTypeSniffer`, `AttachmentFileTypes`): PDF, PNG, JPEG, HEIC/HEIF, `.docx` (read from the ZIP's central directory without unpacking; macros refused) and UTF-8 text. **SVG and HTML are refused by name**, as is text starting with markup. The extension must match the type. A new type needs its signature, tests, and a check that browsers can't run it.
  - **Visible active content is refused:** a `.docx` with an external relationship (`TargetMode="External"`), `word/embeddings/` or `word/activeX/`; a PDF containing `/JavaScript`, `/JS`, `/Launch`, `/EmbeddedFile` or `/RichMedia`. The rest is left to the malware scanner.
  - Names are cleaned (`AttachmentFileName.Clean`: no folders, control characters or Unicode direction controls, at most 255 characters).
- **Files only leave through the API,** never as a storage link or inline on the app's origin. `AttachmentResponseHeaders` sends `Content-Disposition: attachment` (ASCII `filename` and RFC 5987 `filename*`), the sniffed type, `nosniff`, `Cache-Control: no-store`, `Content-Security-Policy: sandbox; default-src 'none'` (replacing the `/api` policy) and `Cross-Origin-Resource-Policy: same-origin`. Pages show only thumbnails; everything else is a download.
- **Thumbnails** (PNG and JPEG, at upload: `SkiaThumbnailGenerator`, SkiaSharp) are turned upright from EXIF, at most 320 px, re-encoded without the original's metadata. The header is read first: no thumbnail (the file is still attached) for an image whose **own** size is over 24 million pixels or 10,000 px a side, one that can't be read, or HEIC. Thumbnail views aren't audited.
- **Expensive work is limited:** at most two uploads at a time per API copy sniff or make thumbnails (`FileWorkLimiter`). A `.docx` listing over 5,000 entries (`FileTypeSniffer.MaxZipEntries`) is refused unopened.
- **Malware scanning:** `IMalwareScanner`, today `NoMalwareScanner`, which marks every file `NotScanned` (shown on each file). `Infected` refuses the upload. A real scanner is a to-do (`docs/todo.md`).
- **Who may do what:** staff list, upload and download any patient's files. Files can be added to a patient always, a procedure unless cancelled or archived, and a stage only while its notes could be edited (`AttachmentTarget.WhyFilesCantChange`; otherwise 409). Staff delete their own, under the same rule; admins any, at any time. `CanDelete` says whether the caller may.
- **Deleting is soft** (`DeletedAtUtc`) and audited: hidden from staff, listed for admins (`includeDeleted=true`), downloadable by nobody. Bytes stay until a purge job after a retention window, not built yet (`docs/todo.md`). A patient or procedure with live files can't be deleted (409): attachments have no foreign keys.
- **Audit:** adding, deleting and downloading are procedure audit entries (`AttachmentAudit`; `AttachmentAdded`, `AttachmentDeleted`, `AttachmentDownloaded`, stored by number, so never renumber them), with the file's ID, name, type, size and target. Not through `ProcedureChange`, so `LastUpdated` doesn't move. Added and deleted files are on the [Patient timeline](#patient-timeline).
- **Storage** (`AddAttachmentStorage`): `ConnectionStrings:Attachments` (Azure Storage) and `Attachments:Container` (default `attachments`, created on first use). Azurite in development and on kind; in Azure, a storage account behind a private endpoint, its connection string in Key Vault. **Without a connection string the API still runs:** uploads and downloads are a 503 (`FileStorageUnavailableException`), lists work (CI's `e2e` job; Kubernetes with the optional attachments Secret missing). **A failing store** is a 503 (`StorageUnavailableException`, `BlobFileStore.Guard`) after the client's retries (`AttachmentStorageOptions.ClientOptions`: 2 retries, 30 seconds a try); a failed upload's clean-up gets 10 seconds.
- **Privacy** ([Activity log and patient privacy](#activity-log-and-patient-privacy)): names are free text and files special category data; a change to what's kept, who sees it or how long must update `docs/governance/` (`data-inventory.md`, `dpia.md`).
  - **A restricted or anonymised record's files can't change** (patient, procedures or stages): the guard's 409 (`PatientRules.EnsureCanChange` on `AttachmentTarget.Owner`), admins too. Listing and downloading still work.
  - **Reads:** listing records `AttachmentsViewed`, a download `AttachmentDownloaded`, against the patient (and procedure), deduplicated like other views. Thumbnails record nothing.
  - **The export** lists every file's details (deleted too: name, type, size, SHA-256, scan state, target, who uploaded and deleted it), not the files (`docs/todo.md`).
  - **Anonymising** renames every file to `Anonymised file` plus its extension, removes names from audit details (`fileName` is a `PatientAnonymiser` free-text key), and deletes every live file, as the anonymising admin, with an `AttachmentDeleted` entry each (saved with the record).
  - **Retention:** a download is a read and doesn't move the conclusion of treatment (`RetentionSchedule.ReadOnlyAuditActions`, left out of the retention review's query); attaching and deleting do.

### Activity log and patient privacy

Roadmap step 7. What it records and why: `docs/governance/` (the data inventory, the DPIA and the rights procedure).
- **The activity log** (`ActivityEntry`, through `IActivityLog` in `MedApp.Application/Activity/Common`), separate from the procedure audit, records who read or changed patient data, sign-ins (refused ones too, with the account's ID when the username exists, and the two-factor step: see [Auth](#auth)), sign-outs, account and role changes, and email, password reset and two-factor security events (`ISecurityEventLog`), with the request's address.
  - **Reads:** a handler returning a patient's data calls `RecordViewsAsync`: `PatientViewed` (the patient's page, timeline and its overview, a FHIR read), `PatientListed` for each patient in a list's results (patient and procedure search, retention review, a FHIR search), `ProcedureViewed` (a procedure's page, a note deletion request's dialog, or `PatientViewed` once its procedure is deleted). The same view by the same person within `Activity:ViewWindowMinutes` (5) adds nothing.
  - **Records nothing, on purpose:** the side nav's pin labels (`GET api/patients/labels`, `GET api/procedures/patient-procedures/labels`: at most 20 names and titles the user chose); the admin-only logs; insights (counts only); a user's own notifications; a calendar read `withoutAppointments`. The deprecated `GET api/patients` records one `PatientsSearched` entry ("all"), so it's in no patient's access report.
  - **Files:** `AttachmentsViewed` and `AttachmentDownloaded` ([Attachments](#attachments)). **A demographics (PDS) lookup:** `DemographicsLookedUp`, with whether anyone was found, never the NHS number.
  - **`EndpointCoverageTests`** (integration tests) lists every GET endpoint with what it records, and fails on any it doesn't know: classify a new one there.
  - **Changes to a patient:** build the entry with `activityLog.Entry(...)` and pass it to the repository method (`AddAsync`, `UpdateAsync`, `DeleteAsync`, `IPatientRecordRepository.SaveAsync`), which saves both in one `SaveChangesAsync`, so no change can miss the log.
  - **Details never hold a patient's details or clinical text:** field names, never values; filter names, never terms (`nhsNumber`, never the number). The only free text is an admin's reason for a restriction, dispute or keep, which anonymising replaces; anonymising's own reason is a fixed choice, with an optional note that must hold no patient details.
  - **Who:** the actor (user and address) comes from `IActivityContext` (`HttpActivityContext` in the API, `DevToolActivityContext` in `medapp-dev`), not the command. Procedure commands still take a `ProcedureActor`.
  - **It fails closed:** a read or sign-in whose entry can't be written fails (500).
  - Action numbers are stored, so never renumber `ActivityAction`. No foreign keys, like the audit log.
  - `GET api/activity/search` (admins) searches it; a patient's page shows its latest entries about them as the access report.
- **Restricted and disputed records** (Articles 18 and 16): admins set them with a reason (`PUT api/patients/{id}/restriction` and `.../dispute`).
  - A restricted or anonymised record can't be changed, nor its procedures, appointments or files: `PatientRules.EnsureCanChange` (409), applied by `GetForUpdateOrThrowAsync` to every procedure change, its namesake in `AppointmentRepositoryExtensions` to every appointment change, and the attachment handlers to every upload and delete.
  - **So care can be paused safely,** a restricted record's procedures can still be blocked or cancelled with a reason, and its appointments cancelled (`GetForCancelOrThrowAsync`): `ChangePatientProcedureStatusHandler` loads them through `GetForPauseOrThrowAsync` for those two statuses (`PatientRules.PausesWhileRestricted`). Restricting a record with procedures under way or open appointments needs `ConfirmOpenProcedures` (400 without it); the dialog shows how many of each, and the API's message if it still asks. Hazard H-22.
  - `GetForUpdateAsync` is called only by those two guards; a convention test checks it, and that every procedure command uses one.
  - A procedure's response gives `ReadOnlyReason` (every `Can...` flag false; only Blocked and Cancelled allowed while restricted) and the patient's flags (`PatientFlags`) for the banners on the procedure, edit and patient-procedures pages. Disputed is a banner only.
- **Patients have a row version** (`Patient.RowVersion`): a save from a copy read before someone else's is a 409 (`SaveChangesOrConflictAsync`, `PatientRules.ChangedMeanwhileMessage`), so a stale edit can't undo a restriction or anonymisation. `SaveChangesOrConflictAsync` takes the message to give (appointments use it too).
- **Everything about a patient** is loaded by `IPatientRecordRepository` (`PatientRecordRepository`) for the subject access export (`GET api/patients/{id}/export`, admins, `no-store`, itself logged) and anonymisation. **A new table holding patient data must be added there.** The export has its own types (`Exported…Response`): staff by username only, never account IDs, network addresses or notification read times, and the JSON holds exactly what the printable page shows.
- **Anonymisation** (`POST api/patients/{id}/anonymise`, admins, `PatientAnonymiser`) replaces identifying details and free text with placeholders in place, across every table, keeping IDs and structure. **The result is pseudonymised, not anonymous:** the patient ID, year of birth, dates, templates and who did what remain, so it's still personal data. It needs the patient's full name typed and a reason from `AnonymisationReason` (optional note), and is refused while a procedure is under way, an appointment is open (Requested or Booked: cancel it or record its outcome first), or the record is restricted. Deleting a patient is unchanged.
- **Retention** (`RetentionOptions`, the `Retention` section of `appsettings.json`; `RetentionSchedule`): `GET api/patients/retention-review` lists records past their minimum period; an admin reviews, keeps (`POST api/patients/{id}/retention/keep`, with a reason and a review date up to two years ahead) or anonymises. **Nothing is ever deleted automatically.**
  - Only adult and children periods exist, not the longer specialty classes (mental health, maternity, oncology): customers with those set `Retention:AdultYears` to their longest, and the page and anonymise dialog warn.
  - The conclusion of treatment counts the procedure audit too, so a deleted procedure still counts; a 29 February birthday falls on 1 March in other years (`RetentionSchedule.AddYears`).
- **Sign-ins:** one throttled by `LoginThrottling` (429) is refused before the handler, so it's not in the activity log (the throttling logs it at Warning). A name no account could have (`UsernameRules.CouldExist`) is logged as a refused sign-in with no account. Signing out everywhere is logged (`SignedOutEverywhere`), and a password change or reset in the same transaction as the change and the end of the sessions.
- **Not built yet:** purging sign-in events and other operational data, which needs a scheduled job (`docs/todo.md`).

### Insights

Admins' overview (`/admin/insights`, `GET api/procedures/insights`): headline counts against the same point last week, overdue work by assignee and priority, procedures created and completed each week, and average time at each stage of each template.
- **Aggregated in SQL:** the LINQ is in `ProcedureInsightsQueries` (`Application/Procedures/Insights/Common`), each query ending in a `GroupBy` or `Count`, run by `ProcedureInsightsRepository` and, in unit tests, in memory. `ProcedureInsightsSqlTests` checks each translates to SQL Server's SQL (`ToQueryString`, no database).
- **SQL-only functions** go in `InsightsFunctions` (with a C# body for tests), mapped in `InsightsDbFunctions` (`SecondsBetween` is `DATEDIFF_BIG`, `JsonValue` is `JSON_VALUE`). They're not in the model snapshot, so need no migration.
- **Last week's state is rebuilt backwards from now** from recent audit entries: status then is the `"from"` of the first `StatusChanged` entry after it (`{"from":"…","to":"…"}`, as `ProcedureAudit` writes it), due date then the `"from"` of the first `DetailsUpdated` with `changes.dueDate` after it; with none, the current value. So keep those details formats. Completions are `StatusChanged` entries to Completed (each one counts), creations `Created` entries.
- **Keep SQL Server on the index:** a correlated `TOP(1)` over a procedure's entries seeks the `(PatientProcedureId, AtUtc)` index only while its `ORDER BY` is plain columns; sorting on the details text scans the table for every procedure. Time changes on a large synthetic database (the review of #66 has the numbers).
- **Deleted procedures:** with every template, the audit log's counts include procedures deleted since; with one template they're matched to existing procedures, so deleted ones drop out (the templates' weeks needn't add up to the total). Last week's statuses count only existing procedures.
- **Drill-down links** go to the procedures list with `mine=false` (so a start-on-mine setting can't narrow them) and `templateId`. "Completed this week" has no link: no list shows that.
- **Weeks are UK weeks,** Monday to Sunday (`InsightsPeriod`); "last week" is the same point in it as now is in this one. SQL counts by hour and C# sums into weeks, since UK offset changes are on the hour.
- **Stage times** run from reached (the previous stage first finished, or the procedure created) to started (waiting), then to first finish (working), from the audit log, as unlocking and finishing again moves `CompletedAtUtc`.

### NHS number, FHIR and SNOMED CT

Roadmap step 9's first part (`docs/architecture.md`, `docs/todo.md`).
- **The NHS number** (`Patient.NhsNumber`) is optional, stored as 10 digits (`varchar(10)`), unique when present (filtered unique index `IX_Patients_NhsNumber`) across the deployment until step 8 makes it per organisation.
  - `NhsNumbers` (`MedApp.Domain/Patients`) strips spaces, checks the Modulus 11 digit, spots placeholders that pass it (`IsPlaceholder`: ten of one digit, `0123456789`, `9876543210`) and formats it. `NhsNumberRules` (`MedApp.Application/Patients/Common`) validates it in every command, refusing placeholders with their own message; the web form checks the same (`src/utils/nhsNumber.ts`). Accept it with or without spaces; show it as `XXX XXX XXXX`.
  - A duplicate is a 409 (`ConflictException`), also when two saves race (the repository maps the index violation). SQL Server's message for that holds the number, so EF Core's failed-save and failed-command events are logged at Debug (`LogFailedSavesAtDebug` in `AddDatabase`), which isn't written; keep it that way.
  - On update, null leaves it unchanged and empty removes it. `GET api/patients/search` filters by it (`NhsNumber`: contains the digits typed, spaces ignored).
  - The export includes it; anonymisation (`PatientAnonymiser`) removes it, but not yet from saved searches. **Always Encrypted for it isn't done** (both `docs/todo.md`).
  - **Keep it out of new URLs** (path or query string), since request logs and traces record URLs: send it in a body, as the demographics lookup does. Only the FHIR search (as FHIR defines it) and the patients list's filter (product owner's decision, `docs/todo.md`) carry it in the URL.
- **The FHIR API** (`FhirController` at `api/fhir`; `MedApp.API/Fhir`, `MedApp.Application/Fhir`, DTOs in `MedApp.Contracts/Fhir`): read-only FHIR R4 in UK Core's shape, patients only. `GET api/fhir/metadata` (minimal CapabilityStatement, open to anyone), `GET api/fhir/Patient/{id}`, and `GET api/fhir/Patient?identifier=https://fhir.nhs.uk/Id/nhs-number|{number}` (a searchset Bundle). A value with no system matches only if it's a valid NHS number; another system matches nothing; no identifier, or an invalid number with the NHS system, is a 400.
  - **Flagged records:** an anonymised patient is `active: false`, an `anonymous` name, year of birth only and no email, never its placeholders; a restricted or disputed one has a `meta.tag` from MedApp's record-flag code system (`FhirPatientMapper`).
  - **Hand-built DTOs, not the Firely SDK:** too big for three resource shapes. Revisit for more resources (CarePlan, Task) or profile validation.
  - **Return resources through `FhirResult`** (`FhirJson`): `application/fhir+json`, `Cache-Control: no-store`, no nulls or empty arrays (leave a list null, not empty).
  - **JSON only:** `FhirFormatFilter` answers a `_format` or `Accept` that doesn't allow JSON with a 406.
  - **Errors are OperationOutcomes, never ProblemDetails:** `FhirExceptionFilter` 400 `invalid` and 404 `not-found`, `FhirAuthorizationResultHandler` 401 `login` and 403 `forbidden`, `FhirFormatFilter` 406 `not-supported`, an unknown path under `/api/fhir` 404 `not-supported` (`FhirJson.NotFoundAsync`, before the generic `/api` 404). The forced password change's 403 stays ProblemDetails.
  - **Who:** the Staff policy, with the session cookie; no OAuth clients yet.
  - **Logged as the pages are:** a read records `PatientViewed`; a search `PatientsSearched` (filters `["nhsNumber"]`, or `["identifier"]` when it couldn't match an NHS number; the count; `via: "fhir"`) and `PatientListed` per patient found.
  - The number's verification status is always `02` ("Number present but not traced"): MedApp doesn't trace with PDS.
- **SNOMED CT codes on stage definitions** (`SnomedCode` and `SnomedTerm`, both or neither): `SnomedCt` (`MedApp.Domain/Terminology`), through `SnomedRules`, checks the format only (SCTID: 6 to 18 digits, no leading zero, partition `00` or `10`, Verhoeff check digit). No terminology server, so nothing checks the concept exists, is active or matches the term. Copied onto a procedure's stages at creation.
- **The demographics lookup (PDS)** is `IPersonalDemographicsService`, by `Pds:Mode`: `Off` (default, `NoPersonalDemographicsService`) or `Offline` (`OfflinePersonalDemographicsService`, four synthetic 999 people). `GET api/demographics/status` says whether there's one; `POST api/demographics/lookup` (`{ nhsNumber }` in the body; staff, `no-store`) is a 400 for an invalid number, 404 for nobody or no lookup. **The real PDS FHIR API isn't wired in:** it needs NHS England's onboarding (`docs/todo.md`).
- **Medical device decision:** all four only identify, label or move data unchanged, so it stands (`docs/clinical-safety/intended-purpose-and-device-decision.md`). Anything that interprets codes or chooses them from clinical data needs a new assessment.

### Migrations

- Migrations live in `MedApp.Infrastructure/Migrations/` with `MedAppDbContextModelSnapshot.cs`: one linear history from `InitialCreate`. Always add new ones on top; never edit or delete one that has been applied to a database.
- Name them in the past-tense commit style, e.g. `RemovedTemplateAuditColumns`, `RenamedTasksToProcedures`.
- **Always read the scaffolded migration before keeping it.** EF can't detect renames of entities or of properties used as keys, so it scaffolds them as `DropTable` + `CreateTable`, deleting the data. When `migrations add` warns "An operation was scaffolded that may result in the loss of data", rewrite `Up` and `Down` by hand: drop the keys and foreign keys whose names change, `RenameTable`, `RenameColumn` and `RenameIndex`, then re-add the keys and foreign keys with the same `onDelete`. `RenamedTasksToProcedures` is the reference.
- **A migration test reads only the columns that exist at its step:** project (`.Select(n => new { n.Id, ... })`) or use SQL, never whole entities (`ToListAsync()` on a `DbSet`), as a later migration's columns would be asked for ("Invalid column name"). Counting (`CountAsync`, `AnyAsync`) is fine.
- After editing a migration, `has-pending-model-changes` must report no changes, or the snapshot and model disagree.
- Test a non-trivial migration up **and** down on a throwaway LocalDB database, never the dev `MedApp` one: `dotnet ef database update --connection "Server=(localdb)\MSSQLLocalDB;Database=MedApp_MigrationTest;Trusted_Connection=True;TrustServerCertificate=True"`, seed synthetic rows, apply, roll back with `database update <PreviousMigration>`, then drop the database.
- Apply a migration to someone's local `MedApp` database only when asked, after a `medapp-dev` `save_snapshot`: it changes their data.

### Auth

- On login a `UserSession` row is created and its ID goes into an HttpOnly cookie. `SessionAuthenticationHandler` checks it on every request and builds claims, including roles and the session ID (`SessionIdClaim`, read with `User.GetSessionId()`).
- **Sessions last 8 hours** from sign-in (`UserSession.Lifetime`), extendable to 24 at most (`MaxLifetime`; over 20 hours, so WCAG 2.2.1 allows the hard limit). Only asking extends one, never using the app:
  - `GET api/auth/session` returns `SessionResponse` (`expiresAtUtc`, `expiresInSeconds` by the server's clock, `canExtend`) and never extends it. `POST api/auth/session/extend` ("Stay signed in") moves the expiry to 8 hours from now, never past 24 from sign-in nor earlier than it was, and resends the cookie with that expiry.
  - Both use the cookie's session, are `[Authorize]` with no role, `no-store`, and a 401 once it's ended. `SessionTimeout` in `MedApp.Web` uses them to warn.
  - **The forced password change page has no timeout warning,** deliberately: `/change-password` is outside the signed-in layout and `PasswordChangeRequired` doesn't allow the session endpoints, so it ends at normal expiry and its user signs in again.
  - A lockout doesn't end a session yet (risk S5).
- The UI is always on the API's origin (`wwwroot` in production, Vite's proxy in development), so there's no CORS and the cookie is `SameSite=Lax`.
- **The cookie's name:** outside Development `__Host-medapp.session` (`Secure`, `Path=/`, no `Domain`, so only this host over https can set it); in Development `medapp.session`, not `Secure` (Vite is plain http). Only `ISessionCookieService` knows the name (`CookieName`, `TryGetSessionCookie`); read the cookie through it.
- **Sessions end when the password changes:** handlers call `ISessionService.RevokeUserSessionsAsync` (through `UserSessions.EndSessionsAsync`) inside `IIdentityTransactions.RunAsync` with the change, so both are kept or neither.
  - Your own change ends your other sessions; an admin's reset ends all the user's. **A role change ends none:** roles are read on every request.
  - An admin can sign a user out everywhere: `POST api/auth/users/{username}/sign-out-everywhere` (204, 404 for unknown), the Users page's confirmed "Sign out everywhere".
  - A new account action that changes how someone signs in should end their sessions the same way, in the same transaction. An open live-notifications connection isn't closed when its session ends (`docs/todo.md`).
- **Sign-in throttling** (`MedApp.API/Common/RateLimiting`), in front of Identity's lockout, which stays: `POST api/auth/login` allows 30 attempts a minute per client address (IPv6 by /64; the `login` rate limiting policy) and 5 a minute per username from anywhere (the `[ThrottleLoginByUsername]` action filter, since the username is in the body), right or wrong. Over either: a 429 `ProblemDetails` ("Too many sign-in attempts. Try again in N seconds.") with `Retry-After`, before the password is checked. Behind Traefik the address comes from forwarded headers. Counts are per API copy, in memory. Settings: `LoginThrottling` (`LoginThrottlingOptions`).
  - **Only a name that could be an account's is counted or looked up** (`UsernameRules.CouldExist`: at most 256 characters of `User.AllowedUserNameCharacters`, trailing spaces ignored). Any other is the usual 401 with no lookup (`IdentityAuthenticationService`), as SQL Server's collation would match some (a leading byte order mark, a trailing NUL, full-width letters) to real accounts. Over 256 characters is a 400 (`LoginValidator`); a body over 16 KB a 413 (`[RequestSizeLimit]`). The per-username key is trimmed, NFKC-normalised and upper-cased (`LoginUsernameLimiter.Key`).
  - **Each refusal is logged** at Warning with the limit and the client's key, never the username (`LoginThrottling.LogRefusal`).
  - **Sizing:** a site behind one NAT address (an HSCN gateway) shares the per-address allowance; if staff are refused at a shift change, raise `LoginThrottling:PerClientPermitLimit` for that deployment (per API copy), keeping the per-username limit at the lockout count. `MedAppApiFactory` sets both out of reach; `LoginThrottlingTests` restores them.
- **Common passwords are refused** when a password is set (creating, changing, resetting), never at sign-in: `CommonPasswordValidator` (`MedApp.Infrastructure/Common/Identity`), the NCSC's 10,000 most used, from `Passwords/common-passwords.txt` (licence in `Passwords/NOTICE.txt`), ignoring case. Generated passwords never match (a test makes 500).
- **Security headers** (`MedApp.API/Middleware/SecurityHeaders.cs`, first in the pipeline) on every response: `nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`, `X-Frame-Options: DENY`, a `Permissions-Policy` turning off device features. Every `/api` response: `Cache-Control: no-store` and `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'`.
  - **Pages, outside Development, get a CSP with a new nonce per response:** scripts only from the origin or with the nonce, styles from the origin and inline, `connect-src 'self'`, `object-src 'none'`, `base-uri 'none'`, `form-action 'self'`, `frame-ancestors 'none'`. The nonce goes `SsrPages` → `X-MedApp-Csp-Nonce` → `server.mjs` (if base64) → `createSsrRouter` (`ssr.nonce`), onto every script and a `csp-nonce` meta tag. The fallback `index.html` has no inline scripts.
  - **Never add an inline script or `eval`:** the policy refuses them. A new external origin (fonts, images, an API) needs the policy changed, deliberately.
  - In Development non-API paths get only `frame-ancestors 'none'` (Vite, and Swagger UI's inline scripts). `SecurityHeaders:PageCspReportOnly=true` makes the page policy report-only: an emergency switch, no rebuild.
- A change to authentication, sessions, headers or a data flow should update `docs/security/threat-model.md` and the ASVS 5.0 Level 2 checklist, `docs/security/asvs.md`.
- **Roles** are `Admin` and `User`. Staff (either role) use `[Authorize(Policy = AuthPolicies.Staff)]`: patients, FHIR, demographics, procedures, attachments, calendars, meetings, appointments and their own calendar's setup. Admin-only is `[Authorize(Roles = "Admin")]`: users and roles, stage definitions, templates and activity types (staff read these), arranging meetings, the team view and suggested times, assigning, unlocking stages, deleting procedures, the audit and activity logs, Insights, a patient's export, restriction, dispute, anonymisation and retention review, and monitoring rules. Each area's section has the detail. There may be several admins; never assume one. `SelfUserOnly` limits a user to their own `{username}` route.
- Procedure handlers get the signed-in user as a `ProcedureActor` from `User.ToProcedureActor()` (`MedApp.API/Common/Authentication/ClaimsPrincipalExtensions.cs`).
- The app seeds no users (its only `HasData` is a default stage definition). **Locally, sign in as `admin` / `admin`:** `medapp-dev` makes it (`ensure_test_admin`, every `reset_demo_data`), bypassing the password rules, in local databases only.
- **A deployed database gets its first admin deliberately:** `dotnet MedApp.API.dll create-first-admin` (`FirstAdminSetup`) makes the Admin and User roles and an admin with both from `FirstAdmin:Username` and `FirstAdmin:Password`, only while there are no accounts, then exits. The chart's migrations Job runs it, with credentials from a Secret.
- **The server generates every new account's first password;** there's no default. `POST api/auth/users` takes only a username and returns the password once in `CreateUserResponse` (`IIdentityUserService.CreateUserAsync`, the reset's generator). The Users page shows it once (`OneTimePassword`, with copy) and forgets it when the dialog closes.
  - **It must be changed at first sign-in:** `IdentityUserService` sets `ApplicationUser.MustChangePassword` in the same save as a created or reset password; `UpdateUserPasswordAsync` clears it in the same update. A new password must differ from the current one (`UpdateUserValidator`).
  - **Enforcement:** `SessionService` reads the flag every request, `SessionAuthenticationHandler` makes it a claim (`SessionAuthenticationDefaults.MustChangePasswordClaim`), and `whoami` reports `mustChangePassword`. While set, `PasswordChangeRequired` (`MedApp.API/Common/Authentication`, between authentication and authorization) refuses every `/api` request, the hub included, with a 403 `ProblemDetails` (`code: "password_change_required"`), except every `AccountHold`'s allowlist (`GET api/auth/whoami`, `me/settings`, `features`; `POST api/auth/login`, `login/two-factor`, `logout`; the anonymous `password/forgot`, `password/reset`, `email/confirm`) and their own `PUT api/auth/users/{username}/update-password` (`AuthPolicies.IsOwnUsername`). Pages pass; the `_app` guard sends them to `/change-password`, and the API client sends any 403 with that `code` there with a full page load.
  - **Exempt:** the first admin, whose password is from the secret store (on kind, generated by `up.sh`), and every account `medapp-dev` makes or sets a password on (`create_user`, `set_password`, `ensure_test_admin`, `reset_demo_data`'s staff but its new starter).
  - **`CreateUserResult`** carries a password exactly when created (`Created(password)` or `Failed(result)`). It and the password responses never print the password in `ToString()`, and are sent `no-store`.
  - **Accounts created before #53** got the published `DefaultPassword1` and may still have it: an admin should reset them. Finding them is a to-do (`docs/todo.md`).
- Identity password and lockout rules are in `appsettings.json`'s `Identity` section.

### Email, password reset and two-factor authentication

All optional: an account without an address or two-factor signs in as before.
- **Sending email:** handlers call `IEmailSender.SendAsync(EmailTemplates.…)` (`MedApp.Application/Common/Email`) after saving. Best effort: `EmailQueue` queues in memory, and `EmailDispatcher` sends over SMTP (MailKit) in the background, notices and two-factor codes before links, logging failures by category, exception type and SMTP status only. Each copy sends at most `Email:MaxPerHour` (`EmailBudget`, 100; 8 on Azure), a fifth kept for notices and a fifth for codes (`EmailCategory.Code`), which links can't use; over it, messages are dropped and logged. A code is made only if `IEmailSender.HasRoomFor` it. Codes by email need a bigger allowance than Azure's managed domain. No retries; a message queued at shutdown is lost.
  - **Settings** (`Email`): `LinkBaseUrl` (configured, never from the Host header), `FromAddress`, `FromName`, `MaxPerHour`, `Smtp:{Host, Port, Security, Username, Password, TimeoutSeconds}` (`Security` defaults to `StartTls`; `None` only for a local catcher). No host and from address: nothing sent; no `LinkBaseUrl`: no links. `EmailAvailability.CanSendLinks` reaches the web app via `GET api/auth/features`, which hides "Forgot your password?" and email fields without it.
  - **Mailpit** (`axllent/mailpit`) catches it under the AppHost (dashboard's **Inbox** link) and on kind (`mailpit.enabled`); Azure uses Azure Communication Services' SMTP relay (`deploy/azure/README.md`).
  - **Templates** (`EmailTemplates`): plain, accessible HTML and the same words as text, every value HTML-encoded. **Never put clinical text in an email** (no patient, procedure or notification text) **and never a password, an authenticator key or recovery codes.** The only code allowed is a single-use two-factor code (`EmailTemplates.TwoFactorCode`), never in the subject, with only its purpose, lifetime and what to do if unasked (no username or link). A new account's welcome is only its confirmation link.
  - **Security notices** (`ISecurityNotices`) go to the confirmed (or, on a change, old) address on a reset by link, two-factor changes (either method), recovery codes used or replaced, address changes or removal, and code lockouts. `SendToOtherAdminsAsync` tells the other admins when an admin's two-factor changes.
- **Staff email addresses** are Identity's `Email` and `EmailConfirmed`; a new one waits in `ApplicationUser.PendingEmail` until confirmed, the old one in use meanwhile. Only a confirmed address is used or returned (`UserResponse.Email`; `PendingEmail` separately). Accounts may share one, but each should have its own, not a team mailbox.
  - Admins give one at creation (`CreateUserRequest.Email`) or remove any (`DELETE api/auth/users/{username}/email`; the address is told). People manage their own (`GET`/`PUT`/`DELETE api/auth/me/email`) with their password (`PasswordConfirmation`: wrong is a 400 on `Password`, counting towards the lockout), at most 3 changes in 15 minutes (`AccountAttemptLimiter`, 429). Only the bare address is stored (`EmailAddressRules.Normalize`: display names dropped, lists and line breaks refused).
  - **Confirming:** `/confirm-email` posts to `POST api/auth/email/confirm` **signed in as the link's account**: signed out is a 401 (the page signs them in, then confirms), another account a 400 `WrongAccount`. Every hold allows it. The token is Identity's change-email token (address and security stamp bound, 24 hours).
  - **With codes by email on,** the address can't be removed (409, `RemoveMyEmailHandler.SignsInWithEmailCodes`, `RemoveUserEmailHandler.SignsInWithEmailCodes`); a new address gets codes only once confirmed.
- **Emailed links carry their token in the fragment** (`/reset-password#user=…&token=…`, `EmailLinks`), never sent to servers, so out of logs and the Referer header.
- **Forgot your password?** `POST api/auth/password/forgot` (username or address) always answers 202, emailing a reset link to that username's account and every account with it as confirmed address. Throttled per address (`login` policy) and per username or address (`ThrottleResetRequestsAttribute`: 3 in 15 minutes, `LoginThrottling:ResetRequests…`). `POST api/auth/password/reset` takes the link's user and token (`PasswordResetTokenProvider`, 30 minutes, single-use) and the new password; any bad link or unknown user is the same 400 `InvalidToken`, and the password must differ from the current (`SamePassword`). Success, in one transaction, ends every session, clears must-change-password and any lockout, and notifies the owner. Two-factor stays on (with codes by email, link and code reach one mailbox: threat model T-52).
- **Data protection keys** (sealing every Identity token) are in the database (`DataProtectionKeys`, `PersistKeysToDbContext`), shared by every copy; on Azure, `DataProtection:KeyVaultKeyUri` wraps them with a Key Vault key. Unwrapped, a database copy could forge reset links, so the API warns at startup outside Development (`DataProtectionKeyCheck`), as does the chart's `NOTES.txt`. The application name is fixed (`MedApp`). Integration tests keep the table across resets.
- **Two-factor authentication** has one method per account (`ApplicationUser.TwoFactorMethod`, stored by number, so never renumber `TwoFactorMethod`: `None = 0`, `Authenticator = 1`, `Email = 2`), read through `EffectiveTwoFactorMethod` or `ITwoFactorService.GetMethodAsync` (on with no method counts as the app). Switching is off then on, except codes by email to the app, which `POST …/authenticator` and `…/enable` allow with the current factor, so it never goes off.
  - **Rollouts:** a rollback and forward may leave an app user reading as `Email` (`TwoFactorEnabled = 1`, `TwoFactorMethod = 2`): reset their two-factor if they say so (a switch in progress looks the same). During the rollout adding codes by email (`AddedEmailTwoFactorCodes`), old copies ignore the method (asking for an app code, and changing or removing the address with the password alone), so nobody turns it on until it's done (the chart's README); then reset accounts with `TwoFactorMethod = 2` and no confirmed address.
  - **The authenticator app** (TOTP, the stronger choice) uses Identity for the key, recovery codes and `TwoFactorEnabled`, but checks codes itself (`Totp`, `TwoFactorService`): by `TimeProvider`, one 30-second step either side, each code once (last step kept as user token `[MedApp]`/`AuthenticatorLastTimeStep`).
  - **Setting up the app:** `POST api/auth/me/two-factor/authenticator` gives a key as text and a server-drawn QR code (`QrCodeRenderer`, Net.Codecrete.QrCodeGenerator) as one SVG path. `…/enable` (password and code) returns ten recovery codes once and ends other sessions; `…/recovery-codes` (password and code) replaces them; `…/disable` takes the password and a code or recovery code. Wrong codes count towards the lockout and code limit; these are limited per address like sign-in. Keys and codes are sent `no-store` and never printed.
  - **Codes by email** (`IEmailCodeService`, `EmailCode`, `EmailCodes`) are the weaker choice: whoever gets into the mailbox gets the code (NIST SP 800-63B rejects email). A code: 6 digits (`RandomNumberGenerator`), 10 minutes (`EmailCode.Lifetime`), single-use, stored only as a SHA-256 hash bound to its row, compared in constant time, sent only to the confirmed address, never logged.
    - **One live code per purpose** (`EmailCodePurpose`): `SignIn` (ends with its challenge, which then lasts 10 minutes, `SignInChallenge.EmailLifetime`), `TurnOn` (bound to its address), `Confirm` (turning off, recovery codes, switching to the app, moving the address). A new one replaces the last (`IX_EmailCodes_UserId_Purpose`, outside sign-in, stops two first sends both standing); one out of tries isn't live.
    - **Checking:** `TwoFactorCodes.Normalise` strips all whitespace; `EmailCodeService.CheckAsync` answers `Right`, `Wrong` (counted) or `NotLive` (not counted). `TurnOn` and `Confirm` codes allow five tries (`TryReserveAttemptAsync`); sign-in codes use their challenge's. Resent at most once a minute, three times (`EmailCode.ResendInterval`, `MaxResends`, `TryResendAsync`, one SQL statement), and 10 per account in 15 minutes (`AccountAttempt.EmailCode`, `LoginThrottling:EmailCodesPerAccount…`); over either, a 429 with `Retry-After`.
    - **Turning on:** `POST api/auth/me/two-factor/email` with the password (409 if two-factor is on, no confirmed address, or no email) sends a code (202 `EmailCodeSentResponse`: the address masked by `EmailAddressMask`, and when it can resend). `POST …/email/enable` with it turns on `Email`, returns ten recovery codes once, ends other sessions and notifies (`EmailCodesEnabled`; other admins for an admin). Wrong codes here don't count towards the lockout.
    - **While on:** `POST …/email/code` with the password (wrong: 400 on `Password`, counted) sends a `Confirm` code, taken as `code` by `…/disable` and `…/recovery-codes` (`TwoFactorCodes.VerifyOrThrowAsync`), as `currentCode` by `…/enable` (moving to the app) and as `code` by `PUT me/email`, so a session and password can't swap the second factor. A switch notifies the owner and other admins (`TwoFactorSwitchedToApp`, `AdminTwoFactorSwitchedToApp`). A held admin may call it, to switch. `GET …/me/two-factor` gives `method`, `canUseEmailCodes` and `emailCodesUnavailableReason` (`NoConfirmedAddress`, `EmailUnavailable`).
  - **Signing in:** `POST api/auth/login` answers **202** with a `TwoFactorChallengeResponse` (`method`, and `emailCode`: the masked address, or null if none went), no session or cookie, emailing the code for codes by email. `POST api/auth/login/two-factor/resend` resends (401 `sign_in_expired`, 409 without codes by email, 429); every hold allows it. The challenge (`SignInChallenge`): 256 random bits, stored only as a SHA-256 hash, bound to the user, 5 minutes, 5 codes, deleted when used. `POST api/auth/login/two-factor` (challenge and a code or recovery code) makes the session. A wrong code, emailed or not, is a 401, counting towards the lockout, which only a complete sign-in clears. An unusable challenge is a 401 `code: "sign_in_expired"`. A recovery code sign-in emails the owner. The forced password change comes after.
  - **Parallel codes get no extra checks:** each attempt is **reserved in one SQL statement before the code is checked** (`TryReserveAttemptAsync`: `WHERE FailedAttempts < 5 AND ExpiresAtUtc > now`; none left is the 401 `sign_in_expired`), and failures counted in one statement (`IAccountLockoutRepository`), never Identity's `AccessFailedAsync` (after a failed Identity update, `Forget` the user). At most 10 code checks per account in 15 minutes (`AccountAttemptLimiter`, `LoginThrottling:TwoFactorCodesPerAccount…`, 429, per copy). Code lockouts last 5 minutes, doubling (`CodeLockouts`, `ApplicationUser.TwoFactorLockouts`, up to a day) until a complete sign-in, and email the holder. Keep `ParallelCodeTests` (30 codes of each kind, and 30 resends, at once) passing.
  - **Admins:** `POST api/auth/users/{username}/reset-two-factor` turns it off, forgets key and codes and ends their sessions (not your own: 400 `OwnTwoFactor`). **Require two-factor authentication for admin accounts** (`/admin/settings`, `GET`/`PUT api/auth/security-settings`, one `SecuritySettings` row) needs the app (codes by email don't count), and only an admin using it can turn it on. While on, an admin without the app is held by **`TwoFactorSetupRequired`**, an `AccountHold` after `PasswordChangeRequired`: a 403 `code: "two_factor_setup_required"` except for setting up (and the hold allowlist), sent to `/set-up-two-factor`; `whoami` reports `mustSetUpTwoFactor`. They can't turn it off while required. `SessionService` reads the setting only for admins without the app, whom Admin Settings' `adminsWithoutTwoFactor` counts.
  - **A new hold** derives from `AccountHold` (allowlist, title, `code`), goes after `PasswordChangeRequired`, and needs a guard in the `_app` route and `src/api/client.ts`.
- **Security events** (two-factor, resets, address changes, recovery codes used, the admin requirement) go through `ISecurityEventLog` (`MedApp.Application/Common/Security`) to the activity log (`ActivitySecurityEventLog`, `ActivityAction` 28 to 41 via `ActionFor`; `EmailCodesEnabled` 40, `EmailCodesDisabled` 41; a switch to the app is `TwoFactorEnabled` with `switchedFrom: "email"`) and the application log (`LoggingSecurityEventLog`). The actor is the owner, the admin who acted, or nobody for a signed-out request (with its address); the account is the target. Never put an email address, password, token or code in an event.
- **Two-factor sign-ins in the activity log:** a right password records `TwoFactorChallenged` (`method: "email"` if a code was emailed); the second step `SignedIn` (`secondStep`: `authenticator`, `email` or `recoveryCode`) or `SignInFailed` (`reason`: `InvalidCode`, `InvalidRecoveryCode`, `CodeNotLive` (no emailed code could work; not a counted guess), `LockedOut`, `ChallengeExpired`, `TooManyCodes`, `InvalidCodeLockedOut` or `InvalidRecoveryCodeLockedOut`), by the challenge's account. Never the code or the address.

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

## Local dev gotchas

- **Docker Desktop must be running.** The dev database is SQL Server 2022 in a container the AppHost starts (volume `medapp-sql-data`, host port `14330` on `127.0.0.1`).
  - It's **persistent**: it keeps running after the AppHost stops, so `dotnet ef` and `medapp-dev` still reach it. `docker stop` it to free memory; the next AppHost run starts it again.
  - **Attached files** are in Azurite, another persistent container (volume `medapp-azurite-data`, blob endpoint on host port `14331` on `127.0.0.1`, which `reset_demo_data` uses with Azurite's well-known development account, `devmcp.json`; without it the reset skips files and says so). `medapp-dev`'s snapshots and resets cover only the database, so after restoring an older snapshot some files may 404 and some blobs be orphaned.
  - Aspire generated the `sa` password on the first run and keeps it in the AppHost's user secrets (`Parameters:sql-password`). The volume's database accepts only that password, so don't delete or change it.
- **Keeping memory down.** Docker Desktop runs containers in a WSL 2 virtual machine, which by default may take half the PC's memory and keeps what it used as cache.
  - **The AppHost is the light setup:** SQL Server (capped at 2 GB with `MSSQL_MEMORY_LIMIT_MB`, else up to 80% of memory; the integration tests' too), Azurite and Mailpit in Docker; the API, Vite and the dashboard as ordinary processes.
  - **The kind cluster is the full production-like setup** and much the heaviest, even idle. Start it only to test the chart or a deploy, and stop it afterwards (`deploy/kind/README.md`, Stopping and starting).
  - **Most changes need no containers:** builds, unit tests and Vitest run without Docker, and the integration tests start and remove their own.
  - **A cap for WSL itself** is per PC, not in the repo: `%UserProfile%\.wslconfig` with `[wsl2] memory=12GB` (for a PC with more than 24 GB; below that, the default half is lower), or 16 GB to run kind and the AppHost together. Current WSL gives cached memory back when idle by default (`[experimental] autoMemoryReclaim=dropCache`). To apply it, quit Docker Desktop, run `wsl --shutdown`, then start Docker Desktop (shutting WSL down under Docker kills its containers mid-run).
- The API's `appsettings*.json` have **no connection string**: the AppHost injects `ConnectionStrings__DefaultConnection`, which running the API alone needs.
- **An empty database** (a new volume) isn't migrated at startup: apply the migrations ([Commands](#commands)), then run `medapp-dev`'s `reset_demo_data`.
- The React dev server (`npm run dev`, Vite rendering on the server) proxies `/api` and `/swagger` to the API, its own server-side calls included. If `/api` calls return the app's HTML instead of JSON, the proxy has dropped out: restart the AppHost.
- **Server-side rendering in production** (`MedApp.API/ServerRendering`): the page server (`MedApp.Web/server/server.mjs`) is in the image at `/app/ssr/` with its bundle and a copy of the client manifest, outside `wwwroot`, so no static file path reaches it. It runs one of two ways:
  - **In Kubernetes, its own container** (the chart's `pages`, `api.pageServer`): the same image running `node /app/ssr/server.mjs` in the API's pod, as its own non-root user, read-only, no capabilities, its own resource limits. It gets none of the API's secrets or environment, only `HOST=127.0.0.1`, `PORT=8082`, `HEALTH_PORT=8083` and `MEDAPP_API_URL=http://127.0.0.1:8080`. The API's `Ssr:ServerUrl` (`http://127.0.0.1:8082`) points at it; the API doesn't start Node.
    - **Supervised:** its first process runs the page server in a worker and restarts it (after 1 s, doubling to 30 s while it keeps failing soon after starting) when it exits, is killed for memory, or misses its heartbeat for 15 s. So the pod stays in service while pages fall back for a moment.
    - **Probes:** liveness only (`GET /alive` on 8083, answered by the supervisor, reachable only by the kubelet). Deliberately no readiness or startup probe: either would take the API's pod out of service with it.
    - Logs: `kubectl logs deploy/medapp-api -c pages`. It needs no renderer key (below): the API never redirects requests received on a loopback address.
  - **Anywhere else** (`docker run`, CI's `e2e` job), with no `Ssr:ServerUrl`: the API starts it with `node` on a free local port from `Ssr:ScriptPath` (`ssr/server.mjs`, relative to `/app`), and never restarts it. Node gets only allowlisted environment variables (`SsrServer.StartInfo`), so the API's secrets can't leak from it by accident; that isn't isolation (same user and container, so `/proc` still exposes them). Its API calls carry a key the API gave it (`SsrServer.IsFromRenderer`).
  - **Either way:**
    - The API forwards the path and query only (the host is always Node's), with the CSP nonce in `X-MedApp-Csp-Nonce`. It falls back to `index.html` if Node is missing, not ready or stopped, or the render fails or answers 5xx, as long as nothing has been sent; a page failing part-way is cut short.
    - The `ssr` HttpClient has no resilience handler, so a render is never retried. `Ssr:RenderTimeout` covers only the status and headers, besides `HttpClient`'s default 100 s; nothing times out a page still streaming, so a stalled render holds the request until the browser gives up.
    - Rendered pages hold the user's data, so they're `Cache-Control: no-store`.
    - Paths with an empty segment (`//`) are refused (`PageServerFiles`), and, as defence in depth, `/ssr` and `/.vite`.
    - No https redirect for loopback requests, so `ASPNETCORE_HTTPS_PORT` (in `api.env`) can't break the page server's data calls.
    - Settings: the `Ssr` section (`SsrOptions`). A `Ssr:ServerUrl` that isn't a bare http server address stops startup.
- **The production image** is `MedApp.API/Dockerfile`, built from the repo root (`docker build -f MedApp.API/Dockerfile -t medapp-api .`): the React app built with Node and the API with the .NET SDK (SkiaSharp's native library from its NuGet package), on the `aspnet` runtime with Node's binary, non-root, port 8080 (TLS ends in front). The app is in `/app/wwwroot`, the page server in `/app/ssr` (`dist/ssr` and the client manifest, not `dist/.vite`). The root `.dockerignore` keeps Windows `node_modules`, build output and local secrets out. If you change which projects the API references, update the Dockerfile's `COPY` lines.
- Kestrel's `https` profiles need the dev certificate: if startup says "the default developer certificate could not be found", run `dotnet dev-certs https`.
- **Session cookies** are as in [Auth](#auth). Deploying the `__Host-` rename signs everyone out once, as the API no longer reads the old `medapp.session` cookie.

## Testing conventions

- The stack is NUnit 3, FluentAssertions 8 (its licence banner is expected), Moq and AutoFixture with AutoMoq.
- The test tree mirrors the source (`MedApp.UnitTests/Application/<Area>/Commands/<Action>/<Action>HandlerTests.cs` and `...ValidatorTests.cs`), except `Authentication` is `Auth` and `Sessions` is `Session`.
- Fixtures are `[TestFixture] public sealed class`, set up in `[SetUp]`: `AutoFixtureFactory.Create()`, `_fixture.Inject(mock.Object)`, then `_fixture.Create<Handler>()`.
- Names follow `Method_Scenario_ExpectedResult` (`Handle_PatientDoesNotExist_ReturnsNull`).
- Shared test data goes in `MedApp.UnitTests/Common/Constants/*TestConstants.cs`, not inlined magic values.
- **Synthetic data only:** never realistic patient PII in tests, seeds or fixtures. **NHS numbers come only from the 999 range** (`999 xxx xxxx`, valid check digit), never issued to real people; never one outside it, even made up, as it could be someone's.
- Procedure handler tests derive from `ProcedureHandlerTestBase` (mocked repository and username lookup, fixed clock) and build procedures with `ProceduresTestConstants.CreateProcedure`.
- Handlers that write the activity log get a `RecordingActivityLog` (`_fixture.Inject<IActivityLog>(...)`), which makes entries as the real one does and keeps them to check.

**Integration tests** (`MedApp.IntegrationTests`) test through HTTP what unit tests can't: SQL translation, Identity stores and transactions, the authentication handler and policies, the middleware.
- `TestEnvironment` (a `[SetUpFixture]`) starts SQL Server and Azurite (the chart's image, `--skipApiVersionCheck`) once per run, migrates and starts the API. Blobs aren't emptied (every file has a new ID). Before each test, `IntegrationTest` empties the database with Respawn (keeping the migrations history, seeded stage definition and activity types, and data protection keys), then makes the first admin with `FirstAdminSetup`.
- Work through the API where you can, with `IntegrationTest`'s helpers (`SignInAsAdminAsync`, `CreateStaffAsync`, `CreatePatientWithProcedureAsync`, ...); `RunInScopeAsync` for a service; `ExecuteSqlAsync` only for what the API can't do (expiring a session).
- **Generated passwords:** an account an admin made must change its password first, or every other `/api` call is a 403 `password_change_required`. `CreateStaffAsync` does when the account has a role; one without can't (that needs the Staff policy) and stays held. To test the Staff policy, give a role, change the password, then remove the role (`AuthorisationTests.StaffEndpoint_RolesRemoved_*`). `Auth/PasswordChangeRequiredTests` tests the hold, with `CreateForcedUserAsync`.
- Constants: `Common/Constants/IntegrationTestConstants.cs`.
- **Emails and the clock:** `IEmailSender` is a `FakeEmailSender` (`Api.Emails`) keeping every message; `TimeProvider` is a `TestClock` (`Api.Clock`, or `Clock`) that tests move (`Advance`) or set (`SetUtcNow`, `MoveClockPast`). Both reset before each test. `AccountSecurity` has helpers to confirm an emailed address, turn two-factor on, make the next code and sign in in two steps. `SmtpEmailTransportTests` sends through the real transport to Mailpit in Docker.

**End-to-end tests** (`MedApp.E2ETests`, deriving from `E2ETest`) drive the React app in Chromium, against the AppHost locally and the production image in CI.
- Open pages with `OpenAsync`, which waits until React has taken over the server-rendered page; input before then is lost.
- Find elements by `GetByRole` and `GetByLabel`, with accessible names.
- Make each journey's data through the API (`Api`), with unique synthetic names (`E2EData.UniqueName`), registering every clean-up with `Data.OnCleanUp`. Rely on nothing you didn't make except the signed-in account (which must be an admin) and the default stage definition.
- Passwords for accounts a test makes come from `E2EData.NewPassword()`, new each run, never written in the source; keep passwords out of messages.
- Missing `E2E_USERNAME` or `E2E_PASSWORD` fails the suite in CI (`CI=true`) and skips it locally. With `E2E_TRACE_DIR` set (only `main.yml` does), failed tests' Playwright traces go there.

## CI

`.github/workflows/main.yml` runs on PRs to `main`, with nine jobs. `web`, `helm`, `unit-tests`, `image`, `integration-tests` and `e2e` are required checks on `main`.
- `changes` skips `web`, `image`, `e2e`, `helm`, `integration-tests` and `unit-tests` (skipped by `if` counts as passing; the run takes under a minute) for a push that changes only `docs/`, on a commit whose run passed, with nothing merged into the base since that run started: that run already tested the same merge, and nothing built or tested reads `docs/`. It runs them all for:
  - a new or reopened PR, or a force push
  - a previous run that failed, was cancelled or hasn't finished
  - anything merged into the base since that run started (the commits API, by date)
  - any change outside `docs/`, a merge from `main` included (a move in or out of `docs/` counts by both names)
  - a push of 300 files or more (the comparison lists only that many)
  - `changes` failing (the jobs run unless it says `false`)

  It compares the push, not the whole PR (which `paths-ignore` can't), and the run's summary says why. Never put a file that a build or test reads under `docs/`.
- `web`: `npm ci`, `npm audit --audit-level=high` (high or critical fails, except IDs in `ACCEPTED`: see the supply chain rules), lint, type check, a check the built theme is current, Vitest with coverage, and `npm run build` (client, server rendering bundle, page server).
- `image` builds `MedApp.API/Dockerfile` as Publish does, without pushing, sharing Publish's build cache. Trivy then scans it: findings with a fix go to code scanning (SARIF), and a critical one with a fix fails the job. Trivy runs from its own image pinned by digest (`TRIVY_IMAGE`), never `aquasecurity/trivy-action`, whose tags were hijacked in March 2026.
- `dependency-review` fails, and comments on, a PR adding or updating a dependency (npm, NuGet, actions) with a known high or critical advisory.
- `helm` lints and renders the chart (`deploy/helm/medapp`) in its main configurations (own SQL Server, kind's values with External Secrets and TLS, an external database, Azure's), and compiles and lints the Bicep (`deploy/azure`). It also renders Traefik, cert-manager, External Secrets and Argo CD at `deploy/versions.sh`'s versions with every values file and `--set` that `deploy/kind/up.sh` and `deploy/azure/bootstrap.sh` use: after changing one of those values files, run the same `helm template` locally.
- `unit-tests` builds `tools/MedApp.DevMcp` (not in `MedApp.sln`), then runs `dotnet test MedApp.UnitTests` in Release with coverage.
- `integration-tests` runs `dotnet test MedApp.IntegrationTests` in Release, with Testcontainers in the runner's Docker.
- `e2e` tests the production image under the chart's restrictions (read-only root with a writable `/tmp`, all capabilities dropped, no privilege escalation; one copy, no Valkey). It builds the image from earlier runs' cache (on a cold cache both it and `image` build from scratch), starts SQL Server on the runner's loopback with a random masked password (no file storage: attachments answer 503, and no E2E test uploads), migrates with `/app/efbundle`, runs `create-first-admin` with a generated password, starts the API on http://localhost:8080 and runs `MedApp.E2ETests`. Failed tests' traces are the `e2e-traces` artifact. The cookie stays `Secure` (browsers keep it from `http://localhost`). `image` and `e2e` have `permissions: contents: read` only.
- `coverage-comment` posts one sticky PR comment with the .NET classes and React files under 80% line coverage.

**Coverage** aims at everything with behaviour worth testing:
- **.NET** counts handlers, validators, domain logic, Infrastructure services and the API (controllers, middleware, authentication, server rendering). Excluded: commands, queries, DTOs, repositories, DbContext, migrations, generated code (`obj/`), the Aspire service defaults, wiring (`ServiceExtensions`, `ApplicationBuilderExtensions`, `Program`), and `SessionService` and `IdentityTransactions` (covered by integration tests, uncounted).
- **React** counts `src/` except generated code, the theme build, test helpers, thin route files and the browser entry. `npm run coverage` fails below `vite.config.ts`'s thresholds, just under today's figures (99% lines, statements and functions, 92% branches).
- New code comes with tests. Leave a branch uncovered only when it's unreachable or needs a real browser or database, and say so in the PR.

`.github/workflows/codeql.yml` runs CodeQL (C#, JavaScript and TypeScript, and the workflows) on PRs to `main`, pushes to `main` and weekly, into code scanning alerts.

`.github/dependabot.yml` proposes weekly grouped updates (minor and patch together) for GitHub Actions, NuGet, npm (`MedApp.Web`, `tools/ui-parity`) and Docker. Bump the Dockerfile's `ARG` image tags and `DOTNET_EF_VERSION` by hand: it can't read them.

**Supply chain rules:**
- **Pin every third-party action to a full commit SHA,** its tag in a comment (`uses: actions/checkout@<sha> # v4.4.0`). Resolve the SHA from the tag yourself (`gh api repos/<owner>/<repo>/git/ref/tags/<tag>`, dereferencing an annotated tag), never from a README. Dependabot keeps them current.
- **Give each job only the permissions it needs,** at the job, not the workflow.
- **NuGet audit:** every csproj treats NU1903 (high) and NU1904 (critical) as errors, transitive packages too, so a new advisory fails the build until the package is updated. There's no `Directory.Build.props`, because the Dockerfile copies only the csproj files before restoring.
- **When a gate fails overnight** (an advisory with no fix yet, or a base image Microsoft hasn't rebuilt), don't switch the gate off. Accept that one issue for a short, dated time, in the same PR as a `docs/todo.md` entry it expires with:
  - **NuGet:** `<NuGetAuditSuppress Include="https://github.com/advisories/GHSA-…" />` in an `ItemGroup` of only the affected csproj, with a comment giving the reason, the date added and the review date (at most 30 days).
  - **npm:** an `overrides` entry in `MedApp.Web/package.json` forcing a fixed version of the transitive package, if one exists. If not, add the advisory's GitHub ID to the `web` job's `ACCEPTED` (space-separated), with a comment on what it is and why it's safe to wait, and set `ACCEPTED_UNTIL` at most 30 days on. Only those IDs pass, and after that date the audit counts them again by itself.
  - **Trivy:** a root `.trivyignore`, one line per CVE with an expiry (`CVE-2026-12345 exp:2026-12-31`) and the reason in a comment above, passed to both Trivy runs (`--ignorefile /out/.trivyignore`). It stops being ignored on that date.

`.github/workflows/e2e.yml` runs the E2E suite by hand against a running app at a URL you give, as an admin from the `E2E_USERNAME` and `E2E_PASSWORD` repository secrets. **Those secrets don't exist yet: create them for a known deployment first.** Without them the run fails. It saves no traces, as they record the real credentials.

`.github/workflows/publish.yml` runs on every push to `main` except docs-only changes:
1. `image` pushes the image to GHCR (`ghcr.io/aliator/medapp-api:<commit>`), makes an SPDX SBOM (`anchore/sbom-action`, also the `medapp-api-sbom` artifact) and attests the build provenance and the SBOM to GHCR (`actions/attest`). It's the only job with `id-token: write` and `attestations: write`. Verify with `gh attestation verify oci://ghcr.io/aliator/medapp-api:<commit> --repo Aliator/MedApp`; nothing checks attestations before a deploy yet.
2. `deploy-kind` commits to `env/kind` exactly `main`'s files plus the tag in `deploy/argocd/kind-image.yaml`, except `.github/workflows`, which keeps the branch's own (`.github/scripts/deploy-to-env.sh`); Argo CD deploys that branch (`deploy/argocd/README.md`). `azure` does the same for `env/azure` once Azure is set up. Each needs only `image`, so one failing doesn't stop the other. Run by hand on another branch, it pushes the image but deploys nothing. Re-running an older run doesn't move a branch back.

After a merge, its run is the deploy: check it as you check CI.

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
   - **If it fails, stop.** Don't merge. Report it and propose a fix, as in step 4 of the CI section.
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
