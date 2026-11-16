# Review: PR #62, times sent and read as UTC

**PR:** [#62](https://github.com/Aliator/MedApp/pull/62), branch `fix/utc-times`, reviewed at `29671a7` on top of `main` at `88dc1fa` (the merge of #56). `main` at `864c900` (the merge of #57, which also brings in #58) was merged into the branch afterwards, for this review's row in the index. Nothing in that merge touches times.

**Reviewed:** 16 November 2026, independently of the PR description's conclusions. Every claim below was checked against the code, a build, a test run or the GitHub API. **No finding is high or critical;** all three are low.

## Summary

Times were stored in UTC but left the API without a time zone, so browsers read them as their own local time and showed them an hour early during British Summer Time. This PR marks every time read from the database as UTC, so the JSON ends in `Z`, and makes the web app read any time without a zone as UTC too. It's correct and complete for every time the API sends today; the findings are about hardening, tests and docs.

## What changed, and why

- **The problem (hazard H-12 in the clinical safety hazard log):** SQL Server's `datetime2` column stores a date and time with no zone. EF Core read it back with `DateTimeKind.Unspecified` (.NET's "no zone given" marker). System.Text.Json writes such a value without a `Z` (`"atUtc":"2026-11-15T03:30:25.07"`), and a browser reads a date-time without a zone as its own local time. In summer, UK time is UTC plus an hour, so stage starts and finishes, assignments, a procedure's created and updated times and the audit log all showed an hour early. In winter, UK time equals UTC, so it hid. Think of a note that says "14:05" with no city on it: the database kept the time right, the API passed the note on, and each browser assumed its own clock.
- **Server, a value converter:** `UtcDateTimeConverter` (`MedApp.Infrastructure/Data/Conversions/UtcDateTimeConverter.cs:12-14`) is a **value converter**, EF's hook for changing a value on its way to and from the database. Reading, it keeps the same time and marks it UTC. Writing, it passes the value through unchanged.
- **Server, applied by convention:** `MedAppDbContext.ConfigureConventions` (`MedApp.Infrastructure/Data/MedAppDbContext.cs:28-35`) applies it to every `DateTime` and `DateTime?` in the model, Identity's `ApplicationUser.CreatedAt` included. A **convention** is a rule EF applies to every matching property, so new ones are covered too. The column types don't change, so there's no migration. Back to the note: the converter writes "UTC" on every note as it leaves the database.
- **Web, one parser:** `parseUtc` (`MedApp.Web/src/utils/dates.ts:45-61`) turns an API time into a `Date`. A time with `Z` or an offset is read as given. One with no zone is read as UTC. Anything that isn't a date-time, a date-only `yyyy-mm-dd` included, gives an invalid `Date`.
- **Web, `formatDateTime` uses it** (`dates.ts:67-78`). It used `new Date(value)`, the cause on this side. `timeAgo` already added a `Z` through a private `utc()` helper; that helper is folded into `parseUtc`, which also accepts `+0100` and a lowercase `z`. Every time the web app shows goes through one of the two: `StageList.tsx:31-32`, `AssigneeList.tsx:64`, `ProcedurePage.tsx:385-386`, `AuditLogPage.tsx:79` and `NotificationBell.tsx:108`.
- **Server rendering now agrees with the browser.** The API's Node page server renders each page first, and the browser then takes it over (**hydration**). In the container, Node runs in UTC: `SsrServer.cs:46` passes `TZ` on only if it's set, and the image sets none. Before this PR, Node read a time without a zone as UTC and showed 14:05, while a browser in BST read it as local and showed 13:05. React then had to patch the text after hydration. Now both show 14:05, because `formatDateTime` always formats in `Europe/London` (`dates.ts:76`) and `parseUtc` no longer depends on the machine's zone.
- **Tests:** the converter both ways, a check that every `DateTime` property in the model has it, and a JSON check (`MedApp.UnitTests/Infrastructure/Data/Conversions/UtcDateTimeConverterTests.cs`, `MedApp.UnitTests/Api/Controllers/ProcedureAuditAndNotificationsControllerTests.cs:42-61`). On the web, 24 new cases in `dates.test.ts`, including times without `Z`, seven-digit fractions and the two nights the clocks change.
- **Tests run in New York time:** Vitest sets `TZ=America/New_York` (`MedApp.Web/vite.config.ts:36-38`). That zone is behind UTC and UK time all year, so a time misread as local fails in winter too, and in CI, which runs in UTC.
- **Docs:** root `AGENTS.md:117` and `MedApp.Web/AGENTS.md:53` say times are UTC with a `Z`, how to parse and show them, and that dates aren't shifted.

