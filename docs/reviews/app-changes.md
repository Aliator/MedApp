# Review: changes to the app's own code

**Pull requests:** #43 (test coverage, and a bug the tests found), #44 (hand-written mappers), and the app side of #48 (`create-first-admin`, health checks on their own port, HSTS). **Status:** merged to `main`.

The server-side rendering code (#42) and the backplane (#50) are reviewed in [server-side-rendering.md](server-side-rendering.md) and [signalr-backplane.md](signalr-backplane.md).

## The first admin (`create-first-admin`, #48)

**Why.** The app seeds no users. Locally, `medapp-dev` makes an `admin`/`admin` login, bypassing the password rules, but a deployed database starts empty, with no way in. #48 makes the first admin a deliberate step of the deploy, with credentials from a secret.

**How it works.**
- `MedApp.API/Program.cs:18-19`: if the only argument is `create-first-admin`, the API builds its services, runs `FirstAdminSetup.RunCommandAsync` and exits with its code, without starting the web server (hosted services such as the SSR server only start in `app.Run()`).
- `MedApp.Infrastructure/Common/Identity/FirstAdminSetup.cs:42-94`: with `FirstAdmin:Username` and `FirstAdmin:Password` set, it adds the `Admin` and `User` roles if they're missing, then, only if there are no users at all, creates the user and adds it to `Admin`. All of it runs in one transaction (`IdentityTransactions`), committed only on success. It exits 0 when it made the admin or found accounts, and 1 when it isn't configured or Identity refuses (for example, a password that breaks the `Identity` rules).
- The Helm chart's migrations Job runs it after the migrations bundle (`migrations-job.yaml:41-42`), with the credentials from the first-admin Secret. On kind and Azure that Secret comes from the secret store, with username `admin` and a generated password.

**Design.** "Only while there are no accounts" makes it safe to run on every deploy: once anyone exists, it changes nothing and never resets a password. The alternative, a seeded account in a migration, would put a known login into every database.

**Verified.** 15 tests in `FirstAdminSetupTests` (missing settings, a new database, one transaction with the token, existing roles, existing accounts, a role that can't be made, a refused password, a role that can't be added, and the command's exit codes). On kind (#48): on an empty database the admin signed in over https and used admin-only endpoints; a redeploy left it alone; the demo-data path still worked.

## Health checks on their own port (#48)

**Why.** Before, `/health` and `/alive` were mapped only in Development, so Kubernetes could only check that the port was open. Mapping them on the app's port in production would publish them through the ingress, which the Aspire template's own comment warns against.

**How it works.** `MedApp.ServiceDefaults/Extensions.cs:109-133`: when `HealthChecks:Port` is set, `UseHealthChecks(path, port, ...)` answers the two paths only for connections on that port; it matches the connection's local port, not the `Host` header, which a caller controls. Without the setting: Development keeps them on the app's port, and anywhere else has none. The chart sets the port to 8081, listens on `8080;8081`, and exposes only 8080 through the Service and the network policy.

**Verified.** On kind (#48), port 8081 answered `Healthy` for both paths, and `/health` through the ingress was the app's page. There's no automated test: ServiceDefaults is excluded from coverage as wiring.

Two things to know (both in [kubernetes.md](kubernetes.md)): both endpoints run only the built-in "self" check, so they say nothing about the database; and port 8081 serves the whole app, not just the two paths. The Service and the network policy are what keep it private.

## HSTS (#48)

`MedApp.API/Common/Extensions/ApplicationBuilderExtensions.cs:16-20`: `UseHsts()` outside Development. ASP.NET Core only sends the header on https requests and never for `localhost`, so it depends on the chart's `ASPNETCORE_FORWARDEDHEADERS_ENABLED`, which lets the API see that Traefik received https. Verified on kind: sent for a real host name, not for `localhost`. It uses the defaults (30 days, no `includeSubDomains`); a production host may want longer.

## A bug the tests found (#43)

`MedApp.API/Middleware/ExceptionHandling.cs:59, 74`: the middleware set `application/problem+json`, then `WriteAsJsonAsync` replaced it with `application/json`. The content type now goes to the write itself. `ExceptionHandlingTests` covers it.

## Mappers (#44)

**Why.** Handlers built responses inline, in several styles, and two patient endpoints returned the Domain `Patient` itself.

**What changed.**
- One hand-written static mapper per area, in the area's `Common/` folder: new `PatientMapper`, `UserSettingsMapper`, `AuditEntryMapper`, and `UserMapper` (in Infrastructure, because `ApplicationUser` is an Infrastructure type); `NotificationMapper` gained the list response. They join the existing `ProcedureMapper`, `StageDefinitionMapper` and `StageTemplateMapper`.
- `PagedMapper` (`MedApp.Application/Common/Paging/PagedMapper.cs`) builds every search endpoint's `PagedResponse` from a repository's `PagedResult`, replacing four copies of the same construction.
- The Patients and Auth handlers, audit search and notifications use them. `AuthController` no longer re-maps a `UserResponse` into a new one.
- `AGENTS.md` (Mapping) makes it the convention: no mapping library, no inline mapping, a test per mapper.

**An API change.** `GET api/patients/{id}` and `GET api/patients` returned the Domain `Patient`, including `CreatedAt` and `LastUpdated`, although Swagger documented them as `PatientResponse`. They now return `PatientResponse`. Checked for this review: neither `MedApp.Web` nor the deprecated Blazor client reads those two fields, and the generated TypeScript types were already `PatientResponse`.

**Verified.** 15 new mapper tests (688 in total then); the patient query tests now check the response's shape; on the AppHost, `GET api/patients/{id}` returned only the response's fields, and UI parity passed for the patient, user, audit and procedure screens.

**Design.** Static mappers keep mapping visible and cheap, and fit the existing procedure mappers. The cost is boilerplate, which a library would hide, and the risk of a mapper forgetting a new field. The per-mapper tests are what guard that.

## Test coverage (#43, and since)

| | Before #43 | After #43 | Now |
|---|---|---|---|
| .NET tests | 505 | 673 | 708 |
| Web tests | 466 | 666 | 666 |
| Web lines / branches | 94.7% / 84.5% | 99.7% / 93.3% | not re-measured |
| .NET counted lines / branches | 92.9% / 84.0%, API not counted | 100% / 98%, API counted | not re-measured |

"Now" was re-run for this review: `dotnet test MedApp.UnitTests -c Release` and `npm test` both pass on the current tree.

- **Web:** every component, page, `app/` provider, API module and the SSR document has tests. `vite.config.ts:45` sets thresholds (99% lines, statements and functions, 92% branches), so CI's `web` job fails if coverage drops.
- **.NET:** controllers are tested through `RecordingMediator` (`MedApp.UnitTests/Common/Fixtures/RecordingMediator.cs`), which records each request and its cancellation token, so a controller test checks what's sent without MediatR. Also new: the exception middleware, session authentication, the cookie service, the SignalR publisher, SSR (including a real Node process), and `AsyncQueryable` for EF's async operators over in-memory data.
- **CI's .NET report** now counts `MedApp.API`, and leaves out generated code (`obj/`), the Aspire service defaults and wiring (`ServiceExtensions`, `ApplicationBuilderExtensions`, `Program`, `IdentityTransactions`).
- **Left uncovered on purpose (#43):** `??` fallbacks for fields the API always sends (the generated types mark everything optional), browser states jsdom can't produce, compiler-generated code, and `Process.Start` returning null.

## Risks and open questions

1. **Medium: the wiring that's excluded from coverage is where the most serious problem sits.** ServiceDefaults, `ServiceExtensions` and `ApplicationBuilderExtensions` aren't counted, and nothing tests the pipeline as a whole. That's how the SSR client came to inherit the standard resilience handler unnoticed ([server-side-rendering.md](server-side-rendering.md), risk 1). The health port, HSTS, the blocked `/ssr` and `/.vite` paths and the fallback are only checked by hand. A few `WebApplicationFactory` tests of the real pipeline would cover them.
2. **Low: .NET coverage isn't a gate.** The `unit-tests` job reports coverage in the PR comment, but only the `web` job fails below a threshold.
3. **Low: the first admin is always called `admin`.** All three generated sources (the chart, kind's store and Bicep) use it. With lockout after 5 failed attempts for 5 minutes, guessing is slow, but anyone can also lock that account out on purpose. A less predictable name would avoid both.
4. **Low: `create-first-admin` would run again on an emptied database.** If every account were ever deleted, the next deploy would recreate the admin with the stored password. That's the intended behaviour, but worth knowing.
5. **Low: HSTS uses the defaults** (30 days, no subdomains).

## Where to look closely

- `MedApp.Infrastructure/Common/Identity/FirstAdminSetup.cs:42-94` with `IdentityTransactions.cs:13-25`.
- `MedApp.API/Program.cs:18-19`.
- `MedApp.ServiceDefaults/Extensions.cs:109-133`.
- `MedApp.Application/Common/Paging/PagedMapper.cs` and the patient query handlers (`MedApp.Application/Patients/Queries/`).
