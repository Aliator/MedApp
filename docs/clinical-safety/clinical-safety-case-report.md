# MedApp: clinical safety case report

**Draft prepared for review. It must be reviewed, completed and signed off by a qualified Clinical Safety Officer before use; it is not a completed DCB0129 assessment.**

| | |
|---|---|
| **Document** | CS-03, clinical safety case report (DCB0129) |
| **Version** | 0.10, draft |
| **Date** | 16 November 2026 |
| **Release** | [Release name or number]. Drafted against `main` at `88dc1fa` (the merge of #56), with file attachments (branch `feature/file-attachments`) added in version 0.4. |
| **Manufacturer** | [Manufacturer name] |
| **Drafted by** | An AI coding agent, for the manufacturer. Not yet reviewed by a person. |
| **Approved by** | [Clinical Safety Officer], [accountable owner] (see [sign-off](#10-sign-off)) |

## How to use this template

- **One report for each release** that customers can use with real patients, and for each change with a clinical safety impact (`clinical-risk-management-plan.md`, section 6).
- **This copy is filled in** as far as the system at `88dc1fa` allows. The parts only the CSO can write are in square brackets.
- **For a new release:** copy this file to `safety-case-<release>.md`, update every section against that release and the hazard log as it then stands, and keep the signed copy (`clinical-risk-management-plan.md`, section 11).

## 1. Summary

- **What MedApp is:** software that records and coordinates the stages of patients' pathways, with task lists, target dates, attached documents and an audit trail, and books staff time for patients' appointments on staff calendars. It isn't a medical device (`intended-purpose-and-device-decision.md`, which checked the decision again for scheduling).
- **The safety position at `88dc1fa`:**
  - 36 hazards are logged (`hazard-log.md`), rated on DCB0129's consequence table, which counts how many patients a harm affects. H-23 to H-28 came with file attachments, H-29 with admins' monitoring rules, and H-30 to H-36 with staff calendars and appointments (drafts whose ratings are left for the CSO).
  - **One is rated 4** (mandatory elimination or control) with today's controls: wrong template content (H-13), because one wrong template reaches every patient whose pathway comes from it.
  - **Twelve are rated 3 (undesirable):** wrong patient (H-01), wrong record (H-02), a missed overdue pathway (H-04), a forgotten blocked pathway (H-05), a stage finished without the work done (H-07), not learning of assigned work (H-09), unavailability (H-10), times shown an hour out (H-12; its defect is fixed by PR #62, which brings it to 2 once verified), a correction not reaching pathways under way (H-14), concurrent edits (H-15), data lost or restored (H-20) and work left on a deleted account (H-21). File attachments add two more at 3: a file on the wrong record (H-23) and malware in a file (H-25).
  - **At first live use,** with the controls marked "before first live use" built, none is above 3 and three stay at 3: H-07, H-10 and H-13.
  - **Residual,** with every further control built, H-13 stays at 3 and the rest come down to 2.
  - **Referrals** (branch `feature/referrals`, Phase 2 of the community specialist centre) bring five draft hazards, H-40 to H-44, written from the design before the build, with ratings left for the CSO. On their draft estimates, four start at 3 (a referral lost or delayed before triage, H-40; triaged into the wrong pathway, priority or patient, H-41; a paused clock forgotten, H-42; a breach unnoticed, H-43) and all come down to 2 at first live use. Registering referrals, the inbox, closing them and **triage** (accept into a pathway, reject, redirect; branch `feature/triage`, PR 6) are built, with the registrant told of every outcome and the Specialties page's warning when nobody can triage; the waiting-time clock (PR 7) isn't. **Referrals mustn't be used with patients until the CSO has verified the triage controls** (H-40, H-41, H-44). They aren't counted in the figures above.
  - **Specimens** (branch `feature/specimens`, Phase 3's PR 13) are built, with their draft hazards H-45 (a specimen lost, or its report never back) and H-47 (a report on the wrong patient or specimen), each starting at 3 on its draft estimate and coming down to 2 at first live use. Their controls in this PR are built, to verify (the lab worklist, turnaround and Overdue at lab, the last stage refused while a specimen is open, lost and rejected specimens told, the label's identifiers, reporting from the specimen found by its number). **Results sign-off (PR 14) isn't built**, and with it the correction for a report attached to the wrong specimen, so **specimens' lab reports mustn't be relied on with patients until results sign-off and its controls are built and verified.** They aren't counted in the figures above either. (H-37 to H-39, from the notepad, note deletion and walk-ins, aren't in this report yet: it's brought up to date with the hazard log before the first release.)
  - **Staff calendars and appointments** add seven draft hazards, H-30 to H-36, whose ratings are left for the CSO. On their draft estimates, four start at 3 (a needed visit that doesn't happen, H-30; a stage finished without its visit, H-32; the wrong patient booked, H-34; a cancellation not passed on to the patient, H-36), and all come down to 2 at first live use with their "before first live use" controls. They aren't counted in the figures above.
- **The conclusion of this draft:** **this release is not yet acceptable for live clinical use.** The controls marked "before first live use" in the hazard log have to be built and verified first, and the CSO has to justify accepting H-07, H-10 and H-13 at 3 (the go-live rule, `clinical-risk-management-plan.md` section 4.4). MedApp has never been used with real patients, so there is no live use to stop.
- **[CSO's conclusion: to be written by the CSO.]**

## 2. The release

| | |
|---|---|
| **Version** | [Release], built from commit [SHA]; this draft: `88dc1fa` |
| **What's in it** | Patients; pathways from templates; stages worked in order, finished and locked, unlocked by admins; status workflow; target dates and overdue; assignments and My Worklist; assignment notifications (each person can turn them off) and admins' monitoring notifications; files attached to patients, pathways and stages (download only, not yet scanned for malware); the pathway audit log; administration of users, stage definitions and templates (`docs/architecture.md`, section 1.1). With staff calendars (`feature/staff-calendars`): staff calendars with blocked time and day and week templates; appointments (requested, booked, attended, did not attend, cancelled) and their history; the Requires Appointment list; stage visits (none, suggested or required) and the required-visit rule with its admin bypass; appointment notifications. With referrals (`feature/referrals`): registering a referral from its letter (finding or adding the patient, the duplicate check), the referral inbox (awaiting triage, oldest received first, with its count in the side navigation), editing a referral awaiting triage, withdrawing it or marking it entered in error, the letter attached to it, and its history on the patient timeline. Not yet: triage and the waiting-time clock. With specimens (`feature/specimens`): recording a specimen taken at a stage, with its type, lab and requesting clinician; its printed label and request sheet; sent, received, reported (the lab's report attached as a file), rejected, lost or entered in error; the Lab worklist; turnaround and Overdue at lab; the last stage refused while a specimen is open; specimen notifications (rejected, lost); specimen types and labs as setup lists. Not yet: results sign-off |
| **Changes since the last safety case** | None: this is the first. [For later releases: the pull requests with a clinical impact, and their clinical safety sections.] Staff calendars and appointments are the first such change: their hazards are H-30 to H-36, and the device decision was checked for them (`intended-purpose-and-device-decision.md`, version 0.6). Referrals are another: their hazards are H-40 to H-44 (H-01 gains a cause), and the device decision was checked for Phase 2 (`intended-purpose-and-device-decision.md`, version 0.9). Specimens are another: their hazards are H-45 and H-47 (H-01 gains the label as a control), and the device decision was checked against what was built (`intended-purpose-and-device-decision.md`, version 0.11). |
| **Where it runs** | [Customer deployment, or the manufacturer's hosted service on Azure UK South.] Today it has run only in development and on a local test cluster. |
| **Who uses it** | Trained clinical and administrative staff of the deploying organisation, and its administrators |

## 3. Intended purpose and scope

The intended purpose, its users, population and environment, and what it's not for, are in `intended-purpose-and-device-decision.md`. In short: MedApp records and coordinates the steps of a pathway; it doesn't interpret clinical data, prioritise patients or recommend treatment.

**Out of scope of this report:** each organisation's deployment, configuration, templates, training and local processes, which its own DCB0160 safety case covers (`dcb0160-deploying-organisations.md`).

## 4. Clinical risk management

- **The process:** `clinical-risk-management-plan.md`.
- **The risk scheme:** DCB0129's consequence table (five levels, minor to catastrophic, each counting whether one or several patients are affected), its five likelihood levels (very low to very high), and its 5×5 matrix, giving ratings 1 (acceptable) to 5 (unacceptable). Each hazard has three ratings: today, at first live use, and residual. The go-live rule uses the rating at first live use (the plan, section 4.4).
- **The CSO:** [name, profession, professional body and registration number].
- **How the hazards were found:** by reading the code at `88dc1fa`, and walking each workflow in `docs/architecture.md` section 6.3 and `docs/pathways-ui-plan.md`. **Not yet done:** the hazard workshop with clinicians from a likely customer, and any review by the CSO. [CSO: record the workshop, its attendees and what it added.]

## 5. Safety claims and the evidence for them

Each claim is backed by evidence that exists today, and says where it falls short.

### Claim 1: The workflow rules that protect the record are enforced by the server, not just the screen

| Evidence | Where |
|---|---|
| Stages can only be worked in order; only the current stage can be edited or finished; a finished stage is locked | `PathwayWorkflow.WhyStageCantBeEdited`, `PathwayChange.FinishStage` |
| A change that breaks a rule is refused with a 409, whatever the screen shows | Every pathway command handler, for example `FinishPathwayStageHandler`, `UpdatePathwayStageNotesHandler` |
| A pathway can only be completed with every stage finished and locked | `ChangePatientPathwayStatusHandler` |
| Status changes follow one table; blocking and cancelling need a reason | `PathwayWorkflow.AllowedStatusChanges`, `ChangePatientPathwayStatusValidator` |
| Only admins can unlock stages, assign staff, delete pathways and change templates | `[Authorize(Roles = "Admin")]` on those endpoints (`docs/architecture.md`, section 5.7) |
| Tests | `PathwayWorkflowTests` (41 cases), `PathwayChangeTests` (19), and a test class for every pathway handler and validator under `MedApp.UnitTests/Application/Pathways` |

**Limits:** the role attributes on endpoints aren't covered by tests, except the own-password and create-user endpoints (`docs/architecture.md`, risk S7). Nothing stops two people's changes overwriting each other (H-15).

### Claim 2: Every change to a pathway can be traced to a person and a time

| Evidence | Where |
|---|---|
| Every pathway change writes audit entries, saved in the same transaction as the change | `PathwayChange.Audit`, `PatientPathwayRepository.SaveAsync` |
| Notes changes keep the old and new text | `PathwayChange.UpdateStageNotes` |
| Audit entries outlive deletion of the pathway (no foreign keys), and keep the username and title as they were | `PatientPathwayAuditEntry`; ADR-7 in `docs/architecture.md` |
| Every file attached, deleted or downloaded is audited with who, when and which file; a deleted file's details stay, marked deleted, with who deleted it | `AttachmentAudit`; `Attachment`; `DeleteAttachmentHandler` |
| Tests | The pathway command handler tests check the audit entries the commands write, and `PathwayChangeTests` checks the entries `PathwayChange` writes |

**Limits:** changes to templates and stage definitions aren't audited (H-13). Since roadmap step 7, patient changes (which fields, not their values) and reads are recorded in the activity log (H-03). Times were shown an hour out in summer at `88dc1fa` (H-12); fixed by PR #62.

### Claim 3: Target dates mean the same thing everywhere

| Evidence | Where |
|---|---|
| A target date is a whole day, due by the end of that day in UK time, stored as that moment in UTC | `DueDates` |
| Overdue has one rule, used in memory and in SQL | `PathwayWorkflow.IsOverdue`, `PathwaySearch.Filter` |
| Tests cover summer, winter and both clock-change days | `DueDatesTests` |

**Limits:** "overdue" is only a badge; today, nothing alerts anyone (H-04), and My Worklist opens on work not yet started, so started overdue work isn't on its default view (H-04, H-09). Blocked pathways are never overdue (H-05). Target dates have no time of day (H-11).

### Claim 4: Finished work isn't rewritten by later changes to templates

| Evidence | Where |
|---|---|
| Each pathway copies its template's stage names, descriptions and instructions when it's created | `CreatePatientPathwayHandler` |
| Templates and definitions in use are archived, not deleted | the template and definition delete and archive handlers |

**Limits:** the same rule means a correction doesn't reach pathways already under way (H-14), and template changes have no history or approval (H-13).

### Claim 5: Optional parts fail soft

| Evidence | Where |
|---|---|
| If server rendering fails, the browser renders the page | `SsrPages`, `SsrPagesTests`, `SsrClientTests` |
| Notifications are stored before they're pushed, so a failed push shows at the next page load | `NotificationSender` |
| A failed migration stops the rollout and leaves the old version running | ADR-10 |
| Without file storage, the rest of MedApp works, and uploads and downloads are refused with a clear message | `UnconfiguredFileStore`; `ExceptionHandling` (503) |

**Limits:** there's no monitoring, so failures go unnoticed (H-10); the live connection stops retrying after about 42 seconds (H-09). Storage that fails is reported as a 503 within about a minute (2 retries, 30 seconds a try), but nothing checks it before staff meet it (H-28).

### Claim 6: Appointments are booked at the time intended, without clashes, and required visits are enforced by the server

| Evidence (to collect once built) | Where |
|---|---|
| Times are stored and sent in UTC and shown in UK time; template times are UK clock times turned into UTC for each date; the browser does no time zone arithmetic | `UkTime`, `CalendarProjection`; `docs/calendar-plan.md`, decision 3 |
| Bookings and blocks outside the working day, at weekends or in the past are refused | `WorkingHours.WhyOutside`; decisions 2 and 17 |
| Overlaps are refused with a 409, and two bookings racing for one slot can't both succeed (a lock per member of staff) | `CalendarClashes`, `ICalendarLock`; decision 6 |
| A stage whose visit is required can't be finished by staff without an Attended appointment; an admin's bypass is recorded with its reason | `PathwayWorkflow.WhyStageNeedsVisit`; decision 24 |
| Every change to an appointment is kept in its history | `AppointmentChange`, `AppointmentEvent`; decision 19 |
| Tests | Unit tests for `WorkingHours`, `TemplateHistory`, `CalendarProjection` (with a clock change), `CalendarClashes`, `AppointmentWorkflow` and the visit rule; integration tests for clashes (with two bookings racing), the visit rule and the admin bypass, projection across a clock change, and the status changes (plan, section 7) |

**Limits:** MedApp doesn't contact patients, so cancellations and changes rely on the organisation (H-36); nothing flags a Requested appointment that has waited too long (H-30); a template change can overlap appointments already booked, which is shown but not resolved (H-31, H-35).

### Claim 7: The residual risk of the release is acceptable

**Not yet supported.** Today one hazard is at 4 and fourteen at 3 (thirteen once H-12's fix is verified); at first live use, three would stay at 3. See [section 7](#7-outstanding-hazards-and-defects).

## 6. Hazards and their risk

From `hazard-log.md`, version 0.9, with H-40 to H-44 from version 0.19, and H-45 and H-47 from version 0.20. H-30 to H-36, H-40 to H-44, H-45 and H-47 are drafts whose ratings are left for the CSO; the figures shown for them are the log's draft estimates, in brackets. **At first live use** counts only the controls marked "before first live use"; **residual** counts every further control.

| ID | Hazard | Initial | At first live use | Residual | Controls needed before first live use |
|---|---|---|---|---|---|
| H-01 | The wrong patient's pathway is worked | 3 | 2 | 2 | Patient banner (built on branch `feature/nhs-integrations`, to verify); date of birth in lists |
| H-02 | Work is recorded on the wrong record | 3 | 2 | 2 | Patient banner (built on branch `feature/nhs-integrations`, to verify); patient named in confirmations |
| H-03 | A patient's details are changed or deleted without trace | 2 | 2 | 2 | Patient changes recorded (fields, not old values; step 7) |
| H-04 | An overdue pathway is missed | 3 | 2 | 2 | Overdue notifications; started work counted on My Worklist; an unassigned-and-due view; local review process |
| H-05 | A blocked pathway is forgotten | 3 | 2 | 2 | A "past target date" flag on blocked pathways; started work counted on My Worklist |
| H-06 | A stage is finished in error | 2 | 2 | 2 | None |
| H-07 | A stage is recorded as finished when the work wasn't done | 3 | **3** | 2 | Notes, or "nothing to record", before finishing; CSO justification |
| H-08 | A pathway is cancelled, completed or archived in error | 2 | 2 | 2 | None |
| H-09 | A member of staff doesn't learn of work assigned to them | 3 | 2 | 2 | Started work counted on My Worklist; information for users |
| H-10 | MedApp is unavailable | 3 | **3** | 2 | Printable task list and summary; downtime procedure; monitoring; CSO justification |
| H-11 | Target dates don't mean what staff think | 2 | 2 | 2 | Information on the form |
| H-12 | Times are shown an hour out | 3 | 2 | 2 | The fix, built in PR #62; to verify |
| H-13 | A template or stage definition has wrong content | **4** | **3** | **3** | Approval step for template changes (brought forward); audit of template and definition changes; local template governance; CSO justification |
| H-14 | A correction to a template doesn't reach pathways under way | 3 | 2 | 2 | Open pathways listed when a template in use is edited; local review process |
| H-15 | Two people edit the same pathway at once | 3 | 2 | 2 | Optimistic concurrency; keep the draft on a conflict |
| H-16 | The screen shows out-of-date information | 2 | 2 | 2 | None |
| H-17 | Stage instructions aren't read | 2 | 2 | 2 | None |
| H-18 | A change is recorded under the wrong person's name | 2 | 2 | 2 | None |
| H-19 | Part of the clinical record is lost by deletion | 2 | 2 | 2 | Anonymisation guarded: typed name, reason, refused while pathways are under way (step 7) |
| H-20 | Data is lost, or restored to an earlier point | 3 | 2 | 2 | Tested restore; restore runbook |
| H-21 | Work assigned to a deleted account drops off every task list | 3 | 2 | 2 | Refuse to delete an account with open assignments, or require reassignment first; leaver process |
| H-22 | Care under way is delayed because the patient's record is restricted | 2 | 2 | 2 | Confirming the pathways under way and open appointments when restricting; blocking and cancelling still allowed; banners; a local procedure for restriction requests |
| H-23 | A file is attached to the wrong patient, pathway or stage | 3 | 2 | 2 | Patient banner (built, to verify); the patient and target named before files are sent |
| H-24 | A document or result is missing, and nobody knows | 2 | 2 | 2 | None |
| H-25 | An attached file carries malware | 3 | 2 | 2 | Malware scanning on upload (brought forward from step 9), or no file storage |
| H-26 | A file is deleted by mistake | 2 | 2 | 2 | None |
| H-27 | An out-of-date or duplicate document is relied on | 2 | 2 | 2 | None |
| H-28 | File storage is unavailable | 2 | 2 | 2 | Monitoring that includes storage; downtime procedure covers documents |
| H-29 | An admin relies on a monitoring rule to follow up clinical work | 2 | 2 | 2 | Information: monitoring is a supervision aid, not how clinical follow-up is assured |
| H-30 | A visit the patient needs doesn't happen | (3) | (2) | (2) | Flag or notify Requested appointments waiting too long and Did not attend not rebooked; a Requires Appointment count; local process |
| H-31 | A member of staff is double-booked | (2) | (2) | (2) | Overlaps from a template change listed and the staff notified |
| H-32 | A stage is finished without the visit it needed | (3) | (2) | (2) | A report of stages finished without a visit, reviewed locally; patient, stage and appointment named in the confirmations; clinical sign-off of which stages need a visit |
| H-33 | An appointment is at the wrong time | (2) | (2) | (2) | Full date and day in confirmations; end-to-end tests outside UK time and on clock-change days |
| H-34 | The wrong patient or pathway is booked | (3) | (2) | (2) | Date of birth in the patient picker and confirmations; the patient's name, date of birth and NHS number in the booking and appointment dialogs (built, to verify) |
| H-35 | An appointment is left without its member of staff | (2) | (2) | (2) | Overlaps from a template change listed and the staff notified; removing every role refused with upcoming bookings (built, to verify); local absence process |
| H-36 | A cancelled or moved appointment isn't passed on to the patient | (3) | (2) | (2) | "Has the patient been told?" recorded on cancel and move; local process for telling patients |
| H-40 | A referral is lost or delayed before triage | (3) | (2) | (2) | Built, to verify: the inbox, oldest first, with its count in the side navigation; no closing without a named person's outcome or a detail; triage, with the registrant told of every outcome; the Specialties page's warning when nobody can triage; the restricted message (give the letter to an admin); archiving a specialty refused with referrals waiting. To build: the clock's days waited and breach badge. Local: a daily inbox check, a downtime procedure for referrals, a triager for every specialty |
| H-41 | A referral is triaged into the wrong pathway, priority or patient | (3) | (2) | (2) | Built, to verify: the patient banner on the referral pages and in every closing and triage confirmation, with the specialty and template chosen; the letter and the patient's other referrals beside the decision; templates limited to the specialty (refused otherwise); nothing prefilled; only a clinician of the specialty decides; two decisions at once refused. Local: clinical review of triage |
| H-42 | A paused waiting-time clock is forgotten | (3) | (2) | (2) | To build (PR 7): a required review date at most 90 days ahead; the target date becomes the review date; the Paused clocks view |
| H-43 | A breach of a waiting-time target goes unnoticed, or the target is wrong | (3) | (2) | (2) | To build (PR 7): Breached on every list, sort by wait; targets visible. Local: set and review targets; review the waiting list on a schedule |
| H-44 | A duplicate referral leads to two pathways for one need | (2) | (2) | (2) | Built, to verify: the duplicate check and its confirmation; entered in error. To build: the Duplicate rejection reason at triage |
| H-45 | A specimen is lost, or its report never comes back | (3) | (2) | (2) | Built, to verify: the lab worklist (To send, Expected, In the lab, Overdue, Rejected and lost, Reported), turnaround and Overdue at lab, the last stage refused while a specimen is open, lost and rejected specimens recorded and the requesting clinician and taker told, lab steps in any pathway status. To build: results sign-off (PR 14); the service alert (Phase 4, later). Local: a daily lab worklist check, chasing the lab |
| H-47 | A report is attached to the wrong patient or specimen | (3) | (2) | (2) | Built, to verify: the label's identifiers (name, date of birth, NHS number, the number as text and QR code); reporting from the specimen found by its number, with the patient banner; a report can't be deleted on its own. To build (PR 14): entered in error sends a reported specimen back; the results list. Local: positive identification when labelling; the lab checks the label against the request |

[CSO: confirm or change each rating, and record the reasons for any change. Assign ratings to H-30 to H-36, H-40 to H-44, H-45 and H-47.]

## 7. Outstanding hazards and defects

### Hazards not yet controlled to an acceptable level

The go-live rule uses the rating **at first live use.** For each hazard at 3 or above today, the CSO confirms that its "before first live use" controls are built and verified. For H-07, H-10 and H-13, which stay at 3 even then, the CSO also writes a justification for accepting them at 3, or asks for more controls first.

| Hazard | Today | At first live use | Decision | Justification or evidence | CSO |
|---|---|---|---|---|---|
| H-01 | 3 | 2 | [Controls verified / not verified] | | |
| H-02 | 3 | 2 | | | |
| H-04 | 3 | 2 | | | |
| H-05 | 3 | 2 | | | |
| H-07 | 3 | 3 | [Accepted at 3 / not accepted] | | |
| H-09 | 3 | 2 | | | |
| H-10 | 3 | 3 | [Accepted at 3 / not accepted] | | |
| H-12 | 3 (2 with the fix in PR #62) | 2 | [Fix verified / not verified] | | |
| H-13 | 4 | 3 | [Accepted at 3 / not accepted] | | |
| H-14 | 3 | 2 | | | |
| H-15 | 3 | 2 | | | |
| H-20 | 3 | 2 | | | |
| H-21 | 3 | 2 | | | |
| H-23 | 3 | 2 | | | |
| H-25 | 3 | 2 (with scanning) | [Controls verified / not verified] | | |
| H-30 | [CSO to assign; draft 3] | [draft 2] | | | |
| H-32 | [CSO to assign; draft 3] | [draft 2] | | | |
| H-34 | [CSO to assign; draft 3] | [draft 2] | | | |
| H-36 | [CSO to assign; draft 3] | [draft 2] | | | |
| H-40 | [CSO to assign; draft 3] | [draft 2] | | | |
| H-41 | [CSO to assign; draft 3] | [draft 2] | | | |
| H-42 | [CSO to assign; draft 3] | [draft 2] | | | |
| H-43 | [CSO to assign; draft 3] | [draft 2] | | | |
| H-45 | [CSO to assign; draft 3] | [draft 2] | | | |
| H-47 | [CSO to assign; draft 3] | [draft 2] | | | |

### Known defects with a clinical impact

| Defect | Hazard | State |
|---|---|---|
| Timestamps read from the database are sent without a time zone, and the web app reads them as local time, so they show an hour early in summer | H-12 | Fixed by PR #62, which marks every time read from the database as UTC, so the API sends them with a `Z`, and reads times without an offset as UTC in the web app. To verify against SQL Server and in a browser during British Summer Time. |
| My Worklist opens on work not yet started, so work already under way, including overdue and blocked work, isn't on its default view | H-04, H-05, H-09 | Known. A count of started work, or opening on all open work, proposed before first live use. |
| Deleting an account leaves its open pathways assigned to it, on nobody's task list | H-21 | Known. Refusing the delete, or requiring reassignment first, proposed before first live use. |
| Live notifications stop reconnecting after about 42 seconds | H-09 | Known (`docs/architecture.md`, risk R11). Planned, step 2. |
| No optimistic concurrency: the last save wins | H-15 | Known (risk R8). Planned, step 6. |
| Sessions have no idle timeout | H-18 | Known (risk S5). Planned, step 2. Sessions now end when a password is changed or reset, and an admin can sign a user out everywhere (`feature/security-baseline`, not yet merged). |
| Attached files aren't scanned for malware; every file shows "Not scanned" | H-25 | Known (`docs/todo.md`). Planned, step 9; proposed before first live use. |
| A deleted file can't be restored, or downloaded, from MedApp | H-26 | Known. Restore planned with the purge job (`docs/todo.md`). |
| Failing (as opposed to absent) file storage gave a general server error after minutes of retries | H-28 | Fixed after the review of #85: a 503, "File storage isn't available", with 2 retries, a 30-second timeout and a 10-second clean-up. |
| Nothing warns before closing a tab with files still being sent | H-24 | Known. Proposed. |
| Triage is built but not yet verified by the CSO; the waiting-time clock isn't built | H-40, H-41 | Known. Referrals mustn't be used with patients until the triage controls are verified; the clock is Phase 2's PR 7. |
| A report attached to the wrong specimen can't be corrected in MedApp: a reported specimen is closed, and its report can't be deleted on its own | H-47 | Known (`docs/todo.md`). Entered in error for a reported specimen comes with results sign-off (Phase 3's PR 14). |
| A reported specimen doesn't yet make a result for a clinician to sign off: the report is filed, and nobody is told it's back | H-45, H-46 | Known. Results sign-off is Phase 3's PR 14; specimens' reports mustn't be relied on with patients before it. |
| Patient changes and deletions are recorded without their old values | H-03 | Known (risk S2). Who, when and which fields since step 7; old values planned (step 7). |

[For each release: list the open defects from the issue tracker that have a clinical impact.]

## 8. Assumptions about deploying organisations

This report holds only if each deploying organisation:
- **identifies patients** at the point of care by its own positive identification process;
- **governs its templates:** clinical sign-off of each template and stage definition before use, including which stages involve a visit and whether it's suggested or required, and review when a template changes;
- **sets up its calendars:** activity types, day and week templates and each person's default week, and records staff absence in MedApp so appointments aren't booked over it;
- **books and follows up visits:** reviews the Requires Appointment list and Did not attend appointments on a set schedule, and keeps MedApp's calendar as the diary for the appointments it holds;
- **handles referrals safely** (once it uses them): gives the permission to register referrals, and later to triage them, only to the right people, with a triager for every specialty; checks the referral inbox every working day; keeps a paper or downtime process for referrals received when MedApp is unavailable, or refused for a restricted patient (the letter goes to an admin); and, once the waiting-time clock exists, sets its targets and reviews its waiting list;
- **handles specimens safely** (once it uses them): gives the permissions to record and to process specimens only to the right people; sets each specimen type's expected turnaround with its lab; checks the lab worklist every working day; checks each label against the patient before a specimen leaves; and keeps a downtime process for lab work;
- **tells patients about their appointments,** and about every cancellation and change, by its own process: MedApp doesn't contact patients;
- **reviews stages finished without a visit,** and limits the admin bypass to cases with a recorded reason;
- **trains its staff** in MedApp, including what finishing a stage means, what a target date means, that notifications aren't to be relied on, and that My Worklist must be set to all open work to show everything assigned to them;
- **has at least two admins,** so assignment and unlocking never wait on one person;
- **reassigns a leaver's open pathways** before their account is removed;
- **reviews overdue, unassigned and blocked pathways** on a set schedule;
- **has a downtime procedure,** with printed task lists, and a way to enter work done during downtime afterwards;
- **keeps the clinical record** of what was done in its own clinical systems, using MedApp to coordinate the steps, and keeps the master copy of each document attached in MedApp (results, letters, images) in its own systems too;
- **checks a document's own identifiers** against the patient before acting on it, and has up-to-date anti-malware and software on every computer that opens attached files;
- **runs DCB0160:** its own CSO, hazard log and safety case for the deployment, and reports incidents to the manufacturer;
- **uses MedApp within its intended purpose,** and not for time-critical or emergency care.

These are listed for the customer in `dcb0160-deploying-organisations.md`.

## 9. Conclusion and recommendation

- **For this draft:** MedApp at `88dc1fa` should not be used with real patients until the controls marked "before first live use" are built and verified, the CSO has reviewed every hazard, and the hazard workshop has been held.
- **[CSO's conclusion:** whether the residual risk of this release is acceptable for its intended purpose, under the stated assumptions, and any conditions on its use.**]**

## 10. Sign-off

| Role | Name | Registration (CSO) | Signature | Date |
|---|---|---|---|---|
| Clinical Safety Officer | [Name] | [Professional body and number] | | |
| Accountable owner | [Name] | n/a | | |

## Change log

| Version | Date | Change | By |
|---|---|---|---|
| 0.1 | 16 November 2026 | Template, filled in for `88dc1fa` | AI coding agent, for review |
| 0.2 | 16 November 2026 | After the review of #60: ratings on DCB0129's consequence table, a rating at first live use and the go-live rule on it, H-21, My Worklist's default view, H-12's fix in PR #62 | AI coding agent, for review |
| 0.3 | 16 November 2026 | H-12's defect fixed by PR #62 | AI coding agent, for review |
| 0.4 | 16 November 2026 | File attachments: H-23 to H-28 in the summary, the hazard table and the outstanding hazards; their evidence in claims 2 and 5; their known defects; two new assumptions | AI coding agent, for review |
| 0.5 | 16 November 2026 | Merged with admins' monitoring (PR #84): H-29 in the hazard table and the count. After the review of #85: H-28's defect (a 500 after minutes of retries) fixed | AI coding agent, for review |
| 0.6 | 16 November 2026 | The privacy features (PR #68): H-22 and H-19's anonymisation control in the hazard summary. Made in `main` without a new version; recorded here when staff calendars merged it | AI coding agent, for review |
| 0.7 | 16 November 2026 | Staff calendars and appointments: what's in the release, a claim for booking times, clashes and the visit rule, the draft hazards H-30 to H-36 (ratings left for the CSO), and the assumptions about calendar setup, visits and telling patients. On merging file attachments: H-23 to H-29 with the calendar hazards in the count and the tables. On merging email and two-factor sign-in (#86): the hazard log's version 0.9 cited, with no change to the hazards or counts | AI coding agent, for review |
| 0.8 | 18 November 2026 | Referrals (branch `feature/referrals`, Phase 2's PR 5): what's in the release, the draft hazards H-40 to H-44 in the summary, the hazard table and the outstanding hazards (ratings left for the CSO), triage not yet built as a known defect, and the deploying organisation's duties for referrals. H-37 to H-39 are still to be added | AI coding agent, for review |
| 0.9 | 18 November 2026 | Triage (branch `feature/triage`, Phase 2's PR 6): the summary, H-40 and H-41 in the hazard table (triage, the registrant told, the Specialties warning, the confirmations and narrowed choices built, to verify), and the known defect now "not yet verified" rather than "not built" | AI coding agent, for review |
| 0.10 | 18 November 2026 | Specimens (branch `feature/specimens`, Phase 3's PR 13): what's in the release, the draft hazards H-45 and H-47 in the summary, the hazard table and the outstanding hazards (ratings left for the CSO), two known defects (no correction yet for a report on the wrong specimen; no result made from a report until PR 14), and the deploying organisation's duties for specimens | AI coding agent, for review |
