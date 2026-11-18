# MedApp: roles, referrals, specialties and waiting times

Status: **planned** · 2026-11-18 · Phase 2 of the community specialist centre (`docs/todo.md`, "Community specialist centre") · **owner's answers, 18 November 2026:** open questions 2 to 8 accepted as recommended; question 1 answered with roles and permissions that admins manage, built in this phase (decisions 51 to 68); questions 9 to 11 accepted as recommended; later questions take the recommended option (section 10) · **the safety documents are done in this PR** (`intended-purpose-and-device-decision.md` version 0.9, hazards H-40 to H-44)

**In one paragraph:** a referral-led centre needs a front door. This phase adds **roles and permissions** that admins manage (so each member of staff does only what their job needs), **specialties** (admin-managed, each pathway template belongs to one, staff belong to several), **referrals** (a record of each request from a GP practice, with its letter, in an inbox), a **triage decision by a clinician** (accept into a pathway, reject, or redirect, with a reason), and a **waiting-time clock** on each referral (days waited, a target by specialty and priority, pauses with reasons, breaches shown). It also adds a filter for the stage a pathway has reached. **The clinician decides; the app records.** Nothing is scored, ranked by clinical content or suggested.

It uses the names from the Pathway rename, which is on `main` (#144; `docs/glossary.md`): `PatientPathway`, `PathwayTemplate`, `PathwayChange`, `PathwayWorkflow`, `api/pathways`. Numbers for stored enums and `ActivityAction` and `NotificationKind` are the next free ones today; if another branch takes one first, use the next free one and correct this document.

## 1. Decisions

### Specialties

1. **A specialty** (`Specialty`) is a name (unique, 100 characters) and four waiting-time targets, one per priority (decision 29). Admins add, rename, archive and restore them. One in use (by a template, pathway, referral or member of staff) is archived, not deleted (409 on delete), like stage definitions; archiving one with referrals awaiting triage is a 409, so none is stranded.
   - *Alternative:* a fixed list in code. No: each centre's services differ, and the demo's must be editable.
2. **Every pathway template belongs to one specialty** (`PathwayTemplate.SpecialtyId`, required). A template can be moved to another; that affects only new pathways.
3. **A pathway copies its template's specialty** when it's created (`PatientPathway.SpecialtyId`), as it copies the stages. Lists and Insights filter on the pathway's own copy.
   - *Alternative:* read it through the template. No: moving a template would silently move every old pathway in reports.
4. **Staff belong to any number of specialties** (`StaffSpecialty`), set by admins on the Users page. A GP with a special interest works in dermatology only; a pharmacist or nurse specialist may work across several.
   - *Alternative:* one specialty each. No: the demo clinic's own staff don't fit it.
5. **Membership is what ties triage to a specialty:** a referral is triaged by someone with the `TriageReferrals` permission who is a member of its specialty (decision 18). The inbox starts on your specialties.
6. **Not yet:** calendars, day and week templates, meetings and clinics have no specialty. Phase 4's clinic sessions will. Specialty membership changes nothing about what staff can see.
7. **Existing data:** the migration adds one specialty, **General** (fixed ID, seeded through `HasData`, like the default stage definition), and points every existing template and pathway at it. Admins rename it or move templates out of it.

### Roles and permissions (decisions 51 to 68)

The owner brought roles into this phase, managed by admins on a page of their own (18 November 2026). They're built before referrals, so triage can check a permission. These decisions are numbered from 51, so the ones above keep their numbers.

51. **Permissions are fixed, in code. Roles are data.**
    - **Permissions:** a named list (`Permission`, in `MedApp.Application/Common/Identity/Permissions.cs`). Each has a plain description and a group for the page (decision 68 has the list).
    - **Roles:** admins create, rename and archive them, and choose each role's permissions. A person may hold several roles, and their permissions add up.
    - **Policies check permissions, never role names:** `[Authorize(Policy = nameof(Permission.TriageReferrals))]`. A policy provider makes one policy per permission. Its requirement passes when any of the session's roles grants that permission.
    - **Handlers that check something themselves** (the timeline's full view, deleting someone else's file, changing someone else's calendar) get the caller's permissions on their actor (`PathwayActor.Permissions`, `CalendarActor.Permissions`), in place of today's `IsAdmin`.
    - *Alternative:* a fixed set of roles in code. No: the owner wants centres to shape roles to their own staff.
    - *Alternative:* permissions that admins can define too. No: a permission is only real if code checks it, so only code can add one.
52. **Roles stay Identity roles.** `ApplicationRole` extends `IdentityRole<Guid>` with `Description`, `IsArchived` and `IsBuiltIn`. A new table, `RolePermissions`, holds each role's permissions by name. Membership stays in `AspNetUserRoles`, so sessions, `RolesChanged` and the Users page keep working.
    - **Names, not numbers:** permissions are stored by name, so the list can be reordered freely. Renaming one needs a migration that renames its rows.
    - **Unknown names are ignored:** a stored name that code no longer knows grants nothing. A unit test checks that every name the seed and migrations write is known.
53. **Permissions are read on every request,** with the roles, in the same query (`SessionService`). There's no cache. A change to a role, or to who holds it, applies on each person's next request, and the session cookie needs no change. One indexed join a request is cheap, and a cache would delay removing access.
54. **Every role makes its holders staff.** A base permission, `UseApp`, belongs to every role automatically and isn't a tick. It covers:
    - the person's own account, settings, notifications and notepad;
    - their own calendar;
    - reading other calendars and the setup lists.

    "Has no staff role left" (deleting an account, `SetUserRoles`) then means holding no role at all.
55. **Admin stays special:**
    - `Admin` is the one built-in role (`IsBuiltIn`). It can't be renamed, edited, archived or deleted.
    - Its permissions are fixed in code: every **administration** permission, and every other permission today's admins use. So admins can still do everything they do today.
    - It doesn't have `TriageReferrals`, the clinical decision this phase is built around. An admin who triages holds a role that grants it too.
56. **Administration permissions are for Admin only:**
    - `ManageUsers`, `ManageRoles`, `ManageSecuritySettings`, `ViewAudit`, `ExportPatientRecords`, `ManagePrivacy`, `CorrectRecords`;
    - the page shows them, read only, under "Administrator only";
    - the server refuses them on any other role (400).

    This stops a role editor making admins by another name. No reason was found to let another role have them. Reading the activity log, for an auditor, is the likeliest future exception; it would be a new, separate read permission, not `ViewAudit`.
57. **Nobody locks everyone out:**
    - **The last admin:** taking `Admin` from the last account that holds it (and isn't deleted or locked out) is a 409, and so is deleting that account.
    - **Your own Admin:** you can't take `Admin` from your own account; another admin must. This guards against doing it by accident.
    - **Recovery:** if every admin is locked out anyway, there's a `grant-admin <username>` command beside `create-first-admin`, run by whoever deploys. It's logged as a security event (open question 11).
58. **A role editor can't give itself more than intended:**
    - only holders of `ManageRoles` (Admin alone) edit roles;
    - roles can't carry administration permissions (decision 56);
    - every change is logged with who made it, and told to the other admins (decision 60);
    - before saving, the editor shows what changes and how many people it affects (decision 62).

    An admin giving themselves `TriageReferrals` through a role is allowed: it's recorded, and other admins are told.
59. **Archived and deleted roles:**
    - **Archiving** hides a role from the picker, so nobody new is given it. People who hold it keep its permissions, as archived templates keep applying. To take access away, remove the role from people or remove the permission.
    - **Deleting:** a role nobody holds can be deleted (`RoleDeleted`). One that anyone holds can only be archived (409 on delete), like stage definitions.
    - **Names** are unique (50 characters); descriptions are up to 200 characters.
60. **Every change is logged and told:**
    - **Logged:** creating a role is `RoleCreated` (22); deleting is `RoleDeleted` (23); changing a role's name, description, permissions, or archiving or restoring it, is a new `RoleChanged = 44`. Each records who did it, the role, and the permission names added and removed (never patient data). Giving people roles stays `RolesChanged`.
    - **Told:** each change also goes through `ISecurityEventLog`, and `ISecurityNotices.SendToOtherAdminsAsync` emails the other admins, as two-factor changes do today.
    - **Two admins saving at once:** each save carries the role's `ConcurrencyStamp` (`expectedVersion`). The later save is a 409.
61. **Removing a permission mid-task** needs nothing more than the next request's 403. A grace period would defeat removing access urgently.
    - The 403 carries `code: "permission_missing"` and the permission's description.
    - The web app then reloads `whoami` and hides what the person can no longer do, but keeps an open dialog open, so typed text can be copied out.
62. **The page: Roles and Permissions** (`/admin/roles`, under Administration after Users).
    - **The list:** each role's name, description, how many people hold it, how many permissions it has, and whether it's archived (Admin first, marked built in).
    - **A role's page:**
      - its name and description;
      - its permissions as ticks, grouped (Patients, Referrals, Pathways, Appointments and calendars, Files, Oversight, Setup), each with its description, and a search box that filters by name or description;
      - the Administrator only group, shown read only;
      - **People with this role**, linking to the Users page.
    - **Saving** opens a confirmation listing the permissions added and removed and "This changes what 6 people can do".
    - **Actions:** Archive, Restore and Delete, as decision 59 allows.
    - **The Users page's role picker** lists roles that aren't archived, with their descriptions.
    - **`whoami`** returns the caller's permissions, and the web app hides what they can't do (`useCan(permission)`), replacing its `isAdmin` checks. The server stays the authority.
63. **The holds are unchanged.** The forced password change and the two-factor setup hold run before authorisation, and allow the same endpoints whatever roles someone holds. The setting that requires two-factor authentication applies to holders of `Admin`, as today.
64. **The old roles, migrated:**
    - **`Admin`** becomes the built-in role, with its fixed permissions.
    - **`User`** stays, renamed **"All staff (before roles)"**, as an editable role with exactly today's staff permissions. Every account keeps the roles it has, so nobody loses access on deploy. Admins then move people onto the starting roles, and can archive it once nobody needs it.
    - **The starting roles**, **Clinician**, **Nurse**, **Booking**, **Lab** and **Manager**, are added with the permissions in decision 68, and stay editable.
    - **Any other role** an admin made through today's role endpoints stays, with no permissions, shown as such.
    - **`FirstAdminSetup`** gives the first admin `Admin` alone, which now includes `UseApp`, so it can still change its own password.
    - **`AppRoles.User`** and the `Staff` policy go.
65. **The old role endpoints go:** `POST api/auth/roles`, `roles/assign`, `roles/revoke` and `DELETE roles/{roleName}`, replaced by `api/roles` (section 3). `GET` and `PUT api/auth/users/{username}/roles` stay, taking role IDs; an archived role can't be given (400).
66. **Permissions are per action, not per stage.** A role with `WorkPathways` can work on any stage. Limiting stages to roles waits for Phase 3, which adds a lab permission for specimens (open question 9).
67. **What people see doesn't change.** Every role reads every patient, as today; roles limit what people do. Narrowing what each role sees (by specialty, or hiding restricted patients) builds on this later.
    - *Alternative:* reading limited by specialty now. No: every query and list would need a filter, and the booking team, the lab and the manager work across specialties anyway.
68. **The permissions, and who has them at the start.** Columns:
    - **A** = Admin (fixed)
    - **S** = All staff (before roles)
    - **C** = Clinician, **N** = Nurse, **B** = Booking, **L** = Lab, **M** = Manager (starting roles, editable)

    Every endpoint under today's `AuthPolicies.Staff` maps to a row S has, and every `[Authorize(Roles = "Admin")]` to a row A has.

| Permission | Group | What it allows | A | S | C | N | B | L | M |
|---|---|---|---|---|---|---|---|---|---|
| `UseApp` (every role) | — | Own account, email, two-factor, password, settings; notifications; notepad; own calendar, blocks and templates; reading calendars, meetings (and replying), activity types, closed days, the stage library and templates | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `ViewPatients` | Patients | Patients, their timeline, pathways, appointments, files (list and download) and referrals; FHIR reads | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `EditPatients` | Patients | Add and edit patients; the demographics lookup | ✓ | ✓ | ✓ | ✓ | ✓ | | |
| `DeletePatients` | Patients | Delete a patient with nothing attached | ✓ | ✓ | | | ✓ | | |
| `RegisterReferrals` | Referrals | Register, edit, withdraw, mark entered in error | ✓ | ✓ | ✓ | ✓ | ✓ | | |
| `TriageReferrals` | Referrals | Accept, reject, redirect (also needs membership of the specialty) | | | ✓ | | | | |
| `CreatePathways` | Pathways | Start a pathway without a referral | ✓ | ✓ | ✓ | ✓ | | | |
| `WorkPathways` | Pathways | Stages, stage and pathway notes, details, hold, discharge, cancel; ask for a note's deletion; pause and resume the clock | ✓ | ✓ | ✓ | ✓ | | ✓ | |
| `AssignPathways` | Pathways | Assign and unassign | ✓ | | | | | | ✓ |
| `OverrideStages` | Pathways | Unlock a stage; finish a Required stage without its visit | ✓ | | ✓ | | | | |
| `DeletePathways` | Pathways | Delete a cancelled or archived pathway | ✓ | | | | | | |
| `BookAppointments` | Appointments and calendars | Request, book, move, cancel, record the outcome or after the event, notes, withdraw your own walk-in | ✓ | ✓ | ✓ | ✓ | ✓ | | |
| `ManageAnyCalendar` | Appointments and calendars | Other people's blocks, day templates, default weeks and templates | ✓ | | | | ✓ | | |
| `ArrangeMeetings` | Appointments and calendars | Meetings, the team view, suggested times | ✓ | | | | | | ✓ |
| `AttachFiles` | Files | Upload, and delete your own | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | |
| `ViewInsights` | Oversight | Insights | ✓ | | | | | | ✓ |
| `ManageMonitoring` | Oversight | Your own monitoring rules | ✓ | | | | | | ✓ |
| `ManageSetup` | Setup | Stage library, pathway templates, specialties and their targets, activity types, closed days | ✓ | | | | | | |
| `ManageUsers` | Administrator only | Accounts, giving roles, specialty membership, password and two-factor resets, signing out everywhere, removing an email | ✓ | | | | | | |
| `ManageRoles` | Administrator only | Roles and their permissions | ✓ | | | | | | |
| `ManageSecuritySettings` | Administrator only | Security settings | ✓ | | | | | | |
| `ViewAudit` | Administrator only | The pathway audit log, the activity log, the timeline's full view | ✓ | | | | | | |
| `ExportPatientRecords` | Administrator only | A patient's export | ✓ | | | | | | |
| `ManagePrivacy` | Administrator only | Restriction, dispute, anonymisation, retention review and keeping | ✓ | | | | | | |
| `CorrectRecords` | Administrator only | Delete notes and decide deletion requests; delete anyone's file; withdraw anyone's walk-in | ✓ | | | | | | |

**Rules that asked "is this an admin?" ask for a permission instead:**
- **Assigning:** who can be assigned a pathway: holders of `WorkPathways`.
- **Monitoring:** rules belong to holders of `ManageMonitoring`; an owner who loses it has their rules skipped, as an ex-admin's are today.
- **Note deletion:** requests go to holders of `CorrectRecords`.

**Tests:**
- **Convention test:** it fails any controller action that has none of these: a permission policy, `[AllowAnonymous]`, or a plain `[Authorize]` from a short allowlist (the session, the notepad, email confirmation). It also fails any `Roles =`, `AuthPolicies.Staff` or `User.IsInRole` in the API.
- **`AdminPermissionsTests`:** pins Admin's fixed set to the table.
- **`PermissionCatalogueTests`:** every permission has a description and a group.
- **Integration tests:**
  - one allowed and one refused role for each permission;
  - a permission removed from a role is refused on the next request;
  - the last admin is protected;
  - administration permissions are refused on an editable role.

### Referrals

8. **A referral** (`Referral`) is one request for the centre to see one patient in one specialty. It holds:
   - the patient (always a MedApp patient: decision 11)
   - the specialty asked for
   - when it arrived (`ReceivedOn`, a UK date, typed by staff: not in the future, at most a year back)
   - the referring practice (plain text, required, 200 characters), the referring clinician (plain text, optional, 200) and the practice's **ODS code** (optional; the Organisation Data Service code, 3 to 10 letters and digits, format checked only)
   - the priority the referrer asked for (`RequestedPriority`, the pathway priority scale: Low, Normal, High, Urgent)
   - the reason for referral (free text, 2,000 characters, typed from the letter)
   - who registered it and when.
9. **The letter is an attachment on the referral.** `Attachment` gets a fourth target, `ReferralId`, beside patient, pathway and stage, with every existing attachment rule (sniffed types, audit entries, soft delete). It's optional at registration (a letter can arrive after an e-mailed or phoned request), and the referral page says plainly when there's none.
10. **Statuses** (`ReferralStatus`, stored by number, never renumbered): `AwaitingTriage = 0`, `Accepted = 1`, `Rejected = 2`, `RedirectedElsewhere = 3` (sent to another provider), `Withdrawn = 4`, `EnteredInError = 5`. Only `AwaitingTriage` is open. Redirecting to another specialty here isn't a status: the referral stays `AwaitingTriage` under the new specialty (decision 21).
11. **A referral for someone not in MedApp starts by adding the patient.** The register page finds the patient by NHS number or name; if there's none, it offers **Add patient**, prefilled from the existing demographics lookup (`POST api/demographics/lookup`) for staff to confirm, through the existing `POST api/patients`. Then the referral is created.
    - *Alternative:* a referral holding unmatched patient details until triage. No: a second, unchecked copy of identity details, and a matching step that's a wrong-patient hazard of its own.
12. **Duplicates are shown, never merged.** Registering checks for open referrals (awaiting triage, or accepted with the pathway still open) for the same patient and specialty. If there are any, the server refuses with a 400 listing them unless `ConfirmPossibleDuplicate` is set, as `ConfirmOpenPathways` works; the form shows them (received when, by which practice, status) and asks. At triage, a duplicate is rejected with the reason `Duplicate`.
13. **Details can be edited only while awaiting triage** (`expectedVersion`; 409 if it changed meanwhile), since the decision is made on them. The history records which fields changed, never the old text; the letter is the source of record. After triage, a wrong referral is dealt with through its pathway (cancel it).
14. **Withdrawing** (the referrer or patient withdrew it) and **Entered in error** (registered on the wrong patient, or twice) are for awaiting triage only, by holders of `RegisterReferrals`, each with a detail (500 characters, required). Both close the referral and stop its clock.
15. **Who:** holders of `RegisterReferrals` register, edit and withdraw referrals; holders of `ViewPatients` read them. Only triagers triage (decision 18).
16. **Every change is recorded in the referral's history** (`ReferralEvent`, its action a `ReferralEventAction`), like `AppointmentEvent`: no foreign keys, saved in the same `SaveChangesAsync` as the change, through one place (`ReferralChange`). Its details hold structured values only (statuses, enums, specialty IDs and names, dates, field names), **never free text**: the reason, decision details, pause details and the redirect destination live only on their rows.
17. **Rows have a row version and a `Version` number.** Two triagers on one referral: the second gets a 409 naming who decided and how (`ReferralWorkflow.AlreadyTriagedMessage`). Every change carries `expectedVersion`, so a page loaded before someone else's change is told.

### Triage

18. **Triage needs the `TriageReferrals` permission and membership of the referral's specialty** (403 otherwise, `ReferralWorkflow.WhyCantTriage`). The built-in Admin role doesn't have the permission (decision 55): being an admin doesn't make someone a clinician.
    - *Alternative:* the permission alone. No: a dermatologist would then triage urology referrals.
19. **Accept** takes a pathway template **of the referral's specialty**, a priority, and optionally a title, an assignee and a first note. It creates the pathway through the shared creation code (`PathwayCreation`) and sets the referral `Accepted` with its `PathwayId`, in one save, with the pathway's `Created` audit entry (its details gain `referralId`) and the referral's `Accepted` event. The pathway's target date comes from the clock (decision 32). The monitor and the assignment notification fire as for any new pathway.
    - **The priority isn't prefilled.** The form shows "The referrer asked for: Urgent" beside an empty choice, and the clinician picks one. *Alternative:* prefill the referrer's priority. No: it invites accepting without looking, and a prefilled value reads as the app's suggestion.
    - **Templates are listed by name**, never ranked or suggested. Using another specialty's template means redirecting first.
20. **Reject** takes a reason (`ReferralRejectionReason`, stored by number: `NotOfferedHere = 0`, `DoesNotMeetCriteria = 1`, `InsufficientInformation = 2`, `Duplicate = 3`, `Other = 4`) and a detail (500 characters, required for Other).
21. **Redirect** has two forms:
    - **to another specialty here:** the referral moves to that specialty's inbox, still awaiting triage, with a detail; its clock keeps running from the same `ReceivedOn`, and its target is re-read from the new specialty (decision 31).
    - **elsewhere:** `RedirectedElsewhere`, with where to (plain text, 200 characters) and a detail. Closes it and stops the clock.
22. **Telling the referrer** is outside MedApp in Phase 2 (letters are Phase 3). The registrant is notified of every outcome (section 6), so the booking team can write.
23. **Once triaged, a referral isn't re-triaged.** A wrong acceptance is corrected by cancelling the pathway; a new referral is registered if the patient is to be seen after all.
24. **"Ask the referrer for more information"** isn't an outcome yet (open question 7): today it's a rejection with `InsufficientInformation`.
25. **The confirmation** for every outcome shows the patient as `PatientBanner` does, the specialty and the template chosen (hazard H-41).
26. **The Referral received and Triage stages in the demo templates go:** the referral record replaces them (section 7). Real deployments' templates are theirs to change.

### The waiting-time clock

27. **Every referral has a clock; pathways without a referral don't.** It starts on `ReceivedOn`. Pathways created directly (follow-ups, internal requests) and every existing pathway keep a target date set by hand, exactly as today, with no days waited. No referral is invented for existing pathways.
    - *Alternative:* backfill a referral from each pathway's creation. No: it would invent data (the real arrival date isn't known).
28. **Days are whole UK dates** (`DateOnly`), like target dates. The arithmetic lives in one pure class, `WaitingClock` (`MedApp.Application/Referrals/Common`), with its own tests:
    - `PausedDays`: the total of finished pauses, each `ResumedOn − PausedOn` (a pause started and ended the same day counts 0).
    - `WaitStartOn = ReceivedOn + PausedDays` (stored; changes only on resume).
    - `TargetOn = WaitStartOn + TargetDays` (stored).
    - `AsOf`: `StoppedOn` if stopped, else `PausedSinceOn` if paused, else today (UK).
    - `DaysWaited = AsOf − WaitStartOn`. **Breached** when `AsOf > TargetOn`. A clock that breached before it was paused stays breached.
29. **Targets are per specialty and priority** (`Specialty.TargetDaysLow`, `…Medium`, `…High`, `…Urgent`, 1 to 365 days; new specialties default to 126, 126, 28 and 14). They're the centre's own administrative targets, not national rules.
    - *Alternative:* one centre-wide table. No: an urgent skin referral and a biomechanics referral have different targets in the same centre.
30. **A clock copies its target** (`TargetDays`) when the referral is registered (from the requested priority). Changing a specialty's targets affects only clocks that start afterwards, as templates affect only new pathways.
31. **The target is re-read** (an event `TargetChanged`, old and new days) when the triager chooses the priority, when the referral is redirected to another specialty, and when a running clock's pathway changes priority. Nothing else moves it.
32. **The pathway's target date is the next date someone must act on:**
    - **running:** the end of `TargetOn` (UK), so today's Overdue, My Worklist's order, Insights and the "target date changed" monitoring rule all keep working unchanged;
    - **paused:** the end of the pause's review date (decision 35), so a forgotten pause shows as overdue;
    - **stopped with the pathway still open:** left as it was, and editable by hand again.
    - While the clock runs or is paused, the Edit Pathway page shows the target date read-only ("from the waiting-time clock"), and `PATCH` refuses a `DueDate` (400, `WaitingClock.TargetFromClockMessage`).
    - Each move goes through `PathwayChange`, as a `DetailsUpdated` entry with `changes.dueDate` in today's format plus `"source":"clock"`, so Insights' history and the timeline's DueDates kind read it as they read a hand edit.
    - *Alternative:* a separate target column beside `DueDateUtc`. No: every overdue rule, list sort, Insights query and monitoring event would need changing twice.
33. **Pauses are for patient-initiated delays only, after acceptance.** A pause (`ReferralClockPause`) has a reason (`ClockPauseReason`, stored by number: `PatientChoseToWait = 0`, `PatientUnavailable = 1`, `Other = 2`), a detail (500 characters, required for Other), a review date, and who paused and resumed it, when. It starts and ends **today** (no backdating). One open pause per referral (a filtered unique index). Holders of `WorkPathways` pause and resume.
    - **On hold doesn't pause the clock.** "Waiting for histology" is the centre's own delay and counts. *Alternative:* pause on hold. No: it would hide the service's own waits from its figures.
    - **Before triage, the clock always runs.** The patient hasn't been offered anything yet.
34. **To change a review date,** resume and pause again; each is recorded.
35. **The review date** is required, from tomorrow to 90 days ahead. The **Paused clocks** view lists every open pause by review date, with past ones flagged (hazard H-42).
36. **The clock stops** (`StoppedOn`, `ClockStopReason`, stored by number: `StopStageFinished = 0`, `PathwayDischarged = 1`, `PathwayCancelled = 2`, `Rejected = 3`, `RedirectedElsewhere = 4`, `Withdrawn = 5`, `EnteredInError = 6`) when:
    - the template's **clock-stop stage** is finished: `PathwayTemplateMap.StopsClock` (at most one per template, set by admins in the template editor: "The waiting-time clock stops when this stage is finished"), copied onto the pathway's stage (`PatientPathwayStage.StopsClock`) like the visit requirement; or, with none,
    - the pathway is discharged (`Completed`) or cancelled; or
    - the referral is closed without a pathway (rejected, redirected elsewhere, withdrawn, entered in error).
    - A stopped clock never restarts, even if an admin unlocks the stage or the pathway reopens.
37. **Stopping is part of the change that causes it:** `PathwayChange` loads the pathway's referral with it (the `GetForUpdateOrThrowAsync` guard includes it) and stops the clock in the same save, with a `ClockStopped` event.
38. **Breach is shown, never sent.** Lists and pages show "Breached" (an error badge, like Overdue) and "Day 84 of 126". No notification, email or escalation in Phase 2: that needs a scheduled job (Phase 4's monitoring rules).

### Lists and filters

39. **The referral inbox** is the Referrals page's first view: referrals awaiting triage, longest wait first, filtered to the viewer's specialties when they have any (in the URL, so it can be changed and saved).
40. **The pathway list sorts by days waited** (`sort=daysWaited`; pathways with no clock last), shows a **Wait** column ("Day 84 of 126", Breached, Paused), and filters by `breachedOnly`. Sorting by days waited is date arithmetic (`DateOnly.DayNumber`, which EF translates; a `ToQueryString` test as Insights has), then `Id`.
41. **Filters by specialty** (`specialtyIds`) on the pathway list, the referral inbox and Insights (`specialtyId`, applied to the pathways' own specialty; drill-down links carry it).
42. **Filter by the stage reached** (`stageDefinitionIds`, any of): pathways whose **current stage** (the first unfinished one, by order, as `PathwayWorkflow` defines it) comes from one of those stage definitions. Open pathways only. Written as LINQ that EF translates and that runs in memory, like `PathwaySearch`. So a saved search "Waiting for histology" is the filter `stage=<Histology (lab)>`.

### Privacy

43. **Referrals are patient data,** special category, from a third party (the referrer). They follow [the privacy guide](agents/privacy.md) in full.
44. **Restricted and anonymised records:** registering, editing, triaging, redirecting, resuming and attaching are refused (409, `PatientRules.EnsureCanChange`). **Withdrawing and pausing are allowed** while restricted, as blocking and cancelling a pathway are, so care can be stopped safely. Restricting a patient counts referrals awaiting triage with the open pathways and appointments (`ConfirmOpenPathways`). The register page's 409 says to give the letter to an admin, so it isn't lost (hazard H-40).
45. **Anonymising is refused while a referral awaits triage.** It replaces the reason, the referrer's practice and clinician, the decision detail, the redirect destination and every pause's detail with placeholders, and clears the ODS code. Dates, statuses, enums and the specialty stay (pseudonymised, as today). Events hold no free text, so they stay.
46. **The activity log:** opening a referral records `ReferralViewed = 43` against the patient (the referral's ID in its details), deduplicated like other views; the inbox and referral search record `PatientListed` for each patient shown. Admins changing someone's specialties records `StaffSpecialtiesChanged = 42` (the account as target, specialty IDs in details). Referral changes go in the referral's history in the same save (decision 16), as pathway changes go in the pathway audit and appointment changes in their history; they aren't written twice. Every new GET is classified in `EndpointCoverageTests`.
47. **Free text** (the reason, decision and withdrawal details, pause details, the redirect destination) never reaches a staff timeline, a notification, an email or a log line. Admins see it on the timeline; staff see it on the referral page, as they see notes.
48. **Patients with referrals can't be deleted** (409), as with pathways and appointments. Deleting a cancelled pathway clears its referral's `PathwayId` in the same save; the referral and its history stay.
49. **Retention:** the referral history counts towards the conclusion of treatment (`RetentionSchedule`), so a patient with only a rejected referral has a clock from the rejection.

### Safety

50. **The clinician decides; the app records.** No scoring, no ranking by clinical content, no automatic priority, no suggested outcome or template. Orders are by dates (received, days waited, target, review date) or by labels staff chose (priority, specialty). The device reasoning and the new draft hazards (H-40 to H-44) are in section 8, and are already in the clinical safety documents (this plan's PR), before any feature is built.

## 2. The data model

**New tables** (one `IEntityTypeConfiguration` each, in `Infrastructure/Data/Configurations/<Area>/`):
- **`Specialties`:** `Id`, `Name` (nvarchar 100, unique index), `IsArchived`, `TargetDaysLow`, `TargetDaysMedium`, `TargetDaysHigh`, `TargetDaysUrgent` (int), `CreatedAt`, `LastUpdated`. Seeded: General.
- **`StaffSpecialties`:** `UserId` (FK to `AspNetUsers`, cascade: goes with the account), `SpecialtyId` (FK, restrict). Key (`UserId`, `SpecialtyId`).
- **`RolePermissions`:** `RoleId` (FK to `AspNetRoles`, cascade), `Permission` (varchar 64, the name). Key (`RoleId`, `Permission`).
- **`Referrals`:**
  - `Id`, `PatientId` (FK, restrict), `SpecialtyId` (FK, restrict), `ReceivedOn` (date)
  - `ReferrerPractice` (nvarchar 200), `ReferrerClinician` (nvarchar 200, null), `ReferrerOdsCode` (varchar 10, null), `RequestedPriority` (int), `Reason` (nvarchar 2000)
  - `Status` (int), `RegisteredAtUtc`, `RegisteredByUserId`, `RegisteredByUsername`
  - the decision: `DecidedAtUtc`, `DecidedByUserId`, `DecidedByUsername`, `RejectionReason` (int), `DecisionDetail` (nvarchar 500), `RedirectedTo` (nvarchar 200), all null until decided
  - `PathwayId` (FK to `PatientPathways`, no action; unique where not null)
  - the clock: `TargetDays` (int), `WaitStartOn`, `TargetOn` (date), `PausedDays` (int), `PausedSinceOn`, `PauseReviewOn` (date, null; the open pause's, copied for sorting), `StoppedOn` (date, null), `StopReason` (int, null)
  - `Version` (int), `RowVersion` (rowversion), `LastUpdated`
  - indexes: (`Status`, `SpecialtyId`, `ReceivedOn`) for the inbox (`WaitStartOn` in place of `ReceivedOn` once the clock comes); `PatientId`; `PauseReviewOn` where not null.
- **`ReferralClockPauses`:** `Id`, `ReferralId` (FK, restrict), `PatientId` (copied, for the export), `PausedOn`, `PausedAtUtc`, `PausedByUserId`, `PausedByUsername`, `Reason` (int), `Detail` (nvarchar 500, null), `ReviewOn`, `ResumedOn`, `ResumedAtUtc`, `ResumedByUserId`, `ResumedByUsername` (null until resumed). Unique index on `ReferralId` where `ResumedAtUtc IS NULL`.
- **`ReferralEvents`:** `Id`, `ReferralId`, `PatientId`, `PathwayId` (null), `Action` (int), `AtUtc`, `UserId` (null), `Username`, `Details` (JSON, structured values only). No foreign keys. Indexes (`ReferralId`, `AtUtc`) and (`PatientId`, `AtUtc`, `Id`) for the timeline.

**New columns:**
- `AspNetRoles`: `Description` (nvarchar 200), `IsArchived`, `IsBuiltIn` (bit), `CreatedAt`, `LastUpdated` (`ApplicationRole`). The migration marks `Admin` built in, renames `User` to "All staff (before roles)" with today's staff permissions (decision 64), and adds the five starting roles with fixed IDs and their permissions.
- `PathwayTemplates.SpecialtyId` and `PatientPathways.SpecialtyId` (FK, restrict, required; General for existing rows).
- `PathwayTemplateMaps.StopsClock` and `PatientPathwayStages.StopsClock` (bit, default 0).
- `Attachments.ReferralId` (null, no FK, indexed). `Notifications.ReferralId` (null, no FK).

**Stored enums, new** (never renumbered): `ReferralStatus` (decision 10), `ReferralRejectionReason` (20), `ClockPauseReason` (33), `ClockStopReason` (36), and `ReferralEventAction`: `Registered = 0`, `DetailsEdited = 1`, `SpecialtyChanged = 2`, `Accepted = 3`, `Rejected = 4`, `RedirectedElsewhere = 5`, `Withdrawn = 6`, `EnteredInError = 7`, `ClockPaused = 8`, `ClockResumed = 9`, `ClockStopped = 10`, `TargetChanged = 11`.

**Stored enums, added to:** `ActivityAction` (`StaffSpecialtiesChanged = 42`, `ReferralViewed = 43`, `RoleChanged = 44`), `NotificationKind` (`ReferralTriaged = 23`).

**Every stored value the build needs, assigned here** so that agents working in parallel don't collide. Use exactly these. If `main` has taken one by the time a PR is built, take the next free value, and correct this table in the same PR.

| What | Stored as | Values | PR |
|---|---|---|---|
| `ReferralStatus` | int | `AwaitingTriage = 0`, `Accepted = 1`, `Rejected = 2`, `RedirectedElsewhere = 3`, `Withdrawn = 4`, `EnteredInError = 5` | 5 |
| `ReferralRejectionReason` | int | `NotOfferedHere = 0`, `DoesNotMeetCriteria = 1`, `InsufficientInformation = 2`, `Duplicate = 3`, `Other = 4` | 5 (column), 6 (used) |
| `ReferralEventAction` | int | `Registered = 0`, `DetailsEdited = 1`, `SpecialtyChanged = 2`, `Accepted = 3`, `Rejected = 4`, `RedirectedElsewhere = 5`, `Withdrawn = 6`, `EnteredInError = 7`, `ClockPaused = 8`, `ClockResumed = 9`, `ClockStopped = 10`, `TargetChanged = 11` | 5 (all declared), 6 and 7 (used) |
| `ClockPauseReason` | int | `PatientChoseToWait = 0`, `PatientUnavailable = 1`, `Other = 2` | 7 |
| `ClockStopReason` | int | `StopStageFinished = 0`, `PathwayDischarged = 1`, `PathwayCancelled = 2`, `Rejected = 3`, `RedirectedElsewhere = 4`, `Withdrawn = 5`, `EnteredInError = 6` | 7 |
| `ActivityAction` (new) | int | `StaffSpecialtiesChanged = 42`, `RoleChanged = 44`, `ReferralViewed = 43` | 1, 4, 5 |
| `ActivityAction` (reused) | int | `RoleCreated = 22`, `RoleDeleted = 23`, `RolesChanged = 21` | 4 |
| `NotificationKind` | int | `ReferralTriaged = 23` | 6 |
| `PatientPathwayAuditAction` | int | **None new.** Accepting writes the existing `Created = 0` (with `referralId` in its details); the clock moves the target date through `DetailsUpdated = 1` (`changes.dueDate`, `"source":"clock"`). 16 onwards stay free | — |
| `PatientTimelineKind` | not stored | Referrals and Waiting time are added; regroup freely | 5, 7 |
| Permissions | text (`RolePermissions.Permission`, varchar 64) | The names in decision 68's table, exactly: `UseApp` (implicit, never stored), `ViewPatients`, `EditPatients`, `DeletePatients`, `RegisterReferrals`, `TriageReferrals`, `CreatePathways`, `WorkPathways`, `AssignPathways`, `OverrideStages`, `DeletePathways`, `BookAppointments`, `ManageAnyCalendar`, `ArrangeMeetings`, `AttachFiles`, `ViewInsights`, `ManageMonitoring`, `ManageSetup`, `ManageUsers`, `ManageRoles`, `ManageSecuritySettings`, `ViewAudit`, `ExportPatientRecords`, `ManagePrivacy`, `CorrectRecords`. Renaming one needs a migration that renames its rows | 2 |
| Fixed IDs | Guid | The General specialty `5a1d0c4e-7b2f-4e61-9c3a-000000000001`; the starting roles Clinician `…-000000000011`, Nurse `…-000000000012`, Booking `…-000000000013`, Lab `…-000000000014`, Manager `…-000000000015` (each `5a1d0c4e-7b2f-4e61-9c3a-` plus those digits). `Admin` and `User` keep their existing IDs and are found by name | 1, 2 |
| Migrations | name, in order | `AddedSpecialties`, `AddedRolePermissions`, `AddedReferrals`, `AddedReferralNotifications`, `AddedWaitingTimeClock`, each on top of `main`'s latest (today `20261118075440_RenamedProceduresToPathways`), so each one's timestamp is later than the one before | 1, 2, 5, 6, 7 |

**PR 5 (referrals) changed nothing in this table:** it used the numbers exactly as assigned (`ReferralStatus`, `ReferralRejectionReason`, every `ReferralEventAction` declared, `ActivityAction.ReferralViewed = 43`, and the migration `AddedReferrals`).

**Where the rules live** (`MedApp.Application`):
- `Specialties/`: commands, queries, `SpecialtyMapper`, `ISpecialtyRepository`; membership under `Authentication/Users/` (`SetUserSpecialties`), as roles are.
- `Referrals/Common/`: `ReferralWorkflow` (statuses, who may do what, messages), `ReferralChange` (applies a change, writes its events), `WaitingClock` (decision 28's arithmetic, and the pathway's target date), `ReferralMapper`, `ReferralNotifications`, `OdsCodes` (the format check).
- `Referrals/Commands/`: `RegisterReferral`, `EditReferral`, `AcceptReferral`, `RejectReferral`, `RedirectReferral`, `WithdrawReferral`, `MarkReferralEnteredInError`, `PauseClock`, `ResumeClock`. `Referrals/Queries/`: `GetReferralById`, `SearchReferrals` (with `ReferralSearch`, LINQ that runs in SQL and memory).
- `IReferralRepository` shares the DbContext with the pathway repository, so accepting (referral and new pathway) and clock changes (referral and pathway target date) save in one `SaveChangesAsync`, as note deletion requests do.

## 3. The API

Each endpoint names its permission (decision 68); triagers are holders of `TriageReferrals` in the referral's specialty. Every change takes `expectedVersion`. Lists return `PagedResponse<T>`.

| Endpoint | What |
|---|---|
| `GET api/permissions` | **`ManageRoles`.** The catalogue: each permission's name, group, description, and whether it's for Admin only |
| `GET api/roles`, `GET api/roles/{id}` | **`ManageRoles`** (the list also for `ManageUsers`). Each role with its permissions and how many people hold it; one role adds who holds it |
| `POST api/roles`, `PUT api/roles/{id}`, `POST …/{id}/archive`, `…/restore`, `DELETE …/{id}` | **`ManageRoles`.** `{name, description, permissions[], expectedVersion}`. Admin's role refuses every change (409); an Admin-only permission is a 400 |
| `GET api/specialties` (`includeArchived`) | The list, with each one's targets and whether any member can triage it |
| `POST`, `PUT api/specialties/{id}`, `POST …/{id}/archive`, `…/restore`, `DELETE …/{id}` | **`ManageSetup`.** Name and targets |
| `GET`, `PUT api/auth/users/{username}/specialties` | **`ManageUsers`.** `[specialtyId]`. `GET api/auth/me/specialties` for your own |
| `POST api/referrals` | Register: `{patientId, specialtyId, receivedOn, referrerPractice, referrerClinician?, referrerOdsCode?, requestedPriority, reason, confirmPossibleDuplicate}`. 201 |
| `GET api/referrals/search` | `status`, `specialtyIds`, `mine` (my specialties), `patientId`, `breachedOnly`, `paused`, `sort` (`daysWaited`, `receivedOn`, `targetOn`, `reviewOn`, `patient`) |
| `GET api/referrals/{id}` | The referral, its clock, pauses, history (free text in it for `ViewAudit` only), `CanTriage`, `CanEdit`, `WhyCantTriage`, and the patient's flags for the banner |
| `PUT api/referrals/{id}` | Edit details (awaiting triage only) |
| `POST api/referrals/{id}/accept` | **Triagers.** `{templateId, priority, title?, assigneeUserId?, notes?}`. 201, `Location` the pathway |
| `POST api/referrals/{id}/reject` | **Triagers.** `{reason, detail?}` |
| `POST api/referrals/{id}/redirect` | **Triagers.** `{specialtyId}` or `{elsewhere}`, with `detail` |
| `POST api/referrals/{id}/withdraw`, `…/entered-in-error` | `{detail}` |
| `POST api/referrals/{id}/clock/pause`, `…/clock/resume` | `{reason, detail?, reviewOn}`; resume takes nothing else |
| `POST api/attachments?referralId=`, `GET api/attachments?referralId=` | The letter, under every existing attachment rule |
| `GET api/pathways/search` | Gains `specialtyIds`, `stageDefinitionIds`, `breachedOnly`, `sort=daysWaited`; each summary gains its clock (`daysWaited`, `targetDays`, `breached`, `paused`) |
| `GET api/pathways/{id}` | Gains `referral` (ID, received, specialty) and its clock |
| `GET api/pathways/insights` | Gains `specialtyId` |

Errors as house style: `NotFoundException` (404), `ConflictException` (409: already triaged, changed meanwhile, restricted, closed, an open pause already), validation (400), 403 without the permission (`code: "permission_missing"`) or the specialty. After the DTOs change, `npm run gen:api`.

## 4. The web app

**Side nav:**
- **Referrals** (`/referrals`), between My Worklist and Patients, with the count awaiting triage in your specialties (all, if you have none), like Requires Appointment's count.
- **Roles and Permissions** (`/admin/roles`) and **Specialties** (`/admin/specialties`), in Admin after Users.

**Pages** (Astryx, TanStack Query, filters and sort in the URL, a Vitest test each):
- **Referrals** (`/referrals`): views **Awaiting triage** (the inbox), **Waiting list** (accepted, clock running), **Paused clocks** (by review date, past ones flagged) and **Closed**. Columns: patient, specialty, received, wait ("Day 84 of 126", Breached), requested priority, practice, letter (yes or no).
- **Register referral** (`/referrals/new`, and from a patient's page with the patient chosen): find or add the patient (decision 11), the details, then the letter upload, then the referral's page. Duplicates shown inline (decision 12).
- **Referral** (`/referrals/{id}`): the banner, details, the letter (thumbnail or download), the clock, the history, and for triagers **Accept**, **Reject**, **Redirect**; for others, why they can't. **Withdraw** and **Entered in error** in its menu.
- **Patient page:** a **Referrals** card (open and recent, linking to each) and **Register referral**.
- **Pathway page:** a **Referral and wait** card: received, specialty, day N of target, Breached or Paused, **Pause clock** and **Resume clock**, past pauses.
- **Pathways list:** the Wait column and sort, **Specialty** and **Stage reached** filters, Breached only.
- **Pathway Templates:** a **Specialty** field, and the clock-stop stage choice.
- **Users:** the role picker (roles that aren't archived, with descriptions) and a **Specialties** action (a dialog of ticks). Account Settings shows your own roles and specialties, read only.
- **Roles and Permissions:** decision 62.
- **Everywhere:** actions shown by permission (`useCan`), not `isAdmin`.
- **Insights:** a **Specialty** choice.

New screens go in `tools/ui-parity/screens.ts`, with the axe audit run on them.

## 5. Timeline, export and anonymisation

- **Timeline:** a third feed, `ReferralEvent`, merged by `AtUtc` then `Id` with the audit and appointment feeds under one cursor. New kinds (not stored): **Referrals** (registered, edited, specialty changed, accepted, rejected, redirected, withdrawn, entered in error) and **Waiting time** (paused, resumed, stopped, target changed).
  - **Staff see** the action, who, when, the specialty, the requested and chosen priority, the outcome and rejection reason by name, the pause reason by name, the review date, target days and the pathway (a link). **Never** the reason, details, redirect destination or referrer.
  - **Admins see** those too, read from the referral and pause rows.
  - `TimelineStaffCanaryTests` gains every new action, with the canary string in each free-text field.
  - The overview lists referrals awaiting triage beside open pathways.
- **Export** (`PatientRecordRepository`, `Exported…Response`): every referral with its details, decision, clock, pauses and events, staff by username only; referral letters in the files list (attached to "Referral"). The printable page gains a Referrals section.
- **Anonymisation:** decision 45. `PatientAnonymiser` gains the referral and pause fields; attachments on referrals are renamed and deleted like the rest.

## 6. Notifications

- **`ReferralTriaged = 23`**, to whoever registered the referral, unless they triaged it: fixed title "Referral triaged"; message from the username, the outcome by name and the specialty (an admin-written name): "k.ashford accepted a Dermatology referral." It carries `ReferralId` and opens the referral. `Notification.HasFixedTitle` covers it, so anonymising leaves it.
- **No notification for a new referral.** The inbox count is the signal. *Alternative:* tell every triager. No: dozens a day would train people to ignore the bell. Phase 4's monitoring adds "not triaged in N days", which needs a scheduled job.
- No email in Phase 2.

## 7. Demo data

In `tools/MedApp.DevMcp/DemoData` (run by `tools/seed-demo-data.sh`), through the shared code the handlers call (`ReferralChange`, `WaitingClock`, `PathwayCreation`), never rows written around the rules:
- **Specialties (PR 1):** Dermatology, Gynaecology, Urology, Diabetes and Biomechanics, with targets (dermatology urgent 14 days, routine 126; biomechanics routine 84, and so on). Each template in its specialty, the archived one too. Memberships: consultants, the GP with a special interest and the nurse specialists in theirs; the pharmacist in several; the booking coordinator and lab in none.
- **Roles (PR 2):** each demo account gets the starting roles its job needs, never "All staff (before roles)": consultants, the GP with a special interest, the podiatrist and the pharmacist get Clinician; the nurse specialists and the healthcare assistant get Nurse; the booking coordinator gets Booking; the biomedical scientist gets Lab; the service manager gets Manager; the clinical lead gets Admin and Clinician. From PR 4, a custom role ("Visiting consultant": Clinician without `OverrideStages`) for the visiting consultants, and one archived role, made through the role commands, show the page. The second admin, the new starter, the locked-out account and the deleted leaver keep their stories. `reset_demo_data` puts the starting roles' permissions back as they were.
- **`medapp-dev`:** `ensure_test_admin` gives `admin` Admin and Clinician, and membership of every specialty, so it can show triage; `create_user` and `set_roles` take any role's name; `list_roles` shows each role's permissions.
- **Referrals (PR 9):** every seeded pathway starts from a referral, registered by the booking coordinator, received a few days before (urgent ones sooner), triaged by a Clinician of its specialty. Around 10% rejected (reasons spread), a few redirected (one to another specialty here, one elsewhere), a few withdrawn, one entered in error. About 20 awaiting triage at "now", a few of them breached. Letters (the existing synthetic referral letters) attached to referrals, not patients.
- **Clocks:** clock-stop stages set on each template (the treatment or first clinic visit stage). About 10 paused clocks now, one with its review date passed (the H-42 control to show). Some breaches among open pathways and stopped ones.
- **Stages:** "Referral received" and "Triage" leave the demo templates (decision 26).
- **Saved searches:** "Waiting for histology" (stage reached: Histology (lab)) and "Longest waits" (`sort=daysWaited`).
- **Referrers:** invented practice and clinician names, checked against the NHS ODS search before committing, with no ODS codes. Synthetic only.
- **Tests:** `DemoDataSeedTests` checks triage after receipt, pauses inside their clock, no event after now, `WaitStartOn` and `TargetOn` consistent with the pauses, and every pathway's target date as decision 32 says. `DemoActivityParityTests` covers `ReferralViewed` and the inbox's `PatientListed`. `docs/demo-data.md` tours the inbox, a triage, a pause and a breach.

## 8. Governance and safety documents

**Clinical safety** (`docs/clinical-safety/`). The first two are **done in this plan's PR**, so the build starts with them in place:
- **`intended-purpose-and-device-decision.md`** (version 0.9): a reassessment saying why each feature stays outside the device definition:
  - **Specialties:** organisational labels.
  - **Referrals:** an administrative record of an incoming request, stored and shown unchanged; the letter is a file, not read.
  - **Triage:** a clinician chooses the outcome, template and priority; MedApp records who and when. It offers no suggestion, prefills no priority, and lists templates by name. The "Not for deciding who is seen, when or how soon" line gains: "it records a clinician's triage decision; it doesn't triage".
  - **The clock and breaches:** date arithmetic on dates staff entered and the organisation's own targets, chosen by priority a clinician set. A breach is an administrative waiting-time measure, like Overdue, not a clinical alert. The roadmap row "Waiting-list tracking" is updated: built, the centre's targets rather than national rules, priority clinician-assigned.
  - **Sorting by days waited, and the stage filter:** orders and filters by dates and labels staff set, not clinical values.
  - **Claims the manufacturer must not make** gains "MedApp triages referrals" and "MedApp tells you which patients are most urgent".
- **`hazard-log.md`** (version 0.17): new drafts for the CSO, with ratings left to the CSO and draft estimates:
  - **H-40 A referral is lost or delayed before triage** (never registered, wrong specialty, unread inbox, nobody can triage the specialty, refused for a restricted patient). Controls: the inbox, longest wait first, with its count in the side nav; no way to close a referral without a named outcome; Specialties warns when no member holds `TriageReferrals`; archiving refused with referrals waiting; the restricted 409 says to give it to an admin; DCB0160: a daily inbox check and a paper fallback.
  - **H-41 A referral is triaged into the wrong pathway, priority or patient.** Controls: the banner on the referral page and every confirmation; the letter beside the decision; templates limited to the specialty; no prefilled priority; who and when recorded; correction by cancelling the pathway.
  - **H-42 A paused clock is forgotten.** Controls: a required review date at most 90 days ahead; the pathway's target date becomes the review date, so it shows Overdue; the Paused clocks view.
  - **H-43 A breach goes unnoticed, or the target is wrong** (wrong specialty targets, wrong priority). Controls: Breached on every list, sort by wait, the target's days shown with the specialty; DCB0160: the organisation reviews its waiting list and its targets. Not an alerting system (Phase 4).
  - **H-44 A duplicate referral leads to two pathways for one need.** Controls: the duplicate check and confirmation; the `Duplicate` rejection reason.
  - **H-01** (wrong patient) gains registering a referral on the wrong patient as a cause.
- **Still to do, in the referrals and triage PRs:** `clinical-safety-case-report.md` and `dcb0160-deploying-organisations.md`: the new hazards, and the deploying organisation's duties (set targets, give the triage permission to the right people, check the inbox daily, keep a downtime process for referrals).

**Governance** (`docs/governance/`), each in the PR that adds the data:
- **`data-inventory.md`** and **`dpia.md`:** referrals (special category, from the referrer: a new source), letters, pause reasons, referrer details (a GP's name: personal data of a third party), staff specialty memberships, roles and permissions. New DPIA risks: 27 (referral free text and letters reaching places they shouldn't), 28 (staff attribution of triage decisions).
- **`ropa.md`**, **`privacy-notice-patients-template.md`** (data received from the referrer), **`privacy-notice-staff-template.md`** (roles, specialties, decisions recorded by name), **`data-subject-rights.md`** (the export covers referrals), **`retention-schedule.md`** (referrals kept with the record).
- **Security** (`docs/security/`), in the roles PRs: `threat-model.md` (elevation of privilege through role editing, the last admin, a page left open after a permission is removed) and `asvs.md` (V8 authorisation: least privilege, deny by default, checks on the server, administrative interfaces); also `docs/governance/security-policies.md` (access control), and `dpia.md` and `isms-risk-register.md` (the risk that any member of staff can change any record, now reduced).
- **Agent guides:** a new `docs/agents/referrals.md`; `auth.md` (roles and permissions); `privacy.md`, `timeline.md`, `notifications.md`, `attachments.md`, `insights.md` and `pathways.md` updated; `AGENTS.md`'s project table and mapper list.

## 9. Building it: the PRs in order

The clinical safety documents are already done (section 8), so the build starts with PR 1. Each PR is one implementation agent's work, with its migration (reviewed, `has-pending-model-changes` clean, tested up and down on LocalDB) and its tests. Each keeps the seed running. **Migrations are one linear history,** so PRs with a migration merge in this order; work without one can run in parallel. Every stored number is assigned in section 2's table, so parallel branches don't collide.

1. **Specialties.** Migration `AddedSpecialties` (the table, General through `HasData`, `SpecialtyId` on templates and pathways, `StaffSpecialties`). Admin page, template field, pathway copy, Users dialog, `StaffSpecialtiesChanged`. For now these sit behind today's Admin role; PR 2 moves them to `ManageSetup` and `ManageUsers`. Seed: specialties and memberships. Tests: handlers, validators, `SpecialtyMapper`, archiving rules, migration test (existing rows get General), Vitest for the pages.
2. **Permissions and the policy layer.** Migration `AddedRolePermissions` (`ApplicationRole`'s columns, `RolePermissions`, Admin built in, User renamed with today's staff permissions, the five starting roles). The `Permission` catalogue, the policy provider, permissions read with the session, every endpoint and handler moved from `Staff`, `Roles = "Admin"` and `IsAdmin` to permissions, the last-admin rules, `whoami`'s permissions, `FirstAdminSetup`, the `grant-admin` command, and the convention test. It changes nothing for existing accounts: Admin and "All staff (before roles)" can do what they did. Seed and `medapp-dev`: the demo staff's roles. Security documents (decision 65). The largest PR, but mostly mechanical; split the endpoint move by controller over two agents if one can't finish it.
3. **The web app follows permissions.** `useCan`, every `isAdmin` check replaced, the `permission_missing` 403 handling, and the Users page's role picker from `api/roles`. No migration.
4. **The Roles and Permissions page.** `api/permissions` and `api/roles` (create, edit, archive, restore, delete, `RoleChanged`, notices to other admins), the page (decision 62), the old role endpoints removed. Integration tests for the last-admin and Admin-only rules. UI parity screens and the axe audit for the page.
   - **Built (`feature/roles-page`), with these choices taken on the owner's behalf:** `PUT api/auth/users/{username}/roles` still takes role **names**, not IDs (names are unique, and pages loaded before keep working in a rollout); `GET api/auth/roles` stays one release, deprecated (`docs/todo.md`); `GET api/roles` needs `ManageUsers` (the Users page reads it; only Admin holds it, as `ManageRoles`); deleting a role takes no `expectedVersion` (only a role nobody holds can go, checked in the same statement); "how many people it affects" is how many hold the role (`holderCount`), with no separate preview endpoint; the role's version is Identity's `ConcurrencyStamp`, sent as a string.
5. **Referrals.** Migration `AddedReferrals` (`Referrals` with the decision columns but not the clock's, `ReferralEvents`, `Attachments.ReferralId`). Register (with the duplicate check), edit, withdraw, entered in error, the letter, the Referrals page (inbox ordered by received date), the referral page, the patient's card. Privacy all in: restriction guard, `ReferralViewed` and `PatientListed`, `EndpointCoverageTests`, export, anonymisation, timeline feed and canary, retention. Governance documents for referral data.
6. **Triage.** Migration `AddedReferralNotifications` (`Notifications.ReferralId`). Accept (pathway created in the same save), reject, redirect both ways, the `TriageReferrals` and specialty checks, `ReferralTriaged`, the pathway page's referral link, the template choice and confirmations. An E2E journey: register a referral, accept it as a Clinician, find the pathway. Integration tests: two triagers at once (one 409), the permission without the specialty (403), accept on a restricted patient (409).
7. **The waiting-time clock.** Migration `AddedWaitingTimeClock` (the clock columns, targets on specialties, `ReferralClockPauses`, `StopsClock` on template maps and stages; existing referrals get their target from today's specialty table). `WaitingClock` with thorough tests (clock changes, same-day pauses, breaches before a pause), pause and resume, stop from `PathwayChange`, the pathway target date rule and the `PATCH` refusal, priority changes, the Wait column and sort, the Paused clocks view, the pathway card, the template editor's clock-stop choice. Insights' history reads `source: clock` entries (a test).
8. **Filters.** Specialty and stage reached on the pathway list (and saved searches), specialty on the referral inbox and Insights. `ToQueryString` tests for the stage and days-waited expressions. No migration.
9. **Demo data.** Referrals, triage outcomes, clocks, pauses and breaches, the stage changes, saved searches, the seed's tests and `docs/demo-data.md` (section 7).

## 10. Questions for the owner

**All decided on 18 November 2026** (the plan follows these):
1. **Who may triage:** holders of the `TriageReferrals` permission (the starting Clinician role) who are members of the referral's specialty. Roles and permissions are managed by admins on their own page and built in this phase (decisions 51 to 68). *(The owner chose this over the recommended "can triage" tick.)*
2. **Targets** per specialty and priority, defaulting to 14, 28, 126 and 126 days for Urgent, High, Normal and Low. No separate "suspected cancer" priority.
3. **The clock stops** when the template's marked stage is finished, otherwise at discharge or cancellation.
4. **Pauses** are only for patient-initiated reasons, only after acceptance, with a review date at most 90 days ahead. On hold doesn't pause.
5. **Pathways without a referral** are still allowed, with a target set by hand and no clock. No referrals are backfilled.
6. **Notifications:** none for a new referral (the inbox count instead); whoever registered it is told of every outcome.
7. **"Ask the referrer for more information"** comes later, with Phase 3's letters. Until then, reject with Insufficient information.
8. **Restricted patients:** new referrals for them are refused, and the message tells staff to pass the letter to an admin.

9. **Permissions stay per action,** not per stage, in Phase 2. Phase 3 adds a lab permission with specimens, and can limit stages to it then (decision 66).
10. **Admin keeps its everyday clinical and booking permissions, and "All staff (before roles)" stays,** until every account has been moved onto the starting roles. Then a small change of its own, before a real team uses MedApp, leaves Admin with administration only and archives the old role. Not on this phase's deploy, so nobody loses access.
11. **A `grant-admin <username>` command** beside `create-first-admin`, for recovery when every admin is locked out, run by whoever deploys and logged as a security event. It needs the same access as the first admin's secret, so it adds no new way in (PR 2).

**Later questions** take the option recommended for them, on the owner's instruction (18 November 2026). Whoever decides one records it here, as the next number, with the date and "recommended option taken".

12. **Withdrawing and marking entered in error while restricted** (18 November 2026, recommended option taken; PR 5): both are allowed while the patient's record is restricted, and refused once it's anonymised. Decision 44 named only withdrawing; marking entered in error is allowed too, as an appointment's entered in error is, because correcting a wrong entry is rectification, not a new change to the record.
13. **Correcting a referral's specialty** (18 November 2026, recommended option taken; PR 5): while a referral awaits triage, its specialty can be corrected by editing it, recorded as `DetailsEdited` with the specialty's old and new name. `SpecialtyChanged` stays for triage's redirect to another specialty (decision 21).
14. **When a letter can be attached** (18 November 2026, recommended option taken; PR 5): a referral's letter can be attached in any status except Entered in error, because a letter can arrive after the referral is triaged.
15. **The side nav's count** (18 November 2026, recommended option taken; PR 5): it has its own endpoint, `GET api/referrals/awaiting-triage/count`, which records nothing (a count names nobody), so the side nav doesn't log a patient listing on every page.
16. **Time since received, before the clock** (18 November 2026, recommended option taken; PR 5): the referral page shows "N days since received" and the list "received N days ago", as plain date arithmetic, until the clock's PR (PR 7) replaces them with the wait.
17. **`ReferralViewed` deduplication** (18 November 2026, recommended option taken; PR 5): deduplicated per referral, not per patient, over the view window, so opening two of a patient's referrals records both.
18. **The stage reached counts pathways on hold,** but never discharged, cancelled or archived ones, even when the list includes closed pathways or asks for those statuses (decision 42's "open pathways only", read as "not closed": a pathway on hold at Histology is still waiting there). 18 November 2026, recommended option taken.
19. **Specialty and stage reached filters are "is" or "is any of", never "is not any of":** the API takes the IDs wanted, and the list's URL is read before the specialties and stages are loaded, so "every one but" can't be turned into IDs. 18 November 2026, recommended option taken.
20. **My Worklist has the specialty and stage reached filters too** (the same search behind it), and Insights' Template choice lists only the chosen specialty's templates, dropping a chosen template that isn't the new specialty's. 18 November 2026, recommended option taken.
21. **The referral inbox's Specialty filter** (any of, `specialty` in the URL) is built with the Filters PR; starting the inbox on the viewer's own specialties (decision 39, the search's `mine`, which the API has) is left to a small follow-up, so the inbox shows every specialty until then. 18 November 2026, recommended option taken.

## 11. Left out, for later

- Receiving referrals electronically (the NHS e-Referral Service): Phase 5, after NHS onboarding.
- Letters to the referrer about the outcome: Phase 3.
- Monitoring rules and notifications for waiting referrals, breaches and pause reviews: Phase 4 (needs a scheduled job).
- Narrowing what each role sees (by specialty, hiding restricted patients) and an auditor's read-only permission: later, on top of this phase's permissions.
- Specialties on clinics, calendars and rooms: Phase 4.
- Waiting-time reports by specialty and month, with CSV: Phase 5.
- A FHIR `ServiceRequest` for referrals: when the API needs more resources.
