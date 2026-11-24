# Review: PR #291, lower limb clinic

- PR: https://github.com/Aliator/MedApp/pull/291
- Branch: `claude/eloquent-feynman-wysbc1`
- Commit reviewed: `adb6394068bd2d8522517aea5476ee77723cb483`
- Date: 2026-11-24
- Diff: `git diff origin/main...HEAD -- . ':!MedApp.Web/src/routeTree.gen.ts'`, read only

## What the PR does

Adds a Lower limb biomechanics clinic to the light demo's synthetic seed (team, admin account, template, one patient, one pathway, two appointments, clinic blocks). Adds a Custom Orthotics card on the patient page, which reads only from the demo module behind `__LIGHT_DEMO__` and a dynamic import (empty outside the demo). Adds a placeholder orthotic details page and its route. Changes the touch nav so that "always show side nav menu buttons" turns the touch nav off (`useIsTouchNav` and `RowMenu`).

## Findings

None at critical, high, medium or low severity from the diff.

Checked and found clean:
- Demo code is behind `__LIGHT_DEMO__` and a dynamic import in `CustomOrthotics.tsx`. The `clinicTemplates.ts` import of `lowerLimb.ts` is inside `src/demo`, so it reaches the production build only if `src/demo` is imported statically from production code, which the diff does not show.
- The one new NHS number, `9991065008` (`joelBrannock`), is in the 999 range, and its check digit is valid (mod 11 sum 300, remainder 3, check digit 8).
- No stored enum is renumbered (no C# changes).
- The placeholder page loads no patient data, so no read needs logging.
- The new route `orthotics.$orthoticId.tsx` matches the link built in `CustomOrthotics.tsx`.
- The new IDs in `ids.ts` do not collide with existing IDs in the diff.

## Remaining risk

- `MedApp.Web/src/routeTree.gen.ts` was excluded from the diff, so the new route is not confirmed registered. A build that regenerates the route tree would cover it; check the build before merging.
- `useIsTouchNav` changed its meaning for every caller. Callers outside the diff are not visible here. A caller that is not under the `useNavSettings` provider would throw, and the new behaviour for them is unverified.
- The demo's synthetic names and dates are not checked beyond the diff.
- Nothing was run: no build, tests or browser check.
