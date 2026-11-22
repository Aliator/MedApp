# The full app in the browser (replacing the light demo)

Status: **in progress** · 22 November 2026 · the owner's decisions below

**In one line:** the real MedApp backend (the Application layer's handlers, Infrastructure's repositories and the API's controllers) compiled to WebAssembly and run in the visitor's browser on SQLite, behind the React app, on GitHub Pages: every feature with a real rule, not the light demo's simplified TypeScript copies.

## The owner's decisions (22 November 2026)

1. **Option A:** everything runs in the browser, on GitHub Pages; no server.
2. **Single user:** assume one person at a time, so whatever exists for concurrency (locks, row versions, lock hints, lease checks) is simplified away in the browser build rather than reproduced.
3. **It replaces the light demo** once it's done and tested: built at `/MedApp/full/` first, then moved to `/MedApp/` and the light demo (`MedApp.Web/src/demo`) retired, with its parity rule (`docs/agents/light-demo.md`), after the owner agrees.
4. **The login page** lists the accounts to sign in as (like the light demo's), and **downloads and caches the backend and the database before sign-in is allowed**, with progress shown; later visits load from the cache, and a newer published database is fetched first.

## Feasibility (checked 22 November 2026)

- .NET 10 for the browser (`Microsoft.NET.Sdk.WebAssembly`, the `wasm-tools` workload for native linking) runs **EF Core with SQLite** (`Microsoft.EntityFrameworkCore.Sqlite`, SQLite linked natively from `SQLitePCLRaw`'s `browser-wasm` build) in Chromium: a database created, written and read in 0.7 s.
- **Trimming must be off** (`PublishTrimmed=false`): EF Core makes objects by reflection; trimmed, it fails at runtime.
- **`MedApp.Application` compiles for the browser unchanged** and its 291 handlers load.
- Size: the runtime and EF Core alone are about 11 MB compressed; the whole backend is expected at 15 to 25 MB.

## Design

### The backend: `MedApp.Browser`

A new project (`Microsoft.NET.Sdk.WebAssembly`, `PublishTrimmed=false`, `WasmBuildNative=true`, `InvariantGlobalization` unless the app needs cultures), referencing `MedApp.Application`, `MedApp.Domain` and `MedApp.Contracts` as they are.

- **Infrastructure by source:** `MedApp.Infrastructure` can't be referenced (it needs the ASP.NET Core shared framework, Azure, MailKit, SkiaSharp's Linux build), so its portable source files are compiled into `MedApp.Browser` (`<Compile Include="../MedApp.Infrastructure/**/*.cs" Exclude="…">`), and the few that aren't are replaced by single-user stand-ins in `MedApp.Browser`:
  - **Locks:** `ICalendarLock` and the last-admin guard do nothing (one user).
  - **Concurrency:** row versions (`IsRowVersion`) aren't mapped; `expectedVersion` checks in handlers stay (they compare numbers the app keeps itself).
  - **Sample numbers:** the SQL Server sequence becomes a counter row.
  - **Raw SQL and lock hints** (roles, scheduled jobs): plain LINQ.
  - **Files:** bytes in a SQLite table instead of Azure Blob storage; no virus scan (files are marked clean); no thumbnails (SkiaSharp).
  - **Email, SMS, secure email:** not sent (recorded as not sent, as with no email configured).
  - **Live notifications (SignalR):** none; notifications are read when the page asks.
  - **Data Protection, sessions:** one in-memory session for the account picked.
  - **PDFs (PDFsharp):** kept if it runs in the browser; otherwise "not available here".
  - **Outside services (PDS, FHIR, hospital):** "not available here".
- **Changes to shared code** only where a seam is cleaner than a copy (an interface for the sample number, say), each small and covered by the existing tests.

### The HTTP layer

The 38 controllers (356 actions) compile **unchanged** into `MedApp.Browser` against a small stand-in for the parts of ASP.NET Core MVC they use: `ControllerBase` and its results (`Ok`, `Created`, `NoContent`, `File`, problem results), the routing and binding attributes, `User`, and `[RequirePermission]`. A reflection router matches `METHOD /api/...` to an action by its route template (with constraints like `{id:guid}`), binds route values, query strings (into request objects) and the JSON body as ASP.NET Core does, checks the permission, calls the action and serialises the result with the API's JSON options. Exceptions become the same responses as `ExceptionHandling.cs` (400 `ValidationProblemDetails`, 401, 403, 404, 409 ProblemDetails). One `[JSExport]` entry point takes a request (method, URL, headers, body) and returns the response (status, headers, body).

### The database

- **SQLite, built from the model** (`EnsureCreated`), not the 110 SQL Server migrations. `MedAppDbContext` gets small provider switches where SQL Server-only mapping is used (row versions, the sequence, `datetime2` and other column types, filtered index SQL in brackets).
- **Case:** SQL Server compares text without case; SQLite doesn't. Searches that rely on it are made case-insensitive for SQLite (the `NOCASE` collation on searched text columns, or lower-casing in the query).
- **Kept in the browser:** after any request that changed data, the SQLite file is saved to IndexedDB; on start it's restored from there.

### The demo data

- **Made at build time** by the full app's own seeder (`tools/MedApp.DevMcp/DemoData`) against SQLite (a native run in CI), so the browser never seeds: the same clinic as the full app's demo data (three teams, nine people, every record), dated from the build day. Passwords aren't needed (the account picker).
- **Published** as `medapp.db` (compressed) with a version file (`db-version.json`: the build's date and a hash).
- **A nightly GitHub Action** rebuilds and redeploys, so dates stay current. A visitor whose cached database is older than the published one gets the new one at the login page (their changes go, as the light demo's daily reseed does).

### The web app

- **A new build mode** (`npm run build:full-demo`), like the light demo's, behind a build-time constant (`__FULL_DEMO__`), loaded by dynamic import, never in the production build: `window.fetch` for `/api/...` goes to the .NET backend's entry point instead of the network (the light demo's `install.ts` pattern).
- **The login page:** the accounts (from the seeded database's demo people, or a fixed list matching the seed), each with its description, the sign-in buttons disabled with a progress bar ("Getting MedApp ready: 12 of 20 MB") until the backend and the database are downloaded and cached; then signing in starts the in-memory session as that person.
- **The walkthrough** (`src/demo/coach`) comes along, its targets found by name (the records' IDs come from the seeded database's `tour` IDs).
- **Not started in this mode:** `LiveNotifications`, server rendering.

### Hosting

The same Pages workflow builds both sites: the light demo at `/MedApp/`, this at `/MedApp/full/`, until the switch (decision 3).

## Phases

1. **The backend answers:** `MedApp.Browser` builds; the MVC stand-in and router; SQLite from the model; a handful of endpoints (sign-in, whoami, patients search, a pathway) working in Chromium against an empty database; the size measured.
2. **The demo database:** the seeder runs against SQLite at build time; the browser loads `medapp.db` and serves the seeded data.
3. **Every endpoint:** all 356 actions routed; a test that every path in `src/api/schema.d.ts` reaches an action; the stand-ins (files, email, PDFs) in place.
4. **The web app:** the build mode, the login page with download, cache and progress, the walkthrough.
5. **Deploying:** the Pages workflow builds both, the nightly rebuild; then, once tested and the owner agrees, the switch.

## What's lost against the full app (for the owner, 22 November 2026)

Sharing between people (each browser has its own database), real security (it all runs on the visitor's machine: synthetic data only, never real patients), email and texts, live notifications, server rendering, outside services (PDS, FHIR), virus scanning, thumbnails, scheduled jobs except while the tab is open, and possibly PDFs. A first visit downloads 15 to 25 MB.
