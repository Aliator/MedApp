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
  - **Pinning images by digest** (the Dockerfile's base images, SQL Server and Valkey), with Dependabot keeping them current. Actions are pinned by commit SHA. Write the Dockerfile's base image tags out (not as `ARG`s), so Dependabot can read them.
  - **Verify the image's provenance attestation before deploying it** (an admission policy, or a check in the deploy job), and copy the attestations to Azure's registry with the image.
  - **Move the PR workflow's `pull-requests: write`** from the whole workflow to the jobs that comment.
  - **Repository settings:** `web`, `helm`, `unit-tests` and `image` are required on `main`, and `env/*` can't be deleted or force-pushed. Still to do: let only a deploy-only GitHub App push to `env/*` (a personal repository can't make GitHub Actions the only pusher).
- **Move the page server out of `wwwroot`** (e.g. to `/app/ssr/`), so static files can't reach `ssr/` or `.vite/` on any host. On Windows, `PageServerFiles`' path check can be got round with `\`, trailing dots or 8.3 short names (PR #51 review, 3.2).

## Security (roadmap step 2, after `feature/security-baseline`)

What that branch left, so the app would work exactly as before (`docs/security/asvs.md` has the rest, requirement by requirement):
- **An idle timeout** (30 minutes as a start, configurable) beside the 8 hours (extendable to 24 since #63), with its own warning.
- **Store a hash of a random 256-bit session token** instead of the row's ID (needs a migration), and stop repeating the session ID in the login response's body.
- **The same answer for a locked account** as for a wrong password (today a 423 confirms the account exists).
- **Close a user's live-notifications connections when their sessions end** (a password change, a reset, a role change or "Sign out everywhere"): today an open page keeps receiving notifications until it reloads.
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
