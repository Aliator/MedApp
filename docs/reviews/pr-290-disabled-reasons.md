# Review: PR #290, disabled buttons' reasons open on tap or click

PR: https://github.com/Aliator/MedApp/pull/290, branch `claude/eloquent-feynman-wysbc1`, commit reviewed `97e115779625eed71462fb1df258c784b82d7fb4`, 2026-11-24. Quick review (diff only, nothing run).

**What it does:** adds `DisabledReason` (`MedApp.Web/src/components/shared/DisabledReason.tsx`), a span wrapper using Astryx `useTooltip({touchTrigger: 'tap'})`. It shows the reason on click as well as hover, and adds the tooltip id to the child's `aria-describedby`. It replaces the `tooltip` prop on nine buttons (data request review, notepad, timeline pathway chips, stage work, list tabs, security page, scheduled jobs, letter submit and approve). The `ListTabsDialog` and `ScheduledJobsPage` buttons keep their own "Remove" and "Run ... now" tooltips when enabled.

**Findings:** none. No critical, high, medium or low issues.
- Every `isDisabled` condition is unchanged, and the wrapper only calls `show()` from capture handlers, with no `preventDefault` or `stopPropagation`. A click can't reach a handler it couldn't before, and an enabled button gets `reason` undefined, so the wrapper does nothing.
- The diff has no backend, migration, enum or data change.

**Remaining risk:** the wrapper adds an `inline-flex` span around each button. This could change layout inside the `HStack`/`StackItem` rows, and the wrapper sets `aria-describedby` on `firstElementChild` by DOM lookup. Both are cosmetic, and the PR's own screenshot check should cover them.
