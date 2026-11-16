# Review: PR #66, an admin Insights page

**PR:** [#66](https://github.com/Aliator/MedApp/pull/66), branch `feature/admin-insights`, reviewed at `c126a2c` on top of `main` at `a506079` (the merge of #61). `main` has since moved to `4d05f0b` (the merge of #63); it was merged into the branch for this review's index row (one conflict, in `AGENTS.md`'s list of mappers, resolved by keeping both sides), and the merged branch was built and tested again.

**Reviewed:** 16 November 2026, independently of the PR description's conclusions. Every claim below was checked against the code, a build or test run, the SQL that EF generates, a scratch program or the GitHub API. **No finding is high or critical;** four are medium and nine are low.

## Summary

A new admin-only page, Insights, shows headline counts against the same point last week, overdue work by assignee and by priority, procedures created and completed per UK week, and the average waiting and working time at each stage. The totals are worked out in SQL; last week's statuses are rebuilt from the audit log. The server side is careful and well tested, and the chart library stays out of the main bundle. The weak spots are where the numbers meet the procedures list: two of the drill-down links open lists that can't match their counts, and last week's figures quietly use today's due dates. The audit log query will also slow down as the history grows.

## What changed, and why

68 files, 3,825 lines added and 28 removed (`git diff --stat a506079 c126a2c`); 385 of the changed lines are in `package-lock.json` and 124 in the generated `schema.d.ts`.

- **Why:** the to-do list asked for an admin overview of overdue and in-progress work, with graphs: headline counts, overdue work by assignee and priority, time in each stage, and created against completed. This PR delivers it and removes the item from `docs/todo.md`.
- **The endpoint:** `GET api/procedures/insights?Weeks=&TemplateId=` (`ProcedureInsightsController`, `[Authorize(Roles = "Admin")]`). `Weeks` is 1 to 52 (12 by default); an unknown template is a 404.
- **The query and its parts** (`MedApp.Application/Procedures/Insights`):
  - `GetProcedureInsightsHandler` asks the repository for nine totals, one after another, then the usernames of the assignees, and `ProcedureInsightsMapper` shapes the response.
  - `ProcedureInsightsQueries` holds the LINQ for each total, written once and run two ways: by `ProcedureInsightsRepository` in SQL, and by the unit tests in memory.
  - `InsightsPeriod` works out the UK weeks (Monday 00:00 UK time, as UTC) and "the same point last week": as long after last Monday 00:00 as now is after this Monday 00:00.
  - `InsightsFunctions.SecondsBetween` is mapped to SQL Server's `DATEDIFF_BIG(second, …)` in `InsightsDbFunctions`. No migration.
- **Last week's statuses come from the audit log.** Each status change is an audit entry whose details are JSON like `{"from":"InReview","to":"Completed"}`. A procedure's status at a past moment is the `"to"` of its last change before then, found by matching text such as `"to":"Completed"` in the details, or Unassigned if there was none. Think of rebuilding where everyone was last Tuesday from a door-entry log.
- **Stage times:** a stage is "reached" when the stage before it was first finished (or the procedure created). It waits until it's started, then it's worked on until it's first finished. "First" comes from the audit log, because unlocking a stage and finishing it again moves the stage's own finish time.
- **A new list filter:** `NobodyAssigned` on `GET api/procedures/patient-procedures/search`, and an "Assigned to" filter on the procedures list (`assignee=<user ID>` or `assignee=none` in the URL). Admins get every account (the first 100) as options; staff, who can't list accounts, get "Nobody" only.
- **The web page:** `/admin/insights` (admin only, in the side nav and on G then I). Four tiles, four chart cards, a period selector (4, 12 or 26 weeks) and a template selector. Each tile and each overdue bar or table row links to the procedures list, filtered.
- **Chart composites** in `src/components/charts/`: `ChartCard` (title, a summary in words, and a Show Table button), `BarChart` (Recharts, coloured from theme tokens, drawn only once its width is known, so nothing is drawn on the server), `ChartTable` and `StatTile`.
- **A new dependency:** `recharts` 3.10.1, pinned exactly, MIT. It brings about 30 packages, all MIT, ISC or BSD-3-Clause (Redux Toolkit, Immer, parts of D3). The page's query lives in its own module so the route's loader doesn't pull Recharts into the main bundle.
- **Docs:** an Insights section in `AGENTS.md`, the charts in `MedApp.Web/AGENTS.md`, the routes and endpoints tables in `docs/architecture.md`, and three UI parity screens.
- **Tests:** .NET tests for the period, each query (in memory), each query's SQL translation (no database), the mapper, the handler, the validator and the controller; web tests for each composite, the page, the route guards and server rendering.

## Design choices

- **Past statuses rebuilt from the audit log's JSON text:**
  - **Instead:** store each status change's target in a typed column on the audit entry, or keep a daily snapshot of the counts.
  - **Chosen because:** no migration, and the audit log already holds the history (every procedure change goes through `ProcedureChange`, which audits it).
  - **Worse at:** the details' wording is now a contract (`AGENTS.md` says to keep it); text matching on `nvarchar(max)` can't use an index (finding 4); procedures from before the audit log have no history (finding 5); and only the status is rebuilt, not the due date (finding 3).
- **One LINQ for SQL and the tests:**
  - **Instead:** hand-written SQL or a view, tested against a database.
  - **Chosen because:** it follows `ProcedureSearch`'s pattern, and the in-memory tests run in milliseconds.
  - **Worse at:** the tests prove the rules and that EF can translate them, not what SQL Server returns (finding 11).
- **UTC hours summed into UK weeks in C#:**
  - **Instead:** convert to UK time in SQL (`AT TIME ZONE`) and group by week there.
  - **Chosen because:** UK offset changes always fall on the hour, so hourly UTC buckets split cleanly into UK weeks, and the same code runs in memory.
  - **Worse at:** up to 8,736 rows per series come back for 52 weeks, which is still small.
- **"The same point last week" measured as elapsed time:**
  - **Instead:** the whole of last week, or the same UK clock time last week.
  - **Chosen because:** on a Tuesday morning it compares two days with two days, not with seven.
  - **Worse at:** around a clock change it's an hour off, and once a year it runs into this week (finding 8).
- **Recharts, drawn only in the browser, in the page's own chunk:**
  - **Instead:** hand-drawn SVG bars, or drawing on the server at a fixed width.
  - **Chosen because:** Astryx's dashboard templates use Recharts; drawing after measuring avoids a hydration mismatch, and the box keeps its height so nothing jumps.
  - **Worse at:** a 110 kB (gzipped) download the first time an admin opens the page, about 30 more packages to keep up to date, and the charts appear a moment after the page.
- **Drill-down through the existing procedures list's URL filters:**
  - **Instead:** a list built for Insights, or new list filters matching each count exactly.
  - **Chosen because:** no new list, and the links are shareable.
  - **Worse at:** the list's own rules (a setting that starts it on "Assigned to me", a template filter by name, no "completed when" filter) change what a link opens (findings 1, 2 and 6). Like a signpost that reads "3 this week" and leads to the whole archive.
- **Completions counted as events, overdue counted once per assignee:**
  - **Instead:** count distinct procedures.
  - **Chosen because:** simple totals in one pass, and a shared procedure really is on each person's plate.
  - **Worse at:** a procedure completed, reopened by an admin and completed again counts twice; the assignee bars add up to more than the overdue total. The response's comments say so; the page doesn't.

## How it was checked

- **Read:** the whole diff, the PR description, and the code around it: `ProcedureChange`, `ProcedureAudit` and `ProcedureWorkflow`, every procedure command handler (to find which can write two status changes at once: only finishing the last stage of an unstarted procedure), `ProcedureSearch`, the audit table's migration and indexes, the procedures list page and its list settings, the half view's router and `RouterLink`, `preload` and the server rendering timeout, the theme tokens in `astryx.css` and `medapp.css`, and Recharts' use of `react-is`.
- **.NET, at `c126a2c`:** `dotnet build MedApp.sln -c Release`, 0 errors and 1 warning, in the deprecated Blazor client (`UsersTable.razor.cs:29`), which this PR doesn't touch. `dotnet test MedApp.UnitTests -c Release`: 882 passed.
- **Web, at `c126a2c`:** `npm run lint` and `npm run typecheck` clean. `npx vitest run`: 765 of 766 passed; "lets an admin into Insights…" hit Vitest's 5-second timeout (finding 11). Run alone, it passed in 620 ms.
- **After merging `main` (`151e997`):** the build is unchanged (0 errors, the same warning); 904 .NET tests and 818 web tests passed, lint and typecheck clean.
- **The bundle:** `npm run build` succeeds. Recharts is only in the lazily loaded `insights-*.js` chunk (375.6 kB, 109.6 kB gzipped); none of the chunks the entry loads statically contain it, and the entry chunk is 125.8 kB. The one chunk over 500 kB is `RichTextField`, which predates this PR.
- **The SQL, without a database:** a scratch program outside the repository printed what EF generates for last week's statuses, completions by hour with a template, stage durations and overdue by assignee (excerpt under Detail). It also ran `InsightsPeriod` around the clock changes in 2026 and 2027.
- **Supply chain:** read `package-lock.json`: `recharts` is pinned to 3.10.1 with an integrity hash from the npm registry; every new package is MIT, ISC or BSD-3-Clause.
- **CI:** all five jobs passed on `c126a2c` (run 43834867352).
- **Not done:** no query was run against SQL Server here, no browser was opened, and no axe audit or parity screenshots were taken. The PR says it ran the queries on LocalDB and axe in three themes before merging `main`; that isn't committed.

## Findings

Most severe first. None is high or critical. All thirteen were open when this review was written; each says what happened to it.

### 1. Every drill-down link is narrowed to "Assigned to me" for an admin whose lists start there

- **Severity:** medium. **Confidence:** high (read from the code; the list's own test shows the behaviour).
- **What could go wrong:** Account Settings has "procedure lists start on Assigned to me". An admin with it on sees "Overdue by assignee: Nobody, 4" and clicks the bar. The list opens with `overdue=true&assignee=none` and, from the setting, "Assigned to me" too: procedures assigned to nobody and to them, which is always empty. Clicking a colleague's bar shows only the procedures shared with the admin; each tile shows only the admin's own. The "Assigned to me" token is on screen, but nothing says why it's there, and the count looks wrong.
- **Evidence:**
  - `MedApp.Web/src/pages/insights/insightsText.ts:38-47`: the links never set `mine`
  - `MedApp.Web/src/pages/procedures/ProceduresListPage.tsx:107` and `:135`: `const mine = search.mine ?? startsOnMine`
  - `MedApp.Web/src/app/listSettings.tsx:18` and `:35`: the setting
  - `MedApp.Web/src/pages/procedures/ProceduresListPage.test.tsx:93`: "starts on Assigned to me when Account Settings says so, until its filter is removed (mine=false)"
- **Fix:** add `mine: 'false'` to every link `proceduresHref` builds (diff under Detail), so the URL overrides the setting, as the list already supports, and test it in `insightsText.test.ts` and with the setting on in `InsightsPage.test.tsx`.
- **What happened to it:** **Fixed.** `proceduresHref` adds `mine=false` to every link (`insightsText.ts`), tested in `insightsText.test.ts`, and in `InsightsPage.test.tsx` with Account Settings starting lists on Assigned to me.

### 2. "Completed this week" opens every completed procedure, from any week

- **Severity:** medium. **Confidence:** high.
- **What could go wrong:** the tile says "Completed this week: 3". It links to `/procedures?status=Completed`, which lists every procedure whose status is Completed now: 140 of them, from any date. Those completed this week and archived since aren't in it, and a procedure completed, reopened and completed again counted twice on the tile. The list has no "completed when" filter, and procedures don't store when they were completed, so no URL can match the tile. The page's own test pins the link as it is.
- **Evidence:**
  - `MedApp.Web/src/pages/insights/InsightsPage.tsx:157-164`: the tile and its link
  - `MedApp.Application/Procedures/Insights/Common/ProcedureInsightsQueries.cs:84-92`: completions counted per status-change event
  - `MedApp.Application/Procedures/PatientProcedures/Queries/SearchPatientProcedures/ProcedureSearch.cs:55-68`: the status filter is the current status only
  - `MedApp.Web/src/pages/insights/InsightsPage.test.tsx:29-32`: the test expects `/procedures?status=Completed`
- **Fix:** either make this tile a plain card with no link and the label "Marked completed this week", or count distinct procedures (`Select(e => e.PatientProcedureId).Distinct().Count()`) and link to the audit log filtered to status changes since this Monday (`/admin/audit?action=StatusChanged&from=…`), where the completions are among the rows listed. Say in the throughput summary that completions are times marked completed.
- **What happened to it:** **Fixed (no link).** The tile is a plain card: `StatTile` draws a `Card`, not a link, when it has no `href`. The page test now checks it isn't a link. Completions are still counted as times marked completed, as `AGENTS.md`'s Insights section says.

### 3. Last week's overdue count uses today's due dates

- **Severity:** medium. **Confidence:** high on the mechanism; how often it matters depends on how often due dates move.
- **What could go wrong:** last Monday 12 procedures were overdue. On Friday, nine of them get new due dates next month. On Monday the Overdue tile reads "3. Same as this time last week": rebuilt with today's due dates, those nine weren't past due last week either. The drop from 12 to 3 disappears. A due date brought forward does the opposite and makes last week look worse. Due-date changes are in the audit log, so the history exists; only the status is rebuilt from it. Nothing on the page says so; the caveat is in a code comment only.
- **Evidence:**
  - `MedApp.Application/Procedures/Insights/Common/ProcedureInsightsQueries.cs:49-51` ("due dates are today's") and `:78` (`IsPastDue = p.DueDateUtc < atUtc`)
  - `MedApp.Application/Procedures/PatientProcedures/Commands/UpdatePatientProcedure/UpdatePatientProcedureHandler.cs:39-43`: a due date change is audited with its `from` and `to`
  - `AGENTS.md`, the Insights section: says past statuses come from the audit log, not that due dates don't
- **Fix:** rebuild the due date too: a procedure's due date at the moment is the `from` of its first `dueDate` change after it, or today's if there's none. The few `DetailsUpdated` entries after the moment can be read and parsed in C#, then the counts adjusted, or `JSON_VALUE` can be mapped like `SecondsBetween`. At the least, say on the tile ("by today's due dates") and in `AGENTS.md` what last week's overdue count means.
- **What happened to it:** **Fixed.** Last week's due date is rebuilt too: the `from` of its first due date change after the moment (`changes.dueDate.from`, read with `JSON_VALUE`, mapped like `SecondsBetween` as `InsightsFunctions.JsonValue`), or the due date now. The days are compared as UK days. Unit tests cover a date moved on, one brought forward, several changes, and one before the moment; `InsightsTests` checks it on SQL Server.

### 4. Last week's statuses read the whole audit history on every load, and will slow down as it grows

- **Severity:** medium. **Confidence:** medium (reasoned from the generated SQL and the indexes; nothing was measured).
- **What could go wrong:**
  - **Last week's statuses:** for every procedure ever created before last week, archived ones from years ago included, the SQL runs a correlated `TOP(1)` over that procedure's audit entries up to the moment. The only index that helps is `(PatientProcedureId, AtUtc)`, which holds neither `Action` nor `Details`, so each of those entries, notes and assignments included, is looked up in the table, and the second sort key (a `LIKE` on `Details`) stops it ending early. The work grows with the whole history, not the chosen period.
  - **Stage durations:** a "first finish" subquery appears six times per stage in the generated SQL (the stage's own twice in `WHERE`, then twice in each clamped average: the stage before's for waiting, its own for working), and every stage row is scanned, as `CompletedAtUtc` has no index.
  - **The effect:** a clinic adding 50 procedures a day, at about 20 audit entries each, has about 1.8 million entries after five years. At that size this query could take several seconds per page load, and the page's server rendering waits up to 15 seconds for it, then falls back to rendering in the browser, which asks again. EF's 30-second command timeout would show "The insights could not be loaded". Every procedure change made in a half view beside Insights refetches it (the query is keyed under `['procedures']`).
- **Evidence:**
  - `MedApp.Application/Procedures/Insights/Common/ProcedureInsightsQueries.cs:53-81` (statuses then) and `:134-188` (stage durations); the generated SQL is under Detail
  - `MedApp.Infrastructure/Data/Configurations/Procedures/PatientProcedures/PatientProcedureAuditEntryConfiguration.cs:36-39`: the indexes
  - `MedApp.Web/src/api/preload.ts:12-15` and `MedApp.API/ServerRendering/SsrOptions.cs:22`: server rendering waits for the loader, up to 15 s
- **Fix:**
  - Rebuild only what changed: a procedure's status at the moment is the `from` of its first status change after it, or its current status if there's none (sketch under Detail). Only procedures changed in the last week then need the subquery, and finding 5 is fixed too.
  - Add an index on `(PatientProcedureId, Action, AtUtc)` including `StageId`, in a migration.
  - Compute each stage's first finish once (a grouped subquery joined to the stages), and clamp with `Math.Max`, which EF translates to `GREATEST` on SQL Server 2022 and Azure SQL.
  - Before relying on it, time the page on a LocalDB with a few million synthetic audit entries.
- **What happened to it:** **Fixed, and measured; the index is a to-do.** Last week's state is rebuilt from the entries after the moment (the Detail sketch), so only recent history is read. The same-moment tie is now a filter, not a second sort key: sorting on the details stopped SQL Server seeking `(PatientProcedureId, AtUtc)`, and it scanned the whole audit table for every procedure. Each stage's first finish is a `TOP(1)` by time, which seeks the same index, worked out once per stage in a derived table (`Distinct` over rows that are already distinct). `Math.Max` isn't translated at this database's compatibility level, so the clamps stay as `CASE`. Timings are under "After the review" below. The index on `(PatientProcedureId, Action, AtUtc)` including `StageId` is in `docs/todo.md`, with its measurement, for a migration of its own.

### 5. Procedures from before the audit log count as open last week, for good

- **Severity:** low. **Confidence:** high on the mechanism; any database migrated through `AddedProcedureWorkflow` with procedures already in it is affected (likely only development databases).
- **What could go wrong:** the audit table was created with no history for the procedures already there. A procedure completed before then, and never touched since, has no status change at all, so it's rebuilt as Unassigned: open last week, every week. Ten of them make "Open: 14, 10 fewer than this time last week" every Monday.
- **Evidence:** `MedApp.Infrastructure/Migrations/20261114045707_AddedProcedureWorkflow.cs:141-160` (the table, with nothing back-filled); `ProcedureInsightsQueries.cs:74-77` (`?? Unassigned`).
- **Fix:** finding 4's rebuild falls back to the current status when nothing changed after the moment, which is right for these too.
- **What happened to it:** **Fixed** by finding 4's rebuild: with no change since the moment, a procedure counts with its status now. Tested (`StatusCountsAt_NoChangeSinceTheMoment_IsItsStatusNow`, which includes one with no history at all).

### 6. The template filter in each link matches by name, which misses renamed templates

- **Severity:** low (the "contains" match is a known caveat). **Confidence:** high.
- **What could go wrong:** with a template chosen, each link adds `template=<its name>`, and the list keeps procedures whose copied template name contains it. "Knee" also picks up "Knee review". And a procedure keeps the name its template had when it was made: rename "Knee review" to "Knee follow-up" and every older procedure drops out of the linked list, while Insights, which filters by the template's ID, still counts them. The time-at-stage heading has the same gap: it shows the alphabetically last of the names procedures were made under, not the template's name now.
- **Evidence:**
  - `MedApp.Web/src/pages/insights/insightsText.ts:38-41` and `MedApp.Web/src/pages/procedures/ProceduresListPage.tsx:114`
  - `MedApp.Application/Procedures/PatientProcedures/Queries/SearchPatientProcedures/ProcedureSearch.cs:43-44` (by name) and `:52-53` (the API already filters by `TemplateId`)
  - `MedApp.Application/Procedures/PatientProcedureStages/Templates/Commands/UpdatePatientProcedureStageTemplate/UpdatePatientProcedureTemplateHandler.cs:19-24`: templates can be renamed
  - `ProcedureInsightsQueries.cs:179` and `ProcedureInsightsMapper.cs:84`: the heading's name
- **Fix:** add a `templateId` URL filter to the procedures list, passed to the API's existing `TemplateId`, and link with it; show it as a token named after the template. For the heading, take the name from the templates query the page already loads.
- **What happened to it:** **Fixed.** The procedures list has a `templateId` URL filter, passed to the API's `TemplateId` and shown as a "From template" token named as the template is now ("Selected template" if it can't be found). Every link uses it; the name filter stays. Time-at-stage headings take the template's name now from the templates the page loads.

### 7. With one template chosen, deleted procedures drop out of the counts; with all templates, they don't

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** "All templates" counts the creations and completions of procedures deleted since (the audit log keeps them). With a template chosen, those entries are matched to existing procedures, so deleted ones drop out, and the per-template weeks don't add up to the total. Separately, last week's statuses only count procedures that still exist, so one open last week and cancelled and deleted since is missing from last week's Open count.
- **Evidence:** `ProcedureInsightsQueries.cs:34-41` (its comment says so) and `:53-58`; `MedApp.Infrastructure/Data/Repositories/ProcedureInsightsRepository.cs:14-15`.
- **Fix:** say it in `AGENTS.md`'s Insights section. To make them agree, the `Created` entry's details already hold the template's name; storing the template's ID there too would let deleted procedures be filtered by template.
- **What happened to it:** **Documented** in `AGENTS.md`'s Insights section: with one template, deleted procedures drop out of the audit log's counts, and last week's statuses count only procedures that still exist.

### 8. Once a year, "last week up to now" runs into this week

- **Severity:** low. **Confidence:** high (run with the real `InsightsPeriod`).
- **What could go wrong:** the week the clocks go forward is an hour short. In the last hour of the next week (Sunday 4 April 2027, 23:00 to midnight UK time), "the same point last week" falls after last week ended: at 23:30 it's 00:30 on this Monday. Completions in this week's first half hour are counted in both "this week" and "last week", and last week's statuses are taken from inside this week.
- **Evidence:** `MedApp.Application/Procedures/Insights/Common/InsightsPeriod.cs:28`; the probe's output is under Detail. The tests cover the clocks going back, not forward (`InsightsPeriodTests.cs:34`).
- **Fix:** cap it: `SameMomentLastWeekUtc = Min(lastWeekStart + (now - thisWeekStart), ThisWeek.StartUtc)`, with a test for 4 April 2027.
- **What happened to it:** **Fixed.** `SameMomentLastWeekUtc` stops at the start of this week, with tests at 23:30 and 22:30 BST on 4 April 2027.

### 9. Changing the template shows the old numbers under the new name, with no sign of loading

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** the query keeps the last result on screen while the next loads (`keepPreviousData`). The subtitle and every link take the template from the URL, which changes at once. So for as long as the new query takes (seconds, given finding 4), the page shows the previous template's counts under the new template's name, and the links open the new template's lists. Nothing shows that it's still loading.
- **Evidence:** `MedApp.Web/src/api/insights.ts:21`; `MedApp.Web/src/pages/insights/InsightsPage.tsx:75-77` and `:117-119`.
- **Fix:** name the template from `data.templateId` (what the numbers are for), and while `isPlaceholderData` is true, say "Updating…" in the subtitle and dim the content.
- **What happened to it:** **Fixed.** The page names the template from `data.templateId` (what the numbers are for), and while `isPlaceholderData`, the subtitle says "Updating..." and the content is dimmed and `aria-busy`. Tested by switching template while the new answer is held back.

### 10. The four Show Table buttons all have the same name

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** a screen reader's list of buttons reads "Show Table" four times, with nothing to tell them apart without moving to each one's card. The parity tool relies on the shared name, too.
- **Evidence:** `MedApp.Web/src/components/charts/ChartCard.tsx:37` and `:39`.
- **Fix:** give each an accessible name with its card's title ("Show Overdue by assignee as a table"), and keep the visible label.
- **What happened to it:** **Fixed.** Each button's accessible name includes its card: "Show Table of Overdue by assignee" (the visible label stays, and is the start of the name, for WCAG 2.5.3). The parity screen and tests use the new names.

### 11. The tests don't pin admin-only access, don't run the SQL, and one timed out

- **Severity:** low. **Confidence:** high.
- **What could go wrong:**
  - The controller test doesn't check `[Authorize(Roles = "Admin")]`, and the API has no fallback policy. Deleting the attribute would make the endpoint open to anyone, signed in or not (usernames and counts), and every test would still pass.
  - `ProcedureInsightsSqlTests` checks that SQL is produced (it contains `GROUP BY`, `TOP(1)`, `DATEDIFF_BIG`), not what it returns. The same-moment tie-break, the `LIKE` matches and the `ISNULL` fallbacks are only proven in memory.
  - In the first full web run here, "lets an admin into Insights, loading it for the period in the address" hit Vitest's 5-second timeout. It loads the split page chunk with Recharts during the test. It passed alone in 620 ms, and in the full run after merging `main`; CI passed. It's a likely flake on a busy runner.
- **Evidence:** `MedApp.UnitTests/Api/Controllers/ProcedureInsightsControllerTests.cs:17-31`; `MedApp.API/Controllers/ProcedureInsightsController.cs:81`; `MedApp.API/Common/Extensions/ServiceExtensions.cs:65-72`; `MedApp.UnitTests/Infrastructure/Data/ProcedureInsightsSqlTests.cs:35-61`; `MedApp.Web/src/app/routeGuards.test.tsx:43`.
- **Fix:** a test that reads the action's `AuthorizeAttribute` and expects `Roles == "Admin"`; the insights queries run on a real SQL Server with synthetic rows, in `MedApp.IntegrationTests` (empty today); and a longer timeout for that route test, or a preload of the route's chunk before it.
- **What happened to it:** **Fixed.** `ProcedureInsightsControllerTests.GetProcedureInsights_IsForAdminsOnly` reads the attribute. `MedApp.IntegrationTests` (merged in #65) now has `Procedures/InsightsTests.cs`: staff get 403; an admin gets the counts, a 404 for an unknown template and a 400 for 0 weeks; and last week's statuses, stage times and hourly counts run on SQL Server 2022 and match the same LINQ in memory on the same rows. The route test has a 15-second timeout.

### 12. Recharts' `react-is` resolves to version 17, under React 19

- **Severity:** low. **Confidence:** high on the version; nothing breaks today.
- **What could go wrong:** Recharts declares `react-is` as a peer dependency, and npm satisfied it with the 17.0.2 copy that `pretty-format` (a testing tool) already brought in, now a production dependency. `react-is` 17 doesn't recognise React 19's elements. Recharts 3 uses it only to find `<Cell>` children inside fragments, which these composites don't use. A later chart that colours bars one by one with `<Cell>` inside a fragment would lose them silently.
- **Evidence:** `MedApp.Web/package-lock.json:6338-6342`; `node_modules/recharts/lib/util/ReactUtils.js:51` and `node_modules/recharts/lib/cartesian/Bar.js:536`.
- **Fix:** add `react-is` at the React version, pinned (`"react-is": "19.3.1"`), to `dependencies`.
- **What happened to it:** **Fixed.** `react-is` 19.3.1 is a pinned dependency; `npm ls` shows Recharts using it.

### 13. Smaller things

- **Severity:** low. **Confidence:** high.
- **What could go wrong, and where:**
  - **Only the first 100 accounts** are offered in the "Assigned to" filter (`ProceduresListPage.tsx:142`). In a larger team, later names can't be chosen, and a link to one shows "Selected person".
  - **Several deleted accounts** each get a bar named "Deleted user", which can't be told apart (`insightsText.ts:34`).
  - **The handbook's roadmap** still lists the same dashboards as future work (`docs/architecture.md:2049`, "Reporting"); a note that Insights is a first version would keep it honest.
- **Fix:** search accounts as the filter is typed, or page through them; add the first characters of the user ID to "Deleted user"; add the note.
- **What happened to it:** **Fixed.** Several deleted accounts are "Deleted user 1", "Deleted user 2" and so on (one is still "Deleted user"); the 100-account limit is explained in a comment where the accounts are asked for; the handbook's roadmap says Insights is a first version of the Reporting step.

## After the review

All thirteen findings were dealt with on the branch (each says how), and `main` was merged in again (#62 to #65).

**Timing on a large database.** A scratch program outside the repository filled a LocalDB database with 20,000 procedures over five years and 342,627 audit entries (status changes, assignments, due date changes, stages started, notes and finishes, in the proportions the app writes them), and timed the repository's nine queries, each the second of two runs, with no template chosen:

| Period | As reviewed (`c126a2c`) | After the fixes | With the to-do's index |
|---|---|---|---|
| 4 weeks | 3,474 ms (last week's statuses 3,201 ms) | 239 ms | 189 ms |
| 12 weeks | 4,456 ms (statuses 3,057 ms, stage times 1,302 ms) | 495 ms | 209 ms |
| 26 weeks | 5,341 ms (statuses 3,173 ms, stage times 2,043 ms) | 1,025 ms (stage times 846 ms) | 516 ms |

Last week's statuses now take about 70 ms whatever the period. Stage times grow with the stages finished in the period; the index (`(PatientProcedureId, Action, AtUtc)` including `StageId`, created by hand for the measurement and dropped after) halves them again, and is in `docs/todo.md`. The same program checked that SQL Server and memory agree on a smaller database (2,000 procedures): last week's statuses exactly, stage times to within a second.

## Remaining risks

- **Fixed since:** the insights queries now run on SQL Server in `MedApp.IntegrationTests` (finding 11).
- **Speed** is measured (After the review), but only on LocalDB on one machine; stage times over 26 weeks take about a second until the index is added.
- **What the averages mean:** waiting time includes time spent blocked and time before anyone was assigned; an average over one or two finishes is shown like any other (the count is only in the table).
- **Template edits change what a stage number means.** Rows are per position and stage definition, so after a stage is inserted in a template, two rows can share a number ("2. Consultation" and "2. Scan").
- **Chart colours:** the teal series is about 2.6:1 against the MedApp theme's light card (`#e4ebf8`), under WCAG's 3:1 for graphics; the legend, the summary and the table make up for it, as the PR says. Custom card colours (#63's colour settings) aren't checked against the series colours at all.
- **The audit details' wording is now load-bearing.** The in-memory tests build their entries with the real `ProcedureChange`, so a change to the wording fails them, which is good; but any other writer of status changes (a script, a data fix) must write exactly the same JSON.
- **Not seen in a browser** in this review: no axe audit, no parity screenshots, no half view.

## Say it like this

"Insights gives admins one page for the state of the work: what's open, overdue and done, where the overdue work sits, how long each stage takes, and how many procedures come in and go out each week. It works the numbers out in the database, and rebuilds last week's picture from the audit log, so it needed no new tables. The trade-off is that the history is read from text and replayed every time: last week's overdue count uses today's due dates, the query grows with the whole history, and two of the links open lists that can't match their counts."

## Detail

### The generated SQL for last week's statuses (excerpt)

```sql
SELECT [p1].[Status0], [p1].[IsPastDue], COUNT(*)
FROM (
    SELECT ISNULL((
        SELECT TOP(1) CASE
            WHEN [p0].[Details] LIKE N'%"to":"Completed"%' THEN 5
            ...
            ELSE 0
        END
        FROM [PatientProcedureAuditEntries] AS [p0]
        WHERE [p0].[PatientProcedureId] = [p].[Id] AND [p0].[Action] = 2 AND [p0].[AtUtc] <= @atUtc
        ORDER BY [p0].[AtUtc] DESC, CASE
            WHEN [p0].[Details] LIKE N'%"to":"InProgress"%' THEN 1
            ELSE 0
        END), 0) AS [Status0], ...
    FROM [PatientProcedures] AS [p]
    WHERE [p].[CreatedAt] <= @atUtc
) AS [p1]
GROUP BY [p1].[Status0], [p1].[IsPastDue]
```

Every procedure created before the moment, each with a subquery over all its entries up to then. In the stage durations' SQL, a `SELECT MIN([AtUtc]) … WHERE [Action] = 7 AND [PatientProcedureId] = … AND [StageId] = …` appears six times per stage: four for the stage itself and two for the one before it.

### Finding 4 (and 5): rebuild only what changed since

```csharp
// Markers for where a status change came from, as ProcedureAudit writes them.
public const string FromUnassigned = "\"from\":\"Unassigned\"";
// ... one per status

// Status at the moment: the "from" of the first change after it, or the status now if nothing changed since.
Status = audit
    .Where(e => e.PatientProcedureId == p.Id && e.Action == Action.StatusChanged && e.AtUtc > atUtc)
    .OrderBy(e => e.AtUtc)
    // Two changes at once (Assigned → InProgress → InReview): the one to InProgress came first.
    .ThenBy(e => e.Details.Contains(ToInProgress) ? 0 : 1)
    .Select(e =>
        e.Details.Contains(FromCompleted) ? Completed
        : e.Details.Contains(FromCancelled) ? Cancelled
        // ... the other statuses
        : Unassigned)
    .Cast<PatientProcedureStatus?>()
    .FirstOrDefault() ?? p.Status,
```

With an index on `(PatientProcedureId, Action, AtUtc)`, most procedures need one empty index seek. Procedures created after the moment are still left out by `CreatedAt <= atUtc`.

### Finding 1: links that override the list's starting filter

```diff
 export function proceduresHref(filters: Record<string, string>, templateName?: string): string {
-  const query = new URLSearchParams({...filters, ...(templateName ? {template: templateName} : {})}).toString();
-  return query ? `/procedures?${query}` : '/procedures';
+  // mine=false: a count's list must not be narrowed by the list's own start (Account Settings).
+  const query = new URLSearchParams({...filters, mine: 'false', ...(templateName ? {template: templateName} : {})}).toString();
+  return `/procedures?${query}`;
 }
```

### Finding 8: the probe

`InsightsPeriod.For(now, 4)`, from the PR's own code:

| Now (UTC) | This week starts | Last week ends | Same moment last week | Past last week's end? |
|---|---|---|---|---|
| 2027-04-04 22:30 (23:30 BST) | 2027-03-28 23:00 | 2027-03-28 23:00 | 2027-03-28 23:30 | Yes, by 30 minutes |
| 2027-04-04 21:30 (22:30 BST) | 2027-03-28 23:00 | 2027-03-28 23:00 | 2027-03-28 22:30 | No |
| 2026-11-01 23:30 (23:30 GMT) | 2026-10-26 00:00 | 2026-10-26 00:00 | 2026-10-25 22:30 | No (an hour early, as the comparison is elapsed time) |

### Questions asked of the PR, and the answers

- **UK week boundaries across the clock changes:** right. Weeks start at Monday 00:00 UK time, and offsets change on the hour, so hourly UTC counts split cleanly. Only "the same point last week" slips (finding 8).
- **Same-moment ties:** only one command writes two status changes at once (finishing the last stage of an unstarted procedure: to InProgress, then to InReview), and the tie-break picks InReview, in memory and in the SQL's `ORDER BY`. Assigning and unassigning are separate requests, so no other pair occurs.
- **The JSON markers:** a reason can't fake one, as System.Text.Json escapes its quotes as `"`. `"to":"Assigned"` doesn't match inside `"to":"Unassigned"`, because of the quote before it.
- **Negative waits:** with the first finish taken from the audit log, a wait is only negative when that log entry is missing (procedures from before the audit log), and those are clamped to zero.
- **Several assignees:** each assignment counts, as documented in the response; the "Nobody" row is procedures with no assignment at all, the same rule as the new list filter.
- **Procedures with no template:** counted under all templates, and shown in time-at-stage as "(no template)"; they can't be chosen on their own.
- **Authorisation:** admin only on the server (the attribute) and in the web app (`requireAdmin`, the side nav, and the shortcut); see finding 11 for the missing test.
- **The new list filter and staff:** existing behaviour doesn't change. `NobodyAssigned` defaults to false; staff gain an "Assigned to" filter with "Nobody" only, and the accounts list is only asked for by admins.
- **Drill-down counts:** with no template and the list starting on everyone, Open, In progress, Overdue, each priority and each assignee match their lists (the list's default hides only closed procedures, so blocked ones are in Open on both sides). Completed doesn't (finding 2); the start-on-mine setting (finding 1) and the template name (finding 6) break the rest.
- **Server rendering and hydration:** the page renders with its numbers and summaries and an empty, fixed-height box for each chart; the first browser render matches, then the chart draws once its width is measured.
- **The bundle split:** holds (see How it was checked).
- **Theme tokens:** `--color-data-categorical-blue` and `-teal` are defined by Astryx for light and dark; the card colour comes from the theme. See Remaining risks for the contrast.
- **Narrow and half views:** the charts go to one column when `useIsNarrow()` is true; the tiles wrap at a minimum of 180 px each, so some widths leave three tiles over one. Not seen in a browser here.
