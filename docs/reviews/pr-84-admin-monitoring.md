# Review: PR #84, admin monitoring rules and a switch for assignment notifications

**PR:** [#84](https://github.com/Aliator/MedApp/pull/84), branch `feature/admin-monitoring`, reviewed at `68084ff` on top of `main` at `2ab00d4` (the merge of #68). `main` moved to `16ee213` (the merge of #69) during the review, and merging it conflicts in eight files (see [How it was checked](#how-it-was-checked)).

**Reviewed:** 16 November 2026, independently of the PR description's conclusions. Every claim below was checked against the code, a build or test run, or the GitHub API. **No finding is high or critical;** 4 are medium and 8 are low.

## Summary

Admins can now set their own rules for being told, in the notification bell, when a staff member (or anyone) does one of eight things to a procedure, optionally only for a priority or higher, a template or a stage; and each person can switch off the notification they get when they're assigned a procedure. The engine is sound: it reads what a change did from the audit entries it already writes, costs nothing for changes no rule can watch, tells each admin once, never about their own change, and never lets a failure undo the change. The weak points are around the edges: a rule naming something since deleted can't be switched off, the new switch weakens a hazard control that isn't updated, patients' exports now carry admins' monitoring notifications, and the staff side of the governance pack doesn't cover the new monitoring data.

## What changed, and why

122 files, 5,662 lines added and 33 removed. About 1,300 of the added lines are generated: the migration's designer file (924), the snapshot (59), the API types (295) and the route tree (21).

- **Why:** `docs/todo.md` listed per-person notification settings and more notification kinds as a feature. The PR builds the admin side as rules, and the person's side as one switch.
- **Monitoring rules** (`MonitoringRule`, table `MonitoringRules`): an owner (the admin), a person or anyone (`SubjectUserId`), one event (`MonitoringEvent`, stored by number 0 to 7), and optional conditions: a minimum priority, a template, and a stage definition for the two stage events. Each has an on/off switch. No foreign keys.
- **The API** (`api/monitoring/rules`, admin-only): list, add, replace (including switching on and off) and delete. Each admin sees only their own rules; another admin's is a 404. At most 50 rules each (409). A named person, template or stage must exist (404).
- **How rules fire:** every procedure command calls `IProcedureMonitor.NotifyAsync(change)` after its save. `ProcedureMonitor`:
  - reads what the change did from its audit entries (`MonitoredActivity`), ignoring notes, starts, unassignments and automatic status moves;
  - loads the switched-on rules for those events, in one query, and only when there's something to watch;
  - matches them on the procedure as it is after the change (`MonitoringRuleMatcher`), leaving out the admin who made the change;
  - drops owners who are no longer admins;
  - sends each remaining admin one notification (`MonitoringNotifications`), and logs and carries on if anything fails.
- **Notification kinds 10 to 17**, one per event, each with a fixed title ("Stage finished") and a message built from usernames, the priority, the template and stage names and due dates. 1 to 9 are kept for the calendar branch. `Notification.HasFixedTitle` tells anonymisation to leave these titles alone.
- **Assignment notification switch** (`NotificationPreferences`, table `NotificationPreferences`, `GET`/`PUT api/notifications/preferences`): `NotificationSender` looks up the person's choice before storing an assignment notification; no row means on.
- **The web app:** a Monitoring page under Administration (rules as sentences, a switch per rule, an add/edit dialog with a live preview, cards when narrow), `RuleSentence` and `describeRule`, a Notifications card in Account Settings, a nav item and icon, the route, and two UI parity screens.
- **The migration** `AddedMonitoringRules`: two new tables and two indexes, nothing else.
- **Tests:** unit tests for every new handler, validator, mapper and the matching code; integration tests for rules end to end, the switch, authorisation, endpoint coverage and the anonymisation canary; web tests for the page, the sentence builder and the card.
- **Docs:** `AGENTS.md` (Notifications and a new Monitoring section), `MedApp.Web/AGENTS.md`, the handbook, the DPIA (risk 21, monitoring of staff), the data inventory, the retention schedule, the staff privacy notice, the device decision (0.3), the safety case report and the to-do list.

## Design choices

- **Reading the audit entries, not raising events from each handler:**
  - **Instead:** each handler names what it did (`monitor.StageFinished(...)`).
  - **Chosen because:** one place decides what each change means, and every command is covered by the same call.
  - **Worse at:** monitoring now depends on the audit details' JSON (`"to"` of a status change, `changes.dueDate`, the assigned `userId`). A change to how `ProcedureAudit` writes them silently stops a rule firing; only the unit tests on `MonitoredActivity` guard it.
- **Inline, after the save, best effort:**
  - **Instead:** an outbox row written in the change's transaction, and a background worker that sends the notifications.
  - **Chosen because:** simple, no worker or lease across two API copies, and a missed monitoring notification harms nobody directly.
  - **Worse at:** a crash or failure between the save and the send loses the notification for good, and the request waits for every insert and push. Like a receptionist who rings each manager after filing a form: quick, but if the line's down the call is never made.
- **The priority and template after the change:**
  - **Instead:** before, or either.
  - **Chosen because:** for create, finish and status changes, "after" is the only value there is.
  - **Worse at:** a due date moved in the same edit that lowers the priority escapes a priority rule (finding 5).
- **Rules private to each admin:**
  - **Instead:** shared rules, or rules visible to a DPO role or to the staff they name.
  - **Chosen because:** an admin's supervision is their own business, and it's simple to secure.
  - **Worse at:** nobody, staff member or DPO, can see which rules name a person (DPIA risk 21 says so, and proposes it).
- **The role checked on every change, not rules removed when a role is:**
  - **Instead:** delete or disable an admin's rules when they lose the Admin role or are deleted.
  - **Chosen because:** nothing to keep in step; a returning admin gets their rules back.
  - **Worse at:** rules stay in the table indefinitely (the retention schedule and to-do list say so).
- **One notification kind per event (10 to 17):**
  - **Instead:** one "monitoring" kind with the event in the message.
  - **Chosen because:** the client can tell them apart without parsing text, and `HasFixedTitle` is one range check.
  - **Worse at:** eight numbers to keep forever; the next event needs number 18 and an edit to the range (the test that names every "Monitored…" kind would catch a missed one, which is worth keeping).
- **The switch kept on the server, in its own table:**
  - **Instead:** the account's settings JSON, which the browser owns.
  - **Chosen because:** the server must read it before sending, and the settings JSON is the browser's to overwrite.
  - **Worse at:** a second place for a person's preferences, and one more query on every assignment.
- **A people picker of the first 100 accounts:**
  - **Instead:** a searchable picker.
  - **Chosen because:** it reuses the users search with no new endpoint.
  - **Worse at:** larger organisations (finding 8).

## How it was checked

- **Read:** the whole diff, the PR description, `AGENTS.md` (Notifications, Monitoring, Procedures, the activity log and anonymisation) and `MedApp.Web/AGENTS.md`, `docs/governance/`, the hazard log, the safety case and the device decision, and the code around the change: every procedure command and `ProcedureChange`, `NotificationSender` and its repository, `PatientRecordRepository` and the export mapper and page, the anonymiser, `IdentityReadService`, My Tasks' default filter, and `medapp-dev`'s demo data reset.
- **Build:** `dotnet build MedApp.sln --configuration Release`: 0 errors, 1 warning (CS0414 in `MedApp.Client.Deprecated`, a file this PR doesn't touch).
- **.NET tests:** `dotnet test MedApp.UnitTests --configuration Release`: 1,495 passed. `dotnet test MedApp.IntegrationTests --configuration Release` filtered to the monitoring, patient privacy, endpoint coverage, authorisation, notification and migration suites: 112 passed in 2 min 4 s, against SQL Server 2022 in its own Testcontainers container, which was removed afterwards. The dev database on port 14330 wasn't touched.
- **Web:** `npm run lint` and `npm run typecheck` clean; `npx vitest run` on the monitoring and account pages, `api/monitoring` and the navigation: 183 tests in 18 files passed. The full suite with coverage ran in CI.
- **The migration:** `dotnet ef migrations has-pending-model-changes` reports no changes. Read by hand: two `CreateTable` and two `CreateIndex`, and `Down` drops both tables; additions only. Not run against a database here (the PR says it ran up and down on LocalDB).
- **CI:** run [43857090574](https://github.com/Aliator/MedApp/actions/runs/43857090574) on `68084ff` passed all eight jobs (`unit-tests`, `integration-tests`, `web`, `image`, `helm`, `e2e`, `dependency-review`, `coverage-comment`), and CodeQL ([43857090648](https://github.com/Aliator/MedApp/actions/runs/43857090648)) passed. In the coverage comment every new .NET class has 100% of its lines covered (the lowest branch figures are `MonitoredActivity` at 84% and `MonitoringRuleMatcher` at 86.1%), and no web file is under 80%.
- **Merging:** `git merge-tree` of `HEAD` with `origin/main` at `16ee213` conflicts in `AGENTS.md`, `MedApp.Infrastructure/Common/Extensions/ServiceExtensions.cs`, `MedApp.Web/src/api/schema.d.ts`, `MedApp.Web/src/components/shared/icons.ts`, `docs/clinical-safety/intended-purpose-and-device-decision.md`, `docs/governance/README.md`, `docs/governance/data-inventory.md` and `docs/governance/dpia.md`. They include source files, so the merge wasn't made here, and this review's commit sits on the branch as it was (see Detail).
- **Not done:** a real browser, UI parity screenshots, the axe audit and a screen reader (they need the AppHost and the dev database); the narrow and half views were checked only through the web tests.

## Findings

Most severe first. None is high or critical. All twelve were open when this review was written; all twelve are now fixed, as each one's "What happened to it" says.

### 1. A rule that names a deleted account, template or stage can't be switched off or edited

- **Severity:** medium. **Confidence:** high (traced through the handler, the shared check and the page; the 404 itself not run end to end).
- **What could go wrong:** a rule's documented promise is that one naming something deleted "stays, shows it as deleted, and matches nothing". The page keeps such a rule's choice in its dropdowns ("Deleted user") so it can be edited. But every `PUT` re-checks that each named person, template and stage exists, even when unchanged, so the API answers 404. A staff member leaves and their account is deleted; their supervisor goes to switch off "Notify me when *(deleted user)* finishes Bloods". The switch fails with "That rule, or something it names, no longer exists. Reload the page and try again." Reloading changes nothing, and the same happens when editing the rule's priority. The only way out is Delete. Like a standing order to a closed account that the bank won't let anyone pause, only tear up.
- **Evidence:**
  - `MedApp.Application/Monitoring/Commands/UpdateMonitoringRule/UpdateMonitoringRuleHandler.cs:23` (checks the request's IDs every time)
  - `MedApp.Application/Monitoring/Common/MonitoringRuleInput.cs:76-86` (404 for any ID that no longer exists)
  - `MedApp.Web/src/pages/monitoring/MonitoringPage.tsx:114-123` (the switch sends the rule's stored IDs), `:59-64` (the "Reload the page" message), `:146-151` (keeps "Deleted user", "Deleted template", "Deleted stage" as choices)
  - `MedApp.Domain/Monitoring/MonitoringRule.cs:10-11` and `AGENTS.md:164` (the promise)
  - The tests don't meet: `MonitoringPage.test.tsx:135-143` opens the editor for such a rule against a mocked API, and `UpdateMonitoringRuleHandlerTests` only tests a new, unknown template.
- **Fix:** in `UpdateMonitoringRuleHandler`, check only the IDs that differ from the stored rule's (an unchanged deleted one is allowed to stay), or give the switch its own `PUT …/{id}/enabled` that checks nothing else. Add a handler test (rule naming a deleted user, switched off: saved) and an integration test that deletes the account first. Let the page's 404 message say "Delete the rule" when the rule itself still exists.
- **What happened to it:** fixed. The switch has its own endpoint, `PUT api/monitoring/rules/{id}/enabled` (`SetMonitoringRuleEnabledHandler`), which checks nothing but the rule, and the page uses it. An edit checks only the person, template and stage it newly names (`MonitoringRuleInput.CheckAsync`, given the stored rule), so a rule may keep naming something deleted. Tests: `SetMonitoringRuleEnabledHandlerTests`, `UpdateMonitoringRuleHandlerTests.Handle_RuleKeepsNamingADeletedAccount_SavesTheOtherChanges` and `Handle_ChangedToADeletedAccount_ThrowsNotFound`, and the integration test `RuleNamingADeletedAccount_CanStillBeSwitchedOffAndEdited`, which deletes the account first. The page's 404 messages now say what's gone: for a switch, "That rule no longer exists"; for a save, "something newly chosen for it".

### 2. Switching off assignment notifications weakens hazard H-09's control, and the hazard log isn't updated

- **Severity:** medium. **Confidence:** high on the behaviour; the clinical rating is the safety officer's.
- **What could go wrong:** H-09 ("A member of staff doesn't learn of work assigned to them") lists My Tasks hiding procedures already under way (it opens on "Not started") as a cause, and relies on "Every notification is stored before it's pushed, so it shows on the bell". With the new switch off, nothing is stored. A staff member switches assignment notifications off, as the card invites ("Work from My Tasks either way"). A colleague goes off sick and an admin hands over their half-finished Urgent procedure. No bell, and My Tasks shows "Nothing waiting to be started". The work waits until someone notices. Like switching off a doorbell in a house where the hall light only shows visitors who haven't come in yet. Separately, nothing in the hazard log covers relying on monitoring notifications, which are best effort, while the DPIA's own example of their use is "a senior clinician reviewing urgent work promptly".
- **Evidence:**
  - `docs/clinical-safety/hazard-log.md:366` (the My Tasks cause), `:374` (the control that no longer always holds)
  - `MedApp.Application/Notifications/Common/NotificationSender.cs:17-20` (returns before storing)
  - `MedApp.Web/src/pages/account/NotificationSettings.tsx:32` ("Work from My Tasks either way")
  - `MedApp.Web/src/pages/tasks/MyTasksPage.tsx:42` (defaults to `notStarted`)
  - `docs/governance/dpia.md:63` (the senior clinician example) against `docs/clinical-safety/intended-purpose-and-device-decision.md:55` ("must not be relied on")
  - `docs/clinical-safety/clinical-risk-management-plan.md:202` (the hazard log is updated in the same pull request; the gate formally starts with a safety officer, but the review of #68 already applied it)
- **Fix:** update H-09: add "the person has switched assignment notifications off" as a cause, reword the stored-notification control as "unless switched off", and either keep the switch until H-04's control (My Tasks showing started work) is built or make the card say "My Tasks, with its filter on all open work". Add a short hazard (or a cause under an existing one) for an admin relying on a monitoring rule that is best effort and stops silently when its owner loses the Admin role, and soften the DPIA's example to supervision rather than clinical follow-up.
- **What happened to it:** fixed, for the clinical safety officer to review. H-09 has the switch as a new cause, its stored-notification control now says "unless the person has switched assignment notifications off", and a new control records the switch's help text, which now says to work from My Tasks "with its filter on all open work" (`NotificationSettings.tsx`, with a test). New hazard H-29, "An admin relies on a monitoring rule to follow up clinical work" (best effort, stops silently, no overdue event), rated Considerable, Low: 2, with an information control before first live use and an outbox later. The DPIA's example is now supervision (a team lead and a new member of staff), and says clinical follow-up doesn't rely on monitoring. Hazard log 0.6.

### 3. Patients' subject access exports now carry admins' monitoring notifications

- **Severity:** medium. **Confidence:** medium (the behaviour is certain; whether these belong in a patient's export is a judgement).
- **What could go wrong:** the export gathers every notification whose `ProcedureId` is one of the patient's procedures, and lists each as "to *(username)*: *title*: *message*". Monitoring notifications have a `ProcedureId`, so a patient's export now reads, for example, "to *admin2*: Stage finished: *staff1* finished the Bloods stage on a High priority Knee review procedure." That tells the patient which admin keeps a watch on which member of staff: information about staff supervision, not about the patient's care. The export's own rule leaves out staff's account IDs, addresses and notification read times as "their data, not the patient's"; a monitoring notification is entirely the staff's. With several admins watching "anyone finishes a stage", the section also fills with copies of what the history already shows. Like handing a patient a copy of the ward manager's supervision notes because they mention the patient's bed.
- **Evidence:**
  - `MedApp.Infrastructure/Data/Repositories/PatientRecordRepository.cs:42-50` (every notification by procedure ID)
  - `MedApp.Application/Patients/Common/PatientExportMapper.cs:85-94` (kind, recipient, title and message)
  - `MedApp.Web/src/pages/patients/PatientExportPage.tsx:239-252` (shown as "to … : title: message")
  - `docs/governance/data-subject-rights.md:37` (the "their data, not the patient's" rule)
  - The staff privacy notice and DPIA risk 21 don't say a patient may see this.
- **Fix:** leave monitoring kinds out of the export: a `Notification.IsMonitoring(kind)` beside `HasFixedTitle`, applied in `PatientExportMapper` rather than the repository, so anonymisation still finds them; with an export test that a monitoring notification isn't in it. If they're kept on purpose, say so in the data subject rights procedure and the staff privacy notice.
- **What happened to it:** fixed. `Notification.IsMonitoring(kind)` beside `HasFixedTitle`, applied in `PatientExportMapper.ExportedNotifications` (the notifications and the usernames it looks up), not in the repository, so anonymisation still finds them. Tests: `PatientExportMapperTests.ToResponse_MonitoringNotification_IsLeftOut_WithItsAdmin`, and the privacy canary now checks the export holds neither the monitoring kind nor the watching admin's name. The rights procedure and the DPIA's risk 24 say so.

### 4. The staff rights procedure and the customer's record of processing don't cover monitoring

- **Severity:** medium. **Confidence:** high.
- **What could go wrong:** a member of staff who feels watched (the very risk DPIA risk 21 describes) asks for their data. The manual procedure collects "account, roles, sessions, settings, notifications and audit entries by `UserId`". That finds notifications sent *to* them, but not the rules that name them (`SubjectUserId`), nor the monitoring notifications *about* them, which are stored under the admin's `UserId` with their username only in the message text. The answer leaves out exactly what they were asking about. The customer's record of processing template (entry 3, staff) still gives its purposes as access, accountability and telling staff about assignments, without supervision of their work; and the lawful bases note says a private provider's legitimate interests assessment should cover the audit trail and access monitoring, not monitoring rules. `AGENTS.md` asks that a change to what personal data is held, or who sees it, update the governance documents in the same PR.
- **Evidence:**
  - `docs/governance/data-subject-rights.md:46`, `:57`, `:68` (unchanged by the PR)
  - `MedApp.Domain/Monitoring/MonitoringRule.cs:25` (`SubjectUserId`); `MedApp.Application/Monitoring/Common/MonitoringNotifications.cs:19-28` (stored under the owner)
  - `docs/governance/ropa-customer-template.md:62-68`; `docs/governance/roles-and-lawful-bases.md:88`
- **Fix:** in the rights procedure, add "monitoring rules naming them (`SubjectUserId`), rules they own, and monitoring notifications that name them (by username in the message, or better by a stored subject ID)", and say who answers it, since no admin can see another admin's rules. Add supervision to the ROPA template's staff entry, and monitoring rules to the legitimate interests note.
- **What happened to it:** fixed. The rights procedure lists, for a staff member, notification choices, rules that name them (`SubjectUserId`), rules they own, and monitoring notifications about them (kinds 10 to 17, by username in the message, as they're stored under the admin), and says the supplier answers which rules name someone, on the customer's instruction, as no admin sees another's rules. The customer ROPA template's entry 3 adds supervision to its purpose, the rules, choices and monitoring notifications to its data, monitoring rules to the 6(1)(f) basis, and their retention. The lawful bases note covers monitoring rules in a legitimate interests assessment, and in an NHS body's monitoring policy.

### 5. A due date moved in the same edit that lowers the priority escapes a priority rule

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** rules match on the priority after the change. An admin watches "anyone changes the due date of an Urgent priority procedure". Someone edits an Urgent procedure, setting it to Low and moving its due date back a month in one save. The rule doesn't fire: the procedure is Low now. Lowering the priority on its own is no event at all. The case the admin most wants to hear about passes quietly.
- **Evidence:** `MedApp.Application/Monitoring/Common/MonitoringRuleMatcher.cs:21`; `MedApp.Application/Procedures/PatientProcedures/Commands/UpdatePatientProcedure/UpdatePatientProcedureHandler.cs:52` (priority changed before `:62` notifies).
- **Fix:** for `DueDateChanged`, match the higher of the priority before and after (the `changes.priority.from` in the same audit entry), and consider a "priority lowered" event. Otherwise, say in `AGENTS.md` and on the dialog that the priority is the one after the change.
- **What happened to it:** fixed. `MonitoredActivity` reads the priority before from the same audit entry (`changes.priority.from`), and `MonitoringRuleMatcher.PriorityOf` matches the higher of before and after. Tests: `MonitoringRuleMatcherTests.Matches_PriorityLoweredInTheSameEdit_MatchesOnThePriorityBefore` and `PriorityOf_PriorityRaised_IsTheOneAfter`, `MonitoredActivityTests.From_DueDateMovedAndPriorityChangedInOneEdit_CarriesThePriorityBefore`, and the integration test `DueDateMovedWhileThePriorityIsLowered_StillMatchesAnUrgentRule`. A "priority lowered" event isn't added.

### 6. Most handlers' calls to the monitor are untested

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** the convention test checks that every procedure command handler *takes* `IProcedureMonitor` in its constructor, not that it calls it. Only finish and status change are checked to call it after saving. Integration tests cover create, finish, status change and assign end to end. Removing the call from `UnlockProcedureStageHandler` or `UpdatePatientProcedureHandler` leaves every test green, and "Stage unlocked" and "Due date changed" rules stop firing. The privacy canary covers only the "Procedure cancelled" message end to end, though `AGENTS.md` says it guards new messages.
- **Evidence:** `MedApp.UnitTests/Application/Monitoring/Common/ProcedureCommandsMonitoringTests.cs:40-53` (constructor only), `:55-80` (two handlers); `MedApp.IntegrationTests/Monitoring/MonitoringTests.cs` (no unlock or due date test); `AGENTS.md:168`.
- **Fix:** a parameterised unit test running each of the ten handlers once and verifying `NotifyAsync` after the save with the same token, and integration tests for an unlock and a due date change. In `AGENTS.md`, say that `MonitoringNotificationsTests` checks every message and the canary checks one.
- **What happened to it:** fixed. `ProcedureCommandsMonitoringTests.Handle_EachCommand_TellsTheMonitorAfterItsSave` runs each of the ten procedure commands and checks it calls the monitor once, after its save, with the request's token; `EveryProcedureCommandHandler_IsRunBelow` fails if a new command isn't added to it. Integration tests for an unlock (`AdminUnlocksAWatchedStage_NotifiesAnotherAdmin`) and a due date change (above). `AGENTS.md` says which tests check what.

### 7. The assignment handler runs the monitor before telling the assignee

- **Severity:** low. **Confidence:** medium (read, not provoked).
- **What could go wrong:** in `AssignPatientProcedureHandler` the monitor now runs between the save and the assignee's own notification, so the assignee waits for every admin's insert and push first. If a monitoring insert fails, its `Notification` stays in the request's shared DbContext as "added", so the assignee's `SaveChangesAsync` tries it again; a failure that persists turns a saved assignment into a 500 and drops the assignee's notification. Transient database errors are retried by EF first, so this needs a lasting fault. Two smaller edges: the monitor rethrows any `OperationCanceledException` by type, not by whether the request was cancelled; and `GetUserIdsInRoleAsync` doesn't pass its token on (with `AddIdentityCore`, `UserManager`'s own token is `None`), and loads every admin's full, tracked user row to get their IDs.
- **Evidence:** `MedApp.Application/Procedures/PatientProcedures/Commands/AssignPatientProcedure/AssignPatientProcedureHandler.cs:39-55`; `MedApp.Infrastructure/Data/Repositories/NotificationRepository.cs:9-13`; `MedApp.Application/Monitoring/Common/ProcedureMonitor.cs:32`, `:46`; `MedApp.Infrastructure/Common/Identity/IdentityReadService.cs:103-108`.
- **Fix:** send the assignee's notification before calling the monitor; have `NotificationRepository.AddAsync` detach the entity when its save fails; filter on `!cancellationToken.IsCancellationRequested`; read admin IDs with a no-tracking projection over `UserRoles` that takes the token.
- **What happened to it:** fixed. The assignment handler tells the assignee before running the monitor (`AssignPatientProcedureHandlerTests.Handle_NewAssignee_TellsThemBeforeTheWatchingAdmins`). `NotificationRepository.AddAsync` detaches the notification when its save fails. The monitor rethrows only when the request's own token is cancelled (`ProcedureMonitorTests`: a timeout of its own is logged and the others still sent). The admin lookup is `IMonitoringRuleRepository.GetAdminsAsync`: one no-tracking projection over the user roles and roles, for just the matching owners, with the token; `IIdentityReadService.GetUserIdsInRoleAsync` is gone. The detach isn't unit tested (the repository needs a real database); the integration suite runs it on every notification.

### 8. The people picker silently stops at 100 accounts

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** the picker loads the first 100 accounts by username, the users search's largest page. In a department with 140 accounts, nobody whose username sorts after the 100th can be chosen, and nothing on the page says the list is cut short. The to-do list records it; the page doesn't.
- **Evidence:** `MedApp.Web/src/api/monitoring.ts:155-156`; `MedApp.Web/src/pages/monitoring/MonitoringPage.tsx:148-152`; `docs/todo.md:19`.
- **Fix:** a searchable picker on `api/auth/users/search`; until then, a line under the field when the total is over 100.
- **What happened to it:** fixed for now: the field's description says "Only the first 100 accounts by username are listed for now." when the search's total is over 100 (a web test). The searchable picker stays a to-do (`docs/todo.md`).

### 9. The list's switch: a label that flips, no pending state, and lost results

- **Severity:** low. **Confidence:** medium (read; not tried with a screen reader).
- **What could go wrong:** each row's switch is named "Switch off: Notify me when …" while on and "Switch on: …" while off, so its name changes as its state does; a switch's name should stay put and its state be read out as on or off. The switch shows the server's value, with no pending state, so after a press it doesn't move until the save and a refetch finish; a screen reader user hears nothing change and may press again. All switches share one mutation, and TanStack Query runs per-call callbacks only for the latest call, so switching two rules quickly loses the first one's announcement and, if it failed, its error.
- **Evidence:** `MedApp.Web/src/pages/monitoring/MonitoringPage.tsx:114-123`, `:194-200`.
- **Fix:** a stable name (the rule's sentence), the value updated optimistically or the switch disabled while saving, and the outcome handled in the mutation's own callbacks or per row.
- **What happened to it:** fixed. Each switch is named by its rule's sentence, which stays put; it shows the new state held still (`isLoading`, `aria-disabled`) while its own save runs; and each call handles its own outcome through `mutateAsync`, so switching two rules at once announces or reports each (an earlier switch's error isn't cleared by the next). Web tests for the pending state and for two at once.

### 10. Edge cases that do nothing or double up

- **Severity:** low. **Confidence:** high.
- **What could go wrong:**
  - "Unlocks a stage (admins only)" can be paired with a member of staff who isn't an admin; that rule can never fire, and nothing says so.
  - An admin with an "anyone is assigned" rule who is assigned by another admin gets two notifications for one change: the assignment and the monitoring one.
  - The 50-rule limit is a count, then an insert, so two adds at once at 49 make 51.
  - `medapp-dev`'s `reset_demo_data` deletes staff, templates and stages but keeps the admin's rules and everyone's choices, which then name deleted things (and, with finding 1, can't be switched off).
- **Evidence:** `MedApp.Web/src/api/monitoring.ts:133`; `MedApp.Application/Monitoring/Common/MonitoringRuleMatcher.cs:35` (excludes only the admin who acted) with `AssignPatientProcedureHandler.cs:43-54` (the assignee's own notification); `MedApp.Application/Monitoring/Commands/CreateMonitoringRule/CreateMonitoringRuleHandler.cs:21-36`; `tools/MedApp.DevMcp/Tools/DemoDataTools.cs:84-104`.
- **Fix:** warn in the dialog when the person isn't an admin for "Stage unlocked"; skip the monitoring notification to the person assigned; accept the race or check again after the insert; wipe `MonitoringRules`, `NotificationPreferences` (and `Notifications`) in the reset.
- **What happened to it:** fixed, each:
  - **"Stage unlocked" for someone not an admin** is refused (a 400 on the person: "Only admins unlock stages, so choose an admin, or anyone."), checked when the person or the event changes, and the page lists only admins for that event, with a hint.
  - **The double for "anyone assigned"**: the matcher leaves out the person assigned, who gets the assignment notification (unit and integration tests).
  - **The 50 limit's race** is accepted and documented (`MonitoringRuleInput.MaxRulesPerAdmin`, `AGENTS.md`): a rule or two over harms nothing.
  - **`reset_demo_data`** deletes monitoring rules, notification choices and notifications with the rest, and its description says so.

### 11. Merging `main` collides on DPIA risk 21, the device decision's version and the migration order

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** `main` (#69) already has a DPIA risk 21, "The NHS number", and a device decision 0.3. This PR adds its own risk 21 and its own 0.3. `dpia.md` and the device decision will conflict and be fixed by hand, but `docs/todo.md` merges cleanly, so its "(DPIA risk 21)" for auditing monitoring rules will point at the NHS number risk without anyone seeing it. Separately, `AddedMonitoringRules` (`20261116110747`) sorts before #69's `AddedNhsNumberAndSnomedCodes` (`20261116111123`), which `main` has already deployed to kind. EF applies it anyway (the tables are independent), but the history stops being the single linear one `AGENTS.md` asks for, and rolling back to #68's migration would undo both.
- **Evidence:** `docs/todo.md:17`; `AGENTS.md:169`; `docs/governance/dpia.md:39`, `:52`, `:91`, `:133`, `:370`; `docs/governance/README.md:30`; `origin/main:docs/governance/dpia.md:132`, `:371`; `MedApp.Infrastructure/Migrations/20261116110747_AddedMonitoringRules.cs`.
- **Fix:** when merging `main`, renumber monitoring to risk 22 everywhere (the seven places above, and "22 risks" in the governance README), make the device decision 0.4, and re-create the migration on top of #69's, as #69 did after #68's.
- **What happened to it:** fixed. `main` with #69 is merged. Monitoring of staff is DPIA risk 24 in all its places (the processing, the context, the necessity row, the risk table, its measures section, the governance README's "24 risks", `AGENTS.md`), `docs/todo.md` points at risk 24 (24, not 22, because the attachments branch, merging first, takes risks 22 and 23; for the same reason the new reliance hazard is H-29, after its H-23 to H-28), the DPIA and data inventory are 0.4, and the device decision is 0.4 after #69's 0.3. `AddedMonitoringRules` is scaffolded again after `AddedNhsNumberAndSnomedCodes` (`20261116114224`), read (two tables, two indexes), `has-pending-model-changes` reports none, and it ran up, down to #69's migration and up again on LocalDB with synthetic rows. The API types are regenerated.

### 12. Docs that don't match the code

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** the handbook says the monitor loads rules in "one query, or none", and `AGENTS.md` says "in one query". That's true of the rules, but with a match there are two more queries for the admins (Identity looks the role up, then its users) and one insert per admin told, so a performance check based on the docs would undercount. The PR description's "rules stop if the owner loses the Admin role" is right; the docs' "drops rules whose owner … is no longer an admin" reads as if rules were deleted, which they aren't.
- **Evidence:** `docs/architecture.md:447`; `AGENTS.md:166`; `MedApp.Application/Monitoring/Common/ProcedureMonitor.cs:61-68`.
- **Fix:** "one query when the change has something to watch, and two more plus one insert per admin when a rule matches"; "skips (doesn't delete) the rules of an owner who is no longer an admin".
- **What happened to it:** fixed. `AGENTS.md`, the handbook and `ProcedureMonitor`'s summary say: one query when the change has something to watch (none otherwise), one more only when a rule matches, then one insert and one push per admin told; and that an owner's rules are skipped, not deleted, while they aren't an admin.

## Remaining risks

- **Notifications can be lost for good:** there's no outbox, so a crash between the save and the send means no monitoring notification, and nothing records that it was missed.
- **Rules nobody can see but their owner:** no audit of who added or changed a rule, and no view for a DPO or the staff they name (DPIA risk 21, proposed).
- **Rules and choices outlive their accounts,** until account deletion cleans them up (to-do).
- **Monitoring depends on the audit details' format:** a change to `ProcedureAudit`'s JSON can stop rules firing without failing anything but `MonitoredActivity`'s unit tests.
- **Every watched change waits for the sends:** one insert and one push per admin, in turn, inside the request.
- **Assignment notifications still carry the procedure's title** (DPIA risk 3, unchanged).
- **The merge with `main`** (#69) conflicts in eight files, among them the generated API types, which should be regenerated, and needs the renumbering in finding 11; every check should run again after it.
- **Not seen in a browser** here: no screenshots, axe audit or screen reader.

## Say it like this

"Admins can now ask to be told when a member of staff, or anyone, does certain things to a procedure: creates one, finishes a stage, completes, blocks or cancels one, is assigned one, unlocks a stage or moves a due date, optionally only for urgent work, one template or one stage. It works like a clerk who reads each new line of the audit log as it's written and rings whoever asked about that kind of line: no extra work for changes nobody watches, one ring per admin, never about their own change, and the change itself is never held up or undone if the ringing fails. Staff can also switch off the note they get when they're assigned a procedure. What needs fixing: a rule naming someone who has since left can't be switched off, only deleted; switching off assignment notes weakens a safety control that the hazard log still counts on; a patient's copy of their record now includes the admins' monitoring notes about staff; and the staff side of the data protection paperwork doesn't yet cover the rules and notes about them."

## Detail

### What each event reads, and who it's about

| Event | Audit entry it comes from | Rule's person is | Notes |
|---|---|---|---|
| Procedure created | `Created` | who created it | |
| Stage finished | `StageFinished` | who finished it | Stage condition: the stage's `StageDefinitionId`, looked up on the procedure. The automatic move to In Review that may follow is ignored. |
| Procedure completed, blocked, cancelled | `StatusChanged` with `"to"` Completed, Blocked or Cancelled | who chose it | These three are only ever chosen by someone (`ProcedureChange.ChangeStatus`); automatic moves go only to Assigned, Unassigned, In Progress or In Review. |
| Procedure assigned | `Assigned` (`userId`, `username`) | **who was assigned** | Only admins assign (`[Authorize(Roles = "Admin")]`). |
| Stage unlocked | `StageUnlocked` | who unlocked it (admins only) | The automatic move back to In Progress is ignored. |
| Due date changed | `DetailsUpdated` with `changes.dueDate` | who edited it | The message gives both dates. Priority is the new one (finding 5). |

Each command writes at most one watched entry, so de-duplicating across several activities of one change can't happen today; per admin, the matcher still sends one notification however many of their rules match. `Matches` checks: switched on, same event, the person (or anyone), `procedure.Priority >= MinimumPriority` (`Low` 0 to `Urgent` 3), the template's ID, and the stage definition's ID. The owner is excluded when they made the change; owners not in the Admin role now are dropped after matching.

### What the monitor costs each command

| Command | Watched event | Extra database round trips |
|---|---|---|
| Start a stage, save notes, unassign, delete, edit details without the due date | none | 0 |
| Create, finish, complete, block, cancel, assign, unlock, move a due date | yes, no rule matches | 1 (the switched-on rules for that event, on the `(Event, IsEnabled)` index) |
| The same, with *n* admins' rules matching | yes | 1 + 2 (Identity: the role, then its users) + *n* inserts, and *n* SignalR pushes through Valkey |
| Assign (as well as the above) | | 1 more: the assignee's notification choice |

Notes, the most frequent change, cost nothing.

### Finding 1, step by step

1. A rule has `SubjectUserId = X`; account X is deleted. `GET api/monitoring/rules` returns `subjectUserId: X, subjectUsername: null`, and the page shows "a deleted user".
2. The row's switch calls `save.mutate({id, ...inputOf(rule), isEnabled: false})`, so the body has `subjectUserId: X`.
3. `UpdateMonitoringRuleHandler` loads the rule (it's theirs), then `CheckNamedThingsExistAsync(request, …)` asks `GetUsernamesByIdAsync([X])`, gets nothing, and throws `NotFoundException("User 'X' was not found.")`.
4. The page maps any 404 to "That rule, or something it names, no longer exists. Reload the page and try again."

The smallest fix in the handler:

```csharp
// Only what this edit newly names has to exist; a rule may keep naming something since deleted.
await MonitoringRuleInput.CheckNamedThingsExistAsync(
    new ChangedNames(request, rule), identityReadService, stagesRepository, cancellationToken);
```

where `ChangedNames` passes on each ID only when it differs from the stored rule's.

### The merge with `main`

`git merge-tree --write-tree HEAD origin/main` at `16ee213` reports conflicts in `AGENTS.md`, `MedApp.Infrastructure/Common/Extensions/ServiceExtensions.cs`, `MedApp.Web/src/api/schema.d.ts`, `MedApp.Web/src/components/shared/icons.ts`, `docs/clinical-safety/intended-purpose-and-device-decision.md`, `docs/governance/README.md`, `docs/governance/data-inventory.md` and `docs/governance/dpia.md`. Both sides add repository registrations and icons, so both must be kept; the API types should be regenerated (`npm run gen:api`) rather than merged by hand. `MedAppDbContextModelSnapshot.cs`, `PatientAnonymiser.cs` and `CreatePatientProcedureHandler.cs` merge cleanly (#69 adds the NHS number to anonymisation and SNOMED codes to new stages, neither of which reaches a monitoring message). #69 adds no procedure command, so the convention test stays true. After the merge, `has-pending-model-changes` and every test should run again, with finding 11's renumbering done.
