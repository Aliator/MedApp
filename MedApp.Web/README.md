# MedApp.Web

MedApp's UI. It replaced the original Blazor client (now `MedApp.Client.Deprecated`) in the React migration; see `docs/react-migration-plan.md`. Built with React 19, TypeScript, Vite and [Astryx](https://astryx.atmeta.com) (`@astryxdesign/core`, pinned to an exact version).

## Running it

The AppHost runs it with everything else, at **http://localhost:5173**:

```bash
dotnet run --project MedApp.AppHost   # from the repo root
```

`npm run dev` on its own also works while the API is running; it proxies `/api` to `https://localhost:7038`.

In production there's no separate web server: on publish, the AppHost builds this app into the API's `wwwroot`, and the API serves it on the same origin as `/api` (plan Q4). Unknown paths fall back to `index.html`, so client-side routes survive a reload.

## Scripts

| Script | Does |
|---|---|
| `npm run dev` | Vite dev server (the AppHost runs this) |
| `npm run build` | Type check, then production build into `dist/` |
| `npm run lint` | ESLint |
| `npm run typecheck` | `tsc -b` |
| `npm test` / `npm run test:watch` / `npm run coverage` | Vitest + React Testing Library |
| `npm run gen:api` | Regenerates `src/api/schema.d.ts` from the API's Swagger document (needs the AppHost running) |
| `npm run theme:build` / `npm run theme:check` | Rebuilds the `medapp` theme, or checks the built files match its source |

## Layout

```
src/
  api/          openapi-fetch client, generated schema.d.ts, 400 → field-error mapper, paged search queries
  app/          App, providers (theme, locale, links, query cache), light/dark mode
  auth/         whoami query, login and logout
  components/   ports of MedApp.Client's Components/, built only from Astryx:
                  pages/ (Page, PageHeader), forms/ (FormCard, FormSection, date-of-birth inputs, PatientSummary),
                  tables/ (DataTable, TableActions, AvatarCell), modals/ (ConfirmDialog, SelectionDialog, SearchDialog),
                  views/ (ViewCard, ViewSection), navigation/, shared/ (AppIcon, AlertMessage)
  pages/        one folder per page: patients/ (list, view, add, edit), users/, account/, login/, home/;
                  listSearch.ts parses the list pages' URL state
  routes/       TanStack Router file routes (src/routeTree.gen.ts is generated from them)
  themes/       the medapp theme: source (medappTheme.ts, icons.ts) and its built output
  utils/        small helpers (dates: age, en-GB formatting, today)
  test/         Vitest setup and render helpers
```

## How it fits together

- **API calls** go to `/api/...` on the app's own origin. In development, Vite proxies them to the API (the AppHost passes in its URL), so the HttpOnly session cookie is first-party and CORS isn't involved. Types come from the API's Swagger document, so after changing a DTO or endpoint, run `npm run gen:api`.
- **Errors:** `unwrap()` throws `ApiValidationError` for a 400. `fieldStatus(errors, 'firstName')` gives a field its error, and `allMessages()` gives the list for a summary `Banner`.
- **Auth:** `whoamiQuery` caches who is signed in. The `_app` layout route's `beforeLoad` sends signed-out visitors to `/login`, and admin routes check the `Admin` role.
- **Theme:** `src/themes/medapp/medappTheme.ts` extends Astryx's stone theme with MedApp's blues, radii, system fonts and Remix icons. It's built ahead of time (`npm run theme:build`) and the output is checked in. Dark mode is the default; the toggle is in the side navigation's footer.
- **Icons:** use `AppIcon` from `src/components/shared/icons.ts` (Blazor's `IconType` names, mapped to Remix Icon).

See `AGENTS.md` for the conventions to follow.
