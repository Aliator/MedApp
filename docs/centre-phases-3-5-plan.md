# MedApp: lab, results and letters; day-to-day running; readiness

Status: **planned** · 2026-11-18 · Phases 3, 4 and 5 of the community specialist centre (`docs/todo.md`, "Community specialist centre") · every question decided as recommended (the owner delegated them); only work outside the team is left open (section 7)

**In one paragraph:** Phase 3 closes the loop the centre is built around: a **specimen** taken at a stage is tracked to the lab and back, its **report** becomes a **result** that a named clinician must sign off, and **letters** (clinic letters, discharge summaries, replies to referrers) are written from templates, approved, rendered as PDFs and sent, with a record of each sending. Phase 4 makes MedApp fit a real team's week: **pathways that branch**, **sites, rooms and working hours** (evenings and weekends), **clinic sessions** with slots, a **scheduled job runner**, **patient reminders** by text and email with a rule for missed appointments, **service alerts** in monitoring, a separate permission for **finishing a stage without its visit**, and **retention classes**. Phase 5 makes it ready for a customer: the **MDT list**, **consent records**, **reports for commissioners** with CSV, **outcome questionnaires**, **private patients**, the **data-request pack**, **video and phone appointments**, a **real malware scanner**, **adapters for NHS systems** with offline stand-ins, **multi-tenancy**, and the **go-live list**. Throughout, **the clinician decides and the app records**: nothing is interpreted, scored, flagged by clinical content or suggested.

This builds on the Phase 2 plan (`docs/centre-phase2-plan.md`): its names (specialties, referrals, `ReferralChange`, the waiting-time clock, roles and permissions), its decisions 1 to 68 and its PRs 1 to 10. Numbering continues from it: **decisions from 69, PRs from 11, hazards from H-45, DPIA risks from 29**. Stored numbers are the next free ones after Phase 2's (section 5); if another branch takes one first, use the next free one and correct this document.

## 1. Decisions for all three phases

69. **The clinician decides; the app records.** Every new feature keeps to the device line (`docs/clinical-safety/intended-purpose-and-device-decision.md`). MedApp never:
    - reads a value out of a report, file or answer, or shows anything as normal or abnormal;
    - totals, bands or compares scores, or colours anything by its clinical content;
    - suggests an outcome, a branch, an action, a template or a priority, or pre-selects one;
    - orders a list by anything but dates, times, labels staff chose, or names.

    Each PR that adds a feature states its device position (the tables in sections 2.6, 3.8 and 4.11), and each phase's first PR updates the device decision and the hazard log before any feature (section 6).
70. **One audit log.** The new clinical items (specimens, results, letters, stage skips and branches, MDT items, consent, questionnaires) write their history as `PatientPathwayAuditEntry` rows with new actions (section 5), not into new event tables. Details hold IDs, enums by name, admin-written names and dates, **never free text**; the text lives only on the item's row. An item about the patient rather than a pathway uses `PatientPathwayAuditEntry.NoPathway`, as attachments do.
    - *Alternative:* an events table per item, as Phase 2's `ReferralEvent`. No: the timeline would merge ten feeds, and the roadmap wants one audit trail.
    - They don't go through `PathwayChange` (a specimen isn't a change to the pathway), so `LastUpdated` doesn't move, as attachments; except stage skips and branch choices (decision 113), which are pathway changes.
71. **Items point at files; files keep their targets.** A specimen's report, a letter's PDF and a consent form's scan are ordinary attachments on the pathway (or the patient), and the item holds the attachment's ID. Attachments gain no new target columns.
    - A file an item points at can't be deleted on its own (409, "This file is a specimen's report"). The item's own correction (entered in error, withdrawn) soft-deletes it with an `AttachmentDeleted` entry.
