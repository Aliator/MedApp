# MedApp: to do

Improvements and features to pick up later, outside the phased plans (`docs/procedures-ui-plan.md`). Move an item into a plan or a branch when it's started, and delete it here when it's merged.

## Accessibility

The axe audit (`npm run a11y` in `tools/ui-parity`: every screen in dark, light and a colour theme, WCAG 2.2 A and AA) passes, and the Accessibility card has the text size, the hover delay and the switches in `src/app/accessibility.tsx`. The statement is at `/accessibility`. Still to do:

- **Three colour themes just under 4.5:1 on secondary text.** Measured against the secondary text colour (subtitles, Back, descriptions), Y2K's light page is 4.19:1, and the header bars of Chocolate's light cards and Butter's dark cards 4.41:1. The custom colour check (`textContrast`) now counts secondary text; these themes were left as they are so they don't change how they look. Decide whether to adjust them (a test lists them in `src/app/colours.test.tsx`).
- **Insights at high zoom.** At 400% zoom (320 x 256 CSS pixels, WCAG 1.4.10's test), the header and filters fill the window, the area under them is 0 px high and the document doesn't scroll, so no chart can be reached. At 200%, each 30rem chart card is taller than the area it scrolls in. Let the whole page scroll when the window is that short, and check the other `fill` pages for the same (review of #94).
- **Screen reader pass by hand.** NVDA and VoiceOver through the main tasks (find a patient, work a stage, finish a procedure), and keyboard-only use of every dialog: focus trapped, and returned on close.

## Features

- **Calendar follow-ups.** What staff calendars and appointments left out (`docs/calendar-plan.md`, section 8):
  - **Closed days and working hours:** bank holidays and other closed days, and working hours for each person or site (today everyone's day is 08:00 to 18:00 UK time, Monday to Friday).
  - **More kinds of booking:** recurring appointments, rooms and resources, and booking several staff for one appointment.
  - **Walk-ins:** recording an appointment after the event. Booking is only in the future today.
  - **Patients:** reminders by email or SMS, and booking for themselves.
  - **Requested appointments left waiting:** flag or notify ones requested long ago and still not booked.
  - **Printing a day list.**
  - **Template edits that undo a day's changes.** Editing a day template's block gives it a new row, and a day's exception (the block removed or moved for one day) points at the old row, so the block comes back on that day, possibly over an appointment booked in its place. Keep a block's identity across versions (a lineage ID the exceptions key on), or at least list the future days' changes an edit will drop before saving (review of #93, finding 2).
- **More monitoring.** Admins' monitoring rules (Administration, Monitoring) and each person's switch for assignment notifications are built (`AGENTS.md`, Monitoring). Still to do:
  - **An overdue event,** for rules and for the assignees. It isn't a change anyone makes, so it needs a scheduled job that runs once across both API copies (a lease, or a Kubernetes CronJob), and a record of which procedures it has already reported.
  - **Monitor visit bypasses:** an event for an admin finishing a stage without the visit it required (`docs/calendar-plan.md`, decision 24), so a rule can watch for it, with its own `NotificationKind` (18). Staff calendars added the bypass after #84's eight events, and it isn't one of them yet.
  - **Audit the rules** (who added, changed or deleted which rule), and let a staff member, or the DPO, see the rules that name them (DPIA risk 24).
  - **Tidy up with accounts:** deleting an account should delete the rules it owns and the rules that name it, and its notification choices (`docs/governance/retention-schedule.md`). Until then they stay, and send nothing.
  - **More people in the picker:** the Monitoring page offers the first 100 accounts by username (the API's largest page), and says so under the field when there are more. A searchable picker, on `api/auth/users/search`, would suit a bigger organisation.
  - **More choices for each person** (Account Settings), once there are more kinds staff receive (the three appointment kinds, booked, rescheduled and cancelled, can't be switched off yet); and rules an admin sets for someone else (for example a team lead's), kept apart from that person's own choices so they can't undo them.
- **More from email** (`feature/email-2fa` built the email service, optional staff addresses, "Forgot your password?" and security notices). Still to do:
  - **Notification emails,** opt-in per person. They must never carry free text that may hold clinical information (a notification's title is a procedure's title), so send "You've been assigned a procedure" with a link, not its title.
  - **A retry for failed emails.** The queue is in memory and best effort: a message that fails, or is still queued when the API stops, is lost (logged). Every email today is a link or a notice that can be asked for again; a durable outbox (a table) would be needed before sending anything that can't.
  - **Share the reset request counts between API copies** (Valkey), as for sign-in throttling, and the per-account code and address-change limits and the hourly email cap (`AccountAttemptLimiter`, `EmailBudget`), which each copy also counts on its own.
  - **Wrap the data protection keys without Azure** (`ProtectKeysWithCertificate`, a certificate from a Secret, in the chart), so a cluster without Key Vault doesn't store them unwrapped beside the security stamps; today the API and `NOTES.txt` only warn (review of #86, finding 9).
  - **The page server in a Deployment of its own,** whose network policy allows only the API: on Azure it shares the API's pod, so it can reach the mail relay, the Key Vault and Microsoft Entra ID (allowed by name), which are shared services (review of #86, finding 4; threat model risk 5).
- **Two-factor authentication: what `feature/email-2fa` left.**
  - **Hash recovery codes** (ASVS 6.5.2): Identity stores them in clear in `AspNetUserTokens`; a custom `IUserTwoFactorRecoveryCodeStore` could keep salted hashes. Encrypting authenticator keys at rest (data protection) is the same kind of change.
  - **Two-factor for everyone,** not just admins, as a setting (and on by default for new deployments), then **passkeys** (.NET 10 Identity) and single sign-on (roadmap step 4).
  - **A written procedure for resetting someone's two-factor** (how an admin checks who's asking), for deploying organisations (ASVS 6.4.4).
  - **The first admin set up with two-factor** at their first sign-in when the requirement is on (today they're held to set it up, like any admin).
  - **In-app notifications to admins** when an admin turns two-factor on, off or has it reset (today only by email, to admins with a confirmed address).
- **File attachments: what's left.** Attachments are built (`AGENTS.md`, Attachments). Still to do:
  - **A real malware scanner.** Every file is "Not scanned" today (`NoMalwareScanner`). On Azure, turn on Microsoft Defender for Storage's malware scanning for the attachments account (about £8 a month per account plus about 12p per GB scanned; `deploy/azure/README.md`), and have the API read its verdict (the blob index tag it writes, or its Event Grid event) into `ScanStatus`, holding downloads until a file is scanned. Elsewhere (kind, other hosts), a ClamAV container behind `IMalwareScanner`. Decide what staff see while a scan is pending.
  - **Purging deleted files' bytes** after a retention window (proposed: 30 days, for the DPO to decide; `docs/governance/retention-schedule.md`), which needs a scheduled job that runs once across the API copies, as overdue notifications do. Until then, deleted files' bytes are kept. The `DeletedAtUtc` index is there for it. Restoring a deleted file (admins) could come with it.
  - **The files themselves in the subject access export.** The export lists every file's details (deleted ones too), but not its contents; add them, for example as a ZIP beside the export's JSON, streamed from the file store and recorded as one export. Anonymisation already renames and deletes the files; their bytes go with the purge.
  - **Storage hardening on Azure:** a customer-managed key, and passwordless access (workload identity) instead of the account key in Key Vault, which needs the API's network policy to reach Entra ID (`deploy/azure/README.md`, "Not yet").
  - **Previews:** HEIC/HEIF thumbnails (SkiaSharp's Linux build can't decode HEIC), and a safe PDF preview (rendered to an image on the server, never shown inline). Today both are download only.
  - **Longer lists:** a list shows the newest 200 files of a patient, procedure or stage and says how many there are; page it if real use gets near that.
  - **E2E tests:** the `e2e` job runs without file storage, so no journey uploads a file. Add Azurite to it and a journey (attach, download, delete).
  - **Snapshots:** `medapp-dev`'s snapshots and demo data cover only the database, not Azurite's blobs.
  - **Check the storage connection string at startup.** A malformed attachments connection string (a mistyped Azurite key, say) isn't noticed until the first upload, which then fails with a 500 (a `FormatException` from `BlobServiceClient`). Validate it when the API starts, so a bad deploy fails at once with a clear message.
- **A customisable home screen.** Let each person arrange their home screen from "widgets" they choose, move and resize: for example today's calendar, My Tasks, overdue work, recent notifications, and the admin insights charts. The layout is saved with their account, like their other settings, and admins can set a default for everyone. Moving and resizing needs a keyboard and click alternative to dragging (WCAG 2.5.7).
- **Find accounts still using `DefaultPassword1`.** Accounts created before #53 got that published password, and may still have it; until this check exists, an admin should reset their passwords or ask their users to change them. Add a one-off check that tests each account's hash with Identity's `PasswordHasher.VerifyHashedPassword` (no side effects, and SQL can't do it because the hashes are salted), then for each match sets a generated password (or locks it), revokes its sessions and lists the usernames. As a `medapp-dev` tool for local databases, and as an admin report or API command (like `create-first-admin`) for deployed ones (PR #53 review, finding 1).
- **Refuse to delete an account with open work** (clinical hazard H-21, before first live use). Deleting a leaver's account leaves their open procedures assigned to "Deleted user", on nobody's task list. List the account's open assignments and refuse the delete until they're reassigned, as it already refuses an account with upcoming bookings. Deactivating accounts instead of deleting them (roadmap step 4) is the longer-term fix.
- **Show started work on My Tasks** (clinical hazards H-04 and H-05, before first live use). My Tasks opens on "Not started", so work with a stage already started, overdue or not, stays hidden until the filter is changed. Show a count of started and overdue work beside the filter, or open it on all open work.

## Privacy (the rest of roadmap step 7)

The first part is built: the activity log and access report, the subject access export, restricted and disputed flags, anonymisation, the retention review, and clearing browser storage at sign-out (`AGENTS.md`, Activity log and patient privacy). Still to do, roughly in order of need (`docs/governance/README.md` has the full list):

- **Automatic purging of operational data:** sessions and sign-in events at 12 months, notifications at 6 months, as a job that runs once across the API copies, logging counts. The activity log's own retention: its sign-in entries at 12 months, its patient entries with the patient's record.
- **Note versions** in their own table, with the procedure audit pointing at them instead of copying notes into `Details` (DPIA risk 4), and the old values of patient edits.
- **A tamper-evident activity log:** ledger tables, and streaming the log to the log workspace (step 5).
- **Access reviews and alerts:** a periodic review of who can sign in, an alert when one account opens an unusual number of records, a "what has this person seen" report, and an `Auditor` role.
- **The browser's address for pages rendered on the server:** requests from the browser already record its address (the chart turns on forwarded headers for the ingress), but a page rendered on the server calls the API from Node, so its reads record the loopback address. Pass the browser's address through server rendering, trusted only with the page server's key.
- **A scheduled job for operational data:** purge sign-in events (including refused sign-ins) and sessions at 12 months and notifications at 6 months, once across the API copies (a lease or a Kubernetes CronJob), logging counts; the activity log's patient entries go with the patient's record. There is no scheduled job in MedApp yet.
- **Restriction hiding patients from lists** except for named roles; a second person approving an anonymisation.
- **A clinician review step in the export,** to hold back what meets the serious harm test, and a PDF.
- **Count appointments in the retention review:** the conclusion of treatment should be the latest of the last closed procedure, the last appointment attended, not attended or cancelled, and the last change to the record, and wait while any appointment is Requested or Booked (`docs/governance/retention-schedule.md`, section 2). Today appointments don't move it.
- **Retention classes per template** and per customer, the conclusion of treatment on the patient's page, destruction with a destruction log, and legal hold.
- **Saved searches that name a patient,** removed on anonymisation or destruction: their terms can be a name or an NHS number (the patient list's NHS number filter), kept in the account's settings and the browser. Anonymisation removes the number from the record but not from saved searches (DPIA risk 21).
- **Archiving in place of deleting patients,** if the product owner decides to (kept as it is for now).
- **Contact preferences** and a National Data Opt-out flag.
- **Always Encrypted for the NHS number** (deterministic, so lookups by exact value still work), which step 7 planned once the number existed. It exists now (branch `feature/nhs-integrations`), protected only by TDE like the rest. Its partial-match search (the list's NHS number filter, which matches numbers containing the digits typed) would have to become an exact match.

## Patient identity and interoperability (the rest of roadmap step 9's first part)

Built on branch `feature/nhs-integrations` (`AGENTS.md`, NHS number, FHIR and SNOMED CT): an optional, validated, unique NHS number; the patient banner; a read-only FHIR R4 Patient API in the UK Core shape; SNOMED CT codes on stage definitions; and a demographics lookup with only an offline stub. Still to do:

- **The real PDS lookup.** Wire `IPersonalDemographicsService` to the PDS FHIR API ([NHS England](https://digital.nhs.uk/developer/api-catalogue/personal-demographics-service-fhir)). It needs NHS England's onboarding first ([onboarding process](https://digital.nhs.uk/developer/guides-and-documentation/onboarding-process)): a CIS2 integration for health workers' access, or an application-restricted API key through the API platform; a DSPT submission; a DCB0129 hazard log; risk logs for each access mode; a penetration test; and a connection agreement. Then trace numbers, so the FHIR API can report them as verified, not "02 Number present but not traced". Update the data inventory, DPIA and ROPA first: NHS England becomes a source of patient data.
- **Validate the FHIR API against the UK Core profiles** with a FHIR validator (the HL7 validator, or Firely's), in CI, before any system relies on it. The resources are hand-built and haven't been checked yet.
- **FHIR access for other systems:** OAuth 2.0 client credentials (SMART Backend Services), with a client per system and its own scopes, instead of a signed-in member of staff's session cookie. The activity log then records the client, not a person.
- **More FHIR resources:** the procedure as a CarePlan or ServiceRequest, stages as Tasks, templates as PlanDefinitions, the audit as AuditEvents (the minimal CapabilityStatement at `api/fhir/metadata` lists only Patient today); webhooks for status changes; a published canonical URL for MedApp's record-flag code system (`urn:medapp:fhir:CodeSystem:record-flag`, today). Revisit the Firely SDK in place of hand-built DTOs then.
- **SNOMED CT on templates and OPCS-4 codes,** as step 9 plans (a procedure concept on each template), and the codes in the FHIR resources once procedures are exposed.
- **A terminology server** (the NHS England Terminology Server) to check that a SNOMED CT concept exists and is active, and to offer its preferred term, instead of the format check and the admin's own term. Inactive concepts need a periodic check too.
- **A duplicate-patient check** when a patient is added or edited: today only an exact duplicate NHS number is refused. Warn about the same name and date of birth, or a near match, and let an admin merge duplicates.
- **The rest of hazards H-01 and H-02's controls** (`docs/clinical-safety/hazard-log.md`): the date of birth beside the patient's name in task and procedure lists; the patient's name in the procedure page's title; the patient named in the Save Notes and Finish Stage confirmations. Then the CSO verifies the banner and these together.
- **Sex and gender on the patient and in the banner:** the NHS patient banner guidance shows them, and the banner can't until MedApp holds them (the NHS Data Dictionary's "person stated gender" and sex at birth, with the care that data needs). Then add them to the banner and the FHIR Patient (`gender`), and update the data inventory and DPIA first.
- **Checking NHS numbers against the issued ranges,** after the placeholder check: possible, but would refuse some valid numbers from the rest of the UK, so only as an option.
- **The rest of step 9's patient details:** a local hospital number (with its assigning organisation), address, phone, registered GP practice (ODS code), and a deceased flag. Each one updates the data inventory and the DPIA first.
- **NHS number unique within an organisation,** not the deployment, with multi-tenancy (step 8).
- **Keep the NHS number out of URLs,** where request logs, traces and the ingress's access logs can record it: the FHIR search takes it in its query string. Support FHIR's `POST Patient/_search`, which keeps the parameters in the body (the demographics lookup is already a POST). The patient list's NHS number filter stays in the URL by the product owner's decision on list filters (DPIA risk 10).

## Deployment

- **Kubernetes, with Helm, Argo CD and Azure.** Run MedApp on Kubernetes, step by step. Done so far:
  1. A local **kind** cluster: `deploy/kind`.
  2. A **Helm chart**: `deploy/helm/medapp`, which kind deploys with.
  3. **Security**: secrets from a secret store (External Secrets), TLS (cert-manager), network policies, the restricted Pod Security Standard, read-only filesystems, health checks on their own port, and a deliberate first admin. See the chart's `README.md`.
  4. **Argo CD**: `deploy/argocd`. Each merge publishes the image to GHCR and records the commit, with its image tag, on `env/kind`, which Argo CD deploys to kind, within the limits of the `medapp` project. Roll back by reverting on `main`.
  5. **Azure**, written but not deployed: `deploy/azure` (Bicep for AKS, Azure SQL, Key Vault and a container registry, then the cluster's setup). Its `README.md` says how to deploy it and what it costs. CI renders the cluster services' values and builds the image, as Publish does.

  Still to do:

  - **Deploy to Azure** for real, then fix what the first run turns up.
  - **A low-cost Azure demo deployment,** alongside the full one, to show every feature as cheaply as possible. It's never sold, so it can drop the production extras: one small node (or Azure Container Apps) instead of a multi-node AKS cluster, the cheapest Azure SQL tier, no private networking, and single copies. The full setup stays for local development (kind) and for a real deployment.
  - **Passwordless Azure SQL**, a least-privilege database user, private endpoints for the registry and Key Vault, and monitoring. See `deploy/azure/README.md`, "Not yet".
  - **A MediatR licence** before running in production: MediatR 14 logs a licence warning at startup.
  - **Readiness that checks the database and Valkey** (the chart's `README.md`, "Not yet").
  - **Restarting the API and Valkey when a password changes,** for example with a reloader. Until then it's by hand (`deploy/azure/README.md`, Passwords).
  - **Pinning images by digest** (the Dockerfile's base images, SQL Server and Valkey), with Dependabot keeping them current. Actions are pinned by commit SHA. Write the Dockerfile's base image tags out (not as `ARG`s), so Dependabot can read them.
  - **Verify the image's provenance attestation before deploying it** (an admission policy, or a check in the deploy job), and copy the attestations to Azure's registry with the image.
  - **Move the PR workflow's `pull-requests: write`** from the whole workflow to the jobs that comment.
  - **Repository settings:** `web`, `helm`, `unit-tests`, `image`, `integration-tests` and `e2e` are required on `main`, and `env/*` can't be deleted or force-pushed. The dependency graph, Dependabot alerts and Dependabot security updates are on (Settings, Advanced Security; done in #67, which `dependency-review` needs), with secret scanning and push protection; CodeQL's default setup stays off, so `codeql.yml`'s advanced setup can upload. Make `dependency-review` a required check too. Still to do: let only a deploy-only GitHub App push to `env/*` (a personal repository can't make GitHub Actions the only pusher).
- **An audit index for Insights' stage times.** Add an index on the audit log's `(PatientProcedureId, Action, AtUtc)`, including `StageId`, in a migration of its own (#66 left it out while two other branches were adding migrations). Each stage's first finish is a `TOP(1)` over its procedure's entries; today that seeks `(PatientProcedureId, AtUtc)` and looks up every entry to check its action. Measured on LocalDB with 20,000 procedures and 342,627 audit entries (the second of two runs), the whole Insights query took 239 ms, 495 ms and 1,025 ms for 4, 12 and 26 weeks; with the index, 189 ms, 209 ms and 516 ms. Stage times are most of it, and grow with the stages finished in the period (PR #66 review, finding 4).

## Security (roadmap step 2, after `feature/security-baseline`)

What that branch left, so the app would work exactly as before (`docs/security/asvs.md` has the rest, requirement by requirement):
- **An idle timeout** (30 minutes as a start, configurable) beside the 8 hours (extendable to 24 since #63), with its own warning.
- **Store a hash of a random 256-bit session token** instead of the row's ID (needs a migration), and stop repeating the session ID in the login response's body.
- **The same answer for a locked account** as for a wrong password (today a 423 confirms the account exists). The second sign-in step answers a locked account with a 423 too.
- **Close a user's live-notifications connections when their sessions end** (a password change, a reset or "Sign out everywhere"): today an open page keeps receiving notifications until it reloads.
- **Share the sign-in throttling counts between API copies** (Valkey), and add a per-session limit on everything else.
- **Passwords:** a breached-password check (Have I Been Pwned's k-anonymity range API, or an offline list), a 15-character minimum without composition rules (ASVS V6.2.1), and context words (the username, "medapp") on the deny list.
- **Tighten the page policy:** move style attributes to classes so `style-src` can drop `'unsafe-inline'`, and collect violation reports (`report-to`).
- **Audit the new session actions** (sign-out everywhere, and the sessions a change ends) with the rest of account administration (step 7).

## Testing

- **Run the patient timeline's query on SQL Server,** once `MedApp.IntegrationTests` has a database (#65): page through entries that share a time (SQL Server orders `uniqueidentifier`s its own way, and the cursor must agree), and filter by kind (its `LIKE` markers ignore case there). Today it's only proven to translate (`PatientTimelineTranslationTests`) and run in memory (PR #64 review, finding 6).

## Long term

- **Make MedApp a platform that companies build their own app on.**
  - **The aim:** turn MedApp into a generic engine a company can shape into its own version.
  - **What they could customise:**
    - **The structure:** what the main records are called and what fields they have, for example Patient becoming Client, Employee or Animal.
    - **The workflows:** their own templates and stages.
    - **The pages and navigation.**
  - **What MedApp provides:** the build and the hosting, using the existing image, Helm chart and Azure setup for each company. Decide between a deployment for each company and many companies in one (multi-tenancy, roadmap step 8).
  - **Today's medical app** becomes one ready-made pack among others.
  - **What it needs first:** multi-tenancy, so each company's data stays separate inside one deployment.
