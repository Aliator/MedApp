# UI parity screenshots

Captures the same screens in the Blazor app (`MedApp.Client`) and the React app (`MedApp.Web`), and writes `report.html` with each pair side by side. It's for comparing the two by eye during the migration (see `docs/react-migration-plan.md`). **It is not an E2E test suite**: nothing is asserted, and it doesn't run in CI.

## One-time setup

1. Install Node.js, then run `npm install` and `npm run install-browser` (Chromium only) in this folder.
2. Create a **dev-only admin account** for the screenshots:
   - Sign in to the Blazor app as an admin, open **Users → Add User**, and create, for example, `parity-admin`. New users get the default password from the create-user dialog.
   - Use **Assign** to give it the `Admin` role.
   - Use synthetic details only, never a real person's.
3. Copy `.env.example` to `.env` (git-ignored) and fill in `PARITY_USERNAME` / `PARITY_PASSWORD`.

## Running it

Start the apps first. Until the Aspire AppHost exists:

```bash
# from the repo root: build once, then run without rebuilding (building both at once locks MedApp.Contracts)
dotnet build MedApp.API/MedApp.API.csproj && dotnet build MedApp.Client/MedApp.Client.csproj
dotnet run --no-build --project MedApp.API --launch-profile https --urls "https://localhost:44305"   # the port the Blazor client calls
dotnet run --no-build --project MedApp.Client --launch-profile https                               # https://localhost:7292
```

If either app fails with "the default developer certificate could not be found", run `dotnet dev-certs https` once.

Then, in this folder:

| Command | Captures |
|---|---|
| `npm run parity` | Both apps (React only when `WEB_URL` is set in `.env`) |
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
