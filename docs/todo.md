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

- **Kubernetes, with Helm, Argo CD and Azure.** Run MedApp on Kubernetes, step by step:
  1. Try it locally first with **kind** (Kubernetes in Docker): the API's image (`MedApp.API/Dockerfile`: the API, the built React app and Node to render its pages) and SQL Server in a local cluster.
  2. A **Helm chart** for the deployment: the API, its config and secrets, the database (or an external one), ingress, and the EF migrations as a job.
  3. **Proper security**: secrets in a secret store (not in values files), TLS, network policies, non-root containers with read-only filesystems, resource limits, and the session cookie and health endpoints set up for running behind an ingress.
  4. **Argo CD** for continuous delivery: the cluster follows the chart and its values in git.
  5. **Azure** for hosting: AKS, Azure SQL, Key Vault for the secrets, and a container registry.