## Design choices

- **Fix both sides, not one:**
  - **Instead:** only the server (every response gains a `Z`, and `new Date` would then be right), or only the web (`formatDateTime` uses `utc()`). H-12 offered either or both.
  - **Chosen because:** the server fix makes the API's contract right for every consumer (Swagger, the dev tool's output, any future client). The web fallback still reads a time correctly if one ever arrives without a zone: from an older API copy during a rolling deploy, or from a future read that bypasses EF.
  - **Worse at:** the fallback hides a server regression. If the converter were lost, the web app would still look right, and only the .NET model test would notice.
- **One convention for every `DateTime`, not a converter on each property:**
  - **Instead:** `HasConversion` on each time property, or store `datetimeoffset` (a SQL Server type that keeps the offset).
  - **Chosen because:** one line covers every current and future time, with no schema change and no migration. `datetimeoffset` would need a migration and a type change on every time property.
  - **Worse at:** it assumes every `DateTime` is UTC. A future property that holds a local wall-clock time (say, "clinic opens at 09:00") would need its own explicit conversion to opt out. And it trusts what it's given when writing (finding 1).
- **Tests in New York time:**
  - **Instead:** leave tests in the machine's zone, or run the date tests in several zones.
  - **Chosen because:** like checking a currency conversion with an exchange rate that's never 1, a step that forgets to convert can't hide. On a machine in London in winter, and in CI (UTC), a time misread as local passed.
  - **Worse at:** it applies to all 82 test files, not only the date tests. A test that compares a browser-local "today" with a UTC or UK "today" now disagrees for 4 to 5 hours every night, not just for an hour on summer nights (none found; see How it was checked). And no test runs in London time, where staff actually are.
- **`parseUtc` refuses date-only values:**
  - **Instead:** let `new Date('2026-07-01')` through, which reads it as UTC midnight.
  - **Chosen because:** a due date is a whole day, not a moment. As a time it would show as "1 Jul 2026, 01:00" in summer.
  - **Worse at:** `formatDateTime` given a date now returns it raw ("2026-07-01"). No caller passes one.

## How it was checked

- **Read:** the whole diff (9 files, 211 lines added and 11 removed), the PR description, and the code around it:
  - every `DateTime` in `MedApp.Domain` and `MedApp.Contracts`, every place that writes one, and every value the API sends (table under Detail)
  - every comparison and query on times: sessions, lockout, overdue, the due date and audit filters (table under Detail)
  - the notification path to SignalR, the API's JSON settings, the dev tool's writes, and the history of `DateTime.Now` in server code
  - `dates.ts` and every web caller, and server rendering's environment
  - H-12 itself, on the unmerged `docs/clinical-safety` branch
