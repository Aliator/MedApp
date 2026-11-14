# MedApp: to do

Smaller improvements to pick up later, outside the phased plans (`docs/procedures-ui-plan.md`). Move an item into a plan or a branch when it's started, and delete it here when it's merged.

## Users

- **Make sign-in count failed passwords and honour lockout.**
  - **Today:** `IdentityAuthenticationService.ValidateCredentialsAsync` checks the password with `CheckPasswordAsync`, which neither counts a wrong one nor checks whether the account is locked out. So the lockout settings (`Identity` in `appsettings.json`) have no effect on sign-in. Deleting a user (which asks for the admin's password) does count and honour them.
  - **Instead:** check `IsLockedOutAsync` first, count a wrong password with `AccessFailedAsync`, and clear the count on success (or use `SignInManager.CheckPasswordSignInAsync` with `lockoutOnFailure: true`). Say on the login page when an account is locked, and for how long.
  - **Tests:** handler and service tests for a locked account, a wrong password, and a right one.

## Account settings

More settings for Account Settings (`MedApp.Web/src/pages/account/AccountPage.tsx`), alongside the Account, Appearance and Accessibility cards. Browser-only settings live in `src/app/` providers, like `colours.tsx` and `textSize.tsx`.

- **Start page.** Which page opens after sign-in: Home (the default), My Tasks, Patients or Procedures.
- **List defaults.** Rows per page for every list (today a fixed 10, `PAGE_SIZE`), and whether the procedure lists start on "Assigned to me".
- **Date and time format.** e.g. 7 Nov 2026 or 07/11/2026, and a 12- or 24-hour clock (`src/utils/dates.ts` formats them all).
- **Density.** A compact option for tables and forms, to fit more on screen (Astryx tables and lists have a `density` prop).
- **Notifications.** An email when you're assigned a procedure, or when one of yours becomes overdue. Needs the API to send mail and store the choice per user.
- **Sessions.** See where you're signed in (device, browser, last used) and sign out other sessions. The API keeps a `UserSession` row per sign-in already.
- **Two-factor sign-in.** An authenticator app code at sign-in, set up from Account Settings (ASP.NET Core Identity supports TOTP).
- **Half view default.** Which page opens in a half view when none is chosen, or whether the half view opens on the left or the right.
- **Settings that follow you.** Keep colours, text size and the rest with the account (an API endpoint and a table), rather than in each browser, so they're the same on every computer.

## Accessibility

Account Settings has an Accessibility card with a text size setting so far (`src/app/textSize.tsx`). The aim is WCAG 2.2 AA throughout, and settings for people who need more. Each setting goes in that card, kept in this browser like the others.

- **Audit the app against WCAG 2.2 AA, and fix what it finds.**
  - **How:** run axe (`@axe-core/playwright`) over every UI parity screen, in light and dark mode and with each colour theme, then check by hand what tools can't: keyboard-only use of every page and dialog, a screen reader (NVDA and VoiceOver) through the main tasks, and 400% zoom (reflow, nothing cut off).
  - **Watch for:** colour contrast with the colour themes and custom colours (warn on the Colours page when a choice fails 4.5:1 for text), focus order and visible focus everywhere, dialogs that trap and return focus, and every icon button named.
- **High contrast mode.** A setting that swaps to a high-contrast palette: stronger borders, text at 7:1, no pale-on-pale tokens, and outlines on cards and inputs. Follow `prefers-contrast: more` by default.
- **Reduce motion.** A setting for no animation (folding cards, dialogs, list moves), following `prefers-reduced-motion` by default. Astryx themes support a reduced-motion mark (`Theme`'s `reducedMotion`).
- **Stronger focus indicators.** A setting for a thicker, high-contrast focus ring on every control, for people who navigate by keyboard.
- **Text spacing and font.** Line height, letter and word spacing (WCAG 1.4.12's values as the "more" option), and an optional dyslexia-friendly font (e.g. Atkinson Hyperlegible or OpenDyslexic).
- **Don't rely on colour alone.** Status, priority and overdue tokens, and validation errors, also differ by icon or text, so they read in greyscale and for colour-blind users. Optionally a colour-blind-safe palette for the tokens.
- **Underline links.** A setting that underlines every link, not only on hover.
- **Bigger targets.** A setting that raises every clickable control (table action buttons, nav buttons) to at least 44 by 44 pixels.
- **Keyboard shortcuts.** Documented shortcuts (go to Home, My Tasks, Patients, Procedures; open search; save a form), listed in a dialog on `?`, which can be turned off in the Accessibility card.
- **Screen reader announcements.** Announce what changes without a page load: a saved form, a finished stage, a list refreshed or filtered (the result count), a session about to end.
- **Session timeout warning.** Warn before a session ends, with time to extend it (WCAG 2.2.1), instead of failing the next action.
- **Accessibility statement.** A page saying what the app supports and how to report a barrier.

## UI polish

- **Expand the power search's favourites (the star beside a list's search).** Today it offers fixed quick look-ups (My Tasks: Not started, Started). Let people save the current search as a favourite of their own, name, rename and delete them, mark one as the list's starting search, and see which favourite (if any) the list is showing. Store them per user (with the account, so they follow you), and offer them on every list with a search (`ListFilterBar`'s `favourites`).
- **Cards instead of table rows when a list is too narrow.** When a list's columns don't fit at their minimum widths (a half view, or a narrow window), show each row as a small card in a list (its name, then the other fields under it, its actions at the end) instead of a table that scrolls sideways. `DataTable` already measures the room it has (`useFittedColumns`), so it can switch there.
- **Space between the Templates page's header controls.** Add a little padding between the "Show archived" switch and the New Template button (`TemplatesPage.tsx`), and check the Stages page's matching pair.

## Features

- **Rich text notes.** Let stage notes (and other long text, like stage instructions) have formatting: bold, italics, lists and links, with a small toolbar. Store them safely (sanitised HTML or Markdown), show them formatted on the procedure page and read-only stages, and keep plain text readable for notes written before.
- **Real-time notifications, with a bell beside MedApp.** A notification icon to the right of the MedApp heading in the side nav, with an unread count, opening a list of recent notifications (assigned a procedure, one of yours overdue or blocked, a stage unlocked). They arrive as they happen, without a reload: SignalR from the API (or server-sent events), with notifications stored per user so they're there after signing in. Ties in with the email option under Account settings.
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