72. **Notifications get one `SubjectId`** (`Guid?`), whose meaning comes from the kind (`NotificationLinks` maps each kind to its page), instead of a column per item. The existing columns (`PathwayId`, `AppointmentId`, `MeetingId`, `NoteDeletionRequestId`, Phase 2's `ReferralId`) stay.
73. **Stage gates live in one place.** `StageGates.WhyCantFinish(stage, pathway, items)` in `MedApp.Application/Pathways/Common`, called by `PathwayWorkflow` and every finish path. Each gate is a flag an admin sets on a template's stage (`PathwayTemplateMap`), copied onto the pathway's stage like `VisitRequirement`:
    - `WaitsForResults` (Phase 3), `NeedsLetterKind` (Phase 3), `NeedsConsent` (Phase 5), beside the existing visit rule;
    - plus two that always apply to the **last** stage: no open specimen, no result awaiting sign-off (decisions 89 and 95).

    A refusal is a 409 naming what's missing. Only the visit gate has an override (`FinishWithoutVisit`, decision 138); the others are met by the action they ask for, or by correcting the item (lost, entered in error).
74. **Permissions:** the new ones (table below) follow Phase 2's rules (decisions 51 to 68): checked by policy, stored by name.
    - **Admin's fixed set** gains only setup, oversight and correction ones (`UnlockStages`, `ManageClinics`, `ViewReports`). It doesn't get the clinical ones (sign-off, approving letters, reporting specimens, finishing without a visit), as it doesn't get `TriageReferrals`.
    - **"All staff (before roles)"** gets none: it's the bridge to the starting roles, to be archived (decision 180).
    - **No permissions by stage.** Phase 2's open question 9 is answered no: the lab's work is the specimen's statuses (`ProcessSpecimens`), and who finished which stage is audited. *Alternative:* a role list on each stage definition. No: every finish path, template editor and seed would check it, for little gain.
75. **Restricted records: work that protects the patient can finish; new work can't.** For a restricted patient, a specimen already taken can still be received, rejected, reported or marked lost, and a result can be seen and signed off (a result must never be stuck). Everything else new (a specimen, a letter, sending a letter, an MDT listing, a consent record, a questionnaire, a session booking, an invoice) is the usual 409 (`PatientRules.EnsureCanChange`), and its message says to ask an admin. Anonymised: nothing changes.
76. **Every new table of patient data** gets the privacy checklist (`docs/agents/privacy.md`) in the PR that adds it: reads logged (`RecordViewsAsync`, `EndpointCoverageTests`), changes audited, added to `PatientRecordRepository` (export and anonymisation), free text replaced on anonymisation, the timeline's staff canary extended, counted for retention, and `docs/governance/` updated.
77. **Systems outside MedApp sit behind interfaces** with a `Mode` setting: `Off`, `Offline` (a stand-in with synthetic behaviour, used in development, on kind, in CI and in the demo) or `Live`. A Live adapter is built only when the system's test environment can be reached, which for NHS systems needs onboarding (section 7). `GET api/integrations/status` (`ManageSetup`) and Administration, Integrations show each one's mode and last contact.

**New permissions, and who has them at the start** (columns as Phase 2's decision 68: A Admin, C Clinician, N Nurse, B Booking, L Lab, M Manager; **SC** is Phase 4's Senior clinician, decision 138):

| Permission | Phase | Group | What it allows | A | C | N | B | L | M | SC |
|---|---|---|---|---|---|---|---|---|---|---|
| `RecordSpecimens` | 3 | Lab | Record a specimen taken, sent, lost; edit before it's sent | | ✓ | ✓ | | ✓ | | ✓ |
| `ProcessSpecimens` | 3 | Lab | Receive, reject, report (attach the report) | | | | | ✓ | | |
| `SignOffResults` | 3 | Results | Mark seen, sign off, take over or reassign a result; be a responsible clinician | | ✓ | | | | | ✓ |
| `WriteLetters` | 3 | Letters | Write and submit letters | | ✓ | ✓ | | | | ✓ |
| `ApproveLetters` | 3 | Letters | Approve or return letters | | ✓ | | | | | ✓ |
| `SendLetters` | 3 | Letters | Send, or record sending, an approved letter | | ✓ | ✓ | ✓ | | | ✓ |
| `UnlockStages` | 4 | Pathways | Unlock a stage; reinstate a skipped stage (half of Phase 2's `OverrideStages`) | ✓ | | | | | | ✓ |
| `FinishWithoutVisit` | 4 | Pathways | Finish a Required stage without its visit (the other half) | | | | | | | ✓ |
| `ManageClinics` | 4 | Appointments and calendars | Clinic patterns and sessions, staff working hours | ✓ | | | ✓ | | ✓ | |
| `RecordMdtOutcomes` | 5 | Pathways | Record an MDT outcome | | ✓ | | | | | ✓ |
| `RecordConsent` | 5 | Pathways | Record, withdraw consent | | ✓ | ✓ | | | | ✓ |
| `RecordQuestionnaires` | 5 | Pathways | Record questionnaire answers | | ✓ | ✓ | ✓ | | | ✓ |
| `ViewReports` | 5 | Oversight | Commissioner reports and CSV | ✓ | | | | | ✓ | |
| `ManageBilling` | 5 | Billing | Funding, fee schedule, insurers, invoices, payments | | | | ✓ | | ✓ | |

Setup lists (specimen types, labs, letter templates, sites, rooms, site hours, consent forms, questionnaire definitions, integrations, scheduled jobs) are `ManageSetup`. Listing for an MDT is `WorkPathways`; data requests are `ManagePrivacy`.

## 2. Phase 3: clinic to lab to result to letter

### 2.1 Patient contact details and practices

78. **Patients gain contact details,** all optional: a postal address (`AddressLines`, up to three lines, and `Postcode`, UK format checked), a UK mobile (`MobilePhone`, stored as `+447…`), a registered GP practice (`PracticeId`), and how they want reminders (`ReminderChannel`, Phase 4; `None` until asked). Edits log the field names (`PatientUpdated`), as today.
    - *Alternative:* wait for the real PDS lookup to supply them. No: letters need them now. When PDS is live, it fills them for staff to confirm, as it fills the name today.
79. **A GP practice directory** (`Practice`): name, optional ODS code (unique when present, Phase 2's `OdsCodes` format check), address, postcode, phone, and an optional secure email whose domain must be in `Letters:SecureEmailDomains` (default `nhs.net`). Holders of `EditPatients` keep it; one in use is archived, not deleted. Registering a referral picks a practice (`Referral.PracticeId`), and Phase 2's typed practice text stays as typed.
    - *Alternative:* look practices up in the national ODS service. Later (section 8): the directory is the record either way.
80. **Staff details for letters:** `ApplicationUser.FullName` and `JobTitle` (100 characters each), set by the person in Account Settings or by `ManageUsers`. Changes are logged (`StaffDetailsChanged = 48`). A letter can't be approved by someone with no full name.

### 2.2 Specimens

81. **A specimen** (`Specimen`) is something taken from a patient at a stage and sent to a lab. It holds:
    - the patient, pathway and stage that took it, and its **specimen number**: MedApp's own ID from a SQL sequence, as `{prefix}{yy}-{nnnnnn}` (`Specimens:NumberPrefix`, default `MA`: `MA26-000123`), unique;
    - its type and lab (decision 82), a site (free text, 200 characters, optional: "left forearm"), request notes for the lab (free text, 1,000, optional), and the **requesting clinician** (holder of `SignOffResults`, required: they get the result);
    - when it was taken, sent, received and reported, and by whom; the lab's own reference (`LabReference`, 50); the courier or batch reference (50);
    - its status, the report's attachment ID, `Version` and a row version.
82. **Specimen types and labs are admin lists** (`SpecimenType`: name and expected turnaround in days, 1 to 90; `Laboratory`: name, and whether it uses MedApp itself). Archived, not deleted, once used.
83. **Statuses** (`SpecimenStatus`, section 5) move forward only: Taken, Sent, Received, Reported; or, from any open one, RejectedByLab (with a `SpecimenRejectionReason` and a detail), Lost (with a detail), or EnteredInError. Taken, sent and received times are now or typed up to 7 days back, never in the future, and never before the step before; reported is now.
    - A rejected or lost specimen is closed. Taking another is a new specimen; the old one's page links to it.
84. **Who:** `RecordSpecimens` takes, sends, edits details before sending, and marks lost; `ProcessSpecimens` receives, rejects, reports and marks lost. Entered in error: whoever recorded it, within 24 hours, or `CorrectRecords` at any time.
85. **Taking needs the stage to be current** and the pathway open (not on hold, discharged, cancelled or archived). Lab steps work in any pathway status, so a report is never stuck.
86. **The report is a file** uploaded when reporting, under every attachment rule (sniffed types, size, audit), attached to the pathway, or to the patient if the pathway has closed meanwhile (decision 71). Reporting creates the result (decision 91) in the same save.
87. **The label and request sheet** are a printable page (HTML with a print stylesheet, no PDF): the specimen number as text and as a QR code (drawn on the server by the existing `QrCodeRenderer`), the patient's name, date of birth and NHS number, the type and when taken; the request sheet adds the site, the request notes, the requesting clinician and the lab. **The QR code holds the specimen number only**, never patient details. Printing records `SpecimenViewed`.
88. **Turnaround** is shown in days and hours (taken to sent, sent to received, received to reported, taken to reported). **Overdue at lab** is a date comparison: not reported by taken + the type's expected days. Neither changes anything.
89. **The last stage of a pathway can't be finished while a specimen is open** (Taken, Sent or Received): 409 listing them (decision 73). So a pathway can't be discharged with a report still to come.
90. **The lab's worklist** (`/lab`): views Expected (sent, not received), In the lab (received, not reported), Overdue, Rejected and lost (last 30 days), Reported (last 7 days); oldest first. Finding a specimen by its number (typed, or scanned by a keyboard scanner) is `POST api/specimens/find {number}`, keeping the number out of URLs.

### 2.3 Results sign-off

91. **A result** (`Result`) is a report a named clinician must sign off. It's made automatically when a specimen is reported, or by a holder of `WorkPathways` marking an attached file as an external result (a scan report from elsewhere: `RecordExternalResult`, with a title of up to 200 characters, the file, the pathway if any, and the responsible clinician). It holds the patient, pathway, specimen (if any), the report's attachment ID, when reported, the **responsible clinician**, its status, who saw it and when, who signed it off and when, the action recorded and its detail, and a row version.
92. **The responsible clinician** is the specimen's requesting clinician. Any holder of `SignOffResults` can take a result over or reassign it to another holder; the new one is told (`ResultReassigned`). A responsible clinician who no longer holds the permission (or whose account has gone) leaves the result flagged **Needs a clinician** on the list until someone takes it.
93. **Seen, then signed off.** *Mark as seen* is optional (the clinician has read it, and action is under way). *Sign off* records what was done, chosen by the clinician from `ResultAction` (No further action, Patient informed, Appointment arranged, To be discussed at MDT, Referred on, Other), with a detail (1,000 characters, required for Other). Any holder of `SignOffResults` may sign off; if it isn't the responsible clinician, they're told (`ResultSignedOffByOther`). A signed-off result is final: anything later goes in a pathway note.
    - The actions are a record of what the clinician did, listed in a fixed order, none pre-selected. MedApp doesn't check that the action fits the result.
94. **The results list** (`/results`, in the side nav with the count of mine awaiting): views **Awaiting my sign-off**, **Awaiting sign-off (everyone)**, **Needs a clinician**, **Signed off**; oldest report first, with its age in days; filter by specialty. The count is the signal: there's no notification per day waiting, only `ResultReady` once (section 2.6) and Phase 4's service alert.
95. **Gates:** a stage flagged `WaitsForResults` can't be finished while any result of the pathway awaits sign-off, and the last stage never can (decision 73).
96. **Entered in error** (a report on the wrong patient or specimen): by `CorrectRecords`, or the lab user who reported it within 24 hours, with a reason. The result closes, its file is soft-deleted (decision 71), and the specimen goes back to Received to be reported again. Hazard H-47.
97. **No interpretation:** MedApp shows the report file as it came (its thumbnail, or a download). The lists show dates, ages, names and statuses only.

### 2.4 Letters

98. **PDFs are made on the server with PDFsharp 6 and MigraDoc** (MIT licence), from a Markdown body parsed with Markdig (BSD-2-Clause), in a bundled font under the SIL Open Font License (Atkinson Hyperlegible, embedded with its licence in `NOTICE`), since the image has no system fonts.
    - **Tagged for accessibility** where the library's accessibility support allows (headings, paragraphs, reading order), and each letter can be made in **large print** (16 point), for the Accessible Information Standard.
    - **PDF/A:** the PR checks the output with veraPDF. If it passes as PDF/A-2b, letters are PDF/A; if not, PDF 1.7, and PDF/A becomes a to-do (GP systems accept plain PDF).
    - *Alternative:* QuestPDF. No: its licence turns commercial above a revenue threshold, as ImageSharp's did. *Alternative:* HTML printed by headless Chromium. No: a browser in the API's image, a larger attack surface, and the locked-down chart's limits.
99. **Letter templates** (`LetterTemplate`, `ManageSetup`): a name, a kind (`LetterKind`: clinic letter, discharge summary, referral response, information request, appointment letter, other), a specialty or all, a letterhead (plain lines: the centre's name, address, phone), a body skeleton (Markdown: the headings the author fills), and a closing. A letter copies its template's text when it's started, so editing a template changes only new letters. Archived, not deleted, once used.
    - **Merge fields** are a fixed list, filled from the record at render time: the patient's name, date of birth, NHS number and address; the practice's name and address; the referral's received date; the date of the last attended appointment on the pathway; the author's and approver's full names and job titles; today's date; the site's name. An unknown field is a 400 when the template is saved. A field with no value stops approval, naming it.
    - **They copy identity and administrative data only.** No merge field reads a clinical value: the clinical content is what the author writes.
100. **Writing:** a holder of `WriteLetters` starts a letter from a pathway or a referral, choosing a template of its specialty. The body is Markdown, up to 20,000 characters, saved explicitly with `expectedVersion`. A draft is its author's alone; nobody else edits it. Drafts aren't audited edit by edit: the approved PDF is the record.
101. **Recipients** (`LetterRecipient`, `LetterRecipientKind`): the GP practice, the patient, the referrer, or another (a name and address typed). A new letter starts with the patient's registered practice and the patient, and the author removes or adds. Each recipient's name and address are copied onto the letter when it's approved, so a later move doesn't change what was sent.
102. **Approval:** the author submits it to a named holder of `ApproveLetters` (told: `LetterAwaitingApproval`), or approves it themselves if they hold it. **Approve** renders the PDF, with the patient's name, date of birth and NHS number and "Page n of m" on every page (hazard H-50), stores it as an attachment, and freezes the letter. **Return** sends it back to the author with a comment (500 characters; told: `LetterReturned`). An author whose letter someone else approved is told (`LetterApproved`).
103. **Sending, per recipient:**
    - **Secure email from MedApp** (`ILetterTransport`, `SecureEmail` mode): only when `Letters:SecureEmail` is configured and the deploying organisation has declared its relay meets the secure email standard (DCB1596; `Letters:SecureEmail:RelayMeetsDcb1596=true`), and only to an address in an allowed domain. It's sent **synchronously**, through its own MailKit transport, not the best-effort `IEmailSender` queue: the recipient is marked Sent only when the SMTP server accepts it, and a failure is shown at once. The email's subject and body hold no patient or clinical text ("A letter from {centre}. It's attached."); the PDF is the attachment.
    - **Recorded as sent outside MedApp:** posted, handed to the patient, emailed from the organisation's own secure mailbox, or other, with the date (today or up to 7 days back).

    Each sending is an audit entry (`LetterSent`, with the recipient kind and method). The **To send** view lists approved letters with any recipient not yet sent.
104. **Withdrawing:** an approved letter found wrong is withdrawn by its approver or `CorrectRecords`, with a reason (500 characters). The page lists the recipients it was already sent to, says to tell them, and offers **Write a correction** (a new draft with the old text, `ReplacesLetterId`). The PDF stays on file, marked withdrawn.
105. **Gate:** a stage flagged `NeedsLetterKind` (for example "Discharge letter to GP" needing a discharge summary) can't be finished until a letter of that kind on the pathway is approved and sent to every recipient.
106. **Referral replies and "Ask for more information"** (Phase 2's open question 7): letters of kind referral response or information request are written from the referral's page. Triage gains **Ask for more information**: the referral becomes `InformationRequested` (still open, in the inbox's own view, its clock still running), with event `InformationRequested`; **Information received** returns it to `AwaitingTriage` (event `InformationReceived`). Both need `TriageReferrals` and the specialty.
107. **How letters reach people without NHS systems:** by post or by hand (from the PDF), or by secure email from MedApp or the organisation's own mailbox. **Not to patients by email:** patients' addresses aren't verified and the PDF would travel unencrypted; patients get post or a copy in clinic. Sending to GP systems directly (GP Connect Send Document) is Phase 5's adapter.
108. **The letters list** (`/letters`, in the side nav with the count of mine to approve plus all to send): views **My drafts**, **Awaiting my approval**, **Returned to me**, **To send**, **Sent**. The pathway and referral pages have a **Letters** card.

### 2.5 Data model, API and pages

**New tables:**
- **`Practices`:** `Id`, `Name`, `OdsCode` (unique where not null), `AddressLines`, `Postcode`, `Phone`, `SecureEmail`, `IsArchived`, `CreatedAt`, `LastUpdated`.
- **`SpecimenTypes`** (`Name` unique, `ExpectedTurnaroundDays`, `IsArchived`) and **`Laboratories`** (`Name` unique, `UsesMedApp`, `IsArchived`).
- **`Specimens`:** `Id`, `Number` (varchar 16, unique; from the sequence `SpecimenNumbers`), `PatientId` (FK, restrict), `PathwayId` (FK, restrict), `StageId`, `SpecimenTypeId`, `LaboratoryId`, `Site` (nvarchar 200), `RequestNotes` (nvarchar 1000), `RequestingUserId`, `Status`, `TakenAtUtc`/`TakenByUserId`, `SentAtUtc`/`SentByUserId`, `CourierReference`, `ReceivedAtUtc`/`ReceivedByUserId`, `LabReference`, `ReportedAtUtc`/`ReportedByUserId`, `ReportAttachmentId`, `RejectionReason`, `ClosedDetail` (nvarchar 500: rejection, loss or error detail), `ClosedAtUtc`/`ClosedByUserId`, `Version`, `RowVersion`. Indexes: (`Status`, `LaboratoryId`, `SentAtUtc`), `PathwayId`, `PatientId`.
- **`Results`:** `Id`, `PatientId` (FK, restrict), `PathwayId` (null), `SpecimenId` (null, unique where not null and not entered in error), `Source`, `Title` (nvarchar 200), `ReportAttachmentId`, `ReportedAtUtc`, `ResponsibleUserId`, `Status`, `SeenAtUtc`/`SeenByUserId`, `SignedOffAtUtc`/`SignedOffByUserId`, `Action`, `ActionDetail` (nvarchar 1000), `ErrorDetail` (nvarchar 500), `Version`, `RowVersion`. Indexes: (`Status`, `ResponsibleUserId`, `ReportedAtUtc`), `PathwayId`.
- **`LetterTemplates`:** `Id`, `Name`, `Kind`, `SpecialtyId` (null), `Letterhead`, `BodySkeleton`, `Closing`, `IsArchived`, `CreatedAt`, `LastUpdated`.
- **`Letters`:** `Id`, `PatientId` (FK, restrict), `PathwayId` (null), `ReferralId` (null), `TemplateId`, `Kind`, `Letterhead`, `Body` (nvarchar max, 20,000 enforced), `Closing`, `LargePrint`, `Status`, `AuthorUserId`, `ApproverUserId`, `SubmittedAtUtc`, `ReturnComment` (nvarchar 500), `ApprovedAtUtc`, `PdfAttachmentId`, `WithdrawnAtUtc`/`WithdrawnByUserId`, `WithdrawnReason` (nvarchar 500), `ReplacesLetterId`, `Version`, `RowVersion`, `CreatedAt`, `LastUpdated`.
- **`LetterRecipients`:** `Id`, `LetterId` (FK, cascade), `Kind`, `PracticeId` (null), `Name`, `AddressLines`, `Postcode`, `Email` (null), `DispatchStatus`, `Method`, `SentOn` (date), `SentAtUtc`/`SentByUserId`, `TransportReference` (varchar 100).

**New columns:** `Patients`: `AddressLines`, `Postcode`, `MobilePhone`, `PracticeId` (FK, restrict), `ReminderChannel`. `AspNetUsers`: `FullName`, `JobTitle`. `Referrals`: `PracticeId`. `PathwayTemplateMaps` and `PatientPathwayStages`: `WaitsForResults` (bit), `NeedsLetterKind` (int, null). `Notifications`: `SubjectId`.

**Where the rules live:** `MedApp.Application/Specimens/Common` (`SpecimenWorkflow`, `SpecimenNumbers`, `SpecimenMapper`, `SpecimenAudit`), `Results/Common` (`ResultWorkflow`, `ResultMapper`, `ResultNotifications`), `Letters/Common` (`LetterWorkflow`, `LetterMergeFields`, `LetterMapper`, `LetterNotifications`), `ILetterRenderer` (Infrastructure: `PdfSharpLetterRenderer`), `ILetterTransport` (`SecureEmailLetterTransport`, `NoLetterTransport`), `Pathways/Common/StageGates`.

| Endpoint | Permission | What |
|---|---|---|
| `GET/POST/PUT api/practices`, `…/{id}/archive` | `ViewPatients` / `EditPatients` | The directory |
| `GET/POST/PUT api/specimen-types`, `api/laboratories` | `UseApp` / `ManageSetup` | Setup lists |
| `POST api/pathways/{id}/specimens` | `RecordSpecimens` | Record taken: `{stageId, typeId, labId, takenAtUtc, site?, requestNotes?, requestingUserId}`. 201 |
| `GET api/specimens/{id}`, `GET api/specimens/search`, `POST api/specimens/find` | `ViewPatients` | One (with its label data), the lab worklist views, find by number |
| `POST api/specimens/{id}/sent`, `/received`, `/rejected`, `/lost`, `/entered-in-error`; `PUT api/specimens/{id}` | as decision 84 | Each with `expectedVersion` |
| `POST api/specimens/{id}/report` | `ProcessSpecimens` | The report file as the body (attachment rules), creates the result |
| `GET api/results/search`, `GET api/results/{id}` | `ViewPatients` | Views of decision 94; one result |
| `POST api/results/external` | `WorkPathways` | `{attachmentId, title, pathwayId?, responsibleUserId}` |
| `POST api/results/{id}/seen`, `/sign-off`, `/reassign`, `/entered-in-error` | `SignOffResults` (error: decision 96) | `{action, detail?, expectedVersion}` for sign-off |
| `GET/POST/PUT api/letter-templates` | `UseApp` / `ManageSetup` | Templates, with a preview PDF of synthetic values |
| `POST api/letters`, `PUT api/letters/{id}`, `GET api/letters/{id}`, `GET api/letters/search` | `WriteLetters` / `ViewPatients` | Draft, edit, read, the views |
| `GET api/letters/{id}/preview` | author or approver | A PDF of the draft, marked "Draft, not approved" on every page |
| `POST api/letters/{id}/submit`, `/approve`, `/return`, `/withdraw` | `WriteLetters` / `ApproveLetters` | |
| `POST api/letters/{id}/recipients/{rid}/send`, `/record-sent` | `SendLetters` | Secure email, or `{method, sentOn}` |
| `POST api/referrals/{id}/ask-for-information`, `/information-received` | `TriageReferrals` | Decision 106 |
| `PUT api/auth/me/details`, `PUT api/auth/users/{username}/details` | `UseApp` / `ManageUsers` | Full name and job title |

**Pages:** the patient form's contact section and practice picker; **Practices** (`/practices`); the pathway page's **Specimens** card on each stage (Record specimen, Print label, the steps) and **Results** and **Letters** cards; **Lab** (`/lab`) and a specimen's page (`/specimens/{id}`) with its print views; **Results** (`/results`, `/results/{id}`); **Letters** (`/letters`, `/letters/{id}`: the editor with the template's headings, a merge-field preview, recipients, then approval and sending); Administration: **Specimen Types and Labs**, **Letter Templates**; the template editor's stage options (waits for results, needs a letter); Account Settings' full name and job title. Side nav: **Results** and **Letters** after Referrals, **Lab** for holders of `ProcessSpecimens`. Each new screen goes in `tools/ui-parity/screens.ts` with its axe audit.

### 2.6 Privacy, safety and the device position

**Privacy** (decision 76 for each table):
- **Reads:** a specimen's page and its print views record `SpecimenViewed = 45`; a result's page `ResultViewed = 46`; a letter's page or preview `LetterViewed = 47`; the lab worklist, results list and letters list record `PatientListed` for each patient shown; downloads are `AttachmentDownloaded` as today.
- **Export:** specimens, results, letters with their recipients and sending, the patient's contact details and practice. Staff by username only.
- **Anonymisation** is refused while a specimen is open, a result awaits sign-off, or a letter is a draft, awaiting approval or not yet sent to everyone. It replaces the patient's address, postcode and mobile (and clears the practice link), specimens' site, request notes and closing detail, results' title, action detail and error detail, letters' body, return comment and withdrawal reason, and recipients' names and addresses where the recipient is the patient or "other"; PDFs and reports are deleted with every file. Specimen numbers, statuses, dates, enums and admin-written names stay.
- **Timeline** (`TimelineMapper`): new kinds **Specimens**, **Results** and **Letters**. Staff see the action, who, when, the specimen type, lab, status, result action by name, letter kind, recipient kind and method. **Never** the site, request notes, result title or action detail, letter text, return comment or any reason. Admins see those from the rows. `TimelineStaffCanaryTests` gains every new action.
- **Notifications** (fixed titles, usernames and admin-written names only, never the patient): "Specimen rejected" ("The lab rejected a Skin biopsy (punch) specimen."), "Specimen lost", "Result ready" ("{user} reported a Skin biopsy (punch) specimen."), "Result reassigned to you", "Result signed off" ("{user} signed off a result you're responsible for."), "Letter to approve", "Letter approved", "Letter returned". All in `Notification.HasFixedTitle`.
- **Governance** (`docs/governance/`): the data inventory, DPIA and ROPA gain specimens and lab reports (a new recipient: an external lab; **DPIA risk 29**), letters and their recipients (**risk 30**: a letter to the wrong recipient, or by email), and patient contact details (**risk 31**); the patient privacy notice names labs and GP practices as recipients; the retention schedule keeps all of them with the record; staff full names and job titles go in the staff notice.

**Safety** (`docs/clinical-safety/`, PR 11): new draft hazards for the CSO.
- **H-45 A specimen is lost, or its report never comes back.** Controls: the lab worklist's Expected and Overdue views; turnaround shown; the last stage can't finish with a specimen open; Lost recorded and told; Phase 4's service alert. DCB0160: a daily worklist check.
- **H-46 A result is never seen or acted on.** Controls: every result has a named clinician; the results list and its count; Needs a clinician; the last stage and `WaitsForResults` gates; Phase 4's alert; sign-off records who and what.
- **H-47 A report is attached to the wrong patient or specimen.** Controls: reporting from the specimen's page found by its number, with the banner; the label's identifiers; entered in error sends it back.
- **H-48 A letter goes to the wrong practice or person.** Controls: recipients shown with addresses before approval and sending; the practice from the directory, not retyped; addresses copied at approval; withdraw and correct.
- **H-49 A letter isn't sent, or goes late.** Controls: the To send view and count; `NeedsLetterKind` gates; secure email marked Sent only when accepted; a restricted record's letters stop with a message to ask an admin.
- **H-50 A letter shows another patient's details, or an unapproved version is sent.** Controls: merge fields from the letter's own patient only; identifiers on every page; previews marked Draft; only approved PDFs can be sent.
- **H-01** gains the specimen label as a cause and a control.

**Device position** (the reassessment in `intended-purpose-and-device-decision.md`; the MHRA flow chart's step 4 changes: MedApp now handles in vitro diagnostic reports, **as documents**):

| Feature | Position | Why |
|---|---|---|
| Specimen tracking | Not a device | Logistics: statuses and times staff record. Turnaround and Overdue at lab are date arithmetic on those times and the organisation's expected days. |
| Lab reports | Not a device (not IVD software) | Stored and shown exactly as the lab sent them; no value is read, parsed, compared or flagged. Like a laboratory information system's filing, not its analysis. |
| Results sign-off and its list | Not a device | Records who saw a report and what they did. Ordered by date; no content-based flag. The actions are the clinician's record, not suggestions. |
| Letters and templates | Not a device | Documents written by clinicians. Merge fields copy identity and administrative data; nothing clinical is generated. |
| Stage gates | Not a device | Enforce the organisation's own template settings, like the visit rule. |

**Claims the manufacturer must not make** gains: "MedApp tells you which results are abnormal" and "MedApp writes clinical letters".

### 2.7 Demo data (PR 17)

Through the shared code the handlers call (`SpecimenWorkflow`, `ResultWorkflow`, `LetterWorkflow`, `ReferralChange`), never rows written around the rules:
- **Practices:** invented practice names and addresses, no ODS codes; each patient registered with one.
- **Contact details:** synthetic addresses on invented streets with postcodes in the `ZZ` area (none exists); mobiles from Ofcom's drama range (07700 900000 to 900999); reminder preferences spread.
- **Specimens:** types (Skin biopsy (punch), Skin excision, Endometrial biopsy, Urine, Blood) and labs (an in-house lab using MedApp, and an invented partner lab). Every Skin lesion and Post-menopausal bleeding pathway's biopsy stage takes one; turnaround spread with a long tail; a few overdue now; one rejected (unlabelled) and retaken; one lost. Reports are synthetic PDFs marked "SYNTHETIC: not a real result" in words, no values.
- **Results:** signed off with every action; about 15 awaiting sign-off now, three older than two weeks; one Needs a clinician (the deleted leaver's); one entered in error.
- **Letters:** templates per specialty (clinic letter, discharge summary, referral response, information request); letters approved and sent for discharged pathways (most recorded as posted, some by secure email to the offline transport); a few drafts, awaiting approval, returned and to send; one withdrawn and corrected.
- **Staff:** full names and job titles (invented). The template stages "Histology (lab)" wait for results; "Discharge letter to GP" needs a discharge summary.
- **Tests:** `DemoDataSeedTests` checks specimen times in order and before now, every reported specimen has one result, no discharged pathway with an open specimen or unsigned result, gates respected. `docs/demo-data.md` tours a biopsy from label to signed-off result to discharge letter.

## 3. Phase 4: day-to-day running for a real team

Roles beyond Admin and User, in the owner's plan for this phase, were built in Phase 2 (decisions 51 to 68).

### 3.1 Scheduled jobs

109. **MedApp runs scheduled jobs inside the API, with a lease in SQL Server.** A `BackgroundService` (`ScheduledJobRunner`) wakes every 30 seconds; for each due job it tries to take the job's lease in one statement (`UPDATE ScheduledJobs SET LeaseHolder = @me, LeaseUntilUtc = @until WHERE Name = @name AND NextRunUtc <= @now AND (LeaseUntilUtc IS NULL OR LeaseUntilUtc < @now)`), and runs it only if that changed a row. So exactly one API copy runs each job, with no new pod, port or connection in the chart.
    - *Alternative:* a Kubernetes CronJob running `dotnet MedApp.API.dll run-job <name>`. No: the AppHost, CI and `docker run` have no Kubernetes, and the locked-down chart would need another workload with the API's secrets. The command exists anyway for running a job by hand (decision 111).
    - *Alternative:* Hangfire or Quartz. No: a handful of jobs doesn't need their tables, dashboards and dependencies.
110. **Jobs are idempotent and say what they did.** Each job (`IScheduledJob`: a name, a schedule, `RunAsync(now, ct)` returning a count) marks each item done in the same save as the work (a unique key per item), so a job cut short repeats nothing. Schedules are an interval or a daily UK time (outside 01:00 to 03:00, which clock changes skip or repeat). The lease lasts 10 minutes and is renewed while a job runs. Times come from `TimeProvider`.
111. **Watching them:** `ScheduledJobs` keeps each job's last start, finish, outcome, count and consecutive failures, and the exception's type name only (never its message, which can quote data). Administration, **Scheduled Jobs** (`ManageSetup`) shows them, with **Run now**. After three failures in a row, holders of `ManageSetup` are told once (`ScheduledJobFailing`) until it succeeds. `/health` reports Degraded when a job is two intervals overdue. `run-job <name>` runs one from the command line; `medapp-dev` gains `run_job`.
    - **Settings:** `Jobs:Enabled` (default true; the integration tests turn it off and call `IScheduledJobRunner.RunNowAsync`) and `Jobs:{Name}:Enabled`.
    - **Who:** jobs act as the system (`SystemActivityContext`, "medapp-jobs") wherever an actor is recorded.
112. **The first job is the operational purge** the privacy to-do asks for: expired sessions and sign-in events, notifications and untouched notepads, at the periods in `docs/governance/retention-schedule.md`, as settings (`Retention:Operational`), logging counts only. Patient records are never deleted by a job.

### 3.2 Pathways that branch

113. **Stages stay one ordered list; some can be skipped.** The current stage is the first that is neither finished nor skipped (`PathwayWorkflow`). Two ways to skip, both pathway changes through `PathwayChange`, audited, and seen by the monitor:
    - **Optional stages** (`PathwayTemplateMap.IsOptional`, copied onto the stage): when it's current, a holder of `WorkPathways` may **Skip** it with a reason (`StageSkipReason`: Not needed, Patient declined, Other, with a detail of 500 characters, required for Other). Audit `StageSkipped`.
    - **Branches:** an admin marks a stage as a **branch point** (`ChoosesBranch`) and gives later stages a branch name (`Branch`, 50 characters: "Benign", "Treatment"). Finishing a branch point asks the clinician to choose one branch; the stages of the other branches after it (up to the next branch point) are skipped with reason `BranchNotChosen`, in the same save. Audit `BranchChosen` (the branch name, and the skipped stage IDs). Stages without a branch name always run.
    - *Alternative:* a graph of stages with arbitrary next-stage links. No: every list, gate, visit, clock-stop and Insights query assumes an order; branches over an ordered list keep them working.
114. **The choice is the clinician's.** Branches are listed in the template's order, none pre-selected, with the stage names each contains. MedApp records the choice; it never reads results or notes to suggest one.
115. **Reinstating:** a holder of `UnlockStages` can reinstate a skipped stage (with a reason; audit `StageReinstated`). If it's before the current stage, it becomes current, as unlocking does. A branch can be re-chosen only by reinstating its stages.
116. **Skipped stages** have no gates and no visit: their Requested appointments are cancelled as when a stage is finished (`PathwayChange.CancelRequestedVisit`). If the clock-stop stage (Phase 2, decision 36) is skipped, the clock stops at discharge instead. Insights' stage times leave skipped stages out.
117. **Template checks:** a branch point must be followed by at least two branch names before the next branch point or the end; a branch-named stage must come after a branch point; the last stage can't be optional or in a branch (every pathway ends on a stage that runs). The template editor shows the branches as indented groups.

### 3.3 Sites, rooms and working hours

118. **Sites and rooms** (`Site`: name, address, postcode, phone; `Room`: a site and a name; archived, not deleted, once used; `ManageSetup`). The migration adds one site, **Main site** (fixed ID), which admins rename. Site names appear in reminders and letters.
119. **Opening hours per site and working hours per person replace the fixed day.** Each is a dated set of rows per weekday (`SiteHours`: opens and closes; `StaffHours`: starts, ends, and the site), kept with `TemplateHistory`, as calendar templates are, so past days show what applied.
    - **A person with no hours of their own** works the deployment default (`WorkingHours:Default`: Monday to Friday, 08:00 to 18:00), so nothing changes on deploy.
    - **Bounds:** any day of the week, between 06:00 and 23:00 UK time, on 5-minute boundaries, start before end. So a UK working day still never spans a clock change.
    - Staff hours are set by holders of `ManageClinics` or `ManageAnyCalendar`; each person sees their own, read only.
120. **The working-day rule reads the hours.** `WorkingHours` becomes `WorkingTime`, built per person and day from their hours, the site's hours and closed days (`WorkingDays` gains the hours). Everything that today checks 08:00 to 18:00 Monday to Friday (validators, `CalendarClashes`, `CalendarProjection`, `Availability`, the walk-in rule) checks the person's day instead. `WorkingDaysConventionTests` keeps every command validator on it. Each calendar day's `workStartUtc` and `workEndUtc` come from the person's hours, so the browser's grid needs no change beyond showing weekends when anyone works them.
121. **Closed days gain an optional site** (`ClosedDay.SiteId`; none means everywhere). A site's closed day closes its rooms and the hours of staff working there that day.
    - *Built in PR 21 (`feature/sites-rooms`), deciding:* one closed day everywhere per date and one per site per date (an unfiltered unique index on date and site); a site can't be closed on a date already closed everywhere, but closing everywhere over sites' days is allowed, and bank holidays ignore sites' days. Until PR 22 gives staff hours a site, a site's closed day closes no one's calendar (`ClosedDaySet.IsClosed` stays "closed everywhere"; `IsClosedAt` adds the site's own), so adding one reports no bookings and tells nobody. Sites and rooms: names unique (rooms within a site); always one open site (archiving or deleting the last is a 409); a site with open rooms can't be archived, and an archived site takes no new rooms or closed days; address (three lines), postcode and phone optional; deletable while unused, archived once used. Endpoints are admin-only for changes and staff for reads (the roles of today), to become `ManageSetup` and `UseApp` with the permissions PR.
122. **Evening and weekend clinics** are just hours: a site open until 20:00 on Tuesdays, a person working Saturday mornings. Nothing else is special about them.
    - *Built in PR 22 (`feature/working-hours`), deciding:*
      - **The default is fixed in code** (`WorkingHours.Start`/`End`, Monday to Friday), not a `WorkingHours:Default` setting: every deployment so far wants it, a person's own hours cover the exceptions, and a setting would have to reach the static rules and the demo seed. A setting is a to-do if a customer needs another default.
      - **A person on the default works at no particular site,** so only days closed everywhere close their calendar, and no site's hours bound theirs. A site with no hours of its own is open the default too.
      - **A person's day is their hours cut to their site's opening hours that day** (worked out when read, so a later change to either can't put anyone outside their site). Setting staff hours outside their site's that day, or at a site closed that weekday, is a 409; setting site hours that would leave anyone's current hours outside them is a 409 naming who, and so is archiving a site anyone works at (such a site also can't be deleted).
      - **No days means the default:** a `PUT` with an empty list puts a person, or a site, back on it. There's no way to say "works no days"; time away is a block.
      - **Anyone signed in reads anyone's hours** (`UseApp`): calendars show them anyway. Only holders of `ManageClinics` or `ManageAnyCalendar` set them; nobody sets their own without one. `ManageClinics` is new (Admin, Booking and Manager, as decision 69's table says; the migration adds it to the starting Booking and Manager roles by their fixed IDs).
      - **What's on a calendar stays** when hours change, as with template changes: nothing is moved or cancelled, and the grid widens to show it.
      - **The validators check the person when the command names them;** moving a block checks its owner in the handler, and a meeting checks each attendee in the handler (a 400 naming them), as the validator can't know whose calendars they are. A day closed meanwhile stays a 409.
      - **The grid:** each day carries `hasHours`, `siteId` and `gridStartUtc`/`gridEndUtc` (the same clock times on every day of one calendar or team view, from the earliest working start, or anything on the calendars, to the latest end, in whole hours); `workStartUtc`/`workEndUtc` are the person's hours, and the grid shades the rest. A day they work shows at full width, weekend or not.
      - **Week templates stay Monday to Friday;** a weekend day someone works gets template blocks through Apply Template. Day template blocks may be anywhere from 06:00 to 23:00.
      - **Pages:** the templates page's **Working Hours** tab (read only without the permission) and **Opening hours** on each open site in Sites and Rooms.

### 3.4 Clinic sessions

123. **A clinic session** (`ClinicSession`) is a block of a clinician's time in a room for a specialty, cut into slots: the member of staff, the room, the specialty, start and end (inside their hours and the site's), the slot length (5 to 120 minutes, dividing the session), and its status. Phase 2's decision 6 promised sessions a specialty.
    - **The session is busy time** for blocks and meetings on that person's calendar; **its slots are where appointments go.** Creating one over existing busy time is a 409 naming the clash, as today.
    - **A room holds one session at a time:** a room lock (`ICalendarLock` keyed `room:{id}`) and a clash check, alongside the person's lock.
124. **Booking into a session:** the booking dialog lists sessions of the appointment's specialty with free slots, by date ("Tue 3 March, k.ashford, Room 2, 4 of 12 free"). The appointment gets `ClinicSessionId` and `RoomId` and the slot's times. Appointments outside sessions stay possible, with an optional room (clash-checked).
125. **Clinic patterns** (`ClinicPattern`: person, room, specialty, weekday, times, slot length, every 1 to 4 weeks, from and until dates) make sessions only when someone asks: **Create sessions** for the next 1 to 13 weeks, by a holder of `ManageClinics`. It skips closed days and clashes, and lists what it made and what it skipped and why. A pattern never makes two sessions on one date (unique pattern and date).
    - *Alternative:* sessions generated by a job. No: sessions hold bookings, so a person should see what's created; and calendars keep their rule that nothing changes in the background.
126. **Cancelling a session** (`ManageClinics`, with a reason) cancels its booked appointments in the same save, each through `AppointmentChange` with that reason, and shows the list of patients to tell, with an option to text or email them (decision 130). The clinician is told when someone else cancels (`ClinicSessionCancelled`). Stage visits follow today's rules for a cancelled appointment.
127. **Sessions have a row version** and changes carry `expectedLastUpdated`, as meetings do. Moving a session with bookings is refused (cancel and recreate); changing its room is allowed if the new room is free.
    - *Built in PR 23 (`feature/clinic-sessions`), deciding:*
      - **The room's lock is `ICalendarLock` with the room's ID** (as the site's hours use the site's), not a `room:{id}` key: IDs are unique, and the lock already sorts and takes several at once.
      - **An appointment outside a session can't overlap a session** on its clinician's calendar: it's booked into a slot instead. One slot per appointment; the session's slots are equal divisions, so the unique index on session and start enforces one patient per slot.
      - **Booking into a session needs only the clinician's lock;** a site closed since the session was made refuses bookings into it. The booking dialog's list (`withFreeSlots`) carries each session's free slot starts, so it never opens a session (which would record reads of the patients in it).
      - **Changing a session never changes its clinician** (cancel and recreate). Moving its room moves its Booked appointments' room, each recorded as a move in its history; attended and missed ones keep the room they happened in.
      - **Cancelling tells the clinician once,** not once per appointment, and nobody is told per appointment. Cancelling part-way through its day leaves appointments that have started, for their outcome. The call list has each patient's name, NHS number and time, and the cancelled session shows it again; texting or emailing them waits for PR 26.
      - **Patterns:** "every N weeks" counts from the first matching weekday on or after the from date; a cancelled session still counts as its date's, so Create sessions doesn't remake it; a pattern can be changed (sessions already made stay) and ended with an until date, but not deleted.
      - **Sessions name a specialty, which the server doesn't match against the appointment's pathway:** specialties are labels (decision 54's rule); the booking dialog lists the pathway's specialty's sessions.
      - **Reads:** sessions are listed (not paged) for at most 62 days; Clinics and Clinic Patterns are pages under the Calendar (not Administration), and Clinic Patterns needs `ManageClinics`.
      - **Seeding sessions** is left to the Phase 4 demo data (PR 28), through the same checks (`SessionPlacement`) with the seed's clock.

### 3.5 Patient reminders and missed appointments

128. **Patient messages go through `IPatientMessageTransport`,** not the staff email queue: `Off` (default), `Offline` (each message is emailed to Mailpit with the destination in its subject, in development and on kind), and `Live` through **GOV.UK Notify** (text and email, one API, used across the NHS; a single template with the message as its personalisation). Another provider is another adapter if a customer's contract needs one.
    - *Alternative:* the existing SMTP sender for email reminders. No: it's best effort, in memory, and capped at 100 an hour per copy (8 on Azure), and texts need a provider anyway.
129. **Every message is a stored row** (`PatientMessage`): the patient, the appointment, the kind, the channel, the destination as sent, the text as sent, its status and the provider's reference. Jobs send pending ones and poll delivery status (no inbound webhook, so no new ingress route). A message is unique per appointment, kind, channel and appointment start, so a job cut short or a reschedule never sends twice.
130. **What's sent:**
    - **Reminders** (`patient-reminders` job, every 15 minutes, sending only from 09:00 to 19:00 UK): for each Booked appointment starting within `Reminders:DaysBefore` days (default 2), more than 2 hours away, on each channel the patient chose (`ReminderChannel`) for which they have a mobile or email.
    - **Moved and cancelled:** when staff move or cancel a booked appointment (or cancel a session), the dialog offers **Tell the patient by text or email**, ticked when the patient has a channel; the job sends it.
    - **Missed appointment:** if `Reminders:MissedMessage` is on, the next working day at 10:00, to a patient still marked Did not attend: "We missed you… please call {phone}".
131. **Messages never say why.** Templates in code, fixed wording: the centre's or site's name, the date and time, the site's address and phone, and for video appointments (Phase 5) the link. **Never** the specialty, clinician, pathway, stage or any note. A text is 160 characters where possible.
132. **Missed appointments are followed up by people.** The Requires Appointment page gains a **Did not attend** view: appointments marked Did not attend with no later appointment for the same pathway stage, oldest first, with the number of missed appointments on that pathway. Booking staff record a follow-up (`DnaFollowUpDecision`: Rebook, Clinician to review, No further appointment, with a detail of 500 characters) as appointment history (`DnaFollowUpRecorded`). **MedApp never discharges, re-prioritises or changes a pathway because of a missed appointment**; discharge is a clinician's action on the pathway.
133. **Preferences and opt-out:** the patient's `ReminderChannel` is asked on the patient form; staff change it when the patient asks (texts can't be replied to). A message to a patient with no channel or no number is stored as `NotSent`, so the appointment shows "No reminder: no consent or contact".
    - *Built in PR 26 (`feature/patient-reminders`), deciding:*
      - **One job, `patient-reminders`,** makes, sends and asks about delivery (rather than one job each). **Moves and cancellations go on its next run at any hour** (staff make them in the working day, and the news can't wait); reminders and missed-appointment messages only 09:00 to 19:00. `Reminders:MissedMessage` is **off by default**; a missed-appointment message goes only on the next working day (Monday to Friday, not closed everywhere), from 10:00, never later.
      - **The settings:** `PatientMessages:Mode` (`Off` by default, `Offline` in Development), `SenderName`, `ContactPhone` (asked for when the site has none), `OfflineRecipient` (a `.invalid` address), `PatientMessages:Notify:{ApiKey, SmsTemplateId, EmailTemplateId, BaseUrl}` (Live won't start without them), `Reminders:{DaysBefore, MissedMessage}`. The Notify client is hand-written (a JWT and two endpoints), with no new dependency.
      - **The site** is the appointment's room's; with no room, the message names no site rather than guess (the clinician's site could be wrong). Its phone, else `ContactPhone`, else "contact us".
      - **Each message is checked again when it's sent** (the appointment as the message says and still to come, the record not restricted or anonymised (a cancellation still goes to a restricted record, as cancelling does), the channel still chosen) and goes to the contact on the record then; one that no longer applies is `Cancelled` with a reason. Reminders aren't made for restricted or anonymised records. Five failed tries give one up (`TooManyAttempts`); a 429 stops the run's sending.
      - **No choice is one `NotSent` message on the Text channel** (`NoConsent`); a chosen channel without the contact is `NotSent` (`NoMobile`, `NoEmail`). A choice without the contact is allowed on the patient form, with a hint.
      - **Telling the patient** is offered when moving a Booked appointment to another time or room, cancelling a Booked one still to come, and cancelling a session, only while messages are on; a move of the staff member alone sends nothing. A drag on the calendar grid doesn't offer it.
      - **The Did not attend view** lists Did not attend appointments with no later appointment for the same stage (else pathway, else the patient's appointments without one) and no **No further appointment** decision: Rebook and Clinician to review stay listed until the later appointment exists, so neither is forgotten (H-55). The detail is required for No further appointment (who decided). Correcting the outcome to Attended clears the follow-up. The appointment gained `DnaFollowUpAtUtc` and `DnaFollowUpByUserId` beside the plan's two columns, and messages `RequestedByUserId`, `SendAttempts` and `StatusReason`.
      - **The stored numbers** are section 5's (`DnaFollowUpRecorded = 9` and the four new enums); `GET api/appointments/patient-messages/status` (`UseApp`) tells the pages whether messages are on.

### 3.6 Service alerts in monitoring

134. **Monitoring gains time-based events,** checked by the `service-alerts` job every 15 minutes rather than fired by a change: `ReferralNotTriaged` (awaiting triage longer than N days), `ResultNotSignedOff` (reported more than N days ago), `VisitNotBooked` (Requested longer than N days), `SpecimenOverdue` (N days past its type's expected turnaround), `WaitBreached` (a running clock N days before its target date, 0 meaning on breach) and `PauseReviewPassed` (a pause's review date passed). Rules gain `ThresholdDays` (required for these, 0 to 90) and a `SpecialtyId` condition; the template, stage and minimum priority conditions apply where they mean something.
    - The person is unused (400 if set), except for `ResultNotSignedOff`, where it's the responsible clinician.
    - Phase 2's decision 38 said breaches are shown, never sent; this makes sending them a rule an owner chooses.
135. **Each item is reported once per rule** (`MonitoringAlertsSent`, unique rule and item). Each run sends **one notification per rule** with the count and a link to the filtered list: "3 Dermatology referrals have waited more than 2 days for triage." Never a patient or free text.
136. **Event-driven:** `StageFinishedWithoutVisit` (the monitoring to-do): fired by `PathwayMonitor` when a Required stage is finished without its visit, matched on person, template and stage like `StageFinished`.
137. **Alerts are a convenience, never the safety net** (hazard H-29): the lists and their counts are. A stopped job is caught by decision 111.

### 3.7 Finishing without a visit, and retention classes

138. **`OverrideStages` splits into `UnlockStages` and `FinishWithoutVisit`.** A migration replaces every role's `OverrideStages` row with both, so no role loses anything, and adds the starting role **Senior clinician** (Clinician's permissions plus both). **Admin's fixed set keeps `UnlockStages` and loses `FinishWithoutVisit`**, as the owner's plan asks: an admin who is a senior clinician holds that role too. The release note tells admins to take both off Clinician if their senior clinicians are to be the only ones; the demo does.
139. **Retention classes per template** (`RetentionClass`: Standard, Oncology, Maternity, MentalHealth), set on the template and copied onto each pathway, like the specialty. A record's minimum period is the longest of the Standard rule (adult or child, as today) and each of its pathways' classes, counted from the conclusion of treatment. Years are settings (`Retention:Classes`), defaulting to 30 (oncology), 25 (maternity) and 20 (mental health), **for the DPO to check against the Records Management Code of Practice** before use. The retention review shows each record's class, and the warning about missing classes goes.
140. **Nothing is deleted automatically,** as today: the review lists, an admin decides.

### 3.8 Data model, API, pages, privacy and safety

**New tables:** `ScheduledJobs` (`Name` key, `NextRunUtc`, `LeaseHolder`, `LeaseUntilUtc`, `LastStartedAtUtc`, `LastFinishedAtUtc`, `LastOutcome`, `LastCount`, `ConsecutiveFailures`, `LastErrorType`); `Sites`, `Rooms`; `SiteHours` and `StaffHours` (weekday as .NET's `DayOfWeek` number, times, `SiteId`, `ValidFrom`, `ValidTo`); `ClinicPatterns`; `ClinicSessions` (`StaffUserId`, `RoomId`, `SpecialtyId`, `PatternId`, `StartUtc`, `EndUtc`, `SlotMinutes`, `Status`, `CancelledAtUtc`/`ByUserId`, `CancelReason`, `RowVersion`; unique `PatternId` and UK date); `PatientMessages` (`PatientId`, `AppointmentId`, `Kind`, `Channel`, `Destination`, `Body`, `AppointmentStartUtc`, `Status`, `ProviderReference`, `CreatedAtUtc`, `SentAtUtc`, `StatusAtUtc`; unique (`AppointmentId`, `Kind`, `Channel`, `AppointmentStartUtc`)); `MonitoringAlertsSent` (`RuleId`, `SubjectId`, `SentAtUtc`; key both IDs).

**New columns:** `PathwayTemplateMaps` and `PatientPathwayStages`: `IsOptional`, `ChoosesBranch`, `Branch`; stages also `SkippedAtUtc`, `SkippedByUserId`, `SkipReason`, `SkipDetail`. `Appointments`: `ClinicSessionId`, `RoomId`, `DnaFollowUp`, `DnaFollowUpDetail`. `ClosedDays`: `SiteId`. `MonitoringRules`: `ThresholdDays`, `SpecialtyId`. `PathwayTemplates` and `PatientPathways`: `RetentionClass` (default Standard).

| Endpoint | Permission | What |
|---|---|---|
| `GET api/jobs`, `POST api/jobs/{name}/run` | `ManageSetup` | Status; run now |
| `POST api/pathways/patient-pathways/{id}/stages/{sid}/skip`, `…/reinstate` | `WorkPathways` / `UnlockStages` | `{reason, detail?, expectedVersion}` |
| `POST …/stages/{sid}/finish` | as today | Gains `branch` for a branch point |
| `GET/POST/PUT api/sites`, `api/rooms`, `api/sites/{id}/hours` | `UseApp` / `ManageSetup` | |
| `GET/PUT api/calendar/staff/{userId}/hours` | `UseApp` / `ManageClinics` or `ManageAnyCalendar` | |
| `GET/POST/PUT api/clinics/patterns`, `POST …/{id}/create-sessions` | `ManageClinics` | `{weeks}` |
| `GET api/clinics/sessions`, `POST …/sessions`, `PUT …/{id}`, `POST …/{id}/cancel` | `UseApp` / `ManageClinics` | |
| `GET api/appointments/search?view=didNotAttend`, `POST api/appointments/{id}/dna-follow-up` | `ViewPatients` / `BookAppointments` | |
| `GET api/appointments/{id}/messages` | `ViewPatients` | The messages sent about it |
| `POST/PUT api/monitoring/rules` | `ManageMonitoring` | Gain `thresholdDays`, `specialtyId` |

**Pages:** the template editor's optional stages and branch groups; the pathway page's **Skip**, branch choice on Finish (a radio group of branches, none chosen) and skipped stages shown struck through with the reason; Administration: **Sites and Rooms**, **Scheduled Jobs**; the Calendar page's **Clinics** view (sessions by day and room) and **Clinic Patterns**; a person's **Working hours** tab on their templates page; the booking dialog's sessions; Requires Appointment's **Did not attend** view; an appointment's **Messages**; the Monitoring page's new events; the retention review's class.

**Privacy:**
- **Patient messages** are patient data: exported (destination, text, status, dates), anonymised (destination and text replaced), shown on the appointment. Sending is a disclosure to a processor: **DPIA risk 32** (a text read by someone else on a shared phone; GOV.UK Notify as a sub-processor; `sub-processors.md` and `international-transfers.md` must be updated in the same PR as the real sender, before it can send anything).
- **Branch choices and skip reasons:** the reason by name on the staff timeline, the detail admins only.
- **Scheduled jobs:** **DPIA risk 33** (automated processing nobody watches: purges and alerts). Jobs never log patient details; counts only.
- **Clinic sessions and hours** are staff data (when someone works): the staff privacy notice gains them.

**Safety** (PR 18): **H-51** A branch is chosen, or a stage skipped, in error (controls: confirmation listing what will be skipped, with the banner; reinstating; the audit). **H-52** A patient is booked when nobody, or no room, is available, or a cancelled clinic's patients aren't told (controls: hours and clash checks on the server; the call list and messages on cancelling). **H-53** A reminder reaches the wrong person or says too much (controls: no clinical words; destinations from the record; the stored copy). **H-54** A patient relies on a reminder that isn't sent (controls: "No reminder" shown; delivery status; reminders are a courtesy, DCB0160 says so). **H-55** A missed appointment isn't followed up (controls: the Did not attend view; no automatic discharge). **H-56** A scheduled job stops and nobody notices (controls: decision 111; extends H-29). **H-57** A record is anonymised too early because its retention class is wrong (controls: class shown on the review; DPO-checked defaults).

| Feature | Device position |
|---|---|
| Branches and optional stages | Not a device: the template author defines the choices; the clinician chooses; nothing is suggested. Becomes a device question if MedApp chose or pre-selected a branch from recorded data. |
| Hours, sites, sessions | Not a device: administrative scheduling. |
| Reminders, missed-appointment messages and the Did not attend view | Not a device: say when an appointment is; acting on attendance stays a person's decision. |
| Service alerts | Not a device: dates and statuses against thresholds an admin chose, never clinical values. |
| Retention classes | Not a device: records management. |

### 3.9 Demo data (PR 27)

- **Sites:** Main site and an invented outreach clinic; rooms in each. Site hours with a Tuesday evening clinic until 20:00 and Saturday biomechanics mornings at the outreach site.
- **Hours:** part-time patterns for the visiting consultants and the podiatrist; everyone else on the default.
- **Clinics:** patterns for each specialty's clinics (from Phase 1's Morning clinic, Afternoon clinic, Minor surgery list, Outreach clinic), sessions made for the 18 months and the next 6 weeks through Create sessions; most appointments booked into slots; one session cancelled with its call list.
- **Branches:** Skin lesion branches after "Results and team review" into Benign (Discharge letter to GP) and Treatment (Treatment, Follow-up, Discharge letter to GP); Follow-up optional in Biomechanics; a few stages skipped (patient declined) and one reinstated.
- **Reminders:** offline messages for past appointments, a few not sent (no consent), some failed; Did not attend follow-ups recorded, three waiting now.
- **Monitoring:** each admin and the service manager have service-alert rules (untriaged 2 days, results 7 days, visits 14 days); alerts sent through the history.
- **Roles:** consultants and the GP with a special interest move to Senior clinician; Clinician loses the two override permissions.
- **Retention:** Skin lesion is Oncology.
- **Jobs:** run history for each job, one failure. `DemoDataSeedTests` checks appointments inside each person's hours and their session's slots, no room double-booked, branches consistent, one message per appointment and kind.

## 4. Phase 5: readiness for a real customer

### 4.1 The MDT list

141. **An MDT item** (`MdtItem`) puts a pathway forward for a specialty's multidisciplinary team meeting: the pathway, the specialty, the meeting (a calendar `Meeting` marked as its specialty's MDT: `Meeting.SpecialtyId` and `IsMdt`), the question for discussion (free text, 1,000 characters), who listed it and when, and its status (`MdtItemStatus`: Listed, Discussed, Deferred, Withdrawn). Holders of `WorkPathways` list and withdraw; audit `MdtListed`, `MdtWithdrawn`.
142. **The outcome** is recorded by a holder of `RecordMdtOutcomes`: free text (2,000 characters) and who was present (chosen from the meeting's attendees; `MdtItemAttendees`). Audit `MdtOutcomeRecorded`; the pathway's assignees are told (`MdtOutcomeRecorded`). **Deferring** moves it to a later meeting (audit `MdtDeferred`). The outcome isn't copied into a note: the pathway page's **MDT** card shows it. Any change to the pathway that follows is a person's action.
143. **The MDT page** (`/mdt`): choose a specialty and meeting; the list in the order items were listed, each with the banner, the question, links to the pathway, its results and files; a print view. A meeting's title and notes still never name patients: the list is separate, and opening or printing it records `PatientListed` for each patient.
144. **A cancelled meeting** clears its items' meeting and keeps them Listed, flagged "Meeting cancelled: list again".

### 4.2 Consent records

145. **A consent record** (`ConsentRecord`): the patient, pathway and stage, the consent form (`ConsentForm`, an admin list with name and version, archived once used), the date signed, who signed (`ConsentSignedBy`: the patient, a person with parental responsibility, or a best-interests decision recorded by a clinician), the clinician recording it (`RecordConsent`), the scanned form's attachment ID (optional, flagged "No scan" when missing), and its status (`ConsentStatus`: Valid, Withdrawn with date and detail, EnteredInError). Audit `ConsentRecorded`, `ConsentWithdrawn`, `ConsentEnteredInError`.
146. **Gate:** a stage flagged `NeedsConsent` can't be finished without a Valid consent record for it. No override: recording the consent is the action.
147. **MedApp doesn't assess consent:** not capacity, validity, or whether the form fits. It records what the clinician states. A withdrawal after the stage finished is shown on the pathway, flagged; nothing changes by itself.

### 4.3 Reports for commissioners

148. **Monthly figures by specialty** (`/admin/reports`, `ViewReports`), UK calendar months, any range up to 24 months:
    - **Referrals:** received, accepted, rejected, redirected; median days to triage.
    - **Pathways:** started, discharged, cancelled.
    - **Appointments:** attended, did not attend, the did-not-attend rate, cancelled.
    - **Waits:** open clocks at month end, breached at month end, and the median and 92nd percentile of days waited for clocks open at month end.
    - **Lab:** specimens reported, and the median and 90th percentile of days from taken to reported, by specimen type.
    - **Results:** median days from report to sign-off, and those awaiting sign-off at month end.
149. **Aggregated in SQL, as Insights is:** the LINQ in `MonthlyReportQueries`, each query ending in a `GroupBy` or `Count`, with `ToQueryString` tests. Percentiles are worked out in C# from SQL's counts per whole number of days (a histogram), so no patient-level rows leave the database.
150. **CSV download** (`GET api/reports/monthly.csv`, `Content-Disposition: attachment`, `no-store`), one row per month and specialty. **For sharing outside**, ticked by default: counts from 1 to 7 are shown as `<8` and rates from them left blank (`Reports:SuppressBelow`, default 8, for the DPO to confirm). The screen shows exact figures to the organisation's own staff.
151. **No patient-level extracts** (a waiting-list dataset per patient) in this phase: they're personal data leaving the organisation and need their own DPIA (section 8). Reports record nothing in the activity log (counts only, as Insights).

### 4.4 Outcome questionnaires

152. **Questionnaire definitions** (`QuestionnaireDefinition`, `ManageSetup`): a name, a version, instructions, and questions (each a choice from listed answers, a number, or text), plus an optional label for a total ("Total score, as written on the form"). Immutable once a response uses it: a change is a new version. An admin confirms the organisation holds any licence the instrument needs (who and when recorded). The demo's are invented.
153. **A response** (`QuestionnaireResponse`): the patient, the pathway (optional), the definition and version, the date completed, the timepoint (`QuestionnaireTimepoint`: Baseline, Follow-up, Other), the answers, **the total as written on the form** (if the definition has one), who entered it, and its status (Recorded, EnteredInError). Holders of `RecordQuestionnaires` enter it from the paper form or with the patient in clinic. Audit `QuestionnaireRecorded`, `QuestionnaireEnteredInError`.
154. **Recorded and shown, never scored.** The pathway's **Questionnaires** card lists responses in date order with their answers and total as entered. MedApp **calculates no total, band or change, draws no chart and colours nothing**. *Alternative:* add up the answers. No: a calculated score moves toward the device definition, and the instruments' own scoring rules are their licensors'.

### 4.5 Private patients

155. **Funding per pathway** (`PathwayFunding`): source (`FundingSource`: NHS, Self-pay, Insured), and for insured, the insurer (`Insurer`: an admin list with name, address and claims email), the policy or membership number and the authorisation code (each up to 50 characters). Set by `ManageBilling` holders; a pathway with no row is NHS.
156. **Referrals gain a source** (`ReferralSource`: GP practice, Self-referral, Other clinician). For a self-referral, the practice is optional.
157. **Fees and invoices:** a fee schedule (`FeeItem`: name, specialty, price in pounds, archived once used). An **invoice** (`Invoice`): a number from a SQL sequence (`INV-{yyyy}-{nnnnn}`), the patient, the pathway, the payer (the patient or the insurer), lines copied from fee items (name and price at the time), issue and due dates, and its status (`InvoiceStatus`: Draft, Issued, Paid, Void with a reason). **Payments** (`InvoicePayment`: date, amount, `PaymentMethod`) are recorded, never taken: MedApp handles no card details. Issuing renders a PDF with the letters' engine (decision 98), attached to the pathway. Prices are `decimal(10,2)`; no VAT (medical care is usually exempt), noted for the customer to confirm.
158. **Opening an invoice** records `InvoiceViewed = 49`. Invoices are in the export. **Anonymisation** is refused while an invoice is Issued and unpaid; it replaces the policy number, authorisation code and payer name and deletes the PDFs, but keeps the invoice number, dates and amounts, which the organisation's tax records need (6 years; the DPO confirms).
159. **Insurers are a new recipient** (**DPIA risk 36**): the privacy notice says so for insured patients.

### 4.6 The data-request pack

160. **A data request** (`DataRequest`, `ManagePrivacy`): the patient, received on, who asked (the patient, or someone on their behalf: name and relationship, free text), identity checked (date, by whom, `IdentityCheckMethod`), the due date (one calendar month from receipt, the last day of the month when there's no matching day), an extension of up to two more months with a reason, and its status (`DataRequestStatus`: Open, Sent, Refused with a reason, Withdrawn). Holders of `ManagePrivacy` are told 7 days before the due date (`DataRequestDueSoon`, a daily job).
161. **The clinician review:** before the pack is made, a clinician (`SignOffResults`, as a proxy for "a clinician") reviews the record and marks items to withhold under the serious harm test, each with a reason; notes flagged `IsAboutAnotherPatient` start withheld. Who reviewed and what was withheld are kept on the request.
162. **The pack:** a PDF in plain language (the letters' engine), with what Article 15 asks for beside the data (why it's held, who it's shared with, how long it's kept, where it came from, the patient's rights and how to complain to the ICO), and a ZIP of the files themselves, leaving out withheld ones. It's built on request and downloaded (`DataRequestPackDownloaded = 50`), never stored. Sending is recorded (date, method).
163. **The request is patient data:** exported with the record, and its free text replaced on anonymisation. **DPIA risk 37:** a pack sent to the wrong person (controls: identity check recorded before the pack can be made; the review).

### 4.7 Video and phone appointments

164. **Appointments gain a mode** (`AppointmentMode`: In person, Phone, Video) and, for video, a link (500 characters, `https` only, its host in `VideoConsultations:AllowedHosts`); each member of staff may set a default room link. Changing the mode is appointment history (`ModeChanged`). Phone and video appointments need no room; a session may be remote (no room).
165. **The call happens in another tool.** MedApp shows the link to staff and puts it in the patient's reminder (decision 131); phone appointments show the patient's mobile. An Attended appointment of any mode satisfies a stage's visit, as today; a stage that needs the patient in person says so in its instructions (section 8). **DPIA risk 40** (the video platform as a processor, chosen by the customer).

### 4.8 A real malware scanner

166. **Two scanners behind `IMalwareScanner`:** `ClamAvMalwareScanner`, which streams the upload to a `clamd` container (`INSTREAM`) before it's stored, and `DefenderForStorageScanner` on Azure, which reads Microsoft Defender for Storage's verdict (the blob index tag it writes) after upload. `Attachments:Scanner` chooses (`None` stays the default).
167. **While a file is being scanned** (`AttachmentScanStatus.Pending`, Defender only), it shows "Being scanned", and downloading it is a 409 ("Try again in a minute") until a `malware-verdicts` job (every minute) reads the verdict. **Infected** after upload: the file is soft-deleted by the system with an `AttachmentDeleted` entry (`reason: malware`), and the uploader and `ManageSetup` holders are told (`MalwareFound`). Infected at upload (ClamAV) is refused, as today.
168. **Deploying:** the chart gains an optional ClamAV container (`malwareScanner.clamav.enabled`, off by default; about 1.5 GB of memory for its signatures), with a network policy allowing only the API to reach it; `deploy/kind/up.sh --clamav` turns it on. Azure turns on Defender's scanning for the attachments account (`deploy/azure`). The AppHost runs ClamAV only when asked (`MedApp:ClamAv=true`). Tried on kind before merging.

### 4.9 Links to NHS systems

Each is an adapter (decision 77); the Offline stand-in is built and tested now, the Live one when its test environment can be reached (section 7).

169. **The e-Referral Service** (`IReferralSource`): fetched referrals land in **Incoming referrals** (`IncomingReferral`: the UBRN, the NHS number, name and date of birth as sent, the practice's ODS code, the e-RS service mapped to a specialty (`ErsServiceMapping`, `ManageSetup`), the letter's bytes, and a status), never straight into the inbox. A holder of `RegisterReferrals` matches each to a patient (exact NHS number shown beside name and date of birth, or Add patient), then registers it through the normal command, with `Channel = ElectronicReferralService` and the UBRN (`Referral.ExternalReference`, unique). **MedApp never matches on its own** (hazard H-62). Offline: a stand-in makes a few synthetic referrals when asked (a button on Incoming referrals). Unmatched incoming referrals are kept until matched or rejected, and listed as patient data without a patient (**DPIA risk 39**).
170. **The real demographics lookup** (`PdsFhirDemographicsService`, `Pds:Mode=Live`): built against PDS's open sandbox now; its integration and live environments need onboarding. It also fills the address and registered practice (decision 78) for staff to confirm.
171. **Letters to GP systems** (`GpConnectSendDocumentTransport`, an `ILetterTransport` method `GpConnect`): sends an approved letter's PDF to the practice through GP Connect Send Document over MESH, addressed by the practice's ODS code. Offline: the PDF is written to an `offline-mesh/` folder in blob storage and marked Sent, so the flow is visible.
172. **The lab's own system** (`ILabGateway`): orders out and status and reports back, **reports as documents only**: no result values are stored, so the results rules stay as they are. Offline: a **simulated lab** job moves Sent specimens to Received after a few hours and Reported after the type's expected days, with a synthetic PDF. The Live adapter is built per lab, from its interface specification (HL7 v2 or FHIR through the lab's interface engine), with OAuth client credentials for the system (the FHIR to-do).
173. **Each Live mode updates governance first:** a new source or recipient in the data inventory, DPIA and ROPA, before it's turned on (`docs/agents/nhs-fhir-snomed.md`'s rule).

### 4.10 Several organisations in one deployment (roadmap step 8)

174. **Build it last in Phase 5, then stop adding tables without it.** Every feature before it is built single-organisation; a first customer runs in a dedicated deployment, as the chart already allows. Waiting longer only adds tables to convert.
175. **The model** (`docs/architecture.md`, step 8): an `Organisation`, Phase 4's sites under it, and `OrganisationId` on every table; a migration (`AddedOrganisations`) puts everything into one default organisation. Each account belongs to one organisation; **usernames stay unique across the deployment**, so sign-in doesn't change. Unique indexes become unique per organisation (the NHS number, specimen and invoice numbers, names in setup lists), and so do the number sequences' prefixes.
176. **Separation in two layers:** EF Core global query filters on the organisation from the session's claim, and SQL Server row-level security on `SESSION_CONTEXT`, set by a connection interceptor, so a query that misses the filter returns nothing. Jobs run once per organisation, each in its own scope. Blobs are stored under the organisation's prefix.
177. **Administration:** a platform administrator, separate from each organisation's admins, who creates an organisation and its first admin (with two-factor), and **offboards** one: export everything it holds, then delete it (Article 28(3)(g)). Per-organisation settings: retention, the two-factor requirement, targets, the centre's name and phone. Organisation events in the activity log: `OrganisationCreated = 51`, `OrganisationOffboarded = 52`.
178. **Tests:** for every endpoint, a user of organisation A gets a 404 for organisation B's IDs; a reflection test that every entity has the filter; a database test that row-level security blocks an unfiltered query. **H-63** (one organisation sees another's patients) and **DPIA risk 38**.

### 4.11 Privacy, safety and the device position

**Privacy:** each table above follows decision 76. Export and anonymisation cover MDT items (question and outcome replaced), consent records (withdrawal detail replaced; scans deleted with the files), questionnaire responses (text answers replaced; choices and numbers kept, as dates are), funding and invoices (decision 158), data requests (requester's name and relationship replaced), video links (cleared), incoming referrals (deleted once matched and registered, or rejected). Reads: the MDT list `PatientListed`; invoices `InvoiceViewed`; consent and questionnaires through the pathway page (`PathwayViewed`). New DPIA risks: **34** (MDT lists shown and printed in meetings), **35** (consent and questionnaire answers: more special category detail), **36** to **40** as above.

**Safety** (PR 28): **H-58** An MDT outcome is recorded on the wrong patient, or not acted on (controls: the banner on each item and the outcome dialog; assignees told). **H-59** A procedure goes ahead without valid consent recorded, or a withdrawal is missed (controls: the `NeedsConsent` gate; withdrawal flagged on the pathway). **H-60** Questionnaire answers are read as an assessment, or recorded on the wrong patient (controls: no totals or charts by MedApp; the banner). **H-61** A video link reaches the wrong patient (controls: the link only in that patient's message; allowed hosts). **H-62** Data from an NHS system is matched to the wrong patient (controls: incoming referrals matched by a person, never automatically; lab reports filed by specimen number). **H-63** as above. **H-64** A file is held as "being scanned" when it's needed (controls: a minute's scan; the job's watch, decision 111).

| Feature | Device position |
|---|---|
| MDT list and outcomes | Not a device: a list people choose and a record of what the team decided. |
| Consent records and the gate | Not a device: records the clinician's statement; assesses nothing. |
| Commissioner reports | Not a device: counts and time spans of workflow. |
| Outcome questionnaires | Not a device **as designed**: answers and the total as written are stored and shown. Calculating, banding or flagging scores would make it a device question. |
| Private patients, data requests, multi-tenancy, malware scanning | Not clinical functions. |
| NHS adapters | Not a device: data moved without change; lab reports stay documents. |
| Video and phone appointments | Not a device: administrative scheduling and a link. |

**Claims the manufacturer must not make** gains "MedApp scores questionnaires", "MedApp checks consent is valid" and "MedApp decides the next step in a pathway".

### 4.12 Before a real team uses it (the go-live list, Step 7)

179. **What the team builds and checks, as the last PRs:**
    - **Put the safety gates back** (`docs/todo.md`, Testing): the full suite a required check on every PR, Publish waiting for it, full reviews again.
    - **Background jobs and scanning:** built in decisions 109 to 112 and 166 to 168; confirmed running on kind.
    - **Accessibility:** the manual screen reader pass (`docs/todo.md`, Accessibility) and the axe audit of every screen.
    - **The device decision and hazard log** brought up to date with everything built, for the CSO (section 7).
    - **A restore test:** restore the database and blobs from backup on kind and Azure, with the timings written down.
    - **The clinical read of the labels** ("Discharged", "On hold") prepared for the CSO.
180. **Roles before go-live** (Phase 2's open question 10): once every account holds the starting roles, archive "All staff (before roles)" and take Admin's everyday clinical and booking permissions away (a small PR), so admins administer and clinicians do clinical work.

### 4.13 Demo data (PR 39)

MDT items each week for the skin cancer team meeting with outcomes; consent forms (invented names, e.g. "Consent for a skin procedure (demo)") with records on biopsy stages, one withdrawn; two invented questionnaires ("Skin comfort questionnaire (demo)", "Walking comfort questionnaire (demo)") with baseline and follow-up responses; private dermatology patients, self-pay and insured with invented insurers, invoices issued, paid, part-paid and void; two data requests (one sent, one due in five days); some video and phone follow-ups with example links on an allowed example host; e-RS referrals through the offline source, two unmatched; the simulated lab reporting the in-house lab's specimens; one demo organisation, and the cross-organisation tests' second organisation only in tests.

## 5. Stored numbers

Every new stored enum and every value added to an existing one, never renumbered. Permissions are stored by name (section 1).

| Enum | Values | Phase |
|---|---|---|
| `ActivityAction` (added) | `SpecimenViewed = 45`, `ResultViewed = 46`, `LetterViewed = 47`, `StaffDetailsChanged = 48` | 3 |
| | `InvoiceViewed = 49`, `DataRequestPackDownloaded = 50`, `OrganisationCreated = 51`, `OrganisationOffboarded = 52` | 5 |
| `NotificationKind` (added) | `SpecimenRejectedByLab = 24`, `SpecimenLost = 25`, `ResultReady = 26`, `ResultReassigned = 27`, `ResultSignedOffByOther = 28`, `LetterAwaitingApproval = 29`, `LetterApproved = 30`, `LetterReturned = 31` | 3 |
| | `MonitoredReferralNotTriaged = 32`, `MonitoredResultNotSignedOff = 33`, `MonitoredVisitNotBooked = 34`, `MonitoredSpecimenOverdue = 35`, `MonitoredWaitBreached = 36`, `MonitoredPauseReviewPassed = 37`, `MonitoredStageFinishedWithoutVisit = 38` (with 10 to 17, `Notification.IsMonitoring`, now a set), `ClinicSessionCancelled = 39`, `ScheduledJobFailing = 40` | 4 |
| | `MdtOutcomeRecorded = 41`, `DataRequestDueSoon = 42`, `MalwareFound = 43` | 5 |
| `MonitoringEvent` (added) | `ReferralNotTriaged = 8`, `ResultNotSignedOff = 9`, `VisitNotBooked = 10`, `SpecimenOverdue = 11`, `WaitBreached = 12`, `PauseReviewPassed = 13`, `StageFinishedWithoutVisit = 14` | 4 |
| `PatientPathwayAuditAction` (added) | `SpecimenTaken = 16`, `SpecimenSent = 17`, `SpecimenReceived = 18`, `SpecimenReported = 19`, `SpecimenRejected = 20`, `SpecimenLost = 21`, `SpecimenEnteredInError = 22`, `SpecimenDetailsEdited = 23`, `ResultRecorded = 24`, `ResultSeen = 25`, `ResultSignedOff = 26`, `ResultReassigned = 27`, `ResultEnteredInError = 28`, `LetterDrafted = 29`, `LetterSubmitted = 30`, `LetterReturned = 31`, `LetterApproved = 32`, `LetterSent = 33`, `LetterWithdrawn = 34` | 3 |
| | `StageSkipped = 35`, `BranchChosen = 36`, `StageReinstated = 37` | 4 |
| | `MdtListed = 38`, `MdtOutcomeRecorded = 39`, `MdtDeferred = 40`, `MdtWithdrawn = 41`, `ConsentRecorded = 42`, `ConsentWithdrawn = 43`, `ConsentEnteredInError = 44`, `QuestionnaireRecorded = 45`, `QuestionnaireEnteredInError = 46` | 5 |
| `ReferralStatus` (added) | `InformationRequested = 6` | 3 |
| `ReferralEventAction` (added) | `InformationRequested = 12`, `InformationReceived = 13` | 3 |
| `AppointmentEventAction` (added) | `DnaFollowUpRecorded = 9` | 4 |
| | `ModeChanged = 10` | 5 |
| `AttachmentScanStatus` (added) | `Pending = 3` | 5 |
| `SpecimenStatus` | `Taken = 0`, `Sent = 1`, `Received = 2`, `Reported = 3`, `RejectedByLab = 4`, `Lost = 5`, `EnteredInError = 6` | 3 |
| `SpecimenRejectionReason` | `Unlabelled = 0`, `Mislabelled = 1`, `Leaked = 2`, `InsufficientSample = 3`, `WrongContainer = 4`, `Other = 5` | 3 |
| `ResultSource` | `Specimen = 0`, `ExternalReport = 1` | 3 |
| `ResultStatus` | `AwaitingSignOff = 0`, `Seen = 1`, `SignedOff = 2`, `EnteredInError = 3` | 3 |
| `ResultAction` | `NoFurtherAction = 0`, `PatientInformed = 1`, `AppointmentArranged = 2`, `ForMdtDiscussion = 3`, `ReferredOn = 4`, `Other = 5` | 3 |
| `LetterKind` | `ClinicLetter = 0`, `DischargeSummary = 1`, `ReferralResponse = 2`, `InformationRequest = 3`, `AppointmentLetter = 4`, `Other = 5` | 3 |
| `LetterStatus` | `Draft = 0`, `AwaitingApproval = 1`, `Approved = 2`, `Withdrawn = 3` (Returned is a Draft with a return comment) | 3 |
| `LetterRecipientKind` | `GpPractice = 0`, `Patient = 1`, `Referrer = 2`, `Other = 3` | 3 |
| `LetterDispatchStatus` | `NotSent = 0`, `Sent = 1`, `Failed = 2` | 3 |
| `LetterDispatchMethod` | `Post = 0`, `HandedToPatient = 1`, `SecureEmail = 2`, `EmailOutsideMedApp = 3`, `Other = 4` | 3 |
| | `GpConnect = 5` | 5 |
| `ReminderChannel` | `None = 0`, `Text = 1`, `Email = 2`, `TextAndEmail = 3` | 3 (used in 4) |
| `ScheduledJobOutcome` | `Succeeded = 0`, `Failed = 1`, `Skipped = 2` | 4 |
| `StageSkipReason` | `BranchNotChosen = 0`, `NotNeeded = 1`, `PatientDeclined = 2`, `Other = 3` | 4 |
| `ClinicSessionStatus` | `Planned = 0`, `Cancelled = 1` | 4 |
| `PatientMessageKind` | `AppointmentReminder = 0`, `AppointmentMoved = 1`, `AppointmentCancelled = 2`, `MissedAppointment = 3` | 4 |
| `PatientMessageChannel` | `Text = 0`, `Email = 1` | 4 |
| `PatientMessageStatus` | `Pending = 0`, `Sent = 1`, `Delivered = 2`, `Failed = 3`, `Cancelled = 4`, `NotSent = 5` | 4 |
| `DnaFollowUpDecision` | `Rebook = 0`, `ClinicianToReview = 1`, `NoFurtherAppointment = 2` | 4 |
| `RetentionClass` | `Standard = 0`, `Oncology = 1`, `Maternity = 2`, `MentalHealth = 3` | 4 |
| `MdtItemStatus` | `Listed = 0`, `Discussed = 1`, `Deferred = 2`, `Withdrawn = 3` | 5 |
| `ConsentSignedBy` | `Patient = 0`, `PersonWithParentalResponsibility = 1`, `BestInterestsDecision = 2` | 5 |
| `ConsentStatus` | `Valid = 0`, `Withdrawn = 1`, `EnteredInError = 2` | 5 |
| `QuestionnaireTimepoint` | `Baseline = 0`, `FollowUp = 1`, `Other = 2` | 5 |
| `QuestionnaireResponseStatus` | `Recorded = 0`, `EnteredInError = 1` | 5 |
| `FundingSource` | `Nhs = 0`, `SelfPay = 1`, `Insured = 2` | 5 |
| `ReferralSource` | `GpPractice = 0`, `SelfReferral = 1`, `OtherClinician = 2` | 5 |
| `ReferralChannel` | `Manual = 0`, `ElectronicReferralService = 1` | 5 |
| `InvoiceStatus` | `Draft = 0`, `Issued = 1`, `Paid = 2`, `Void = 3` | 5 |
| `PaymentMethod` | `Card = 0`, `BankTransfer = 1`, `Cash = 2`, `Insurer = 3`, `Other = 4` | 5 |
| `DataRequestStatus` | `Open = 0`, `Sent = 1`, `Refused = 2`, `Withdrawn = 3` | 5 |
| `IdentityCheckMethod` | `InPerson = 0`, `Documents = 1`, `KnownToService = 2`, `Other = 3` | 5 |
| `AppointmentMode` | `InPerson = 0`, `Phone = 1`, `Video = 2` | 5 |
| `IncomingReferralStatus` | `Waiting = 0`, `Registered = 1`, `Rejected = 2` | 5 |

Hazards **H-45 to H-64** and DPIA risks **29 to 40** are allocated above.

## 6. Building it: the PRs in order

Each is one implementation agent's work, with its migration (reviewed, `has-pending-model-changes` clean, tested up and down on LocalDB), its tests (handlers, validators, mappers, Vitest for pages, `ToQueryString` tests for new SQL), its privacy checklist (decision 76) and governance updates, and it keeps the seed running. **∥** marks PRs that can be built at the same time as the others in their group once what they depend on has merged.

**Phase 3** (after Phase 2's PR 10):
11. **Safety and governance first (docs only).** *Built together with PRs 18 and 29, as one docs-only PR (branch `feature/centre-safety-phases-3-5`), so every feature PR builds on them; their hazard and DPIA risk numbers were free, so nothing here was renumbered.* The device reassessment (section 2.6, step 4 of the flow chart), hazards H-45 to H-50, DPIA risks 29 to 31 as drafts, and the deploying organisation's new duties in `dcb0160-deploying-organisations.md` (a daily lab worklist and results check, a downtime process for letters).
12. **∥ Contact details, practices and staff details.** Migration `AddedPatientContactDetails` (patient columns, `Practices`, `Referrals.PracticeId`, `FullName` and `JobTitle`). The patient form, the Practices page, the referral's practice picker, Account Settings. `StaffDetailsChanged`. *Built (branch `feature/patient-contact-gp`), deciding as recommended where this plan was silent:* `Practices` also has a `Version` (`expectedVersion` on every change, as specialties) and can be deleted while unused (`DELETE api/practices/{id}`, 409 once in use) and restored (`…/restore`); `GET api/auth/me/details` reads your own; a referral's typed practice and ODS code, left empty with a practice chosen, take the directory's; the FHIR Patient gains `address`, a mobile `telecom` and `generalPractitioner` in UK Core's shape; the offline demographics stub gives synthetic addresses and mobiles but no practice; `ReminderChannel` is a column only until PR 26; anonymising removes the contact details rather than writing placeholders.
13. **∥ Specimens.** Migration `AddedSpecimens` (types, labs, specimens, the sequence, `Notifications.SubjectId`, the `StageGates` home with the last-stage specimen gate). The permissions `RecordSpecimens` and `ProcessSpecimens`, the specimen card, label and request sheet, the Lab page, reporting with the file (PR 14 adds the result it creates), `SpecimenViewed`, the two notifications, timeline kind, export and anonymisation.
14. **Results sign-off** (after 13). Migration `AddedResults` (`Results`, `WaitsForResults`). Results from reports and external files, the Results pages, sign-off, reassigning, entered in error, the gates, `SignOffResults`, notifications 26 to 28, `ResultViewed`.
15. **∥ The PDF engine and letter templates** (after 12). PDFsharp and MigraDoc, Markdig, the bundled font and its `NOTICE`, `ILetterRenderer`, merge fields, the veraPDF check, `LetterTemplates` and its page with a preview. `NuGetAudit` clean; the Dockerfile unchanged (no native dependency). *Built (branch `feature/letter-templates`, migration `AddedLetterTemplates`: `docs/agents/letters.md`), deciding as recommended where this plan was silent:* **PDFsharp and MigraDoc 6.2.4**, the last MIT release (6.5.0 moved to the AGPL-3.0; Dependabot ignores 6.3 and later); **PDF/A not claimed yet** (veraPDF passes PDFsharp's PDF/A-1a output while all text is Windows-1252, and fails it for other characters, as no CIDSet is written: a to-do), so letters are tagged PDFs; templates are archived but not deleted until PR 16 knows whether one was used; template names are unique within a specialty (and among those for every specialty), ignoring case; merge fields are written `{{patient.name}}` (the list in decision 99, with `appointment.lastAttendedDate` for the last attended appointment); the preview is `POST api/letter-templates/preview` (the unsaved text, made-up details, a download); characters the bundled font lacks are a 400 when a template is saved or previewed, and drawn as `?` as a last resort; at most two renders at a time in each API copy; a specialty a template is for counts it and can't be deleted.
16. **Letters** (after 15). Migration `AddedLetters` (`Letters`, `LetterRecipients`, `NeedsLetterKind`). Writing, approval, recipients, the secure email transport, recording sending, withdrawing and correcting, the Letters page and cards, the letter gate, `InformationRequested` on referrals, the permissions, notifications 29 to 31, `LetterViewed`. An E2E journey: write, approve, record as posted.
17. **Phase 3 demo data** (section 2.7).

**Phase 4** (after PR 17):
18. **Safety and governance first (docs only).** *Done with PR 11.* Hazards H-51 to H-57, DPIA risks 32 and 33, the device rows (section 3.8).
19. **∥ Scheduled jobs.** Migration `AddedScheduledJobs`. The runner, the lease, the Scheduled Jobs page, `/health`, `run-job`, `medapp-dev`'s `run_job`, `ScheduledJobFailing`, and the operational purge as its first job. An integration test that two runners run a job once.
20. **∥ Branching pathways.** Migration `AddedStageBranches`. Optional stages, branch points, skipping, reinstating, the template editor's groups, Insights and the clock-stop rule for skipped stages, `PathwayCommandsMonitoringTests` for the new commands. *Built (branch `feature/branching-pathways`), deciding as recommended where this plan was silent:* the stored numbers are section 5's (`StageSkipped = 35` to `StageReinstated = 37`, `StageSkipReason` as listed); reinstating needs `OverrideStages` until PR 24 splits it (then `UnlockStages`); **no `expectedVersion`** on skip or reinstate, as pathways have no version and finish and unlock take none: a stale request finds the stage no longer current, or no longer skipped, and gets a 409; reinstating takes a free-text `reason` (500, required), not a `StageSkipReason`; templates take `stageOptions` beside `stageDefinitionIdsInOrder` (left out, plain stages, so older pages keep working; an edit without them keeps the stored ones while the stage list is the same, and makes every stage plain with a new list); a branch point is never optional or in a branch itself; branch names match ignoring case; skipping moves an unstarted pathway to In progress (it's work on it); one `BranchChosen` entry lists the skipped stages (IDs and names) rather than a `StageSkipped` each; no monitoring event for skips (none was allocated); `CompletedStageCount` stays finished stages only, with a new `SkippedStageCount`. **The clock-stop rule:** a skipped clock-stop stage stops nothing (the clock stops at discharge or cancellation), and reinstating never restarts a stopped clock. After the safety review: skipping a stage whose visit is Required needs `OverrideStages` and a detail, as finishing it without the visit does; a template edit that leaves `stageOptions` out with the same stage list keeps the stored options. Demo data's branches come with PR 28.
21. **∥ Sites, rooms and closed days by site.** Migration `AddedSites` (Main site by `HasData`). The admin pages. Small.
22. **Working hours** (after 21). Migration `AddedWorkingHours`. `WorkingTime` replacing the fixed day in every check (`WorkingDaysConventionTests`), site and staff hours with `TemplateHistory`, the hours tab, weekend days in the grid. Large; the integration tests for clashes and the walk-in rule are re-run with the owner's agreement (`ci:full`).
23. **Clinic sessions** (after 22). Migration `AddedClinicSessions`. Patterns, Create sessions, the room lock, booking into slots, cancelling with the call list, `ManageClinics`, `ClinicSessionCancelled`.
24. **∥ Permission split and the bypass event** (after Phase 2). Migration `SplitOverrideStages` (rows renamed, Senior clinician added), Admin's fixed set, `StageFinishedWithoutVisit` monitoring event. Small.
25. **Service alerts** (after 19, and Phase 3's 14). Migration `AddedServiceAlerts`. The time-based events, thresholds, specialty condition, `MonitoringAlertsSent`, the digest notifications.
26. **Patient reminders and missed appointments** (after 19, 21). Migration `AddedPatientMessages`. `IPatientMessageTransport` (Off, Offline to Mailpit, GOV.UK Notify), the reminder, moved, cancelled and missed messages, delivery polling, the Did not attend view and follow-up, the patient form's channel.
27. **∥ Retention classes.** Migration `AddedRetentionClasses`. `RetentionSchedule`, the review page, the template field. *Built (branch `feature/retention-classes`), deciding where this plan was silent:* changing a template's class also changes the pathways already made from it (otherwise classes set after go-live would leave existing records on the shorter period), so the copy on each pathway matters only for pathways whose template is gone; no new endpoint (the class goes through the template's create and edit, `ManageSetup`); the review's `retentionClass` field keeps its type and now also says Oncology, Maternity or MentalHealth when a class set the period; on a tie the adult or child rule names it; class years under 1 stop startup; the demo's Skin lesion templates are Oncology.
28. **Phase 4 demo data** (section 3.9). Numbered here after 27; built last in the phase.

**Phase 5** (after Phase 4's demo data):
29. **Safety and governance first (docs only).** *Done with PR 11.* Hazards H-58 to H-64, DPIA risks 34 to 40, the device rows (section 4.11).
30. **∥ MDT list.** Migration `AddedMdtItems` (and `Meeting.SpecialtyId`, `IsMdt`).
31. **∥ Consent records.** Migration `AddedConsentRecords` (and `NeedsConsent`).
32. **∥ Outcome questionnaires.** Migration `AddedQuestionnaires`.
33. **∥ Commissioner reports and CSV.** No migration.
34. **∥ Video and phone appointments.** Migration `AddedAppointmentModes`; the reminder text gains the link.
35. **∥ Private patients.** Migration `AddedBilling` (funding, insurers, fee items, invoices, payments, `ReferralSource`).
36. **∥ The data-request pack.** Migration `AddedDataRequests`; the reminder job, the review, the pack.
37. **∥ The malware scanner.** `AttachmentScanStatus.Pending`, ClamAV and Defender scanners, the verdict job, the chart's ClamAV container and network policy, `up.sh --clamav`, Azure's setting. Tried on kind (`docs/agents/deploy.md`).
38. **NHS adapters, offline first,** three ∥ PRs: **38a** the e-Referral Service source and Incoming referrals (migration `AddedIncomingReferrals`); **38b** the GP Connect letter transport and the simulated lab (`ILabGateway`), offline; **38c** the PDS adapter against its sandbox. Plus Administration, Integrations.
39. **Phase 5 demo data** (section 4.13).
40. **Multi-tenancy, last,** in sequence: **40a** the model and migration `AddedOrganisations` (every table, the default organisation, the session claim); **40b** enforcement (query filters, row-level security, unique indexes per organisation, the cross-organisation test suite); **40c** the platform administrator, onboarding and offboarding, per-organisation settings. Each XL-sized for one agent; split further by area if one can't finish.
41. **Go-live** (decisions 179 and 180): CI strict again, the roles clean-up, the restore test, the documents for the CSO.

## 7. Outside the team

Code can make MedApp ready; these need a person, an organisation or a contract.

| What | What MedApp builds to be ready | What stays with a person |
|---|---|---|
| **Clinical safety sign-off** (DCB0129) | The hazard log (H-01 to H-64), the safety case, the device decision, each phase's reassessment | A qualified Clinical Safety Officer reviews, completes and signs them; the customer's own CSO does DCB0160 |
| **The medical device decision** | The reasoning per feature (sections 2.6, 3.8, 4.11) and the claims not to make | A regulatory person confirms it, and the EU and Northern Ireland assessment |
| **DSPT** (Data Security and Protection Toolkit) | `docs/governance/dspt-readiness.md`, the policies, logging, encryption | The organisation submits it each year |
| **DTAC** (digital technology assessment) | The evidence pack from the docs above | Completed and submitted to each NHS buyer |
| **Penetration test** and Cyber Essentials | Fixes, the threat model, ASVS checklist | An accredited tester (CREST or CHECK) and the certification body |
| **NHS onboarding:** PDS, e-RS, MESH and GP Connect Send Document | Adapters with Offline stand-ins, PDS against its sandbox | NHS England's onboarding: connection agreements, CIS2 or API keys, test environments, assurance; months each |
| **A lab's interface** | `ILabGateway` and the simulated lab | The lab's interface specification and an agreement with the lab |
| **Secure email** | The DCB1596 switch and the transport | The organisation's NHSmail (or accredited) relay and its declaration |
| **Texts and emails to patients** | The GOV.UK Notify adapter and the Offline one | Eligibility for GOV.UK Notify, or a contract with another provider; the sub-processor listed |
| **Contracts** | `docs/governance/dpa-template.md`, the ROPA and sub-processor list | The customer's contract and DPA; insurers' agreements for private patients; licences for any questionnaire used |
| **DPO decisions** | Settings with proposed defaults | Retention class years, operational purge periods, small-number suppression, invoice retention |
| **People-run checks** | The axe audit, the restore runbook | The manual screen reader pass; the hazard workshop with clinicians; the CSO's read of the labels |

## 8. Left out, for later

- **Structured result values** (numbers, ranges, the lab's flags): a device question; reports stay documents.
- **Letters to patients by email, or through the NHS App,** and print-and-post services: after a verified address or portal.
- **Looking practices up in the national ODS service:** the directory is typed for now.
- **A stage that must be face to face** (a flag beside the visit rule): instructions say so for now.
- **Patient-level extracts for commissioners:** need their own DPIA.
- **Patients completing questionnaires themselves,** and self-booking: need a patient portal.
- **Recurring clinic changes in bulk** (moving a whole session's bookings to another session).
- **Narrowing what each role sees** (by specialty; hiding restricted patients) and an auditor's read permission: on top of Phase 2's permissions.
- **Per-organisation identity providers (OIDC):** roadmap step 4, before or with multi-tenancy's onboarding of a second customer that wants one.
