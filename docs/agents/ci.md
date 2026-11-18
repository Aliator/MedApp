# CI workflows

What each CI job does, coverage, CodeQL, Dependabot and the supply chain rules: read before changing a workflow, a dependency gate or an action. The per-change rules are in the root `AGENTS.md` (CI, and Checking CI on PRs you opened or pushed to).

`.github/workflows/main.yml` runs on PRs to `main`, with nine jobs. `web`, `helm`, `unit-tests`, `image`, `integration-tests` and `e2e` are required checks on `main`.
- `changes` skips `web`, `image`, `e2e`, `helm`, `integration-tests` and `unit-tests` (skipped by `if` counts as passing; the run takes under a minute) for a push that changes only `docs/`, on a commit whose run passed, with nothing merged into the base since that run started: that run already tested the same merge, and nothing built or tested reads `docs/`. It runs them all for:
  - a new or reopened PR, or a force push
  - a previous run that failed, was cancelled or hasn't finished
  - anything merged into the base since that run started (the commits API, by date)
  - any change outside `docs/`, a merge from `main` included (a move in or out of `docs/` counts by both names)
  - a push of 300 files or more (the comparison lists only that many)
  - `changes` failing (the jobs run unless it says `false`)

  It compares the push, not the whole PR (which `paths-ignore` can't), and the run's summary says why. Never put a file that a build or test reads under `docs/`.
- `web`: `AGENTS.md` at most 40,000 characters, `npm ci`, `npm audit --audit-level=high` (high or critical fails, except IDs in `ACCEPTED`: see the supply chain rules), lint, type check, a check the built theme is current, Vitest with coverage, and `npm run build` (client, server rendering bundle, page server).
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
