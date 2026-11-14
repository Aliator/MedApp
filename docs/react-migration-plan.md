# MedApp: Blazor to React migration plan

Status: **draft for review** · 2026-11-13

Build `MedApp.Web`, a React app on Astryx run under .NET Aspire, to replace `MedApp.Client` (Blazor WebAssembly) as the UI, keeping the same routes, behaviour and API. `MedApp.Client` stays in the repo as a deprecated project. **Nothing in the repo changes until the [open questions](#7-open-questions) are settled.**

## 1. Goals and fixed decisions

- **Astryx** is the UI library: Meta's open-source React design system, `@astryxdesign/core` (beta, v0.7.x, MIT).
- **.NET Aspire** runs the app locally: one AppHost starts the API, the React dev server and the database together.
- **Components move 1:1** where Astryx has an equivalent. Where it doesn't, the replacement is a **composite built only from Astryx primitives**, with no other UI library and no hand-rolled HTML/CSS widgets.
- **`MedApp.Web`** is the new project, at the repo root next to `MedApp.Client`. `MedApp.Client` is kept as an existing, **deprecated** project rather than deleted (Q5).
- API, Application, Domain and Infrastructure stay as they are, apart from what hosting under Aspire needs (CORS origins, cookie settings, connection strings).

## 2. Current state

`MedApp.Client` is a .NET 8 Blazor WebAssembly app. It has 8 routes and 28 shared components, plus its own CSS. It references only `MedApp.Contracts` and calls the API with a cookie-carrying `HttpClient`.

| Area | What's there |
|---|---|
| Routes | `/login`, `/` (home), `/account`, `/patients`, `/patients/add`, `/patients/{id}`, `/patients/{id}/edit`, `/admin/users` |
| Shared components | 28 under `Components/`; all are mapped in [section 4](#4-component-mapping) |
| Layout | `MainLayout` + `NavMenu` with `NavItem` links |
| Auth | `AuthState` caches `GET api/auth/whoami` (authenticated, roles, username). `Page` has `RequireAuth` and redirects to `/login` |
| HTTP | `CredentialHandler` sends the session cookie on every request. The base address is hard-coded to `https://localhost:44305/` |
| API surface used | Patients: list, get, create, update, delete. Auth: login, logout, whoami, users (list, create, delete, reset password), roles (list, a user's roles, assign, revoke) |
| Errors | 400 responses are read as `ValidationErrorResponse` and shown in `AlertMessage`. `Pages/Patients/EditPatient.razor.cs` is the reference |
| Styling | Bootstrap CSS + a 539-line `app.css` (33 custom properties) + scoped `.razor.css` per component |
| Icons | In-repo `Icon` component with a 34-value `IconType` enum (Back, Email, Save, UserPlus, Patients, Logout, Shield, Lock, Sort…) |
| JS interop | One file: `DataTable.razor.js` measures the viewport to pick the page size, so the table fills the screen without scrolling |
| Client-only models | `PartialDate` (year/month/day, each optional), patient and user search criteria, search field definitions |

Four behaviours need care in the port:
- **Partial dates:** `DatePickerFormGroup` supports partial dates. Only the patient search dialog uses them.
- **Viewport page size:** `DataTable` sizes its page to the viewport.
- **Dialog icons:** the confirm dialogs on the Users page show large custom icons.
- **Role assignment:** `SelectionModal` drives assigning and revoking roles.

## 3. Target architecture

```
MedApp.AppHost  (Aspire 13.6, C#, .NET 10 SDK)
 ├── sql        SQL Server container + data volume, database "MedApp"   (Q2)
 ├── api        MedApp.API  (unchanged apart from config)              ← references sql
 │                └── publish: serves the built React app from wwwroot  (Q4)
 └── web        MedApp.Web  (React 19 + TypeScript + Vite + Astryx)    ← references api
                  └── dev: Vite dev server; /api proxied to the API     (Q3)

MedApp.ServiceDefaults  (Aspire: OpenTelemetry, health checks, service discovery)
                        referenced by MedApp.API
```

- **AppHost:** a new `MedApp.AppHost` project uses `Aspire.Hosting.JavaScript` and `AddViteApp("web", "../MedApp.Web")`. Its `WithReference(api)` gives the React app the API's URL, so nothing is hard-coded (today's `https://localhost:44305/` goes away).
- **ServiceDefaults:** a new `MedApp.ServiceDefaults` project. The API calls `builder.AddServiceDefaults()` to get the Aspire dashboard's logs, traces and health checks.
- **Production:** Aspire builds the Vite output on publish but doesn't serve it, so something else has to (Q4).
- **`MedApp.Web` structure** (proposed):
  ```
  MedApp.Web/
    src/
      api/          typed API client + generated Contracts types (Q6)
      auth/         whoami query, beforeLoad auth/role guards
      components/   ports of Components/* (same folder groups: pages, forms, tables, modals, views, shared, navigation)
      pages/        one folder per route, same as Pages/*
      routes/       TanStack Router routes (file-based or code-based, Q7b)
      app.tsx       QueryClientProvider + RouterProvider + Astryx Theme/LinkProvider/InternationalizationProvider; AppShell + SideNav in the root layout
  ```
- **Astryx wiring:**
  - `Theme`: the custom `medapp` theme (Q11), light mode, with Remix icon overrides (Q8).
  - `LinkProvider`: makes every Astryx link and `href` button use the router's `Link`, for client-side navigation.
  - `InternationalizationProvider`: sets the locale to `en-GB` (Q15).
  - `@astryxdesign/core` is pinned to an exact version.

## 4. Component mapping

**1:1** means one Astryx component with equivalent props. **Composite** means a small component in `MedApp.Web/src/components` built only from Astryx primitives. All listed Astryx components report `isReady: true` in the Astryx MCP.

| Blazor component | Kind | Astryx replacement | Notes |
|---|---|---|---|
| **Layout / navigation** | | | |
| `MainLayout` | 1:1 | `AppShell` | `sideNav`, `topNav`, `banner` slots; skip-to-content. Mobile navigation is out of scope for now |
| `NavMenu` | 1:1 | `SideNav` | Admin-only items hidden by role, as today |
| `NavItem` | 1:1 | `SideNavItem` | `isSelected` from the router's current match (`Match` param) |
| **Pages** | | | |
| `Page` | Composite | `Layout` / `Section` + `PageHeader` composite + `Spinner` / `Skeleton` while loading | `RequireAuth` and `OnLoad` move to a route guard and loaders, not the UI |
| `PageHeader` | Composite | `HStack`: `IconButton` (back), `Heading` + `Text` (subtitle), actions slot | |
| **Forms** | | | |
| `FormCard` | Composite | native `<form>` + `Section` + `FormLayout` + footer `HStack` of `Button`s | Save uses `isLoading`; delete uses the destructive variant. Astryx says `<form>` stays native |
| `FormSection` | 1:1 | `Section` + `Heading` + `FormLayout` | `UseGrid` maps to `Grid` / `FormLayout` direction |
| `FormGroup` | 1:1 | `Field` | `Label`→`label`, `Required`→`isRequired`, `Hint`→`description`, `Error`→`status={type:'error', message}` |
| `DatePickerFormGroup` (full date) | 1:1 | `DateInput` | Add/edit patient date of birth. Value is an ISO `YYYY-MM-DD` string. The age hint goes in `description` |
| `DatePickerFormGroup` (`Partial`) | 1:1 | `DateRangeInput` | Patient search only. A born-between range replaces the partial date (Q9) |
| `PatientSummary` | Composite | `MetadataList` (+ `Avatar`) | Domain-specific; name, date of birth, email |
| **Tables** | | | |
| `DataTable` | Composite | `Table` + `useTableSortable` + `useTablePagination` + `Pagination` + `Skeleton` rows | `Table` shows a built-in empty state for empty data. Fixed page size of 10, with no viewport sizing (Q10) |
| `SortableHeader` | Removed | built into `Table` via `useTableSortable` | Sort glyphs come from the plugin |
| `TableCard` | 1:1 | `Section` (or `Card`) | Astryx guidance: `Card` is for discrete items, not general layout |
| `TableActions` | Composite | `HStack` of `IconButton`s | |
| `ActionButton` | 1:1 | `IconButton` | `Title`→`tooltip`, `Disabled`→`isDisabled` |
| `AvatarCell` | Composite | `HStack`: `Avatar` (initials via `name`) + `VStack` of `Text` (secondary truncated with `maxLines`) | |
| **Modals** | | | |
| `Modal` | 1:1 | `Dialog` | `IsDanger`→`purpose`; `CloseOnBackdropClick` handled via `onOpenChange` |
| `ConfirmModal` | Composite | `ConfirmDialog`: `Dialog` + `Icon` + `Heading` + `Text` + footer `Button`s | Built on `Dialog` to keep the icons, since `AlertDialog` has no icon slot (Q16) |
| `SearchModal` | Composite | `Dialog` + `FormLayout` + `TextInput` / `Selector` / `DateRangeInput`, driven by the same field definitions | Patients and Users search |
| `SelectionModal` | Composite | `Dialog` + checkbox list + `EmptyState` + `Button`s | Assign/revoke roles |
| **Views** | | | |
| `ViewCard` | Composite | `Section`s + footer `HStack` of `Button`s | Mirrors `FormCard` |
| `ViewSection` | 1:1 | `Section` + `Heading` + `MetadataList` | `UseGrid` → `MetadataList` `columns` |
| `ViewGroup` | 1:1 | `MetadataList` item | Label / value |
| **Shared** | | | |
| `AlertMessage` | 1:1 | `Banner` | `status`, `title`, `description`; validation errors listed as children |
| `EmptyState` | 1:1 | `EmptyState` | `title`, `description`, `icon`, `actions` |
| `LoadingState` | 1:1 | `Spinner` with `label` | |
| `Spinner` | 1:1 | `Spinner` | |
| `Summary` / `SummaryItem` | 1:1 | `MetadataList` / its item | |
| `Icon` | 1:1 | `Icon` with a Remix Icon component | `IconType` is replaced by `@remixicon/react` imports; Astryx's built-in icons are swapped to Remix in the theme (Q8) |

Totals: 18 mapped 1:1, 11 composites and 1 removed. `DatePickerFormGroup` is listed twice because it splits by usage.

## 5. Pages and cross-cutting concerns

**Pages** keep their routes and are ported one per route: Login, Home, Account, Patients (list, view, add, edit) and Admin Users. Each page composes the components above, just as the Blazor pages do.

**Auth.**
- A TanStack Query `whoami` query replaces `AuthState`. It calls `GET api/auth/whoami` once, caches authenticated / roles / username, and is cleared on logout.
- A TanStack Router `beforeLoad` guard replaces `Page.RequireAuth` and redirects to `/login`.
- Admin-only routes check the `Admin` role, as today.
- The session cookie stays HttpOnly. Every request uses `credentials: 'include'`, or nothing extra if the dev proxy makes it same-origin (Q3).

**API client and types.**
- One small `fetch` wrapper sets the base path and credentials and parses JSON.
- A 400 is turned into a typed validation error carrying `ValidationErrorResponse`.
- The DTO types come from `MedApp.Contracts` (Q6).

**Validation errors.** The API's `ValidationProblemDetails` errors are keyed by property name. Each page maps them onto each `Field`'s `status`, and shows the rest (or a summary) in a `Banner`. That behaviour matches `EditPatient` today.

**Routing.** TanStack Router (Q7) keeps the same URLs. `LinkProvider` connects Astryx links to the router's `Link`.

**Not carried over.**
- `DataTable.razor.js`: dropped, because the page size is fixed (Q10).
- Bootstrap, `app.css` and the scoped `.razor.css` files: the custom Astryx theme replaces them (Q11).
- `_Imports.razor`, `App.razor` and the `.razor` / `.razor.cs` / `.razor.css` three-file split: replaced by one `.tsx` file per component. Any `className` overrides stay minimal.

**Repo conventions.** `AGENTS.md`'s frontend section is rewritten for `MedApp.Web`, and `MedApp.Client` is listed as deprecated. The `.razor` split rule goes, and the composite-only-from-Astryx rule is added. The Astryx MCP (`https://astryx.atmeta.com/mcp`) goes in a project `.mcp.json`, so agents can look up components.

### Visual comparison while developing (Playwright)

✅ **Decided (2026-11-14):** set up Playwright to take screenshots of the **same screens in both apps**, so `MedApp.Client` and `MedApp.Web` can be compared side by side throughout the migration. This is a development tool, not the E2E test suite: `MedApp.E2ETests` stays empty (Q13), and nothing here asserts behaviour or fails a build.

- **Location:** `tools/ui-parity/`, a small Node project using `@playwright/test` and TypeScript.
  - It isn't in `MedApp.Web`, because it has to capture Blazor before the React app exists.
  - It isn't in `MedApp.E2ETests`, which stays blank.
- **Two Playwright projects:** `blazor` and `react`, each with its own `baseURL` from an env var (`BLAZOR_URL`, `WEB_URL`). Locally, these are the URLs Aspire shows; `BLAZOR_URL` is `https://localhost:7292` until Aspire is in place.
- **Both apps use the same API and database**, so both show the same data. Fixed patient IDs from the synthetic dev data are used for the view and edit pages.
- **Login:** a setup step logs in through each app's login page once, using `PARITY_USERNAME` / `PARITY_PASSWORD` from env (never committed), and saves the session (`storageState`) per app. The account must be an `Admin` to reach `/admin/users` (Q17).
- **One shared list of screens,** each captured in both apps under the same name:

  | Screen | Route / state |
  |---|---|
  | `login` | `/login`, empty |
  | `login-error` | `/login` after a wrong password |
  | `home` | `/` |
  | `account` | `/account` |
  | `patients-list` | `/patients`, page 1 |
  | `patients-search` | `/patients` with the search dialog open |
  | `patients-sorted` | `/patients?sort=DateOfBirth&asc=false` |
  | `patient-view` | `/patients/{id}` |
  | `patient-add` | `/patients/add`, empty |
  | `patient-add-errors` | `/patients/add` after submitting it empty (validation errors) |
  | `patient-edit` | `/patients/{id}/edit` |
  | `patient-delete-confirm` | delete confirmation open |
  | `users-list` | `/admin/users` |
  | `users-reset-password` | reset-password confirmation open |
  | `users-delete` | delete-user confirmation open |
  | `users-create` | create-user dialog open |
  | `users-roles` | role selection dialog open |

  The side navigation is visible in every signed-in screenshot, so it has no screen of its own.

- **Status (2026-11-14): set up.** The tool is in `tools/ui-parity/`, with `README.md` covering setup and running. The Blazor login screens are captured; the signed-in screens wait for the dev admin account in `.env`.
- **Stable captures:**
  - Fixed desktop viewport, 1440×900. **No mobile captures, and no mobile UI work in this migration (decided 2026-11-14)**: mobile layouts are out of scope for now.
  - Locale `en-GB`, timezone `Europe/London`.
  - Animations off, and a short wait for the network to go idle.
  - Full-page screenshots.
- **Output:**
  - Each screen is saved as `tools/ui-parity/screenshots/<screen>/blazor.png` and `react.png`.
  - A generated `report.html` puts each pair side by side, with a "missing in React" marker for screens not yet ported.
  - Screenshots are git-ignored.
  - Comparison is **by eye, not a pixel diff**: the two apps are meant to look alike but not identical (Astryx vs Bootstrap, Q11).
- **Running it:** `npm run parity` (both apps) or `npm run parity:blazor` / `npm run parity:react`, while the apps are running. It's not in CI, because it needs live apps and a login.
- **When it's used:**
  - In phase 2, capture the Blazor baseline.
  - In phases 3–5, after each component or page is ported, re-capture both, compare, and fix differences before moving on.
  - `AGENTS.md` gets a line telling agents to run it and look at both screenshots when they change a page during the migration.
- **Browsers:** only Chromium is installed (`npx playwright install chromium`, about 150–300 MB), which counts against the disk space already short on C: (Q2).

## 6. Phased delivery

Each phase ends with a working app. Blazor stays the default UI until phase 5, and is then deprecated, not deleted.

1. **Decisions:** settle every open question in this doc. *Exit:* section 7 is all answered, and the doc is updated.
2. **Aspire foundation and Blazor baseline:**
   - Add `MedApp.AppHost` and `MedApp.ServiceDefaults`. The AppHost runs the database, the API and the existing Blazor client.
   - Set up `tools/ui-parity` and capture the Blazor baseline for every screen.

   *Exit:* one command starts everything, login works in Blazor, and a full set of Blazor screenshots exists.
3. **React shell:**
   - Scaffold `MedApp.Web` (Vite + React 19 + TypeScript + Astryx) and add it to the AppHost.
   - Add `AppShell` + `SideNav`, the auth provider and route guard, and the API client and types.
   - Build the Login page.
   - Set up lint and unit tests.

   *Exit:* log in, navigate, and log out against the real API.
4. **Components:** build the section 4 composites, each with a unit test. *Exit:* every Blazor component has a React replacement.
5. **Pages and cutover:**
   - Port the pages: Patients list, view, add and edit; Admin Users; Account; Home.
   - Check parity against the checklist below.
   - Deprecate `MedApp.Client`, as Q5b settles it: take it out of the AppHost, and mark it deprecated in `AGENTS.md` and its project.
   - Update CORS and cookie settings, then CI and `AGENTS.md`.

   *Exit:* parity checklist complete, CI green, `MedApp.Web` is the only UI the AppHost runs, and `MedApp.Client` is marked deprecated.

Parity is checked by hand against the checklist below, with the Playwright screenshot pairs as the visual reference. End-to-end tests come later (Q13).

Parity checklist (per page): same route; same data shown; the same API calls; validation errors shown on the right fields; loading, empty and error states; admin-only behaviour; keyboard and screen-reader basics (Astryx provides most of this).

## 7. Open questions

Each question lists options; where I have a recommendation, it's marked.

1. **.NET SDK and Aspire version.** ✅ **Decided (2026-11-13): move to .NET 10 and use the current Aspire (13.6).**
   - Install the .NET 10 SDK locally, and move `global.json` to `10.0.x`.
   - CI's `setup-dotnet` moves to `10.0.x`.
   - The AppHost uses `Aspire.Hosting.JavaScript` with `AddViteApp`.

   **Follow-up (1b): should the existing projects also retarget `net8.0` → `net10.0`?** .NET 8's support ended on 10 Nov 2026, while .NET 10 is LTS. Retargeting also moves the EF Core, ASP.NET Core and Identity packages to 10.x, so the tests need re-running and possibly some small fixes.
   - **(a)** Retarget everything to `net10.0` in its own PR, before the migration starts. *(recommended)*
   - **(b)** Keep `net8.0` and change only the SDK.
2. **Database under Aspire.** ✅ **Decided (2026-11-13): use a SQL Server container via Docker.**
   - The AppHost declares `AddSqlServer("sql").WithDataVolume().AddDatabase("MedApp")` and passes it to the API with `WithReference`.
   - The data volume keeps data across restarts.
   - LocalDB stops being the dev database, so `ConnectionStrings` in `appsettings.Development.json` goes, and Aspire injects the connection string instead.
   - Migrations still run manually, per `AGENTS.md`, against the container's connection string.

   **Prerequisites:**
   - **Docker Desktop on the dev machine.** It's blocked for now: C: has 2.5 GB free, and Docker Desktop needs 3.3 GB to install. The SQL Server image and WSL disk need several GB more, so about 10 GB free in total.
   - **Docker Desktop licence.** It's free for personal use and for companies under 250 employees and $10M revenue; otherwise it needs a paid subscription.

   **Follow-up (2b): existing LocalDB data.** The container starts empty. Either start fresh (the data is synthetic: 53 patients, stage definitions and templates), or export it from LocalDB and import it into the container once.
3. **Dev networking.** ✅ **Decided (2026-11-13): (a), a Vite dev proxy.**
   - The Vite dev server proxies `/api` to the API, using the API's URL from Aspire (`WithReference(api)`).
   - The browser sees one origin, so the session cookie is first-party and CORS isn't needed in development.
   - The hard-coded `https://localhost:7292` origin in `ServiceExtensions.cs` and the `SameSite` dev branch in `SessionCookieService` get revisited at cutover.
4. **Production hosting.** ✅ **Decided (2026-11-13): (a), the API serves the built React app.**
   - On publish, Aspire builds `MedApp.Web` and copies the output into the API's `wwwroot` (`PublishWithContainerFiles`).
   - The API adds `UseStaticFiles` and a fallback to `index.html` for client-side routes, keeping it clear of `/api` and `/swagger`.
   - App and API share one origin in production too, so CORS and cross-site cookie settings aren't needed.
5. **Project name, location and package manager.** ✅ **Decided (2026-11-13):** the new project is **`MedApp.Web/` at the repo root**, next to `MedApp.Client/`. `MedApp.Client` stays in the repo as an existing, **deprecated** project.

   **Follow-ups still open:**
   - **(5b) What "deprecated" means for `MedApp.Client`.** Proposed:
     - No new features; changes only to keep it building, for example after the .NET 10 move (Q1b).
     - It stays in `MedApp.sln` and keeps building in CI.
     - It's removed from the AppHost at cutover, so it no longer runs by default.
     - `AGENTS.md` marks it deprecated, and points UI work at `MedApp.Web`.
     - The API's CORS entry for its origin (`https://localhost:7292`) goes at cutover, since it won't be running. Running it by hand would then need that entry back.
   - **(5c) Solution membership for `MedApp.Web`.** Options:
     - Add it to `MedApp.sln` as a solution folder, so it's visible in Rider. *(recommended)*
     - Leave it out of the solution; only the AppHost references it, by path.
   - **(5d) Package manager.** npm (ships with the Node.js already installed) or pnpm (faster, stricter dependencies)?
6. **TypeScript types for `MedApp.Contracts`.** Options:
   - **(a)** Generate them from the API's Swagger document with `openapi-typescript`, and optionally `openapi-fetch` for a typed client. One source of truth that can't drift. *(recommended)*
   - **(b)** Use NSwag to generate a TypeScript client.
   - **(c)** Write the types by hand.
7. **Non-UI libraries.** ✅ **Decided (2026-11-14): TanStack Router, TanStack Query and controlled state.** The "only Astryx primitives" rule covers UI, so these non-UI libraries are allowed.
   - **TanStack Router:**
     - Routes are typed, including the `{id}` params.
     - The auth guard is a `beforeLoad` on a protected layout route. It reads the whoami query from the router context and redirects to `/login`. Admin routes also check the `Admin` role.
     - Astryx's `LinkProvider` wraps the router's `Link`.
   - **TanStack Query:**
     - Each API read is a query keyed by resource (`['patients']`, `['patients', id]`, `['users']`, …).
     - Each write is a mutation that invalidates the matching queries.
     - `whoami` is a query too, cleared on logout. This replaces `AuthState`.
   - **Controlled state:**
     - Each form holds its fields in `useState`.
     - On a 400 it puts the API's `ValidationProblemDetails` errors on each `Field`'s `status`.
     - The server's FluentValidation stays the source of truth, with no duplicated client-side rules.

   **Follow-up (7b): file-based or code-based routes?** Options:
   - **File-based:** `@tanstack/router-plugin` for Vite generates the route tree from `src/routes/`. *(recommended: the routes are then laid out like `Pages/` today)*
   - **Code-based:** routes declared by hand in one file.
8. **Icons.** ✅ **Decided (2026-11-14): use [Remix Icon](https://remixicon.com) (`@remixicon/react`, v4.9.0) instead of the in-repo `Icon` / `IconType`, which isn't ported.**
   - **App icons:** import Remix components (`RiUserAddLine` …) and pass them to Astryx's `Icon`, `IconButton` or `Button` through `icon`, which accepts an SVG component.
   - **Astryx's own icons:** sort arrows, pagination chevrons, dialog close and banner status icons are replaced with Remix in our theme (`defineTheme({icons: {...}})`), so only one icon set appears. Astryx recommends overriding through the theme rather than the global `registerIcons()`.
   - **Style:** the outline (`…Line`) variant everywhere. Its licence is the Remix Icon License 1.0 (free for personal and commercial use), not MIT.

   `IconType` → Remix (all names checked against the 4.9.0 exports; ⚠ = a judgement call worth a look):

   | `IconType` | Remix | `IconType` | Remix |
   |---|---|---|---|
   | Back | `RiArrowLeftLine` | Shield | `RiShieldLine` |
   | Email | `RiMailLine` | View | `RiEyeLine` |
   | Save | `RiSaveLine` | Edit | `RiPencilLine` |
   | Cancel | `RiCloseLine` | Add | `RiAddLine` |
   | Delete | `RiDeleteBinLine` | Password | `RiKeyLine` |
   | Check | `RiCheckLine` | Assign ⚠ | `RiUserFollowLine` |
   | Alert | `RiAlertLine` | Revoke ⚠ | `RiUserUnfollowLine` |
   | Success | `RiCheckboxCircleLine` | Refresh | `RiRefreshLine` |
   | Warning | `RiErrorWarningLine` | Search | `RiSearchLine` |
   | Info | `RiInformationLine` | Menu | `RiMenuLine` |
   | UserPlus | `RiUserAddLine` | ChevronDown | `RiArrowDownSLine` |
   | UserMinus | `RiUserMinusLine` | ChevronLeft | `RiArrowLeftSLine` |
   | Home | `RiHomeLine` | ChevronRight | `RiArrowRightSLine` |
   | Patients ⚠ | `RiUserHeartLine` | ChevronDoubleLeft | `RiArrowLeftDoubleLine` |
   | Users | `RiTeamLine` | ChevronDoubleRight | `RiArrowRightDoubleLine` |
   | Logout | `RiLogoutBoxRLine` | Lock | `RiLockLine` |
   | SortAsc | `RiSortAsc` | SortDesc | `RiSortDesc` |

   Astryx semantic icon → Remix (theme override; any we don't override fall back to Astryx's artwork):

   | Astryx name | Remix | Astryx name | Remix |
   |---|---|---|---|
   | `close` | `RiCloseLine` | `calendar` | `RiCalendarLine` |
   | `check` | `RiCheckLine` | `clock` | `RiTimeLine` |
   | `success` | `RiCheckboxCircleLine` | `search` | `RiSearchLine` |
   | `error` | `RiCloseCircleLine` | `menu` | `RiMenuLine` |
   | `warning` | `RiErrorWarningLine` | `moreHorizontal` | `RiMoreLine` |
   | `info` | `RiInformationLine` | `externalLink` | `RiExternalLinkLine` |
   | `chevronDown` / `Left` / `Right` | `RiArrowDownSLine` / `RiArrowLeftSLine` / `RiArrowRightSLine` | `arrowUp` / `arrowDown` / `arrowsUpDown` | `RiArrowUpLine` / `RiArrowDownLine` / `RiArrowUpDownLine` |
   | `chevronsLeft` / `chevronsRight` | `RiArrowLeftDoubleLine` / `RiArrowRightDoubleLine` | `funnel` / `eyeSlash` / `copy` | `RiFilterLine` / `RiEyeOffLine` / `RiFileCopyLine` |
9. **Partial dates.** ✅ **Decided (2026-11-14): use Astryx's `DateRangeInput` for the date-of-birth search, and drop the partial-date composite.**
   - **Add/edit patient:** use `DateInput` for the full date of birth (ISO `YYYY-MM-DD`).
   - **Patient search:** use `DateRangeInput`, labelled "Date of birth", which filters patients born between `start` and `end`, both inclusive.
     - Its value is Astryx's own `DateRange` type, `{start, end}` as ISO dates, or `null` when cleared.
     - Set `max` to today, `hasClear`, and `numberOfMonths={1}` so it fits inside the search dialog.
   - **Not ported:** `PartialDate`, and the day/month/year part of `PatientSearchCriteria`.
   - **URL:** the range goes in the query string as `dobFrom` / `dobTo`, next to `page`, `ps`, `sort`, `asc` and `q`, so a search is still a shareable link.
   - **Search capability lost:** today's partial match checks day, month and year independently (`PartialDate.Matches`). That allows searches a range can't express, such as "born in March, any year" or "born on the 15th". Year-only and month-and-year searches still work as ranges, for example 1980-01-01 to 1980-12-31.
     - **Follow-up (9b):** is losing the any-year searches acceptable? Presets on `DateRangeInput` (`presets`) can give quick ranges, such as decades, if that helps.
10. **Table page size.** ✅ **Decided (2026-11-14): a fixed page size of 10, kept as plain as possible.**
    - 10 is the same default the Blazor tables start with before measuring.
    - There's no viewport measuring and no page-size picker: `Pagination` gets `page`, `totalPages` and `onChange` only.
    - The `ps` query parameter is dropped; `page`, `sort`, `asc` and `q` stay.
    - Auto-sizing or a page-size picker can be revisited after the migration.
11. **Theme and look.** ✅ **Decided (2026-11-14): a custom Astryx theme that matches the current look.**
    - **Base:** the theme is a `medapp` theme extending `@astryxdesign/theme-neutral` (v0.7.15), overriding only the tokens below, which are taken from `app.css`. Anything not overridden keeps the neutral theme's value.
    - **Build:** `astryx theme build` turns it into static CSS plus a theme object, as Astryx recommends for themes known ahead of time. It's checked in under `MedApp.Web/src/theme/`.
    - **Light mode only** (`<Theme mode="light">`), because the current app has no dark mode (follow-up 11b).
    - **Icons:** the theme also carries the Remix icon overrides (Q8).

    | Current (`app.css`) | Value | Astryx token |
    |---|---|---|
    | `--bg-base` (page background) | `#f4f6f8` | `--color-background-body` |
    | Card / panel background ⚠ assumed white | `#ffffff` | `--color-background-surface`, `--color-background-card`, `--color-background-popover` |
    | `--gray-50` | `#f9fafb` | `--color-background-muted` |
    | `--primary-500` (most-used accent) | `#3b82f6` | `--color-accent`, `--color-info`, `--color-icon-accent` |
    | `--primary-600` | `#2563eb` | `--color-text-accent` (links), `--color-text-info` |
    | `--primary-50` | `#eff6ff` | `--color-accent-muted`, `--color-info-muted` |
    | white on primary buttons | `#ffffff` | `--color-on-accent` |
    | `--gray-900` | `#111827` | `--color-text-primary`, `--color-icon-primary` |
    | `--gray-500` | `#6b7280` | `--color-text-secondary`, `--color-icon-secondary` |
    | `--gray-400` | `#9ca3af` | `--color-text-disabled`, `--color-icon-disabled` |
    | `--gray-200` (most-used colour: borders) | `#e5e7eb` | `--color-border` |
    | `--gray-300` | `#d1d5db` | `--color-border-emphasized` |
    | `--success-500` / `--success-50` | `#10b981` / `#ecfdf5` | `--color-success` / `--color-success-muted` |
    | `--danger-500` / `--danger-600` | `#ef4444` / `#dc2626` | `--color-error` / `--color-text-error` |
    | `--danger-50` / `--danger-200` | `#fef2f2` / `#fecaca` | `--color-error-muted` / `--color-border-error` |
    | `--warning-500` / `--warning-800` | `#f59e0b` / `#92400e` | `--color-warning` / `--color-text-warning` |
    | `--warning-50` / `--warning-200` | `#fffbeb` / `#fde68a` | `--color-warning-muted` / `--color-border-warning` |
    | `--purple-500` / `--purple-50` | `#8b5cf6` / `#f5f3ff` | `--color-text-purple` / `--color-background-purple` |
    | Radius 12px (cards, modals) | `12px` | `--radius-container` |
    | Radius 8px (most common: buttons, inputs) | `8px` | `--radius-element` |
    | Radius 6px | `6px` | `--radius-inner` |
    | Small shadow | `0 1px 2px 0 rgba(0,0,0,.05)` | `--shadow-low` |
    | Medium shadow | `0 4px 6px -1px rgba(0,0,0,.1)` | `--shadow-med` |
    | Modal shadow | `0 20px 25px -5px rgba(0,0,0,.1), 0 10px 10px -5px rgba(0,0,0,.04)` | `--shadow-high` |
    | Body font | `system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif` | `--font-family-body`, `--font-family-heading` |
    | Code font | `ui-monospace, SFMono-Regular, Menlo, monospace` | `--font-family-code` |

    **Not carried over:**
    - `--btn-height: 40px`: control heights come from Astryx's `size` prop (`md` by default). Matching 40px exactly would mean overriding components, which this plan avoids.
    - The 54 Bootstrap component overrides in `app.css` (`.btn`, `.form-control`, `.card`, `.table`, `.modal`…): Astryx components plus the tokens above replace them.

    **Follow-up (11b): dark mode.** None today. Options: stay light-only (proposed), or add dark values now. Every token accepts a `[light, dark]` pair, so this can wait.
12. **Cutover strategy.** ✅ **Decided (2026-11-14): (a), build alongside, then switch.** `MedApp.Web` is built next to `MedApp.Client`, with both running under the AppHost through phases 3–4. Once every page passes the parity checklist, `MedApp.Web` becomes the only UI the AppHost runs, and `MedApp.Client` is deprecated (Q5). There's no page-by-page mixing of the two apps.
13. **Testing and CI.** ✅ **Decided in part (2026-11-14): no end-to-end tests in this migration.** `MedApp.E2ETests` stays an empty shell, and E2E tests are added later as separate work.

    **Follow-up (13b): unit tests and CI for `MedApp.Web`.** Still proposed, not yet confirmed:
    - Vitest + React Testing Library for components and pages.
    - Add the web build, lint and tests to `main.yml`, as a step or job beside the .NET unit tests.
    - Decide whether web coverage joins the PR coverage comment, or the comment stays .NET-only for now.
14. **Astryx beta.** ✅ **Decided (2026-11-14):**
    - Pin exact versions of all `@astryxdesign/*` packages (no `^` or `~`), starting at 0.7.15.
    - Upgrade deliberately, in their own PRs, after reading the changelog.
    - Don't use `swizzle` (ejecting a component's source into the repo) unless it's agreed for a specific component.
    - Only stable packages; nothing from the `@canary` tag.
15. **Locale.** ✅ **Decided (2026-11-14): `en-GB`.**
    - `InternationalizationProvider` sets `en-GB` at the app root.
    - `DateInput`, `DateRangeInput` and `Timestamp` then show UK dates (day before month).
    - Calendars should start the week on Monday. If the locale doesn't do that on its own, set it with `weekStartsOn` (to check in phase 4).
    - Values sent to the API stay ISO `YYYY-MM-DD`, unchanged.
16. **Confirm dialog icons.** ✅ **Decided (2026-11-14): a `ConfirmDialog` composite built on `Dialog`, with the icons kept.**

    Astryx's `AlertDialog` has no icon or children slot, so it isn't used. The composite is built only from Astryx parts:
    - `Dialog`, with `isOpen` / `onOpenChange`, `purpose` matching today's `ConfirmModal` dismissal, and `width`.
    - Inside it, a `VStack` with `Icon` (a Remix icon, coloured by intent), `Heading` (title), `Text` (message) and an optional `children` slot.
    - A footer `HStack` with a `secondary` Cancel `Button` and a confirm `Button`. The confirm button is `destructive` when `isDanger`, shows `isLoading` while processing, and is disabled when `confirmDisabled`.

    Its props mirror `ConfirmModal`: `isOpen`, `title`, `message`, `icon`, `isDanger`, `isProcessing`, `confirmDisabled`, `confirmText`, `cancelText`, `onConfirm`, `onCancel`, `children`.

    Today's uses, all on the Users page:

    | Dialog | Icon | Intent |
    |---|---|---|
    | Reset password | `RiLockLine` | warning |
    | Delete user | `RiErrorWarningLine` | danger |
    | Create new user | `RiAddLine` | accent |

    **Follow-up (16b): icon size.** Today's icons are 48px, but Astryx's largest `Icon` size is `lg`, 24px. Options:
    - **(a)** Use 24px (`lg`). Plain, and the same size as everywhere else.
    - **(b)** Keep 48px by wrapping the icon in a nested `<Theme>` that sets `components.icon['size:lg']` to 48px for this dialog only. That stays within Astryx, using a scoped theme override.

17. **Screenshot tool login and timing.** ✅ **Decided (2026-11-14):**
    - **17a:** use a dev-only admin account, with its credentials in a git-ignored `tools/ui-parity/.env`.
    - **17b:** set the tool up now, against today's Blazor app, and capture the baseline before the migration starts.

    The options that were considered:
    - **(17a) Admin account.** No admin user is seeded, so which synthetic account should screenshots log in with? Options:
      - A dev-only account you create once and keep in a local, git-ignored `.env` file. *(proposed)*
      - A dev-only seed in the database. That's a schema/data change, so it's best avoided.
    - **(17b) When to set it up.** Options:
      - Set it up now, against today's Blazor app, to capture the baseline before anything changes.
      - Set it up in phase 2, as planned above.

      Setting it up now needs the client and API URLs to line up (the client calls `https://localhost:44305/`, but `dotnet run` serves the API on `7038`), and about 300 MB of disk for Chromium.

## 8. Risks and mitigations

| Risk | Mitigation |
|---|---|
| Astryx is beta (v0.7.x), so APIs may change | Pin the exact version; wrap the most-used patterns in our own composites, so changes land in one place |
| React 19 is required by Astryx | Start fresh on React 19; no legacy React code to migrate |
| Moving to the .NET 10 SDK (Q1) breaks the build or tests | Do the SDK change (and any retarget, Q1b) in its own PR before phase 2, with CI green |
| Session cookies break across origins | Same-origin dev proxy (Q3) and same-origin production hosting (Q4) |
| Behaviour gaps during the port (for example role selection) | Parity checklist per page; Blazor stays runnable until cutover. Deliberate changes (date-range search, Q9; fixed page size, Q10) are listed, so they aren't mistaken for regressions |
| Validation errors don't land on the right field | One shared mapper from `ValidationProblemDetails` to `Field` status, unit tested |
| Losing local data when moving to a SQL container (Q2) | Data volume for persistence; decide on a one-off export/import from LocalDB (Q2b) |
| Not enough disk for Docker (2.5 GB free on C:) | Free about 10 GB before phase 2; Docker is a prerequisite for it. Playwright's Chromium needs about 300 MB more |
| Visual drift between the apps goes unnoticed | Playwright screenshot pairs for 17 desktop screens, re-captured after each ported page |

## Sources

- Astryx: [repository](https://github.com/facebook/astryx), [docs](https://astryx.atmeta.com), and component details (props, readiness) from the hosted MCP at `https://astryx.atmeta.com/mcp`, queried 2026-11-13 (`@astryxdesign/core` v0.7.15)
- Aspire: [prerequisites (Aspire 13.6, .NET 10 SDK for C# AppHosts)](https://aspire.dev/get-started/prerequisites/), [JavaScript apps in the AppHost (`AddViteApp`)](https://aspire.dev/integrations/frameworks/javascript/), [deploying JavaScript apps](https://aspire.dev/deployment/javascript-apps/)
