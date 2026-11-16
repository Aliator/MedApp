# Review: PR #60, the clinical safety pack

**PR:** [#60](https://github.com/Aliator/MedApp/pull/60), branch `docs/clinical-safety`, reviewed at `d2033f6` on top of `main` at `88dc1fa` (the merge of #56).

**Reviewed:** 16 November 2026, independently of the PR's own notes. Every claim below was checked against the files, the code, git, the GitHub API or the published guidance. **No finding is high or critical.** Six are medium and five are low.

## Summary

The PR adds MedApp's first clinical safety file: draft DCB0129 documents (a risk management plan, a hazard log of 20 hazards and a safety case report), the intended purpose with a decision that MedApp isn't a medical device, and guidance for organisations that deploy it under DCB0160. All of it is marked as a draft for a Clinical Safety Officer to sign. The code citations hold up well and the device reasoning is sound, but the consequence scale isn't DCB0129's, and two product behaviours undercut controls the drafts rely on.

## What changed, and why

Seven files, 1,542 lines added and none removed. No code changes.

- **`docs/clinical-safety/README.md` (index):** lists the five documents (CS-01 to CS-05), their status, the main findings, the choices made in drafting, and the seven steps to finish the file. It's the front page of the clinical risk management file, the folder DCB0129 expects a manufacturer to keep.
- **CS-01, the clinical risk management plan:** scope, roles (with the Clinical Safety Officer, or CSO: the registered clinician who approves clinical safety work), how hazards are found and rated, a 5×5 risk matrix, incident handling, and a release gate at the pull request. DCB0129 (the NHS standard for manufacturers of health IT) requires a plan like this before anything else.
- **CS-02, the hazard log:** 20 hazards, each with causes, effects, existing controls linked to the code, further controls, and an initial and residual rating. A **hazard** is a way the software could contribute to harm to a patient; **initial risk** is with today's controls, **residual risk** is once the further controls are built. Nine hazards are rated 3 ("undesirable") today; none is 4 or 5.
- **CS-03, the clinical safety case report:** the per-release argument that the system is safe enough, filled in as far as today's code allows. Its draft conclusion is that MedApp isn't ready for live use.
- **CS-04, the intended purpose and medical device decision:** what MedApp is for, and why that keeps it outside the UK Medical Devices Regulations 2002 (UK MDR 2002), as an administrative workflow tool. It also lists the features that would make it a device, and a change-control rule.
- **CS-05, guidance for deploying organisations (DCB0160):** what a customer must do, the controls passed to them, and a starter hazard log for their deployment.
- **`docs/architecture.md`:** a Status line in roadmap step 6, and a row in the document map.

**Why:** step 6 of the roadmap (`docs/architecture.md:1815-1876`) asks for a clinical risk management file, a hazard log, a safety case and an intended purpose before any NHS buyer can assess the product (DTAC starts with clinical safety). This PR drafts all of them.

## Design choices

- **Not a medical device, by writing the intended purpose to stay outside the definition:**
  - **Instead:** treat MedApp as a low-class device from the start, or leave the question open until a feature forces it.
  - **Chosen because:** nothing in today's code interprets clinical data, and UKCA marking would cost far more than the product needs. The MHRA's own examples ("just follows the path of a procedure/treatment - there are no decisions") fit it closely.
  - **Worse at:** it fences off a large part of the roadmap (scores, triage, waiting-list priority, questionnaires), and it holds only as long as the change-control rule actually runs, which nothing enforces yet (finding 5).
- **Two ratings per hazard: today's, and once every further control is built:**
  - **Instead:** a third rating, "at first live use", with only the controls marked "before first live use".
  - **Chosen because:** simpler, and the plan says a planned control doesn't lower risk until it's built.
  - **Worse at:** the go-live decision needs the middle rating, and the residual one counts controls planned for much later (finding 4).
- **The release gate is written down but not switched on:**
  - **Instead:** add the pull request template and a rule in `AGENTS.md` now.
  - **Chosen because:** without a CSO, nobody can sign the section, so the README says the workflow shouldn't change yet.
  - **Worse at:** until then, the device change-control rule depends on someone remembering CS-04 exists; and the plan itself says the section is filled in by hand from now on, which contradicts the README (finding 5).
- **Drafted from the code alone, before any clinician or workshop:**
  - **Instead:** wait for a CSO and a hazard workshop.
  - **Chosen because:** it gives the CSO something concrete to start from, and finds code-level hazards (H-12) early.
  - **Worse at:** it sees what the code does, not how wards use it. Hazards from real use (handovers, leavers, workarounds) are thinner, and two of them are missing (findings 2 and 3).
- **The safety file lives in the public repository:**
  - **Instead:** keep it in a private document store.
  - **Chosen because:** it versions with the code, and pull requests can update both together.
  - **Worse at:** signed copies, the incident log and anything naming people or customers must live elsewhere; CS-01 section 11 already says so.

## How it was checked

- **Read in full:** all seven changed files, step 6 of `docs/architecture.md`, and the parts of `docs/procedures-ui-plan.md` the documents cite (decisions, section 3, screens).
- **Code citations:** about 70 claims about the code checked against the files at `d2033f6` (the PR changes no code, so it's the same as `88dc1fa`). Among them: `ProcedureWorkflow` (statuses, overdue, unlock, current stage), `ProcedureChange`, `DueDates`, every handler and validator cited, the role attributes on all four controllers, `ProcedureSearch` (filters and default sort), `ProcedurePage.tsx` (title, folding patient info, dialogs, the notes box key), `dates.ts` and its tests, `LiveNotifications.tsx`, `queryClient.ts`, `SessionService`, the lockout settings, and the test classes and counts (`ProcedureWorkflowTests` 41 cases, `ProcedureChangeTests` 19, `DueDatesTests` with both 2026 clock-change days). Almost all are accurate; the exceptions are findings 2, 6, 7 and 11.
- **H-12 (times shown an hour out):** no value converter, `DateTimeKind` handling or JSON converter exists in Infrastructure or the API, so times read from SQL Server go out without a `Z`; `formatDateTime` passes them to `new Date()`, and `dates.test.ts` only uses times ending in `Z`. The code supports the suspicion. Not reproduced in a browser.
- **Ratings:** all 40 ratings (20 initial, 20 residual) re-derived from the plan's matrix. Every one matches.
- **The risk scheme against DCB0129:** the plan's matrix, likelihood scale and acceptability levels compared with DCB0129's, as reproduced in a published clinical safety case report that follows DCB0129 (PRSB, nursing care needs standard, appendix A). Matrix, likelihoods and acceptability are identical; the consequence table isn't (finding 1).
- **The device decision:** the MHRA guidance PDF the documents link (it says v1.10g) was read. Every quotation in CS-04 appears in it word for word, and the flow chart's questions match CS-04's table (in a slightly different order, with the same result).
- **Followed through the code:** My Tasks' default filter from the page to the SQL (finding 2), and deleting a user through to procedure assignments (finding 3).
- **Sign-off and personal details:** every document checked for the draft banner and a sign-off block; all files searched for names, emails, organisations and personal wording (finding 9).
- **CI** (the checks GitHub runs on every PR): run [43826740937](https://github.com/Aliator/MedApp/actions/runs/43826740937) on `d2033f6` passed all five jobs (`web`, `helm`, `unit-tests`, `image`, `coverage-comment`).
- **Not done:** the app wasn't run; NHS England's own DCB0129 page wasn't fetched (a published copy of its tables was used); the legal citations taken from the handbook (DUAA section 121 and its date, CQC Regulation 17, the planned device consultation) weren't checked against their sources.

## Findings

Most severe first. None is high or critical. All are open at `d2033f6`.

### 1. The consequence scale isn't DCB0129's, and it rates harm to many patients lower

- **Severity:** medium. **Confidence:** high that the table differs from DCB0129's; medium on how much each rating would move.
- **What could go wrong:** the plan says its consequence scale is "from the DCB0129 implementation guidance", but it's a reworded version. DCB0129 rates each harm by **how many patients** it affects as well as how bad it is; the plan mostly drops that, and moves one line. In DCB0129, "severe injury … from which recovery is not expected in the short term", affecting **multiple** patients, is **Catastrophic**; the plan puts exactly that under **Major**. H-13 (a wrong template) is rated Major because a missed step affects "many patients". On DCB0129's scale that's Catastrophic: with Low likelihood it's a **4** ("mandatory elimination or control"), and even fully controlled (Very low) a 3. So the headline "None is rated 4 or 5" and "every hazard comes down to 2 or below" may not survive a CSO applying the standard's own table. It's like a ruler redrawn by hand: every measurement agrees with the others, but not with anyone else's ruler. The plan also adds "a delay to care that needs extra treatment" under Significant, which DCB0129 doesn't have.
- **Evidence:**
  - `docs/clinical-safety/clinical-risk-management-plan.md:85` ("from the DCB0129 implementation guidance") and `:89-95` (the reworded table); `docs/clinical-safety/clinical-safety-case-report.md:50`; `docs/clinical-safety/README.md:33`
  - `docs/clinical-safety/hazard-log.md:466` (H-13: "for many patients. Major") and `:475`, `:484`
  - the headline counts: `hazard-log.md:52`, `clinical-safety-case-report.md:25-27`, `README.md:26`, `docs/architecture.md:1817`
  - other hazards that affect many patients at once and may move up: H-10 (`hazard-log.md:367`), H-12 (`:437`), H-20 (`:683`)
- **Fix:** replace the consequence table with DCB0129's, including its "number of patients affected" column (see Detail). Re-rate H-13, H-10, H-12, H-14 and H-20 against it, and update the counts in the four places above. Or, if the reworded scale is wanted, stop calling it DCB0129's, and have the CSO record why it differs.
- **What happened to it:** open; recorded here.

### 2. My Tasks shows only work not yet started, so the hazard log leans on a list that hides started work

- **Severity:** medium. **Confidence:** high.
- **What could go wrong:** My Tasks opens filtered to **Not started**: procedures where no stage has been started. Anything already under way, including blocked and overdue work, appears only after changing the filter. The hazard log and CS-05 treat My Tasks as the safety net: "My Tasks always lists every procedure assigned to you, whether or not the notification arrived" (H-09), and "Blocked procedures stay on the assignee's My Tasks list" (H-05); CS-05 tells customers to train staff that "My Tasks is the list to work from". Scenario: a nurse is off sick, and an admin reassigns her in-progress procedure to a colleague. The live notification is lost (H-09's causes). The colleague opens My Tasks from the side navigation, sees "Nothing waiting to be started", and the procedure waits. It's a to-do list whose default tab shows only jobs nobody has touched, so a job handed over half-done never appears there.
- **Evidence:**
  - `MedApp.Web/src/pages/tasks/MyTasksPage.tsx:42` (`progress ?? 'notStarted'`) and `:161-162` (`Started: … progress === 'started'`, so `false` by default)
  - `MedApp.Web/src/pages/listSearch.ts:142` ("`progress` is Not started unless the URL says otherwise")
  - `MedApp.Application/Procedures/PatientProcedures/Queries/SearchPatientProcedures/ProcedureSearch.cs:97-103` (`Started == false` drops every procedure with a started or finished stage)
  - the claims: `docs/clinical-safety/hazard-log.md:206`, `:337`, `:346`; `docs/clinical-safety/dcb0160-deploying-organisations.md:75`, `:101`
- **Fix:** correct the three control statements to say what the default shows. Add it as a cause in H-04, H-05 and H-09. And add a further control, before first live use: open My Tasks on all open work (or show a count of started and overdue work beside the filter), so the list staff are told to rely on shows everything assigned to them.
- **What happened to it:** open; recorded here.

### 3. Deleting a leaver's account leaves their open work assigned to nobody, and CS-05 tells customers to do it

- **Severity:** medium. **Confidence:** high.
- **What could go wrong:** assignments have no foreign key to the user, and deleting a user doesn't touch them. A procedure assigned only to a deleted account stays **Assigned** (not Unassigned), shows "Deleted user", and is on nobody's My Tasks, because My Tasks matches the signed-in user's ID. The proposed "unassigned and due soon" view (H-04) wouldn't catch it either, since its status isn't Unassigned. Nothing tells anyone. CS-05 tells customers to "Remove leavers on their last day", and the only way to remove an account is to delete it. So following the guidance quietly orphans a leaver's open procedures. It's like post still addressed to someone who has left: it looks delivered, so nobody else opens it. The hazard log mentions deleting users only for who gets the credit for past work (H-18).
- **Evidence:**
  - `MedApp.Infrastructure/Data/Configurations/Procedures/PatientProcedures/PatientProcedureAssignmentConfiguration.cs:13-24` (a foreign key to the procedure only)
  - `MedApp.Infrastructure/Common/Identity/IdentityUserService.cs:99-108` (deletes the account, nothing else)
  - `MedApp.Application/Procedures/Common/ProcedureWorkflow.cs:55-56` (status counts assignments, not live accounts); `ProcedureSearch.cs:84-88` (My Tasks matches the user's ID)
  - `docs/clinical-safety/dcb0160-deploying-organisations.md:90` and `:129` (D-09); `docs/clinical-safety/hazard-log.md:163` (H-04 covers an assignee who's away, not one who's deleted) and `:619` (H-18)
- **Fix:** log it as a new hazard, H-21, "Work assigned to a deleted account drops off every task list". Further controls: refuse to delete an account with open assignments, or list them and require reassignment first (protective; before first live use, since CS-05 sends customers down this path); deactivate instead of delete (already planned, step 4). In CS-05, change line 90 to "Reassign a leaver's open procedures, then remove the account."
- **What happened to it:** open; recorded here.

### 4. Residual risk counts controls planned for later, so the risk at first live use isn't stated

- **Severity:** medium. **Confidence:** medium (the re-ratings are judgement, the gap isn't).
- **What could go wrong:** "residual risk" is defined as the risk once **all** further controls are built, including ones marked "later" (step 9 features, versions, approvals). But the go-live decision rests on the "before first live use" controls only, and no rating says where each hazard stands after those. CS-03 puts both in one table, so a CSO reading "Residual 2" next to "Controls needed before first live use" would take those controls to be enough. For H-13 they're an audit of template changes (which records a wrong edit, but doesn't stop one) and the customer's own governance. The Very low likelihood behind its residual 2 rests on versions and an approval step marked "later". With only the first-live-use controls, H-13 is plausibly still Major, Low: 3. H-07 is similar: a required note can be "nothing to record". It's like quoting a car's crash rating with airbags that are still on order.
- **Evidence:**
  - `docs/clinical-safety/hazard-log.md:18` (the definition), `:85`, `:281`, `:476-484` (H-13's controls and residual), `:52`
  - `docs/clinical-safety/clinical-safety-case-report.md:27` ("every hazard comes down to 2 or below") and `:119-140`
  - `docs/clinical-safety/clinical-risk-management-plan.md:139` ("Residual risk is re-rated once a control is in place and verified")
- **Fix:** add a third rating, "at first live use", to each hazard and to the summary tables: the risk once only the "before first live use" controls are built. Point the go-live rule (CS-03 section 7) at that rating. Where it stays at 3 (H-13 at least), either bring a control forward (the approval step) or note that the CSO must justify accepting it.
- **What happened to it:** open; recorded here.

### 5. The device change-control rule isn't in force anywhere, and the plan contradicts the README about it

- **Severity:** medium. **Confidence:** high.
- **What could go wrong:** the device decision holds only while new features stay outside the device triggers, and the documents say any such feature "triggers a new assessment before it's merged". But the trigger is the clinical safety section of a pull request, which isn't in a template or in `AGENTS.md`, the file the agents that write most of the code follow. The README says on purpose that the workflow doesn't change until a CSO is appointed; the plan says instead that "the author adds it by hand" and changes are merged "with the clinical safety section filled in". This PR has no such section. Scenario: a pull request adds a scored pre-assessment questionnaire from the to-do list; nothing in the workflow mentions CS-04, and it merges. The handbook names exactly this as a risk of skipping step 6 ("a later feature could make MedApp a medical device without anyone noticing").
- **Evidence:**
  - `docs/clinical-safety/intended-purpose-and-device-decision.md:22` and `:155-159`; `docs/clinical-safety/README.md:25`
  - `docs/clinical-safety/clinical-risk-management-plan.md:193` and `:209`, against `docs/clinical-safety/README.md:37`
  - `AGENTS.md` has no clinical safety or device rule; PR #60's description has no clinical safety section
  - `docs/architecture.md:1875` (the risk of skipping)
- **Fix:** add one rule to `AGENTS.md` now, which needs no CSO: "A change that adds a calculation, score, priority, triage, recommendation or alert from patient data, or changes the intended purpose, reassesses `docs/clinical-safety/intended-purpose-and-device-decision.md` in the same pull request." And make CS-01 section 8 say what the README says: the pull request section starts when a CSO is appointed, except for the device question.
- **What happened to it:** open; recorded here.

### 6. The release gate's list of paths misses files that hold the controls the log relies on

- **Severity:** medium. **Confidence:** high.
- **What could go wrong:** the gate decides which changes get a clinical safety review, by path. It covers Application and Domain, some web pages and components, and migrations. It leaves out files that the hazard log names as controls, or where its fixes would go. A change there would skip the review. Scenarios: the H-12 fix as proposed (an EF Core value converter or convention) lives in `MedApp.Infrastructure`, outside the gate; so does the `rowversion` for H-15. Changing My Tasks' default again (finding 2) is in `MedApp.Web/src/pages/listSearch.ts`, which `pages/{…}/**` doesn't match. Removing `[Authorize(Roles = "Admin")]` from the unlock endpoint is in `MedApp.API/Controllers`. It's a guard given a list of doors to watch that leaves out some of the doors.
- **Evidence:**
  - `docs/clinical-safety/clinical-risk-management-plan.md:175-183` (the list)
  - outside it, but cited as controls or fixes: `MedApp.Web/src/pages/listSearch.ts` (`hazard-log.md:206`), `MedApp.Web/src/app/queryClient.ts:9` (`:558`), `MedApp.API/Controllers/*` (`:162`, `:469`; `clinical-safety-case-report.md:66`), `MedApp.Infrastructure/Data/Repositories/PatientProcedureRepository.cs` (`hazard-log.md:686`), `MedApp.Contracts/Procedures/PatientProcedures/Responses/PatientProcedureResponse.cs:15` (`:406`), `MedApp.API/ServerRendering/SsrPages.cs` (`:372`), `MedApp.Infrastructure/Common/Identity/IdentityUserService.cs` (finding 3), and the H-12 and H-15 fixes (`:446`, `:543`)
- **Fix:** widen the list (Detail has one), or invert it: every change under `MedApp.*` and `deploy/` needs the section, which may say "no clinical impact, because …" in one line.
- **What happened to it:** open; recorded here.

### 7. "Stages worked out of order" isn't designed out once an admin unlocks a stage

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** the log says working stages out of order is "designed out: only the current stage can be started, edited or finished". That's true until an admin unlocks a finished stage. The unlocked stage still counts as finished, so the current stage stays where it was, and **both** can then be edited and finished. Scenario: an admin unlocks a consent stage to correct it; meanwhile the next stage, the procedure itself, is finished as normal, and the record shows consent as finished throughout. H-06 describes the unlock half of this, but not that work carries on past it.
- **Evidence:** `docs/clinical-safety/hazard-log.md:703`; `MedApp.Application/Procedures/Common/ProcedureWorkflow.cs:68-69` (`CurrentStage` skips finished stages) and `:92-98` (an unlocked finished stage is editable); `ProcedureChange.cs:98-106`
- **Fix:** reword line 703 to "designed out while no stage is unlocked", and add to H-06's effect that later stages can be worked and finished while an earlier one is reopened. H-06's proposed "Reopen" control (making it the current stage again) is the right fix.
- **What happened to it:** open; recorded here.

### 8. H-15 describes the notes-after-finish race as a stale page, which the same hazard says gets a 409

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** H-15 says notes can change after a stage is locked if someone "who loaded the procedure before that" saves notes. But each request reloads the procedure and checks it against the database (`GetForUpdateOrThrowAsync`, then `WhyStageCantBeEdited`), so a stale page gets a 409, as H-15's own existing controls say four lines later. The real case is two requests handled at the same moment: a narrow race, not a common one. Overstating it makes H-15's Medium likelihood look better supported than it is; the lost-update case for two people saving notes on the current stage is real, and is enough on its own.
- **Evidence:** `docs/clinical-safety/hazard-log.md:526` against `:536`; `MedApp.Application/Procedures/PatientProcedures/Commands/UpdateProcedureStageNotes/UpdateProcedureStageNotesHandler.cs:18-25`
- **Fix:** reword line 526: "Notes saved at the same moment a stage is finished: if both requests are handled at once, both pass their checks, and the notes change after the stage is locked."
- **What happened to it:** open; recorded here.

### 9. CS-05 has no sign-off, its banner names the wrong standard, and the README records how the drafting was instructed

- **Severity:** low. **Confidence:** high.
- **What could go wrong:**
  - CS-05 is the document handed to customers, but it has no "Approved by" row or sign-off table, and the README's finishing steps don't list it for the CSO to sign. Its banner says it "is not a completed **DCB0129** assessment", though it's DCB0160 guidance. A customer's CSO could take an unsigned draft as the manufacturer's position.
  - README line 31 records an instruction given while drafting, and calls what follows decisions "made". In a public file, it's a detail about how the work was commissioned rather than about the product, and it frames proposals (the risk scheme, incident times, retention) as settled.
- **Evidence:** `docs/clinical-safety/dcb0160-deploying-organisations.md:3` and `:5-12` (no approval row; compare `clinical-risk-management-plan.md:13`); `docs/clinical-safety/README.md:29-31` and `:49`
- **Fix:** in CS-05, change the banner to say it must be signed off by the manufacturer's CSO before it's given to any deploying organisation, and add an "Approved by" row and a sign-off table; add CS-05 to README step 5. Replace README lines 29-31 with "## Choices proposed in drafting" and "These are proposals for the CSO and the accountable owner to confirm:".
- **What happened to it:** open; recorded here.

### 10. Building H-04's control will make the intended purpose untrue

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** the intended purpose says MedApp "is not a scheduling, alerting or patient monitoring system", that "nothing alerts anyone" when a procedure is overdue, and that notifications are "for assignments only". H-04's control for first live use is overdue notifications. Once built, those lines, CS-03's "nothing alerts anyone" and CS-05's training point are wrong, and the intended purpose is what the MHRA judges a product by. The roadmap table in CS-04 already says workflow notifications stay outside the device definition, so the decision itself doesn't change; the wording does.
- **Evidence:** `docs/clinical-safety/hazard-log.md:180`; `docs/clinical-safety/intended-purpose-and-device-decision.md:35`, `:54`, `:55`; `docs/clinical-safety/clinical-safety-case-report.md:90`; `docs/clinical-safety/dcb0160-deploying-organisations.md:99`
- **Fix:** in H-04's control, note that it updates CS-04 (lines 35, 54, 55), CS-03 and CS-05 in the same pull request. Or word the intended purpose now as "not a clinical alerting or patient monitoring system; it may remind staff about workflow dates they set".
- **What happened to it:** open; recorded here.

### 11. Small inaccuracies

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** each is small, but a safety file is read as exact.
  - **H-09 is listed as transferred in CS-05, not in the log:** `dcb0160-deploying-organisations.md:75`; `hazard-log.md:346` marks the same control "proposed", not "transferred".
  - **A status the plan doesn't define:** "Open, to verify" (`hazard-log.md:42`, `:450`) isn't one of the plan's four statuses (`clinical-risk-management-plan.md:146`).
  - **Wrong file for a control:** "deleting a user ends their sessions (`SessionAuthenticationHandler`)" (`hazard-log.md:627`); the check is in `SessionService` (`MedApp.Infrastructure/Common/Authentication/SessionService.cs:47-49`).
  - **The "not assigned to you" question is weaker than stated:** H-15 counts it as a control (`hazard-log.md:538`), but it's asked once per visit and never of admins (`MedApp.Web/src/pages/procedures/ProcedurePage.tsx:120-127`).
  - **Five statuses, not eight:** CS-04's function table lists in progress, in review, completed, blocked and cancelled (`intended-purpose-and-device-decision.md:99`); the workflow also has unassigned, assigned and archived (`MedApp.Domain/Procedures/PatientProcedures/PatientProcedureStatus.cs`).
  - **The PR description says the H-12 defect "is fixed in its own PR";** no such pull request exists at review time (open pull requests are #57 to #60). The documents themselves correctly call it suspected and unverified.
- **Fix:** mark H-09's information control "transferred"; add "To verify" to the plan's statuses or use "Open"; cite `SessionService`; say "asked once per visit, and not of admins"; list all eight statuses; and when the H-12 fix lands, update H-12's status, the README's main findings, CS-03's defects table and the handbook's Status line together.
- **What happened to it:** open; recorded here.

## Remaining risks

- **Everything is provisional:** no CSO has seen any of it, and no clinician has walked a workflow. The ratings are one reading of the code.
- **H-12 is unverified,** and no fix exists yet. See Detail for a trap when checking it.
- **Hazards from real use are thin:** code reading finds what the code does, not how a ward uses it. Findings 2 and 3 came from following one handover and one leaver through the code; the hazard workshop should do this on purpose (handovers, leavers, bank and agency staff, night shifts).
- **External citations taken on trust:** the DUAA section 121 date, the MHRA guidance's last-updated date, CQC Regulation 17's wording and the planned device consultation come from the handbook, and weren't checked here.
- **Northern Ireland and the EU** aren't assessed, as CS-04 says.
- **Drift:** the hazard log cites classes and files, not lines, which ages well, but nothing checks that the controls it names still exist. The release gate (finding 6) is the only thing that would catch it.

## Say it like this

"MedApp now has a draft clinical safety file: a risk management plan, a hazard log of twenty hazards, a safety case, guidance for customers, and a reasoned decision that it's an administrative workflow tool, not a medical device. The code references are accurate and the device reasoning matches the MHRA's guidance, and every document is clearly a draft for a Clinical Safety Officer to sign. Before a CSO relies on it, three things need fixing: the consequence scale should be DCB0129's own, which may raise the template hazard to a 4; My Tasks hides started work by default, so it isn't the safety net the log says; and deleting a leaver's account leaves their open work assigned to nobody."

## Detail

- **The ratings, re-derived:** every initial and residual rating in `hazard-log.md` was looked up in the plan's matrix (`clinical-risk-management-plan.md:111-117`). All 40 match, for example Significant, High = 3 (H-12) and Significant, Very low = 1 (H-12 residual).
- **DCB0129's consequence table** (from the published copy used; the plan's version is at `clinical-risk-management-plan.md:89-95`):

  | Consequence | Harm | Patients |
  |---|---|---|
  | Catastrophic | Death | Multiple |
  | Catastrophic | Permanent life-changing incapacity, or a prognosis of death or it; severe injury or incapacity, recovery not expected in the short term | Multiple |
  | Major | Death | Single |
  | Major | Permanent life-changing incapacity, or a prognosis of death or it; severe injury or incapacity, recovery not expected in the short term | Single |
  | Major | Severe injury or incapacity, recovery expected in the short term | Multiple |
  | Major | Severe psychological trauma | Multiple |
  | Considerable | Severe injury or incapacity, recovery expected in the short term | Single |
  | Considerable | Severe psychological trauma | Single |
  | Considerable | Minor injury or injuries, recovery not expected in the short term | Multiple |
  | Considerable | Significant psychological trauma | Multiple |
  | Significant | Minor injury or injuries, recovery not expected in the short term | Single |
  | Significant | Significant psychological trauma | Single |
  | Significant | Minor injury, recovery expected in the short term | Multiple |
  | Significant | Minor psychological upset; inconvenience | Multiple |
  | Minor | Minor injury, recovery expected in the short term; minor psychological upset; inconvenience; any negligible consequence | Single |

  The likelihood scale, the matrix and the five acceptability levels in the plan match DCB0129's exactly.
- **My Tasks, from the side navigation to SQL:** the nav links to `/tasks` with no query (`AppSideNav.tsx:481`), so `progressOf` returns `'notStarted'` (`MyTasksPage.tsx:42`), `taskQuery` sends `Started: false` (`:162`), and `ProcedureSearch.Filter` keeps only procedures with no started or finished stage (`ProcedureSearch.cs:100-102`). The "Overdue" and "Blocked" quick filters do show started work, because applying them through the bar sets the progress to "any". `docs/procedures-ui-plan.md:133` still describes the older design, two lists (Not started and Started) side by side, which would have met H-09's claim.
- **Deleting a user:** `DeleteUserHandler` → `IdentityUserService.DeleteUserAsync` → `UserManager.DeleteAsync`. `PatientProcedureAssignments` keys on `(PatientProcedureId, UserId)` with no relationship to the user table, so the rows stay. `ProcedureMapper` then shows "Deleted user" for the assignee.
- **A wider release gate list, for finding 6:**

  ```markdown
  - the API's controllers, middleware and server rendering: `MedApp.API/Controllers/**`, `MedApp.API/Middleware/**`, `MedApp.API/ServerRendering/**`
  - contracts: `MedApp.Contracts/**`
  - data access: `MedApp.Infrastructure/Data/**` (the DbContext, configurations, repositories and migrations)
  - accounts: `MedApp.Infrastructure/Common/Identity/**`, `MedApp.Infrastructure/Common/Authentication/**`, `MedApp.Web/src/pages/users/**`
  - the web app's shared parts: `MedApp.Web/src/pages/*.ts`, `MedApp.Web/src/app/**`, `MedApp.Web/src/api/**`, `MedApp.Web/src/components/shared/RichText.tsx`, `MedApp.Web/src/components/forms/RichTextField.tsx`, `MedApp.Web/src/components/navigation/**`
  - deployment, where it affects availability or data: `deploy/**`, `MedApp.API/Dockerfile`
  ```

- **The device decision against the MHRA guidance:** the linked PDF (v1.10g) contains, word for word, each phrase CS-04 quotes: "stores or transmits medical data without change", the electronic patient record and appointment-booking examples, the decision support bullets ("just follows the path of a procedure/treatment - there are no decisions", "the health care professional decides which path to take", "influence the actual treatment", "results in a diagnosis or prognosis"), "applies automated reasoning such as a simple calculation…", and the module example "intended to indicate the risk that a specific patient has of developing a disease". Its flow chart asks: software in a system or module; computer program or functional document; medical purpose; then works with a device, and works with data obtained in vitro. CS-04's four steps cover the same questions in a slightly different order and reach the same result. The guidance also says general disclaimers don't help if claims are made elsewhere, and that a manufacturer's own view isn't decisive. CS-04's "claims the manufacturer must not make" is the right response to both.
- **A trap when verifying H-12:** in production the page is first rendered by Node in the container, which runs in UTC, so a time without a `Z` is read as UTC and shown correctly in the server's HTML. The browser then re-renders it in its own time zone, and on a UK browser in summer it changes to an hour early (React reports a hydration mismatch). Checking the page source, or a test that only renders on the server, would wrongly clear H-12. Check the page after it has loaded in a browser during British Summer Time, or the API's JSON for a missing `Z`.
- **The searches:**
  - `grep -rn -E "IsRowVersion|IsConcurrencyToken|\[Timestamp\]"` in Domain and Infrastructure: none (H-15's cause holds)
  - `grep -rn -E "DateTimeKind|HasConversion|JsonConverter"` in Infrastructure, Application and the API: only the date of birth converter and `DueDates` (H-12's cause holds)
  - names, emails, organisations and personal wording across `docs/clinical-safety/`: no names or emails; one line about how the drafting was instructed (finding 9)
  - `gh pr list --state all --limit 8`: #57 to #60 open, no H-12 fix
  - CI run 43826740937: `head_sha` `d2033f6`, all five jobs `success`
