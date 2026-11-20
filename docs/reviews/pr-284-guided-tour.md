# PR #284 quick review: guided tour for the light demo

- PR: https://github.com/Aliator/MedApp/pull/284, branch `claude/eloquent-feynman-wysbc1` into `main`
- Commit reviewed: `d82a70e9e77ae086055c6d63059e0e282df7b92e`, 20 November 2026 (quick review, diff only, nothing run)

What it does: adds a guided tour to the light demo (`MedApp.Web/src/demo/coach/`: `CoachTour`, `coachMarks`, `coachState`, `findTarget`), started from a new "Guided Tour" side nav entry (`guidedTourNavEntry` in `demo/readMeNav.tsx`, added in `AppSideNav.tsx` behind `__LIGHT_DEMO__`) or the Read Me; it outlines controls step by step, can switch demo account and open pages, and keeps its position in localStorage `medappLightDemo.coach`. `docs/agents/light-demo.md` updated.

Findings:
- `MedApp.Web/src/demo/readMeNav.tsx:1` (low, open): `readMeNav` is statically imported by `AppSideNav.tsx:40` (existing pattern) and now statically imports `coach/coachState`. Its use is inside the `__LIGHT_DEMO__` branch, and the module is small and side-effect free, so the bundler should drop it from the production build; strictly the AGENTS.md rule asks for demo code by dynamic import. Worth confirming the production bundle holds no `medappLightDemo.coach` string. Checked by the author: `vite build` (production) client output grepped for `medappLightDemo.coach`, `Guided Tour`, `Do It for Me` and `data-coach-card`, none found; left open as a pattern to move to a dynamic import later.

No data loss, migration, stored enum, backend, CI or deploy change. The localStorage entry holds only `{step, mark, opened?}` (tour position), no patient or synthetic record data, and only in the demo build.

Remaining risk: low; the production-bundle exclusion relies on dead-code elimination of the constant-guarded branch rather than a dynamic import.
