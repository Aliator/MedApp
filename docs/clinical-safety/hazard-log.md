# MedApp: hazard log

**Draft prepared for review. It must be reviewed, completed and signed off by a qualified Clinical Safety Officer before use; it is not a completed DCB0129 assessment.**

| | |
|---|---|
| **Document** | CS-02, hazard log (DCB0129) |
| **Version** | 1.0, draft |
| **Date** | 18 November 2026 |
| **System** | MedApp, `main` at `88dc1fa` (the merge of #56); H-12 updated for its fix in PR #62; H-03, H-19 and the new H-22 for the privacy features (PR #68); H-01, H-02 and H-13 for the NHS integrations (PR #69: the NHS number, the patient banner, the FHIR API, SNOMED CT codes and the demographics lookup); H-09 and the new H-29 for admins' monitoring (PR #84); H-23 to H-28 added for file attachments (branch `feature/file-attachments`), and H-10 for its review; H-30 to H-36 drafted for staff calendars and appointments, from their design (`docs/calendar-plan.md`, `feature/staff-calendars`); H-37 drafted for the notepad's Add Note to Pathway (`feature/procedure-notes`), and updated for the notepad in a half view (`feature/notepad-window`); H-38 drafted for deleting pathway notes (`feature/note-deletion`); H-39 drafted for appointments recorded after the event, and H-31 to H-35 updated for closed days and walk-ins (`feature/closed-days-walk-ins`); H-40 to H-44 drafted for referrals, triage and the waiting-time clock, from their design (`docs/centre-phase2-plan.md`), and H-01 updated; H-45 to H-64 drafted for the centre's Phases 3 to 5, from their design (`docs/centre-phases-3-5-plan.md`), and H-01 updated; H-40, H-41, H-44 and H-01 updated for the referrals built (`feature/referrals`), and H-40, H-41 and H-44 for triage (`feature/triage`); H-51's controls built with branching pathways (`feature/branching-pathways`); H-53 to H-55 updated for patient reminders and the Did not attend view built, and H-30, H-36 and H-52 for what they add (`feature/patient-reminders`); H-29, H-32 and H-51 updated for the stage overrides split and the bypass monitored (`feature/stage-permissions`); H-45, H-47 and H-01 updated for the specimens built (`feature/specimens`); H-48 to H-50 updated for letters built (PR 16) |
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
- **Names of statuses:** this log names a pathway's statuses as the code and the audit log do. Three show on screen under other names (`docs/glossary.md`): in review as "Awaiting review", blocked as "On hold" and completed as "Discharged". Nothing about what they do changed with the names.
- **Assumption behind every rating:** the deploying organisation identifies patients at the point of care by its own positive identification process, and uses MedApp within its intended purpose (`intended-purpose-and-device-decision.md`). If it doesn't, several ratings go up.

## Summary

| ID | Hazard | Initial risk | At first live use | Residual risk | Needs a product change? | Status |
|---|---|---|---|---|---|---|
| [H-01](#h-01-the-wrong-patients-pathway-is-worked) | The wrong patient's pathway is worked | Major, Low: **3** | Major, Very low: **2** | Major, Very low: **2** | **Yes, before first live use** (the banner built on branch `feature/nhs-integrations`, to verify; the rest proposed) | Open |
| [H-02](#h-02-work-is-recorded-on-the-wrong-record) | Work is recorded on the wrong record | Considerable, Medium: **3** | Considerable, Low: **2** | Considerable, Low: **2** | **Yes, before first live use** (the banner built on branch `feature/nhs-integrations`, to verify; the rest proposed) | Open |
| [H-03](#h-03-a-patients-details-are-changed-or-deleted-without-trace) | A patient's details are changed or deleted without trace | Considerable, Low: **2** | Considerable, Low: **2** | Considerable, Very low: **2** | Yes, later | Open |
| [H-04](#h-04-an-overdue-pathway-is-missed) | An overdue pathway is missed | Considerable, Medium: **3** | Considerable, Low: **2** | Considerable, Low: **2** | **Yes, before first live use** | Open |
| [H-05](#h-05-a-blocked-pathway-is-forgotten) | A blocked pathway is forgotten | Considerable, Medium: **3** | Considerable, Low: **2** | Considerable, Low: **2** | **Yes, before first live use** | Open |
| [H-06](#h-06-a-stage-is-finished-in-error) | A stage is finished in error | Considerable, Low: **2** | Considerable, Low: **2** | Considerable, Very low: **2** | Yes, later | Open |
| [H-07](#h-07-a-stage-is-recorded-as-finished-when-the-work-wasnt-done) | A stage is recorded as finished when the work wasn't done | Major, Low: **3** | Major, Low: **3** | Major, Very low: **2** | **Yes, before first live use** | Open |
| [H-08](#h-08-a-pathway-is-cancelled-completed-or-archived-in-error) | A pathway is cancelled, completed or archived in error | Considerable, Low: **2** | Considerable, Low: **2** | Considerable, Very low: **2** | Yes, later | Open |
| [H-09](#h-09-a-member-of-staff-doesnt-learn-of-work-assigned-to-them) | A member of staff doesn't learn of work assigned to them | Considerable, Medium: **3** | Considerable, Low: **2** | Considerable, Very low: **2** | **Yes, before first live use** | Open |
| [H-10](#h-10-medapp-is-unavailable) | MedApp is unavailable | Major, Medium: **3** | Major, Low: **3** | Major, Very low: **2** | **Yes, before first live use** | Open |
| [H-11](#h-11-due-dates-dont-mean-what-staff-think) | Target dates don't mean what staff think | Considerable, Low: **2** | Considerable, Low: **2** | Considerable, Very low: **2** | Yes, later | Open |
| [H-12](#h-12-times-are-shown-an-hour-out) | Times are shown an hour out | Considerable, High: **3** | Considerable, Very low: **2** | Considerable, Very low: **2** | **Yes, before first live use** (built in PR #62; to verify) | Open |
| [H-13](#h-13-a-template-or-stage-definition-has-wrong-content) | A template or stage definition has wrong content | Catastrophic, Low: **4** | Catastrophic, Very low: **3** | Catastrophic, Very low: **3** | **Yes, before first live use** | Open |
| [H-14](#h-14-a-correction-to-a-template-doesnt-reach-pathways-already-under-way) | A correction to a template doesn't reach pathways already under way | Major, Low: **3** | Major, Very low: **2** | Major, Very low: **2** | **Yes, before first live use** | Open |
| [H-15](#h-15-two-people-edit-the-same-pathway-at-once) | Two people edit the same pathway at once | Considerable, Medium: **3** | Considerable, Very low: **2** | Considerable, Very low: **2** | **Yes, before first live use** | Open |
| [H-16](#h-16-the-screen-shows-out-of-date-information) | The screen shows out-of-date information | Significant, Medium: **2** | Significant, Medium: **2** | Significant, Low: **2** | Yes, later | Open |
| [H-17](#h-17-stage-instructions-arent-read) | Stage instructions aren't read | Considerable, Low: **2** | Considerable, Low: **2** | Considerable, Very low: **2** | Yes, later | Open |
| [H-18](#h-18-a-change-is-recorded-under-the-wrong-persons-name) | A change is recorded under the wrong person's name | Significant, Medium: **2** | Significant, Medium: **2** | Significant, Low: **2** | Yes, later | Open |
| [H-19](#h-19-part-of-the-clinical-record-is-lost-by-deletion) | Part of the clinical record is lost by deletion | Considerable, Low: **2** | Considerable, Low: **2** | Considerable, Very low: **2** | Yes, later | Open |
| [H-20](#h-20-data-is-lost-or-restored-to-an-earlier-point) | Data is lost, or restored to an earlier point | Major, Low: **3** | Major, Very low: **2** | Major, Very low: **2** | Operational, before first live use | Open |
| [H-21](#h-21-work-assigned-to-a-deleted-account-drops-off-every-task-list) | Work assigned to a deleted account drops off every task list | Major, Medium: **3** | Major, Very low: **2** | Major, Very low: **2** | **Yes, before first live use** | Open |
| [H-22](#h-22-care-under-way-is-delayed-because-the-patients-record-is-restricted) | Care under way is delayed because the patient's record is restricted | Considerable, Low: **2** | Considerable, Low: **2** | Considerable, Very low: **2** | Yes, later (a local procedure before first live use) | Open |
| [H-23](#h-23-a-file-is-attached-to-the-wrong-patient-pathway-or-stage) | A file is attached to the wrong patient, pathway or stage | Major, Low: **3** | Major, Very low: **2** | Major, Very low: **2** | **Yes, before first live use** | Open |
| [H-24](#h-24-a-document-or-result-is-missing-and-nobody-knows) | A document or result is missing, and nobody knows | Considerable, Low: **2** | Considerable, Low: **2** | Considerable, Very low: **2** | Yes, later | Open |
| [H-25](#h-25-an-attached-file-carries-malware) | An attached file carries malware | Major, Low: **3** | Major, Very low: **2** | Major, Very low: **2** | **Yes, before first live use** | Open |
| [H-26](#h-26-a-file-is-deleted-by-mistake) | A file is deleted by mistake | Considerable, Low: **2** | Considerable, Low: **2** | Considerable, Very low: **2** | Yes, later | Open |
| [H-27](#h-27-an-out-of-date-or-duplicate-document-is-relied-on) | An out-of-date or duplicate document is relied on | Considerable, Low: **2** | Considerable, Low: **2** | Considerable, Very low: **2** | Yes, later | Open |
| [H-28](#h-28-file-storage-is-unavailable) | File storage is unavailable | Considerable, Low: **2** | Considerable, Low: **2** | Considerable, Very low: **2** | Operational, before first live use | Open |
| [H-29](#h-29-an-admin-relies-on-a-monitoring-rule-to-follow-up-clinical-work) | An admin relies on a monitoring rule to follow up clinical work | Considerable, Low: **2** | Considerable, Low: **2** | Considerable, Very low: **2** | Information before first live use; an outbox later | Open |
| [H-30](#h-30-a-visit-the-patient-needs-doesnt-happen) | A visit the patient needs doesn't happen | Draft: Considerable, Medium: **3** | Draft: Considerable, Low: **2** | Draft: Considerable, Very low: **2** | **Yes, before first live use** | Open, draft for the CSO |
| [H-31](#h-31-a-member-of-staff-is-double-booked) | A member of staff is double-booked | Draft: Considerable, Low: **2** | Draft: Considerable, Low: **2** | Draft: Considerable, Very low: **2** | Yes, before first live use | Open, draft for the CSO |
| [H-32](#h-32-a-stage-is-finished-without-the-visit-it-needed) | A stage is finished without the visit it needed | Draft: Major, Low: **3** | Draft: Major, Very low: **2** | Draft: Major, Very low: **2** | **Yes, before first live use** | Open, draft for the CSO |
| [H-33](#h-33-an-appointment-is-at-the-wrong-time) | An appointment is at the wrong time | Draft: Considerable, Low: **2** | Draft: Considerable, Low: **2** | Draft: Considerable, Very low: **2** | Yes, before first live use | Open, draft for the CSO |
| [H-34](#h-34-the-wrong-patient-or-pathway-is-booked) | The wrong patient or pathway is booked | Draft: Considerable, Medium: **3** | Draft: Considerable, Low: **2** | Draft: Considerable, Low: **2** | **Yes, before first live use** | Open, draft for the CSO |
| [H-35](#h-35-an-appointment-is-left-without-its-member-of-staff) | An appointment is left without its member of staff | Draft: Considerable, Low: **2** | Draft: Considerable, Low: **2** | Draft: Considerable, Very low: **2** | Yes, before first live use | Open, draft for the CSO |
| [H-36](#h-36-a-cancelled-or-moved-appointment-isnt-passed-on-to-the-patient) | A cancelled or moved appointment isn't passed on to the patient | Draft: Considerable, Medium: **3** | Draft: Considerable, Low: **2** | Draft: Considerable, Very low: **2** | **Yes, before first live use** | Open, draft for the CSO |
| [H-37](#h-37-a-note-from-the-notepad-is-added-to-the-wrong-patients-pathway) | A note from the notepad is added to the wrong patient's pathway | Draft: Considerable, Medium: **3** | Draft: Considerable, Low: **2** | Draft: Considerable, Low: **2** | **Yes, built (the confirmation)** | Open, draft for the CSO |
| [H-38](#h-38-a-pathways-note-is-deleted-wrongly) | A pathway's note is deleted wrongly | Draft: Considerable, Low: **2** | Draft: Considerable, Low: **2** | Draft: Considerable, Very low: **2** | Yes, built (admins only, a reason, the confirmation, kept) | Open, draft for the CSO |
| [H-39](#h-39-a-visit-is-recorded-after-the-event-that-didnt-happen-or-on-the-wrong-patient) | A visit is recorded after the event that didn't happen, or on the wrong patient | Draft: Major, Low: **3** | Draft: Major, Very low: **2** | Draft: Major, Very low: **2** | **Yes, built (the patient shown, the 30-day limit, who recorded it)** | Open, draft for the CSO |
| [H-40](#h-40-a-referral-is-lost-or-delayed-before-triage) | A referral is lost or delayed before triage | Draft: Major, Medium: **3** | Draft: Major, Very low: **2** | Draft: Major, Very low: **2** | **Yes, before first live use** (the clock not built: `docs/centre-phase2-plan.md`) | Open, draft for the CSO |
| [H-41](#h-41-a-referral-is-triaged-into-the-wrong-pathway-priority-or-patient) | A referral is triaged into the wrong pathway, priority or patient | Draft: Major, Low: **3** | Draft: Major, Very low: **2** | Draft: Major, Very low: **2** | **Yes, before first live use** (built, to verify) | Open, draft for the CSO |
| [H-42](#h-42-a-paused-waiting-time-clock-is-forgotten) | A paused waiting-time clock is forgotten | Draft: Considerable, Medium: **3** | Draft: Considerable, Low: **2** | Draft: Considerable, Low: **2** | **Yes, before first live use** (not built) | Open, draft for the CSO |
| [H-43](#h-43-a-breach-of-a-waiting-time-target-goes-unnoticed-or-the-target-is-wrong) | A breach of a waiting-time target goes unnoticed, or the target is wrong | Draft: Considerable, Medium: **3** | Draft: Considerable, Low: **2** | Draft: Considerable, Low: **2** | **Yes, before first live use** (not built) | Open, draft for the CSO |
| [H-44](#h-44-a-duplicate-referral-leads-to-two-pathways-for-one-need) | A duplicate referral leads to two pathways for one need | Draft: Significant, Medium: **2** | Draft: Significant, Low: **2** | Draft: Significant, Low: **2** | Yes, before first live use (not built) | Open, draft for the CSO |
| [H-45](#h-45-a-specimen-is-lost-or-its-report-never-comes-back) | A specimen is lost, or its report never comes back | Draft: Major, Medium: **3** | Draft: Major, Very low: **2** | Draft: Major, Very low: **2** | **Yes, with the feature** (PR 13: built on branch `feature/specimens`, to be verified by the CSO; Phase 4's service alert later) | Open, draft for the CSO |
| [H-46](#h-46-a-result-is-never-seen-or-acted-on) | A result is never seen or acted on | Draft: Major, Medium: **3** | Draft: Major, Very low: **2** | Draft: Major, Very low: **2** | **Yes, with the feature** (PR 14; not built: `docs/centre-phases-3-5-plan.md`) | Open, draft for the CSO |
| [H-47](#h-47-a-report-is-attached-to-the-wrong-patient-or-specimen) | A report is attached to the wrong patient or specimen | Draft: Major, Low: **3** | Draft: Major, Very low: **2** | Draft: Major, Very low: **2** | **Yes, with the feature** (PRs 13 and 14: PR 13's built on branch `feature/specimens`, to be verified by the CSO; "entered in error sends it back" waits for PR 14) | Open, draft for the CSO |
| [H-48](#h-48-a-letter-goes-to-the-wrong-practice-or-person) | A letter goes to the wrong practice or person | Draft: Considerable, Medium: **3** | Draft: Considerable, Low: **2** | Draft: Considerable, Low: **2** | **Yes, with the feature** (PRs 12 and 16: built) | Open, draft for the CSO |
| [H-49](#h-49-a-letter-isnt-sent-or-goes-late) | A letter isn't sent, or goes late | Draft: Considerable, Medium: **3** | Draft: Considerable, Low: **2** | Draft: Considerable, Low: **2** | **Yes, with the feature** (PR 16: built) | Open, draft for the CSO |
| [H-50](#h-50-a-letter-shows-another-patients-details-or-an-unapproved-version-is-sent) | A letter shows another patient's details, or an unapproved version is sent | Draft: Major, Low: **3** | Draft: Major, Very low: **2** | Draft: Major, Very low: **2** | **Yes, with the feature** (PRs 15 and 16: built) | Open, draft for the CSO |
| [H-51](#h-51-a-branch-is-chosen-or-a-stage-skipped-in-error) | A branch is chosen, or a stage skipped, in error | Draft: Major, Low: **3** | Draft: Major, Very low: **2** | Draft: Major, Very low: **2** | **Yes, built** (PR 20, `feature/branching-pathways`: the confirmation, none pre-selected, reasons, reinstating, the audit, the template checks) | Open, draft for the CSO |
| [H-52](#h-52-a-patient-is-booked-when-nobody-or-no-room-is-available-or-a-cancelled-clinics-patients-arent-told) | A patient is booked when nobody, or no room, is available, or a cancelled clinic's patients aren't told | Draft: Considerable, Medium: **3** | Draft: Considerable, Low: **2** | Draft: Considerable, Low: **2** | **Yes, with the feature** (PRs 21 to 23 and 26; not built: `docs/centre-phases-3-5-plan.md`) | Open, draft for the CSO |
| [H-53](#h-53-a-reminder-reaches-the-wrong-person-or-says-too-much) | A reminder reaches the wrong person or says too much | Draft: Significant, Medium: **2** | Draft: Significant, Low: **2** | Draft: Significant, Low: **2** | Yes, with the feature (PR 26; built, to verify) | Open, draft for the CSO |
| [H-54](#h-54-a-patient-relies-on-a-reminder-that-isnt-sent) | A patient relies on a reminder that isn't sent | Draft: Considerable, Low: **2** | Draft: Considerable, Low: **2** | Draft: Considerable, Low: **2** | Yes, with the feature (PRs 19 and 26; built, to verify) | Open, draft for the CSO |
| [H-55](#h-55-a-missed-appointment-isnt-followed-up) | A missed appointment isn't followed up | Draft: Considerable, Medium: **3** | Draft: Considerable, Low: **2** | Draft: Considerable, Low: **2** | **Yes, with the feature** (PR 26; built, to verify) | Open, draft for the CSO |
| [H-56](#h-56-a-scheduled-job-stops-and-nobody-notices) | A scheduled job stops and nobody notices | Draft: Considerable, Medium: **3** | Draft: Considerable, Low: **2** | Draft: Considerable, Low: **2** | **Yes, with the feature** (PR 19; not built: `docs/centre-phases-3-5-plan.md`) | Open, draft for the CSO |
| [H-57](#h-57-a-record-is-anonymised-too-early-because-its-retention-class-is-wrong) | A record is anonymised too early because its retention class is wrong | Draft: Considerable, Low: **2** | Draft: Considerable, Very low: **2** | Draft: Considerable, Very low: **2** | Yes, with the feature (PR 27; built) | Open, draft for the CSO |
| [H-58](#h-58-an-mdt-outcome-is-recorded-on-the-wrong-patient-or-not-acted-on) | An MDT outcome is recorded on the wrong patient, or not acted on | Draft: Major, Low: **3** | Draft: Major, Very low: **2** | Draft: Major, Very low: **2** | **Yes, with the feature** (PR 30, built: `feature/mdt-list`) | Open, draft for the CSO |
| [H-59](#h-59-a-procedure-goes-ahead-without-valid-consent-recorded-or-a-withdrawal-is-missed) | A procedure goes ahead without valid consent recorded, or a withdrawal is missed | Draft: Major, Low: **3** | Draft: Major, Very low: **2** | Draft: Major, Very low: **2** | **Yes, with the feature** (PR 31: built) | Open, draft for the CSO |
| [H-60](#h-60-questionnaire-answers-are-read-as-an-assessment-or-recorded-on-the-wrong-patient) | Questionnaire answers are read as an assessment, or recorded on the wrong patient | Draft: Considerable, Low: **2** | Draft: Considerable, Very low: **2** | Draft: Considerable, Very low: **2** | Yes, with the feature (PR 32; not built) | Open, draft for the CSO |
| [H-61](#h-61-a-video-link-reaches-the-wrong-patient) | A video link reaches the wrong patient | Draft: Significant, Low: **2** | Draft: Significant, Very low: **1** | Draft: Significant, Very low: **1** | Yes, with the feature (PR 34; not built) | Open, draft for the CSO |
| [H-62](#h-62-data-from-an-nhs-system-is-matched-to-the-wrong-patient) | Data from an NHS system is matched to the wrong patient | Draft: Major, Low: **3** | Draft: Major, Very low: **2** | Draft: Major, Very low: **2** | **Yes, with the feature** (PRs 38a to 38c; not built: `docs/centre-phases-3-5-plan.md`) | Open, draft for the CSO |
| [H-63](#h-63-one-organisation-sees-anothers-patients) | One organisation sees another's patients | Draft: Considerable, Low: **2** | Draft: Considerable, Very low: **2** | Draft: Considerable, Very low: **2** | Yes, with the feature (PRs 40a to 40c; not built) | Open, draft for the CSO |
| [H-64](#h-64-a-file-is-held-as-being-scanned-when-its-needed) | A file is held as "being scanned" when it's needed | Draft: Considerable, Low: **2** | Draft: Considerable, Very low: **2** | Draft: Considerable, Very low: **2** | Yes, with the feature (PR 37; not built) | Open, draft for the CSO |

**Initial ratings, at `88dc1fa`:** one hazard is rated 4 (H-13), and twelve are rated 3 (H-01, H-02, H-04, H-05, H-07, H-09, H-10, H-12, H-14, H-15, H-20 and H-21). None is rated 5. Since then, H-12's defect is fixed (PR #62), which brings it to its residual 2 once the CSO has verified the fix, leaving eleven at 3.

**File attachments** (version 0.7) add six hazards, rated with the controls on the `feature/file-attachments` branch: two at 3, a file on the wrong record (H-23) and malware in a file (H-25), and four at 2 (H-24, H-26, H-27 and H-28). That makes fourteen at 3 on the initial ratings, thirteen with H-12's fix. Both new ones come down to 2 at first live use, H-25 only if malware scanning is built before then.

**At first live use,** with every "before first live use" control built: none is above 3, and three stay at 3 (H-07, H-10 and H-13). Under the plan's go-live rule, each of those three needs the CSO's written justification before first live use. H-13 gets to 3 only because its approval step is brought forward to before first live use.

**The patient banner,** the main control for H-01 and H-02, and for files on the wrong record (H-23), is built on branch `feature/nhs-integrations`, with the NHS number. It doesn't change any rating yet: the initial ratings are for `88dc1fa`, and the "at first live use" ratings already assumed it. Those two ratings now depend on the CSO verifying the banner, and on the other controls marked "before first live use" (the date of birth in lists, the patient's name in the page title and in the confirmations), which aren't built.

**Residual,** with every control built: H-13 stays at 3, because the worst credible harm of a wrong template, affecting many patients, stays catastrophic however unlikely it's made. All the others come down to 2.

**The notepad and several notes per pathway (H-37)** is a draft for the CSO too, written with the feature (branch `feature/procedure-notes`), with a draft estimate in the same way: 3 to start, 2 with its confirmation, which is built.

**Deleting pathway notes (H-38)** is a draft for the CSO, written with the feature (branch `feature/note-deletion`): admins delete notes (soft, with a reason), and staff ask them to. Draft estimate: 2 to start, as its controls (admins only, a reason, a confirmation showing the patient and the note, the note kept and recoverable) are built with it.

**Closed days and appointments recorded after the event (H-39)** (branch `feature/closed-days-walk-ins`): a new draft hazard, H-39 (a visit recorded after the event that didn't happen, or on the wrong patient), estimated at 3 to start and 2 with its built controls; and closed days' and walk-ins' effect on H-31 to H-35's causes and controls. No earlier draft estimate changed.

**Referrals, triage and the waiting-time clock (H-40 to H-44)** are drafts written from the design of Phase 2 of the community specialist centre (`docs/centre-phase2-plan.md`), before anything was built, so the build includes their controls. **Referrals and triage are built** (branches `feature/referrals`, PR 5, and `feature/triage`, PR 6): H-40, H-41 and H-44 now list the controls they built as existing ones, for the CSO to verify, and keep the rest (the clock, PR 7) as planned. H-42 and H-43 have no existing control yet. Draft estimates: four start at 3 (H-40 to H-43) and all come down to 2 at first live use once their planned controls are built. H-01 gains registering a referral on the wrong patient as a cause.

**The centre's Phases 3 to 5 (H-45 to H-64)** are drafts written from the design of Phases 3, 4 and 5 of the community specialist centre (`docs/centre-phases-3-5-plan.md`), **before anything is built**, so each feature's PR builds their controls with it: H-45 to H-50 for specimens, results and letters (Phase 3), H-51 to H-57 for branching pathways, hours and clinics, reminders, scheduled jobs and retention classes (Phase 4), and H-58 to H-64 for the MDT list, consent, questionnaires, video appointments, NHS adapters, several organisations and malware scanning (Phase 5). None had an existing control when drafted. **Specimens are built** (branch `feature/specimens`, PR 13): H-45 and H-47 now list the controls it built as existing ones, for the CSO to verify, and keep the rest (results sign-off, PR 14; Phase 4's service alert) as planned; H-01's specimen label is built. Draft estimates: 13 start at 3 (H-45, H-46, H-47, H-48, H-49, H-50, H-51, H-52, H-55, H-56, H-58, H-59 and H-62), and all come down to 2 or below at the feature's first live use, once its planned controls are built; no feature may be used with patients before then. H-01 gains the specimen label as a cause and a control. **Patient reminders and the Did not attend view are built** (PR 26, `feature/patient-reminders`): H-53 to H-55 list the controls it built as existing ones, for the CSO to verify, and H-30, H-36 and H-52 gain them where they help. No draft estimate changed: H-55's initial estimate of 3 assumed none of its controls, and its estimate at first live use (2) already assumed them.

**Staff calendars and appointments (H-30 to H-36)** are drafts for the CSO, written from the design (`docs/calendar-plan.md`) while the feature was being built, and not yet checked against the code. Their ratings are left for the CSO to assign; each entry gives a draft estimate, marked "Draft", which the counts above don't include. On those estimates, four start at 3 (H-30, H-32, H-34 and H-36), and all come down to 2 at first live use once their "before first live use" controls are built, so none would need a justification at 3 under the go-live rule.

---

## H-01: The wrong patient's pathway is worked

**Description.** A member of staff opens, and records work on, a pathway that belongs to a different patient from the one in front of them, believing it's the right one.

**Causes:**
- Patients are identified by first name, last name, date of birth and email only. There's no NHS number or local hospital number, and nothing stops two patients with the same name and date of birth (`MedApp.Domain/Patients/Patient.cs`, `CreatePatientHandler`). **Since branch `feature/nhs-integrations`,** a patient can have an NHS number, optional and unique, so two records with the same number are refused; but two patients with the same name and date of birth, or one patient recorded twice with the number on only one record, still aren't caught. There's still no local hospital number.
- The pathway page shows the patient's name only, in the "Patient info" part, which can be folded away. It doesn't show the date of birth. The page's title is the pathway's title, not the patient's name (`MedApp.Web/src/pages/pathways/PathwayPage.tsx`, `details()`). Branch `feature/nhs-integrations` adds the patient banner above it (further controls); the title is unchanged.
- **A wrong NHS number** (branch `feature/nhs-integrations`): one mistyped, or the right digits typed on the wrong patient's record. The Modulus 11 check digit catches any single mistyped digit and most swapped pairs, but not a valid number that belongs to someone else. Nothing traces numbers with PDS, so MedApp never confirms that a number is the patient's (the FHIR API says so: "02 Number present but not traced"). The banner then shows a precise-looking identifier that's wrong, and another system reading the patient through the FHIR API could match them to the wrong person.
- **The demographics lookup** fills a patient's name and date of birth from an NHS number, so a wrong number can fill in another person's details. Today it's off by default, and its only source is an offline stub of four synthetic people; the real PDS isn't connected. Since the review of #69: on Edit Patient it shows who it found beside the record's details and fills only on "Use These Details"; on Add Patient it fills at once; and on either, if the number is changed after a lookup while the filled details are unchanged, the save is refused until the new number is looked up or the details are changed by hand.
- Task lists and pathway lists show the patient's name only (`MyTasksPage.tsx`, `PathwaysListPage.tsx`). Search matches on part of a name (`PathwaySearch.Filter`).
- Two pages can be open side by side (half views), and pathways can be pinned in the side navigation.
- **A specimen labelled for the wrong patient** (built on branch `feature/specimens`, `docs/centre-phases-3-5-plan.md`, PR 13): the label is printed from the specimen's page, and a label stuck on another patient's pot sends their report to this patient's record (H-47).
- The same causes can put a file on the wrong patient: H-23.
- **A referral registered on the wrong patient** (built on branch `feature/referrals`, `docs/centre-phase2-plan.md`): the referral's patient is found by NHS number or name when it's registered, or added first (Add Patient, with the demographics lookup). Its controls are under H-41: built, the patient banner on the Register Referral page once the patient is chosen, on the referral page and in the withdraw and entered-in-error confirmations, and marking a wrong one entered in error with a detail; built with triage (PR 6), the banner in every triage confirmation.

**Effect.** Notes are written on the wrong patient's pathway, or a stage is finished on it, and the other patient's pathway is left undone (see also H-02). If staff act on what they read, a step could be done, or skipped, for the wrong patient.

**Possible harm.** A step of care given to or withheld from the wrong patient, for example a pre-procedure check recorded as done for a patient who didn't have it. One patient at a time; Major in the worst credible case (death or permanent incapacity, single patient).

**Existing controls:**
- New pathways are created from a patient's own page, and the New pathway page says which patient it's for ("For {name}", `NewPathwayPage.tsx`).
- The patients list shows date of birth and age, and View Patient shows the full record (`PatientsListPage.tsx`, `ViewPatientPage.tsx`).
- A pathway can't be moved to another patient: the update command has no patient field (`UpdatePatientPathwayCommand`).
- Every change is audited with who and when, so a wrong-patient entry can be found and traced (`PathwayChange.Audit`).
- **Transferred:** positive patient identification at the point of care by the organisation's own process.

**Initial risk:** Major, Low: **3**.

**Further controls:**
- **A patient banner** on every pathway page and dialog that changes a pathway: full name, date of birth and age, and from step 9 the NHS number, always visible, not foldable. Protective; **built** on branch `feature/nhs-integrations` (`PatientBanner`): `SURNAME, Forename` in bold, the date of birth with the age, and the NHS number as `XXX XXX XXXX` (or "No NHS number"), with tokens for a restricted, disputed or anonymised record; not foldable, under the page header of the patient's page, Edit Patient, the export page, the patient's pathways, the pathway page, Edit Pathway and New Pathway; it stays in view on every one but the printable export page (the forms scroll inside their cards under it), wraps in a narrow window or half view, is a region named for the patient, upper-cases the surname by CSS so screen readers read it as a name, and gives young children's ages in months, weeks or days. Edit Patient shows it from the saved record, and its save confirmation shows the old and new name, date of birth and NHS number when they change. A test renders every patient and pathway route and checks for it. Not shown: sex or gender, which MedApp doesn't hold yet (`docs/todo.md`). Pathway responses carry the patient, so the pathway pages need no second read to show it. **Before first live use,** to be verified by the CSO. A dialog over the page can hide the banner, so dialogs that change a pathway rely on naming the patient themselves (H-02's confirmations).
- **Show the date of birth** beside the patient's name in task lists and pathway lists, and put the patient's name in the pathway page's title. Protective; **proposed**; before first live use. Not done on branch `feature/nhs-integrations`.
- **NHS number** (validated, unique within an organisation) and **a duplicate check** when a patient is added. Design. **Built in part** on branch `feature/nhs-integrations`: the number is optional, checked with its check digit on the server and in the form, unique across the deployment (within an organisation once step 8 adds them), and searchable; an exact duplicate number is refused with a 409. **Still planned** (step 9): a duplicate check on name and date of birth, and merging; later.
- **Trace NHS numbers with PDS,** so a number is confirmed to be the patient's, and have the lookup show who it found and ask staff to confirm before it fills the form. Protective. The confirmation is **built** on Edit Patient, with the hold on a changed number on both forms (above); Add Patient still fills at once. Tracing is **planned** (step 9, after NHS England's onboarding); before the real PDS is connected, Add Patient should confirm as Edit Patient does.
- **The specimen label** carries the patient's name, date of birth and NHS number beside the specimen number (as text and as a QR code holding only the number), so the pot can be checked against the patient when it's taken and by the lab (`docs/centre-phases-3-5-plan.md`, decision 87). Protective; **built** on branch `feature/specimens` (`/specimens/{id}/print`, `SpecimenPrintPage`; the QR code drawn on the server by `IQrCodeRenderer`), to be verified by the CSO; before specimens are first used. Its paper size is A4 today, not a label printer's (`docs/todo.md`). Checking the label against the patient stays a person's job (transferred). See H-47.
- **Train staff** to check the banner against the patient before recording work. Information; transferred.

**At first live use:** Major, Very low: **2**. Unchanged by branch `feature/nhs-integrations`: this rating already assumed the banner, and it holds only once the CSO has verified it and the proposed controls above are built.

**Residual risk:** Major, Very low: **2**. The NHS number makes two patients harder to confuse, but adds the wrong-number cause above, which tracing with PDS controls; on balance the rating is unchanged.

**Owner:** [CSO]. **Status:** Open: the banner is built and awaits the CSO's verification; the other controls needed before first live use aren't built.

---

## H-02: Work is recorded on the wrong record

**Description.** Information meant for one pathway, stage or patient is saved on another: a pathway is created for the wrong patient, notes are typed into the wrong stage or pathway, or the wrong template is chosen.

**Causes:**
- The same identification limits as H-01.
- Half views put two pathways side by side.
- Files attached to the wrong record are H-23.
- The notes box always belongs to the current stage, so a note about an earlier stage can only be added to the current one, unless an admin unlocks the earlier stage.
- Template names can be similar, and the template list is the organisation's own.

**Effect.** The right record lacks the information and the wrong record carries it. Someone reading either can act on the wrong information.

**Possible harm.** A delayed or wrong step of care for one patient. Considerable.

**Existing controls:**
- New pathway lists the chosen template's stages before it's created, and fills the title from the template's name (`NewPathwayPage.tsx`; `docs/pathways-ui-plan.md`, phase 4).
- A pathway created for the wrong patient can be cancelled with a reason and archived, and an admin can then delete it; its audit entries are kept (`PathwayWorkflow.AllowedStatusChanges`, `CanBeDeleted`; `DeletePatientPathwayHandler`).
- Notes on a stage can be corrected while it's current, and after an admin unlocks it; every save keeps the old and new text in the audit log (`PathwayChange.UpdateStageNotes`).
- A finished stage is locked (`PathwayChange.FinishStage`), so a wrong entry can't be made on an earlier stage by accident.

**Initial risk:** Considerable, Medium: **3**.

**Further controls:**
- **The patient banner** from H-01. Protective; **built** on branch `feature/nhs-integrations`, on the pathway page, Edit Pathway and New Pathway, and Edit Patient (so a pathway is created under the patient's banner); before first live use, to be verified by the CSO.
- **The pathway and stage named** in the Save Notes and Finish Stage confirmations ("Save notes on {stage} of {pathway} for {patient}?"). The finish confirmation already names the stage (`PathwayPage.tsx`); add the patient. Protective; **proposed**; before first live use. Not done on branch `feature/nhs-integrations`.
- **An "entered in error" action** for stage notes and pathways, which strikes the entry through, keeps it in the record and asks for a reason, instead of only overwriting or deleting. Design; **proposed**; later.
- **Train staff** to correct a wrong entry with a note saying so, never by deleting it. Information; transferred.

**At first live use:** Considerable, Low: **2**. As for H-01, this already assumed the banner, and holds once the CSO has verified it and the confirmations name the patient.

**Residual risk:** Considerable, Low: **2**.

**Owner:** [CSO]. **Status:** Open: the banner is built and awaits the CSO's verification; the confirmations aren't changed yet.

---

## H-03: A patient's details are changed or deleted without trace

**Description.** A patient's name or date of birth is changed in error (for example, the wrong patient edited), or a patient is deleted, and nothing records what it was before or who did it.

**Causes:**
- Any staff member can edit any patient, and the change overwrites the old values (`UpdatePatientHandler`).
- Any staff member can permanently delete a patient who has no pathways (`DeletePatientHandler`).
- Since roadmap step 7 (branch `feature/privacy-features`), both are recorded in the activity log, with who, when and which fields changed, but **not the old values** (the log holds no patient details by design), so what a field said before can't be recovered from it.

**Effect.** All of that patient's pathways now show the new, wrong details. An admin can see who changed which fields and when (the activity log, and the patient's access report), but not what they said before. A deleted patient's record is gone.

**Possible harm.** Wrong-patient errors (H-01) on every pathway of that patient; a patient lost from follow-up. One patient; Considerable.

**Existing controls:**
- A patient with pathways can't be deleted (409; `DeletePatientHandler`).
- Pathway audit entries keep the patient's ID, so pathways can still be traced to the patient row.
- Patient creates, edits (the fields changed) and deletes are recorded in the activity log with the user, time and address, in the same save as the change (`UpdatePatientHandler`, `DeletePatientHandler`, `PatientRepository`; step 7).
- A restricted record can't be edited or deleted, nor its pathways changed but for blocking or cancelling (`PatientRules`; step 7; its own hazard is H-22), and a disputed one carries a banner on every page about the patient (the patient's page, Edit Patient, their pathways, the pathway page, Edit Pathway, New Pathway and the appointment dialogs). Since branch `feature/warning-banners` it's a label, "Disputed record: <what's disputed>" when the admin's words fit (40 characters) and "Disputed record" otherwise, with what the patient disputes and since when over the page on hover, focus, a click or a tap. So what's disputed (perhaps an identifier staff check, H-01) may now be on demand rather than on screen.
- Two saves of the same patient at once can't silently undo each other: the second, made from an older copy, is refused (a row version, step 7).

**Initial risk:** Considerable, Low: **2**.

**Further controls:**
- **Audit patient changes** (create, edit, delete). Protective; **built** (step 7) for who, when and which fields. **Still planned:** keeping the old values (a version table, step 7); later. Rated 2 today, but the CSO may bring it forward.
- **Make deleting a patient admin-only.** Protective; **planned** (step 2); later.
- **Show "details changed on {date}"** on the patient banner for a recent change. Protective; **proposed**; later.

**At first live use:** Considerable, Low: **2**.

**Residual risk:** Considerable, Very low: **2**.

**Owner:** [CSO]. **Status:** Open.

---

## H-04: An overdue pathway is missed

**Description.** A pathway passes its target date, and nobody notices in time to act.

**Causes:**
- **Nothing tells anyone.** "Overdue" is a badge on lists and on the pathway page (`DueDateCell`, `PathwayCells.tsx`), computed when the page loads (`PathwayWorkflow.IsOverdue`). There's no notification, email or escalation for overdue work; the only notification is for being assigned (`docs/todo.md`, "More notifications").
- **My Worklist opens on work not yet started.** Its default filter is "Not started" (`MyTasksPage.tsx`, `progressOf`), which sends `Started: false`, so every pathway with a started or finished stage is left out, overdue or not (`PathwaySearch.Filter`). Started work shows only after changing the filter, or through the "Overdue" quick filter.
- **Unassigned pathways are on nobody's task list.** My Worklist shows only pathways assigned to you (`PathwaySearch.Filter`, `AssignedToMe`). An unassigned pathway is visible only on the full Pathways list.
- **Assignment depends on admins:** only admins can assign (`[Authorize(Roles = "Admin")]` on the assignment endpoints), so if no admin is about, new pathways stay unassigned.
- **The assignee is away:** assignment doesn't stop anyone else working on it, but nothing reassigns it or tells anyone it's waiting. (An assignee whose account is deleted is H-21.)
- **Pages don't refresh by themselves:** a list left open overnight keeps its old overdue badges (H-16).

**Effect.** A step of care isn't done by the date staff set for it.

**Possible harm.** Delayed care for one patient: a delayed test, review or pathway. Considerable in the worst credible case for the pathways MedApp is meant for; it could be worse for a time-critical one, which is outside the intended purpose.

**Existing controls:**
- The overdue rule is the same everywhere, in one place (`PathwayWorkflow.IsOverdue`, and the same rule in SQL for the "overdue only" filter, `PathwaySearch.Filter`), with tests (`PathwayWorkflowTests`, `IsOverdue_StatusAndDueDate_OnlyOpenPathwaysPastTheirDueDate`).
- Lists sort by target date, soonest first, by default, and hide closed pathways (`PathwaySearch.Sort`, `Filter`).
- The Pathways list has an "overdue only" filter, and its searches can be saved as favourites.
- My Worklist has an "Overdue" quick filter, which shows started and not-started work, and highlights overdue pathways (`MyTasksPage.tsx`).
- Target dates can't be set in the past when a pathway is created (`CreatePatientPathwayValidator`).

**Initial risk:** Considerable, Medium: **3**.

**Further controls:**
- **Overdue notifications** to the assignees, and to admins for unassigned pathways, the day a pathway becomes overdue, through `INotificationSender`. Protective; **planned** (`docs/todo.md`; step 9 adds email); **before first live use** for the in-app notification. The intended purpose already allows workflow reminders about dates staff set (`intended-purpose-and-device-decision.md`); when this is built, update the statements that "nothing alerts anyone" in CS-03 and CS-05 in the same pull request.
- **A visible count of started and overdue work on My Worklist** beside its filter, or opening My Worklist on all open work, so the list staff are told to work from shows everything assigned to them. Protective; **proposed**; before first live use.
- **An "unassigned and due soon" view** on the Welcome page or My Worklist for admins, so nothing waits unseen for assignment. Protective; **proposed**; before first live use.
- **A daily digest or escalation to a lead.** Protective; **planned** (step 9); later.
- **A local process:** who reviews the overdue list, and how often; at least two admins so assignment never waits on one person. Information; transferred.

**At first live use:** Considerable, Low: **2**.

**Residual risk:** Considerable, Low: **2**.

**Owner:** [CSO]. **Status:** Open.

---

## H-05: A blocked pathway is forgotten

**Description.** A pathway is blocked (waiting on something outside MedApp), and stays blocked long after the reason has gone, or past the date it was needed.

**Causes:**
- **A blocked pathway is never overdue,** by design: "Blocked pathways are waiting on something else, so never overdue" (`PathwayWorkflow.IsOverdue`; `docs/pathways-ui-plan.md`, decision 15). Its target date can pass with no badge.
- A block has a reason but no review date, and nothing reminds anyone about it.
- The "overdue only" filter leaves blocked pathways out (`PathwaySearch.Filter`).
- **My Worklist opens on "Not started"** (H-04), so a blocked pathway that had a stage started before it was blocked doesn't show there by default.

**Effect.** The pathway drops out of everyone's attention, and the patient's care stalls.

**Possible harm.** Delayed care for one patient. Considerable.

**Existing controls:**
- Blocking needs a reason, kept in the audit log (`PathwayWorkflow.RequiresReason`, `ChangePatientPathwayStatusValidator`).
- Blocked pathways are among My Worklist's statuses (`OPEN_PATHWAY_STATUSES`, `MedApp.Web/src/pages/listSearch.ts`): they show by default only if no stage was started, and otherwise under the "Started" filter or the "Blocked" quick filter. Their status shows on every list.
- The pathway page says what status it was blocked from (`PathwayPage.tsx`).

**Initial risk:** Considerable, Medium: **3**.

**Further controls:**
- **Show "past target date" on blocked pathways** (a separate, quieter badge than overdue), and include them in an "overdue or past due" filter. Protective; **proposed**; before first live use.
- **The count of started work on My Worklist** from H-04. Protective; **proposed**; before first live use.
- **A review date on each block,** asked for when blocking, with a notification when it arrives. Protective; **proposed**; later.
- **Notify the assignees when a pathway is blocked.** Protective; **planned** (`docs/todo.md`); later.
- **A local process** for reviewing blocked pathways. Information; transferred.

**At first live use:** Considerable, Low: **2**.

**Residual risk:** Considerable, Low: **2**.

**Owner:** [CSO]. **Status:** Open.

---

## H-06: A stage is finished in error

**Description.** Someone finishes a stage that isn't complete, or the wrong stage, by mistake.

**Causes:**
- A slip: Finish Stage is the main button beside Save Notes (`StageWork.tsx`).
- The wrong pathway (H-01, H-02).

**Effect:**
- The stage locks, and the next stage becomes the current one, so the pathway appears to have moved on. Staff picking up the next stage may assume the earlier one was done.
- Only an admin can unlock it; staff can't. After an unlock, the stage still counts as finished (`IsCompleted` stays true): unlocking reopens its notes, but the record still says it was finished, by whom and when (`PathwayChange.UnlockStage`). There's no way to mark a stage "not done".
- **Work carries on past an unlocked stage.** Because the unlocked stage still counts as finished, the current stage doesn't move back to it (`PathwayWorkflow.CurrentStage` skips finished stages), so later stages can be worked and finished while the earlier one is reopened. For example, the pathway stage can be finished while a consent stage is unlocked for correction, and the record shows consent as finished throughout.

**Possible harm.** A step relied on that wasn't done, for one patient. Considerable.

**Existing controls:**
- Finishing asks for confirmation, naming the stage: "The stage will be locked and the pathway moves on to the next one. An admin can unlock it if it needs correcting." (`PathwayPage.tsx`, the `finish` dialog).
- Only the current stage can be finished, and stages are worked strictly in order while none is unlocked, checked by the API (`PathwayWorkflow.WhyStageCantBeEdited`; `FinishPathwayStageHandler` returns 409 otherwise). Tested in `PathwayWorkflowTests` and `FinishPathwayStageHandlerTests`.
- The finish, any unlock, and a later finish are audited (`PathwayChange.FinishStage`, `UnlockStage`).
- An admin can unlock a finished stage while the pathway is open or completed; a completed or in-review pathway goes back to in progress (`PathwayWorkflow.CanUnlockStage`, `PathwayChange.UnlockStage`).
- A pathway can't be completed while any stage is unlocked (`PathwayWorkflow.AllStagesFinished`; `ChangePatientPathwayStatusHandler`).

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
- **Finishing needs nothing.** A stage can be finished with empty notes (`FinishPathwayStageValidator` checks only the IDs), and stages have no structured fields to say what was checked.
- Staff finish stages to keep the list tidy, or to move on to the stage they need.
- The stage's instructions weren't read (H-17).

**Effect.** Everyone after relies on a record that says the stage was done. For a stage such as consent or a pre-procedure check, the next steps go ahead on that basis.

**Possible harm.** A pathway carried out without a required step, such as a check or consent. One patient; Major in the worst credible case.

**Existing controls:**
- The finish confirmation (H-06).
- Who finished each stage, and when, is shown on the stage and kept in the audit log (`StageList.tsx`; `PathwayChange.FinishStage`), so it's accountable.
- **Since staff calendars, for a stage that involves a visit:** a stage whose definition requires a visit can't be finished until an appointment for it is Attended, unless an admin finishes it with a reason; a suggested visit asks for confirmation (`PathwayWorkflow.WhyStageNeedsVisit`; `docs/calendar-plan.md`, decisions 24 and 25). How that rule can fail is H-32.
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

## H-08: A pathway is cancelled, completed or archived in error

**Description.** A pathway's status is changed by mistake, or for the wrong pathway.

**Causes:**
- A slip, or the wrong pathway (H-01).
- Any staff member can block, cancel, complete or archive, whether or not it's assigned to them; the "not assigned to you" question isn't asked for status changes, only their own confirmation (`PathwayPage.tsx`, the status buttons).

**Effect:**
- **Cancelled:** can't be reopened. The only way on is archiving (`PathwayWorkflow.AllowedStatusChanges`), so the pathway has to be created again, and its earlier stages' work is on the old one.
- **Archived:** can't be reopened, and is hidden from lists unless asked for (`PathwaySearch.Filter`).
- **Completed:** drops off task lists. An admin can bring it back by unlocking a stage.

Each removes the pathway from the open lists, where it would otherwise be seen.

**Possible harm.** Remaining care not given, for one patient. Considerable.

**Existing controls:**
- Each change asks for confirmation; cancelling is shown as destructive and needs a reason, saying "A cancelled pathway can only be archived after this" (`StatusChangeDialog.tsx`).
- A pathway can only be completed from in review, once every stage is finished and locked (`ChangePatientPathwayStatusHandler`).
- Every status change is audited with from, to, reason and whether it was automatic (`PathwayChange.SetStatus`).

**Initial risk:** Considerable, Low: **2**.

**Further controls:**
- **Let an admin reopen a cancelled or archived pathway,** with a reason, audited. Design; **proposed**; later.
- **Ask the "not assigned to you" question before status changes too.** Protective; **proposed**; later.

**At first live use:** Considerable, Low: **2**.

**Residual risk:** Considerable, Very low: **2**.

**Owner:** [CSO]. **Status:** Open.

---

## H-09: A member of staff doesn't learn of work assigned to them

**Description.** A member of staff is assigned a pathway, but doesn't learn of it in time.

**Causes:**
- **Live delivery is best effort.** A failed push is only logged (`SignalRNotificationPublisher`).
- **The browser stops reconnecting** after about 42 seconds offline (attempts at 0, 2, 10 and 30 seconds), until the page is reloaded (`MedApp.Web/src/app/LiveNotifications.tsx`, `withAutomaticReconnect()`; `docs/architecture.md`, risk R11).
- **No email:** notifications are in-app only. Someone who isn't signed in learns of it only at their next sign-in.
- **My Worklist hides it if it's already under way.** My Worklist opens on "Not started" (H-04), so a pathway handed over part-way through, for example from a colleague who's off sick, doesn't show there by default. The colleague who opens My Worklist sees "Nothing waiting to be started".
- **Nothing tells someone they've been unassigned,** and nothing tells anyone about a pathway assigned to someone who's away.
- **The person has switched assignment notifications off** (Account Settings, Notifications; `NotificationPreferences`). Then nothing is stored or pushed, and with My Worklist on its default filter, work handed over part-way through shows nowhere.

**Effect.** Work waits until the person next opens MedApp and finds it, or longer.

**Possible harm.** Delayed care for one patient. Considerable.

**Existing controls:**
- Every notification is stored before it's pushed, so it shows on the bell at the next page load (`NotificationSender`), **unless the person has switched assignment notifications off**, when none is stored.
- The switch's own help text says to work from My Worklist with its filter on all open work, so work already under way shows (`NotificationSettings.tsx`).
- My Worklist lists every open pathway assigned to you once its filter is set to "Started" or to all (`MyTasksPage.tsx`); by default it shows only those not yet started.
- With two copies of the API, notifications reach browsers on either, through the Valkey backplane (`SignalRBackplane`; 8 of 8 delivered in testing, `docs/architecture.md`, section 6.4).
- When the connection comes back, the list is refreshed (`connection.onreconnected`).

**Initial risk:** Considerable, Medium: **3**.

**Further controls:**
- **The count of started work on My Worklist,** or opening it on all open work, from H-04. Protective; **proposed**; before first live use.
- **Information:** tell users that notifications are a convenience, and that My Worklist, with its filter on all open work, is the list to work from. Information; **proposed**; before first live use (it costs nothing); transferred.
- **Keep reconnecting** with backoff for as long as the page is open. Protective; **planned** (step 2); later.
- **Email** for assignments, with no patient details in the message. Protective; **planned** (step 9); later.

**At first live use:** Considerable, Low: **2**.

**Residual risk:** Considerable, Very low: **2**.

**Owner:** [CSO]. **Status:** Open.

---

## H-10: MedApp is unavailable

**Description.** MedApp can't be reached, or doesn't work, while pathways are under way.

**Causes:**
- **It's never run in production:** no monitoring, alerting, service level or on-call (`docs/architecture.md`, risk R1, section 10.2).
- **Readiness checks** only that the process is up, not the database or Valkey (risk R3).
- **One maintainer** (risk O1).
- A failed deploy: every merge deploys (risk D1).
- The organisation's own network or devices.
- **For one member of staff (since `feature/email-2fa`): they can't sign in.** They've lost the phone with their authenticator app, or five wrong codes (or passwords) have locked their account for five minutes, or an admin who must use two-factor hasn't set it up and is held to doing so. **Or, with codes by email (`feature/email-2fa-codes`), the emailed code doesn't arrive:** mail delayed or down, the hour's email allowance used up, or the email in the junk folder. A person locked out of a clinical system mid-shift can't record or look up work. **New causes, for the clinical safety officer to assess.**
- File storage alone can fail while the rest works: H-28.
- **A hostile file uploaded as an attachment** (branch `feature/file-attachments`): an image whose header claims a huge size (a 1 MB progressive JPEG claiming 24,000 by 24,000 pixels needs over a gigabyte to decode for its thumbnail), a Word file whose table of contents lists hundreds of thousands of entries, or many such uploads at once, could take an API copy past its memory limit, so that the kernel kills it with every request it was serving. Two such uploads could take both copies down. Controlled since the review of #85 (existing controls).

**Effect.** Staff can't see who's assigned to what, which stage a pathway is at, its notes or its target date. Work is done without recording it, or not done.

**Possible harm.** Delayed or missed care for every patient with a pathway under way while it's down; records that don't match what was done afterwards. Severe injury from which recovery is expected, affecting multiple patients: Major.

**Existing controls:**
- Two copies of the API in Kubernetes, with a disruption budget and rolling updates that served every request in testing (`docs/architecture.md`, sections 7.4 and 10.1).
- A failed migration stops the rollout and leaves the old version running (ADR-10).
- Server rendering falls back to rendering in the browser if Node fails (`SsrPages`).
- Rollback is a revert on `main` (`AGENTS.md`).
- **For a member of staff locked out of their account** (`feature/email-2fa`): two-factor is optional, and off unless they turn it on; ten one-time recovery codes, shown at set-up with copy and download; an admin can reset someone's two-factor at once, from the Users page (recorded, and the owner is emailed); "Forgot your password?" works by email without an admin; a lockout lasts five minutes; requiring two-factor for admins can't be turned on by an admin who doesn't use it, and a held admin can set it up from the page they're sent to. Deploying organisations should make sure there's always an admin on duty who can reset another's two-factor, and that staff keep their recovery codes (a downtime procedure item, step 6). **For codes by email:** a code that can't be sent now isn't promised (the sign-in says so, and a recovery code still signs in); codes have a share of the hourly email allowance that reset links can't use, so anonymous reset requests can't stop them (`EmailBudget`); a code can be sent again (once a minute, three times a sign-in), and the email says to look in the junk folder; the authenticator app, which needs no email, is offered as the stronger choice.
- **Hostile files are bounded:** the thumbnail generator reads the image's header first and makes no thumbnail (the upload still succeeds, without a preview) when the source is over 24 megapixels or over 10,000 pixels on either side (`SkiaThumbnailGenerator`); a `.docx` whose ZIP table of contents lists more than 5,000 entries is refused, the count read from the end-of-central-directory record before the archive is opened (`FileTypeSniffer`); at most two thumbnails or Word inspections run at a time per API copy (a `SemaphoreSlim(2)`); and a request is at most 20 MiB.

**Initial risk:** Major, Medium: **3**.

**Further controls:**
- **A printable task list and pathway summary,** so staff can carry on from paper. Design; **planned** (step 6); before first live use.
- **A downtime procedure** for deploying organisations: what to print and when, how to work on paper, and how to enter the record afterwards. Information; **planned** (step 6, the DCB0160 pack); before first live use; transferred.
- **Monitoring and alerting,** a published service level, and readiness that checks the database. Protective; **planned** (step 5); before first live use.
- **A status page,** and telling customers about planned maintenance. Information; **planned** (step 5); later.
- **A tested rebuild in a second region,** and a year of availability history. Protective; **planned** (step 5); later.

**At first live use:** Major, Low: **3**. An outage affects every patient at once, and with no operating history the likelihood can't yet be shown to be very low. Needs the CSO's written justification for first live use.

**Residual risk:** Major, Very low: **2**.

**Owner:** [CSO]. **Status:** Open.

---

## H-11: Target dates don't mean what staff think

**Description.** The target date is set, shown or understood differently from what the person who set it meant.

**Causes:**
- **A target date is a whole day,** due by the end of that day, UK time (`DueDates.EndOfDayUtc`). Something needed by 09:00 isn't overdue until after midnight.
- **Editing allows a past date:** creating a pathway refuses a target date in the past (`CreatePatientPathwayValidator`), but editing doesn't check (`UpdatePatientPathwayValidator`), so a slip in the year makes a pathway overdue at once, or a long way in the future.
- **The time zone database:** `DueDates` looks up `Europe/London` when it's first used. If the container image had no time zone data, every pathway request would fail; nothing tests this in the image.
- **A browser outside the UK:** the date picker's "today" is the browser's date (`todayIso`, `MedApp.Web/src/utils/dates.ts`), while the API uses the UK date. The API refuses the difference, so this causes an error, not a wrong date.
- **Priority isn't part of "overdue":** an urgent pathway with a distant target date isn't flagged.

**Effect.** Work is treated as on time when it isn't, or the other way round.

**Possible harm.** Delayed care for one patient. Considerable.

**Existing controls:**
- One rule, in one class, stored as the end of the UK day in UTC, so the day survives the clocks changing (`DueDates`). Tested for summer, winter and both clock-change days (`DueDatesTests`).
- The API takes and returns a date, not a time (`PatientPathwayResponse.DueDate`), and the web app shows it without converting it (`formatShortDate` uses UTC on a date-only value).
- Lists sort by target date by default.

**Initial risk:** Considerable, Low: **2**.

**Further controls:**
- **Say what a target date means** on the form ("due by the end of this day, UK time") and in the instructions for use. Information; **proposed**; before first live use (it costs nothing).
- **Confirm a target date in the past,** or more than a year ahead, when editing. Protective; **proposed**; later.
- **A start-up check** that `Europe/London` resolves in the image, and a test in the `image` CI job. Protective; **proposed**; later.
- **Target dates for each stage, and times of day** if customers need them. Design; **planned** in part (step 9); later.

**At first live use:** Considerable, Low: **2**.

**Residual risk:** Considerable, Very low: **2**.

**Owner:** [CSO]. **Status:** Open.

---

## H-12: Times are shown an hour out

**The cause is confirmed, and the defect is fixed by PR #62.** The initial rating below is for `88dc1fa`, before that fix; the fix is recorded as a further control, and the residual rating takes it into account. The CSO verifies it before first live use (see Residual risk).

**Description.** The time a stage was started or finished, a pathway was created or changed, a person was assigned, or an audit entry was made is shown wrongly: an hour early during British Summer Time on a UK browser, and by the browser's offset from UTC elsewhere.

**Causes:**
- Times are stored in UTC. When EF Core reads them from SQL Server, they come back without a time zone (`DateTimeKind.Unspecified`), and nothing in the code at `88dc1fa` marks them as UTC, so the API's JSON carries them without a `Z`.
- The web app's `formatDateTime` (`MedApp.Web/src/utils/dates.ts`) passes the string to `new Date()`, which reads a date-time without a zone as the browser's local time, then shows it in UK time. The same file has a `utc()` helper for exactly this case, with the comment "one read from the database may not say so", but only `timeAgo` uses it.
- It's used for stage started and finished times (`StageList.tsx`), assignment times (`AssigneeList.tsx`), a pathway's created and last updated times (`PathwayPage.tsx`) and the audit log (`AuditLogPage.tsx`).
- The tests only pass times that end in `Z` (`dates.test.ts`), so they didn't catch it.
- **A trap when checking it:** the server renders the page in Node in UTC, so the server's HTML shows the right time; the browser then re-renders it an hour early. Check the page after it has loaded in a browser during British Summer Time, or the API's JSON for a missing `Z`.

**Effect.** The timeline of every pathway is wrong by an hour for half the year: a stage looks as if it was finished an hour before it was, and the audit log disagrees with other systems' times during an investigation.

**Possible harm.** A clinical decision made on a wrong time, for example about how long ago a step was done; an incident investigation that reaches the wrong conclusion. It affects every patient's record at once, so multiple patients: Considerable.

**Existing controls:**
- Target dates aren't affected: they're sent as dates and shown without conversion.
- Times are stored correctly, so a fix corrects every past entry too.

**Initial risk:** Considerable, High: **3**.

**Further controls:**
- **Fix it:** mark every `DateTime` read from the database as UTC, read any time without an offset as UTC in the web app, and test with times that have no `Z`. Design; **built** in PR #62: a model-wide `UtcDateTimeConverter` marks every time read from the database as UTC (and turns a local time into UTC when writing), so the API's JSON ends in `Z`; `parseUtc` reads a time without a zone as UTC in the web app; .NET tests check every `DateTime` in the model has the converter and that the API's controller and hub JSON end in `Z`; the web tests run in `America/New_York`, so a time misread as local fails them in summer and winter; **before first live use**, to be verified by the CSO.

**At first live use:** Considerable, Very low: **2**.

**Residual risk:** Considerable, Very low: **2**. With the fix built, what's left is a future change that brings the defect back: a new way of reading or sending times that bypasses EF or the web app's date helpers. The tests above and the rules in `AGENTS.md` and `MedApp.Web/AGENTS.md` control that. Not yet done: seeing the fix end to end, against SQL Server and in a browser during British Summer Time. A related cause not fixed by #62: "today" in the web app is the machine's day, and server rendering runs in UTC, so between 23:00 and midnight UTC in summer the server's day and the browser's can differ (`todayIso`; a to-do).

**Owner:** [CSO], with the maintainer. **Status:** Open: the defect is fixed by PR #62; the fix awaits the CSO's verification.

---

## H-13: A template or stage definition has wrong content

**Description.** An admin creates or edits a template or stage definition with a mistake: a missing stage, stages in the wrong order, or wrong instructions. Pathways created from it carry the mistake.

**Causes:**
- Any admin can create and edit templates and stage definitions, and the change applies at once to new pathways (`UpdatePathwayTemplateHandler`, `UpdatePatientPathwayStageDefinitionHandler`).
- **No second check:** there's no approval or clinical review step in MedApp.
- **No history:** changes to templates and stage definitions aren't audited and have no versions. Nobody can see who changed what, or what a template said when a pathway was created from it, beyond the copy on the pathway itself.
- Admin is also the role that manages users, so the people who can edit clinical content aren't necessarily clinicians (`docs/architecture.md`, step 4, splits these roles).
- **SNOMED CT codes** (branch `feature/nhs-integrations`): a stage definition can carry a SNOMED CT code with a display term the admin types, copied onto pathways' stages. Only the code's format is checked (`SnomedCt`): nothing checks that the concept exists, is active, or means what the term says, so a code and its term can disagree. MedApp does nothing with the code, so today a wrong one only misleads someone reading it; once codes leave MedApp (step 9's FHIR resources for pathways), another system could act on one.

**Effect.** Every pathway created afterwards misses a step, does them in the wrong order, or shows wrong instructions, and staff follow it because MedApp enforces the order.

**Possible harm.** A required step of care missed for every patient whose pathway comes from that template. Severe injury or permanent incapacity, multiple patients: Catastrophic in the worst credible case.

**Existing controls:**
- Only admins can change templates and definitions; staff can only read them (`docs/architecture.md`, section 5.7).
- Templates and definitions in use are archived, not deleted (`SetPathwayTemplateArchivedHandler`; deletion of one in use is a 409).
- Each pathway keeps its own copy of the stages, with the template's name and ID, so what a pathway was given is preserved (`CreatePatientPathwayHandler`).
- The Templates page shows how many pathways use each template (`docs/pathways-ui-plan.md`, phase 6).
- **Transferred:** the organisation is responsible for the clinical content of its templates, approved under its own governance.

**Initial risk:** Catastrophic, Low: **4** (mandatory elimination or control).

**Further controls:**
- **An approval step:** a template or stage definition change takes effect only when a second person with a clinical approver role approves it. Protective; **proposed**; **brought forward to before first live use**, because it's the control that lowers the likelihood of a wrong template being used, and a 4 can't be released.
- **Audit every change** to templates and stage definitions (who, when, old and new). Protective; **proposed**; before first live use.
- **A local template governance process:** clinical sign-off before a template is used. Information; transferred; before first live use.
- **Versions:** each template change makes a new version; pathways record the version they came from. Design; **proposed**; later.
- **A separate role** for template administration. Protective; **planned** (step 4); later.
- **Check SNOMED CT codes with a terminology server:** that the concept exists and is active, with its preferred term shown beside the admin's. Protective; **planned** (`docs/todo.md`); before codes leave MedApp in the FHIR API.

**At first live use:** Catastrophic, Very low: **3**. Needs the CSO's written justification for first live use.

**Residual risk:** Catastrophic, Very low: **3**. The consequence can't be lowered by the software, only the likelihood; the CSO decides whether 3 is as low as reasonably practicable.

**Owner:** [CSO]. **Status:** Open.

---

## H-14: A correction to a template doesn't reach pathways already under way

**Description.** An admin corrects a template or stage definition (for example, an instruction that was wrong or unsafe), but pathways created before the correction keep the old stages and the old text.

**Causes:**
- **Copy on create, by design:** creating a pathway copies each stage's name, description and instructions onto it, so "later edits to the template or definitions don't change this record" (`CreatePatientPathwayHandler`; `docs/pathways-ui-plan.md`, section 3, D). This protects finished work from being rewritten, which is the right default.
- There's no way to apply a correction to unfinished stages, and nothing shows that a pathway's stages are out of date.

**Effect.** Staff working those pathways follow the old, wrong instructions, or miss a stage that was added.

**Possible harm.** A wrong or missed step for every patient whose pathway was already under way. Severe injury from which recovery is expected, multiple patients: Major.

**Existing controls:**
- The pathway shows the template's name as it was (`TemplateName`), and the Templates page shows how many pathways use a template, so admins can find the affected pathways by searching on the template (`PathwaySearch.Filter`, `TemplateId`).
- A pathway can be cancelled with a reason and recreated from the corrected template.

**Initial risk:** Major, Low: **3**.

**Further controls:**
- **List the open pathways when a template or definition in use is edited,** with a link to them, saying that they keep the old stages and need reviewing. Protective; **proposed**; before first live use.
- **A local process:** when a template is corrected for safety, search for its open pathways and review each. Information; transferred; before first live use.
- **Flag pathways whose template has changed** since they were created, on the pathway page and in lists, with what changed. Protective; **proposed**; later (depends on versions, H-13).
- **Let an admin apply a correction to the unfinished stages** of chosen pathways, audited, never to finished ones. Design; **proposed**; later.

**At first live use:** Major, Very low: **2**.

**Residual risk:** Major, Very low: **2**.

**Owner:** [CSO]. **Status:** Open.

---

## H-15: Two people edit the same pathway at once

**Description.** Two people change the same pathway, or the same stage, at about the same time, and one person's change silently replaces the other's.

**Causes:**
- **No optimistic concurrency:** no entity has a concurrency token, so the last save wins (`docs/architecture.md`, section 8.9 and risk R8).
- **Stage notes:** two people saving notes on the current stage each replace the whole text. Nobody is warned.
- **Notes saved at the same moment a stage is finished:** if both requests are handled at once, both pass their checks, and the notes change after the stage is locked. A narrow race, since each request reloads the pathway and checks it (`UpdatePathwayStageNotesHandler`).
- **Pathway details** (title, notes, target date, priority) behave the same way (`UpdatePatientPathwayHandler`).
- **Unsaved typing can disappear:** the notes box is reset whenever the saved notes change (`PathwayPage.tsx`, `key={...stage.notes}`), so if the page reloads the pathway after someone else saved, text typed but not saved is replaced by theirs.

**Effect.** Information recorded by one person is no longer on the record, though it is in the audit log; or text typed but not saved is lost.

**Possible harm.** Someone acts on incomplete notes, for one patient. Considerable.

**Existing controls:**
- Every notes save is audited with the old and new text, so nothing saved is lost for good (`PathwayChange.UpdateStageNotes`).
- Stage order, locks and status rules are checked on the server against the saved pathway, so a page that's out of date can't start or finish the wrong stage, or save notes on a locked one (409 from `WhyStageCantBeEdited`).
- "Unsaved changes" shows beside the notes box (`StageWork.tsx`).
- The "not assigned to you" question makes it a little less likely that a second person works a pathway that someone else is working. It's asked once per visit, and not of admins (`PathwayPage.tsx`, `checked`).

**Initial risk:** Considerable, Medium: **3**.

**Further controls:**
- **Optimistic concurrency:** a `rowversion` on pathways and stages, and a 409 when someone else has changed it since you loaded it. Protective; **planned** (step 6); **before first live use**.
- **Keep the draft on a conflict:** show the other person's text beside yours, never replace unsaved text without asking. Protective; **proposed**; before first live use (with the above).
- **Show who else has the pathway open.** Protective; **proposed**; later.

**At first live use:** Considerable, Very low: **2**.

**Residual risk:** Considerable, Very low: **2**.

**Owner:** [CSO]. **Status:** Open.

---

## H-16: The screen shows out-of-date information

**Description.** A page shows a pathway as it was when the page loaded, not as it is now.

**Causes:**
- Pages don't refresh when the window regains focus (`refetchOnWindowFocus: false`, `MedApp.Web/src/app/queryClient.ts`), and nothing refreshes a pathway page when someone else changes it. Only the user's own changes and an assignment notification refresh it.
- The overdue badge is worked out when the data loads, so a page left open over midnight doesn't show work that has since become overdue.
- If the server can't render a page, the browser renders it instead from fresh data, so this isn't stale, but it's slower (`SsrPages`).

**Effect.** Someone reads a status, assignee, current stage or notes that have changed, for example a pathway that someone else has cancelled or blocked.

**Possible harm.** A decision made on old information, for one patient. Significant: any change they then try is checked against the saved pathway (H-15).

**Existing controls:**
- The server checks every change against the saved pathway's rules (H-15).
- The pathway page shows "Last updated" with a time (`PathwayPage.tsx`; but see H-12).
- Pages are sent with `Cache-Control: no-store`, so the browser never shows a cached page.

**Initial risk:** Significant, Medium: **2**.

**Further controls:**
- **Refresh pathway data when the window regains focus,** at least on pathway pages and task lists. Protective; **proposed**; later.
- **Live updates** to pathway pages and lists when someone else changes them. Protective; **planned** (`docs/pathways-ui-plan.md`, phase 7, "later"); later.

**At first live use:** Significant, Medium: **2**.

**Residual risk:** Significant, Low: **2**.

**Owner:** [CSO]. **Status:** Open.

---

## H-17: Stage instructions aren't read

**Description.** Staff work a stage without reading its instructions.

**Causes:**
- Each stage's instructions start folded, on the current stage as well as in the list, and open only when clicked (`PathwayPage.tsx`, `instructionsOpen`).
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
- **Passwords only for most accounts:** two-factor authentication is available but optional (since `feature/email-2fa`; admins can require it for admin accounts), so a shared or passed-on password is still enough for most (risk S8).
- **Deleting a user** removes the account. Their name then shows as "Deleted user" on stages and assignments (`PathwayMapper.DeletedUser`), though the audit log keeps a snapshot of the username. (What happens to their open work is H-21.)

**Effect.** It's unclear who did a step, which matters when someone needs to ask about it or when something went wrong.

**Possible harm.** A question about a patient's care can't be answered quickly; accountability is lost. One patient; Significant.

**Existing controls:**
- Each change is recorded with the signed-in user's ID and a snapshot of their username (`PathwayActor`, `PathwayAudit`).
- Sessions are revoked on sign-out, and a session whose user no longer exists is refused, so deleting a user ends their sessions (`SessionService`, `MedApp.Infrastructure/Common/Authentication/SessionService.cs`).
- Five wrong passwords lock an account for five minutes. Sign-in is also throttled: 5 attempts a minute per username and 30 per address (`LoginThrottling`; `feature/security-baseline`, not yet merged).
- Ending sessions when the password changes: changing a password ends the user's other sessions, an admin's reset ends all of them, and an admin can sign a user out everywhere, for example when a password may have been shared (`UpdateUserPasswordHandler`, `ResetUserPasswordHandler`, `SetUserRolesHandler`, `SignOutEverywhereHandler`; same branch).
- The 10,000 most common passwords are refused when a password is set (`CommonPasswordValidator`; same branch).

**Initial risk:** Significant, Medium: **2**.

**Further controls:**
- **An idle timeout,** with a warning. Protective; **planned** (step 2); later. (The warning before the fixed limit, added in #63, is not this control.)
- **End a user's other sessions** on a password change and a reset. Protective; **in place** (`feature/security-baseline`, #67). A role change applies to the next request without ending sessions.
- **Multi-factor authentication** and single sign-on. Protective; an authenticator app with recovery codes, optional, which admins can require for admin accounts, is **in place** (`feature/email-2fa`); compulsory for everyone, passkeys and single sign-on are **planned** (step 4); later.
- **Deactivate accounts instead of deleting them.** Design; **planned** (step 4); later.
- **Local policy:** no shared accounts; lock screens when away. Information; transferred.

**At first live use:** Significant, Medium: **2**.

**Residual risk:** Significant, Low: **2**.

**Owner:** [CSO]. **Status:** Open.

---

## H-19: Part of the clinical record is lost by deletion

**Description.** Information about a patient's care is deleted.

**Causes:**
- **Pathways:** an admin can delete a cancelled or archived pathway, with its stages and assignments (`DeletePatientPathwayHandler`). Its stage notes then survive only as text inside audit entries, which only admins can read.
- **Patients:** any staff member can delete a patient without pathways or files (H-03); since step 7, the deletion is recorded in the activity log.
- **Anonymisation (step 7):** an admin can anonymise a patient's record, which replaces their name, email and every note, pathway title and reason (in the record, its audit trail and notifications) with placeholders, and keeps only the year of birth. It's meant for records past their retention period. Done on the wrong patient, or too early, the clinical content is gone from the live system (backups keep it until they expire). It also renames and deletes the patient's attached files (H-26).
- **Attached files:** deleted files can't be restored from MedApp: H-26.

**Effect.** The record of what was done, or of the patient, is no longer where staff look for it.

**Possible harm.** A later decision made without that history, for one patient. Considerable.

**Existing controls:**
- Only cancelled or archived pathways can be deleted, only by admins, after a confirmation that names the pathway and patient (`PathwayWorkflow.CanBeDeleted`; `PathwayPage.tsx`).
- The audit log keeps the deletion and every earlier change, including the old and new text of every notes save, with no foreign keys, so it outlives the pathway (`PatientPathwayAuditEntry`).
- A patient with pathways can't be deleted.
- Anonymising is admin-only; it needs the patient's full name typed, and a reason, which the activity log keeps with the admin's name; it's refused while any of the patient's pathways is under way or the record is restricted; the dialog shows the patient's name and date of birth, and says it can't be undone (`AnonymisePatientHandler`, `AnonymiseDialog`; step 7). An admin can export the whole record first (step 7).
- The retention review lists records past the **adult or children** minimum period only, and nothing is deleted automatically (step 7). It doesn't know the longer specialty classes (mental health 20 years, maternity 25, oncology 30), so a specialty record can be listed years before its period ends: the Retention Review page and the anonymise dialog warn about this, the retention schedule tells customers to set `Retention:AdultYears` to the longest class they use until classes exist, and a specialty record listed early is kept (Keep, with the class as the reason).

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
- **Attached files aren't backed up** beyond 7 days of soft delete on Azure, and they're stored apart from the database (`deploy/azure/README.md`, "Not yet"). A database restored to an earlier point loses the details of files attached since, so they vanish from the record although their contents are still in storage.

**Effect.** Stages finished, notes written and pathways created since the restore point are gone, for every patient, and staff don't know which.

**Possible harm.** Steps done twice, or believed done when the record of them is gone. Severe injury from which recovery is expected, multiple patients: Major.

**Existing controls:**
- Each change and its audit entries are saved in one transaction (`PatientPathwayRepository.SaveAsync`).
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

**Description.** An admin deletes the account of someone who has left, and the pathways assigned to them stay assigned to the deleted account, on nobody's task list.

**Causes:**
- **Deleting an account touches nothing else.** `DeleteUserHandler` calls `IdentityUserService.DeleteUserAsync`, which deletes the Identity user only.
- **Assignments have no link to the user table:** they're keyed on the pathway and the user's ID, with a foreign key to the pathway only (`PatientPathwayAssignmentConfiguration`), so the rows stay.
- **The status still says Assigned,** because it counts assignments, not live accounts (`PathwayWorkflow.StatusFromAssignments`). So an "unassigned" view (H-04) wouldn't show it either.
- **My Worklist matches the signed-in user's ID** (`PathwaySearch.Filter`, `AssignedToMe`), so no one else's list shows it.
- **Deleting is the only way to remove an account,** and the guidance to customers is to remove leavers promptly.

**Effect.** The leaver's open pathways look looked after, showing "Deleted user" as the assignee, but nobody is working them, and nothing tells anyone.

**Possible harm.** Delayed care for every patient on the leaver's list. Severe injury from which recovery is expected, multiple patients: Major.

**Existing controls:**
- The pathway page and lists show the assignee as "Deleted user" (`PathwayMapper.DeletedUser`), which someone looking at the pathway would notice.
- Any staff member can still work the pathway, and an admin can unassign the deleted account and assign someone else.
- Overdue badges still show on the full Pathways list.
- Since staff calendars, an account with upcoming Booked appointments can't be deleted until they're moved or cancelled (H-35). Open assignments still aren't checked.

**Initial risk:** Major, Medium: **3**.

**Further controls:**
- **Refuse to delete an account with open assignments, or require reassignment first:** list the open pathways assigned to the account, and delete it only once they're reassigned. Protective; **proposed**; **before first live use**.
- **Deactivate accounts instead of deleting them,** keeping their assignments visible until reassigned. Design; **planned** (step 4); later.
- **A leaver process:** reassign a leaver's open pathways, then remove the account. Information; transferred; before first live use.

**At first live use:** Major, Very low: **2**.

**Residual risk:** Major, Very low: **2**.

**Owner:** [CSO]. **Status:** Open.

---

## H-22: Care under way is delayed because the patient's record is restricted

**Description.** An admin restricts a patient's record (UK GDPR Article 18), for example while the patient contests their recorded details, and the pathways under way for that patient can't be worked on until the restriction is lifted.

**Causes:**
- **Restriction freezes the record and its pathways** (`PatientRules.EnsureCanChange`, applied to every pathway change by `GetForUpdateOrThrowAsync`): no stage can be started, written up or finished, and nothing can be assigned, while the record is restricted.
- **Its appointments are frozen too** (since staff calendars): none can be booked, rescheduled or given an outcome while the record is restricted (they can only be cancelled), so a visit a stage needs can't be arranged or recorded (H-30).
- **Files are frozen too** (branch `feature/file-attachments`): nobody, admins included, can attach a file to a restricted record, its pathways or its stages, or delete one, so a new letter or result can't be added while the restriction lasts (H-24), nor a file on the wrong record removed (H-23).
- **A restriction can last weeks** while the contested details are checked, and only an admin can lift it.
- **The admin restricting may not be the clinician working the pathways,** and may not know what's due.

**Effect.** The next stages of a pathway under way (pre-assessment, consent, a follow-up) can't be recorded, and the pathway goes overdue with nothing staff can do but wait for an admin.

**Possible harm.** Delayed care for that patient. Considerable.

**Existing controls (step 7, PR #68):**
- **The admin must confirm:** restricting a record with pathways under way or open appointments shows how many of each there are, and needs an explicit confirmation that their work stops (the API refuses it otherwise, `SetPatientFlagHandler`, `ConfirmOpenPathways`). Since the review of PR #93, open appointments alone ask for it too (decision 49), and if the dialog's counts are out of date the API's own message is shown.
- **Care can still be paused safely:** a restricted record's pathways can be blocked or cancelled, with a reason (`PatientRules.PausesWhileRestricted`, `GetForPauseOrThrowAsync`), so they don't sit silently overdue; a blocked pathway is never shown as overdue. Its appointments can still be cancelled, with a reason (`GetForCancelOrThrowAsync`), so a patient isn't left booked for care that's paused.
- **Everyone sees that the record is restricted, and why on demand** (until branch `feature/warning-banners`, "Everyone sees why": the whole message was on screen). The restricted banner shows a label, "Restricted record: changes paused", with its warning colour and icon, in the header of the patient's page, Edit Patient, the patient's pathways, the pathway page, Edit Pathway and New Pathway, and under the patient in the booking and appointment dialogs. The whole message (what can't be changed, that a pathway can still be blocked or cancelled and an appointment cancelled, the reason and since when) is no longer on screen: it shows over the page when the label is hovered, focused from the keyboard, clicked or tapped. Screen readers get the whole message as the label's description, and as an alert when the banner appears in a page already open (restricting the record, or moving to the page inside the app); on a fresh page load an alert already in the page isn't announced, so they meet it on reaching the label. The pathway page offers only blocking and cancelling; Edit Pathway says why the details can't be edited, and the booking dialog says why nothing can be booked, both on screen. The printable export page shows only the `PatientBanner`'s "Restricted" token, which stays beside the patient's name on every page about the patient. **For the CSO to accept:** moving the reason and the exceptions behind the label is a change to this control.
- Lifting the restriction is one admin action, recorded with a reason.

**Initial risk:** Considerable, Low: **2**.

**Further controls:**
- **Let an admin record the patient's consent, or another lawful basis, for treatment to continue** (Article 18(2)), with the restriction then stopping only edits to the contested details. Design; **proposed**; later. A clinical safety decision.
- **Tell the pathways' assignees** when their patient's record is restricted. Protective; **proposed**; later.
- **A local procedure for restriction requests:** the admin checks with the clinician responsible for the patient's care before restricting, and agrees how care continues. Information; transferred; before first live use.

**At first live use:** Considerable, Low: **2**.

**Residual risk:** Considerable, Very low: **2**.

**Owner:** [CSO]. **Status:** Open.

---

## H-23: A file is attached to the wrong patient, pathway or stage

**Description.** A letter, result, scan or photo is attached to a different patient from the one it's about, or to the wrong pathway or stage of the right patient, and someone reads it as belonging there. The file-level form of H-01 and H-02.

**Causes:**
- The same identification limits as H-01: the NHS number is optional and isn't traced, and nothing compares the document with the patient.
- **Nothing asks which patient before a file is sent.** Files are dropped on, or chosen in, an "Add files" area on the patient page, the pathway page and each stage, several at once, and sent straight away (`MedApp.Web/src/components/attachments/Attachments.tsx`). There's no confirmation naming the patient.
- **Half views** put two patients or pathways side by side, each with its own drop area; dragging a file from the desktop onto the wrong one is easy.
- **The file on the computer doesn't say who it's for:** a scanner or a phone names files by date or number, and choosing several at once can pick up another patient's file from the same folder.
- **A file can't be moved:** correcting it means deleting it and attaching it again in the right place, and staff can delete only their own files, and only while the stage or pathway can still change; otherwise only an admin can (`DeleteAttachmentHandler`, `WhyFilesCantChange`).
- **On a restricted or anonymised record nobody can delete a file,** admins included (the restriction guard's 409, `PatientRules`), so a file attached to the wrong patient stays on their record until the restriction is lifted (H-22).

**Effect.** The right record lacks the document and the wrong record has it. A clinician reads another patient's result, letter or image as this patient's, or doesn't find this patient's. It is also a disclosure of the other patient's data ([DPIA](../governance/dpia.md), risk 8).

**Possible harm.** A decision made on another patient's result or image, for example a pathway going ahead on someone else's normal result. One patient; Major in the worst credible case.

**Existing controls:**
- A file is attached to whatever the page shows: the patient page attaches to the patient, a pathway page to the pathway, and a stage's area to that stage. The patient page lists every file of the patient, each saying what it's attached to (`ViewPatientPage.tsx`, `Attachments.tsx`).
- Each file shows who attached it and when, and the upload is audited and shown on the patient's timeline as "a file was attached" (`AttachmentAudit`, `TimelineMapper`).
- **The target is checked again once the upload is staged,** just before it's stored: it still exists, the record isn't restricted or anonymised, and the stage or pathway still takes files (`UploadAttachmentHandler`). That narrows the window in which a record that changed during a slow upload could take the file to milliseconds; closing it fully would need the save to re-read the record in the same transaction.
- A wrong file can be deleted at once by whoever attached it, or by an admin, wherever it is; it's then hidden from staff and can't be downloaded, and the deletion is audited (`DeleteAttachmentHandler`).
- **Transferred:** positive patient identification, and checking the identifiers printed on a document against the patient before acting on it.

**Initial risk:** Major, Low: **3**.

**Further controls:**
- **The patient banner** from H-01, on every page where files can be attached (the patient page and the pathway page, with each stage's files). Protective; **built** on branch `feature/nhs-integrations` (`PatientBanner`), awaiting the CSO's verification; **before first live use**.
- **Name the patient and the target before sending:** "Attach 3 files to {stage} of {pathway} for {patient name, date of birth}?", including for files dropped in a half view. Protective; **proposed**; **before first live use**.
- **Move a file** to another patient, pathway or stage (admins, with a reason, audited), so a correction doesn't depend on deleting and attaching again. Design; **proposed**; later.
- **Train staff** to check the patient before attaching, to check a document's own identifiers before acting on it, and to delete and report a file attached in error. Information; transferred.

**At first live use:** Major, Very low: **2**. It assumes the banner, which is built and awaits the CSO's verification, and the confirmation naming the patient, which isn't built.

**Residual risk:** Major, Very low: **2**.

**Owner:** [CSO]. **Status:** Open: the banner is built and awaits the CSO's verification; the confirmation naming the patient isn't built.

---

## H-24: A document or result is missing, and nobody knows

**Description.** A document staff expect to find in MedApp isn't there, and the person looking assumes it doesn't exist: a result not yet received, a letter not sent, a scan not done.

**Causes:**
- **An upload fails or is abandoned:** the file is refused (a type that isn't allowed, a name that doesn't match its contents, over 20 MB), storage is unavailable (H-28), or the tab is closed or the page left while files are still being sent. Nothing warns before closing a tab with uploads in progress, and a failed upload's message is shown only to the person who sent it, in that browser, until dismissed (`Attachments.tsx`).
- **Files have to be downloaded to be read.** There's no preview of PDFs, and no thumbnail for HEIC photos (`docs/todo.md`), so the list gives only a name, a type and a size, and a document's content is easy to miss.
- **"Not scanned"** on every file (H-25) may put staff off opening a file at all.
- **Long lists:** a list shows the newest 200 files of a patient, pathway or stage, and says how many there are in all (`AttachmentLimits.MaxListed`).
- A file deleted by mistake (H-26).
- **A restricted or anonymised record takes no new files** (the restriction guard's 409, admins included), so a letter or result for a patient whose record is restricted can't be attached until the restriction is lifted (H-22).

**Effect.** A clinician acts as if a result or letter doesn't exist, or waits for one that was received.

**Possible harm.** Delayed or wrong care for one patient. Considerable.

**Existing controls:**
- Each file shows its progress as it's sent, with Cancel, and a failed one stays listed with the reason until dismissed (`Attachments.tsx`, `useAttachmentsState`).
- The API checks type and size and refuses with a clear message; the web app states the limits.
- Every file attached is listed, newest first, with who attached it and when, on the page it's attached to and on the patient's page; an upload shows on the patient's timeline.
- **Transferred:** MedApp isn't the record of results; the organisation's own systems (laboratory, imaging, EPR) hold the master copy.

**Initial risk:** Considerable, Low: **2**.

**Further controls:**
- **Warn before closing the tab or leaving the page** while files are still being sent. Protective; **proposed**; later.
- **A safe preview** of PDFs and HEIC photos (rendered to an image on the server, never shown inline). Protective; **planned** (`docs/todo.md`); later.
- **Page long lists** instead of stopping at 200. Protective; **planned** (`docs/todo.md`); later.
- **Train staff** to check a document is listed after attaching it, and that MedApp's files are copies: look in the source system before concluding a result doesn't exist. Information; transferred.

**At first live use:** Considerable, Low: **2**.

**Residual risk:** Considerable, Very low: **2**.

**Owner:** [CSO]. **Status:** Open.

---

## H-25: An attached file carries malware

**Description.** A file attached in MedApp carries malware, for example a PDF or Word document received from outside the organisation, and is opened on a clinical workstation.

**Causes:**
- **Files aren't scanned.** No malware scanner is set up; every file is stored and shown as "Not scanned" (`NoMalwareScanner`; `deploy/azure/README.md`, "Not yet").
- Staff attach documents that came from outside: from patients, referrers or other organisations.
- Every file is opened by downloading it into a desktop application (a PDF reader, Word, an image viewer), whose own flaws are the target.

**Effect.** The workstation, and possibly the organisation's network and clinical systems, are compromised, for example by ransomware, and become unavailable.

**Possible harm.** Delayed or missed care for every patient whose care depends on the affected systems while they're down. Severe injury from which recovery is expected, multiple patients: Major, as for H-10.

**Existing controls:**
- **A short list of types,** found from each file's contents, never its name or the browser's type, with the name's extension matching: PDF, PNG, JPEG, HEIC/HEIF, Word `.docx` and plain text. Macro-enabled Word, SVG and HTML are refused, as is anything else (`FileTypeSniffer`, `UploadAttachmentValidator`).
- **The commonest active content is refused too:** a `.docx` with any external relationship (`TargetMode="External"` in a `.rels` part: a remote template, an external link to a share), embedded objects (`word/embeddings/`) or ActiveX (`word/activeX/`); and a PDF containing `/JavaScript`, `/JS`, `/Launch`, `/EmbeddedFile` or `/RichMedia`, found by a byte search, so a harmless PDF that merely mentions one is refused as well (`FileTypeSniffer`). **Everything else is left to the malware scanner:** an exploit in an image decoder or in a PDF's own structure isn't caught by type checks.
- **Nothing is shown or run in the app:** every file is sent as a download, with headers that stop a browser showing or running it on MedApp's site (`AttachmentResponseHeaders`). The only images shown in pages are thumbnails the server re-encodes (`SkiaThumbnailGenerator`).
- **"Not scanned"** is shown on every file, with a note saying what it means (`Attachments.tsx`).
- **Transferred:** up-to-date anti-malware, PDF readers and Office on every computer that opens files, with macros off.

**Initial risk:** Major, Low: **3**.

**Further controls:**
- **Malware scanning on upload:** Microsoft Defender for Storage on Azure, a scanner such as ClamAV elsewhere, behind the existing `IMalwareScanner`; infected files refused, and downloads held until a file is scanned. Protective; **planned** (roadmap step 9; `docs/todo.md`); **brought forward to before first live use**, as the DPIA does (risk 22), or the deployment runs without file storage. Holding downloads until a scan finishes adds a delay before a new file can be read (H-24); assess it when it's built.
- **Tell staff** what "Not scanned" means and how to report a suspicious file. Information; transferred.

**At first live use:** Major, Very low: **2**, with scanning built. Without it, Major, Low: **3**, which would need the CSO's written justification.

**Residual risk:** Major, Very low: **2**. Scanning finds known malware, not all of it; the organisation's own endpoint protection is the second layer.

**Owner:** [CSO], with the maintainer. **Status:** Open.

---

## H-26: A file is deleted by mistake

**Description.** A file that belongs on the record is deleted, by slip or by the wrong person, and staff can no longer open it.

**Causes:**
- Staff can delete files they attached, and admins any file (`DeleteAttachmentHandler`). Deleting a patient or pathway that has files is refused until the files are deleted (`DeletePatientHandler`, `DeletePatientPathwayHandler`), which invites deleting files in bulk.
- **There's no restore.** Deleting is soft: the file's details stay, admins can see it was there ("Show deleted files"), and its contents are kept in storage. But nobody can download or restore a deleted file from MedApp (`DownloadAttachmentHandler`), so getting it back needs the supplier, working in the database and storage by hand.
- Once a purge job exists (planned, proposed 30 days after deletion), the contents are gone after that window, and after 7 more days of storage soft delete on Azure.
- Deleting is also how a file on the wrong record is corrected (H-23), so deletions are routine.
- **Anonymising a record deletes all its files** and renames them, and can't be undone. Done on the wrong patient, or too early (H-19), the files are gone from the app, though their contents stay in storage until the purge job removes them.

**Effect.** A document that was part of the record is missing (H-24), though admins can see that it existed and who deleted it.

**Possible harm.** A later decision made without that document, for one patient. Considerable.

**Existing controls:**
- Deleting asks for confirmation, naming the file (`Attachments.tsx`, `ConfirmDialog`).
- Who deleted it, and when, is kept on the file and in the audit log, and shows on the patient's timeline (`AttachmentAudit`); that includes the files anonymisation deletes, each with its own audit entry (its name already replaced, `PatientAnonymiser`).
- The contents are kept, so the supplier can recover a file on the customer's instruction ([support-access-policy.md](../governance/support-access-policy.md)).
- **Transferred:** the master copy of a document is in the organisation's own systems.

**Initial risk:** Considerable, Low: **2**.

**Further controls:**
- **Restore a deleted file** (admins, audited), within the purge window. Protective; **planned** (`docs/todo.md`, with the purge job); later.
- **A purge window long enough to notice a mistake,** decided by the customer's DPO (proposed 30 days, [retention-schedule.md](../governance/retention-schedule.md)). Design; **proposed**; later.
- **A local rule:** delete a file only if it was attached in error; correct a wrong document by attaching the right one and saying why, never by deleting a document that was relied on. Information; transferred.

**At first live use:** Considerable, Low: **2**.

**Residual risk:** Considerable, Very low: **2**.

**Owner:** [CSO]. **Status:** Open.

---

## H-27: An out-of-date or duplicate document is relied on

**Description.** A patient has several versions of a document, for example an early and a final report, or the same letter twice, and staff read the wrong one.

**Causes:**
- **No versions:** a file can't be replaced, marked as superseded, or linked to the one that replaces it.
- **The list is in upload order,** not the document's own date, and a file's name is free text that may not say what or when it is.
- **No preview** of PDFs (H-24), so staff choose a file by its name.
- The same file can be attached twice; MedApp keeps each file's SHA-256 (`Attachment.Sha256`) but doesn't use it to warn.

**Effect.** A clinician reads a superseded result or letter, and acts on it.

**Possible harm.** A decision made on out-of-date information, for one patient. Considerable.

**Existing controls:**
- Each file shows when it was attached and by whom; the list is newest first.
- Superseded copies can be deleted (H-26 applies).
- **Transferred:** the date and status printed on the document itself; the master copy in the source system.

**Initial risk:** Considerable, Low: **2**.

**Further controls:**
- **Warn when the same file is attached twice** to a patient, using its SHA-256. Protective; **proposed**; later.
- **Mark a file as replaced by another,** shown on both. Design; **proposed**; later.
- **A local rule:** name files with what they are and the document's date, and delete superseded copies attached in error. Information; transferred.

**At first live use:** Considerable, Low: **2**.

**Residual risk:** Considerable, Very low: **2**.

**Owner:** [CSO]. **Status:** Open.

---

## H-28: File storage is unavailable

**Description.** MedApp works, but its file storage doesn't: files can't be attached or opened.

**Causes:**
- The storage (Azure Blob Storage, or Azurite in the cluster) is down, unreachable, or misconfigured; or its key was rotated without redeploying (`deploy/azure/README.md`, Passwords).
- **Nothing checks it:** readiness doesn't test storage, and there's no monitoring (H-10).
- A deployment can run with no file storage at all; then every upload and download is refused (`UnconfiguredFileStore`). In Kubernetes the API's reference to the attachments secret is optional, so a missing secret (for example a kind cluster whose secret store wasn't refreshed with `deploy/kind/up.sh` after upgrading) leaves the API running with no file storage, rather than unable to start.

**Effect.** Uploads and downloads fail while everything else works, with a 503: "File storage isn't set up" when there's none, and "File storage isn't available" (`StorageUnavailableException`) when it's set up but failing. Staff can't open a letter or result they need, or can't attach one.

**Possible harm.** Delayed care for the patients whose documents are needed while it lasts. Minor injury from which recovery isn't expected in the short term, multiple patients, in the worst credible case: Considerable.

**Existing controls:**
- The rest of MedApp keeps working, and a failed upload or download is an error, never a wrong or empty file; both kinds of failure are a 503 that says what's wrong (`FileStorageUnavailableException`, `StorageUnavailableException`, mapped by `ExceptionHandling`).
- **A failure is quick:** the blob client makes 2 retries with a 30-second network timeout, and cleaning up after a failed upload has its own 10-second bound, so an upload doesn't hang for minutes when storage stops answering.
- On Azure, the files are zone-redundant, so one zone's outage doesn't stop them (`deploy/azure/main.bicep`).
- **Transferred:** the master copies in the organisation's own systems; the downtime procedure (H-10).

**Initial risk:** Considerable, Low: **2**.

**Further controls:**
- **Monitoring and alerts that include storage,** and readiness that reports it. Protective; **planned** (step 5, with H-10's monitoring); before first live use.
- **Report a storage failure as a 503,** as for a deployment without storage, instead of a general server error. Protective; **built** after the review of #85 (above).
- **The downtime procedure says where to find documents** when MedApp's files can't be opened. Information; transferred; before first live use.

**At first live use:** Considerable, Low: **2**.

**Residual risk:** Considerable, Very low: **2**.

**Owner:** [CSO]. **Status:** Open.

---

## H-29: An admin relies on a monitoring rule to follow up clinical work

**Description.** An admin sets a monitoring rule (Administration, Monitoring) to be told when something happens, for example "anyone blocks an Urgent pathway", and relies on it to follow the work up, but isn't told.

**Causes:**
- **Monitoring is best effort.** Notifications are sent after the change is saved, outside its transaction: a failure is only logged, and a crash between the save and the send loses the notification for good (`PathwayMonitor`).
- **A rule stops silently** when its owner loses the Admin role, when it's switched off, or when the account, template or stage it names is deleted (it then matches nothing).
- **Only nine actions can be watched;** being overdue isn't one of them, and nothing reports a rule that never fires. Since PR 24 (decision 136 of `docs/centre-phases-3-5-plan.md`) a Required stage finished without its visit is one (`StageFinishedWithoutVisit`), but skipping a Required stage, which needs the same permission, isn't (`docs/todo.md`): a rule for it doesn't see every stage left without its visit, and the stages finished without a visit stay H-32's report, not this rule.
- **Live delivery** has H-09's causes: pushes are best effort, the browser stops reconnecting after a while, and there's no email.

**Effect.** A blocked, cancelled or re-dated Urgent pathway isn't followed up by the person who expected to hear of it.

**Possible harm.** Delayed care for one patient. Considerable.

**Existing controls:**
- **The intended purpose says it:** monitoring rules are supervision of work, not clinical alerting, and "must not be relied on to make sure anything clinical happens" (`intended-purpose-and-device-decision.md`).
- **The work is visible anyway:** the pathways list, My Worklist, the overdue badge and the patient's timeline show every blocked, cancelled and re-dated pathway, whatever the rules.
- The notifications that are sent are stored, so they show at the next page load.

**Initial risk:** Considerable, Low: **2**.

**Further controls:**
- **Information:** in training and the instructions for use, monitoring is described as a supervision aid, never as the way clinical follow-up is assured; follow-up is through the lists, My Worklist and local procedures. Information; **proposed**; before first live use (it costs nothing); transferred.
- **An outbox,** so a notification can't be lost between the save and the send. Protective; **proposed**; later.

**At first live use:** Considerable, Low: **2**.

**Residual risk:** Considerable, Very low: **2**.

**Owner:** [CSO]. **Status:** Open.

---

## Staff calendars and appointments: draft hazards for the CSO

H-30 to H-36 come from the design of staff calendars and appointments (`docs/calendar-plan.md`), drafted while the feature was being built on `feature/staff-calendars`. "Decision n" refers to section 1 of that plan. In short: staff book appointments for patients on staff calendars; an appointment goes from Requested (Requires appointment) to Booked, then Attended or Did not attend, or Cancelled; a stage definition can say a stage involves a visit (none, suggested or required), and MedApp then requests an appointment when the stage becomes current and, for a required visit, won't let the stage be finished until the appointment is Attended, unless an admin finishes it with a reason.

**Ratings:** left for the CSO to assign. Each entry gives a draft estimate, on the same scheme as the rest of the log, as a starting point. Check every existing control against the code before relying on it.

---

## H-30: A visit the patient needs doesn't happen

**Description.** A patient needs to be seen, but isn't: a Requested appointment is never booked, or a patient who didn't attend is never offered another appointment.

**Causes:**
- **A Requested appointment has nobody's name on it.** It has no member of staff until it's booked, so no one is notified about it (notifications go to the member of staff an appointment is booked with; decision 20). It waits on the Requires Appointment list (decision 29) until someone looks.
- **The list has no side navigation item** (since 17 November 2026): it's reached only from the Calendar page's "Requires Appointment (N)" link, so someone who doesn't open the Calendar doesn't see it or its count.
- **Nothing flags a request that has waited too long.** Flagging or notifying old Requested appointments is left for later (plan, section 8).
- **Did not attend leads nowhere by itself.** It's a status staff record (decision 16). The UI offers "Book another", and since PR 26 the Did not attend view lists it until someone records a follow-up (H-55), but nobody is notified.
- **Cancelling a Booked appointment doesn't request another.** A Requested appointment is made only when a stage becomes current (decision 23), so after a cancellation the stage has no open appointment until someone books one.
- **Appointments not for a stage** have no rule behind them at all, and a stage with a suggested visit can be finished without one (H-32).
- **A restricted record's appointments can't be booked, moved or given an outcome** until an admin lifts the restriction (H-22).
- **Pages don't refresh by themselves** (H-16), so a list left open doesn't show new requests.

**Effect.** The patient isn't seen when they need to be. For a stage whose visit is required, the pathway can't move past that stage, which shows; for anything else, nothing in MedApp shows that the visit didn't happen.

**Possible harm.** Delayed care, such as a missed pre-assessment or follow-up, for one patient. Considerable in the worst credible case.

**Existing controls** (in the design):
- **MedApp requests the appointment itself** when a stage with a visit becomes current, unless one is already Requested, Booked or Attended (decision 23), recorded in the appointment's history as requested by the app.
- **The Requires Appointment list** shows every Requested appointment, soonest pathway target date first, each with Book (decision 29). It isn't in the side navigation: it's reached from the Calendar page's **Requires Appointment (N)** link, which shows how many are waiting, so the count is seen by whoever opens the Calendar, but not on other pages.
- **A required visit holds up the stage:** it can't be finished until an appointment for it is Attended, except by an admin with a reason (decision 24), so a pathway can't quietly move on.
- **One open appointment per stage** (decision 18), so a stage's appointment is never lost among duplicates.
- The pathway's target date and overdue badge still apply to the pathway as a whole (H-04).
- **Every change is in the appointment's history** (decision 19), and on the patient's timeline (decision 27).

**Initial risk:** [For the CSO to assign.] Draft estimate: Considerable, Medium: **3**.

**Further controls:**
- **Flag or notify Requested appointments that have waited too long,** and Did not attend appointments not yet rebooked, to the pathway's assignees, or to admins when it has none. Protective; **planned** (`docs/todo.md`, "Calendar follow-ups"); **before first live use**.
- **A visible count of Requires Appointment,** in the side navigation or on Home, so the list isn't only seen by those who open the Calendar. (Since 17 November 2026 the list has no side navigation item at all; the count shows only on the Calendar page's link.) Protective; **proposed**; before first live use.
- **A local process:** who books from Requires Appointment, how often it's reviewed, and who follows up Did not attend. Information; transferred; before first live use.
- **Reminders to patients** by text or email, which cut missed appointments. Protective; **built** (PR 26), off unless the organisation turns it on, and a courtesy only (H-54).
- **The Did not attend view** lists missed appointments until a follow-up is recorded (H-55). Detective; **built** (PR 26).

**At first live use:** Draft: Considerable, Low: **2**.

**Residual risk:** Draft: Considerable, Very low: **2**.

**Owner:** [CSO]. **Status:** Open, draft for the CSO.

---

## H-31: A member of staff is double-booked

**Description.** Two patients are booked with one member of staff at the same time, or a patient is booked over time the member of staff has blocked off (for example annual leave or training).

**Causes:**
- **Two people book the same slot at the same moment,** from two browsers.
- **Two people book the same Requires Appointment appointment at the same moment,** with different members of staff (found in the review of PR #93): the lock is per member of staff, so they didn't wait for each other, and the later save silently overwrote the earlier. The first member of staff was told about an appointment that wasn't on their calendar.
- **A later template change** (an admin moves everyone's lunch, or applies a day template with leave to a day) lands on appointments already booked. MedApp allows that, and shows both (decision 6).
- **A page left open** shows slots that have since been taken (H-16).
- **Appointments kept elsewhere** (a paper diary, another system), so MedApp's calendar isn't the whole picture.
- **An appointment recorded after the event** (a walk-in, decision 74) may overlap blocked time or a meeting, because what happened, happened; it can't overlap another appointment.

**Effect.** One patient is seen late, or not at all, or the member of staff isn't where the calendar says.

**Possible harm.** Delayed care for one patient, or a rushed appointment. Considerable in the worst credible case.

**Existing controls** (in the design):
- **The server refuses any overlap:** a new or moved appointment or block that overlaps a Booked, Attended or Did not attend appointment, or a block (stored or from a template), on that person's calendar is a 409 that names the clash (decision 6; `CalendarClashes`). The UI only offers free slots (decisions 6 and 30).
- **Racing bookings are serialised** by a database lock per member of staff (`ICalendarLock`, SQL Server's `sp_getapplock`), so one wins and the other gets the 409 (decision 6). An integration test books the same slot twice at once (plan, section 7).
- **Two changes to one appointment can't both win:** an appointment has a row version, so the later of two overlapping saves is a 409, and a booking says whether it's booking a Requested appointment or moving a Booked one, refused (409) if someone else got there first (decision 46). An integration test books one Requested appointment twice at once, with two different members of staff: one succeeds, one gets the 409, and only one is told.
- **Overlaps from template changes are shown, not hidden:** both appear, the appointment on top, and the block is flagged as overlapping; nothing is moved or removed by the app (decision 6). A walk-in over blocked time or a meeting is flagged the same way (decision 76).
- **A walk-in can't overlap another appointment:** recording one over a Booked, Attended or Did not attend appointment of that member of staff is a 409 naming it, under the same calendar lock (decision 76).
- **Past days don't change** (decision 8).

**Initial risk:** [For the CSO to assign.] Draft estimate: Considerable, Low: **2**.

**Further controls:**
- **After a template change or a day's template is applied,** list the booked appointments it now overlaps for the person who made it, and notify each member of staff affected. Protective; **proposed**; before first live use.
- **A local rule:** MedApp's calendar is the only diary for these appointments. Information; transferred.

**At first live use:** Draft: Considerable, Low: **2**.

**Residual risk:** Draft: Considerable, Very low: **2**.

**Owner:** [CSO]. **Status:** Open, draft for the CSO.

---

## H-32: A stage is finished without the visit it needed

**Description.** A stage whose definition says it involves a visit is finished although the patient wasn't seen: a required visit is bypassed in error, or a suggested one is skipped when it was needed.

**Causes:**
- **The bypass** (decision 24) is used without checking, or because someone who holds it was asked to "just finish it". Since PR 24 it's `FinishWithoutVisit` (Clinician and Senior clinician to start with), not the built-in Admin role (decision 138 of `docs/centre-phases-3-5-plan.md`).
- **The suggested-visit confirmation** (decision 25) is clicked through. Confirmations people see often stop being read (H-06).
- **An appointment is marked Attended in error:** the wrong appointment, the wrong patient (H-34), or a correction from Did not attend made wrongly. Attended satisfies the rule.
- **The definition's setting is wrong:** a stage that needs a visit is set to none or suggested. It's copied onto each pathway when it's created (decision 22), so a correction doesn't reach pathways already under way (as H-14).
- **Finishing again after an unlock** doesn't ask for a visit again (decision 26).
- **An Attended appointment linked to the stage counts,** however long before the stage it was.
- **An appointment recorded after the event as Attended counts too** (decision 77), so recording one in error, or on the wrong stage, lets a Required stage be finished: H-39.

**Effect.** The record says the stage is done, and the pathway moves on, although the visit it depends on didn't happen.

**Possible harm.** A pathway goes ahead without a step that needed the patient seen, such as a pre-procedure assessment. One patient; Major in the worst credible case, as H-07.

**Existing controls** (in the design):
- **Required is enforced on the server,** not just the screen (`PathwayWorkflow.WhyStageNeedsVisit`): staff get a 409 that says to book the visit and mark it Attended (decision 24).
- **Only holders of `FinishWithoutVisit` can bypass it** (Clinician and Senior clinician to start with; since PR 24, not the built-in Admin role: decision 138 of `docs/centre-phases-3-5-plan.md`), after a confirmation dialog that asks for a reason (decision 24). An organisation can keep it to its senior clinicians by taking it off Clinician (the release note says so; the demo does).
- **Every finish without a visit is recorded:** the `StageFinished` audit entry records `withoutVisit`, the requirement and, for the bypass, the reason (decisions 24 and 25). An admin can be told of each bypass as it happens with a monitoring rule (`StageFinishedWithoutVisit`, PR 24), a convenience, never the control (H-29). The patient's timeline shows that a stage was finished without a visit (to admins, with the reason; decision 27).
- **Attended can only be recorded once the appointment's start has passed** (decision 16).
- **Existing stages and definitions are None** (decision 22), so nothing changes for pathways that were under way before.
- **Transferred:** template governance (H-13) covers which stages need a visit.

**Initial risk:** [For the CSO to assign.] Draft estimate: Major, Low: **3**.

**Further controls:**
- **A report of stages finished without a visit,** for admins, from the audit's `withoutVisit`, reviewed on a set schedule. Protective; **proposed**; **before first live use**; the review is transferred.
- **Name the patient, the stage and the appointment** (or "no attended appointment") in the finish and bypass confirmations. Protective; **proposed**; before first live use.
- **Ask for a reason when a suggested visit is skipped too,** so skipping is deliberate. Protective; **proposed**; later.
- **When a definition's visit requirement is changed,** list the open pathways with that stage, as H-14's control does for template corrections. Protective; **proposed**; later.
- **Clinical sign-off of which stages need a visit,** as part of template governance. Information; transferred; before first live use.

**At first live use:** Draft: Major, Very low: **2**.

**Residual risk:** Draft: Major, Very low: **2**.

**Owner:** [CSO]. **Status:** Open, draft for the CSO.

---

## H-33: An appointment is at the wrong time

**Description.** An appointment is booked, shown or passed on at a different time or day from the one intended: an hour out, on the wrong day, or in the wrong week.

**Causes:**
- **Time zones:** times are stored in UTC and shown in UK time. A conversion done wrongly, or a time read without its `Z`, would be an hour out in summer (as H-12 was).
- **Clock changes:** template times are UK clock times, turned into UTC for each date; done wrongly, blocks and free slots move by an hour after a clock change.
- **"Today" on the server:** server rendering runs in UTC, so between 23:00 and midnight UTC in summer the server's day and the browser's can differ (H-12's residual note). Home's day calendar or the Calendar page could open on the wrong day for that hour.
- **The wrong day or slot picked** in the booking dialog, or the wrong week open in the week view.
- **A reschedule** that moves the appointment, when the patient was told the old time (H-36).
- **A closed day that's wrong:** a bank holiday worked out wrongly, a one-off change (a coronation, a moved spring bank holiday) not added by hand, or a day closed by mistake, so a day the clinic is open can't be booked, or a closed one can. Bank holidays are England and Wales's only (decision 73).
- **A stale "book" that moves someone else's booking** (found in the review of PR #93): someone books a Requires Appointment appointment and tells the patient; a colleague whose list is out of date books it too, and the second request quietly moved it to their time, with nobody told.

**Effect.** The patient and the member of staff turn up at different times, or the patient is told the wrong time.

**Possible harm.** A missed or delayed appointment. One patient for a slip; if a conversion is wrong, every appointment on the days it affects, so multiple patients: Considerable.

**Existing controls** (in the design):
- **Times are stored and sent as UTC with a `Z`** (`UtcDateTimeConverter`, from PR #62), and the web app shows them in UK time (decision 3).
- **One place for UK time:** `UkTime` turns UK dates and clock times into UTC, and template times are turned into UTC for each date, so a 12:00 lunch is at 12:00 UK time on both sides of a clock change (decision 3).
- **The browser does no time zone arithmetic:** the calendar response gives each day's working start and end in UTC, and the browser steps through slots from them (decision 3). A UK working day never spans a clock change.
- **The server checks every time:** inside the working day, on a weekday that isn't a closed day, on a 5-minute boundary, start before end, and not in the past for a booking (decisions 2, 17 and 72), from one rule every command uses (`WorkingDays`). A walk-in must have started, within the last 30 days (decision 74).
- **Bank holidays are worked out from gov.uk's rules,** tested against the dates gov.uk published for 2025 to 2028, substitute days included, and added only when an admin asks; the list is on an admin page, by year, where a wrong day can be removed (decision 73).
- **Tests:** `CalendarProjection` across a clock change, in unit and integration tests (plan, section 7).
- **Messages say the day and time in words** ("Monday 23 November at 10:00", `UkTime`).
- **A booking says what it expects** (`expectedStatus`): booking an appointment someone else has already booked is a 409 ("Someone else has already booked this appointment"), not a move; moving one needs the caller to have seen it Booked (decision 46).

**Initial risk:** [For the CSO to assign.] Draft estimate: Considerable, Low: **2**.

**Further controls:**
- **Show the full date, with the day of the week, and the member of staff** in the booking and reschedule confirmations and on the appointment. Protective; **proposed**; before first live use.
- **End-to-end tests in a browser outside UK time,** and on the days of both clock changes. Protective; **proposed**; before first live use.
- **Use the UK date on the server for "today",** rather than the machine's (the to-do from H-12). Design; **proposed**; later.

**At first live use:** Draft: Considerable, Low: **2**.

**Residual risk:** Draft: Considerable, Very low: **2**.

**Owner:** [CSO], with the maintainer. **Status:** Open, draft for the CSO.

---

## H-34: The wrong patient or pathway is booked

**Description.** An appointment is booked for a different patient from the one intended, or linked to the wrong pathway or stage.

**Causes:**
- **The same identification limits as H-01:** name, date of birth, email and an optional NHS number that isn't traced; no duplicate check on name and date of birth.
- **Booking from an empty calendar slot or the New button** asks for the patient (and pathway and stage) to be chosen (decision 30), by name.
- **An appointment's patient can't be changed:** booking and rescheduling change only the time and member of staff (the plan's API, section 4), so a mistake must be cancelled and booked again; until then it looks real.

**Effect.** The wrong patient is called in, and the right one isn't. An Attended recorded on the wrong patient's appointment could satisfy that patient's required visit (H-32).

**Possible harm.** Delayed care for one patient, or a step done on the basis of a visit that was someone else's. Considerable.

**Existing controls** (in the design):
- **The patient is filled in when it's known:** booking from the patient page, the pathway page, a stage ("Book visit") or the Requires Appointment list (decision 30), and the app's own requests link to the right stage (decision 23).
- **One open appointment per stage** (decision 18): a second booking for a stage is refused, and the refusal points to the one there is.
- **Every change is in the appointment's history**, so a wrong booking can be traced (decision 19).
- **Transferred:** positive patient identification at the point of care.

**Initial risk:** [For the CSO to assign.] Draft estimate: Considerable, Medium: **3**.

**Further controls:**
- **Show the date of birth beside the patient's name** in the booking dialog's patient picker, in the confirmation, and on the appointment's dialog. Protective; **proposed**; before first live use.
- **The patient's details in the booking and appointment dialogs,** as H-01's patient banner shows them: once a patient is chosen, the dialog shows their name, date of birth and NHS number. Protective; **built** (with the patient banner from PR #69); to be verified by the CSO before first live use. The dialog for recording an appointment after the event shows them the same way (H-39).
- **A duplicate check on name and date of birth, and tracing NHS numbers with PDS** (H-01). Design; **planned** (step 9); later.

**At first live use:** Draft: Considerable, Low: **2**.

**Residual risk:** Draft: Considerable, Low: **2**.

**Owner:** [CSO]. **Status:** Open, draft for the CSO.

---

## H-35: An appointment is left without its member of staff

**Description.** A patient is booked with a member of staff who won't be there: their account has been deleted, they're on leave, or their calendar has changed under the appointment.

**Causes:**
- **Leave or training added by a template change** (a week template edit, or a day's template applied) over appointments already booked. It's allowed, and both show, flagged (decision 6), but nothing moves the appointments or tells the patient.
- **Absence that isn't in MedApp:** sickness, or leave recorded only in another system.
- **A day closes after appointments are booked on it** (a bank holiday or training day added late): they stay, as nothing is ever moved or cancelled by the app (decision 75), but the clinic may not be open for them.
- **A member of staff loses their role:** the staff picker lists everyone with a role (`GET api/calendar/staff`), so their calendar would drop out of the picker and become a 404, with their appointments on it. Now refused while they have upcoming Booked appointments (below).

**Effect.** The patient arrives and nobody is there to see them, or the appointment silently doesn't happen.

**Possible harm.** Delayed care; for a whole day's list, several patients. Considerable.

**Existing controls** (in the design):
- **An account with upcoming Booked appointments can't be deleted** until they're moved or cancelled (decision 21), **nor lose every role** (decision 48): both are refused with the same `HasUpcomingAppointments` error, from one check (`WhyCantStopBeingStaffAsync`).
- **A new block can't overlap an appointment** (decision 6), so leave added as a block forces the appointments to be moved or cancelled first.
- **Overlaps from template changes are shown, not hidden,** with the appointment on top and the block flagged (decision 6).
- **Appointments on a day that later closed are shown, flagged** on the Calendar page's grid: the day reads "Closed: Christmas Day" (in the team view's "Everyone free" column too) and each item on it says it's on a closed day (decision 75). Nothing new can be booked there. Lists, the pathway page and Home's day don't mark them.
- **Closing a day says what's already on it, and tells those staff:** the Add Closed Day dialog (and the bank holidays result) shows how many appointments, blocks and meetings are on the day and whose, before saving, and each member of staff with something on it gets a "Day closed" notification naming the day and what they have then, asking them to move or cancel what's still to come and tell the patients (decision 75). Protective; **built** (after the review of #119).
- **The member of staff is notified** when someone else books an appointment with them, moves one to them or away from them, or cancels one (decision 20), and sees their day's appointments on Home (decision 32).

**Initial risk:** [For the CSO to assign.] Draft estimate: Considerable, Low: **2**.

**Further controls:**
- **After a template change or a day's template is applied,** list the booked appointments it overlaps and notify the member of staff (as H-31). Protective; **proposed**; before first live use.
- **Refuse to remove the last role from someone with upcoming Booked appointments,** as account deletion does. Protective; **built** (decision 48, from the review of PR #93); to be verified by the CSO before first live use.
- **A local process for absence:** when someone is away unexpectedly, move or cancel their appointments and tell the patients (H-36). Information; transferred; before first live use.
- **Deactivate accounts instead of deleting them** (H-21). Design; **planned** (step 4); later.

**At first live use:** Draft: Considerable, Low: **2**.

**Residual risk:** Draft: Considerable, Very low: **2**.

**Owner:** [CSO]. **Status:** Open, draft for the CSO.

---

## H-36: A cancelled or moved appointment isn't passed on to the patient

**Description.** An appointment is cancelled or rescheduled in MedApp, but the patient isn't told, so they come at the old time, or don't come to the new one.

**Causes:**
- **MedApp contacts patients only if the organisation turns it on, and only on the channels each patient chose** (PR 26): telling the patient of a move or cancellation by text or email happens only when staff tick "Tell the patient" (moving a Booked appointment to another time or room, cancelling one still to come, or cancelling a clinic session); moving an appointment to another member of staff alone, or a drag on the calendar, sends nothing. Otherwise telling the patient is the organisation's own process.
- **Nothing in MedApp asks whether the patient has been told,** when an appointment is cancelled or moved.
- **Notifications go to staff only:** the member of staff whose appointment changed, and only when someone else changed it (decision 20).
- **Cancelling a pathway leaves its Booked appointments alone** (decision 26), deliberately, so someone can tell the patient; if no one acts on the list on the pathway page, they stay booked for a pathway that won't go ahead.

**Effect.** The patient makes a wasted journey, misses a rescheduled appointment, or is seen for a pathway that has been cancelled.

**Possible harm.** Delayed care, and distress, for one patient; worse for patients who are frail or travel far. Considerable in the worst credible case.

**Existing controls** (in the design):
- **The app never cancels a Booked appointment by itself** (decision 26); only a person does, so the person who cancels knows.
- **A cancellation needs a reason** (decision 16), and every cancellation and reschedule is in the appointment's history with who and when (decision 19).
- **The pathway page lists a pathway's Booked appointments** (decision 26), so they're visible when it's cancelled.
- **Transferred:** the organisation's own process for contacting patients about their appointments.

**Initial risk:** [For the CSO to assign.] Draft estimate: Considerable, Medium: **3**.

**Further controls:**
- **Ask "has the patient been told?"** when a Booked appointment is cancelled or moved, record the answer, and list the ones not yet told. Protective; **proposed**; before first live use.
- **A local process** for telling patients of every cancellation and change, and for the Booked appointments of a cancelled pathway. Information; transferred; before first live use.
- **Telling the patient by text or email** when a Booked appointment is moved or cancelled, or a session cancelled ("Tell the patient by text or email", ticked when the patient has a channel), with the message stored and its delivery shown on the appointment; and reminders before each appointment, which show a moved appointment's new time. Protective; **built** (PR 26: `BookAppointmentHandler`, `CancelAppointmentHandler`, `CancelClinicSessionHandler`, `PatientRemindersJob`), off unless the organisation turns it on, and only for patients who chose a channel. Not for a cancelled pathway's Booked appointments, which staff cancel one by one (and can tell the patient then), nor for a closed day's (`docs/todo.md`).

**At first live use:** Draft: Considerable, Low: **2**.

**Residual risk:** Draft: Considerable, Very low: **2**.

**Owner:** [CSO]. **Status:** Open, draft for the CSO.

---

## H-37: A note from the notepad is added to the wrong patient's pathway

**Description.** A member of staff jots something about a patient in their notepad (during a call or a clinic), then uses Add Note to Pathway, and it's added as a note on a different patient's pathway, or on the right patient's wrong pathway.

**Causes:**
- **The notepad isn't tied to a patient.** It's one per member of staff, open beside every page, so what's in it can be about anyone, and the pathway page open beside it may not be the patient it's about.
- **Two pathway pages open at once** (a half view), each a possible target.
- **Several patients' jottings in one notepad,** where all of it is added rather than the part about this patient.
- **A wrong note stays until an admin deletes it:** staff can't delete notes (clinical record integrity); they ask an admin to, with a reason, and until one decides, the note is visible, marked "Deletion requested". Deleting it wrongly is H-38.
- **The same words in two patients' jottings** ("obs stable", "bloods sent", "call back"): taking the added text out of the notepad could take out the wrong copy, leaving the other patient's line short and this patient's still there, ready to be added again elsewhere. Found in the review of #110: the first build searched the notepad for the selected text and removed the first copy it found.
- **The notepad in a half view** (`/notepad` beside a page, branch `feature/notepad-window`) offers the pathway page beside it, so the target is the page in the other half, which may not be the patient the jotting is about. (A separate notepad window, which would have offered pathway pages from other windows out of view, was tried on that branch and removed before merging, by the product owner's decision.)

**Effect.** A clinical note about one patient sits on another patient's record: the wrong patient may be treated on it, the right one's record misses it, and it's a disclosure.

**Possible harm.** A clinical decision made on someone else's information, or information missed; distress and a breach of confidentiality. Considerable.

**Existing controls:**
- **A confirmation before anything is added** (`AddToPathwayDialog`), showing the patient as the patient banner does (`SURNAME, Forename`, date of birth and age, NHS number), the pathway's title, and the exact text that will be added. Protective; **built**.
- **With two pathways open, it asks which,** naming each by its side (Left, Right), title and patient, and nothing is chosen until the member of staff chooses: Add Note stays disabled until then. Protective; **built**.
- **The notepad in a half view uses the same confirmation** for the pathway page beside it (the signed-in page's, as the docked notepad offers): the patient as the banner shows them, the pathway's title and the text. With no pathway page open, Add Note to Pathway stays disabled. Protective; **built** (`NotepadProvider`, `NotepadPage`).
- **Only the selected text is added when there is a selection,** so one patient's part can be added on its own. It's taken from the editor's own selection, as Markdown, so it keeps its formatting (bold, a whole list item's bullet) and the confirmation shows exactly what will be saved. Protective; **built**.
- **"Remove it from the notepad" takes out exactly what was selected,** by its position in the editor (the editor deletes that range, as pressing Delete would), never by searching for the same words, so another line with the same text is never touched. It starts ticked, so the text doesn't linger there to be added again elsewhere. **When it can't be done exactly,** it says so rather than guessing: if the editor can't say where the selection is, the option is off and disabled in the confirmation, with the reason shown; if the notepad changed while the note was being added, nothing is taken out, and the pane says so, on the pane and to screen readers, so the member of staff removes it themselves. Protective; **built** (`RichTextField`'s `selection()`, `NotepadPane`; tested with the same words on two lines).
- **The note is added by the member of staff, with the time, audited** (`NoteAdded`), and shows on the pathway page at once, under the patient banner; its author can edit it. Detective; **built**.
- ~~**The notepad says it's for jottings, and that anything about a patient belongs in their record.** Information; built.~~ **Removed** on 17 November 2026, by the product owner's decision (branch `feature/notepad-window`), with the notepad's prompt. The confirmation above stays the main control; the staff privacy notice still says what the notepad is for. The draft estimates below rest on the confirmation, not on that text, so they're unchanged; for the CSO to confirm.
- **Transferred:** staff check the patient before writing in their record, as for any note.

**Initial risk:** [For the CSO to assign.] Draft estimate: Considerable, Medium: **3** (without the confirmation).

**Further controls:**
- **Delete a note added to the wrong record,** by an admin, with a reason, directly or when a member of staff asks: soft, so it's hidden from the pathway page but kept, with its versions, in the audit, the admins' timeline and the export. Corrective; **built** (`feature/note-deletion`; its own hazard is H-38).

**At first live use:** Draft: Considerable, Low: **2**.

**Residual risk:** Draft: Considerable, Low: **2**.

**Owner:** [CSO]. **Status:** Open, draft for the CSO.

## H-38: A pathway's note is deleted wrongly

**Description.** An admin deletes a note on a pathway that should have stayed: the wrong note (another note on the same pathway, or the right words on the wrong patient's pathway), or a note that was on the right record and is still clinically needed. They delete it directly, or by approving a member of staff's request to delete it.

**Causes:**
- **A request names the wrong note.** A member of staff asks for a note to be deleted from the note's own row, but picks the wrong row (two similar notes, "bloods sent" twice), and the admin approves it.
- **The admin opens the wrong request,** or approves without reading the note or the patient, trusting the requester's reason.
- **A note looks wrong but isn't:** a note that seems to belong to another patient (a relative mentioned by name, a shared surname) is still about this patient.
- **Deleting directly,** on the pathway page, picks the wrong note's Delete.
- **The note changes after the admin reads it:** its author edits it (into the right result, say) after the request was made, or while the admin has the dialog open, and the admin deletes words they never saw.
- **A note still needed for care** is deleted as "duplicate" or "not relevant", and whoever reads the record next misses it.

**Effect.** A clinical note is hidden from the pathway page, so staff working on the pathway don't see it, and the wrong note may stay where it was.

**Possible harm.** A clinical decision made without information that was in the record (a result, an allergy mentioned in passing, a follow-up agreed with the patient). Considerable.

**Existing controls:**
- **Only admins delete notes** (`[Authorize(Roles = "Admin")]` on the delete and decision endpoints). Staff can only ask, and the admin decides. Protective; **built**.
- **A reason is required,** a fixed choice (wrong patient, written in error, duplicate, other) with an optional detail (required for "other"), for a direct delete and for a request; the admin may keep or change the requester's. Protective; **built**.
- **Only the version the admin read is deleted:** a deletion carries the note's version the admin saw, and a note edited since is refused (409), so they read it again first. The request's dialog says when the note was edited, and warns when that was after the request was made (the reason may be about other words). Protective; **built** (review of #115).
- **A confirmation before anything is deleted:** the request's dialog shows the note's text as it reads now, the patient as the patient banner shows them (`SURNAME, Forename`, date of birth and age, NHS number), the pathway's title, who asked, when and why, with **Delete note** and **Keep note**. A direct delete asks for the reason in a confirmation dialog that shows the note and the patient as the banner shows them (as the request's does), so the admin checks the patient even with two pathway pages side by side. Each note's buttons are named for their note ("Delete note by s.nurse, 17 Nov 2026, 10:32"). A request's link opens only on its own pathway's page. Protective; **built**.
- **Deleting is soft:** the note is hidden, not erased. Its row and every version are kept, with who deleted it, when and why; the audit log records the deletion (`NoteDeleted`); admins see it on the patient's timeline, with the text it had and the reason; and the patient's export lists it as deleted. So a wrong deletion can be found and its text recovered. Detective; **built**.
- **The requester is told the outcome** ("Note deleted" or "Note kept"), so a deletion they didn't expect, or a request still waiting, is noticed. Detective; **built**.
- **The note's author is told** ("Your note was deleted"), whether it was deleted directly or from someone else's request (unless they deleted it, or asked for it, themselves), so a wrong deletion has a second witness who knows what the note said, and a note on the wrong patient can be added to the right one (H-37's corrective control). Detective; **built** (review of #115).
- **One request per note at a time,** and a request already decided by another admin shows its outcome instead of the buttons (deciding it again is refused), so two admins can't act on it twice. Protective; **built**.
- **A restricted or anonymised record refuses deletions and requests** (409), so a record under restriction stays exactly as it is. Protective; **built**.
- **Transferred:** admins read the note and check the patient before deleting, as for any change to a record.

**Initial risk:** [For the CSO to assign.] Draft estimate: Considerable, Low: **2**.

**Further controls:**
- **Restore a deleted note,** by an admin, with a reason, so a wrong deletion can be undone on the page rather than copied back from the timeline. Corrective; **planned** (`docs/todo.md`); later.
- **A list of waiting requests,** so none is left undecided once its notification has been read. Protective; **planned** (`docs/todo.md`); later.

**At first live use:** Draft: Considerable, Low: **2**.

**Residual risk:** Draft: Considerable, Very low: **2**.

**Owner:** [CSO]. **Status:** Open, draft for the CSO.

---

## H-39: A visit is recorded after the event that didn't happen, or on the wrong patient

**Description.** Staff record an appointment after the event (a walk-in, or one booked outside MedApp, decision 74), and it's wrong: the patient wasn't seen, or it's recorded on a different patient, pathway or stage, or with the wrong outcome.

**Causes:**
- **No booking came before it,** so nothing in MedApp shows the visit was expected: it's only as good as the memory or paper it's recorded from.
- **The patient is chosen by name** in the dialog (opened from the Calendar page's New menu), with the same identification limits as H-01 and H-34.
- **Recording one for a stage's visit in Requires Appointment** turns that visit into the recorded one (decision 77); an Attended one then satisfies a Required stage (H-32), so the stage can be finished as though the patient had been seen.
- **Recorded days later,** the details (the time, the member of staff, whether they came) are easier to get wrong.
- **A visit from before the day the pathway was created, or for a stage not yet reached,** would satisfy that stage: refused (409), so it's recorded without the pathway or for the right stage (decision 74). A visit earlier on the day the pathway was created is allowed, as the pathway is often created after the clinician has seen the patient.
- **Races:** a visit booked for the stage at the same moment as the walk-in (re-checked under the calendar lock, but another person's booking takes their own lock), or the stage becoming current at that moment (a needless Requested visit appears beside the walk-in). Each window is milliseconds and loses nothing; documented in the plan (decision 76).

**Effect.** The record says a patient was seen when they weren't, or another patient's record says so. A Required stage can be finished on the strength of it, and the patient who needed the visit isn't followed up (H-30).

**Possible harm.** A pathway goes ahead without the visit it needed, or care is missed for the patient actually seen, or a disclosure on the wrong record. One patient; Major in the worst credible case, as H-32.

**Existing controls** (built on `feature/closed-days-walk-ins`):
- **The patient as the patient banner shows them** (`SURNAME, Forename`, date of birth and age, NHS number) in the dialog once chosen, as for booking (H-34). From a stage's visit, the patient, pathway and stage are filled in and can't be changed. Protective; **built**.
- **The dialog says it's for an appointment that has already happened, and that it's recorded with your name;** the outcome has no default, so Attended or Did not attend is a choice. Protective; **built**.
- **Only within 30 days, and only once it has started,** on a weekday, inside the working day: the server refuses anything else (400, decision 74). A closed day is allowed, as what happened, happened (a day closed part-way through, or later), and the visit shows flagged on it. Protective; **built**.
- **Who recorded it, and when,** is in the appointment's history (`RecordedAfterEvent`), on the patient's timeline and on the appointment ("Recorded after the event by …"); the member of staff it's with is notified when someone else recorded it. Detective; **built**.
- **A stage's visit already Booked can't be bypassed** by recording another: that's a 409 saying to record the booked one's outcome, or cancel it (decision 77). Protective; **built**.
- **The restriction guard** applies as for any appointment change: a restricted or anonymised record can't gain one (409). Protective; **built**.
- **The outcome can be corrected** (Attended to Did not attend and back) as for any appointment. Corrective; **built**.
- **A wrong walk-in can be withdrawn as entered in error** (it didn't happen, or it's on the wrong patient), with a reason, by whoever recorded it within 30 days of it, or by an admin at any time: it leaves the calendar, stops counting as a stage's visit, and stays in the history with the reason (admins see it); if it was a stage's visit, the stage's visit goes back to Requires Appointment when the stage would ask for one (not on a restricted record, which can't gain appointments), or the person is told why not. Corrective; **built** (after the review of #119; admins at any time after its follow-up review).
- **A finished stage left without its visit is told to the people who can act on it:** when the walk-in withdrawn was the only attended visit of a finished stage that requires one, the pathway's assignees and every holder of `FinishWithoutVisit` (every admin until PR 24; not whoever withdrew it) get a "Stage visit withdrawn" notification linked to the pathway, saying which stage and that someone who can unlock stages can unlock it and book the visit, as finishing it again after an unlock doesn't ask for the visit. Without it, only the person withdrawing it was told, in a dialog they could close. Detective; **built** (after the follow-up review of #119). It relies on someone reading the notification: it doesn't mark the stage on the pathway page, which is a further control to consider.
- **The visit's own notes are kept** when a walk-in takes over a stage's waiting visit; the walk-in's notes are added after them, and the dialog shows them first. Protective; **built**.
- **Transferred:** positive patient identification, and recording visits as they happen.

**Initial risk:** [For the CSO to assign.] Draft estimate: Major, Low: **3** (without the controls).

**Further controls:**
- **A list of appointments recorded after the event,** for admins, reviewed on a set schedule, beside the stages finished without a visit (H-32). Protective; **proposed**; before first live use.
- **Ask for the patient's details to be confirmed** (date of birth) before saving a walk-in for a Required stage. Protective; **proposed**; later.

**At first live use:** Draft: Major, Very low: **2**.

**Residual risk:** Draft: Major, Very low: **2**.

**Owner:** [CSO]. **Status:** Open, draft for the CSO.

---

## Referrals, triage and waiting times: draft hazards for the CSO

H-40 to H-44 come from the design of Phase 2 of the community specialist centre (`docs/centre-phase2-plan.md`), drafted **before the features are built**, so the build can include their controls. "Decision n" refers to section 1 of that plan. In short:
- **Referrals:** staff register each referral the centre receives (the patient, the referring practice, when it arrived, the specialty and priority asked for, the letter), and it waits in an inbox.
- **Triage:** a clinician with the triage permission in that specialty accepts it into a pathway (choosing the template and the priority), rejects it or redirects it, with a reason.
- **The waiting-time clock:** counts the days since the referral arrived, less pauses staff record, against the organisation's target for the specialty and priority, and shows a breach.

**Ratings:** left for the CSO to assign. Each entry gives a draft estimate as a starting point.
- **Existing controls:** there are none yet, because nothing here is built. Today the centre handles referrals outside MedApp.
- **Further controls:** the plan's controls are listed as **planned (Phase 2)**, before first live use.
- **Check each control** against the code once it's built.

---

## H-40: A referral is lost or delayed before triage

**Description.** A referral the centre received isn't triaged, or is triaged late, so the patient isn't seen when they should be.

**Causes:**
- **Never registered:** the letter arrives by post or email and is set aside, or registering it is refused (a restricted patient's record can't gain a referral, decision 44).
- **Registered under the wrong specialty,** so it waits in an inbox nobody of the right specialty reads.
- **Nobody can triage the specialty:** no member of it has the triage permission (a new specialty, someone away, a role edited).
- **The inbox isn't read,** or is read without sorting by wait.
- **Closed by mistake:** withdrawn or marked entered in error.

**Effect.** The patient waits without anyone deciding about their referral; the referrer believes it's in hand.

**Possible harm.** A delayed diagnosis or treatment (for an urgent skin referral, a delayed cancer diagnosis). One patient; Major in the worst credible case.

**Existing controls** (built on branch `feature/referrals`, Phase 2's PR 5; to be verified by the CSO):
- **The inbox:** the Referrals page opens on every referral awaiting triage, **oldest received first**, each showing "received N days ago", and their count shows in the side navigation (`ReferralsListPage`, `AppSideNav`, `GET api/referrals/awaiting-triage/count`). Protective.
- **No silent closing:** a referral leaves Awaiting triage only by a named person's recorded action: a triage outcome (accepted into a pathway, rejected with a reason, redirected elsewhere with where to and a detail), withdrawing it or marking it entered in error (each with a detail). Every one has a confirmation that shows the patient as the banner does; its history records who, when and the outcome, and the referral stays (`ReferralWorkflow`, `ReferralChange`, the triage and close dialogs). Nothing closes one automatically. Protective.
- **Triage** (branch `feature/triage`, PR 6): a clinician with the triage permission who belongs to the referral's specialty accepts, rejects or redirects it (`AcceptReferralHandler`, `RejectReferralHandler`, `RedirectReferralHandler`); a referral redirected to another specialty here stays awaiting triage, in that specialty's inbox, with the same date received. **Whoever registered it is told of every outcome** (`ReferralTriaged`, a fixed title and the outcome by name), so the booking team can tell the referrer. Protective.
- **The Specialties page warns when nobody can triage a specialty:** each specialty says how many of its members hold the triage permission (`SpecialtyResponse.TriagerCount`), and the page names those with none, so an admin can give someone the role or the membership. Protective.
- **Archiving a specialty with referrals awaiting triage is refused** (409, saying how many), so none is stranded in an archived specialty (`SpecialtyRules`). Protective.
- **A restricted record:** registering is refused (409) with a message telling staff to give the referral letter to an admin now, so it isn't lost; for an anonymised record, to check the patient, then give the letter to an admin (`ReferralWorkflow.RestrictedRegisterMessage`, `AnonymisedRegisterMessage`). Restricting a patient counts their referrals awaiting triage, with their open pathways and appointments, and asks for confirmation (`ConfirmOpenPathways`). Information and protective.
- **A wrong specialty can be corrected** by editing the referral while it awaits triage; the history records the old and new specialty. Corrective.
- **Someone who can't triage is told why** on the referral page (no permission, or not a member of its specialty: `ReferralWorkflow.WhyCantTriage`), and the server refuses them (403). Information.

**Initial risk:** [For the CSO to assign.] Draft estimate: Major, Medium: **3**.

**Further controls** (planned, Phase 2, before first live use unless marked):
- **The inbox's days waited and breach badge,** longest wait first, from the waiting-time clock (PR 7, branch `feature/waiting-clock`: **built**), and filtered to the viewer's specialties (PR 8). Detective.
- **Transferred** (`dcb0160-deploying-organisations.md`):
  - a daily check of the inbox;
  - a procedure for referrals received when MedApp is unavailable;
  - making sure every specialty has a triager.
- **A monitoring rule for referrals not triaged in N days.** Protective; planned for Phase 4 (it needs a scheduled job); later.

**At first live use:** Draft: Major, Very low: **2**.

**Residual risk:** Draft: Major, Very low: **2**.

**Owner:** [CSO]. **Status:** Open, draft for the CSO: registering, the inbox and its count, triage with the registrant told, closing with a detail, the restricted message, the archiving guard and the Specialties page's warning are built; the clock isn't.

---

## H-41: A referral is triaged into the wrong pathway, priority or patient

**Description.** The triage decision recorded isn't the one the clinician meant, or is made on the wrong patient's referral: the wrong pathway template, a lower priority than intended, or a referral registered on the wrong patient.

**Causes:**
- **Similar names:** templates with similar names, or two patients with the same name (H-01).
- **The letter not read:** the clinician decides from the typed reason without the letter, or no letter is attached.
- **A priority accepted without thought:** a default priority would invite this, so the plan prefills none (decision 19).
- **Two clinicians at once** on one referral.

**Effect.** The patient follows the wrong pathway, or waits against a longer target than their condition needs, or another patient is booked in their place.

**Possible harm.** A delayed or inappropriate investigation or treatment. One patient; Major in the worst credible case.

**Existing controls** (built on branch `feature/referrals`; to be verified by the CSO):
- **The patient banner** on the referral page, the Edit Referral page, the Register Referral page once the patient is chosen, and in the withdraw and entered-in-error confirmations (`PatientBanner`). Protective.
- **The letter beside the referral:** its page shows the letter attached to it, or says plainly "No letter attached yet.", and the inbox has a Letter column. Protective.
- **Two changes at once can't both succeed:** every edit and closing carries the version it started from, and the row version catches two saves at once (409, `ReferralWorkflow.ChangedMeanwhileMessage`). Protective.
- **A referral on the wrong patient** is marked entered in error, with a detail, and stays in the record, closed. Corrective.
- **Triage** (branch `feature/triage`, PR 6):
  - **The patient as the patient banner shows them** in every triage confirmation (accept, reject, redirect), with the specialty and, for accepting, the template and priority chosen (`AcceptReferralDialog`, `RejectReferralDialog`, `RedirectReferralDialog`). Protective.
  - **Choices kept narrow, nothing prefilled:** only the specialty's own templates are offered, by name and never ranked, and the server refuses another specialty's (400, `ReferralWorkflow.TemplateNotInSpecialtyMessage`); the priority and the template start empty and must be chosen, with the referrer's request shown beside the priority as text; the pathway's target date is the waiting-time clock's for the priority chosen (PR 7), not typed. Protective.
  - **Only a clinician of the specialty decides:** the triage permission and membership of the referral's specialty, checked on the server (403). Protective.
  - **Who decided, when and why** is recorded on the referral and in its history, and on the timeline; the pathway's Created audit entry names the referral, and the pathway page links back to it. Two decisions at once can't both succeed: the row version, and the second is told who decided and how (409, `ReferralWorkflow.AlreadyTriagedMessage`); an acceptance that loses leaves no pathway. Detective and protective.
  - **The patient's other referrals** are listed on the referral page. Protective.

**Initial risk:** [For the CSO to assign.] Draft estimate: Major, Low: **3**.

**Further controls:**
- **Correction:** a wrong acceptance is corrected by cancelling the pathway; the priority can be changed on the pathway. Corrective; available.
- **Transferred:** positive patient identification, and clinical review of triage.

**At first live use:** Draft: Major, Very low: **2**.

**Residual risk:** Draft: Major, Very low: **2**.

**Owner:** [CSO]. **Status:** Open, draft for the CSO: the triage controls are built (branch `feature/triage`), to be verified.

---

## H-42: A paused waiting-time clock is forgotten

**Description.** A clock is paused for a patient-initiated delay (the patient chose to wait, or is unavailable), and nobody comes back to it, so the patient drops off the waiting list's attention.

**Causes:**
- **No follow-up:** the person who paused it leaves or forgets.
- **A far-off review date:** set too far ahead.
- **A false breach picture:** a paused clock shows no breach, so the list looks healthy.

**Effect.** The pathway sits without progress, and the waiting figures hide it.

**Possible harm.** A delayed investigation or treatment. One patient; Considerable in the worst credible case.

**Existing controls** (built on branch `feature/waiting-clock`, Phase 2's PR 7; to be verified by the CSO):
- **A review date is required,** from tomorrow to at most 90 days ahead, checked on the server (`PauseClockValidator`); the pause records its reason (a fixed, patient-initiated list), an optional detail (required for Other), who paused it and when. Protective.
- **While paused, the pathway's target date is the review date** (`WaitingClock.PathwayTargetOn`, `PathwayChange.FollowClock`), so a missed review shows as Overdue on every list and on My Worklist, and the monitoring rule for a changed target date fires. Detective.
- **The Paused clocks view** on the Referrals page lists every open pause by review date, with past ones flagged (`paused=true`, `sort=reviewOn`; `WaitingClockResponse.IsReviewOverdue`). Detective.
- **Only patient-initiated reasons, only after acceptance;** On hold doesn't pause the clock; one open pause at a time (a unique index); a pause starts and ends today, never backdated. Design.

**Initial risk:** [For the CSO to assign.] Draft estimate: Considerable, Medium: **3**.

**Further controls:**
- **A notification or monitoring rule when a review date passes.** Protective; planned for Phase 4 (it needs a scheduled job); later.

**At first live use:** Draft: Considerable, Low: **2**.

**Residual risk:** Draft: Considerable, Low: **2**.

**Owner:** [CSO]. **Status:** Open, draft for the CSO: the controls are built (branch `feature/waiting-clock`), to be verified; the notification when a review date passes is later.

---

## H-43: A breach of a waiting-time target goes unnoticed, or the target is wrong

**Description.** A referral or pathway passes its target and nobody acts, or the target itself is wrong, so a breach isn't shown when it should be.

**Causes:**
- **Unread lists:** breaches are shown, never sent, and nobody looks at the lists.
- **Wrong targets:** a specialty's targets entered wrongly (too long), or a priority set too low (H-41).
- **A target changed later:** a specialty's targets changed after clocks started, which don't follow (by design, decision 30).
- **Waiting wrongly paused:** a pause used for the centre's own delay instead of a patient-initiated one.

**Effect.** Long waits accumulate unseen.

**Possible harm.** A delayed diagnosis or treatment. Several patients; Considerable in the worst credible case.

**Existing controls** (built on branch `feature/waiting-clock`, Phase 2's PR 7; to be verified by the CSO):
- **Breached** on the referral lists and the pathway lists (a Wait column, "Day n of target"), on the referral page and the pathway's Referral and wait card; sorting by days waited and a "Breached only" filter (`PathwaySearch`, `ReferralSearch`). A clock that breached before a pause stays breached. Detective.
- **The target's source is visible:** each clock shows its target days and date; each specialty's targets are on the Specialties page; every re-read of a clock's target (the triager's priority, a redirection, a correction, a new priority) is in the referral's history with the old and new days. Information.
- **One rule, in one class, with its own tests** (`WaitingClock`, `WaitingClockTests`): whole UK days, the clock changes, same-day pauses, breaches before a pause. Protective.
- **Pause reasons are a fixed list** of patient-initiated reasons; On hold doesn't pause. Design.

**Initial risk:** [For the CSO to assign.] Draft estimate: Considerable, Medium: **3**.

**Further controls:**
- **Transferred:**
  - the organisation sets and reviews its targets;
  - it reviews its waiting list on a set schedule.
- **Breach notifications, and reports by specialty.** Protective; planned for Phases 4 and 5; later. They stay administrative: never derived from clinical values (`intended-purpose-and-device-decision.md`).

**At first live use:** Draft: Considerable, Low: **2**.

**Residual risk:** Draft: Considerable, Low: **2**.

**Owner:** [CSO]. **Status:** Open, draft for the CSO: the lists, sort, filter and visible targets are built (branch `feature/waiting-clock`), to be verified; breach notifications are later.

---

## H-44: A duplicate referral leads to two pathways for one need

**Description.** The same referral is registered twice (sent twice, or by post and email), and both are accepted, so the patient has two pathways for one problem.

**Causes:**
- **One referral, two copies:** sent twice, or by post and by email.
- **A patient recorded twice** (H-01).
- **Two people at once:** two coordinators register the same letter.

**Effect.** Duplicate appointments, or one pathway discharged while staff think the other covers the patient, or results filed on the wrong one.

**Possible harm.** Confusion about the patient's care, and a missed step if each pathway is assumed to be the other's. One patient; Significant in the worst credible case.

**Existing controls** (built on branch `feature/referrals`; to be verified by the CSO):
- **The duplicate check:** registering looks for the patient's open referrals to the same specialty (awaiting triage, or accepted with the pathway still open) and refuses (400) unless `ConfirmPossibleDuplicate` is set; the form shows each one inline (when it was received and its status, never its text) and asks for the confirmation (`RegisterReferralHandler`, `ReferralWorkflow.PossibleDuplicateMessage`). Protective.
- **Correction:** a duplicate registered by mistake is marked entered in error, with a detail, after a confirmation showing the patient. Corrective.
- **A Duplicate rejection reason** at triage (`ReferralRejectionReason.Duplicate`), and the referral page lists the patient's other referrals beside the decision (branch `feature/triage`). Protective.
- **The patient page's Referrals card** lists the patient's referrals, so a second one is visible beside the first. Detective.

**Initial risk:** [For the CSO to assign.] Draft estimate: Significant, Medium: **2**.

**Further controls** (planned, Phase 2's triage, PR 6, before first live use):
- **Two people registering the same letter at the same moment** both pass the check, which reads before either saves; the second shows on the patient's Referrals card and is corrected as entered in error, or rejected as a duplicate at triage. Accepted.

**At first live use:** Draft: Significant, Low: **2**.

**Residual risk:** Draft: Significant, Low: **2**.

**Owner:** [CSO]. **Status:** Open, draft for the CSO: the duplicate check and its confirmation, and the Duplicate rejection reason at triage, are built.

---

## The centre's Phases 3 to 5: draft hazards for the CSO

H-45 to H-64 come from the design of Phases 3, 4 and 5 of the community specialist centre (`docs/centre-phases-3-5-plan.md`), drafted **before any of the features is built**, so each feature's PR can build its controls with it. "Decision n" and "PR n" refer to sections 1 to 4 and section 6 of that plan. In short:
- **Phase 3 (H-45 to H-50):** specimens tracked from the stage that took them to the lab and back; each lab report (or a report from elsewhere) becomes a **result** a named clinician signs off; **letters** written from templates, approved, rendered as PDFs and sent to the GP practice, the patient or others.
- **Phase 4 (H-51 to H-57):** pathways with optional stages and branches; sites, rooms and working hours; clinic sessions; scheduled jobs; reminders to patients by text and email, and the follow-up of missed appointments; retention classes.
- **Phase 5 (H-58 to H-64):** the MDT list and its outcomes; consent records; outcome questionnaires; video and phone appointments; adapters for NHS systems and a lab's system; several organisations in one deployment; a real malware scanner.

Throughout, the plan keeps the device line (decision 69): MedApp records what clinicians decide, and reads, scores, flags or suggests nothing from clinical content (`intended-purpose-and-device-decision.md`, the reassessments for Phases 3 to 5).

**Ratings:** left for the CSO to assign. Each entry gives a draft estimate as a starting point.
- **Existing controls:** none when drafted, because nothing here was built. As each feature's PR is built, its entries list what it built as existing controls, for the CSO to verify: so far specimens (PR 13, branch `feature/specimens`) in H-45 and H-47. Otherwise the centre does this work outside MedApp today.
- **Further controls:** the plan's controls are listed as **planned**, with the PR that builds them. They're built with their feature, so "at first live use" here means the feature's first live use: a feature mustn't be used with patients before its controls are built and verified.
- **Check each control** against the code once it's built, and update the entry in the feature's PR.

---

## H-45: A specimen is lost, or its report never comes back

**Description.** A specimen taken at a stage never reaches the lab, is lost or rejected there, or its report is never returned, and nobody notices that a result is missing.

**Causes:**
- **Lost in transit,** or never sent: the specimen is recorded as taken and left in the clinic.
- **Rejected by the lab** (unlabelled, leaked, insufficient) and the clinic isn't told, so the patient isn't asked back.
- **The report goes elsewhere,** or the lab never reports, and the specimen sits as Sent or Received.
- **The pathway moves on** without the result, for example to discharge.

**Effect.** A test the clinician meant to happen doesn't, and everyone assumes the result is normal or still coming.

**Possible harm.** A delayed or missed diagnosis (for a skin biopsy, a missed cancer). One patient; Major in the worst credible case.

**Existing controls** (built on branch `feature/specimens`, Phase 3's PR 13; to be verified by the CSO):
- **The lab worklist** (`/lab`, `GET api/specimens/search?view=`, `SpecimenSearch`): **To send** (taken, not sent), **Expected** (sent, not received), **In the lab** (received, not reported), **Overdue** (open past taken plus the specimen type's expected days), **Rejected and lost** (the last 30 days) and **Reported** (the last 7 days), each oldest first, and All (newest first, by pathway, patient or lab) (decision 90). It opens on Expected for the lab's staff and To send for others. Detective.
- **Turnaround and Overdue at lab shown** on each specimen: days and hours from step to step, when the report is expected, and an "Overdue at lab" mark once it's late (decision 88; `specimenTimes.ts`, `SpecimenPage`). Date arithmetic only. Information.
- **The last stage can't be finished while a specimen is open** (taken, sent or received): a 409 naming each by number, type and status (decisions 73 and 89; `StageGates.WhyCantFinish`, called by `FinishPathwayStageHandler`), so a pathway can't be discharged with a report still to come. Protective.
- **Lost is recorded with a detail** (required), and **rejected by the lab with a reason** (a fixed choice: unlabelled, mislabelled, leaked, insufficient sample, wrong container, or other with a detail); either way **the requesting clinician and whoever took it are told** ("Specimen lost", "Specimen rejected"), unless they made the change themselves (`SpecimenNotifications`). A rejected or lost specimen is closed and final; taking another is a new specimen, and the specimen's page lists the other specimens its stage took (decision 83). Protective.
- **Lab steps work in any pathway status,** so a report is never stuck behind a pathway on hold or closed (decision 85; `SpecimenWorkflow`); the report is attached to the pathway, or to the patient if the pathway no longer takes files. Design.
- **A restricted record can still finish its lab steps:** a specimen already taken can be sent, received, rejected, reported, marked lost or entered in error; only new specimens and edits are refused (decision 75). Design.
- **Anonymising a record is refused while a specimen is open,** so its report can't arrive at a record that can no longer take it. Protective.

**Initial risk:** [For the CSO to assign.] Draft estimate: Major, Medium: **3**.

**Further controls** (before the feature's first live use unless marked):
- **Results sign-off** (PR 14, built on branch `feature/results-signoff`): each report becomes a result its requesting clinician is told of and signs off, so a report that comes back is also seen (H-46). Protective.
- **A service alert** for specimens N days past their expected turnaround. Protective; planned, Phase 4 (PR 25); later.
- **Transferred** (`dcb0160-deploying-organisations.md`): a daily check of the lab worklist's Expected and Overdue views (and To send, for specimens left in the clinic), and a local process for chasing the lab.

**At first live use:** Draft: Major, Very low: **2**.

**Residual risk:** Draft: Major, Very low: **2**.

**Owner:** [CSO]. **Status:** Open, draft for the CSO: the worklist, turnaround, the last-stage gate and the lost and rejected notifications are built; results sign-off and the service alert aren't.

---

## H-46: A result is never seen or acted on

**Description.** A report comes back and is filed, but no clinician reads it or acts on it.

**Causes:**
- **Nobody responsible:** the requesting clinician has left, lost the permission, or is away.
- **A long list:** results awaiting sign-off build up and old ones are overlooked.
- **The pathway moves on** before the result is read.
- **Signed off without action:** the clinician signs it off but the action recorded isn't done.

**Effect.** An abnormal result isn't acted on.

**Possible harm.** A delayed or missed diagnosis or treatment. One patient; Major in the worst credible case.

**Existing controls** (built on branch `feature/results-signoff`, PR 14, for the CSO to verify): the named responsible clinician and "Result ready"; the results list with Awaiting my sign-off and its side-nav count, Awaiting sign-off (everyone) and Needs a clinician, oldest first with age in days; the `WaitsForResults` and last-stage gates (`StageGates.WhyResultsAwaiting`); sign-off records who and the action (none pre-selected), and tells the responsible clinician when someone else signs off; a restricted record's results can still be seen and signed off. The service alert (Phase 4) isn't built.

**Initial risk:** [For the CSO to assign.] Draft estimate: Major, Medium: **3**.

**Further controls** (planned, PR 14 unless marked; before the feature's first live use unless marked):
- **Every result has a named responsible clinician** (the specimen's requesting clinician, or the one named for an external report), who is told once ("Result ready") (decisions 91 and 92). Protective.
- **The results list and its count:** Awaiting my sign-off (with the count in the side navigation), Awaiting sign-off (everyone), oldest report first with its age in days (decision 94). Detective.
- **Needs a clinician:** a result whose responsible clinician no longer holds `SignOffResults`, or whose account has gone, is flagged on the list until someone takes it over (decision 92). Protective.
- **Gates:** a stage flagged `WaitsForResults`, and always the pathway's last stage, can't be finished while any of its results awaits sign-off (decision 95). Protective.
- **Sign-off records who and what:** the action the clinician took (none pre-selected) and its detail; a sign-off by someone other than the responsible clinician tells them (decision 93). Detective.
- **A restricted record's results can still be seen and signed off** (decision 75), so a restriction never strands one. Design.
- **A service alert** for results not signed off within N days. Protective; planned, Phase 4 (PR 25); later.
- **Transferred:** a daily check of results awaiting sign-off and Needs a clinician, and cover for clinicians who are away.

**At first live use:** Draft: Major, Very low: **2**.

**Residual risk:** Draft: Major, Very low: **2**.

**Owner:** [CSO]. **Status:** Open, draft for the CSO.

---

## H-47: A report is attached to the wrong patient or specimen

**Description.** A lab report, or a report from elsewhere recorded as a result, is filed against the wrong specimen or patient.

**Causes:**
- **The wrong specimen chosen** when reporting: similar numbers, two specimens from one patient, or a specimen found by name rather than number.
- **A mislabelled specimen:** the label on the pot isn't the patient's (H-01).
- **The wrong file uploaded** for the right specimen.
- **An external report** marked as a result on the wrong patient's pathway (H-23).

**Effect.** One patient's result is acted on for another, and the right patient's result looks missing.

**Possible harm.** A wrong diagnosis or treatment for one patient, and a missed one for the other. Major in the worst credible case.

**Existing controls** (built on branch `feature/specimens`, Phase 3's PR 13; to be verified by the CSO). Today's attachment controls (H-23) apply to report files too.
- **Reporting from the specimen's own page:** a report is attached only from a specimen, never from a list of files (`POST api/specimens/{id}/report`). **Finding a specimen by its number,** typed or scanned (`POST api/specimens/find`, the number in the body), opens that specimen's page, with the patient banner; the Attach Report dialog shows the patient as the banner does, with the specimen's number (decisions 86 and 90; `SpecimenPage`, `ReportSpecimenDialog`). Protective.
- **The label's identifiers:** the specimen number as text and as a QR code holding only that number, with the patient's name, date of birth and NHS number, and the request sheet with them too (decision 87; `SpecimenPrintPage`). Protective.
- **A specimen's report can't be deleted on its own** (a 409, admins too; decision 71), so a report isn't removed from a specimen by mistake. Protective. (It also means a wrong one can't be removed yet: below.)
- **A wrong specimen recorded before it's reported** (taken on the wrong patient, say) is marked **entered in error** with a detail, by whoever recorded it within 24 hours, or a holder of `CorrectRecords` at any time; it's closed and kept with its history. Corrective.
- **A wrong report** (built with results sign-off, PR 14, branch `feature/results-signoff`; to be verified by the CSO): its result is marked **entered in error** with a detail, by whoever reported it within 24 hours or a holder of `CorrectRecords` at any time; the report file is deleted (audited) and the specimen goes back to Received, so the lab attaches the right report (decision 96; `MarkResultEnteredInErrorHandler`). Corrective.

**Initial risk:** [For the CSO to assign.] Draft estimate: Major, Low: **3**.

**Further controls** (before the feature's first live use):
- **Entered in error sends it back:** by `CorrectRecords` or the lab user who reported it within 24 hours, with a reason; the result closes, its file is soft-deleted, and the specimen returns to Received to be reported again (decision 96). Corrective; **planned, PR 14**. **Until it's built, a report attached to the wrong specimen can't be corrected in MedApp:** a reported specimen is closed, and its report can't be deleted on its own (`docs/todo.md`). So results sign-off, with this correction, is part of specimens' first live use.
- **The results list** (PR 14): each report becomes a result its clinician reads and signs off, a second look at whether it belongs to the patient. Detective; planned, PR 14.
- **Transferred:** positive patient identification when labelling, and the lab's own checks of the label against the request.

**At first live use:** Draft: Major, Very low: **2**.

**Residual risk:** Draft: Major, Very low: **2**.

**Owner:** [CSO]. **Status:** Open, draft for the CSO: reporting from the specimen's page found by its number, the label's identifiers and the protected report are built; entered in error for a reported specimen, and the results list, aren't (PR 14).

---

## H-48: A letter goes to the wrong practice or person

**Description.** A clinic letter or discharge summary is sent to the wrong GP practice, the wrong address, or someone who shouldn't have it.

**Causes:**
- **The wrong practice** recorded on the patient, or a practice typed rather than chosen.
- **An old address:** the patient has moved.
- **An "other" recipient** typed wrongly.
- **The wrong secure email address.**

**Effect.** The patient's GP doesn't receive the letter (so H-49 too), and someone else receives a patient's clinical details.

**Possible harm.** Follow-up the letter asks of the GP doesn't happen; a breach of confidentiality. One patient; Considerable in the worst credible case.

**Existing controls** (built in PR 16; the planned ones below, in place):
- Recipients with their addresses on the letter's page, and again in the approval confirmation (with the patient banner) and the secure email confirmation (naming the address): the web app's Letter page.
- The practice from the directory (`LetterRecords.RecipientsAsync`), a new letter starting with the patient's registered practice and the patient (`CreateLetterHandler.StartingRecipients`).
- Names and addresses copied from the record again at approval (`LetterRecords.RefreshRecipientsAsync`); a recipient with no address, or a practice gone from the directory, stops approval.
- Secure email only to an allowed domain, through a declared relay, never to a patient (`LetterWorkflow.WhyNoSecureEmail`).
- Withdraw (approver or `CorrectRecords`, with a reason) and Write a correction (`WithdrawLetterHandler`, `WriteLetterCorrectionHandler`); the PDF stays on file.

**Initial risk:** [For the CSO to assign.] Draft estimate: Considerable, Medium: **3**.

**Further controls** (planned, PRs 12 and 16; before the feature's first live use):
- **Recipients shown with their addresses** before approval and again before sending (decisions 101 to 103). Protective.
- **The practice from the directory,** not retyped (decision 79); a new letter starts with the patient's registered practice and the patient. Design.
- **Addresses copied onto the letter at approval,** so what was sent is what's on record (decision 101). Detective.
- **Secure email only to an address in an allowed domain** (`Letters:SecureEmailDomains`), and never to patients (decisions 103 and 107). Protective.
- **Withdraw and correct:** the withdrawn letter's page lists who it was already sent to, says to tell them, and offers **Write a correction** (decision 104). Corrective.
- **Transferred:** checking the patient's address and practice with them at each visit.

**At first live use:** Draft: Considerable, Low: **2**.

**Residual risk:** Draft: Considerable, Low: **2**.

**Owner:** [CSO]. **Status:** Open, draft for the CSO.

---

## H-49: A letter isn't sent, or goes late

**Description.** A letter is written and approved but never sent, or sent late, so the GP or the patient doesn't learn what the clinic decided.

**Causes:**
- **Approved but left:** nobody works the To send list.
- **A failed send** that nobody notices.
- **Waiting for approval** with an approver who is away.
- **A restricted record:** its letters can't be sent (decision 75).
- **MedApp unavailable** when letters are due (H-10).

**Effect.** The GP doesn't start or change treatment the letter asks for; the patient doesn't know the plan.

**Possible harm.** A delayed treatment or follow-up. One patient; Considerable in the worst credible case.

**Existing controls** (built in PR 16; the planned ones below, in place):
- The To send view and the side navigation's count (`LetterSearch`, `GET api/letters/count`); Awaiting my approval and Returned to me views; the approver told when a letter is submitted, the author when it's approved or returned (`LetterNotifications`).
- The `NeedsLetterKind` gate on a stage definition, copied onto the pathway's stage (`StageGates.WhyLetterMissing`, `FinishPathwayStageHandler`).
- Secure email sent synchronously, marked Sent only when the relay accepts it; a refusal recorded as Failed and shown at once (`SendLetterHandler`).
- A restricted record's letters refused with a message to ask an admin (`LetterWorkflow.RestrictedMessage`).

**Initial risk:** [For the CSO to assign.] Draft estimate: Considerable, Medium: **3**.

**Further controls** (planned, PR 16; before the feature's first live use):
- **The To send view and its count,** in the Letters list and the side navigation (decisions 103 and 108). Detective.
- **The `NeedsLetterKind` gate:** a stage flagged with a letter kind (a discharge summary) can't be finished until a letter of that kind is approved and sent to every recipient (decision 105). Protective.
- **Secure email marked Sent only when the SMTP server accepts it,** sent synchronously, with a failure shown at once (decision 103). Protective.
- **A restricted record's letters stop with a message** to ask an admin (decision 75). Information.
- **Awaiting my approval and Returned to me** views, and the author told when it's approved or returned (decision 102). Detective.
- **Transferred:** a daily check of letters to send and awaiting approval, and a downtime process for letters (`dcb0160-deploying-organisations.md`).

**At first live use:** Draft: Considerable, Low: **2**.

**Residual risk:** Draft: Considerable, Low: **2**.

**Owner:** [CSO]. **Status:** Open, draft for the CSO.

---

## H-50: A letter shows another patient's details, or an unapproved version is sent

**Description.** A letter carries another patient's name, date of birth or NHS number, or a draft that wasn't approved is sent.

**Causes:**
- **A merge field filled from another record.**
- **A draft printed or downloaded** from the preview and sent as if final.
- **A page of one patient's letter** mixed with another's when printed.
- **An edit after approval.**

**Effect.** The recipient acts on the wrong patient's information, or on wording no clinician approved.

**Possible harm.** Treatment changed for the wrong patient, or on wrong advice; a breach of confidentiality. One patient; Major in the worst credible case.

**Existing controls** (built in PRs 15 and 16; the planned ones below, in place):
- Merge fields from the letter's own patient and record only (`LetterRecords.MergeValuesAsync`); approval refused naming any field without a value (`LetterRendering.MissingMessage`), and refused for characters the font can't print.
- The patient's identifiers and "Page n of m" on every page (`PdfSharpLetterRenderer`); previews marked "Draft, not approved" on every page (`PreviewLetterHandler`).
- Approval freezes the letter; only its approved PDF is sent by secure email, and that PDF can't be deleted on its own (`DeleteAttachmentHandler`).

**Initial risk:** [For the CSO to assign.] Draft estimate: Major, Low: **3**.

**Further controls** (planned, PRs 15 and 16; before the feature's first live use):
- **Merge fields from the letter's own patient only,** a fixed list of identity and administrative data; a field with no value stops approval, naming it (decision 99). Design.
- **The patient's name, date of birth and NHS number and "Page n of m" on every page** of the PDF (decision 102). Protective.
- **Previews marked "Draft, not approved" on every page** (the preview endpoint). Protective.
- **Only approved PDFs can be sent,** and approval freezes the letter (decisions 102 and 103). Design.
- **Transferred:** checking the identifiers on each page before posting or handing over.

**At first live use:** Draft: Major, Very low: **2**.

**Residual risk:** Draft: Major, Very low: **2**.

**Owner:** [CSO]. **Status:** Open, draft for the CSO.

---

## H-51: A branch is chosen, or a stage skipped, in error

**Description.** At a branch point the clinician chooses the wrong branch, or an optional stage is skipped that the patient needed.

**Causes:**
- **A misclick** among the branches, or on Skip.
- **The wrong patient's pathway** (H-01).
- **The template's branches set up wrongly** (H-13).
- **Skipping to clear a list** rather than because the stage isn't needed.

**Effect.** The stages of the other branch are skipped in the same save, so the patient misses a treatment or follow-up, or gets one they didn't need.

**Possible harm.** A missed or unnecessary treatment. One patient; Major in the worst credible case.

**Existing controls:** built with the feature (PR 20, branch `feature/branching-pathways`), as listed below. Since PR 24, reinstating needs `UnlockStages`, and skipping a Required-visit stage `FinishWithoutVisit` (both halves of the old `OverrideStages`).

**Initial risk:** [For the CSO to assign.] Draft estimate: Major, Low: **3**.

**Further controls** (built in PR 20; `PathwayWorkflow`, `PathwayChange`, `TemplateBranchRules`, the pathway page and the template editor):
- **A confirmation listing what will be skipped,** with the patient banner (section 3.8). Branches are listed in the template's order with the stages each contains, **none pre-selected** (decision 114). Protective.
- **A reason for every skip** (Not needed, Patient declined, Other with a detail) (decision 113). Protective.
- **Reinstating:** a holder of `UnlockStages` reinstates a skipped stage with a reason; a branch is re-chosen by reinstating its stages (decision 115). Corrective.
- **The audit:** `StageSkipped`, `BranchChosen` (the branch and the skipped stage IDs) and `StageReinstated`, on the timeline; skipped stages shown struck through with the reason. Detective.
- **Template checks:** every branch point has at least two branches, and the last stage is never optional or in a branch (decision 117); a branch point is itself never optional or in a branch. Design.
- **A stage whose visit is Required** can be skipped only by a holder of `FinishWithoutVisit`, with a detail, as finishing it without its visit needs (`PathwayWorkflow.SkipNeedsOverride`); the audit records the requirement. A branch not chosen still skips such a stage, as the clinician chose the other branch. Protective.
- **No branch assumed:** finishing a branch point without a branch is refused (400), and a branch can't be given again when it's finished after an unlock. Design.
- **Transferred:** clinical sign-off of each template's branches and optional stages.

**At first live use:** Draft: Major, Very low: **2**.

**Residual risk:** Draft: Major, Very low: **2**.

**Owner:** [CSO]. **Status:** Open, draft for the CSO.

---

## H-52: A patient is booked when nobody, or no room, is available, or a cancelled clinic's patients aren't told

**Description.** An appointment is booked outside the clinician's or site's hours, in a room already in use, or a clinic session is cancelled and its patients still come or aren't rebooked.

**Causes:**
- **Hours entered wrongly,** or not entered (a person on the default hours who doesn't work them).
- **A room double-booked** by two sessions or an appointment outside a session.
- **A site's closed day** missing.
- **A cancelled session's patients not contacted,** or the call list not worked.

**Effect.** The patient arrives and isn't seen, or isn't told their appointment was cancelled.

**Possible harm.** A delayed assessment or treatment. One or several patients; Considerable in the worst credible case.

**Existing controls:** none (not built). Today's fixed working day and clash checks (H-31, H-33) apply until it's replaced.

*Built so far:* the hours (PR 22), and clinic sessions (PR 23, `feature/clinic-sessions`): one patient per slot (the clinician's calendar lock, the clash check and a unique index on session and start), one session per room at a time (the room's lock and its clash check), a site's closed day and opening hours closing its rooms for sessions and bookings, cancelling with the list of patients to tell, moving a session with bookings refused, and Create sessions saying what it skipped. Integration tests race two bookings for one slot and two sessions for one room. Since PR 26 (`feature/patient-reminders`), cancelling a session can also text or email its patients ("Tell the patient", for those who chose a channel), each message stored and its delivery shown; the list stays for phoning the rest.

**Initial risk:** [For the CSO to assign.] Draft estimate: Considerable, Medium: **3**.

**Further controls** (planned, PRs 21 to 23 and 26; before the feature's first live use):
- **Hours and clash checks on the server:** each person's working time from their hours, the site's hours and closed days (`WorkingTime`), checked by every booking path; `WorkingDaysConventionTests` keeps every validator on it (decisions 119 to 121). Protective.
- **A room holds one session at a time** (a room lock and a clash check), and a session over existing busy time is refused (decision 123). Protective.
- **Cancelling a session** cancels its bookings in the same save and shows **the list of patients to tell,** with the option to text or email them; the clinician is told (decision 126). Protective.
- **Moving a session with bookings is refused** (cancel and recreate) (decision 127). Design.
- **Create sessions** lists what it made and what it skipped and why (decision 125). Information.
- **Transferred:** keeping hours and closed days up to date, and working the call list when a clinic is cancelled.

**At first live use:** Draft: Considerable, Low: **2**.

**Residual risk:** Draft: Considerable, Low: **2**.

**Owner:** [CSO]. **Status:** Open, draft for the CSO.

---

## H-53: A reminder reaches the wrong person or says too much

**Description.** A text or email reminder goes to someone other than the patient, or reveals more than when and where the appointment is.

**Causes:**
- **A wrong or old mobile number or email** on the record.
- **A shared phone or inbox** (a family member reads it).
- **A message that names the specialty,** clinician or condition.
- **A sender or site name that names the service** (a sexual health or mental health clinic, say).

**Effect.** Someone learns that the patient is attending a clinic, and possibly why; the patient may not get the reminder.

**Possible harm.** Distress and a breach of confidentiality; a missed appointment (H-54). One patient; Significant in the worst credible case.

**Existing controls** (built in PR 26, `feature/patient-reminders`; for the CSO to verify against the code):
- **No clinical words:** fixed wording in code: the organisation's name (`PatientMessages:SenderName`), the date and time (UK), and the site's name, address, postcode and phone (from the appointment's room's site; with no room, no site is named rather than guessed), or a contact phone setting; never the specialty, clinician, pathway, stage, a note, a cancellation's reason or the NHS number. Emails have a fixed subject for each kind and say replies aren't read (decision 131; `PatientMessageTexts`, with tests). Design.
- **Destinations from the record, on the channels the patient chose:** the reminder choice is asked on the patient form; with no choice, or no mobile or email for the chosen channel, nothing is sent (decisions 130 and 133; `PatientMessageRules`). Protective.
- **Checked again just before sending,** to the contact on the record at that moment: a number or address corrected after a message was made is used, and a message whose appointment has changed or passed, whose record is now restricted (except a cancellation) or anonymised, or whose channel the patient no longer wants, is cancelled, not sent (`PatientMessageRules.WhyNotToSend`). Protective.
- **Moves and cancellations only when a person asks** ("Tell the patient by text or email"), and missed-appointment messages off unless the organisation turns them on (`Reminders:MissedMessage`). Design.
- **The stored copy:** every message is kept as sent, with its destination, text, status and who asked, and shown on the appointment (decision 129; `PatientMessage`, `GET api/appointments/{id}/messages`). Detective.
- **Off by default:** nothing is sent unless the organisation chooses the Live mode with its own Notify service (`PatientMessages:Mode`). Design.

**Initial risk:** [For the CSO to assign.] Draft estimate: Significant, Medium: **2**.

**Further controls** (before the feature's first live use):
- **Transferred:** confirming the patient's mobile, email and reminder choice with them when they're given or change, and recording their opt-out at once; choosing a sender name and site names that don't reveal the kind of care where that matters.

**At first live use:** Draft: Significant, Low: **2**.

**Residual risk:** Draft: Significant, Low: **2**.

**Owner:** [CSO]. **Status:** Open, draft for the CSO.

---

## H-54: A patient relies on a reminder that isn't sent

**Description.** A patient expects a reminder, doesn't get one, and misses the appointment.

**Causes:**
- **No consent or contact** recorded, so nothing is sent.
- **The transport is off** or failing (Off, a provider outage), or the job has stopped (H-56).
- **The appointment was booked inside the reminder window,** less than 2 hours ahead, or outside sending hours.
- **Delivery failed** (a wrong number, a full inbox).

**Effect.** A missed appointment.

**Possible harm.** A delayed assessment or treatment. One patient; Considerable in the worst credible case.

**Existing controls** (built in PRs 19 and 26; for the CSO to verify against the code):
- **"No reminder" shown** on the appointment when the patient has no channel or contact: the message is stored as not sent, with the reason ("No reminder: no consent or contact") (decision 133; `PatientMessageFactory`). Information.
- **Delivery status** fetched from the provider for 3 days and shown with each message (waiting, sent, delivered, failed, and why) (decision 129; `PatientRemindersJob`). Detective.
- **Retries, then a visible failure:** a message that fails for a passing reason is tried again on later runs, and given up after 5 attempts (`TooManyAttempts`); one the provider refuses (an invalid number) is marked failed at once (`Rejected`). Detective.
- **The job's watch:** `patient-reminders` runs under the job runner, so a failing job tells `ManageSetup` holders and makes `/health` Degraded (decision 111; H-56). Detective.
- **Reminders are a courtesy:** nothing in MedApp relies on one, and they're off unless the organisation turns them on. Design.

**Initial risk:** [For the CSO to assign.] Draft estimate: Considerable, Low: **2**.

**Further controls** (before the feature's first live use):
- **Reminders are a courtesy,** not the organisation's contact with the patient: the appointment letter or call stays the organisation's process, and `dcb0160-deploying-organisations.md` says so. Information; transferred.

**At first live use:** Draft: Considerable, Low: **2**.

**Residual risk:** Draft: Considerable, Low: **2**.

**Owner:** [CSO]. **Status:** Open, draft for the CSO.

---

## H-55: A missed appointment isn't followed up

**Description.** A patient is marked Did not attend, and nobody decides what happens next.

**Causes:**
- **No owner** for the Did not attend list.
- **An assumption that MedApp acts on it:** it doesn't, by design.
- **A follow-up recorded but not done** (a Rebook or Clinician to review that nobody acts on).
- **The Requires Appointment page has no side navigation item** (H-30), so the view is seen only by those who open it.

**Effect.** The pathway stalls with no appointment, and the patient drops out of care.

**Possible harm.** A delayed diagnosis or treatment. One patient; Considerable in the worst credible case.

**Existing controls** (built in PR 26, `feature/patient-reminders`; for the CSO to verify against the code):
- **The Did not attend view** on Requires Appointment (`GET api/appointments/search?view=didNotAttend`): every appointment marked Did not attend with no later appointment for the same stage (or, without a stage, the same pathway; without a pathway, the patient's appointments without one) and no **No further appointment** decision, oldest first, with the number missed on that pathway (decision 132; `AppointmentSearch`, `DidNotAttendViewTests`). Detective.
- **A follow-up stays listed until it's done:** Rebook and Clinician to review leave the appointment on the view until a later appointment exists, so neither is forgotten; only No further appointment, which needs a detail saying who decided, takes it off (`RecordDnaFollowUpValidator`). Protective.
- **A recorded follow-up** (Rebook, Clinician to review, No further appointment, with a detail of up to 500 characters) on the appointment, with who and when, and in its history (`DnaFollowUpRecorded`), on the patient timeline (decision 132; `RecordDnaFollowUpHandler`). Correcting the outcome to Attended clears it. Protective.
- **No automatic discharge:** MedApp never discharges, re-prioritises or changes a pathway because of a missed appointment; discharge is a clinician's action on the pathway (decision 132). Design.
- **A missed-appointment message,** if the organisation turns it on (`Reminders:MissedMessage`, off by default), asks the patient to call, the next working day from 10:00. Protective; a courtesy only.

**Initial risk:** [For the CSO to assign.] Draft estimate: Considerable, Medium: **3**.

**Further controls** (before the feature's first live use):
- **Transferred:** a named owner for the Did not attend view, who reviews it on a set schedule (Rebook and Clinician to review items included), and the organisation's own policy for patients who don't attend.

**At first live use:** Draft: Considerable, Low: **2**.

**Residual risk:** Draft: Considerable, Low: **2**.

**Owner:** [CSO]. **Status:** Open, draft for the CSO.

---

## H-56: A scheduled job stops and nobody notices

**Description.** A scheduled job (reminders, service alerts, the simulated lab, data request reminders, malware verdicts, the purge) fails or stops running, silently.

**Causes:**
- **A repeated failure** (a provider down, a bug).
- **Turned off** by a setting and forgotten.
- **A lease never released** by a copy of the API that died.
- **Every copy of the API down** or restarting.

**Effect.** Reminders, alerts and verdicts that staff rely on don't happen (H-29, H-54, H-64).

**Possible harm.** Through the hazards it feeds: a missed appointment or a late follow-up. Several patients; Considerable in the worst credible case.

**Initial risk:** [For the CSO to assign.] Draft estimate: Considerable, Medium: **3**.

**Controls** (built in PR 19, the job runner; today its only job is the operational purge, which feeds no clinical work):
- **Each job's last start, finish, outcome and count, and consecutive failures,** on Administration, Scheduled Jobs, with Run now (decision 111; `/admin/jobs`). Detective. **In place.**
- **After three failures in a row, `ManageSetup` holders are told** once (`ScheduledJobFailing`) until it succeeds (decision 111). Protective. **In place.**
- **`/health` reports Degraded** when a job is switched on, not running and two intervals overdue, for the deployment's monitoring (decision 111; `ScheduledJobsHealthCheck`). Detective. **In place.**
- **Leases expire** after 10 minutes unless renewed, so a dead copy can't hold a job (decision 110). Design. **In place;** an integration test runs two runners against one database.
- **Alerts are a convenience, never the safety net:** the lists and their counts are (decision 137; extends H-29). Design.
- **Transferred:** alerting on `/health`, and a weekly look at Scheduled Jobs.

**At first live use:** Draft: Considerable, Low: **2**.

**Residual risk:** Draft: Considerable, Low: **2**.

**Owner:** [CSO]. **Status:** Open, draft for the CSO.

---

## H-57: A record is anonymised too early because its retention class is wrong

**Description.** A record that should be kept longer (oncology, maternity, mental health) shows as past its retention period and is anonymised.

**Causes:**
- **The wrong class on a template,** or none (Standard).
- **Wrong years** in the settings, not checked against the Records Management Code of Practice.
- **A record with pathways of several classes** read by the shortest.

**Effect.** Clinical history needed for later care, or a claim, is lost.

**Possible harm.** Later care given without the history (a recurrence treated without the earlier histology). One patient; Considerable in the worst credible case.

**Existing controls:** the retention review lists records past their period and an admin decides; nothing is anonymised automatically (`RetentionSchedule`). Only the adult and children periods exist, and the review and the anonymise dialog warn of that.

**Initial risk:** [For the CSO to assign.] Draft estimate: Considerable, Low: **2**.

**Further controls** (planned, PR 27; before the feature's first live use):
- **The class shown on the review,** for each record (decision 139). Information.
- **The longest period wins:** a record's minimum period is the longest of the Standard rule and each of its pathways' classes (decision 139). Design.
- **DPO-checked defaults:** the years are settings, defaulting to 30, 25 and 20, for the DPO to check before use (decision 139). Information; transferred.
- **Nothing deleted automatically,** as today (decision 140). Design.

**Built (PR 27, `feature/retention-classes`):** the four controls above, for the CSO to verify (`RetentionSchedule.Due`, `RetentionReviewPage`'s Class column, `Retention:Classes`). Also: giving a template a longer class raises its existing pathways' class, so a class set after go-live covers the records already made from it, and **a shorter class never lowers them**, so lowering a template's class by mistake can't bring existing records into the review early (`UpdatePathwayTemplateHandler`, `RetentionClassYears.ShorterThan`); the review and anonymise dialog's warning about missing classes is gone. Left open: a deleted pathway's class stops counting, and existing templates are Standard until an admin sets them (`docs/todo.md`; the deploying organisation's duty in `dcb0160-deploying-organisations.md`).

**At first live use:** Draft: Considerable, Very low: **2**.

**Residual risk:** Draft: Considerable, Very low: **2**.

**Owner:** [CSO]. **Status:** Open, draft for the CSO.

---

## H-58: An MDT outcome is recorded on the wrong patient, or not acted on

**Description.** The team's decision at the MDT meeting is recorded against the wrong item, or recorded and then not acted on.

**Causes:**
- **Several patients discussed quickly,** with similar names (H-01).
- **The outcome recorded later,** from memory or notes.
- **Nobody owns the action** the outcome asks for.
- **A cancelled meeting:** its items stay listed but undiscussed.

**Effect.** One patient gets another's plan, or the plan isn't carried out.

**Possible harm.** A wrong, delayed or missed treatment. One patient; Major in the worst credible case.

**Existing controls:** built with PR 30 (`feature/mdt-list`), for the CSO to verify: the controls below, plus an outcome only once the meeting has started, from the version the recorder read (409 otherwise), with who was present chosen from the meeting's attendees; MedApp never suggests an outcome or changes the pathway because of one. No estimate changed.

**Initial risk:** [For the CSO to assign.] Draft estimate: Major, Low: **3**.

**Further controls** (planned, PR 30; before the feature's first live use):
- **The patient banner on each item and in the outcome dialog** (decisions 142 and 143). Protective.
- **The pathway's assignees are told** when an outcome is recorded (`MdtOutcomeRecorded`), and the pathway page's MDT card shows it (decision 142). Protective.
- **A cancelled meeting's items stay Listed,** flagged "Meeting cancelled: list again" (decision 144). Detective.
- **Who was present** recorded with the outcome (decision 142). Detective.
- **Transferred:** the MDT's own process for confirming outcomes and owning actions.

**At first live use:** Draft: Major, Very low: **2**.

**Residual risk:** Draft: Major, Very low: **2**.

**Owner:** [CSO]. **Status:** Open, draft for the CSO.

---

## H-59: A procedure goes ahead without valid consent recorded, or a withdrawal is missed

**Description.** A stage that needs consent is done without it, or the patient withdraws consent and the team carries on.

**Causes:**
- **No gate set** on a stage that needs consent.
- **Consent recorded on the wrong stage or patient.**
- **A withdrawal recorded after the stage finished,** and not noticed.

**Effect.** A procedure without the patient's agreement.

**Possible harm.** Harm from an unwanted procedure, and a breach of the patient's rights. One patient; Major in the worst credible case.

**Existing controls** (built in PR 31, `feature/consent-records`, for the CSO to verify): the controls below, but the transferred one.

**Initial risk:** [For the CSO to assign.] Draft estimate: Major, Low: **3**.

**Further controls** (built in PR 31 but the transferred one; the patient banner shows on every consent dialog):
- **The `NeedsConsent` gate:** a flagged stage can't be finished without a Valid consent record for it; no override (decision 146). Protective.
- **A withdrawal flagged on the pathway,** including after the stage finished (decision 147). Detective.
- **No scan flagged** ("No scan") when the form isn't attached (decision 145). Information.
- **MedApp doesn't assess consent:** it records the clinician's statement (decision 147). Design.
- **Transferred:** the organisation's consent policy, including capacity, and clinical sign-off of which stages need consent.

**At first live use:** Draft: Major, Very low: **2**.

**Residual risk:** Draft: Major, Very low: **2**.

**Owner:** [CSO]. **Status:** Open, draft for the CSO.

---

## H-60: Questionnaire answers are read as an assessment, or recorded on the wrong patient

**Description.** A total recorded from a paper form is taken as MedApp's assessment, or answers are entered on the wrong patient's record.

**Causes:**
- **A total misread** as calculated or checked by MedApp.
- **A total written wrongly** on the form and entered as written.
- **The wrong patient's pathway** (H-01).

**Effect.** A clinical decision based on a wrong total, or another patient's answers.

**Possible harm.** A treatment changed or stopped on wrong information. One patient; Considerable in the worst credible case.

**Existing controls:** none (not built).

**Initial risk:** [For the CSO to assign.] Draft estimate: Considerable, Low: **2**.

**Further controls** (planned, PR 32; before the feature's first live use):
- **No totals, bands, changes or charts by MedApp:** the total is labelled "as written on the form" (decision 154). Design.
- **The patient banner** when recording (decision 76's checklist). Protective.
- **Entered in error** for a wrong entry (decision 153). Corrective.
- **Transferred:** the instrument's own scoring rules, and checking the total on the form.

**At first live use:** Draft: Considerable, Very low: **2**.

**Residual risk:** Draft: Considerable, Very low: **2**.

**Owner:** [CSO]. **Status:** Open, draft for the CSO.

---

## H-61: A video link reaches the wrong patient

**Description.** A video consultation's link is sent to, or used by, someone other than the patient.

**Causes:**
- **A staff member's default room link** reused for every patient, so a later patient joins an earlier one's call.
- **The link sent to a wrong contact** (H-53).
- **A link to an unexpected host.**

**Effect.** Another patient, or someone else, sees or hears a consultation.

**Possible harm.** A breach of confidentiality, and a consultation stopped. One patient; Significant in the worst credible case.

**Existing controls:** none (not built).

**Initial risk:** [For the CSO to assign.] Draft estimate: Significant, Low: **2**.

**Further controls** (planned, PR 34; before the feature's first live use):
- **The link only in that patient's message,** and shown only to staff (decision 165). Design.
- **Allowed hosts:** `https` only, with the host in `VideoConsultations:AllowedHosts` (decision 164). Protective.
- **Transferred:** the video platform's waiting room and admitting each patient, chosen and configured by the organisation.

**At first live use:** Draft: Significant, Very low: **1**.

**Residual risk:** Draft: Significant, Very low: **1**.

**Owner:** [CSO]. **Status:** Open, draft for the CSO.

---

## H-62: Data from an NHS system is matched to the wrong patient

**Description.** A referral from the e-Referral Service, a report from a lab's system, or demographics from PDS is filed against the wrong patient.

**Causes:**
- **Automatic matching** on name and date of birth.
- **A wrong NHS number** on either side (H-01).
- **A report filed by the lab's reference** rather than the specimen number.

**Effect.** One patient's referral or result is worked on another's record.

**Possible harm.** A wrong or missed diagnosis or treatment. One patient; Major in the worst credible case.

**Existing controls:** none (not built). The demographics lookup today is an offline stub, and its confirmation on Edit Patient is under H-01.

**Initial risk:** [For the CSO to assign.] Draft estimate: Major, Low: **3**.

**Further controls** (planned, PRs 38a to 38c; before each adapter's first live use):
- **Incoming referrals matched by a person, never automatically:** they land in Incoming referrals, with the exact NHS number shown beside the name and date of birth, and are registered through the normal command (decision 169). Design.
- **Lab reports filed by specimen number,** and kept as documents (decision 172). Protective.
- **PDS fills details for staff to confirm** (decision 170). Protective.
- **Each Live mode updates governance and safety documents first** (decision 173). Information.
- **Transferred:** positive patient identification, and the NHS onboarding's own assurance.

**At first live use:** Draft: Major, Very low: **2**.

**Residual risk:** Draft: Major, Very low: **2**.

**Owner:** [CSO]. **Status:** Open, draft for the CSO.

---

## H-63: One organisation sees another's patients

**Description.** With several organisations in one deployment, staff of one see, or work on, another organisation's patients.

**Causes:**
- **A query missing the organisation filter.**
- **A job run** for the wrong organisation.
- **A shared identifier** (an NHS number, a specimen number) matched across organisations.

**Effect.** Staff act on a record that isn't their organisation's, and a breach of confidentiality.

**Possible harm.** Care decisions on the wrong record (H-01), and a breach. Several patients; Considerable in the worst credible case.

**Existing controls:** none needed today: each customer has its own deployment. Becomes a hazard with multi-tenancy.

**Initial risk:** [For the CSO to assign.] Draft estimate: Considerable, Low: **2**.

**Further controls** (planned, PRs 40a to 40c; before a second organisation shares a deployment):
- **Two layers of separation:** EF Core query filters on the organisation from the session, and SQL Server row-level security on `SESSION_CONTEXT`, so a query that misses the filter returns nothing (decision 176). Design.
- **Unique numbers per organisation,** and jobs run once per organisation in its own scope (decisions 175 and 176). Design.
- **Tests:** every endpoint returns 404 for another organisation's IDs; every entity has the filter; row-level security blocks an unfiltered query (decision 178). Protective.

**At first live use:** Draft: Considerable, Very low: **2**.

**Residual risk:** Draft: Considerable, Very low: **2**.

**Owner:** [CSO]. **Status:** Open, draft for the CSO.

---

## H-64: A file is held as "being scanned" when it's needed

**Description.** A file waits for its malware verdict and can't be downloaded when a clinician needs it.

**Causes:**
- **The verdict job stopped** (H-56).
- **The scanner unavailable** (Defender for Storage, a ClamAV container).
- **A file wrongly marked infected** and removed.

**Effect.** A report or letter can't be opened during a clinic.

**Possible harm.** A delayed decision. One patient; Considerable in the worst credible case.

**Existing controls:** none (not built). Today no file is scanned, so none is held (H-25).

**Initial risk:** [For the CSO to assign.] Draft estimate: Considerable, Low: **2**.

**Further controls** (planned, PR 37; before the feature's first live use):
- **A minute's scan:** the `malware-verdicts` job runs every minute; the file shows "Being scanned" and a download says to try again in a minute (decision 167). Information.
- **The job's watch** (decision 111; H-56). Detective.
- **Infected files are soft-deleted, not destroyed,** and the uploader and `ManageSetup` holders are told (decision 167). Corrective.
- **Transferred:** master copies of documents in the organisation's own systems (H-24, H-28).

**At first live use:** Draft: Considerable, Very low: **2**.

**Residual risk:** Draft: Considerable, Very low: **2**.

**Owner:** [CSO]. **Status:** Open, draft for the CSO.

---

## Hazards considered and not logged

- **Stages worked out of order.** Designed out while no stage is unlocked: only the current stage can be started, edited or finished, checked by the API (`PathwayWorkflow.WhyStageCantBeEdited`), with tests. Once an admin unlocks a finished stage, it and the current stage can both be edited and finished; that's logged under H-06.
- **Completing a pathway with a stage unfinished or unlocked.** Designed out: `ChangePatientPathwayStatusHandler` refuses it (`AllStagesFinished`).
- **One user's data in another's server-rendered page.** A security and privacy risk rather than a clinical one, controlled by a new query cache per request (`createSsrRouter`; `docs/architecture.md`, section 5.8). Left to the security work in step 2.
- **Clinical calculations or recommendations going wrong.** MedApp has none (`intended-purpose-and-device-decision.md`). Booking and the visit rule compute nothing from clinical data either.
- **Appointments booked in the past, at weekends or outside working hours.** Designed out: the server refuses them (`docs/calendar-plan.md`, decisions 2 and 17). The cost, that a walk-in can't be recorded after the event, is a gap in the record rather than a hazard, and is on `docs/todo.md`.
- **Two open appointments for one stage.** Designed out: a second is refused, pointing to the first (decision 18).
- **An earlier day's calendar changing after the event.** Designed out: past days can't be changed, and template history is kept by dated rows (decisions 8 and 11).
- **The FHIR API sending wrong data** (branch `feature/nhs-integrations`). It's read-only, and returns the patient's details as stored, with no clinical content and no logic, to staff who could see the same on the patient's page; nothing in MedApp changes because of it. Its one clinical risk, another system matching the wrong person by a wrong NHS number, is under H-01. Each receiving system assesses its own use under DCB0129 and DCB0160.
- **The NHS number check refusing a valid number.** Staff can't save it, so the record goes without it until it's fixed: no worse than before the number existed. The check is unit tested with valid and invalid numbers.
- **An uploaded file running script in MedApp.** Designed out: files are never shown inline on MedApp's site, only downloaded, with a sandboxing Content Security Policy, and SVG and HTML are refused (`AttachmentResponseHeaders`, `FileTypeSniffer`). A security risk rather than a clinical one; malware opened on the desktop is H-25.
- **A lab value or a questionnaire score misread by MedApp** (planned features, `docs/centre-phases-3-5-plan.md`). Designed out: reports stay documents, shown as the lab sent them, and MedApp reads, compares or flags no value; a questionnaire's total is only the one written on the form, and MedApp calculates none (decisions 69, 97 and 154). Doing either would be a device question first (`intended-purpose-and-device-decision.md`).
- **A branch chosen by MedApp.** Designed out: the clinician chooses, with none pre-selected (decision 114).
- **Thumbnails misread as the image.** A thumbnail is at most 320 pixels, too small to read clinically, and the full file is a download away. Not logged; the CSO may decide otherwise.

## Change log

| Version | Date | Change | By |
|---|---|---|---|
| 0.1 | 16 November 2026 | First draft: 20 hazards from reading the code at `88dc1fa` | AI coding agent, for review |
| 0.2 | 16 November 2026 | After the review of #60: DCB0129's own consequence table, which re-rated H-09, H-10, H-12, H-13, H-14, H-20; an "at first live use" rating; My Worklist's default filter added to H-04, H-05 and H-09; new hazard H-21; H-13's approval step brought forward; H-12's fix in PR #62; smaller corrections | AI coding agent, for review |
| 0.3 | 16 November 2026 | H-12's defect fixed by PR #62: the fix recorded as built, the residual risk's reasons updated, and what's left to verify | AI coding agent, for review |
| 0.4 | 16 November 2026 | The privacy features (PR #68): H-03 and H-19's new controls and causes (anonymisation, the retention review's adult and children periods only), and new hazard H-22 (restricting a record delays care under way), after the review of #68 | AI coding agent, for review |
| 0.5 | 16 November 2026 | The NHS integrations (PR #69): the patient banner recorded as built for H-01 and H-02, awaiting the CSO's verification; the NHS number in H-01's causes and controls, with new causes for a wrong number and the demographics lookup; SNOMED CT codes in H-13's causes, with a terminology server check; the FHIR API and the NHS number check considered and not logged. No rating changed. After the review of #69: the banner on every page about one patient, and the lookup's confirmation on Edit Patient | AI coding agent, for review |
| 0.6 | 16 November 2026 | Admins' monitoring rules and the assignment notification switch (PR #84): H-09 gets the switch as a cause, its stored-notification control now says "unless switched off", and the switch's help text points to My Worklist on all open work; new hazard H-29 (relying on best-effort monitoring for clinical follow-up; H-29 because the attachments branch takes H-23 to H-28). No existing rating changed | AI coding agent, for review |
| 0.7 | 16 November 2026 | File attachments: six new hazards, H-23 (a file on the wrong record), H-24 (a missing document), H-25 (malware), H-26 (a file deleted by mistake), H-27 (an out-of-date or duplicate document) and H-28 (file storage unavailable); links from H-01, H-02, H-10, H-19 and H-20. H-23 has the built patient banner (PR #69) as a control, and the restriction guard and anonymisation (PR #68) are causes in H-22, H-23, H-24 and H-26. After the review of #85: hostile images and archives as a cause of H-10, with their bounds; the upload re-checked after staging (H-23); active content in Word and PDF files refused (H-25); storage failures bounded and a 503 (H-28, its defect fixed); anonymisation's file deletions audited (H-26). No earlier rating changed | AI coding agent, for review |
| 0.8 | 16 November 2026 | `feature/email-2fa`: a new cause of H-10 (a member of staff locked out by two-factor or the lockout) with its controls (recovery codes, an admin reset, reset by email); optional two-factor added to H-18's controls. Ratings unchanged, for the CSO to assess | AI coding agent, for review |
| 0.9 | 16 November 2026 | Staff calendars and appointments (`docs/calendar-plan.md`): new draft hazards H-30 to H-36 for the CSO, with ratings left for the CSO to assign; the visit rule added to H-07's existing controls, and the refused deletion of an account with booked appointments to H-21's; three more hazards considered and designed out. On merging 0.4 to 0.6: a restricted record's appointments added to H-22's causes and H-30's, and the patient banner and NHS number to H-34 (drafted as this branch's 0.4). On merging 0.7 (file attachments): the calendar hazards keep H-30 to H-36, which now follow H-29 with no gap. On merging 0.8 (email and two-factor sign-in): no new hazard, so the numbering stands | AI coding agent, for review |
| 0.10 | 17 November 2026 | Several notes per pathway and the pop-out notepad (branch `feature/procedure-notes`): new draft hazard H-37 (a note added from the notepad to the wrong patient's pathway), with its confirmation as the control. Pathway notes can no longer be deleted or overwritten (each edit is a version, by its author only), which strengthens H-03 and H-15 without changing their ratings. After the review of #110: a new cause of H-37 (the same words in two patients' jottings), and "Remove it from the notepad" described as built: it takes out exactly the selection, by its position, or says it can't. Still a draft (0.10, after 0.9), not an issued version | AI coding agent, for review |
| 0.11 | 17 November 2026 | The notepad in a half view, and its explanation removed (branch `feature/notepad-window`): the control "The notepad says it's for jottings, and that anything about a patient belongs in their record" removed by the product owner's decision, with the notepad's prompt, leaving the confirmation as H-37's main control (the staff privacy notice still says what the notepad is for); a new cause of H-37, the notepad in a half view offering the pathway page beside it, with the same confirmation as its control (a separate notepad window was removed before merging, by the product owner's decision). No rating changed. Still a draft, not an issued version | AI coding agent, for review |
| 0.12 | 17 November 2026 | The Needs booking list renamed Requires Appointment (branch `feature/requires-appointment`, shipped with `feature/notepad-window`) and taken out of the side navigation: H-30 now records, as a cause and in its existing controls, that the list is reached from the Calendar page's "Requires Appointment (N)" link, which shows the count, and its proposed control (a visible count in the side navigation or on Home) stays proposed. No rating changed. Still a draft, not an issued version | AI coding agent, for review |
| 0.13 | 18 November 2026 | Shorter record banners (branch `feature/warning-banners`): the restricted, disputed and anonymised banners show a label on screen ("Restricted record: changes paused", "Disputed record: <what's disputed>" when it's short, "Anonymised record"), with the whole message over the page on hover, keyboard focus, a click or a tap; screen readers get it as the label's description, and as an alert when it appears. **A change to H-22's control for the CSO to accept:** "Everyone sees why" now means the label on screen and the reason, and what can still be done, on demand (renamed "Everyone sees that the record is restricted, and why on demand"). After the review of #114, the label is also on Edit Pathway and New Pathway, Edit Pathway gives the restriction as its reason, and the booking dialog shows its own refusal again. H-03's disputed banner reworded to match (what's disputed may now be on demand). No rating changed | AI coding agent, for review |
| 0.14 | 18 November 2026 | Deleting pathway notes (branch `feature/note-deletion`): new draft hazard H-38 (a note deleted wrongly), with its controls (admins only, a reason, a confirmation showing the patient, the pathway and the note, the note kept and in the export and the admins' timeline, the requester told, restricted and anonymised records refused); H-37's "entered in error" further control is now built as admin deletion, and its cause updated. After the review of #115: a new cause (the note edited after the admin read it) with its control (only the version read is deleted), the reason a fixed choice, the author told, and the patient in the direct delete's confirmation. Ratings left for the CSO | AI coding agent, for review |
| 0.15 | 18 November 2026 | Two-factor codes by email (branch `feature/email-2fa-codes`, after its review): an emailed code that doesn't arrive added to H-10's causes for the CSO, with its controls (codes' own share of the email allowance, no promise of a code that can't be sent, resending, recovery codes and the app) | AI coding agent, for review |
| 0.16 | 18 November 2026 | Closed days and appointments recorded after the event (branch `feature/closed-days-walk-ins`): new draft hazard H-39 (a visit recorded after the event that didn't happen, or on the wrong patient), with its built controls; closed days and walk-ins added to H-31 (a walk-in may overlap blocked time or a meeting, flagged; never another appointment), H-32 (an Attended walk-in counts as the visit), H-33 (a closed day that's wrong; the working day rule now includes closed days; bank holidays tested against gov.uk), H-34 (the walk-in dialog shows the patient) and H-35 (a day closed after appointments were booked on it: they stay, flagged; listing them when a day closes proposed). No draft estimate changed. Still a draft, not an issued version After the review of #119: H-39 gets entered in error as a corrective control (a wrong walk-in withdrawn with a reason, its stage's visit asked for again), walk-ins on a closed day allowed, the pathway and stage checks, the visit's notes kept, and the races named; H-35's control for a day closed with bookings made true (what's on it shown before closing, and those staff told). No draft estimate changed. After its follow-up review: H-39's entered in error by admins at any time, no visit asked for again on a restricted record, and a new detective control (a finished stage's only visit withdrawn is told to the pathway's assignees and the admins); a visit earlier on the day its pathway was created is allowed. No draft estimate changed | AI coding agent, for review |
| 0.17 | 18 November 2026 | Referrals, triage and the waiting-time clock, from their design (`docs/centre-phase2-plan.md`), before they're built: new draft hazards H-40 (a referral lost or delayed before triage), H-41 (triaged into the wrong pathway, priority or patient), H-42 (a paused clock forgotten), H-43 (a breach unnoticed, or a wrong target) and H-44 (a duplicate referral), each with its planned controls and draft estimates; H-01 gains a referral registered on the wrong patient as a cause. No earlier draft estimate changed | AI coding agent, for review |
| 0.18 | 18 November 2026 | The community specialist centre's Phases 3 to 5, from their design (`docs/centre-phases-3-5-plan.md`; its safety PRs 11, 18 and 29 combined), before they're built: new draft hazards H-45 to H-50 (specimens, results and letters), H-51 to H-57 (branches and skipped stages, hours and clinics, reminders, missed appointments, scheduled jobs, retention classes) and H-58 to H-64 (MDT outcomes, consent, questionnaires, video links, NHS adapters, several organisations, files awaiting a scan), each with the plan's controls named by feature and PR, and draft estimates; H-01 gains the specimen label as a cause and a control; two more hazards considered and designed out (a lab value or score read by MedApp, a branch chosen by it). No earlier draft estimate changed | AI coding agent, for review |
| 0.19 | 18 November 2026 | Referrals built (branch `feature/referrals`, Phase 2's PR 5): H-40's built controls (the inbox, oldest received first, with its count in the side navigation; no closing without a named person's action and a detail; the restricted and anonymised messages telling staff to give the letter to an admin; archiving a specialty refused while referrals await triage; correcting the specialty by editing), and a note that referrals mustn't be used with patients before triage is built; H-41's (the patient banner on the referral pages and the closing confirmations, the letter beside the referral or "No letter attached yet.", two changes at once refused); H-44's (the duplicate check and its confirmation, entered in error, the patient's Referrals card); H-01's cause marked built. Still planned: triage, its confirmations and the `Duplicate` rejection reason (PR 6), the Specialties page's warning when nobody can triage (PR 6), the clock's days waited and breach badge (PR 7), and the DCB0160 duties. No draft estimate changed | AI coding agent, for review |
| 0.20 | 18 November 2026 | Triage built (branch `feature/triage`, Phase 2's PR 6): H-40's controls now include triage with the registrant told of every outcome (`ReferralTriaged`), the Specialties page's warning when no member can triage a specialty, and the referral page saying why someone can't triage; "nobody can triage yet" removed. H-41's planned triage controls are built (the banner, specialty and template in every confirmation; only the specialty's templates, refused otherwise; nothing prefilled; the permission and membership checked on the server; who decided recorded, with the pathway's Created entry naming the referral; two decisions at once refused, the second told who; the patient's other referrals beside the decision). H-44's Duplicate rejection reason is built. No draft estimate changed | AI coding agent, for review |
| 0.21 | 18 November 2026 | Patient reminders and the Did not attend view built (branch `feature/patient-reminders`, Phase 4's PR 26): H-53's, H-54's and H-55's planned controls recorded as existing ones, with their code names, for the CSO to verify (fixed wording with no clinical words, the patient's chosen channels, each message checked again just before sending to the contact then on the record, the stored copy and its delivery status, "No reminder" shown, retries then a visible failure, the job runner's watch; the Did not attend view, which keeps Rebook and Clinician to review listed until a later appointment exists, the recorded follow-up, and no automatic discharge); new causes (a sender or site name that names the service; the view seen only by those who open it), and H-55's "follow-up recorded but not done" now names Rebook and Clinician to review; H-30 and H-36 gain the reminders and "Tell the patient" as built controls, and H-36's cause "MedApp doesn't contact patients" now says it does only when turned on; H-52's cancelled session can text or email its patients. No draft estimate changed | AI coding agent, for review |
| 0.22 | 18 November 2026 | Letters built (PR 16): H-48 to H-50 list their existing controls; the risk estimates stay drafts for the CSO | AI coding agent, for the CSO |
| 0.23 | 18 November 2026 | Specimens built (branch `feature/specimens`, Phase 3's PR 13): H-45's built controls (the lab worklist's To send, Expected, In the lab, Overdue, Rejected and lost and Reported views; turnaround and Overdue at lab shown; the last stage refused while a specimen is open, naming them; lost recorded with a detail and rejected with a reason, the requesting clinician and taker told; lab steps in any pathway status and on a restricted record; anonymising refused while one is open); H-47's (reporting from the specimen's own page, found by its number typed or scanned, with the patient banner; the label's identifiers; a specimen's report can't be deleted on its own; entered in error before reporting); H-01's specimen label marked built. Still planned: results sign-off and "entered in error sends it back" (PR 14), with no correction for a wrong report until then; the results list (PR 14); Phase 4's service alert; the DCB0160 duties. No draft estimate changed | AI coding agent, for review |
