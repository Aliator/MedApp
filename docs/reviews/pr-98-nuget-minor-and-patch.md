# Review: PR #98, Dependabot's grouped minor and patch NuGet updates

**PR:** [#98](https://github.com/Aliator/MedApp/pull/98) ("Bump coverlet.collector and 16 others"), branch `dependabot/nuget/MedApp.Application/nuget-minor-and-patch-9faabb69b8`, reviewed at `d793461` against `origin/main` at `1f6f79c`. The branch was cut from `af1da36`; `main` has since gained #97 and #100.

**Reviewed:** 17 November 2026, independently of the PR description, which Dependabot wrote and cut short. Each claim below was checked against the packages, their release notes and source, a build, a test run, or the dev tool started by hand. **No finding is high or critical:** all 3 are low.

## Summary

The PR moves 17 NuGet packages up by a minor or patch version, in six project files: MediatR, FluentValidation, FluentAssertions, Moq, OpenTelemetry, the HTTP resilience and service discovery packages, the MCP SDK, and the test tooling.

- **No licence changed,** no known vulnerability is added, and every package's signature checks out.
- **It fixes a high-severity OpenTelemetry advisory** that MedApp isn't exposed to, but that will fail every build on `main` once it reaches NuGet's vulnerability feed. So it's worth merging soon.
- **It adds two compiler warnings** (Moq), and leaves the architecture handbook's version table out of date.

## What changed, and why

6 `.csproj` files, 26 lines changed, no code. Dependabot proposes these every Monday (`.github/dependabot.yml:30-46`), grouped into one pull request, and only once a release is a week old.

### The licensed libraries

- **MediatR 14.0.0 to 14.3.0** (in the production image)
  - **What it is:** a "mediator". A controller hands it a command; it finds the command's handler and runs it, through a pipeline of steps every command passes. Like a hospital switchboard: the caller says what they need, the switchboard puts them through, and every call goes through the same checks first.
  - **In MedApp:** every controller action calls `mediator.Send(...)`. The one pipeline step is `ValidationBehaviour`, which runs a command's validators before its handler (`MedApp.Application/Common/Behaviors/ValidationBehavior.cs:7-35`, registered at `MedApp.Application/Common/Extensions/ServiceExtensions.cs:19-23`).
  - **What changed:** mostly licence handling. A key can now come from DI or from an environment variable (`MEDIATR_LICENSE_KEY` or `LUCKYPENNY_LICENSE_KEY`); perpetual licences; a warning, not an error, in a renewal's grace period; and the check moved to a background thread, once per process. Also: open pipeline steps get their type parameters inferred from constraints, and notification handlers are de-duplicated.
  - **For MedApp:** nothing visible. With no key, it logs the same single warning as before (`LicenseValidator.cs:34-43` at `v14.3.0`). `ValidationBehaviour`'s only constraint is `notnull`, so the new inference doesn't change which requests it runs for. MedApp publishes no MediatR notifications.
  - **Licence:** unchanged. `LICENSE.md` is byte-identical in 14.0.0 and 14.3.0: the Reciprocal Public License 1.5, or a commercial licence.
- **FluentAssertions 8.8.0 to 8.11.0** (tests only)
  - **What it is:** readable test assertions, such as `result.Should().Be(expected)`.
  - **What changed:** new assertions (spans, JSON, supersets), a much faster `BeEquivalentTo` on large unordered collections, clearer failure messages, and a fix for a null reference in `AssertionScope`.
  - **Licence:** unchanged. The `LICENSE` file is byte-identical: Xceed's Community License, for non-commercial use only. It still prints its banner when tests run.
- **Moq 4.20.72 to 4.21.0** (tests only)
  - **What it is:** fake versions of a class's dependencies ("mocks"), so a unit test can check one class alone. Like a flight simulator: the cockpit is real, the sky is not.
  - **What changed:** Moq's own code now declares where nulls are allowed ("nullable annotations"). That's why two of MedApp's tests now get compiler warnings (finding 1). Also faster `It.Is` matching and a `ValueTask` fix.
  - **Licence:** BSD-3-Clause, as before. The package has no `analyzers`, `build` or `buildTransitive` folder, so nothing of Moq's runs at build time. (Moq 4.20.0 briefly added "SponsorLink", a build-time hook that read a hash of the developer's email address; it was removed in 4.20.2 and hasn't come back.)

### Validation

- **FluentValidation and its DependencyInjectionExtensions 12.1.1 to 12.2.0** (in the image; Apache-2.0)
  - **What it is:** validation rules written as code ("Name is required", "at most 200 characters"). A failure becomes the API's 400 response, with an error for each field, and the web app shows each one next to its field.
  - **What changed:** new languages; fixes to property paths in `.When()` on `RuleForEach` and to sibling property chains; an inverted range (`InclusiveBetween(10, 1)`) is refused in more cases; the enum check rewritten for native AOT; a null fix in `TestValidationResult`.
  - **For MedApp:** nothing visible.
    - It never uses `.When()` on `RuleForEach`.
    - Its `InclusiveBetween` ranges all run from 1 upwards.
    - Its one `IsInEnum` (`SetPatientFlagValidator.cs:12`) has its own tests, which pass.
    - Its messages are mostly its own `WithMessage` text, and the release notes change no English message.
  - **The API's legacy package:** `FluentValidation.AspNetCore` 11.3.1 isn't updated (it has no newer version). It asks for FluentValidation 11.11.0 or later, so it runs against 12.2.0, as it ran against 12.1.1 before.

### Calling other services

- **Microsoft.Extensions.Http.Resilience and ServiceDiscovery 10.10.0 to 10.11.0** (in the image; MIT)
  - **What they are:** resilience adds automatic retries, timeouts and a circuit breaker to outgoing HTTP calls, like redialling a busy line. Service discovery finds a service by name instead of address.
  - **In MedApp:** the service defaults add both to every `HttpClient` (`MedApp.ServiceDefaults/Extensions.cs:29-36`). The `ssr` client, which forwards page requests to the Node page server, removes resilience (`MedApp.API/Common/Extensions/ServiceExtensions.cs:44-48`), so a failed render falls back to the client-rendered app at once instead of being retried.
  - **What changed:** logging fixes and work on Microsoft's AI libraries. The only HTTP entry is a duplicated separator in HTTP logging.
  - **For MedApp:** the `ssr` client still makes one attempt. `SsrClientTests.CreateClient_RendererFails_AsksItOnceWithoutRetrying` (`MedApp.UnitTests/Api/ServerRendering/SsrClientTests.cs:45`) builds the API's real services, service defaults included, and passes.

### Telemetry

- **OpenTelemetry 1.17.0 to 1.20.1 (Hosting, the OTLP exporter, Runtime) and 1.20.2 (ASP.NET Core, HTTP client)** (in the image; Apache-2.0)
  - **What it is:** the standard way for an app to record traces (each request's path through the code), metrics and logs, and send them to a monitoring tool. Like a flight recorder.
  - **In MedApp:** set up in `MedApp.ServiceDefaults/Extensions.cs:47-98`. It's only sent anywhere when `OTEL_EXPORTER_OTLP_ENDPOINT` is set: in development, to the Aspire dashboard. Nothing sets it in Kubernetes.
  - **A security fix (ASP.NET Core 1.20.2):** [GHSA-6xq4-gr25-qg2x](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/security/advisories/GHSA-6xq4-gr25-qg2x), rated high (CVSS 4.0: 8.7), for versions 1.15.2 up to 1.20.1, so `main`'s 1.17.0 is in range.
    - **The bug:** for an endpoint with no route template, a route value named `page` taken from the URL was used as the route's name, and kept in a cache for the life of the process. Anyone could send unique paths until the process ran out of memory.
    - **MedApp isn't exposed:** its catch-all routes name their value `path`, not `page`, and have templates (`MedApp.API/Common/Extensions/ApplicationBuilderExtensions.cs:70-75`). It has no Razor Pages, and its controllers use attribute routes.
    - **But NuGet's audit can't tell:** the advisory isn't in GitHub's global advisory database yet (its API answers 404). Once it is, every project that includes this package fails to restore on `main`, because a high advisory is an error there (`MedApp.ServiceDefaults/MedApp.ServiceDefaults.csproj:3-9`). This PR moves past it first.
  - **Other changes that touch MedApp:**
    - Server spans and the request duration metric now carry `error.type` for a 5xx response.
    - The OTLP exporter no longer follows redirects, caps request and response sizes, keeps inline certificate material out of its diagnostics, and no longer logs its own HTTP calls at Information.
    - Logs: replaced log attributes are no longer exported alongside their original values, and pooled log records no longer hold their data until reused.
    - Runtime: an options overload is marked obsolete; MedApp uses the plain `AddRuntimeInstrumentation()`.
  - **Unchanged:** query string values are still replaced with `Redacted` in `url.query` and `url.full` by default (since 1.8.1). No changelog entry from 1.17.0 to 1.20.2 touches it, so the governance pack's description of telemetry (`docs/governance/data-inventory.md:75`) is no less true.

### The dev tool

- **ModelContextProtocol 2.3.0 to 2.4.0** (dev only; Apache-2.0)
  - **What it is:** the C# SDK for the Model Context Protocol, the way AI coding agents call tools. In MedApp it runs `medapp-dev` (`tools/MedApp.DevMcp`), a local tool over standard input and output that is never deployed.
  - **What changed:** an experimental "Skills" extension (a separate package), OAuth hardening, and fixes to transports, sessions and cancellation. Protocol property names in MCP Apps now match case-sensitively. It follows 2.3.1, a security fix for authenticated HTTP sessions, which a standard-input tool doesn't use.
  - **For MedApp:** the tool starts, negotiates, and lists all 19 of its tools (How it was checked).

### Test tooling (tests only)

- **Microsoft.NET.Test.Sdk 17.8.0 to 17.14.1** (MIT): what `dotnet test` uses to run a test project. It now fails loudly on target frameworks it doesn't support; .NET 10 is fine. Its notes also list a fix in `Microsoft.TestPlatform.CLI`, which ships inside the .NET SDK, not in this package.
- **NUnit3TestAdapter 4.5.0 to 4.6.0** (MIT): lets `dotnet test` find and run NUnit tests.
- **NUnit.Analyzers 3.9.0 to 3.10.0** (its own licence file, as before): compiler checks for common NUnit mistakes. No new warnings.
- **coverlet.collector 6.0.0 to 6.0.4** (MIT): measures which lines and branches the tests run. It now counts async and generic code more accurately (How it was checked).

## Design choices

These are Dependabot's settings (`.github/dependabot.yml`), and the choice to take the update now.

- **One grouped pull request, rather than one per package:**
  - **Instead:** a pull request for each package.
  - **Chosen because:** one review and one CI run instead of 17.
  - **Worse at:**
    - One package that breaks something holds up the other 16.
    - Licence-sensitive libraries arrive among routine ones (finding 3).
    - Dependabot cuts long descriptions. This one stops partway through the Test SDK's notes, so it says nothing about Moq, OpenTelemetry, the MCP SDK or the NUnit packages.
- **Minor and patch versions only:**
  - **Instead:** majors in the same group.
  - **Chosen because:** a major version may need code changes, so it gets its own pull request.
  - **Worse at:** the test tooling stays a major behind. NUnit 5, Test SDK 18, coverlet 10 and NUnit.Analyzers 4 are open separately (#79 to #82, #88, #91, #92).
- **A week's cooldown before a release is proposed:**
  - **Instead:** propose releases at once.
  - **Chosen because:** a hijacked release is usually found and pulled within days.
  - **Worse at:** security fixes wait too. OpenTelemetry's 1.20.2 came 10 days after its release. (Dependabot's security updates, for advisories in its database, skip the cooldown; this one isn't in the database yet.)
- **Merge it soon, rather than wait for the next batch:**
  - **Instead:** leave it for a quieter week.
  - **Chosen because:** once the OpenTelemetry advisory reaches NuGet's feed, `main` stops building until this version is in.
  - **Worse at:** nothing much. The real risk is the combination with today's `main`, which CI hasn't run yet (Remaining risks).

## How it was checked

- **Read:**
  - the diff and the PR description
  - the release notes or changelogs of every package, from GitHub: MediatR, FluentAssertions, FluentValidation, Moq, six OpenTelemetry changelogs, `dotnet/extensions` 10.11.0, the MCP SDK 2.3.1 and 2.4.0, and the two NUnit packages
  - MediatR's licensing source at `v14.0.0` and `v14.3.0` (`LicenseAccessor`, `LicenseValidator`, `MediatRServiceCollectionExtensions`)
  - the advisory GHSA-6xq4-gr25-qg2x
  - the code each package touches: `ValidationBehaviour` and its registration, the validators, the `ssr` client, the service defaults, the routes, the dev tool and the Dockerfile.
- **Licences:** compared each new package's licence metadata with the old one's. FluentAssertions' and MediatR's licence files are byte-identical. Moq, FluentAssertions and MediatR have no build-time hooks.
- **Signatures:** `dotnet nuget verify --all` on eight of the new packages: all valid. MediatR is signed by Lucky Penny Software LLC and Http.Resilience by Microsoft, as well as by nuget.org; the rest by nuget.org.
- **Advisories:**
  - GitHub's advisory database, for the 17 packages and the main ones they bring in: nothing affects the new versions.
  - `dotnet list package --vulnerable --include-transitive` on the API, the three test projects and the dev tool: no vulnerable packages.
- **Consistency:** each updated package is at one version in every project that has it, and no project is left on an old one.
- **The Dockerfile:** it copies the API's six project files before restoring (`MedApp.API/Dockerfile:40-47`). The PR adds no project and no shared props file, so it needs nothing. CI's `image` job built it, and Trivy passed.
- **CI at `d793461`:** all 13 checks pass, including `e2e` against the production image and the integration tests. Those include `ErrorTests` (`MedApp.IntegrationTests/Api/ErrorTests.cs:21-46`), which check the 400 from a command's and a query's validators through the real MediatR pipeline. **But it ran before #97 and #100 merged.**
- **Release builds of the branch:** `MedApp.UnitTests`, `MedApp.IntegrationTests`, `MedApp.E2ETests` and `tools/MedApp.DevMcp` all build. The unit tests have 2 new warnings (finding 1). CI's log agrees: 2 warnings in this PR's `unit-tests` job, 0 in #96's.
- **This PR on today's `main`:** exported `origin/main` (`1f6f79c`) to a scratch folder outside git, applied the PR's diff, and built `MedApp.UnitTests` and `MedApp.IntegrationTests` from clean. The integration tests now build the dev tool, with #97's new demo data seeder. Both build, with the same 2 warnings and nothing else.
- **Unit tests, filtered:**
  - **On the branch:** validators, day templates, the `ssr` client, the dev tool's services, `ValidationBehaviour`, the exception middleware and the two warned files: 610 passed.
  - **On `main` with the PR:** the dev tool's tests (demo data included), validators, the `ssr` client, `ValidationBehaviour` and the two warned files: 578 passed.
  - **The fix for finding 1,** tried in the scratch copy: 0 warnings, and the 10 tests in those two files pass.
- **The dev tool by hand:** started the branch's Release build over standard input and sent `initialize` and `tools/list`. It answered with protocol `2025-06-18` and all 19 tools, with their read-only and idempotent hints. Its start-up read the local dev database's data protection keys (a `SELECT`); nothing was written.
- **Coverage** (the sticky PR comment, against #96's):
  - **Lines:** 92% both (9,337 of 10,146, against 9,338 of 10,147).
  - **Branches:** 90.3% both, but 88 fewer branches are counted (3,931 against 4,019). coverlet 6.0.3 and later stop counting some branches the compiler makes, so many validators now show no branch figure.
  - **Under 80%:** the same 19 classes. `PatientProcedureAssignment`, a class of properties only, drops from 100% to 80% of lines.
- **Not done:**
  - the integration and E2E suites locally (they need Docker; CI runs them again once this review is pushed)
  - telemetry in the Aspire dashboard, with the AppHost
  - any test with a MediatR licence key.

## Findings

### 1. Moq 4.21.0 adds two compiler warnings to the unit tests

- **Severity:** low. **Confidence:** high (built, and in CI's log).
- **What could go wrong:**
  - **The rule:** `AGENTS.md:44` says a change is done only when the build has "no new warnings". This PR merges two.
  - **Noise hides signal:** the next person to change MedApp builds the solution and sees two warnings they didn't cause. They learn to ignore warnings, and a real one (a possible null in production code) slips past.
  - **Every pull request** gets the same two annotations in CI.
- **Why:** Moq 4.21.0 turned on nullable annotations, so a log entry's `state` and a call's arguments are now "possibly null".
  - `state.ToString()` warns CS8602 (dereferencing a possibly null value).
  - `(LogLevel)i.Arguments[0]` warns CS8605 (unboxing a possibly null value).
- **Evidence:**
  - `MedApp.UnitTests/Application/Common/Security/SecuritySeamTests.cs:30`
  - `MedApp.UnitTests/Infrastructure/Common/Identity/DataProtectionKeyCheckTests.cs:32`
  - `MedApp.UnitTests/MedApp.UnitTests.csproj:28` (Moq 4.21.0)
  - CI: run 43978342923's `unit-tests` log has these 2 warnings; #96's run 43963036891 has 0.
- **Fix:** two null-forgiving operators (`!`), which only tell the compiler what the test already knows (Detail has the diff). Tried in a scratch copy: no warnings, and the tests pass. Push it to this branch before merging, or straight after.
- **What happened to it:** fixed in the PR, as suggested: `state!.ToString()!` and `(LogLevel)i.Arguments[0]!`. `MedApp.UnitTests` builds with no warnings, and the two files' tests pass.

### 2. The architecture handbook's version table goes out of date

- **Severity:** low. **Confidence:** high.
- **What could go wrong:**
  - **Wrong answers:** someone fills in a security questionnaire or a buyer's due diligence list from the handbook. It says OpenTelemetry 1.17.0, inside the advisory's range, so they report MedApp as affected, or spend time proving it isn't.
  - **Two sources disagree:** an assessor compares the published image's SBOM (from the Publish workflow) with the handbook, and they differ in nine places.
  - **The table says where its versions come from:** "Versions come from the `.csproj` files…" (`docs/architecture.md:249`). Until now it matched them; this is the first Dependabot NuGet update to merge.
- **Evidence:** `docs/architecture.md:260` (MediatR 14.0.0), `:261` (FluentValidation 12.1.1), `:265` (OpenTelemetry 1.17.0), `:266` (Http.Resilience and ServiceDiscovery 10.10.0), `:268` (ModelContextProtocol 2.3.0), `:316` (NUnit3TestAdapter 4.5.0, NUnit.Analyzers 3.9.0), `:317` (FluentAssertions 8.8.0), `:318` (Moq 4.20.72), `:319` (coverlet.collector 6.0.0).
- **Fix:**
  - **Now:** update those nine rows (Detail has the new values).
  - **For later:** every weekly batch will do this again. Either update the table with each one, or say once that the project files are the source and the table shows the versions on a given date.
- **What happened to it:** fixed in the PR: the nine rows show the new versions.

### 3. Licence-sensitive libraries arrive inside a 17-package group, and nothing checks licences

- **Severity:** low. **Confidence:** medium (a judgement about process; nothing is wrong today).
- **What could go wrong:**
  - **A licence can change in a minor version.** Moq added SponsorLink in a minor (4.20.0). MediatR's move to a commercial licence came in a major (13), but its licence check has changed in every minor since: 14.1, 14.2 and 14.3.
  - **The scenario:** a future minor of FluentAssertions, MediatR or Moq changes its terms, or adds a build-time hook. It arrives as one line among 17, in a description Dependabot has cut short. Every check is green, because none looks at licences, and it merges.
  - **This time** the licences were checked by hand (they're unchanged); nothing makes that happen next week.
- **Evidence:**
  - `.github/dependabot.yml:45-46`: one `nuget-minor-and-patch` group with the pattern `"*"`
  - `.github/workflows/main.yml:133-137`: `dependency-review` fails on vulnerabilities only, with no `allow-licenses`
  - `docs/architecture.md:1580-1583`: the licence check is planned (roadmap step 1), not built
  - `docs/governance/isms-risk-register.md:37` (IR12): the commercial dependencies are already a scored risk.
- **Fix,** either or both:
  - **A licence gate:** give `dependency-review` an `allow-licenses` list (MIT, Apache-2.0, BSD-2-Clause, BSD-3-Clause and the like). Name MediatR and FluentAssertions, whose licences are files rather than standard names, in `allow-dependencies-licenses`, so a new licence anywhere else fails the check.
  - **A group of their own:** in `dependabot.yml`, put `MediatR*`, `FluentAssertions*` and `Moq` in a group before `nuget-minor-and-patch`, so they come in a small pull request of their own whose notes aren't cut.
- **What happened to it:** left as a to-do. It's roadmap step 1's licence check, which this review recommends doing early.

## Remaining risks

- **Not yet tested on today's `main` in CI:**
  - #97 and #100 merged after this PR's CI ran (04:03 and 04:38 UTC on 17 November, against 23:19 UTC the night before).
  - **What's new there:** `MedApp.IntegrationTests` now references the dev tool, so the MCP SDK is in its graph, and #97's demo data seeder (about 4,800 lines) runs through MediatR in `DemoDataSeedTests`.
  - **Covered so far:** the combination builds, and its unit tests pass (How it was checked).
  - **Still to do:** pushing this review starts a CI run on the merge with today's `main`. Wait for it before merging (step 4 of the merge flow).
- **This review's row in `docs/reviews/README.md` conflicts with `main`:**
  - `main` added #100's row at the same place in the table, and git can't merge two rows added at one spot.
  - Until it's resolved, GitHub can't build the merge, so CI won't run.
  - **Fix:** merge `origin/main` into this branch, and keep both rows, #98 then #100.
- **A duplicate pull request:** #78 makes exactly the same 17 changes. Dependabot usually closes a superseded one itself; if it doesn't, close it after this merges.
- **Commits from others stop Dependabot rebasing this branch.** Once this review is pushed, any later conflict with `main` is ours to fix.
- **The majors are open per project:** NUnit 5 (#81, #88, #91, #92), Test SDK 18 (#80), coverlet 10 (#79) and NUnit.Analyzers 4 (#82). Merging one alone splits a package's version across the test projects. Take them together.
- **The licences themselves are still unresolved** (R7 and IR12; `docs/todo.md:110`). This PR changes neither, but MediatR 14.2 and later read their key from `MEDIATR_LICENSE_KEY`. A key could come from the secret store through the chart's environment, with no code change.
- **OpenTelemetry 1.21.0 is out** (11 November). It drops .NET 8 and 9, which doesn't matter here, and will come in a later batch.
- **`FluentValidation.AspNetCore` 11.3.1** is deprecated, built for FluentValidation 11, and runs on 12.2. CI shows it works, and it has nothing to validate (`docs/architecture.md`, 11.4). Removing it would end the question for good.
- **The dev tool keeps running 2.3.0 until it's rebuilt:** `.mcp.json` starts it with `--no-build`. Rebuild it after pulling, with Claude Code's `medapp-dev` stopped, since the running tool locks its files (#100 documents this).
- **What telemetry records hasn't been looked at in the dashboard.** The changelogs show the query redaction is unchanged, but the governance pack's "check what the instrumentation records before exporting traces" (`docs/governance/data-inventory.md:75`) is still open.

## Say it like this

"Dependabot's weekly pull request moved 17 .NET libraries up by minor or patch versions, including an OpenTelemetry release that fixes a high-severity memory-exhaustion advisory. I checked that no licence changed, the signatures and advisories are clean, and validation, the page-rendering fallback and the dev tool behave the same. The trade-off of grouping updates is that licence-sensitive libraries like MediatR arrive inside a 17-package pull request, so until CI checks licences, that check is done by hand."

## Detail

### Every package

| Package | From | To | Licence | In the image | What it means for MedApp |
|---|---|---|---|---|---|
| MediatR | 14.0.0 | 14.3.0 | RPL-1.5 or commercial (unchanged) | Yes | Same unlicensed warning, once per process, now from a background thread |
| FluentValidation (+ DI extensions) | 12.1.1 | 12.2.0 | Apache-2.0 | Yes | No rule MedApp uses changes |
| Microsoft.Extensions.Http.Resilience | 10.10.0 | 10.11.0 | MIT | Yes | `ssr` client still has no retries |
| Microsoft.Extensions.ServiceDiscovery | 10.10.0 | 10.11.0 | MIT | Yes | Nothing |
| OpenTelemetry.Exporter.OpenTelemetryProtocol | 1.17.0 | 1.20.1 | Apache-2.0 | Yes | Only used in development |
| OpenTelemetry.Extensions.Hosting | 1.17.0 | 1.20.1 | Apache-2.0 | Yes | Nothing |
| OpenTelemetry.Instrumentation.AspNetCore | 1.17.0 | 1.20.2 | Apache-2.0 | Yes | Fixes GHSA-6xq4-gr25-qg2x; `error.type` on 5xx |
| OpenTelemetry.Instrumentation.Http | 1.17.0 | 1.20.2 | Apache-2.0 | Yes | Semantic conventions 1.45.0 |
| OpenTelemetry.Instrumentation.Runtime | 1.17.0 | 1.20.1 | Apache-2.0 | Yes | Nothing |
| ModelContextProtocol | 2.3.0 | 2.4.0 | Apache-2.0 | No | Dev tool starts and lists its 19 tools |
| FluentAssertions | 8.8.0 | 8.11.0 | Xceed Community (unchanged) | No | New assertions; tests pass |
| Moq | 4.20.72 | 4.21.0 | BSD-3-Clause | No | Two new warnings (finding 1) |
| Microsoft.NET.Test.Sdk | 17.8.0 | 17.14.1 | MIT | No | Nothing |
| NUnit3TestAdapter | 4.5.0 | 4.6.0 | MIT | No | Nothing |
| NUnit.Analyzers | 3.9.0 | 3.10.0 | Its own file | No | No new warnings |
| coverlet.collector | 6.0.0 | 6.0.4 | MIT | No | 88 fewer branches counted |

### The fix for finding 1

```diff
--- a/MedApp.UnitTests/Application/Common/Security/SecuritySeamTests.cs
+++ b/MedApp.UnitTests/Application/Common/Security/SecuritySeamTests.cs
@@ -30 +30 @@
-            It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("TwoFactorReset") && state.ToString()!.Contains("an.admin")),
+            It.Is<It.IsAnyType>((state, _) => state!.ToString()!.Contains("TwoFactorReset") && state.ToString()!.Contains("an.admin")),
--- a/MedApp.UnitTests/Infrastructure/Common/Identity/DataProtectionKeyCheckTests.cs
+++ b/MedApp.UnitTests/Infrastructure/Common/Identity/DataProtectionKeyCheckTests.cs
@@ -32 +32 @@
-        return logger.Invocations.Count(i => i.Method.Name == nameof(ILogger.Log) && (LogLevel)i.Arguments[0] == LogLevel.Warning);
+        return logger.Invocations.Count(i => i.Method.Name == nameof(ILogger.Log) && (LogLevel)i.Arguments[0]! == LogLevel.Warning);
```

### The rows for finding 2

| Line | Now | After this PR |
|---|---|---|
| 260 | MediatR 14.0.0 | 14.3.0 |
| 261 | FluentValidation 12.1.1 | 12.2.0 |
| 265 | OpenTelemetry 1.17.0 | 1.20.1 (Hosting, OTLP exporter, Runtime), 1.20.2 (ASP.NET Core, HTTP) |
| 266 | Http.Resilience, ServiceDiscovery 10.10.0 | 10.11.0 |
| 268 | ModelContextProtocol 2.3.0 | 2.4.0 |
| 316 | NUnit3TestAdapter 4.5.0, NUnit.Analyzers 3.9.0 | 4.6.0, 3.10.0 |
| 317 | FluentAssertions 8.8.0 | 8.11.0 |
| 318 | Moq 4.20.72 | 4.21.0 |
| 319 | coverlet.collector 6.0.0 | 6.0.4 |

### MediatR's licence check in 14.3.0

- **Where the key comes from,** first non-blank wins (`LicenseAccessor.cs:91-103` at `v14.3.0`): `cfg.LicenseKeyFactory` (from DI), `cfg.LicenseKey`, the static `Mediator.LicenseKey`, then the environment variables `MEDIATR_LICENSE_KEY` and `LUCKYPENNY_LICENSE_KEY`. Whitespace in a key is removed.
- **When it runs:** the first time a `Mediator` is made, so on the API's first request, not at start-up. Once per process, on a dedicated background thread (`MediatRServiceCollectionExtensions.cs:63-95`). In 14.0.0 it ran inline, also once.
- **Offline:** the key is a signed token checked against an embedded public key. Nothing is sent anywhere.
- **With no key:** one warning, "You do not have a valid license key for the Lucky Penny software MediatR…", as before (`LicenseValidator.cs:34-43`).

### GHSA-6xq4-gr25-qg2x, and why MedApp isn't exposed

- **Affected:** `OpenTelemetry.Instrumentation.AspNetCore` from 1.15.2 up to 1.20.1, on .NET 8 to 10, when all of these hold:
  - tracing records the request
  - an endpoint has no route template (`IRouteDiagnosticsMetadata.Route`)
  - that endpoint puts text from the URL in a route value named `page`
  - an attacker can reach it without signing in.
- **The examples it gives:** `MapFallbackToController("Fallback/{*page}", …)`, dynamic controller routes and the old `IRouter` routing.
- **MedApp's open endpoints:** `app.Map("/api/fhir/{**path}", …)`, `app.Map("/api/{**path}", …)` and `app.MapFallback("{*path:nonfile}", SsrPages.Handle)` (`ApplicationBuilderExtensions.cs:70-75`): minimal API routes with templates, whose value is named `path`. No Razor Pages; controllers use attribute routes. So none of the conditions holds.
- **Its state on 17 November:** published by the project on 6 November; not yet in GitHub's global advisory database, which NuGet's audit reads.
