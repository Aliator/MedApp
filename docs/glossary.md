# Glossary

The words MedApp uses, on screen and in code, and what they were called before.

MedApp was built around "procedures". In November 2026 they became **pathways**, to suit a community specialist centre (migration `RenamedProceduresToPathways`, script `tools/rename-procedures-to-pathways.py`). This page is the one place that keeps both vocabularies side by side.

## The vocabulary

| Before | Now | Where it changed |
|---|---|---|
| Procedure (`PatientProcedure`, `PatientProcedureStage`, `PatientProcedureNote`, `ProcedureChange`, `ProcedureWorkflow`, `ProcedureActor`, the `Procedures` area) | **Pathway** (`PatientPathway`, `PatientPathwayStage`, `PatientPathwayNote`, `PathwayChange`, `PathwayWorkflow`, `PathwayActor`, the `Pathways` area) | Code, database, API, screens |
| Template of stages (`PatientProcedureStageTemplate`, `StageTemplateMapper`, the Templates admin page) | **Pathway template** (`PathwayTemplate`, `PathwayTemplateMapper`, the Pathway Templates page) | Code, database, API, screens |
| Stages (the admin list) | **Stage library** | Screens only: the code still says `StageDefinition` |
| Status In review | **Awaiting review** | Screens only: the status is still `InReview` |
| Status Blocked | **On hold** | Screens only: the status is still `Blocked` |
| Status Completed | **Discharged** | Screens only: the status is still `Completed` |
| Due date | **Target date** | Screens only: the field is still `DueDate` |
| My Tasks | **My Worklist** | Screens only: the page is still `MyTasksPage`, at `/tasks` |

**Unchanged:** Unassigned, Assigned, In progress, Cancelled, Archived, "Assigned to", the priorities, and Requires Appointment.

**Calendar day and week templates are a different thing** and keep their names (`DayTemplate`, `WeekTemplate`, `/calendar/templates`).

## Screens only

"Screens only" means the label people see changed, and nothing underneath did: the enum member, the DTO field, the route and the column keep their names. So the code and the audit log say `Blocked` where the screen says "On hold".

- **Statuses in words** come from one place on each side: `PathwayWorkflow.Describe` in the API (its messages) and `statusWords` / `statusLabel` in the web app (`MedApp.Web/src/utils/statusWords.ts`).
- **The actions follow the statuses:** Put on Hold and Take off Hold (the status change to and from `Blocked`), Mark Discharged (to `Completed`).
- **Field and column labels inside a page stay short:** a pathway's "Template", the "Stages" of a pathway.

## The waiting-time clock's words

A referral's clock (`WaitingClock`, `docs/agents/referrals.md`) shows as the **wait**, "Day 84 of 126" (days waited of the target); **Breached** once it's past its target date; **Paused** (with its review date, flagged once past); and **Stopped**. The **clock-stop stage** is the template's stage whose finishing stops it (`StopsClock`). While the clock runs or is paused, a pathway's target date comes from it ("from the waiting-time clock").

## The multi-clinic centre's words

From `docs/multi-clinic-plan.md` (decision 181). Planned: each word arrives with the PR that builds it.

- **Team:** a clinic or the lab. Was a **specialty** (renamed in code by MC2; stored detail keys keep `specialty`). Screens say "team" looking inward: your team, the team calendar, the team worklist.
- **Clinic:** a team that sees patients (Dermatology). Screens say "clinic" when sending outward: "Refer to Dermatology", "a pathway in Dermatology".
- **The lab:** the centre's in-house laboratory team. It serves every clinic and a nearby hospital.
- **Centre:** the whole organisation; never "company".
- **Relay:** a pathway passing work between a clinic and the lab, like a baton: the team that owns a stage works it, then hands back. **Handover** is the pass; **pick up** is the receiving team taking it.
- **Fork:** where a pathway splits into parallel routes. **Join:** a stage that waits until every route into it is finished or skipped.
- **Branch** (added): a route added to a running pathway from its template's list, when new information arrives.
- **Sample:** what screens call a specimen. The code keeps `Specimen` (FHIR's word).
- **Lab case:** the lab's record of the work on a request: the test, its steps, the report and its clock.
- **Hospital request:** the hospital asking the lab to examine a sample, shaped on FHIR UK Core's `ServiceRequest-Lab`.
- **Clock** (a lab case's): its deadline, the lab's own limit or the hospital's date, whichever is sooner; at risk, then breached. Not the referral's waiting-time clock above.
- **Admin, staff, clinic admin:** the account types. Admins read everything and change nothing clinical; staff do clinical work in their team; a clinic admin is staff with small extras for their own team.

## Where "procedure" stays

- **In its medical sense:** something done to a patient. A stage named "Procedure", "minor procedures", "pre-procedure check", SNOMED CT's concept 71388002 (Procedure), and the kinds of procedure in the demo data (`ProcedureKind`).
- **In its everyday sense:** a way of doing something. The rights procedure, the breach procedure, a downtime procedure, a stored procedure.
- **In history:** migrations and their names (`RenamedTasksToProcedures`, `AddedProcedureWorkflow`, `AddedProcedureNotes`), `docs/reviews/`, and old branch names.

## Stored names that kept the old word

Stored numbers never changed (statuses, audit actions, notification kinds, monitoring events). A few stored names did, or deliberately didn't:

| Stored | What happened |
|---|---|
| Tables, columns, keys and indexes | Renamed in place by the migration; every row kept |
| What a file is attached to, in the audit entries about files (`"attachedTo":"Procedure"`) | Rewritten to `"Pathway"` by the migration |
| The title of an anonymised record's pathways ("Anonymised procedure") | Rewritten to "Anonymised pathway" by the migration |
| Notifications already sent | Left as they were: they keep the words people were shown |
| Account settings: `medapp.proceduresStartOnMine`, the start page's value `procedures`, saved searches under `procedures` and `patientProcedures` | Kept: the keys aren't shown to anyone |
| Account settings: pinned pathways, under `procedures` in `medapp.pins` | Read from the old key until the pins are next saved, which writes them under `pathways` |

## Old addresses and saved searches

The web app still answers the addresses pages had before, and sends them on (`MedApp.Web/src/app/beforePathways.ts`, the one module there that knows the old names):

| Old address | Goes to |
|---|---|
| `/procedures`, `/procedures/new`, `/procedures/{id}`, `/procedures/{id}/edit` | `/pathways/…` |
| `/patients/{id}/procedures` | `/patients/{id}/pathways` |
| `/admin/templates`, `/admin/templates/new`, `/admin/templates/{id}` | `/admin/pathway-templates/…` |

A bookmark or saved search that sorts by the old Procedure column, filters the activity log by `ProcedureViewed`, or opens the audit log with `procedureId`, is read as its name now. The API has no aliases: `api/procedures/…` is gone, and `api/pathways/…` replaced it.
