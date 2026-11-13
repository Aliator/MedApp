# AGENTS.md

Guidance for AI coding agents working in this repository.

## Overview

MedApp is a patient and procedure management app. It has a .NET 8 Web API backend built with Clean Architecture, CQRS and MediatR, and a Blazor WebAssembly frontend. Data is stored in SQL Server through EF Core, and ASP.NET Core Identity handles users. Authentication is a custom cookie-based session scheme.

## Commands

Run everything from the repo root. `global.json` pins the SDK to 8.0.x.

```bash
dotnet build MedApp.sln                     # build everything
dotnet test MedApp.UnitTests                # the only project with tests (CI runs this)
dotnet test MedApp.UnitTests --filter "FullyQualifiedName~UpdatePatient"   # run a subset

dotnet run --project MedApp.API --launch-profile https       # API + Swagger at /swagger
dotnet run --project MedApp.Client --launch-profile https    # Blazor client at https://localhost:7292

# EF Core migrations (dotnet-ef 8.x is installed as a global tool)
dotnet ef migrations add <Name> --project MedApp.Infrastructure --startup-project MedApp.API
dotnet ef database update      --project MedApp.Infrastructure --startup-project MedApp.API
dotnet ef migrations has-pending-model-changes --project MedApp.Infrastructure --startup-project MedApp.API   # model and snapshot in sync?
```

