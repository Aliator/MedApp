# MedApp: to do

Smaller improvements to pick up later, outside the phased plans (`docs/procedures-ui-plan.md`). Move an item into a plan or a branch when it's started, and delete it here when it's merged.

## Accessibility

The axe audit (`npm run a11y` in `tools/ui-parity`: every screen in dark, light and a colour theme, WCAG 2.2 A and AA) passes, and the Accessibility card has the text size, the hover delay and the switches in `src/app/accessibility.tsx`. The statement is at `/accessibility`. Still to do:

- **Screen reader pass by hand.** NVDA and VoiceOver through the main tasks (find a patient, work a stage, finish a procedure), and keyboard-only use of every dialog: focus trapped, and returned on close.

## Features

- **Force a password change at first sign-in** (a must-change flag on the account; needs a migration). A generated first password is shown once, but works until the user changes it.
- **Find accounts still using `DefaultPassword1`.** Accounts created before #53 got that published password, and may still have it; until this check exists, an admin should reset their passwords or ask their users to change them. Add a one-off check that tests each account's hash with Identity's `PasswordHasher.VerifyHashedPassword` (no side effects, and SQL can't do it because the hashes are salted), then for each match sets a generated password (or locks it), revokes its sessions and lists the usernames. As a `medapp-dev` tool for local databases, and as an admin report or API command (like `create-first-admin`) for deployed ones (PR #53 review, finding 1).
- **`medapp-dev`'s `create_user` with a chosen password can leave a half-made account.** It creates the account with a generated password, then resets it to the chosen one and adds the roles; a failure after the create leaves an account nobody can sign in to, with no roles. Delete the account when a later step fails, or create it with the chosen password in one step (PR #53 review, finding 5).
- **The same duplicate label on three more lists.** Stage definitions and Templates open on Active, and the procedures list can open on Assigned to me: each shows that filter as a token in the bar and again as a blue label beside the favourites star. My Tasks turns its quick filters' names off (`namesQuickFilters`). Either do the same on those lists, or have `ListFavourites` leave out a quick filter's name whenever its one token already says it, and drop the switch (PR #55 review, finding 3).
- **More notifications.** The bell (beside MedApp) and live delivery are in place, for being assigned a procedure. Add more kinds through `INotificationSender`: one of yours overdue or blocked, a stage unlocked, and optionally an email for each.

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
