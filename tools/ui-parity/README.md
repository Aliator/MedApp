# UI parity screenshots

Captures the same screens in the React app (`MedApp.Web`) and, optionally, the deprecated Blazor app (`MedApp.Client.Deprecated`). It writes `report.html` with each pair side by side.
- During the migration (see `docs/react-migration-plan.md`), it compared the two apps by eye.
- Since cutover it captures React only, unless `BLAZOR_URL` is set. The last Blazor baseline stays in `screenshots/*/blazor.png` on the machine that took it, since screenshots are git-ignored.

**It is not an E2E test suite**: nothing is asserted, and it doesn't run in CI.

## One-time setup

1. Install Node.js, then run `npm install` and `npm run install-browser` (Chromium only) in this folder.
2. Copy `.env.example` to `.env` (git-ignored). It signs in as the local test login `admin` / `admin`, the app's only Admin account. `medapp-dev` creates it (`ensure_test_admin`, and on every `reset_demo_data`). It needs to be an admin to reach `/admin/users`.

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
| `npx playwright test --project=blazor -g "patient-view"` | One screen |

Screenshots are desktop only (1440×900); mobile layouts are out of scope for now. They land in `screenshots/<screen>/{blazor,react}.png`, and `report.html` is regenerated after every run. Screens not yet captured for an app show as "missing".

## Adding or changing a screen

Screens are listed in `screens.ts`. Each has a name (the folder), a route, and optional `prepare` steps, such as opening a dialog.
- Prefer role and label selectors (`getByRole`, `getByLabel`), so the same steps work in both apps while the labels match.
- Branch on `app` only where the two apps genuinely differ.
- Never confirm a destructive action: open the dialog, take the screenshot, and stop.

If a React screen needs a different loading, dialog or alert marker, extend `BUSY`, `DIALOG` or `ALERT` in `shared.ts`.
