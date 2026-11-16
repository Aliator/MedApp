# MedApp: to do

Improvements and features to pick up later, outside the phased plans (`docs/procedures-ui-plan.md`). Move an item into a plan or a branch when it's started, and delete it here when it's merged.

## Accessibility

The axe audit (`npm run a11y` in `tools/ui-parity`: every screen in dark, light and a colour theme, WCAG 2.2 A and AA) passes, and the Accessibility card has the text size, the hover delay and the switches in `src/app/accessibility.tsx`. The statement is at `/accessibility`. Still to do:

- **Three colour themes just under 4.5:1 on secondary text.** Measured against the secondary text colour (subtitles, Back, descriptions), Y2K's light page is 4.19:1, and the header bars of Chocolate's light cards and Butter's dark cards 4.41:1. The custom colour check (`textContrast`) now counts secondary text; these themes were left as they are so they don't change how they look. Decide whether to adjust them (a test lists them in `src/app/colours.test.tsx`).
- **Screen reader pass by hand.** NVDA and VoiceOver through the main tasks (find a patient, work a stage, finish a procedure), and keyboard-only use of every dialog: focus trapped, and returned on close.

## Features

- **An admin insights page.** An overview for admins of the procedures that are overdue or in progress, with graphs, to keep an eye on the work: (Roadmap step 9 has a version of this.)
  - KPI tiles: open, overdue, in progress, completed this week
  - overdue work by assignee and by priority
  - the time spent in each stage, to show the bottlenecks (the audit log already records when each stage starts and finishes)
  - created against completed over time
- **Appointments and booking.** Book appointments for patients with staff. Each member of staff gets a calendar (day and week) showing their appointments with their procedures' due dates. Appointments tie into the workflow: booking the next stage, or a follow-up when a procedure finishes.
- **Notification settings for each person.** Admins choose which events notify each member of staff (for example, a procedure finished, a stage unlocked, something overdue or blocked), separately for each person. Only "assigned to a procedure" exists today, so it needs those new notification kinds too, and a per-user setting checked before each one is sent. Overdue notifications need a scheduled job that runs once across both API copies. Store the admins' choices apart from each person's own settings, so nobody can undo them.
- **Emails, and resetting your own password.** Send emails (notifications, a welcome email for a new account), and let people reset a forgotten password by an emailed link instead of asking an admin. It needs an email service and single-use, expiring reset links. ASP.NET Identity's password reset tokens fit. Staff accounts have no email address yet, so add one. Reset links need data protection keys shared by every API copy (roadmap step 2). Emails must never carry free text that may hold clinical information (a notification's title is a procedure's title), and a welcome email never carries the password. Roadmap step 4 plans the reset.
- **File attachments.** Attach files (scans, photos, results, letters) to a patient, a procedure or a stage, by dragging them in, with a preview and a download. Adding or removing a file is audited. It needs file storage (Azure Blob Storage when deployed) and limits on file types and sizes. Serve files safely: download by default, a strict content type and `nosniff`, and never render an uploaded SVG or HTML inline on the app's own origin. Scan for malware, audit downloads too, and offer a button as well as dragging (WCAG 2.5.7). Roadmap step 9 has a version.
- **A customisable home screen.** Let each person arrange their home screen from "widgets" they choose, move and resize: for example today's calendar, My Tasks, overdue work, recent notifications, and the admin insights charts. The layout is saved with their account, like their other settings, and admins can set a default for everyone. Moving and resizing needs a keyboard and click alternative to dragging (WCAG 2.5.7).
- **Find accounts still using `DefaultPassword1`.** Accounts created before #53 got that published password, and may still have it; until this check exists, an admin should reset their passwords or ask their users to change them. Add a one-off check that tests each account's hash with Identity's `PasswordHasher.VerifyHashedPassword` (no side effects, and SQL can't do it because the hashes are salted), then for each match sets a generated password (or locks it), revokes its sessions and lists the usernames. As a `medapp-dev` tool for local databases, and as an admin report or API command (like `create-first-admin`) for deployed ones (PR #53 review, finding 1).

## Privacy (the rest of roadmap step 7)

The first part is built: the activity log and access report, the subject access export, restricted and disputed flags, anonymisation, the retention review, and clearing browser storage at sign-out (`AGENTS.md`, Activity log and patient privacy). Still to do, roughly in order of need (`docs/governance/README.md` has the full list):

- **Automatic purging of operational data:** sessions and sign-in events at 12 months, notifications at 6 months, as a job that runs once across the API copies, logging counts. The activity log's own retention: its sign-in entries at 12 months, its patient entries with the patient's record.
- **Note versions** in their own table, with the procedure audit pointing at them instead of copying notes into `Details` (DPIA risk 4), and the old values of patient edits.
- **A tamper-evident activity log:** ledger tables, and streaming the log to the log workspace (step 5).
- **Access reviews and alerts:** a periodic review of who can sign in, an alert when one account opens an unusual number of records, a "what has this person seen" report, and an `Auditor` role.
- **The client's address behind proxies:** record the browser's address, not the ingress's or the page server's, by trusting forwarded headers from the ingress and passing the address through server rendering.
- **Restriction hiding patients from lists** except for named roles; a second person approving an anonymisation.
- **A clinician review step in the export,** to hold back what meets the serious harm test, and a PDF.
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
  - **Readiness that checks the database and Valkey,** and **restarting Node** when the page server exits (the chart's `README.md`, "Not yet").
  - **Restarting the API and Valkey when a password changes,** for example with a reloader. Until then it's by hand (`deploy/azure/README.md`, Passwords).
  - **Pinning actions and images by digest,** with Dependabot keeping them current.
  - **Repository settings:** `web`, `helm`, `unit-tests` and `image` are required on `main`, and `env/*` can't be deleted or force-pushed. Still to do: let only a deploy-only GitHub App push to `env/*` (a personal repository can't make GitHub Actions the only pusher).
- **Move the page server out of `wwwroot`** (e.g. to `/app/ssr/`), so static files can't reach `ssr/` or `.vite/` on any host. On Windows, `PageServerFiles`' path check can be got round with `\`, trailing dots or 8.3 short names (PR #51 review, 3.2).

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
