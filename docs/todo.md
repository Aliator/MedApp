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

- **Rich text notes.** Let stage notes (and other long text, like stage instructions) have formatting: bold, italics, lists and links, with a small toolbar. Store them safely (sanitised HTML or Markdown), show them formatted on the procedure page and read-only stages, and keep plain text readable for notes written before.
- **More notifications.** The bell (beside MedApp) and live delivery are in place, for being assigned a procedure. Add more kinds through `INotificationSender`: one of yours overdue or blocked, a stage unlocked, and optionally an email for each.
- **Server-side rendering.** Consider rendering pages on the server (e.g. TanStack Start, which the app's TanStack Router builds on) for a faster first paint. Weigh it first: the app sits behind sign-in, its data comes from the API per user, and it's served as static files from the API's `wwwroot` today, so SSR would need a Node server (or the API to host one) and the cookie session passed through.

## Deployment

- **Kubernetes, with Helm, Argo CD and Azure.** Run MedApp on Kubernetes, step by step:
  1. Try it locally first with **kind** (Kubernetes in Docker): the API (serving the built React app) and SQL Server in a local cluster.
  2. A **Helm chart** for the deployment: the API, its config and secrets, the database (or an external one), ingress, and the EF migrations as a job.
  3. **Proper security**: secrets in a secret store (not in values files), TLS, network policies, non-root containers with read-only filesystems, resource limits, and the session cookie and health endpoints set up for running behind an ingress.
  4. **Argo CD** for continuous delivery: the cluster follows the chart and its values in git.
  5. **Azure** for hosting: AKS, Azure SQL, Key Vault for the secrets, and a container registry.

## Backend

- **Decide how Domain entities are mapped to API responses, then apply it everywhere.**
  - **Today:** handlers map by hand, in three styles:
    - inline `new XResponse(...)` in each handler (Patients, Auth);
    - shared static mapper classes (`ProcedureMapper`, `StageDefinitionMapper`, `StageTemplateMapper` in the Procedures area);
    - helper methods in a few handlers.
  - **Options:**
    - **A mapping library,** such as Mapster (free) or AutoMapper (commercial licence from v15).
    - **Custom mappers everywhere:** one static mapper class per area, as Procedures does, and no library. Explicit and easy to test, so this is my recommendation. A library adds little where the responses compute things (Procedures' `CanEdit`, `IsOverdue`, `AllowedStatusChanges`), and those mappings would stay hand-written anyway.
  - **Either way:**
    - write it down in `AGENTS.md` (the "Mapping" section, which says there's no mapping library on `main`);
    - move the Patients and Auth handlers onto it;
    - add tests for each mapper.
