# MedApp: hazard log

**Draft prepared for review. It must be reviewed, completed and signed off by a qualified Clinical Safety Officer before use; it is not a completed DCB0129 assessment.**

| | |
|---|---|
| **Document** | CS-02, hazard log (DCB0129) |
| **Version** | 0.4, draft |
| **Date** | 16 November 2026 |
| **System** | MedApp, `main` at `88dc1fa` (the merge of #56); H-12 updated for its fix in PR #62; H-01, H-02 and H-13 updated for branch `feature/nhs-integrations` (the NHS number, the patient banner, the FHIR API, SNOMED CT codes and the demographics lookup) |
| **Drafted by** | An AI coding agent, for the manufacturer, from the code. Not yet reviewed by a person or a clinician. |
| **Approved by** | [Clinical Safety Officer] |

## How to read this log

- **Where the hazards came from:** reading the code and walking each workflow (`clinical-risk-management-plan.md`, section 4.1). No clinician has reviewed them yet, and the hazard workshop is still to come. Every rating is a provisional estimate for the CSO to confirm or change.
- **Ratings** use DCB0129's consequence table (which counts how many patients a harm affects), its likelihood scale and its 5×5 matrix, as set out in the plan (section 4). Written as consequence, likelihood and rating, for example "Major, Low: **3**".
- **Three ratings per hazard:**
  - **Initial risk:** with the controls that exist in `main` today.
  - **At first live use:** once only the further controls marked "before first live use" are built and verified. **The go-live rule uses this rating** (plan, section 4.4).
  - **Residual risk:** once every further control listed, including those marked "later", is built and verified.

  A planned control doesn't lower the risk until it's built.
- **Existing controls** name the code that provides them. They are only those in the code today.
- **Further controls** are marked with:
  - their **type:** design (designs the hazard out), protective (a check, lock, confirmation or warning in the software), or information (instructions, training);
  - their **status:** **planned** (in `docs/architecture.md`'s roadmap or `docs/todo.md`, with the step) or **proposed** (new, suggested by this log);
  - **when** they're needed: **before first live use**, or **later**;
  - **transferred** if the deploying organisation provides them (`dcb0160-deploying-organisations.md`).
- **Assumption behind every rating:** the deploying organisation identifies patients at the point of care by its own positive identification process, and uses MedApp within its intended purpose (`intended-purpose-and-device-decision.md`). If it doesn't, several ratings go up.

## Summary

| ID | Hazard | Initial risk | At first live use | Residual risk | Needs a product change? | Status |
|---|---|---|---|---|---|---|
| [H-01](#h-01-the-wrong-patients-procedure-is-worked) | The wrong patient's procedure is worked | Major, Low: **3** | Major, Very low: **2** | Major, Very low: **2** | **Yes, before first live use** (the banner built on branch `feature/nhs-integrations`, to verify; the rest proposed) | Open |
| [H-02](#h-02-work-is-recorded-on-the-wrong-record) | Work is recorded on the wrong record | Considerable, Medium: **3** | Considerable, Low: **2** | Considerable, Low: **2** | **Yes, before first live use** (the banner built on branch `feature/nhs-integrations`, to verify; the rest proposed) | Open |
| [H-03](#h-03-a-patients-details-are-changed-or-deleted-without-trace) | A patient's details are changed or deleted without trace | Considerable, Low: **2** | Considerable, Low: **2** | Considerable, Very low: **2** | Yes, later | Open |
| [H-04](#h-04-an-overdue-procedure-is-missed) | An overdue procedure is missed | Considerable, Medium: **3** | Considerable, Low: **2** | Considerable, Low: **2** | **Yes, before first live use** | Open |
| [H-05](#h-05-a-blocked-procedure-is-forgotten) | A blocked procedure is forgotten | Considerable, Medium: **3** | Considerable, Low: **2** | Considerable, Low: **2** | **Yes, before first live use** | Open |
| [H-06](#h-06-a-stage-is-finished-in-error) | A stage is finished in error | Considerable, Low: **2** | Considerable, Low: **2** | Considerable, Very low: **2** | Yes, later | Open |
| [H-07](#h-07-a-stage-is-recorded-as-finished-when-the-work-wasnt-done) | A stage is recorded as finished when the work wasn't done | Major, Low: **3** | Major, Low: **3** | Major, Very low: **2** | **Yes, before first live use** | Open |
| [H-08](#h-08-a-procedure-is-cancelled-completed-or-archived-in-error) | A procedure is cancelled, completed or archived in error | Considerable, Low: **2** | Considerable, Low: **2** | Considerable, Very low: **2** | Yes, later | Open |
| [H-09](#h-09-a-member-of-staff-doesnt-learn-of-work-assigned-to-them) | A member of staff doesn't learn of work assigned to them | Considerable, Medium: **3** | Considerable, Low: **2** | Considerable, Very low: **2** | **Yes, before first live use** | Open |
| [H-10](#h-10-medapp-is-unavailable) | MedApp is unavailable | Major, Medium: **3** | Major, Low: **3** | Major, Very low: **2** | **Yes, before first live use** | Open |
| [H-11](#h-11-due-dates-dont-mean-what-staff-think) | Due dates don't mean what staff think | Considerable, Low: **2** | Considerable, Low: **2** | Considerable, Very low: **2** | Yes, later | Open |
| [H-12](#h-12-times-are-shown-an-hour-out) | Times are shown an hour out | Considerable, High: **3** | Considerable, Very low: **2** | Considerable, Very low: **2** | **Yes, before first live use** (built in PR #62; to verify) | Open |
| [H-13](#h-13-a-template-or-stage-definition-has-wrong-content) | A template or stage definition has wrong content | Catastrophic, Low: **4** | Catastrophic, Very low: **3** | Catastrophic, Very low: **3** | **Yes, before first live use** | Open |
| [H-14](#h-14-a-correction-to-a-template-doesnt-reach-procedures-already-under-way) | A correction to a template doesn't reach procedures already under way | Major, Low: **3** | Major, Very low: **2** | Major, Very low: **2** | **Yes, before first live use** | Open |
| [H-15](#h-15-two-people-edit-the-same-procedure-at-once) | Two people edit the same procedure at once | Considerable, Medium: **3** | Considerable, Very low: **2** | Considerable, Very low: **2** | **Yes, before first live use** | Open |
| [H-16](#h-16-the-screen-shows-out-of-date-information) | The screen shows out-of-date information | Significant, Medium: **2** | Significant, Medium: **2** | Significant, Low: **2** | Yes, later | Open |
| [H-17](#h-17-stage-instructions-arent-read) | Stage instructions aren't read | Considerable, Low: **2** | Considerable, Low: **2** | Considerable, Very low: **2** | Yes, later | Open |
| [H-18](#h-18-a-change-is-recorded-under-the-wrong-persons-name) | A change is recorded under the wrong person's name | Significant, Medium: **2** | Significant, Medium: **2** | Significant, Low: **2** | Yes, later | Open |
| [H-19](#h-19-part-of-the-clinical-record-is-lost-by-deletion) | Part of the clinical record is lost by deletion | Considerable, Low: **2** | Considerable, Low: **2** | Considerable, Very low: **2** | Yes, later | Open |
| [H-20](#h-20-data-is-lost-or-restored-to-an-earlier-point) | Data is lost, or restored to an earlier point | Major, Low: **3** | Major, Very low: **2** | Major, Very low: **2** | Operational, before first live use | Open |
| [H-21](#h-21-work-assigned-to-a-deleted-account-drops-off-every-task-list) | Work assigned to a deleted account drops off every task list | Major, Medium: **3** | Major, Very low: **2** | Major, Very low: **2** | **Yes, before first live use** | Open |

**Initial ratings, at `88dc1fa`:** one hazard is rated 4 (H-13), and twelve are rated 3 (H-01, H-02, H-04, H-05, H-07, H-09, H-10, H-12, H-14, H-15, H-20 and H-21). None is rated 5. Since then, H-12's defect is fixed (PR #62), which brings it to its residual 2 once the CSO has verified the fix, leaving eleven at 3.

**At first live use,** with every "before first live use" control built: none is above 3, and three stay at 3 (H-07, H-10 and H-13). Under the plan's go-live rule, each of those three needs the CSO's written justification before first live use. H-13 gets to 3 only because its approval step is brought forward to before first live use.

**The patient banner,** the main control for H-01 and H-02, is built on branch `feature/nhs-integrations`, with the NHS number. It doesn't change any rating yet: the initial ratings are for `88dc1fa`, and the "at first live use" ratings already assumed it. Those two ratings now depend on the CSO verifying the banner, and on the other controls marked "before first live use" (the date of birth in lists, the patient's name in the page title and in the confirmations), which aren't built.

**Residual,** with every control built: H-13 stays at 3, because the worst credible harm of a wrong template, affecting many patients, stays catastrophic however unlikely it's made. All the others come down to 2.

---

## H-01: The wrong patient's procedure is worked

**Description.** A member of staff opens, and records work on, a procedure that belongs to a different patient from the one in front of them, believing it's the right one.

**Causes:**
- Patients are identified by first name, last name, date of birth and email only. There's no NHS number or local hospital number, and nothing stops two patients with the same name and date of birth (`MedApp.Domain/Patients/Patient.cs`, `CreatePatientHandler`). **Since branch `feature/nhs-integrations`,** a patient can have an NHS number, optional and unique, so two records with the same number are refused; but two patients with the same name and date of birth, or one patient recorded twice with the number on only one record, still aren't caught. There's still no local hospital number.
- The procedure page shows the patient's name only, in the "Patient info" part, which can be folded away. It doesn't show the date of birth. The page's title is the procedure's title, not the patient's name (`MedApp.Web/src/pages/procedures/ProcedurePage.tsx`, `details()`). Branch `feature/nhs-integrations` adds the patient banner above it (further controls); the title is unchanged.
- **A wrong NHS number** (branch `feature/nhs-integrations`): one mistyped, or the right digits typed on the wrong patient's record. The Modulus 11 check digit catches any single mistyped digit and most swapped pairs, but not a valid number that belongs to someone else. Nothing traces numbers with PDS, so MedApp never confirms that a number is the patient's (the FHIR API says so: "02 Number present but not traced"). The banner then shows a precise-looking identifier that's wrong, and another system reading the patient through the FHIR API could match them to the wrong person.
- **The demographics lookup** fills a patient's name and date of birth from an NHS number, so a wrong number can fill in another person's details. Today it's off by default, and its only source is an offline stub of four synthetic people; the real PDS isn't connected. Since the review of #69: on Edit Patient it shows who it found beside the record's details and fills only on "Use These Details"; on Add Patient it fills at once; and on either, if the number is changed after a lookup while the filled details are unchanged, the save is refused until the new number is looked up or the details are changed by hand.
- Task lists and procedure lists show the patient's name only (`MyTasksPage.tsx`, `ProceduresListPage.tsx`). Search matches on part of a name (`ProcedureSearch.Filter`).
- Two pages can be open side by side (half views), and procedures can be pinned in the side navigation.

**Effect.** Notes are written on the wrong patient's procedure, or a stage is finished on it, and the other patient's procedure is left undone (see also H-02). If staff act on what they read, a step could be done, or skipped, for the wrong patient.

**Possible harm.** A step of care given to or withheld from the wrong patient, for example a pre-procedure check recorded as done for a patient who didn't have it. One patient at a time; Major in the worst credible case (death or permanent incapacity, single patient).

**Existing controls:**
- New procedures are created from a patient's own page, and the New procedure page says which patient it's for ("For {name}", `NewProcedurePage.tsx`).
- The patients list shows date of birth and age, and View Patient shows the full record (`PatientsListPage.tsx`, `ViewPatientPage.tsx`).
- A procedure can't be moved to another patient: the update command has no patient field (`UpdatePatientProcedureCommand`).
- Every change is audited with who and when, so a wrong-patient entry can be found and traced (`ProcedureChange.Audit`).
- **Transferred:** positive patient identification at the point of care by the organisation's own process.

**Initial risk:** Major, Low: **3**.

**Further controls:**
- **A patient banner** on every procedure page and dialog that changes a procedure: full name, date of birth and age, and from step 9 the NHS number, always visible, not foldable. Protective; **built** on branch `feature/nhs-integrations` (`PatientBanner`): `SURNAME, Forename` in bold, the date of birth with the age, and the NHS number as `XXX XXX XXXX` (or "No NHS number"), with tokens for a restricted, disputed or anonymised record; not foldable, under the page header of the patient's page, Edit Patient, the export page, the patient's procedures, the procedure page, Edit Procedure and New Procedure; it stays in view on every one but the printable export page (the forms scroll inside their cards under it), wraps in a narrow window or half view, is a region named for the patient, upper-cases the surname by CSS so screen readers read it as a name, and gives young children's ages in months, weeks or days. Edit Patient shows it from the saved record, and its save confirmation shows the old and new name, date of birth and NHS number when they change. A test renders every patient and procedure route and checks for it. Not shown: sex or gender, which MedApp doesn't hold yet (`docs/todo.md`). Procedure responses carry the patient, so the procedure pages need no second read to show it. **Before first live use,** to be verified by the CSO. A dialog over the page can hide the banner, so dialogs that change a procedure rely on naming the patient themselves (H-02's confirmations).
- **Show the date of birth** beside the patient's name in task lists and procedure lists, and put the patient's name in the procedure page's title. Protective; **proposed**; before first live use. Not done on branch `feature/nhs-integrations`.
- **NHS number** (validated, unique within an organisation) and **a duplicate check** when a patient is added. Design. **Built in part** on branch `feature/nhs-integrations`: the number is optional, checked with its check digit on the server and in the form, unique across the deployment (within an organisation once step 8 adds them), and searchable; an exact duplicate number is refused with a 409. **Still planned** (step 9): a duplicate check on name and date of birth, and merging; later.
- **Trace NHS numbers with PDS,** so a number is confirmed to be the patient's, and have the lookup show who it found and ask staff to confirm before it fills the form. Protective. The confirmation is **built** on Edit Patient, with the hold on a changed number on both forms (above); Add Patient still fills at once. Tracing is **planned** (step 9, after NHS England's onboarding); before the real PDS is connected, Add Patient should confirm as Edit Patient does.
- **Train staff** to check the banner against the patient before recording work. Information; transferred.

**At first live use:** Major, Very low: **2**. Unchanged by branch `feature/nhs-integrations`: this rating already assumed the banner, and it holds only once the CSO has verified it and the proposed controls above are built.

**Residual risk:** Major, Very low: **2**. The NHS number makes two patients harder to confuse, but adds the wrong-number cause above, which tracing with PDS controls; on balance the rating is unchanged.

**Owner:** [CSO]. **Status:** Open: the banner is built and awaits the CSO's verification; the other controls needed before first live use aren't built.

---

## H-02: Work is recorded on the wrong record

**Description.** Information meant for one procedure, stage or patient is saved on another: a procedure is created for the wrong patient, notes are typed into the wrong stage or procedure, or the wrong template is chosen.

**Causes:**
- The same identification limits as H-01.
- Half views put two procedures side by side.
- The notes box always belongs to the current stage, so a note about an earlier stage can only be added to the current one, unless an admin unlocks the earlier stage.
- Template names can be similar, and the template list is the organisation's own.

**Effect.** The right record lacks the information and the wrong record carries it. Someone reading either can act on the wrong information.

**Possible harm.** A delayed or wrong step of care for one patient. Considerable.

**Existing controls:**
- New procedure lists the chosen template's stages before it's created, and fills the title from the template's name (`NewProcedurePage.tsx`; `docs/procedures-ui-plan.md`, phase 4).
- A procedure created for the wrong patient can be cancelled with a reason and archived, and an admin can then delete it; its audit entries are kept (`ProcedureWorkflow.AllowedStatusChanges`, `CanBeDeleted`; `DeletePatientProcedureHandler`).
- Notes on a stage can be corrected while it's current, and after an admin unlocks it; every save keeps the old and new text in the audit log (`ProcedureChange.UpdateStageNotes`).
- A finished stage is locked (`ProcedureChange.FinishStage`), so a wrong entry can't be made on an earlier stage by accident.

**Initial risk:** Considerable, Medium: **3**.

**Further controls:**
- **The patient banner** from H-01. Protective; **built** on branch `feature/nhs-integrations`, on the procedure page, Edit Procedure and New Procedure, and Edit Patient (so a procedure is created under the patient's banner); before first live use, to be verified by the CSO.
- **The procedure and stage named** in the Save Notes and Finish Stage confirmations ("Save notes on {stage} of {procedure} for {patient}?"). The finish confirmation already names the stage (`ProcedurePage.tsx`); add the patient. Protective; **proposed**; before first live use. Not done on branch `feature/nhs-integrations`.
- **An "entered in error" action** for stage notes and procedures, which strikes the entry through, keeps it in the record and asks for a reason, instead of only overwriting or deleting. Design; **proposed**; later.
- **Train staff** to correct a wrong entry with a note saying so, never by deleting it. Information; transferred.

**At first live use:** Considerable, Low: **2**. As for H-01, this already assumed the banner, and holds once the CSO has verified it and the confirmations name the patient.

**Residual risk:** Considerable, Low: **2**.

**Owner:** [CSO]. **Status:** Open: the banner is built and awaits the CSO's verification; the confirmations aren't changed yet.

---

## H-03: A patient's details are changed or deleted without trace

**Description.** A patient's name or date of birth is changed in error (for example, the wrong patient edited), or a patient is deleted, and nothing records what it was before or who did it.

**Causes:**
- Any staff member can edit any patient, and the change overwrites the old values (`UpdatePatientHandler`).
- Any staff member can permanently delete a patient who has no procedures (`DeletePatientHandler`).
- Since roadmap step 7 (branch `feature/privacy-features`), both are recorded in the activity log, with who, when and which fields changed, but **not the old values** (the log holds no patient details by design), so what a field said before can't be recovered from it.

**Effect.** All of that patient's procedures now show the new, wrong details. An admin can see who changed which fields and when (the activity log, and the patient's access report), but not what they said before. A deleted patient's record is gone.

**Possible harm.** Wrong-patient errors (H-01) on every procedure of that patient; a patient lost from follow-up. One patient; Considerable.

**Existing controls:**
- A patient with procedures can't be deleted (409; `DeletePatientHandler`).
- Procedure audit entries keep the patient's ID, so procedures can still be traced to the patient row.
- Patient creates, edits (the fields changed) and deletes are recorded in the activity log with the user, time and address, in the same save as the change (`UpdatePatientHandler`, `DeletePatientHandler`, `PatientRepository`; step 7).
- A restricted record can't be edited or deleted, nor its procedures changed (`PatientRules`; step 7), and a disputed one carries a banner saying what the patient disputes.

**Initial risk:** Considerable, Low: **2**.

**Further controls:**
- **Audit patient changes** (create, edit, delete). Protective; **built** (step 7) for who, when and which fields. **Still planned:** keeping the old values (a version table, step 7); later. Rated 2 today, but the CSO may bring it forward.
- **Make deleting a patient admin-only.** Protective; **planned** (step 2); later.
- **Show "details changed on {date}"** on the patient banner for a recent change. Protective; **proposed**; later.

**At first live use:** Considerable, Low: **2**.

**Residual risk:** Considerable, Very low: **2**.

**Owner:** [CSO]. **Status:** Open.

---

## H-04: An overdue procedure is missed

**Description.** A procedure passes its due date, and nobody notices in time to act.

**Causes:**
- **Nothing tells anyone.** "Overdue" is a badge on lists and on the procedure page (`DueDateCell`, `ProcedureCells.tsx`), computed when the page loads (`ProcedureWorkflow.IsOverdue`). There's no notification, email or escalation for overdue work; the only notification is for being assigned (`docs/todo.md`, "More notifications").
- **My Tasks opens on work not yet started.** Its default filter is "Not started" (`MyTasksPage.tsx`, `progressOf`), which sends `Started: false`, so every procedure with a started or finished stage is left out, overdue or not (`ProcedureSearch.Filter`). Started work shows only after changing the filter, or through the "Overdue" quick filter.
- **Unassigned procedures are on nobody's task list.** My Tasks shows only procedures assigned to you (`ProcedureSearch.Filter`, `AssignedToMe`). An unassigned procedure is visible only on the full Procedures list.
- **Assignment depends on admins:** only admins can assign (`[Authorize(Roles = "Admin")]` on the assignment endpoints), so if no admin is about, new procedures stay unassigned.
- **The assignee is away:** assignment doesn't stop anyone else working on it, but nothing reassigns it or tells anyone it's waiting. (An assignee whose account is deleted is H-21.)
- **Pages don't refresh by themselves:** a list left open overnight keeps its old overdue badges (H-16).

**Effect.** A step of care isn't done by the date staff set for it.

**Possible harm.** Delayed care for one patient: a delayed test, review or procedure. Considerable in the worst credible case for the procedures MedApp is meant for; it could be worse for a time-critical one, which is outside the intended purpose.

**Existing controls:**
- The overdue rule is the same everywhere, in one place (`ProcedureWorkflow.IsOverdue`, and the same rule in SQL for the "overdue only" filter, `ProcedureSearch.Filter`), with tests (`ProcedureWorkflowTests`, `IsOverdue_StatusAndDueDate_OnlyOpenProceduresPastTheirDueDate`).
- Lists sort by due date, soonest first, by default, and hide closed procedures (`ProcedureSearch.Sort`, `Filter`).
- The Procedures list has an "overdue only" filter, and its searches can be saved as favourites.
- My Tasks has an "Overdue" quick filter, which shows started and not-started work, and highlights overdue procedures (`MyTasksPage.tsx`).
- Due dates can't be set in the past when a procedure is created (`CreatePatientProcedureValidator`).

**Initial risk:** Considerable, Medium: **3**.

**Further controls:**
- **Overdue notifications** to the assignees, and to admins for unassigned procedures, the day a procedure becomes overdue, through `INotificationSender`. Protective; **planned** (`docs/todo.md`; step 9 adds email); **before first live use** for the in-app notification. The intended purpose already allows workflow reminders about dates staff set (`intended-purpose-and-device-decision.md`); when this is built, update the statements that "nothing alerts anyone" in CS-03 and CS-05 in the same pull request.
- **A visible count of started and overdue work on My Tasks** beside its filter, or opening My Tasks on all open work, so the list staff are told to work from shows everything assigned to them. Protective; **proposed**; before first live use.
- **An "unassigned and due soon" view** on the Welcome page or My Tasks for admins, so nothing waits unseen for assignment. Protective; **proposed**; before first live use.
- **A daily digest or escalation to a lead.** Protective; **planned** (step 9); later.
- **A local process:** who reviews the overdue list, and how often; at least two admins so assignment never waits on one person. Information; transferred.

**At first live use:** Considerable, Low: **2**.

**Residual risk:** Considerable, Low: **2**.

**Owner:** [CSO]. **Status:** Open.

---

## H-05: A blocked procedure is forgotten

**Description.** A procedure is blocked (waiting on something outside MedApp), and stays blocked long after the reason has gone, or past the date it was needed.

**Causes:**
- **A blocked procedure is never overdue,** by design: "Blocked procedures are waiting on something else, so never overdue" (`ProcedureWorkflow.IsOverdue`; `docs/procedures-ui-plan.md`, decision 15). Its due date can pass with no badge.
- A block has a reason but no review date, and nothing reminds anyone about it.
- The "overdue only" filter leaves blocked procedures out (`ProcedureSearch.Filter`).
- **My Tasks opens on "Not started"** (H-04), so a blocked procedure that had a stage started before it was blocked doesn't show there by default.

**Effect.** The procedure drops out of everyone's attention, and the patient's care stalls.

**Possible harm.** Delayed care for one patient. Considerable.

**Existing controls:**
- Blocking needs a reason, kept in the audit log (`ProcedureWorkflow.RequiresReason`, `ChangePatientProcedureStatusValidator`).
- Blocked procedures are among My Tasks' statuses (`OPEN_PROCEDURE_STATUSES`, `MedApp.Web/src/pages/listSearch.ts`): they show by default only if no stage was started, and otherwise under the "Started" filter or the "Blocked" quick filter. Their status shows on every list.
- The procedure page says what status it was blocked from (`ProcedurePage.tsx`).

**Initial risk:** Considerable, Medium: **3**.

**Further controls:**
- **Show "past due date" on blocked procedures** (a separate, quieter badge than overdue), and include them in an "overdue or past due" filter. Protective; **proposed**; before first live use.
- **The count of started work on My Tasks** from H-04. Protective; **proposed**; before first live use.
- **A review date on each block,** asked for when blocking, with a notification when it arrives. Protective; **proposed**; later.
- **Notify the assignees when a procedure is blocked.** Protective; **planned** (`docs/todo.md`); later.
- **A local process** for reviewing blocked procedures. Information; transferred.

**At first live use:** Considerable, Low: **2**.

**Residual risk:** Considerable, Low: **2**.

**Owner:** [CSO]. **Status:** Open.

---

## H-06: A stage is finished in error

**Description.** Someone finishes a stage that isn't complete, or the wrong stage, by mistake.

**Causes:**
- A slip: Finish Stage is the main button beside Save Notes (`StageWork.tsx`).
- The wrong procedure (H-01, H-02).

**Effect:**
- The stage locks, and the next stage becomes the current one, so the procedure appears to have moved on. Staff picking up the next stage may assume the earlier one was done.
- Only an admin can unlock it; staff can't. After an unlock, the stage still counts as finished (`IsCompleted` stays true): unlocking reopens its notes, but the record still says it was finished, by whom and when (`ProcedureChange.UnlockStage`). There's no way to mark a stage "not done".
- **Work carries on past an unlocked stage.** Because the unlocked stage still counts as finished, the current stage doesn't move back to it (`ProcedureWorkflow.CurrentStage` skips finished stages), so later stages can be worked and finished while the earlier one is reopened. For example, the procedure stage can be finished while a consent stage is unlocked for correction, and the record shows consent as finished throughout.

**Possible harm.** A step relied on that wasn't done, for one patient. Considerable.

**Existing controls:**
- Finishing asks for confirmation, naming the stage: "The stage will be locked and the procedure moves on to the next one. An admin can unlock it if it needs correcting." (`ProcedurePage.tsx`, the `finish` dialog).
- Only the current stage can be finished, and stages are worked strictly in order while none is unlocked, checked by the API (`ProcedureWorkflow.WhyStageCantBeEdited`; `FinishProcedureStageHandler` returns 409 otherwise). Tested in `ProcedureWorkflowTests` and `FinishProcedureStageHandlerTests`.
- The finish, any unlock, and a later finish are audited (`ProcedureChange.FinishStage`, `UnlockStage`).
- An admin can unlock a finished stage while the procedure is open or completed; a completed or in-review procedure goes back to in progress (`ProcedureWorkflow.CanUnlockStage`, `ProcedureChange.UnlockStage`).
- A procedure can't be completed while any stage is unlocked (`ProcedureWorkflow.AllStagesFinished`; `ChangePatientProcedureStatusHandler`).

**Initial risk:** Considerable, Low: **2**.

**Further controls:**
- **"Reopen" instead of "unlock":** let an admin mark a finished stage as not finished, with a reason, so the record shows it wasn't done and it becomes the current stage again, holding up later stages until it's finished. Design; **proposed**; later.
- **Show an unlocked stage clearly** in lists and task lists ("correction in progress"). Protective; **proposed**; later.
- **Notify the assignees when a stage is unlocked.** Protective; **planned** (`docs/todo.md`); later.
- **At least two admins** available while staff work, so a correction isn't delayed. Information; transferred.

**At first live use:** Considerable, Low: **2**.

**Residual risk:** Considerable, Very low: **2**.

**Owner:** [CSO]. **Status:** Open.

---

## H-07: A stage is recorded as finished when the work wasn't done

**Description.** A stage is finished in MedApp, deliberately, but the work it stands for wasn't done, or wasn't done fully, and the notes don't say so.

**Causes:**
- **Finishing needs nothing.** A stage can be finished with empty notes (`FinishProcedureStageValidator` checks only the IDs), and stages have no structured fields to say what was checked.
- Staff finish stages to keep the list tidy, or to move on to the stage they need.
- The stage's instructions weren't read (H-17).

**Effect.** Everyone after relies on a record that says the stage was done. For a stage such as consent or a pre-procedure check, the next steps go ahead on that basis.

**Possible harm.** A procedure carried out without a required step, such as a check or consent. One patient; Major in the worst credible case.

**Existing controls:**
- The finish confirmation (H-06).
- Who finished each stage, and when, is shown on the stage and kept in the audit log (`StageList.tsx`; `ProcedureChange.FinishStage`), so it's accountable.
- **Transferred:** MedApp records the workflow; the clinical record of what was done stays in the organisation's clinical systems, and the professional who does the step is accountable for it.

**Initial risk:** Major, Low: **3**.

**Further controls:**
- **Ask for notes, or an explicit "nothing to record", before a stage can be finished,** with a per-definition setting for stages where notes are mandatory. Protective; **proposed**; before first live use. It makes finishing deliberate, but "nothing to record" can still be chosen without the work, so it doesn't lower the likelihood much on its own.
- **Structured stage fields** (for example, "consent form signed: yes or no") on the stages that need them. Design; **planned** (step 9); later. Must stay recording only (`intended-purpose-and-device-decision.md`).
- **Training and local policy:** finishing a stage is a statement that its work was done. Information; transferred.

**At first live use:** Major, Low: **3**. Needs the CSO's written justification for first live use (plan, section 4.4), or structured fields brought forward for the stages that matter most.

**Residual risk:** Major, Very low: **2**.

**Owner:** [CSO]. **Status:** Open.

---

## H-08: A procedure is cancelled, completed or archived in error

**Description.** A procedure's status is changed by mistake, or for the wrong procedure.

**Causes:**
- A slip, or the wrong procedure (H-01).
- Any staff member can block, cancel, complete or archive, whether or not it's assigned to them; the "not assigned to you" question isn't asked for status changes, only their own confirmation (`ProcedurePage.tsx`, the status buttons).

**Effect:**
- **Cancelled:** can't be reopened. The only way on is archiving (`ProcedureWorkflow.AllowedStatusChanges`), so the procedure has to be created again, and its earlier stages' work is on the old one.
- **Archived:** can't be reopened, and is hidden from lists unless asked for (`ProcedureSearch.Filter`).
- **Completed:** drops off task lists. An admin can bring it back by unlocking a stage.

Each removes the procedure from the open lists, where it would otherwise be seen.

**Possible harm.** Remaining care not given, for one patient. Considerable.

**Existing controls:**
- Each change asks for confirmation; cancelling is shown as destructive and needs a reason, saying "A cancelled procedure can only be archived after this" (`StatusChangeDialog.tsx`).
- A procedure can only be completed from in review, once every stage is finished and locked (`ChangePatientProcedureStatusHandler`).
- Every status change is audited with from, to, reason and whether it was automatic (`ProcedureChange.SetStatus`).

**Initial risk:** Considerable, Low: **2**.

**Further controls:**
- **Let an admin reopen a cancelled or archived procedure,** with a reason, audited. Design; **proposed**; later.
- **Ask the "not assigned to you" question before status changes too.** Protective; **proposed**; later.

**At first live use:** Considerable, Low: **2**.

**Residual risk:** Considerable, Very low: **2**.

**Owner:** [CSO]. **Status:** Open.

---

## H-09: A member of staff doesn't learn of work assigned to them

**Description.** A member of staff is assigned a procedure, but doesn't learn of it in time.

**Causes:**
- **Live delivery is best effort.** A failed push is only logged (`SignalRNotificationPublisher`).
- **The browser stops reconnecting** after about 42 seconds offline (attempts at 0, 2, 10 and 30 seconds), until the page is reloaded (`MedApp.Web/src/app/LiveNotifications.tsx`, `withAutomaticReconnect()`; `docs/architecture.md`, risk R11).
- **No email:** notifications are in-app only. Someone who isn't signed in learns of it only at their next sign-in.
- **My Tasks hides it if it's already under way.** My Tasks opens on "Not started" (H-04), so a procedure handed over part-way through, for example from a colleague who's off sick, doesn't show there by default. The colleague who opens My Tasks sees "Nothing waiting to be started".
- **Nothing tells someone they've been unassigned,** and nothing tells anyone about a procedure assigned to someone who's away.

**Effect.** Work waits until the person next opens MedApp and finds it, or longer.

**Possible harm.** Delayed care for one patient. Considerable.

**Existing controls:**
- Every notification is stored before it's pushed, so it shows on the bell at the next page load (`NotificationSender`).
- My Tasks lists every open procedure assigned to you once its filter is set to "Started" or to all (`MyTasksPage.tsx`); by default it shows only those not yet started.
- With two copies of the API, notifications reach browsers on either, through the Valkey backplane (`SignalRBackplane`; 8 of 8 delivered in testing, `docs/architecture.md`, section 6.4).
- When the connection comes back, the list is refreshed (`connection.onreconnected`).

**Initial risk:** Considerable, Medium: **3**.

**Further controls:**
- **The count of started work on My Tasks,** or opening it on all open work, from H-04. Protective; **proposed**; before first live use.
- **Information:** tell users that notifications are a convenience, and that My Tasks, with its filter on all open work, is the list to work from. Information; **proposed**; before first live use (it costs nothing); transferred.
- **Keep reconnecting** with backoff for as long as the page is open. Protective; **planned** (step 2); later.
- **Email** for assignments, with no patient details in the message. Protective; **planned** (step 9); later.

**At first live use:** Considerable, Low: **2**.

**Residual risk:** Considerable, Very low: **2**.

**Owner:** [CSO]. **Status:** Open.

---

## H-10: MedApp is unavailable

**Description.** MedApp can't be reached, or doesn't work, while procedures are under way.

**Causes:**
- **It's never run in production:** no monitoring, alerting, service level or on-call (`docs/architecture.md`, risk R1, section 10.2).
- **Readiness checks** only that the process is up, not the database or Valkey (risk R3).
- **One maintainer** (risk O1).
- A failed deploy: every merge deploys (risk D1).
- The organisation's own network or devices.

**Effect.** Staff can't see who's assigned to what, which stage a procedure is at, its notes or its due date. Work is done without recording it, or not done.

**Possible harm.** Delayed or missed care for every patient with a procedure under way while it's down; records that don't match what was done afterwards. Severe injury from which recovery is expected, affecting multiple patients: Major.

**Existing controls:**
- Two copies of the API in Kubernetes, with a disruption budget and rolling updates that served every request in testing (`docs/architecture.md`, sections 7.4 and 10.1).
- A failed migration stops the rollout and leaves the old version running (ADR-10).
- Server rendering falls back to rendering in the browser if Node fails (`SsrPages`).
- Rollback is a revert on `main` (`AGENTS.md`).

**Initial risk:** Major, Medium: **3**.

**Further controls:**
- **A printable task list and procedure summary,** so staff can carry on from paper. Design; **planned** (step 6); before first live use.
- **A downtime procedure** for deploying organisations: what to print and when, how to work on paper, and how to enter the record afterwards. Information; **planned** (step 6, the DCB0160 pack); before first live use; transferred.
- **Monitoring and alerting,** a published service level, and readiness that checks the database. Protective; **planned** (step 5); before first live use.
- **A status page,** and telling customers about planned maintenance. Information; **planned** (step 5); later.
- **A tested rebuild in a second region,** and a year of availability history. Protective; **planned** (step 5); later.

**At first live use:** Major, Low: **3**. An outage affects every patient at once, and with no operating history the likelihood can't yet be shown to be very low. Needs the CSO's written justification for first live use.

**Residual risk:** Major, Very low: **2**.

**Owner:** [CSO]. **Status:** Open.

---

## H-11: Due dates don't mean what staff think

**Description.** The due date is set, shown or understood differently from what the person who set it meant.

**Causes:**
- **A due date is a whole day,** due by the end of that day, UK time (`DueDates.EndOfDayUtc`). Something needed by 09:00 isn't overdue until after midnight.
- **Editing allows a past date:** creating a procedure refuses a due date in the past (`CreatePatientProcedureValidator`), but editing doesn't check (`UpdatePatientProcedureValidator`), so a slip in the year makes a procedure overdue at once, or a long way in the future.
- **The time zone database:** `DueDates` looks up `Europe/London` when it's first used. If the container image had no time zone data, every procedure request would fail; nothing tests this in the image.
- **A browser outside the UK:** the date picker's "today" is the browser's date (`todayIso`, `MedApp.Web/src/utils/dates.ts`), while the API uses the UK date. The API refuses the difference, so this causes an error, not a wrong date.
- **Priority isn't part of "overdue":** an urgent procedure with a distant due date isn't flagged.

**Effect.** Work is treated as on time when it isn't, or the other way round.

**Possible harm.** Delayed care for one patient. Considerable.

**Existing controls:**
- One rule, in one class, stored as the end of the UK day in UTC, so the day survives the clocks changing (`DueDates`). Tested for summer, winter and both clock-change days (`DueDatesTests`).
- The API takes and returns a date, not a time (`PatientProcedureResponse.DueDate`), and the web app shows it without converting it (`formatShortDate` uses UTC on a date-only value).
- Lists sort by due date by default.

**Initial risk:** Considerable, Low: **2**.

**Further controls:**
- **Say what a due date means** on the form ("due by the end of this day, UK time") and in the instructions for use. Information; **proposed**; before first live use (it costs nothing).
- **Confirm a due date in the past,** or more than a year ahead, when editing. Protective; **proposed**; later.
- **A start-up check** that `Europe/London` resolves in the image, and a test in the `image` CI job. Protective; **proposed**; later.
- **Due dates for each stage, and times of day** if customers need them. Design; **planned** in part (step 9); later.

**At first live use:** Considerable, Low: **2**.

**Residual risk:** Considerable, Very low: **2**.

**Owner:** [CSO]. **Status:** Open.

---

## H-12: Times are shown an hour out

**The cause is confirmed, and the defect is fixed by PR #62.** The initial rating below is for `88dc1fa`, before that fix; the fix is recorded as a further control, and the residual rating takes it into account. The CSO verifies it before first live use (see Residual risk).

**Description.** The time a stage was started or finished, a procedure was created or changed, a person was assigned, or an audit entry was made is shown wrongly: an hour early during British Summer Time on a UK browser, and by the browser's offset from UTC elsewhere.

**Causes:**
- Times are stored in UTC. When EF Core reads them from SQL Server, they come back without a time zone (`DateTimeKind.Unspecified`), and nothing in the code at `88dc1fa` marks them as UTC, so the API's JSON carries them without a `Z`.
- The web app's `formatDateTime` (`MedApp.Web/src/utils/dates.ts`) passes the string to `new Date()`, which reads a date-time without a zone as the browser's local time, then shows it in UK time. The same file has a `utc()` helper for exactly this case, with the comment "one read from the database may not say so", but only `timeAgo` uses it.
- It's used for stage started and finished times (`StageList.tsx`), assignment times (`AssigneeList.tsx`), a procedure's created and last updated times (`ProcedurePage.tsx`) and the audit log (`AuditLogPage.tsx`).
- The tests only pass times that end in `Z` (`dates.test.ts`), so they didn't catch it.
- **A trap when checking it:** the server renders the page in Node in UTC, so the server's HTML shows the right time; the browser then re-renders it an hour early. Check the page after it has loaded in a browser during British Summer Time, or the API's JSON for a missing `Z`.

**Effect.** The timeline of every procedure is wrong by an hour for half the year: a stage looks as if it was finished an hour before it was, and the audit log disagrees with other systems' times during an investigation.

**Possible harm.** A clinical decision made on a wrong time, for example about how long ago a step was done; an incident investigation that reaches the wrong conclusion. It affects every patient's record at once, so multiple patients: Considerable.

**Existing controls:**
- Due dates aren't affected: they're sent as dates and shown without conversion.
- Times are stored correctly, so a fix corrects every past entry too.

**Initial risk:** Considerable, High: **3**.

**Further controls:**
- **Fix it:** mark every `DateTime` read from the database as UTC, read any time without an offset as UTC in the web app, and test with times that have no `Z`. Design; **built** in PR #62: a model-wide `UtcDateTimeConverter` marks every time read from the database as UTC (and turns a local time into UTC when writing), so the API's JSON ends in `Z`; `parseUtc` reads a time without a zone as UTC in the web app; .NET tests check every `DateTime` in the model has the converter and that the API's controller and hub JSON end in `Z`; the web tests run in `America/New_York`, so a time misread as local fails them in summer and winter; **before first live use**, to be verified by the CSO.

**At first live use:** Considerable, Very low: **2**.

**Residual risk:** Considerable, Very low: **2**. With the fix built, what's left is a future change that brings the defect back: a new way of reading or sending times that bypasses EF or the web app's date helpers. The tests above and the rules in `AGENTS.md` and `MedApp.Web/AGENTS.md` control that. Not yet done: seeing the fix end to end, against SQL Server and in a browser during British Summer Time. A related cause not fixed by #62: "today" in the web app is the machine's day, and server rendering runs in UTC, so between 23:00 and midnight UTC in summer the server's day and the browser's can differ (`todayIso`; a to-do).

**Owner:** [CSO], with the maintainer. **Status:** Open: the defect is fixed by PR #62; the fix awaits the CSO's verification.

---

## H-13: A template or stage definition has wrong content

**Description.** An admin creates or edits a template or stage definition with a mistake: a missing stage, stages in the wrong order, or wrong instructions. Procedures created from it carry the mistake.

**Causes:**
- Any admin can create and edit templates and stage definitions, and the change applies at once to new procedures (`UpdatePatientProcedureTemplateHandler`, `UpdatePatientProcedureStageDefinitionHandler`).
- **No second check:** there's no approval or clinical review step in MedApp.
- **No history:** changes to templates and stage definitions aren't audited and have no versions. Nobody can see who changed what, or what a template said when a procedure was created from it, beyond the copy on the procedure itself.
- Admin is also the role that manages users, so the people who can edit clinical content aren't necessarily clinicians (`docs/architecture.md`, step 4, splits these roles).
- **SNOMED CT codes** (branch `feature/nhs-integrations`): a stage definition can carry a SNOMED CT code with a display term the admin types, copied onto procedures' stages. Only the code's format is checked (`SnomedCt`): nothing checks that the concept exists, is active, or means what the term says, so a code and its term can disagree. MedApp does nothing with the code, so today a wrong one only misleads someone reading it; once codes leave MedApp (step 9's FHIR resources for procedures), another system could act on one.

**Effect.** Every procedure created afterwards misses a step, does them in the wrong order, or shows wrong instructions, and staff follow it because MedApp enforces the order.

**Possible harm.** A required step of care missed for every patient whose procedure comes from that template. Severe injury or permanent incapacity, multiple patients: Catastrophic in the worst credible case.

**Existing controls:**
- Only admins can change templates and definitions; staff can only read them (`docs/architecture.md`, section 5.7).
- Templates and definitions in use are archived, not deleted (`SetPatientProcedureTemplateArchivedHandler`; deletion of one in use is a 409).
- Each procedure keeps its own copy of the stages, with the template's name and ID, so what a procedure was given is preserved (`CreatePatientProcedureHandler`).
- The Templates page shows how many procedures use each template (`docs/procedures-ui-plan.md`, phase 6).
- **Transferred:** the organisation is responsible for the clinical content of its templates, approved under its own governance.

**Initial risk:** Catastrophic, Low: **4** (mandatory elimination or control).

**Further controls:**
- **An approval step:** a template or stage definition change takes effect only when a second person with a clinical approver role approves it. Protective; **proposed**; **brought forward to before first live use**, because it's the control that lowers the likelihood of a wrong template being used, and a 4 can't be released.
- **Audit every change** to templates and stage definitions (who, when, old and new). Protective; **proposed**; before first live use.
- **A local template governance process:** clinical sign-off before a template is used. Information; transferred; before first live use.
- **Versions:** each template change makes a new version; procedures record the version they came from. Design; **proposed**; later.
- **A separate role** for template administration. Protective; **planned** (step 4); later.
- **Check SNOMED CT codes with a terminology server:** that the concept exists and is active, with its preferred term shown beside the admin's. Protective; **planned** (`docs/todo.md`); before codes leave MedApp in the FHIR API.

**At first live use:** Catastrophic, Very low: **3**. Needs the CSO's written justification for first live use.

**Residual risk:** Catastrophic, Very low: **3**. The consequence can't be lowered by the software, only the likelihood; the CSO decides whether 3 is as low as reasonably practicable.

**Owner:** [CSO]. **Status:** Open.

---

## H-14: A correction to a template doesn't reach procedures already under way

**Description.** An admin corrects a template or stage definition (for example, an instruction that was wrong or unsafe), but procedures created before the correction keep the old stages and the old text.

**Causes:**
- **Copy on create, by design:** creating a procedure copies each stage's name, description and instructions onto it, so "later edits to the template or definitions don't change this record" (`CreatePatientProcedureHandler`; `docs/procedures-ui-plan.md`, section 3, D). This protects finished work from being rewritten, which is the right default.
- There's no way to apply a correction to unfinished stages, and nothing shows that a procedure's stages are out of date.

**Effect.** Staff working those procedures follow the old, wrong instructions, or miss a stage that was added.

**Possible harm.** A wrong or missed step for every patient whose procedure was already under way. Severe injury from which recovery is expected, multiple patients: Major.

**Existing controls:**
- The procedure shows the template's name as it was (`TemplateName`), and the Templates page shows how many procedures use a template, so admins can find the affected procedures by searching on the template (`ProcedureSearch.Filter`, `TemplateId`).
- A procedure can be cancelled with a reason and recreated from the corrected template.

**Initial risk:** Major, Low: **3**.

**Further controls:**
- **List the open procedures when a template or definition in use is edited,** with a link to them, saying that they keep the old stages and need reviewing. Protective; **proposed**; before first live use.
- **A local process:** when a template is corrected for safety, search for its open procedures and review each. Information; transferred; before first live use.
- **Flag procedures whose template has changed** since they were created, on the procedure page and in lists, with what changed. Protective; **proposed**; later (depends on versions, H-13).
- **Let an admin apply a correction to the unfinished stages** of chosen procedures, audited, never to finished ones. Design; **proposed**; later.

**At first live use:** Major, Very low: **2**.

**Residual risk:** Major, Very low: **2**.

**Owner:** [CSO]. **Status:** Open.

---

## H-15: Two people edit the same procedure at once

**Description.** Two people change the same procedure, or the same stage, at about the same time, and one person's change silently replaces the other's.

**Causes:**
- **No optimistic concurrency:** no entity has a concurrency token, so the last save wins (`docs/architecture.md`, section 8.9 and risk R8).
- **Stage notes:** two people saving notes on the current stage each replace the whole text. Nobody is warned.
- **Notes saved at the same moment a stage is finished:** if both requests are handled at once, both pass their checks, and the notes change after the stage is locked. A narrow race, since each request reloads the procedure and checks it (`UpdateProcedureStageNotesHandler`).
- **Procedure details** (title, notes, due date, priority) behave the same way (`UpdatePatientProcedureHandler`).
- **Unsaved typing can disappear:** the notes box is reset whenever the saved notes change (`ProcedurePage.tsx`, `key={...stage.notes}`), so if the page reloads the procedure after someone else saved, text typed but not saved is replaced by theirs.

**Effect.** Information recorded by one person is no longer on the record, though it is in the audit log; or text typed but not saved is lost.

**Possible harm.** Someone acts on incomplete notes, for one patient. Considerable.

**Existing controls:**
- Every notes save is audited with the old and new text, so nothing saved is lost for good (`ProcedureChange.UpdateStageNotes`).
- Stage order, locks and status rules are checked on the server against the saved procedure, so a page that's out of date can't start or finish the wrong stage, or save notes on a locked one (409 from `WhyStageCantBeEdited`).
- "Unsaved changes" shows beside the notes box (`StageWork.tsx`).
- The "not assigned to you" question makes it a little less likely that a second person works a procedure that someone else is working. It's asked once per visit, and not of admins (`ProcedurePage.tsx`, `checked`).

**Initial risk:** Considerable, Medium: **3**.

**Further controls:**
- **Optimistic concurrency:** a `rowversion` on procedures and stages, and a 409 when someone else has changed it since you loaded it. Protective; **planned** (step 6); **before first live use**.
- **Keep the draft on a conflict:** show the other person's text beside yours, never replace unsaved text without asking. Protective; **proposed**; before first live use (with the above).
- **Show who else has the procedure open.** Protective; **proposed**; later.

**At first live use:** Considerable, Very low: **2**.

**Residual risk:** Considerable, Very low: **2**.

**Owner:** [CSO]. **Status:** Open.

---

## H-16: The screen shows out-of-date information

**Description.** A page shows a procedure as it was when the page loaded, not as it is now.

**Causes:**
- Pages don't refresh when the window regains focus (`refetchOnWindowFocus: false`, `MedApp.Web/src/app/queryClient.ts`), and nothing refreshes a procedure page when someone else changes it. Only the user's own changes and an assignment notification refresh it.
- The overdue badge is worked out when the data loads, so a page left open over midnight doesn't show work that has since become overdue.
- If the server can't render a page, the browser renders it instead from fresh data, so this isn't stale, but it's slower (`SsrPages`).

**Effect.** Someone reads a status, assignee, current stage or notes that have changed, for example a procedure that someone else has cancelled or blocked.

**Possible harm.** A decision made on old information, for one patient. Significant: any change they then try is checked against the saved procedure (H-15).

**Existing controls:**
- The server checks every change against the saved procedure's rules (H-15).
- The procedure page shows "Last updated" with a time (`ProcedurePage.tsx`; but see H-12).
- Pages are sent with `Cache-Control: no-store`, so the browser never shows a cached page.

**Initial risk:** Significant, Medium: **2**.

**Further controls:**
- **Refresh procedure data when the window regains focus,** at least on procedure pages and task lists. Protective; **proposed**; later.
- **Live updates** to procedure pages and lists when someone else changes them. Protective; **planned** (`docs/procedures-ui-plan.md`, phase 7, "later"); later.

**At first live use:** Significant, Medium: **2**.

**Residual risk:** Significant, Low: **2**.

**Owner:** [CSO]. **Status:** Open.

---

## H-17: Stage instructions aren't read

**Description.** Staff work a stage without reading its instructions.

**Causes:**
- Each stage's instructions start folded, on the current stage as well as in the list, and open only when clicked (`ProcedurePage.tsx`, `instructionsOpen`).
- Familiarity: staff who work the same template often stop opening them.

**Effect.** A step described only in the instructions is missed.

**Possible harm.** A missed or wrong step, for one patient. Considerable.

**Existing controls:**
- The instructions are on the page, beside the notes box, a click away.
- The stage's name and description are always shown.

**Initial risk:** Considerable, Low: **2**.

**Further controls:**
- **Open the current stage's instructions** the first time a person works that stage. Protective; **proposed**; later.
- **"Must read" instructions:** a per-definition setting that asks the person to confirm they've read them before they can finish the stage. Protective; **proposed**; later.
- **Write anything essential as its own stage,** not only in instructions. Information; transferred (template governance).

**At first live use:** Considerable, Low: **2**.

**Residual risk:** Considerable, Very low: **2**.

**Owner:** [CSO]. **Status:** Open.

---

## H-18: A change is recorded under the wrong person's name

**Description.** The audit log, or a stage's "finished by", names someone other than the person who did the work.

**Causes:**
- **No idle timeout:** a session lasts 8 hours from sign-in, so someone else can use an unattended, signed-in screen (`SessionService`; `docs/architecture.md`, risk S5).
- **A session can be extended (since #63):** five minutes before the end, a warning offers "Stay signed in", which extends the session to 8 hours from then, up to 24 hours from sign-in (`UserSession.Extend`). Whoever is at the screen can press it, so an unattended session can now be kept for up to 24 hours instead of 8. The warning is for the fixed limit (WCAG 2.2.1), not an idle timeout. **A change to this hazard's cause, for the clinical safety officer to assess.**
- **Passwords only,** with no multi-factor authentication (risk S8), and shared or passed-on passwords.
- **Deleting a user** removes the account. Their name then shows as "Deleted user" on stages and assignments (`ProcedureMapper.DeletedUser`), though the audit log keeps a snapshot of the username. (What happens to their open work is H-21.)

**Effect.** It's unclear who did a step, which matters when someone needs to ask about it or when something went wrong.

**Possible harm.** A question about a patient's care can't be answered quickly; accountability is lost. One patient; Significant.

**Existing controls:**
- Each change is recorded with the signed-in user's ID and a snapshot of their username (`ProcedureActor`, `ProcedureAudit`).
- Sessions are revoked on sign-out, and a session whose user no longer exists is refused, so deleting a user ends their sessions (`SessionService`, `MedApp.Infrastructure/Common/Authentication/SessionService.cs`).
- Five wrong passwords lock an account for five minutes.

**Initial risk:** Significant, Medium: **2**.

**Further controls:**
- **An idle timeout,** with a warning. Protective; **planned** (step 2); later. (The warning before the fixed limit, added in #63, is not this control.)
- **End a user's other sessions** on a password change, a reset and a role change. Protective; **in progress** (the security baseline branch); later.
- **Multi-factor authentication** and single sign-on. Protective; **planned** (step 4); later.
- **Deactivate accounts instead of deleting them.** Design; **planned** (step 4); later.
- **Local policy:** no shared accounts; lock screens when away. Information; transferred.

**At first live use:** Significant, Medium: **2**.

**Residual risk:** Significant, Low: **2**.

**Owner:** [CSO]. **Status:** Open.

---

## H-19: Part of the clinical record is lost by deletion

**Description.** Information about a patient's care is deleted.

**Causes:**
- **Procedures:** an admin can delete a cancelled or archived procedure, with its stages and assignments (`DeletePatientProcedureHandler`). Its stage notes then survive only as text inside audit entries, which only admins can read.
- **Patients:** any staff member can delete a patient without procedures (H-03); since step 7, the deletion is recorded in the activity log.
- **Anonymisation (step 7):** an admin can anonymise a patient's record, which replaces their name, email and every note, procedure title and reason (in the record, its audit trail and notifications) with placeholders, and keeps only the year of birth. It's meant for records past their retention period. Done on the wrong patient, or too early, the clinical content is gone from the live system (backups keep it until they expire).

**Effect.** The record of what was done, or of the patient, is no longer where staff look for it.

**Possible harm.** A later decision made without that history, for one patient. Considerable.

**Existing controls:**
- Only cancelled or archived procedures can be deleted, only by admins, after a confirmation that names the procedure and patient (`ProcedureWorkflow.CanBeDeleted`; `ProcedurePage.tsx`).
- The audit log keeps the deletion and every earlier change, including the old and new text of every notes save, with no foreign keys, so it outlives the procedure (`PatientProcedureAuditEntry`).
- A patient with procedures can't be deleted.
- Anonymising is admin-only; it needs the patient's full name typed, and a reason, which the activity log keeps with the admin's name; it's refused while any of the patient's procedures is under way or the record is restricted; the dialog shows the patient's name and date of birth, and says it can't be undone (`AnonymisePatientHandler`, `AnonymiseDialog`; step 7). An admin can export the whole record first (step 7).
- The retention review lists only records past their minimum period, and nothing is deleted automatically (step 7).

**Initial risk:** Considerable, Low: **2**.

**Further controls:**
- **Retention rules instead of deletion,** set by each organisation's retention schedule. Design; **partly built** (step 7: a review list from configurable periods, keep or anonymise, no automatic deletion); per-template classes and a destruction log **planned**; later.
- **A second person to approve an anonymisation** (and a destruction), as the retention schedule's "approved by a named person" asks. Protective; **proposed**; later.
- **Admin-only patient deletion.** Protective; **planned** (step 2); later.

**At first live use:** Considerable, Low: **2**.

**Residual risk:** Considerable, Very low: **2**.

**Owner:** [CSO]. **Status:** Open.

---

## H-20: Data is lost, or restored to an earlier point

**Description.** Data is lost, for example after a database failure or a bad migration, or the database is restored from a backup, and recent work disappears.

**Causes:**
- **Backups:** 7 days of point-in-time restore on Azure, never tested; none on the local cluster (`docs/architecture.md`, section 8.2 and risk R5).
- **Migrations** are reviewed by hand; a rename scaffolded as drop and create would lose data if missed (`AGENTS.md`, Migrations).

**Effect.** Stages finished, notes written and procedures created since the restore point are gone, for every patient, and staff don't know which.

**Possible harm.** Steps done twice, or believed done when the record of them is gone. Severe injury from which recovery is expected, multiple patients: Major.

**Existing controls:**
- Each change and its audit entries are saved in one transaction (`PatientProcedureRepository.SaveAsync`).
- Migrations are reviewed and tested up and down on a throwaway database, never applied at start-up, and a failed migration stops the rollout (`AGENTS.md`; ADR-10).

**Initial risk:** Major, Low: **3**.

**Further controls:**
- **A tested restore** before first live use, then 35 days of point-in-time restore, long-term backups and restore drills. Protective; **planned** (step 5); the tested restore before first live use, the rest later.
- **A runbook** that, after a restore, tells customers the restore point, so they can re-enter work from their downtime records. Information; **planned** (step 5); before first live use.

**At first live use:** Major, Very low: **2**.

**Residual risk:** Major, Very low: **2**.

**Owner:** [CSO]. **Status:** Open.

---

## H-21: Work assigned to a deleted account drops off every task list

**Description.** An admin deletes the account of someone who has left, and the procedures assigned to them stay assigned to the deleted account, on nobody's task list.

**Causes:**
- **Deleting an account touches nothing else.** `DeleteUserHandler` calls `IdentityUserService.DeleteUserAsync`, which deletes the Identity user only.
- **Assignments have no link to the user table:** they're keyed on the procedure and the user's ID, with a foreign key to the procedure only (`PatientProcedureAssignmentConfiguration`), so the rows stay.
- **The status still says Assigned,** because it counts assignments, not live accounts (`ProcedureWorkflow.StatusFromAssignments`). So an "unassigned" view (H-04) wouldn't show it either.
- **My Tasks matches the signed-in user's ID** (`ProcedureSearch.Filter`, `AssignedToMe`), so no one else's list shows it.
- **Deleting is the only way to remove an account,** and the guidance to customers is to remove leavers promptly.

**Effect.** The leaver's open procedures look looked after, showing "Deleted user" as the assignee, but nobody is working them, and nothing tells anyone.

**Possible harm.** Delayed care for every patient on the leaver's list. Severe injury from which recovery is expected, multiple patients: Major.

**Existing controls:**
- The procedure page and lists show the assignee as "Deleted user" (`ProcedureMapper.DeletedUser`), which someone looking at the procedure would notice.
- Any staff member can still work the procedure, and an admin can unassign the deleted account and assign someone else.
- Overdue badges still show on the full Procedures list.

**Initial risk:** Major, Medium: **3**.

**Further controls:**
- **Refuse to delete an account with open assignments, or require reassignment first:** list the open procedures assigned to the account, and delete it only once they're reassigned. Protective; **proposed**; **before first live use**.
- **Deactivate accounts instead of deleting them,** keeping their assignments visible until reassigned. Design; **planned** (step 4); later.
- **A leaver process:** reassign a leaver's open procedures, then remove the account. Information; transferred; before first live use.

**At first live use:** Major, Very low: **2**.

**Residual risk:** Major, Very low: **2**.

**Owner:** [CSO]. **Status:** Open.

---

## Hazards considered and not logged

- **Stages worked out of order.** Designed out while no stage is unlocked: only the current stage can be started, edited or finished, checked by the API (`ProcedureWorkflow.WhyStageCantBeEdited`), with tests. Once an admin unlocks a finished stage, it and the current stage can both be edited and finished; that's logged under H-06.
- **Completing a procedure with a stage unfinished or unlocked.** Designed out: `ChangePatientProcedureStatusHandler` refuses it (`AllStagesFinished`).
- **One user's data in another's server-rendered page.** A security and privacy risk rather than a clinical one, controlled by a new query cache per request (`createSsrRouter`; `docs/architecture.md`, section 5.8). Left to the security work in step 2.
- **Clinical calculations or recommendations going wrong.** MedApp has none (`intended-purpose-and-device-decision.md`).
- **The FHIR API sending wrong data** (branch `feature/nhs-integrations`). It's read-only, and returns the patient's details as stored, with no clinical content and no logic, to staff who could see the same on the patient's page; nothing in MedApp changes because of it. Its one clinical risk, another system matching the wrong person by a wrong NHS number, is under H-01. Each receiving system assesses its own use under DCB0129 and DCB0160.
- **The NHS number check refusing a valid number.** Staff can't save it, so the record goes without it until it's fixed: no worse than before the number existed. The check is unit tested with valid and invalid numbers.

## Change log

| Version | Date | Change | By |
|---|---|---|---|
| 0.1 | 16 November 2026 | First draft: 20 hazards from reading the code at `88dc1fa` | AI coding agent, for review |
| 0.2 | 16 November 2026 | After the review of #60: DCB0129's own consequence table, which re-rated H-09, H-10, H-12, H-13, H-14, H-20; an "at first live use" rating; My Tasks' default filter added to H-04, H-05 and H-09; new hazard H-21; H-13's approval step brought forward; H-12's fix in PR #62; smaller corrections | AI coding agent, for review |
| 0.3 | 16 November 2026 | H-12's defect fixed by PR #62: the fix recorded as built, the residual risk's reasons updated, and what's left to verify | AI coding agent, for review |
| 0.4 | 16 November 2026 | Branch `feature/nhs-integrations`: the patient banner recorded as built for H-01 and H-02, awaiting the CSO's verification; the NHS number in H-01's causes and controls, with new causes for a wrong number and the demographics lookup; SNOMED CT codes in H-13's causes, with a terminology server check; the FHIR API and the NHS number check considered and not logged. No rating changed | AI coding agent, for review |
