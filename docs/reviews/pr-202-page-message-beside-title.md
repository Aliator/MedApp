# PR #202: Page messages beside the title, and SummaryAlert's icon trigger

https://github.com/Aliator/MedApp/pull/202 (`feature/page-message-beside-title`), head `c12b741d`, reviewed 18 November 2026 (quick review).

`PageHeader`'s message starts right after the title instead of in the middle (still wrapping under it on a phone). `SummaryAlert` drops its leading icon and label button: the label and summary are plain text, and the status's icon at the end (`AppIcon.Error` added for errors) is a ghost `IconButton` named "<label>: <summary>, show details" that opens the hover card on hover, focus, click or tap; the hidden full message and its `role="alert"` for warnings are unchanged. Every use changes with it.

Findings: one low, no change. The trigger is now an icon-only small button, so its target is smaller than the old label's; Astryx's small control height keeps it above WCAG 2.5.8's 24 by 24 px.

Checked: the patient flag labels (H-22) stay on screen as text; a warning is announced once and opening the card doesn't announce it again; the card opens by keyboard and closes on Escape as before; every status has an icon.

Remaining risk: none.
