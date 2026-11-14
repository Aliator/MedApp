# MedApp: procedures in the UI

Status: **all decisions settled; phases 0, 1, 2 and 5 merged (PRs #30, #31), phase 3 done on `feature/procedure-work` (section 7)** · 2026-11-14

Staff use standard **procedure templates** to start a **procedure** for a patient. A template is an ordered list of **stages**, and admins create and maintain templates and stages. Any staff member can work through a procedure's stages, in order. As they go they record notes, and they **finish** each stage, which locks it and moves the procedure on to the next stage. Admins **assign** staff to procedures; that puts the procedure on those staff members' task lists, on their Home page, but doesn't limit who may work on it. Admins can unlock a finished stage so it can be corrected. Every change to a procedure goes into an **audit log**, which admins can read.

This plan brings that concept to the UI (`MedApp.Web`), and changes the API and database to match it. The current backend only partly follows the concept; section 3 lists where it doesn't, and how to fix each gap.

## 1. Decisions (settled 2026-11-14)

1. **Roles.** **Staff** (role `User`) can do almost everything: patients, and creating, working on and following procedures.
   - **Admins** (role `Admin`) can additionally:
     - manage users, roles and passwords;
     - manage stage definitions and templates;
     - assign staff to procedures;
     - unlock finished stages.
   - There's one admin account for now (`admin`), but more may be added. Nothing may assume a single admin; access is always checked by role.
2. **Status workflow.** Sensible, API-enforced transitions with automatic moves. `NotStarted` is dropped: existing rows become `Assigned` in the migration, and the other values keep their numbers. See section 4.
3. **Stages are done strictly in order.** A finished stage is **locked**: staff can't change it. An admin can **unlock** it so a staff member can edit it again. Staff can't change anything structural (users, permissions, templates, stage definitions).
4. **Home becomes the staff member's task list.** It shows the procedures assigned to them that aren't finished, in two tables side by side: **Not started** and **Started** (section 6). Real-time updates come later; until then the lists refresh when the page loads, regains focus, or after a change.
5. **Access paths.**
   - Staff can see the patients list; only admins see the users list.
   - A patient's procedures are reached from a button in the patients table and on View Patient.
   - There's also a full **Procedures** table listing every procedure, searchable and sortable.
6. **Lists:** server-side paged search. By default it sorts by due date, soonest first, and hides `Completed`, `Cancelled` and `Archived` until asked.
7. **Due date is a date only:** due by the end of that day, UK time, and stored as that instant in UTC.
8. **Staff create procedures only from an existing template.** Only admins create and edit templates (and the stage definitions they're built from). Templates are the way new kinds of procedure are made.
9. **Finishing a stage is audited:** who finished it and when. It's then locked until an admin unlocks it.
10. **Assignment:** admins assign staff to a procedure after it's created. Only admins can assign or unassign, and "assigned by" is kept.
    - Assignment **only decides whose task list a procedure appears on.** It doesn't restrict who can work on it: any staff member may work on any procedure, for example when its assignee is off.
    - Changing a procedure that isn't assigned to you first asks for confirmation (decision 18).
11. **Stage definitions in use can't be deleted.** They're archived instead.
12. **Deleting a patient who has procedures is blocked** with a 409 and a clear message. Today it's a 500.
13. **Only `Cancelled` or `Archived` procedures can be deleted.**
14. **Colours** as proposed in section 6; they can be changed later.
15. **Overdue:** the due date has passed and the status isn't `Completed`, `Cancelled`, `Archived` or `Blocked`.
16. **A full audit log for procedures.** **Only admins** can read it, on a new admin page with the whole log in a paginated, searchable table (section 5).
17. **Admin pages** for stage definitions and for templates. Admins can create, edit and archive both. Templates in use are archived (hidden from New procedure, but kept); only templates that were never used can be deleted.
18. **The "not assigned to you" check.** When a staff member changes a procedure that isn't assigned to them, the app first asks: *"This procedure is not assigned to you, are you sure you want to make these changes?"* (a confirmation dialog). This is a UI safeguard, not a permission: the API allows it, and the audit log records who made the change.
19. **Unlocking a finished stage reopens only that stage** for editing. Later stages keep their state. If the procedure was `InReview` or `Completed`, it goes back to `InProgress`. Both the unlock and the stage being finished again are audited.
20. **Only admins can delete a `Cancelled` or `Archived` procedure.**
21. **One notes box per stage** (Q-B, b). It's editable until the stage is finished, and again only while an admin has the stage unlocked. Every save is audited with the old and new text.
22. **The "not assigned to you" check asks once per visit** to the procedure (Q-G, a).
23. **The Home task list is built now** (Q-H), replacing the old welcome card. It refreshes on load, on focus and after changes; real-time updates come later.

## 2. How the concept maps onto the model

| Concept | Today | Target |
|---|---|---|
| Stage (reusable step, e.g. "Consent") | `PatientProcedureStageDefinition` (name, description, instructions) | Same, plus `IsArchived` |
| Procedure template (admin-made, ordered stages) | `PatientProcedureStageTemplate` + `…TemplateMap` (ordered definitions) | Same. Admin-only writes. |
| Procedure (one patient going through a template) | `PatientProcedure` (title, notes, due date, priority, status) + stages + assignments | Plus `TemplateId` and `TemplateName` (what it was created from). Created **from a template only**. |
| Procedure stage (that patient's copy of a step) | `PatientProcedureStage` (definition, order, `IsCompleted`, `CompletedAt/ByUserId`) | Plus a **snapshot** of name, description and instructions (section 3, D), started and finished who/when, locked state, and the stage's notes |
| Stage notes ("recorded as they happen") | Nothing | A `Notes` box on each procedure stage (decision 21); every save is audited |
| Audit log | Nothing | `PatientProcedureAuditEntry`: procedure, optional stage, action, who, when, details |
| Assignment | `PatientProcedureAssignment` (user, assigned by, when) | Same. Admin-only, with unassign added. It drives task lists, not permissions. |

## 3. Where the current implementation doesn't follow the concept, and how to fix it

- **A. Permissions are inverted.**
  - Nearly every patient and procedure endpoint is `[Authorize(Roles = "Admin")]`. Staff can't use the Patients pages at all today: the React routes let them in, but the API answers 403.
  - **Fix:** two policies.
    - `Staff`, for role `User` or `Admin`: patients and procedures.
    - `Admin`: users, roles and passwords; stage definitions and templates; assigning and unassigning; unlocking stages.
  - Apply them per endpoint, with tests per role. The React side then shows the admin-only actions to admins only.
- **B. Anyone can assign anyone.** The assign endpoint is plain `[Authorize]`. **Fix:** `Admin` only, and add unassign.
- **C. Procedures are created from a hand-picked list of stages, and their stages can be rewritten later.**
  - `CreatePatientProcedureRequest` takes `StageDefinitionIdsInOrder`, and `UpdatePatientProcedureRequest` can replace the stage list.
  - **Fix:**
    - Create takes a `TemplateId` and copies the template's stages, recording `TemplateId` and `TemplateName` on the procedure.
    - The title defaults to the template's name.
    - Update no longer touches stages; a procedure's stages come from its template, and admins change templates.
- **D. Stages point at live definitions, so history can change after the fact.**
  - A procedure stage shows its definition's current name and instructions. If an admin later edits a definition, finished procedures would show text that wasn't there when the work was done.
  - **Fix:** copy the name, description and instructions onto the procedure's stage when the procedure is created (a snapshot). Templates already work this way for stage order; this extends it to the text.
- **E. Stages have nowhere to record work.**
  - There are no notes, no "started", and no lock.
  - There's also no endpoint to finish a stage at all: `IsCompleted` and `CompletedByUserId` are never set.
  - **Fix:**
    - New fields: `StartedAtUtc`/`StartedByUserId`, `CompletedAtUtc`/`CompletedByUserId`, and `IsLocked`.
    - A `Notes` column on the stage (decision 21).
    - Endpoints to start a stage, save its notes, finish it and unlock it, with the order and lock rules enforced in the API.
- **F. Status is free text,** and includes the redundant `NotStarted`. **Fix:** the transition table in section 4, enforced by the API, and a migration for `NotStarted`.
- **G. Nothing is audited.** **Fix:** an audit table, written in the same transaction as each change (section 5).
- **H. The two delete bugs** (both reproduced against the dev database):
  - **Deleting a patient who has procedures:** 500. **Fix:** a 409 "This patient has N procedures", and the count shown in the delete confirmation.
  - **Deleting a stage definition in use:** 500. **Fix:** a 409 suggesting archiving instead. The "Definition missing" seed placeholder is kept, archived, for any old rows that refer to it.
- **I. Responses carry IDs, not names.** **Fix:** procedure responses include patient name, usernames (assignees, assigned by, started and finished by) and stage progress, with a lighter summary DTO for table rows.
- **J. No search.** `GET patient-procedures` returns everything. **Fix:** paged search, as for patients and users.
- **K. Leftovers:**
  - An empty duplicate `PatientProcedureStageTemplateResponse` in the `Definitions` namespace.
  - The Procedures area has no unit tests.
  - **Fix:** remove the duplicate, and test every procedure handler and validator.

## 4. Status workflow

| From | To | How |
|---|---|---|
| `Unassigned` | `Assigned` | Automatic when the first person is assigned |
| `Assigned` | `Unassigned` | Automatic when the last person is unassigned |
| `Unassigned` / `Assigned` | `InProgress` | Automatic when the first stage is started (or its notes are saved); any staff member can start one |
| `InProgress` | `InReview` | Automatic when the last stage is finished |
| `InReview` | `Completed` | By hand, once every stage is finished and locked |
| `InReview` / `Completed` | `InProgress` | Automatic when an admin unlocks a stage for correction (decision 19) |
| any open status | `Blocked` | By hand, with a reason (recorded in the audit log) |
| `Blocked` | the status it was blocked from | By hand |
| any open status | `Cancelled` | By hand, with a reason |
| `Completed` / `Cancelled` | `Archived` | By hand |

"Open" means `Unassigned`, `Assigned`, `InProgress` or `InReview`. While a procedure is `Blocked`, `Cancelled`, `Completed` or `Archived`, its stages can't be worked on. Any staff member can make the manual moves. Assigning someone while a procedure is `Blocked` updates the status it will return to. The rules live in `ProcedureWorkflow` and `ProcedureChange` (`MedApp.Application/Procedures/Common`), and every response lists the moves allowed from where the procedure is (`allowedStatusChanges`).

## 5. Audit log

Every change to a procedure writes a `PatientProcedureAuditEntry` in the same transaction:
- **Columns:** `ProcedureId`; optional `StageId`; `Action`; `UserId` plus a username snapshot; `AtUtc`; and `Details` (from/to values or a reason, as JSON).
- **Actions:** created (from which template); details edited (which fields, from/to); assigned or unassigned (who); status changed (from/to, reason, and whether it was automatic); stage started; stage notes updated (old and new text); stage finished (again, after an unlock); stage unlocked; procedure deleted.

Deleted procedures keep their audit entries, so the table has no cascade from procedures.

**Reading it is admin-only** (decision 16):
- A new **Audit log** page under Administration has the whole log in a paginated table. It's searchable by procedure, patient, user, action and date range, and sortable by time (newest first by default).
- `GET api/procedures/audit/search` serves it.
- Admins also get a **View audit log** button on a procedure's page, which opens the audit page filtered to that procedure.
- Staff still see who finished each stage and when, on the stage itself; that's procedure data, not the audit log.

## 6. Screens and routes

| Route | Screen | Who |
|---|---|---|
| `/procedures` | **All procedures:** paged table, searchable (title, patient, template, assignee, status, priority, due range, overdue only) and sortable (due date by default, then priority, status, title, last updated) | Staff |
| `/patients/{id}/procedures` | **A patient's procedures**, reached from a new button on each patients-table row and on View Patient, with **New procedure** | Staff |
| `/procedures/new?patientId=` | **New procedure:** pick a template (its stages shown read-only), title (defaults to the template name), notes, due date, priority | Staff |
| `/` (Home) | **My tasks:** procedures assigned to me that aren't finished, in two lists that are always the same size and fill the page. **Not started** has no stage started yet (`Unassigned` can't apply, so in practice `Assigned`). **Started** is `InProgress`, `InReview` and `Blocked`. Each works like the patients list: its own search, sortable columns and pages, kept in the URL under a prefix (`notStartedPage`, `startedSort`, ...). Both default to due date, soonest first, with overdue highlighted. `Completed`, `Cancelled` and `Archived` don't appear. On a narrow window only one list shows, with a full-height switch to the other along the edge of its table card. | Staff |
| `/procedures/{id}` | **Procedure:** header (title, patient link, template, status and priority badges, due date or overdue); status actions; assignees (assign/unassign for admins); **stages in order**, each showing state, who/when, notes and **Finish stage** on the current one (**Unlock** for admins on finished ones); **View audit log** for admins. If it isn't assigned to you, your first change asks for confirmation (decision 18). | Staff |
| `/procedures/{id}/edit` | **Edit details:** title, notes, due date, priority (not stages) | Staff |
| `/admin/stages` | **Stage definitions:** create, edit and archive (and restore) | Admin |
| `/admin/templates` | **Templates:** create and edit (name + reorderable stages); archive and restore; delete only if never used | Admin |
| `/admin/audit` | **Audit log:** the whole log, paginated, searchable and sortable (section 5) | Admin |

**Side nav:** **Procedures** under Home and Patients; **Stage definitions**, **Templates** and **Audit log** under Administration, with Users.

**Colours** (Astryx `Token`; `Badge` is for counts only):
- **Status:** `Unassigned` neutral; `Assigned` info; `InProgress` blue; `InReview` purple; `Blocked` warning; `Completed` success; `Cancelled`/`Archived` neutral.
- **Priority:** `Low` neutral; `Medium` info; `High` orange; `Urgent` error.
- **Overdue:** an error badge.

**Astryx parts:**
- `Token` for status, priority and overdue (`src/components/procedures/ProcedureCells.tsx`).
- `ProgressBar` ("3 of 8 stages") and `Stepper` for the overview.
- `List`/`Item` for the stage list, with `Collapsible` instructions and a `TextArea` for notes.
- `List` + `createReorderable` for ordering a template's stages (drag, plus keyboard and screen-reader support).
- `Selector` for picking the template; `DateInput` for the due date.
- `SelectionDialog` for assigning staff.
- The existing `DataTable`, `SearchDialog`, `ConfirmDialog` and `FormCard`.

**New composites,** each with a Vitest test: `ProcedureStatusBadge`, `ProcedurePriorityBadge`, `StageList`, `StageCard`, `TemplateStageEditor`, `ProcedureForm` and `NotAssignedConfirm` (the decision 18 dialog, built on `ConfirmDialog`).

## 7. Phased delivery

Each phase is its own PR, with CI green and parity screenshots for new screens.

0. **Fixes now:** patient delete becomes a 409, with the count in the confirmation; remove the duplicate DTO. **Done.**
1. **Model, permissions and API.** **Done** (migration `AddedProcedureWorkflow`). This is the big one: one migration, reviewed and tested up and down on a throwaway database, per `AGENTS.md`.
   - **Schema:**
     - stage-definition `IsArchived`;
     - procedure `TemplateId`/`TemplateName`;
     - the stage snapshot, started, finished and locked fields;
     - the stage `Notes` column and the audit table;
     - `NotStarted` → `Assigned`;
     - backfilling snapshots for existing stages.
   - **Behaviour:**
     - `Staff` and `Admin` policies;
     - create from a template;
     - search;
     - stage start, notes, finish and unlock;
     - the task lists, as the procedure search with `AssignedToMe` and `Started`;
     - audit search (admin);
     - assign and unassign;
     - status transitions;
     - audit writes;
     - richer responses.
   - **Supporting work:** handler and validator tests for the whole Procedures area; `medapp-dev`'s `reset_demo_data` seeds notes, locks and audit history to match.
2. **Read-only UI:** All procedures, a patient's procedures (with the buttons on the patients table and View Patient), and the procedure page (stages and notes, read-only). Patients pages open up to staff. **Done.**
3. **Working a procedure:** add notes, finish stages, status actions, admin assign and unassign, admin unlock, and the "not assigned to you" confirmation. **Done:** the current stage is worked on in the left card (notes box, Start, Save Notes, Finish Stage); stages an admin unlocks are edited in the stage list; status changes are buttons in the page header.
4. **Creating and editing:** New procedure from a template; edit details; admin delete when `Cancelled`/`Archived`.
5. **Home task list:** the two side-by-side tables (Not started / Started). **Done** (decision 23).
6. **Admin pages:** Stage definitions, Templates (with archiving) and the Audit log.
7. **Later (not in this plan):** real-time updates for the task list and procedure pages.

## 8. Follow-up questions

All settled on 2026-11-14: Q-A became decisions 10 and 18, Q-B decision 21, Q-C decision 19, Q-D decision 20, Q-E decision 16, Q-F decision 17, Q-G decision 22 and Q-H decision 23.
