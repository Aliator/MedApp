# PR #204: The pathway page's layout, and no patient banner on it

https://github.com/Aliator/MedApp/pull/204 (`feature/pathway-layout`), reviewed 18 November 2026 (quick review).

**Layout (`PathwayView`):**
- **Wide:** the left `FoldingCard` holds Details (patient, status tokens, Patient info) and then the Current stage, so the current stage starts on the first screen at 900 to 1000 px tall. The right card holds Stages (folded into their progress bar, closed), Appointments, Assigned staff, Referral and wait, Notes, MDT, Specimens, Results, Consent, Files and Letters.
- **Narrow** (phone, half view, beside the docked notepad): one card in the same order.
- **No current stage:** Stages takes the Current stage's place on the left.
- Folding state stays in the page across layout changes.

**Banner removal:** `PatientBanner` is gone from `/pathways/$id` (the owner's decision). `PathwayPatient` shows the name (surname in capitals, linking to the patient), date of birth with age and NHS number (spoken a digit at a time) at the top of Details. Details is a `canFold: false` section and always first. The flag messages stay in the page header.
- `patientBanners.test.tsx` names the page as its one exception and checks Details instead.
- The hazard log's H-01 and H-02 controls are updated.
- `docs/todo.md` (Testing) asks for a clinical safety officer's review before first live use.

**`FoldingCard`:** sections take `canFold` (false: always open, no chevron, the bar isn't clickable) and `summary` (`PanelHeader`'s new `summary`, after the title). The Stages bar's summary is the "Stages finished" `ProgressBar` (label visually hidden) with `progressWords`. The whole bar toggles, and its chevron carries `aria-expanded`.

Findings: none blocking.

Remaining risk: in a half view or a narrow window the one card scrolls, so Details, and with it the patient's identity, can scroll out of view while work is recorded further down. The same happens in the wide layout once the left card scrolls. Recorded in H-01 and H-02 for the CSO's review.
