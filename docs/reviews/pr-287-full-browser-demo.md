# PR #287: full browser demo (quick review)

- PR: https://github.com/Aliator/MedApp/pull/287, branch `claude/eloquent-feynman-wysbc1` into `main`
- Commit reviewed: `29a27174c483cd7499ac5c94438608a556696654`, 23 November 2026 (quick review, diff only, nothing run)

**What it does:** adds `MedApp.Browser` (the real backend on WebAssembly and SQLite, compiled from source) and `tools/MedApp.BrowserDb`, which builds the demo SQLite database with the DevMcp seeder. It also adds a `__FULL_DEMO__` build of the React app, loaded by dynamic import and gated in `entry-client.tsx` and `routes/login.tsx`, and extends `pages.yml` to build both demos, including a nightly run. Changes to shared code:
- `ServiceExtensions` is split into a portable partial. The registrations are the same; `private` became `internal`.
- `PatientRepository.GetRetentionCandidatesAsync` reads retention classes in a second query and merges them through a lookup. The result is equivalent.
- `JsonColumn.Read/Write` are now `internal`.
- The seeder is split into `*.SqlServer.cs`.

**Findings:** none.

**Checked:**
- `DemoDataSeeder.RunAsync` (SQL Server path) still calls `DevGuard.Check` and wipes as before. The new `SeedAsync(wipe:)` entry point is reachable only through `SeedServices` built by the caller.
- `pages.yml` keeps `permissions: {}` at the top level, `pages`/`id-token: write` only on deploy, and every action pinned to a full SHA.
- `__FULL_DEMO__` is `false` outside `mode === 'full-demo'`, so the demo code is dead-code-eliminated from the production build.
- No migrations, enum changes, auth or session code, or secrets are in the diff.

**Remaining risk:**
- The `MedApp.Browser/Data/CompiledModel/*` files and the `MedApp.Browser` stand-ins (including the browser-only sign-in) were only skimmed. Their isolation rests on the project being referenced only by the Pages workflow and `MedApp.sln`, not by `MedApp.API` or the Dockerfile.
- Making `PatientRepository` retention reads two queries loads every non-Standard pathway row. That is fine at current scale, but the cost grows with data.
- The nightly Pages run now builds from the Application, Infrastructure and API sources, so a break there also breaks the demo deploy.

Author's note: the retention rewrite was checked against SQL Server 2022 with the `RetentionReview` integration fixtures (2/2 passed, 23 November 2026).
