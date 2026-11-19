# UI parity screenshots

Captures the same screens in the React app (`MedApp.Web`) and, optionally, the deprecated Blazor app (`MedApp.Client.Deprecated`). It writes `report.html` with each pair side by side.
- During the migration (see `docs/react-migration-plan.md`), it compared the two apps by eye.
- Since cutover it captures React only, unless `BLAZOR_URL` is set. The last Blazor baseline stays in `screenshots/*/blazor.png` on the machine that took it, since screenshots are git-ignored.

**It is not an E2E test suite**: nothing is asserted, and it doesn't run in CI.

## One-time setup

1. Install Node.js, then run `npm install` and `npm run install-browser` (Chromium only) in this folder.
2. Create the tools' own logins with `medapp-dev`, then copy `.env.example` to `.env` (git-ignored) and put their passwords in it. They're separate accounts because each screen sets its saved settings (the app keeps settings with the account, and the server renders pages with them), so your own account's are never touched.
   - **The admin** (`PARITY_USERNAME`): `create_user`, username `dev-parity`, roles `["Admin"]`, and a password of your choosing. It reaches `/admin/users` and the setup pages. `reset_demo_data` removes it unless you pass `keepUsers: ["dev-parity"]`.
   - **Members of staff** (`PARITY_STAFF_*`, `PARITY_LAB_*`): admins are read only and clinical records sit behind team walls, so screens that need clinical access (`as: 'clinic'` or `as: 'lab'` in `screens.ts`) are captured as staff. `ensure_test_staff` with prefix `dev-parity` and a password makes `dev-parity-clinician` (Clinician and Booking, Dermatology's clinic admin) and `dev-parity-lab` (Lab and Pathologist, in the lab). The lab account is optional: its screens skip without it. Run `ensure_test_staff` again after `reset_demo_data`, which remakes the teams.

## Running it

Start the apps first, from the repo root (Docker must be running):

```bash
dotnet run --project MedApp.AppHost   # SQL container, API (https://localhost:7038), React app (http://localhost:5173)
```

Wait until the dashboard shows the API and the React app as running. If either fails with "the default developer certificate could not be found", run `dotnet dev-certs https` once.

Then, in this folder:

| Command | Captures |
|---|---|
| `npm run parity` | Every app whose URL is set in `.env`: React, plus Blazor only when `BLAZOR_URL` is set |
| `npm run parity:blazor` | Blazor only |
| `npm run parity:react` | React only |
| `npm run parity:react -- -g "my-tasks\|patient-view"` | Only the named screens (a regex over the names in `screens.ts`); sign-in still runs first |
| `npx playwright test --project=blazor -g "patient-view"` | One screen |

Screenshots are desktop only (1440×900); mobile layouts are out of scope for now. They land in `screenshots/<screen>/{blazor,react}.png`, and `report.html` is regenerated after every run. Screens not yet captured for an app show as "missing".

## Accessibility audit

`npm run a11y` runs axe over every React screen in `screens.ts`, three times: dark mode, light mode, and light mode with the Butter colour theme, checking WCAG 2.2 A and AA. It takes about ten minutes. Each screen's results land in `a11y-results/`, and the run ends with a summary of each rule that failed, its screens and a few of its elements (also in `a11y-report.json`). Both are git-ignored. Add `-- -g "<screen>"` to check only some screens. It exits non-zero if anything failed. Like the screenshots, each screen gets exactly the settings it's checked with (`useSettings`), saved to the tools' account.

## Adding or changing a screen

Screens are listed in `screens.ts`. Each has a name (the folder), a route, and optional `prepare` steps, such as opening a dialog.
- Prefer role and label selectors (`getByRole`, `getByLabel`), so the same steps work in both apps while the labels match.
- Branch on `app` only where the two apps genuinely differ.
- Never confirm a destructive action: open the dialog, take the screenshot, and stop.

If a React screen needs a different loading, dialog or alert marker, extend `BUSY`, `DIALOG` or `ALERT` in `shared.ts`.
