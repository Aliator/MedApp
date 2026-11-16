# MedApp: to do

Smaller improvements to pick up later, outside the phased plans (`docs/procedures-ui-plan.md`). Move an item into a plan or a branch when it's started, and delete it here when it's merged.

## Accessibility

The axe audit (`npm run a11y` in `tools/ui-parity`: every screen in dark, light and a colour theme, WCAG 2.2 A and AA) passes, and the Accessibility card has the text size, the hover delay and the switches in `src/app/accessibility.tsx`. The statement is at `/accessibility`. Still to do:

- **Screen reader pass by hand.** NVDA and VoiceOver through the main tasks (find a patient, work a stage, finish a procedure), and keyboard-only use of every dialog: focus trapped, and returned on close.
- **Warn on unreadable custom colours.** On the Colours page, warn when a custom page or card colour leaves text under 4.5:1 (the colour themes all pass).
- **More announcements.** Announce a saved form and a finished stage, as lists already announce their results.
- **A save shortcut.** Ctrl+S (or Cmd+S) to save the form that's open, listed in the shortcuts dialog.
- **Session timeout warning.** Warn before a session ends, with time to extend it (WCAG 2.2.1), instead of failing the next action.

## Features

- **An admin insights page.** An overview for admins of the procedures that are overdue or in progress, with graphs, to keep an eye on the work:
  - KPI tiles: open, overdue, in progress, completed this week
  - overdue work by assignee and by priority
  - the time spent in each stage, to show the bottlenecks (the audit log already records when each stage starts and finishes)
  - created against completed over time
- **Appointments and booking.** Book appointments for patients with staff. Each member of staff gets a calendar (day and week) showing their appointments with their procedures' due dates. Appointments tie into the workflow: booking the next stage, or a follow-up when a procedure finishes.
- **A patient timeline tab.** On the patient page: one feed, grouped by day, of everything that happened to that patient across their procedures:
  - what's shown: procedures started, stages finished, notes, assignments, blocks and unlocks
  - filters by type and by procedure
  - what's coming up, at the top

  Built from the audit log.
- **Notification settings for each person.** Admins choose which events notify each member of staff (for example, a procedure finished, a stage unlocked, something overdue or blocked), separately for each person. Only "assigned to a procedure" exists today, so it needs those new notification kinds too, and a per-user setting checked before each one is sent.
- **Emails, and resetting your own password.** Send emails (notifications, a welcome email for a new account), and let people reset a forgotten password by an emailed link instead of asking an admin. It needs an email service and single-use, expiring reset links. ASP.NET Identity's password reset tokens fit.
- **File attachments.** Attach files (scans, photos, results, letters) to a patient, a procedure or a stage, by dragging them in, with a preview and a download. Adding or removing a file is audited. It needs file storage (Azure Blob Storage when deployed) and limits on file types and sizes.
- **Force a password change at first sign-in** (a must-change flag on the account; needs a migration). A generated first password is shown once, but works until the user changes it.
- **Find accounts still using `DefaultPassword1`.** Accounts created before #53 got that published password, and may still have it; until this check exists, an admin should reset their passwords or ask their users to change them. Add a one-off check that tests each account's hash with Identity's `PasswordHasher.VerifyHashedPassword` (no side effects, and SQL can't do it because the hashes are salted), then for each match sets a generated password (or locks it), revokes its sessions and lists the usernames. As a `medapp-dev` tool for local databases, and as an admin report or API command (like `create-first-admin`) for deployed ones (PR #53 review, finding 1).
- **`medapp-dev`'s `create_user` with a chosen password can leave a half-made account.** It creates the account with a generated password, then resets it to the chosen one and adds the roles; a failure after the create leaves an account nobody can sign in to, with no roles. Delete the account when a later step fails, or create it with the chosen password in one step (PR #53 review, finding 5).
- **The same duplicate label on three more lists.** Stage definitions and Templates open on Active, and the procedures list can open on Assigned to me: each shows that filter as a token in the bar and again as a blue label beside the favourites star. My Tasks turns its quick filters' names off (`namesQuickFilters`). Either do the same on those lists, or have `ListFavourites` leave out a quick filter's name whenever its one token already says it, and drop the switch (PR #55 review, finding 3).

## Deployment

- **Kubernetes, with Helm, Argo CD and Azure.** Run MedApp on Kubernetes, step by step. Done so far:
  1. A local **kind** cluster: `deploy/kind`.
  2. A **Helm chart**: `deploy/helm/medapp`, which kind deploys with.
  3. **Security**: secrets from a secret store (External Secrets), TLS (cert-manager), network policies, the restricted Pod Security Standard, read-only filesystems, health checks on their own port, and a deliberate first admin. See the chart's `README.md`.
  4. **Argo CD**: `deploy/argocd`. Each merge publishes the image to GHCR and records the commit, with its image tag, on `env/kind`, which Argo CD deploys to kind, within the limits of the `medapp` project. Roll back by reverting on `main`.
  5. **Azure**, written but not deployed: `deploy/azure` (Bicep for AKS, Azure SQL, Key Vault and a container registry, then the cluster's setup). Its `README.md` says how to deploy it and what it costs. CI renders the cluster services' values and builds the image, as Publish does.

  Still to do:

  - **Deploy to Azure** for real, then fix what the first run turns up.
  - **Passwordless Azure SQL**, a least-privilege database user, private endpoints for the registry and Key Vault, and monitoring. See `deploy/azure/README.md`, "Not yet".
  - **A MediatR licence** before running in production: MediatR 14 logs a licence warning at startup.
  - **Readiness that checks the database and Valkey,** and **restarting Node** when the page server exits (the chart's `README.md`, "Not yet").
  - **Restarting the API and Valkey when a password changes,** for example with a reloader. Until then it's by hand (`deploy/azure/README.md`, Passwords).
  - **Pinning actions and images by digest,** with Dependabot keeping them current.
  - **Repository settings:** `web`, `helm`, `unit-tests` and `image` are required on `main`, and `env/*` can't be deleted or force-pushed. Still to do: let only a deploy-only GitHub App push to `env/*` (a personal repository can't make GitHub Actions the only pusher).
- **Move the page server out of `wwwroot`** (e.g. to `/app/ssr/`), so static files can't reach `ssr/` or `.vite/` on any host. On Windows, `PageServerFiles`' path check can be got round with `\`, trailing dots or 8.3 short names (PR #51 review, 3.2).

- **A customisable home screen.** Let each person arrange their home screen from "widgets" they choose, move and resize: for example today's calendar, My Tasks, overdue work, recent notifications, and the admin insights charts. The layout is saved with their account, like their other settings, and admins can set a default for everyone.

## Long term

- **Make MedApp a platform that companies build their own app on.**
  - **The aim:** turn MedApp into a generic engine a company can shape into its own version.
  - **What they could customise:**
    - **The structure:** what the main records are called and what fields they have, for example Patient becoming Client, Employee or Animal.
    - **The workflows:** their own templates and stages.
    - **The pages and navigation.**
  - **What MedApp provides:** the build and the hosting, using the existing image, Helm chart and Azure setup for each company.
  - **Today's medical app** becomes one ready-made pack among others.
  - **What it needs first:** multi-tenancy, so each company's data stays separate inside one deployment.
