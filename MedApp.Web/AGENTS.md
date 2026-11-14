# AGENTS

Guidance for AI coding agents working in `MedApp.Web`, the React app replacing the Blazor client. The repo-wide rules are in the root `AGENTS.md`; the migration plan is `docs/react-migration-plan.md`. `README.md` describes the layout and scripts.

## MedApp conventions

- **Only Astryx for UI.** Components map 1:1 to an Astryx component, or are composites built only from Astryx primitives (plan section 4). No other UI library and no hand-rolled widgets; keep `style` overrides to layout values the components have no prop for, using tokens.
- **Icons:** `AppIcon` from `src/components/shared/icons.ts`, never `@remixicon/react` directly. Add new icons there.
- **Routes** are thin files in `src/routes/` that point at a page component in `src/pages/`. Signed-in pages go under `src/routes/_app/`; admin-only routes use `beforeLoad: requireAdmin` (`src/auth/auth.ts`), which sends anyone else home.
- **Pages share one layout:** `Page` gives every page the same content width (`PAGE_MAX_WIDTH`), so titles and cards line up; don't pass `maxWidth` without a reason. A Back button (`backHref`/`onBack`) sits under the title, with `backLabel` as its tooltip.
- **Fit the window; scroll the cards, not the page.** Pages that show more than a short form use `Page` with `fill`, and their cards fill the space. A card's open parts scroll inside it; the page never does.
- **Build reusable patterns, not one-off page wiring.** When a page needs a layout or behaviour that another page could plausibly use, make it a composite in `src/components/` with its own test and a doc comment saying when to use it, then use it from the page. Look for an existing one first, and extend it rather than writing a near-copy. Signs a piece should be shared: you're reaching for flex or measuring code in a page, a second page wants the same thing, or the user describes it as a rule ("headers should look like…").
  - The shared building blocks so far:
    - `Page` and `PageHeader`: the frame, title and Back. A page's own messages (load and save errors) go in its `message`, a rounded banner in the middle of the header, so they never push the content down. On a form, `ValidationAlert` takes the form's `fields` and lists only the messages that aren't already on a field.
    - `TwoColumns`: two equal cards side by side.
    - `PanelCard` and `PanelHeader`: a header bar, a scrolling body and a pinned footer.
    - `FoldingCard`: sections under header bars that fold, scrolling together as one (not each on its own).
    - `CardSection`: titled sections in `ViewCard` and `FormCard`, whose titles become header bars.
    - `FormCard` with `fill`: a long form whose sections scroll inside the card, its footer pinned.
    - `DataTable`: a filling, paged table.
    - `ProcedureCells`: status, priority, due and progress cells.
    - `TextCell`: plain text in a table cell, kept to its lines.
    - `PageControls`: a list's page buttons (DataTable uses it); each "…" opens a box to type a page number.
    - `ListFilterBar`: a list's search as an Astryx PowerSearch, above its table (`DataTable`'s `search`): a token per filter, kept in the URL; choices take "is any of" and "is not any of"; optional favourites (a star beside it). Every list with a search uses it.
    - `ColourField` and `ColourSwatches`: picking a colour (the browser's colour input in an Astryx Field), and small squares showing a few.
  - Clickable things shade under the pointer: list rows do already, and `PanelHeader` (with `onClick`) and `ColourField` do the same. Keep it up for new clickable surfaces.
    - `SetupCells`: the Active/Archived token and name cells of the stage definitions and templates lists.
  - **Half views** (`src/components/pages/halfView.tsx`, `AppLayout`): every side nav item has a button (shown on hover, like the pin) that opens its page beside the current one, each in its narrow layout, with the nav folded away. With a half view open, each item has a left and a right button instead (both greyed out while either side shows its page), and clicking the item itself opens its page on its own, closing the half view (or, by a setting in Account Settings, on the left). A page with a narrow layout asks `useIsNarrow()` (narrow window or half view), never the window query directly. The second page runs in a router of its own, so its links and Back stay in its half.
  - Keep state that should survive a layout change (what's open or folded) in the page and pass it in, as the procedure page does, rather than inside each collapsible.
- **List pages** fill the window: `Page` with `fill`, and `DataTable` stretches to the bottom with its page controls pinned there. Their search is a `ListFilterBar` above the table (no Search button or dialog); a list that starts filtered (My Tasks on Not started, Stages and Templates on Active) shows that filter as a token and keeps an "all" value in the URL for when it's removed. They keep page, sort and filters in the URL: the route parses them with `validateSearch` (`src/pages/listSearch.ts`), then passes `search` and `onSearchChange` to the page as props, so pages are testable without the real router. Lists come from the API's paged search endpoints (`src/api/search.ts`).
- **Colours:** people choose the page, card and side nav colours for each mode (Account Settings, Colours: a colour theme, or Custom). `ColoursProvider` (`src/app/colours.tsx`) applies them through the theme's own background tokens (`--color-background-body`, `-surface`, `-card`, `-muted`, `-popover`). The side nav is always dark (a nested dark `Theme` in `AppLayout`).
  - **Every new component, page and surface must follow the chosen colours.** Use those tokens (or Astryx components, which do), never fixed colours or a theme's own defaults. Header bars use `-muted`, cards `-card`, menus and popovers `-popover`.
  - A surface that needs a chosen colour the theme doesn't route through a token: add the token to `ColoursProvider`, from the chosen colours, rather than hard-coding it.
  - Inside the side nav, the dark `Theme` sets its own tokens, so a surface there that should follow the app (a row's menu) uses `--medapp-card-background` and takes the app's mode (see `RowMenu` in `AppSideNav.tsx`).
  - Check a new component in light and dark mode, and with a colour theme other than Standard (Account Settings, Colours), before calling it done.
- **Data:** TanStack Query. Reads are queries keyed by resource (`['patients']`, `['patients', id]`); writes are mutations that invalidate them. Call the API through `api` + `unwrap` from `src/api/client.ts`.
- **Forms:** fields in `useState`; on a 400, show `fieldStatus(errors, field)` on each field. The server's FluentValidation is the source of truth, so don't duplicate its rules here.
  - Fields side by side line up: Astryx puts a description between the label and the input, so when one field in a row has a description, give the others `BLANK_DESCRIPTION` (`src/components/forms/fieldSpace.ts`). Do the same for a description that comes and goes, so the input doesn't jump.
  - Nothing moves when an error shows: the theme gives every field a reserved, empty line under its input that its error message fills. A field that never shows an error can drop it with `containerStyle={NO_STATUS_LINE}`.
- **Tests:** every component and page gets a Vitest test next to it (`X.test.tsx`). Use `renderWithApp` / `renderApp` and `mockFetch` from `src/test/render.tsx`. Synthetic data only.
- **Before calling a change done:** `npm run lint`, `npm run typecheck` and `npm test` pass, and for a visible change, capture the UI parity screenshots of just the screens you changed (`npm run parity:react -- -g "<screen>"` in `tools/ui-parity`). The full set runs once, when merging (root `AGENTS.md`).
- **Versions:** `@astryxdesign/*` are pinned exactly (plan Q14). Upgrade them only in their own PR, after reading the changelog, then run `npx astryx upgrade` and `npm run theme:build`.

## Astryx reference (generated by `astryx init`)

<!-- ASTRYX:START -->
Astryx v0.7.15 · 174 components
CLI: run every command as `npx astryx <cmd>` (shown below as `astryx ...`).

SETUP (once, in app entry e.g. main.tsx) — without these, components render unstyled:
  import "@astryxdesign/core/reset.css";
  import "@astryxdesign/core/astryx.css";

WORKFLOW — start every page from a template, never from scratch:
1. `astryx build "<idea>"` — names the template to scaffold from and the parts it lacks.
2. `astryx template <name> <path>` — scaffold it; keep its frame, gap and padding; replace the content.
3. `astryx template <Block>` for parts it lacks; `astryx component <Name>` to read props before using one.
Changing an existing page? Keep it: skip step 2 and add blocks/components inside its sections.
Before you deliver a build, run `astryx doctor --built <dir>`; don't deliver it if it fails.

RULES:
- No <div> — components handle all layout and spacing.
- Read `astryx docs layout` before changing a template's frame.
- Dense data = rows (Table, List/Item), not Card-wrapped lists. Badge = counts only.
- Style with component props first, then tokens (var(--color-*|--spacing-*|--radius-*)). No raw hex/px. No StyleX/Tailwind compiler here — don't use xstyle/utility classes directly. Some templates use StyleX and need a compiler plugin; see `astryx docs styling-overview`.
- Palettes and custom colors go through the theme, never :root overrides. Run `astryx theme add <slug>` to import; `theme eject <slug>` only forks source. See `astryx docs theme`.
- SELF-CHECK: re-read the file; replace any raw <div>/<span>, imported .css/@apply, or hardcoded #hex/px with the component or a token.

`astryx help` lists every command. Key ones beyond the workflow:
  search "<query>"   find any component / hook / doc / template
  discover <words>   integrations you could add
  docs <topic>       getting-started, principles, tokens, theme … (`astryx docs` lists all)
  docs cli           commands and API reference
  docs authoring     build an integration package, and every field it holds
  upgrade --from <old version> --apply   run after a dependency bump
<!-- ASTRYX:END -->