- **Build:** `dotnet build MedApp.sln -c Release` succeeded. Its one warning, CS0414 in `MedApp.Client.Deprecated/Pages/Admin/Users/UsersTable.razor.cs:29`, is in a file this PR doesn't touch.
- **Tests:** `dotnet test MedApp.UnitTests -c Release`: 774 passed. In `MedApp.Web`, `npm run lint` and `npm run typecheck` are clean, and `npx vitest run` passed 703 tests in 82 files.
- **Schema:** `dotnet ef migrations has-pending-model-changes` against the Release build reports "No changes have been made to the model since the last migration". The PR's claim of no migration holds.
- **Do the tests really test the change?** Yes, on the web. In a scratch copy, `formatDateTime` was put back to `new Date(isoUtc)` (a **mutation test**: breaking the code on purpose to see whether the tests notice). 9 of the 41 tests in `dates.test.ts` fail, winter cases included, which fail only because the tests run in New York time. On the server, by reading only: `Model_EveryDateTimeProperty_IsReadAsUtc` would fail if the convention went, since each property's converter would be null. Two other new .NET tests can't fail for the reason they give (finding 2).
- **The test time zone at awkward hours:** the whole web suite was run twice more with the clock moved forward, through a script preloaded into the test processes that's not in the repository:
  - **02:30 UTC on 17 November:** still the 16th in New York. 703 passed.
  - **23:30 UTC on 1 July:** already 2 July in the UK, 19:30 on the 1st in New York. 702 passed and 1 failed: `PatientPages.test.tsx:69-82`, "shows the API's validation errors on the fields and in a summary" (expected the error text more than once, found it once). That test uses no dates. Its file passed alone at the same moment, twice, and without the shift. It's a timing flake under load, not the time zone (see Remaining risks).
