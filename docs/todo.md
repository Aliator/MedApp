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

- **More notifications.** The bell (beside MedApp) and live delivery are in place, for being assigned a procedure. Add more kinds through `INotificationSender`: one of yours overdue or blocked, a stage unlocked, and optionally an email for each.

## Deployment

- **Kubernetes, with Helm, Argo CD and Azure.** Run MedApp on Kubernetes, step by step. Done so far:
  1. A local **kind** cluster: `deploy/kind`.
  2. A **Helm chart**: `deploy/helm/medapp`, which kind deploys with.
  3. **Security**: secrets from a secret store (External Secrets), TLS (cert-manager), network policies, the restricted Pod Security Standard, read-only filesystems, health checks on their own port, and a deliberate first admin. See the chart's `README.md`.
  4. **Argo CD**: `deploy/argocd`. Each merge publishes the image to GHCR and merges the commit, with its image tag, into `env/kind`, which Argo CD deploys to kind.

  Still to do:

  5. **Azure** for hosting: AKS, Azure SQL, Key Vault for the secrets, and a container registry.
- **More than one copy of the API.** The chart runs one (`api.replicas`), because live notifications reach only the copy a browser's SignalR connection is on. Add a backplane (Azure SignalR Service, or Redis), then raise it.