See [Migrations](#migrations) before adding or applying one.

Before you call a change done, `dotnet build MedApp.sln` must succeed with no new warnings and `dotnet test MedApp.UnitTests` must pass.

## Solution layout

Dependencies point inward: `API → Application → Domain` and `Infrastructure → Application`.

| Project | Responsibility |
|---|---|
| `MedApp.Domain` | Entities and enums (`Patient`, `PatientProcedure`, `PatientProcedureStage*`, `UserSession`). No dependencies. |
| `MedApp.Contracts` | Request/response DTOs, shared by the API and the Blazor client. |
| `MedApp.Application` | Commands, queries, handlers and validators (MediatR + FluentValidation). Also repository and service **interfaces** and app exceptions. |
| `MedApp.Infrastructure` | `MedAppDbContext`, EF configurations, repository implementations, Identity and session services, and migrations. |
| `MedApp.API` | Thin controllers, the session auth handler, exception middleware and CORS. |
| `MedApp.Client` | Blazor WASM UI. It references only `Contracts` and talks to the API over `HttpClient`. |
| `MedApp.UnitTests` | NUnit tests for Application handlers/validators and Infrastructure services. |
| `MedApp.IntegrationTests`, `MedApp.E2ETests` | Empty project shells, with no tests yet. |

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
- **Preferred style for new code:** throw `NotFoundException` (becomes 404) or `ConflictException` (becomes 409) from handlers. The Procedures area follows this.
- The Patients and Auth slices predate that and return `null`/`bool`/`IdentityResult`, leaving controllers to choose the status code. Keep that existing style when you edit those slices, unless the task is to migrate them.
- Unhandled exceptions are not caught by the middleware, so they surface as 500s.

### Mapping

Handlers map Domain entities to `Contracts` responses by hand, with inline `new XResponse(...)` / object initialisers. There is no mapping library on `main`.

### Data access

- Each repository method calls `SaveChangesAsync` itself, so there is no unit of work.
- Read methods use `AsNoTracking()`, and updates go through `context.X.Update(entity)`.
- Pass `CancellationToken` all the way through. The tests check that it is passed on.
- IDs are `Guid`s generated in handlers (`Guid.NewGuid()`). Timestamps are UTC (`CreatedAt`, `LastUpdated`), set in handlers.

### Migrations

- Migrations live in `MedApp.Infrastructure/Migrations/` with `MedAppDbContextModelSnapshot.cs`. They are a single linear history starting at `InitialCreate`. Always add a new migration on top, and never edit or delete one that has been applied to a database.
- Name migrations in the same past-tense style as commits, for example `RemovedTemplateAuditColumns` or `RenamedTasksToProcedures`.
- **Always read the scaffolded migration before keeping it.** EF can't detect renames of entities or of properties used as keys, so it scaffolds a rename as `DropTable` + `CreateTable`, which deletes the data. When `migrations add` warns that "An operation was scaffolded that may result in the loss of data", rewrite `Up` and `Down` by hand. Drop the keys and foreign keys whose names change, then call `RenameTable`, `RenameColumn` and `RenameIndex`, then re-add the keys and foreign keys with the same `onDelete` behaviour. `RenamedTasksToProcedures` is the reference example.
- After editing a migration, `has-pending-model-changes` must report no changes. Otherwise the snapshot and the model disagree.
- Test a non-trivial migration up **and** down against a throwaway LocalDB database, not the dev `MedApp` database. Pass `--connection "Server=(localdb)\MSSQLLocalDB;Database=MedApp_MigrationTest;Trusted_Connection=True;TrustServerCertificate=True"` to `dotnet ef database update`, seed synthetic rows, apply the migration, roll back with `database update <PreviousMigration>`, then drop the database.
- Applying a migration to someone's local `MedApp` database changes their data, so do it only when asked, and take a `BACKUP DATABASE ... WITH COPY_ONLY` first.

### Auth

- On login, a `UserSession` row is created and its ID goes into an HttpOnly cookie. `SessionAuthenticationHandler` checks the cookie on every request and builds claims, including roles.
- The roles are `Admin` and `User`. Most endpoints are `[Authorize(Roles = "Admin")]`. The `SelfUserOnly` policy limits a user to their own `{username}` route.
- No admin user is seeded. The only `HasData` seed is a default stage definition.
- Identity password and lockout rules live under the `Identity` section of `appsettings.json`.

## Frontend conventions (MedApp.Client)

- Every component and page is split into three files: `X.razor` (markup), `X.razor.cs` (`partial class` code-behind, using `[Inject]` properties and `[Parameter]`s) and `X.razor.css` (scoped CSS). Keep that split.
- Reusable building blocks live under `Components/`:
  - Pages: `Page`, `PageHeader`
  - Forms: `FormCard`, `FormSection`, `FormGroup`, `DatePickerFormGroup`
  - Tables: `TableCard`, `DataTable`, `SortableHeader`
  - Modals: `Modal`, `ConfirmModal`, `SearchModal`, `SelectionModal`
  - Views: `ViewCard`
  - Shared: `AlertMessage`, `LoadingState`, `EmptyState`, `Icon`

  Compose pages from these rather than writing raw markup. Their namespaces are already imported in `_Imports.razor`.
- Pages call the API with the injected `HttpClient` and the `Contracts` DTOs. They handle 400 responses by deserialising `ValidationErrorResponse` into `AlertMessage`. `Pages/Patients/EditPatient.razor.cs` is the reference example.
- `AuthState` caches the result of `api/auth/whoami`. `CredentialHandler` makes every request send the session cookie.

## Local dev gotchas

- The connection string points at `(localdb)\MSSQLLocalDB`, database `MedApp`, so local runs need Windows LocalDB.
- The client's `HttpClient.BaseAddress` is hard-coded to `https://localhost:44305/` in `MedApp.Client/Program.cs`, which is the API's **IIS Express** SSL port. The API's Kestrel `https` profile listens on `7038`. When you run the API with `dotnet run`, the client won't reach it unless you change one side to match.
- API CORS only allows `https://localhost:7292`, the client's `https` profile.
- Session cookies are `Secure` with `SameSite=None` only in Production/Staging. In Development they are `SameSite=Lax`.

## Testing conventions

- The stack is NUnit 3, FluentAssertions 8, Moq and AutoFixture with AutoMoq. FluentAssertions prints a license banner when tests run; it's expected and not an error.
- The test tree mirrors the source: `MedApp.UnitTests/Application/<Area>/Commands/<Action>/<Action>HandlerTests.cs` and `...ValidatorTests.cs`. Note that `Authentication` maps to `Auth` and `Sessions` to `Session` in the test tree.
- Fixtures are `[TestFixture] public sealed class`, with setup in `[SetUp]`. Build the system under test with `AutoFixtureFactory.Create()`, then `_fixture.Inject(mock.Object)`, then `_fixture.Create<Handler>()`.
- Test names follow `Method_Scenario_ExpectedResult`, for example `Handle_PatientDoesNotExist_ReturnsNull`.
- Put shared test data in `MedApp.UnitTests/Common/Constants/*TestConstants.cs`, and add new constants there rather than inlining magic values.
- Use synthetic data only. This is a medical app, so never put realistic patient PII in tests, seeds or fixtures.
- **Gap:** the Procedures area (`Application/Procedures/**`) has no unit tests yet.

## CI

`.github/workflows/main.yml` runs on PRs to `main`. It runs `dotnet test MedApp.UnitTests` in Release with coverage, then posts a sticky PR comment listing classes under 80% line coverage. Commands, queries, DTOs, repositories, DbContext, migrations and `ServiceExtensions` are excluded from coverage. Handlers and validators are what count.

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
2. **If it passed**, say so in one line, and mention any coverage problems from the sticky PR comment, such as handlers or validators under 80%.
3. **If it failed**, find the cause before reporting.
   - Note which step failed. A failure in "Set up job" means the workflow itself is broken (bad action ref, runner image), not the code.
   - Read the annotations with `GET /repos/Aliator/MedApp/check-runs/<job_id>/annotations`. Full job logs need auth, so read them through `gh run view <run_id> --log-failed` when `gh` is available.
   - Reproduce the failure locally the way CI runs it (`dotnet test MedApp.UnitTests --configuration Release`).
   - Check whether the failure is yours or already broken on `main`, for example when the workflow file is unchanged by your branch.
4. **Prompt the user before fixing.** Tell them which step failed and the error, the cause, whether your change caused it or it was already broken, and the fix you propose with the exact diff. Wait for them to approve before you commit or push it, unless they've already told you to fix it. After pushing a fix, check the new run again (step 1).

## Git workflow

- Branch from `main` as `feature/<name>`, and merge back through a PR to `main`.
- Commit messages are short, capitalised and past tense, for example "Updated patient task routing", "Added get all methods for tasks".
- Never commit `bin/`, `obj/`, `.idea/` or `*.DotSettings.user`.