- **After merging `main`:** #58's four changed test files and `dates.test.ts`, now in New York time: 88 tests in 5 files passed.
- **CI** (the checks GitHub runs on every PR): run [43828168504](https://github.com/Aliator/MedApp/actions/runs/43828168504) on `29671a7` passed (`web`, `helm`, `unit-tests`, `image` and `coverage-comment`). The coverage comment lists no .NET class or React file under 80%; `UtcDateTimeConverter` is at 100%.
- **Not done:** the API against a real SQL Server to see the `Z` in a response (the PR description quotes the response before the fix); a browser in BST; server rendering in the built image; UI parity screenshots.

## Findings

Most severe first. None is high or critical. All three were open when this review was written.

### 1. Writes aren't checked: a local time would be stored as it is, then read back as UTC

- **Severity:** low. **Confidence:** high on the mechanism; low that it happens soon, as nothing writes a local time today.
- **What could go wrong:** a future handler, seed or tool sets a time with `DateTime.Now`, which has `DateTimeKind.Local`. On a development machine in the UK in summer, 14:05 BST is stored as 14:05, read back as 14:05 UTC, and shown as 15:05: an hour late, now with a confident `Z`. Before this PR, the two errors cancelled on a UK browser and it showed 14:05. In the containers, local time is UTC, so `DateTime.Now` is harmless there; a machine or host with a local zone set isn't. Stage times and the audit log are where an hour matters.
- **Evidence:**
  - `MedApp.Infrastructure/Data/Conversions/UtcDateTimeConverter.cs:13`: `value => value` on the way in
  - `MedApp.UnitTests/Infrastructure/Data/Conversions/UtcDateTimeConverterTests.cs:27-37`: tests `Utc` and `Unspecified`, not `Local`
  - every writer today is UTC: `timeProvider.GetUtcNow().UtcDateTime` in the procedure handlers (e.g. `AssignPatientProcedureHandler.cs:33`), `DateTime.UtcNow` in `CreatePatientHandler.cs:23-24`, `UpdatePatientHandler.cs:32`, `SessionService.cs:23` and `tools/MedApp.DevMcp`, and `TimeZoneInfo.ConvertTimeToUtc` in `DueDates.cs:12-17`
  - no `DateTime.Now` or `DateTime.Today` in server code; only the deprecated Blazor client uses `DateTime.Today`
- **Fix:** convert a local time on the way in: `value => value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value`, with a `Local` test case (sketch under Detail). Optionally, ban `DateTime.Now` and `DateTime.Today` in the server projects with the .NET banned-API analyzer; `AGENTS.md` already says to take a `TimeProvider`.
- **What happened to it:** open when this review was written.

### 2. Two of the new .NET tests can't fail for the reason they give

- **Severity:** low. **Confidence:** high.
- **What could go wrong:**
  - **The date of birth test guards nothing.** `Model_DateOfBirth_KeepsItsOwnDateConversion` reads as proof that dates of birth aren't shifted. But `Patient.DateOfBirth` is a `DateOnly`, and `Properties<DateTime>()` picks properties by their .NET type, so the convention can't reach it whatever happens. The test passes with or without this PR.
  - **The JSON test checks the framework, not the API.** `SearchProcedureAudit_TimeReadFromTheDatabase_IsSentAsUtcWithZ` converts a value by hand, then serialises it with options from a fresh `AddControllers()`, not the API's own `AddApi`. It confirms that System.Text.Json writes a UTC `DateTime` with a `Z`, which is fixed by the framework. Suppose a later change adds a JSON setting to `AddApi`, say a custom `DateTime` converter for an export. Times could lose their `Z` again, and every test would stay green.
  - **Nothing checks the hub's JSON.** SignalR has its own JSON settings (`AddSignalR()`), and no test serialises a pushed notification. They match the controllers' today (Detail).
- **Evidence:**
  - `MedApp.UnitTests/Infrastructure/Data/Conversions/UtcDateTimeConverterTests.cs:49-58`
  - `MedApp.Domain/Patients/Patient.cs:8` (`DateOnly DateOfBirth`), with its own converter at `MedApp.Infrastructure/Data/Configurations/Patients/PatientConfiguration.cs:26-32`
  - `MedApp.Infrastructure/Data/MedAppDbContext.cs:34`
  - `MedApp.UnitTests/Api/Controllers/ProcedureAuditAndNotificationsControllerTests.cs:42-56`, and its helper at `:58-61`, whose comment ("The JSON options the API's controllers write responses with") holds only while `AddApi` adds none
  - `MedApp.API/Common/Extensions/ServiceExtensions.cs:12-16` (`AddApi`, `AddControllers()`) and `:20` (`AddSignalR()`)
  - the wiring itself is covered: `Model_EveryDateTimeProperty_IsReadAsUtc` at `UtcDateTimeConverterTests.cs:39-47`
- **Fix:**
  - Take the options from the API's own registration: `new ServiceCollection().AddApi(new ConfigurationBuilder().Build())`, then `IOptions<JsonOptions>` for the controllers and `IOptions<JsonHubProtocolOptions>` for the hub. Serialise an audit entry with one and a `NotificationResponse` with the other.
  - Drop the date of birth test, or replace it with one that could fail: the JSON of a `PatientResponse` and of a procedure's `DueDate` is `yyyy-mm-dd`.
- **What happened to it:** open when this review was written.

### 3. The docs don't say what `dueDateUtc` is

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** both docs sort the API's values into times and dates. Root `AGENTS.md` says "Due dates and dates of birth are dates, not times, and are never shifted". `MedApp.Web/AGENTS.md` lists times as `...AtUtc`, `createdAt` and `lastUpdated`, and dates as `yyyy-mm-dd`. `dueDateUtc` fits neither:
  - it's sent beside `dueDate` on every procedure and summary
  - it's a moment: the end of the due day, UK time, in UTC
  - it's the one due-date value this PR changes, from `"2026-06-15T22:59:59.9999999"` to `"2026-06-15T22:59:59.9999999Z"`

  Nothing in the web app reads it today. Suppose a new due-date column or a "due in 3 hours" badge reaches for it. With `formatDate` it shows the raw string, as `formatDate` accepts only `yyyy-mm-dd`. With `formatDateTime` it shows "15 Jun 2026, 23:59": the right moment, but a time on a value the docs call a date.
- **Evidence:**
  - `AGENTS.md:117` and `MedApp.Web/AGENTS.md:53`
  - `MedApp.Contracts/Procedures/PatientProcedures/Responses/PatientProcedureResponse.cs:15-16` and `PatientProcedureSummaryResponse.cs:11-12`, set at `MedApp.Application/Procedures/Common/ProcedureMapper.cs:52` and `:129`
  - `MedApp.Web/src/utils/dates.ts:6` and `:34-36` (`formatDate` accepts only a date)
- **Fix:** one clause in each doc: "`dueDateUtc` is the moment the due day ends (UK time), for comparing with now; show `dueDate`." Or remove `dueDateUtc` from the responses, as the server already works out `isOverdue` and nothing reads it. That's a contract change, which needs `npm run gen:api`.
- **What happened to it:** open when this review was written.

## Remaining risks

- **"Today" is still the machine's day in the web app, and server rendering runs in UTC.** Not changed by this PR; times now match, but days don't.
  - **Where:** `todayIso()` (`dates.ts:18-22`) and `dayFromToday()` (`ListFilterBar.tsx:54-59`) use the machine's local date. They set the date of birth limit (`DateOfBirthInput.tsx:38`), the earliest due date (`NewProcedurePage.tsx:104`), ages (`PatientsListPage.tsx:86`, `ViewPatientPage.tsx:27`), and the Today, Last 7 days, Under 18 and 65 and over quick filters (`AuditLogPage.tsx:34-35`, `PatientsListPage.tsx:157-158`).
  - **When it goes wrong:** from 23:00 to midnight UTC in summer, Node's day is the day before the browser's. An age on a birthday, or a date limit, can render one way on the server and another in the browser.
  - **The server disagrees with itself too:** the earliest due date is the UK day (`CreatePatientProcedureValidator.cs:32`), but the latest date of birth is the UTC day (`CreatePatientValidator.cs:25`).
  - **Fix, as a to-do:** a UK-day helper in `dates.ts` that formats now in `Europe/London`, used for every "today".
- **The web fallback can hide a server regression.** If the converter were lost, the web app would still look right while the API's JSON lost its `Z` for every other consumer. Only the .NET model test guards it.
- **The hazard log still lists H-12 as open.** It's on the unmerged `docs/clinical-safety` branch: `hazard-log.md:42` ("Open, to verify"), and `clinical-safety-case-report.md:132` and `:166`. Whichever of the two merges second should record the cause as confirmed and the fix as built in #62.
- **A flaky test, unrelated to this PR:** `PatientPages.test.tsx:80` counts the error text straight after the alert appears. Under load, the field's copy can arrive a moment later. Wrapping the check in `waitFor` would fix it.
- **Not seen end to end** against SQL Server, or in a browser during British Summer Time.

## Say it like this

"Times were stored correctly but sent without a time zone, so browsers read them as their own local time and showed them an hour early in summer. This PR marks every time read from the database as UTC, so the API's JSON ends in Z, and the web app now reads any time without a zone as UTC as well. Due dates and dates of birth are dates, not times, and don't change. The web tests now run in New York time, so the old mistake fails all year, not only in summer. Nothing found is serious: the converter trusts whatever it's given when writing, two of the new .NET tests can't catch what they're named for, and the docs don't say what `dueDateUtc` is."

## Detail

- **Every time the API sends, and where it comes from:**

  | JSON field | Response | Comes from | Before → after |
  |---|---|---|---|
  | `createdAtUtc` | notification | the database (the list), or the assign handler's `now` (the live push, `AssignPatientProcedureHandler.cs:50`) | list: no `Z` → `Z`; push: `Z` already |
  | `atUtc` | audit entry | the database | no `Z` → `Z` |
  | `assignedAtUtc`, `startedAtUtc`, `completedAtUtc`, `createdAt`, `lastUpdated` | procedure, stage, assignment, summary | the database, or `ProcedureChange`'s `nowUtc` for what a command just changed | no `Z` → `Z`; new values had one already |
  | `dueDateUtc` | procedure, summary | the database, or `DueDates.EndOfDayUtc` (`TimeZoneInfo.ConvertTimeToUtc`, so UTC) | no `Z` → `Z` (finding 3) |
  | `expiresAtUtc` | login | `SessionService.cs:23-24` (`DateTime.UtcNow`); never read back from the database | `Z` both |
  | `updatedAtUtc` | settings | the database | no `Z` → `Z` |
  | `dueDate`, `dateOfBirth` | procedure, summary, patient | `DueDates.ToUkDate` (`DueDates.cs:20-21`), and a SQL `date` column; both `DateOnly` | `yyyy-mm-dd` both: unaffected |

  No response is built with an unmarked `DateTime` outside EF: every handler takes `timeProvider.GetUtcNow().UtcDateTime` or `DateTime.UtcNow`. No query projects a time outside an entity, and there's no raw SQL.
- **Every comparison and write, and what the change does to it:**

  | What | Where | Effect |
  |---|---|---|
  | Session expiry | `UserSession.cs:18-19`, `SessionService.cs:44` | none: `>` compares the times, not their zone markers |
  | Session cookie's expiry | `SessionCookieService.cs:12`, `:34` | none: from the login's `LoginResult`, already UTC |
  | Lockout | `IdentityAuthenticationService.cs:45-46` | none: `DateTimeOffset`, which the convention doesn't touch |
  | Overdue | `ProcedureWorkflow.cs:51-52`; in SQL, `ProcedureSearch.cs:94` | none |
  | Due date and audit filters | `ProcedureSearch.cs:107-113`, `ProcedureAuditSearch.cs:45-52` | none: bounds from `DueDates` (UTC), passed to SQL unchanged |
  | A stored due date as a UK day | `DueDates.cs:20-21` | none: it already marks the value UTC itself |
  | Anything saved back | every repository's `SaveChangesAsync` | none: the converter writes values as they are, and SQL Server ignores the marker |

  No server code calls `ToLocalTime`, `ToUniversalTime`, `TimeZoneInfo.ConvertTime(DateTime, …)` or `new DateTimeOffset(DateTime)`, or formats a time with `"o"` or `"K"`. Those are the calls whose results depend on the marker. The only `DateTime.Now` ever in server code was the template's weather sample, removed in `c51795f`.
- **SignalR:** a live notification is built from the handler's `now`, which is UTC, so pushes had a `Z` even before this PR while the same notification from `GET api/notifications` didn't (`NotificationSender.cs:10-11`, `SignalRNotificationPublisher.cs:16-17`). Now both do. The hub uses SignalR's own JSON settings, `AddSignalR()`'s defaults, which write a UTC `DateTime` the same way. With the Valkey backplane, the message crosses already serialised.
- **Server rendering, before and after,** for the stored time `2026-07-01T13:05:00`:

  | Where | Machine's zone | Before | After |
  |---|---|---|---|
  | Node in the container | UTC | 1 Jul 2026, 14:05 | 1 Jul 2026, 14:05 |
  | Browser in the UK, summer | Europe/London | 1 Jul 2026, 13:05 (React patches the page) | 1 Jul 2026, 14:05 |
  | Node in development | the machine's (London) | 1 Jul 2026, 13:05 (wrong, but matching) | 1 Jul 2026, 14:05 |

  `timeAgo` can still differ between server and browser by a minute boundary, as it counts from now; that's unchanged, and the notification list sits in a popover that starts closed (`NotificationBell.tsx:121-129`), so it's unlikely to be in a server-rendered page.
- **A sketch of the fix for finding 1:**

  ```csharp
  public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
      // A local time (DateTime.Now) is turned into UTC, so it isn't stored as a wall-clock time and read back an hour out.
      value => value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value,
      value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
  ```

  Plus `[TestCase(DateTimeKind.Local)]` in a test expecting the UTC equivalent, with `Kind` UTC.
- **A sketch of the fix for finding 2's JSON test:**

  ```csharp
  private static IServiceProvider Api() =>
      new ServiceCollection().AddApi(new ConfigurationBuilder().Build()).BuildServiceProvider();

  // Controllers: Api().GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions
  // The hub:     Api().GetRequiredService<IOptions<JsonHubProtocolOptions>>().Value.PayloadSerializerOptions
  ```
- **The clock shim** used for the runs at awkward hours, preloaded with `NODE_OPTIONS=--import=…`: it replaced `Date` with a subclass whose `new Date()` and `Date.now()` run from a chosen moment without freezing. A probe test confirmed that the Vitest worker processes saw the moved clock.
