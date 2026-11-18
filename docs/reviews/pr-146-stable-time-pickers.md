# Review: PR #146, time and option pickers opened in a fixed place

**PR:** [#146](https://github.com/Aliator/MedApp/pull/146), branch `fix/stable-time-pickers`, reviewed on 18 November 2026. A quick review of `AnchoredSelector.tsx`, `eslint.config.js` and two call sites (`StaffPicker.tsx`, `MonitoringPage.tsx`); nothing was run. No finding is high or critical.

Astryx's `Selector` laid its list over the field, positioned from the list's last `scrollTop`, so after scrolling and closing, the next open placed the popup somewhere else. `AnchoredSelector` and `AnchoredMultiSelector` pass `placement="below"` and, on each open, scroll the list to the selected option (or the first); every `Selector` and `MultiSelector` in the app uses them, and an ESLint `no-restricted-imports` rule refuses the plain ones.

**Checked:** the MutationObserver lives in a callback ref that disconnects it and cancels its frame on clean-up (`AnchoredSelector.tsx` about 84-99); it watches only the trigger's `aria-expanded`, so scrolling can't retrigger it; `document`, `MutationObserver` and `requestAnimationFrame` are touched only after mount, so server rendering is safe; call sites keep their `value`, `onChange` and `label`; Astryx's ARIA and keyboard handling are untouched.

**Finding, low, left:** with no popover or dialog ancestor, `scrollerOf` (about lines 11-15) checks only the listbox and doesn't walk up to a scrolling parent. Harmless today.

**Remaining risk:** the scroll-on-open is a workaround for Astryx `Selector` having no open callback; an Astryx upgrade that adds one should replace it.
