# PR #184: Light demo on GitHub Pages

https://github.com/Aliator/MedApp/pull/184 (`feature/light-demo`), head `93357e11`, reviewed 18 November 2026 (quick review).

A demo build of `MedApp.Web` (`npm run build:demo`, Vite mode `demo`, base `/MedApp/`, client-only) for https://aliator.github.io/MedApp/. `window.fetch` is patched so `/api` calls go to typed in-browser handlers over IndexedDB (seeded with fixed IDs, Reset reseeds); unhandled endpoints answer 501. Two accounts replace sign-in; live notifications don't connect. A Read Me page (first visit, side nav) has the linked tour and what doesn't work. `.github/workflows/pages.yml` builds and deploys on pushes to `main`.

Findings: one low. A `workflow_dispatch` run could start from another branch; the `github-pages` environment's deployment branch policy already allows only `main`, so no change.

Checked: every demo path is behind the Vite `define` constant `__LIGHT_DEMO__` (`mode === 'demo'`) with dynamic `import()` (entry-client's fetch patch, the login picker, `AppLayout`'s banner and notifications, `AppSideNav`, the router's `basepath`), nothing a visitor can set enables it, and `npm run build`'s client output has no demo markers; the workflow pins every action to a SHA, has top-level `permissions: {}` with `contents: read` for build and `pages: write`/`id-token: write` for deploy, no secrets and no pull-request trigger; the seed's NHS numbers are in the 999 range and its names are invented; the coverage exclusion adds only `src/demo/**`.

Remaining risk: production keeps a `/read-me` route that answers not found; the production marker grep is manual (no CI step yet); handlers and seed were skimmed, not read line by line.
