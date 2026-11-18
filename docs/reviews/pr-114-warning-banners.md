# Review: PR #114, the record banners shortened to a label, with the whole message on hover, focus or tap

**PR:** [#114](https://github.com/Aliator/MedApp/pull/114), branch `feature/warning-banners`. Reviewed at `cbef96f`, compared with `main` at `894c1fe` (the merge base). `main` has since moved (#112 and #113); the branch merged it in `efe2335`, keeping #113's "Requires Appointment" in the booking dialog's test, and the hazard log's version for this change became 0.13, after #113's 0.11 and 0.12. The findings' fixes are in `647288f`.

**Reviewed:** 18 November 2026, as an independent, adversarial review, without the author's conclusions. Each claim below was checked against the code, Astryx 0.7.15's own source (`node_modules/@astryxdesign/core/src`), CI's results, or temporary Vitest probes (written, run and deleted). No browser, AppHost or Docker was started, by the project's rules. **No finding is high or critical:** 3 are medium and 4 low.

## Summary

The yellow and blue banners in a patient's page headers (restricted, disputed, anonymised), the retention review's specialty warning and a read-only procedure's warning now show only a short label, such as "Restricted record". The whole message opens in an overlay on hover, keyboard focus, a click or a tap, and screen readers still get it in full. The component is sound, its tests are real, and server rendering is safe. The weak points are that the overlay opens inside the screen reader alert, that the reason and what's still allowed are now off screen for a clinical safety control without the clinical safety officer (CSO) being asked, and that the hazard log now claims more than the pages do.

## What changed, and why

16 files, 335 lines added and 54 removed: 10 in `MedApp.Web/src`, `MedApp.Web/AGENTS.md`, and the hazard log.

- **Some terms first:**
  - **A standing message** is one that stays while a state lasts (a restricted record), as opposed to an error from one action (a failed save).
  - **A hover card** is a small panel that opens over the page when you point at something, and stays while the pointer rests on it. Like a luggage tag: the front shows a name, and you turn it over for the details. Astryx's `useHoverCard` makes one and positions it under the label.
  - **An alert** (`role="alert"`) is the page's loudspeaker: when something appears in it, a screen reader reads it out at once, interrupting whatever it was saying.
  - **`aria-describedby`** is a caption a screen reader reads after a button's name.
  - **WCAG 1.4.13** (content on hover or focus) says such an overlay must be dismissible (Escape closes it without moving), hoverable (you can move onto it) and persistent (it doesn't vanish by itself).
- **`SummaryAlert`, a new shared component** (`src/components/shared/SummaryAlert.tsx`):
  - An Astryx `Banner` in the status's colours, whose title is a small ghost button showing the label, with a down chevron (`:60-75`).
  - The button's name is "<label>, show details" (`:64`). Its visible words come first, so voice control users can say what they see (WCAG 2.5.3).
  - The whole message and its details (reason, since when) are in the banner as visually hidden text, which also describes the button (`:69, 76`). A warning or error banner is an alert, as `AlertMessage`'s are (`:51, 57`).
  - The overlay opens on hover (after 300 ms), on keyboard focus (at once), on a click, and on a tap (`touchTrigger: 'tap'`, `:50`). The click handler reopens it after Escape, and skips touch clicks, which the hover card's own tap handling toggles (`:24-28, 71-73`).
  - The overlay is at most 32rem wide, never wider than the screen less a margin (`:21-22`).
- **Where it's used:**
  - `PatientFlagBanners`: "Restricted record" (warning), "Disputed record" and "Anonymised record" (info). On the patient's page, Edit Patient, the patient's procedures, the procedure page, and the appointment dialogs.
  - The procedure page's read-only reason, when the procedure carries no patient flags: "Read-only procedure" (`ProcedurePage.tsx:324`).
  - The Retention Review page's warning: "Specialty retention periods not applied" (`RetentionReviewPage.tsx:142`). The anonymise dialog keeps the full warning (`AnonymiseDialog.tsx:97`).
  - Errors (a failed load or save) stay full `AlertMessage`s.
- **Why:** the full restricted message is about 260 characters plus the admin's reason, and it took most of the header beside the title. A label keeps headers small and the same height.
- **Tests:**
  - `SummaryAlert.test.tsx` (new, 9 tests): closed by default, the alert and the description hold the whole message, info isn't an alert, hover (and moving onto the card), focus then Escape then Enter, click, tap, focus leaving, StrictMode. Astryx takes Escape only while the layer matches `:popover-open`, which jsdom never does, so the tests stub `Element.matches` for that selector (`:26-33`).
  - A helper, `openAlertDetails(user, label)` (`src/test/render.tsx:195-203`), clicks the label and returns the overlay. Seven page and component test files use it, looking inside with `within`, so they can't match the hidden copy by accident.
  - A server rendering test: the label, `aria-expanded="false"` and the hidden message are in the HTML, and the overlay isn't (`entry-server.test.tsx:601-616`).
- **Docs:** `MedApp.Web/AGENTS.md` describes `SummaryAlert` and when to use it (standing messages, not errors). The hazard log rewords H-22's "Everyone sees why" control and H-03's disputed banner, and adds version 0.11 with no rating changed.

## Design choices

- **A label with an overlay, rather than the full banner:**
  - **Instead:** keep the full text, perhaps with a shorter wording.
  - **Chosen because:** the header stays one small line, the same on every page about the patient, and the state is named at a glance.
  - **Worse at:** the reason, what can still be done, and what's disputed are no longer read without reaching for them (finding 2). On a touch screen nothing hovers, so people must think to tap a label.
- **An overlay, rather than Banner's own folding content:**
  - **Instead:** Astryx's `Banner` can fold children behind an Expand toggle, a *disclosure* (click to show, click to hide, the page moves down).
  - **Chosen because:** the overlay never pushes the page down or changes the header's height, and it opens on hover too.
  - **Worse at:** it covers whatever is under it while open, and it opens on every keyboard focus, so a keyboard user tabbing through the header gets a card over the content each time (gone 200 ms after focus moves on). A disclosure only opens when asked.
- **A hover card, rather than a tooltip or a click-only popover:**
  - **A tooltip** holds a line of text, not a message with details.
  - **A click-only popover** surprises nobody, but is found only by clicking.
  - **The hover card** opens on hover, focus, click and tap, meets WCAG 1.4.13 by Astryx's design, and is named as a dialog ("Restricted record: details").
  - **Worse at:** a dialog that opens on focus without taking focus is unusual. The button says `aria-haspopup="dialog"`, so a screen reader user may expect to land in it; they don't need to, as the description already reads the whole message.
- **The whole message kept in the page for screen readers,** rather than only in the overlay:
  - **Chosen because:** a screen reader user hears the restriction in full without discovering an overlay.
  - **Worse at:** the alert holds the label and then the message, which starts with the label again, so it reads "Restricted record, show details. Restricted record: nothing about…". And the overlay, when open, is inside the same alert (finding 1).
- **A shared component, rather than shortening `PatientFlagBanners` alone:** the project's rule (`MedApp.Web/AGENTS.md`) is to build patterns, not page wiring. It also means the next standing warning will be shortened by default, so each use needs its own judgement about what's safe to hide.

## How it was checked

- **Read:**
  - the diff, the PR description, and the code around it: `PageHeader`, `PatientBanner`, `AppointmentPatient`, `BookAppointmentDialog`, `EditPatientPage`, `EditProcedurePage`, `NewProcedurePage`, `ProceduresListPage`, `ViewPatientPage`, `AnonymiseDialog`, `src/app/colours.tsx`, the MedApp theme (`src/themes/medapp/medapp.css`)
  - Astryx 0.7.15: `Banner.tsx`, `Button.tsx`, `useHoverCard.tsx`, `useLayer.tsx` (where the overlay is put in the page), `useTouchTrigger.ts`, `Dialog.tsx` (initial focus)
  - the hazard log (H-01, H-03, H-19, H-22), `docs/governance/data-subject-rights.md`, `docs/clinical-safety/clinical-safety-case-report.md`, `tools/ui-parity/screens.ts`
- **Ran:**
  - `npx vitest run` on all 9 changed test files: 229 tests passed.
  - `npm run typecheck`: clean.
- **Temporary probes,** written, run, then deleted (`SummaryAlert.probe.test.tsx`):
  - **Where the open overlay sits:** inside the `role="alert"` element (true). With it open, the alert's text holds the message twice. `aria-controls` points at the overlay.
  - **Two taps:** user-event's taps, a tap whose click is a plain `MouseEvent`, and one whose click reports `touch`: each opens, then closes.
  - **Hover, then away:** closes.
  - **Without the touch guard** (`if (!isTouchClick(event))` removed, then restored with `git checkout`): every `SummaryAlert` test and probe still passes (finding 5).
- **Arithmetic from the styles,** not a browser: the label's room at the header's narrowest (finding 4), and the label's contrast in light and dark mode (finding 6).
- **CI:** all 14 checks pass at `cbef96f` (run 44220527541, with CodeQL and Trivy).
- **Not done:** no browser, axe audit, UI parity screenshots, screen reader or touch device. The PR says the browser and axe checks come before merging.

## Findings

### 1. The overlay opens inside the screen reader alert, so the warning may be read out again each time it opens

- **Severity:** medium. **Confidence:** medium. The structure is certain (shown with a probe); how each screen reader treats a popover added to an alert wasn't tried.
- **What could go wrong:**
  - A warning `SummaryAlert` passes `role="alert"` to `Banner`, which puts it on the element holding the title (`Banner.tsx:721`). The title holds the button, the hidden message, *and* the overlay.
  - The overlay is rendered in place, next to the button, not elsewhere in the page (`useLayer.tsx:1029-1034`: a safe parent keeps it inline). So each time it opens it's added inside the alert.
  - An alert is *assertive* (it interrupts) and *atomic* (any change makes the whole alert be read again) by ARIA's defaults.
  - **So:** a keyboard user with NVDA or JAWS tabs to "Restricted record". The screen reader starts reading the button and its description (the whole message), the overlay opens, and the alert interrupts to read the label, the hidden message and the overlay's copy. A low-vision user with a magnifier and speech hears it twice more every time the pointer rests on the label.
  - People who hear a warning three times on every pass learn to skip it, which is the opposite of what a safety banner wants.
  - Astryx's own Banner says the alert should cover "the authored message only, so controls, their tooltips and the content area stay out of what is announced" (`Banner.tsx:643-645`). Putting a button and an overlay in the title goes around that.
- **Evidence:** `SummaryAlert.tsx:57-87`; `Banner.tsx:643-645, 721-733`; `useLayer.tsx:1029-1034`; the probe.
- **Fix:** make only the message the alert, so the button and the overlay sit outside it:
  - Drop `role` from the `Banner`, and put it on the visually hidden message: `<VisuallyHidden id={fullId} role={isUrgent ? 'alert' : undefined}>…</VisuallyHidden>`. It's still inserted with the banner, so it's still announced when the restriction appears, and it still describes the button.
  - Add a test: open the overlay, then `expect(screen.getByRole('alert')).not.toContainElement(card)`.
  - The existing tests that read the alert's text keep passing (it holds the same message).
- **What happened to it:** fixed in the PR (`647288f`). `role="alert"` is now on the visually hidden message only (`SummaryAlert.tsx`), so the button and the open overlay sit outside the alert. New tests: the open card and the button aren't in the alert, and the alert's text is the message once (`SummaryAlert.test.tsx`); the server rendering test checks the alert starts at the message (`entry-server.test.tsx`).

### 2. The reason, what can still be done, and what's disputed are now off screen: a weaker control for H-22, not put to the CSO

- **Severity:** medium. **Confidence:** high that the text is hidden; medium on how much it matters clinically, which is the CSO's call.
- **What could go wrong:**
  - **Restricted:** a nurse opens a procedure for a restricted patient to record a pre-assessment. Before, the header said that nothing can change until an admin lifts the restriction, that the procedure can still be blocked or cancelled with a reason, why it was restricted, and since when. Now it says "Restricted record", the stage actions are gone, and only Block and Cancel remain. Who to ask and why is a hover or a tap away. On a tablet, nothing hovers, and the chip doesn't look like it hides a paragraph.
  - **Disputed:** the patient disputes their date of birth. The patient banner shows that date of birth, the identifier staff check before working (H-01), with a small "Disputed" token. What's disputed used to be on screen beside it; now it's behind "Disputed record".
  - **The appointment dialog** hides its own refusal for a restricted patient because "the patient's restricted or anonymised banner above already says so" (`BookAppointmentDialog.tsx:548-551`). The banner now says only "Restricted record", so the dialog shows disabled Book buttons with no reason on screen.
  - **The hazard log:** H-22's control is still headed "Everyone sees why" (`hazard-log.md:869`), though everyone now sees *that*, and *why* on demand. Version 0.11 says "No rating changed" (`:1470`), but nothing asks the CSO to accept a control moved behind an interaction. The safety case report lists "banners" as an H-22 control (`clinical-safety-case-report.md:160`).
  - **Not affected:** the state itself is still named on those pages (the label, and the patient banner's token), the actions are still limited by the API, and the anonymise dialog keeps the full retention warning at the step that can't be undone.
- **Evidence:** `PatientFlagBanners.tsx:34-61`; `ProcedurePage.tsx:313-325`; `BookAppointmentDialog.tsx:548-551`; `hazard-log.md:869, 185, 1470`.
- **Fix:** both of these, or the second at least:
  - **Put the most useful words in the label,** short enough for one line: "Restricted record: read-only" (or "only Block and Cancel"), and for a dispute, "Disputed record: <what's disputed>" when the admin's text is short (say 40 characters), the overlay keeping the rest. Restore the appointment dialog's refusal for restricted patients, or give it a one-line reason.
  - **Record it as a change to the control,** for the CSO: rename H-22's control ("Everyone sees that the record is restricted; the reason and what can still be done on demand"), say the reason moved behind the label, and ask the CSO to accept it. Note under H-01 or H-02 that what's disputed (perhaps an identifier) is now on demand.
- **What happened to it:** fixed in the PR (`647288f`), with the control's change documented for the CSO to accept.
  - **The label carries the key fact** (a new `summary` prop): "Restricted record: changes paused" (blocking and cancelling stay possible, as the overlay says), "Disputed record: <what's disputed>" when the admin's words are at most 40 characters (`DISPUTE_IN_LABEL_MAX`, "Disputed record" otherwise), "Anonymised record".
  - **The booking dialog shows its own refusal again** for a restricted or anonymised patient (`BookAppointmentDialog.tsx`), with its test.
  - **The hazard log** renames H-22's control to "Everyone sees that the record is restricted, and why on demand", says the reason and the exceptions moved behind the label, and asks the CSO to accept it; H-03 notes that what's disputed may now be on demand; version 0.13 records it, with no rating changed.

### 3. The safety and governance documents now claim more than the pages show

- **Severity:** medium. **Confidence:** high (each checked in the code).
- **What could go wrong:** the CSO, or an auditor, accepts H-22 on the strength of a claim the app doesn't meet.
  - **"In the header of every page about the patient"** (`hazard-log.md:869`, new in this PR; before, it listed three pages). Edit Procedure, New Procedure and the printable export show the patient banner but no flag banners (`EditProcedurePage.tsx:80-104`, `NewProcedurePage.tsx:145-154`). For a restricted patient, Edit Procedure even says "This procedure is in progress, so its details can't be edited" (`EditProcedurePage.tsx:82-87`: `canEditDetails` is false, and the message gives the status as the reason; from before this PR).
  - **"Screen readers are read it in full as an alert"** (`:869`) is garbled, and true only when the alert appears: after restricting the record in the page, or arriving by a link inside the app. On a fresh page load, screen readers don't announce alerts already in the HTML; the user meets the message on reaching the label (its description).
  - **The governance pack** says staff see the reason and what's disputed on a banner (`data-subject-rights.md:38, 40`). They now see a label, with the reason on demand. `AGENTS.md` asks for `docs/governance/` to stay true in the same PR.
  - **Comments in the code** still describe the full text: `PatientBanner.tsx:46` ("the full `PatientFlagBanners`"), `EditPatientPage.tsx:166` ("a disputed one says what the patient disputes"), `BookAppointmentDialog.tsx:548`.
- **Evidence:** as listed.
- **Fix:**
  - H-22: list the pages and dialogs the label is on, and say the patient banner's token covers Edit Procedure, New Procedure and the export. Better, add `PatientFlagBanners` to Edit Procedure and New Procedure (the procedure already carries `patientFlags`), and give Edit Procedure the read-only reason for a restricted record.
  - Reword the screen reader sentence: "screen readers get the whole message as the label's description, and as an alert when it appears".
  - Update `data-subject-rights.md` rows 38 and 40, and the three comments.
- **What happened to it:** fixed in the PR (`647288f`).
  - **The label is now on Edit Procedure and New Procedure** (`PatientFlagBanners` in their `message`), and Edit Procedure gives the procedure's `readOnlyReason` (the restriction) instead of its status as why it can't be edited; tests for both.
  - **H-22 lists exactly where the label shows,** and that the printable export shows only the `PatientBanner`'s token.
  - **The screen reader sentence** now says what's true: the whole message is the label's description, and an alert when the banner appears in a page already open; on a fresh load it isn't announced.
  - **`data-subject-rights.md`** rows 38 and 40, and the comments in `PatientBanner.tsx`, `EditPatientPage.tsx` and `BookAppointmentDialog.tsx`, describe the label and the overlay.

### 4. The label can be cut short with "…" where the header leaves the message its least room

- **Severity:** low. **Confidence:** medium (worked out from the styles; not seen in a browser).
- **What could go wrong:**
  - Astryx's `Button` never wraps: one line, and an ellipsis when it doesn't fit (`Button.tsx:133-140`). The old banner wrapped its words instead.
  - `PageHeader` gives the message at least 12rem beside the title and actions before it wraps (`PageHeader.tsx:35, 83`).
  - At that minimum, after the banner's padding (2rem), its icon (1.25rem) and gap (0.5rem), and the button's padding (1.5rem), gap (0.5rem) and chevron (1rem), about **5.25rem (84 px)** is left for the words. All of it is in rem, so it's the same at any text size.
  - "Restricted record" needs about 105 px at the label size (14 px), so it shows as "Restricted r…". "Read-only procedure" shows as "Read-only pr…". **"Specialty retention periods not applied" shows as "Specialty re…",** which says nothing.
  - That minimum is a normal case: the parity screen `procedure-restricted` exists because, at 1366 px, a restricted procedure's banner sits beside a title and a row of actions.
  - Screen readers and voice control get the full name; only the eye is short-changed.
- **Evidence:** `Button.tsx:133-140`; `PageHeader.tsx:35, 83`; `Banner.tsx:241-251`; the arithmetic in Detail.
- **Fix:** check `procedure-restricted` and the Retention Review page in the browser. Then shorten the long labels ("Specialty periods", "Read-only"), or let the label wrap (a `Text` label with a separate small "Show details" button, or the button's text allowed to wrap).
- **What happened to it:** fixed in the PR (`647288f`). The label wraps instead of being cut short: `LABEL_STYLE` on the button (`white-space: normal`, `overflow-wrap: anywhere`, `height: auto` with the small element height as a minimum, `text-align: start`), layout values only, which undo Astryx's one-line `nowrap` and fixed height so the label's own `overflow: hidden` never has a line to clip. jsdom has no layout, so the test checks those styles reach the button; a narrow header in a browser is left to the parity screens (finding 7).

### 5. The touch guard relies on a click's pointer type, which iOS reports as "mouse", and no test checks it

- **Severity:** low. **Confidence:** medium (WebKit's bug report; no iPad tried).
- **What could go wrong:**
  - The click handler calls `show()` unless the click came from a finger or pen, read from the click's `pointerType` (`SummaryAlert.tsx:25-28, 71-73`).
  - On iOS Safari 18.2 and later, a tap's `click` reports `pointerType` "mouse", though its `pointerdown` says "touch" ([WebKit bug 282988](https://bugs.webkit.org/show_bug.cgi?id=282988), still open). Before 18.2, iOS clicks weren't pointer events at all ([WebKit bug 239810](https://bugs.webkit.org/show_bug.cgi?id=239810)).
  - So on an iPad, a tap on an open label closes it (the hover card's tap handling) and then asks it to open again. In jsdom the second request does nothing, so the probes close; whether a real layer reopens mid-close wasn't tried. The worst case is a label that a second tap can't close (a tap outside, or Escape, still does).
  - **No test covers the guard:** with it removed, every test still passes (probe).
- **Evidence:** `SummaryAlert.tsx:25-28, 71-73`; `useTouchTrigger.ts` (the tap toggles on the press's own click); the mutation probe.
- **Fix:** remember the last press's type, not the click's: `onPointerDown={(e) => { lastPointer.current = e.pointerType; }}`, and in `onClick`, skip when it was `touch` or `pen`. Add a test that taps twice and expects the overlay closed, with a click that carries no pointer type.
- **What happened to it:** fixed in the PR (`647288f`). The click handler no longer reads the click's pointer type: it records whether the card was open when the press began (`onPointerDown`) and only opens it if it wasn't, so a tap that closes it (the hover card's own tap handling) is never undone, whatever iOS reports. A click with the mouse or keyboard still opens it (after Escape too). New tests simulate iOS's tap (a `touch` press, then a click that says `mouse`) and a click on an open card, and check nothing asks the card to open again (a counting wrapper round `useHoverCard`); both fail with the guard removed (checked by removing it).

### 6. The label's colour isn't the banner's own, and the overlay's edge is faint where page and card colours match

- **Severity:** low. **Confidence:** high (from the theme's values).
- **What could go wrong:**
  - **The label:** a ghost button paints its text in `--color-text-primary` (`Button.tsx:318-321`). Banner's own buttons are switched to the status's text colour (`statusControlStyles`, `Banner.tsx:427-440, 786`); `SummaryAlert`'s isn't. In MedApp's theme it still reads well (about 11.8:1 in light mode, 6.5:1 in dark, on the warning yellow), but it isn't the banner's colour (`--color-on-warning-muted`), and a theme whose status fill differs from the page could make it unreadable.
  - **The overlay:** Astryx paints a hover card with `--color-background-surface` (`useHoverCard.tsx:49`), which MedApp's colour themes set to the *page* colour (`colours.tsx:190-191`), while menus use the card colour (`:194`). Over a card in dark mode, or in a theme whose page and card match (Y2K dark), only a dark shadow marks the overlay's edge. Its text stays readable.
- **Evidence:** as listed; `medapp.css:280-283, 481-484`.
- **Fix:** give the button the status text colour through the token: ``style={{'--color-text-primary': `var(--color-on-${status}-muted)`}}``. Give the overlay a border (`--color-border`), or `--color-background-popover`, through `renderHoverCard`'s `style`.
- **What happened to it:** partly fixed in the PR (`647288f`). The overlay is painted with `--color-background-popover`, as menus and popovers are (`CARD_STYLE`), with a test. The label keeps the ghost button's `--color-text-primary`, which passes in MedApp's theme (11.8:1 light, 6.5:1 dark); giving it the status colour would mean setting a colour token in a `style`, which the project keeps to layout values, so it's documented here rather than changed.

### 7. Nothing has looked at it in a browser yet, and the parity screens still describe the long banner

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** the parts jsdom can't show (the overlay's position near the right edge and on a phone, the top layer inside the modal appointment dialogs, the truncation in finding 4, the colours in finding 6) ship unseen.
  - `AGENTS.md` asks for a visible change to be looked at, with parity screenshots and the axe audit of the screens it touches. The PR says this comes before merging.
  - No parity screen shows the overlay open, and the two restricted screens are titled for "the long restricted banner" (`tools/ui-parity/screens.ts:367, 377`).
- **Fix:** add a screen that focuses "Restricted record, show details" on a restricted procedure (and one at phone width); retitle the two; capture `procedure-restricted`, `procedure-restricted-half`, `patient-view` and `admin-retention`, and run the axe audit on them.
- **What happened to it:** partly fixed in the PR (`647288f`). The two restricted screens are retitled for the label, and two screens are added: `procedure-restricted-details` (the label focused, the overlay open, at 1366 px) and `procedure-restricted-phone` (the label tapped at 390 px). Capturing them and the axe audit still need the AppHost, so they're left for the pre-merge browser check.

## Remaining risks

- **Screen readers and touch, untried:** how NVDA, JAWS and VoiceOver treat the alert, the description and the dialog-on-focus, and how an iPad treats the tap, are reasoned from the code, not heard or touched.
- **A long reason on a short screen:** a 500-character reason (the limit, `SetPatientFlagValidator.cs:7`) at 200% text on a phone held sideways makes the overlay scroll. Its content has nothing focusable, so a keyboard can't scroll it. Screen readers still have the description.
- **From before:** on the procedure page, Edit Patient and the patient's procedures, an error replaces the flag banners in the header (`ProcedurePage.tsx:313-321`, `EditPatientPage.tsx:160-167`, `ProceduresListPage.tsx:237-242`). While an error shows, only the patient banner's token says the record is restricted. Now that the label is small, both could show.
- **Printing:** the overlay isn't printed, so a printed page shows only the label. No print styles were checked.
- **The pattern will spread:** `MedApp.Web/AGENTS.md` now sends every long standing warning to `SummaryAlert`. Each new use hides its detail by default, and needs its own judgement about whether that's safe.

## Say it like this

"The yellow warnings at the top of a patient's pages were long paragraphs that crowded the header, so we cut them to a label like 'Restricted record', with the full text in a card that opens when you point at, tab to or tap the label, while screen readers still get the whole message. The trade-off is that the reason, and what you can still do, are now one gesture away instead of on screen, and for a clinical safety warning that's a decision for the clinical safety officer, not only a layout choice."

## Detail

### Where the overlay lands (finding 1)

```text
Banner frame
└─ header (yellow)
   ├─ icon (aria-hidden)
   └─ div role="alert"            ← Banner puts the alert here (Banner.tsx:721)
      └─ title div
         ├─ button "Restricted record, show details"
         ├─ span (visually hidden): the whole message
         └─ template marker, then, while open:
            div popover role="dialog" "Restricted record: details": the message again
```

The probe's alert text, with the overlay open: `"Synthetic stateSynthetic standing warning: nothing here can be changed. Reason: xSynthetic standing warning: nothing here can be changed.Reason: x"`.

The suggested shape: no role on the `Banner`, and `role="alert"` on the visually hidden span only. The overlay then sits beside the alert, not in it.

### The label's room at the header's narrowest (finding 4)

| Part | Width |
|---|---|
| The message's minimum (`MESSAGE_MIN_INLINE`) | 12rem |
| Banner padding, both sides (`--spacing-4`) | −2rem |
| Status icon (`md`) and gap (`--spacing-2`) | −1.75rem |
| Button padding, both sides (`--spacing-3`) | −1.5rem |
| Gap and chevron (`sm`) | −1.5rem |
| **Left for the words** | **5.25rem, 84 px at the default size** |

The label is 14 px (`--text-label-size`), medium weight. Estimated widths: "Restricted record" about 105 px, "Disputed record" 95, "Anonymised record" 115, "Read-only procedure" 120, "Specialty retention periods not applied" 240.

### Contrast of the label (finding 6)

Inside a warning banner, MedApp's theme sets the fill to `--color-background-yellow` (`#f4e1b7` light, `#615620` dark; `medapp.css:280, 481-484`).

| Mode | Label now (`--color-text-primary`) | Banner's own text (`--color-on-warning-muted`) |
|---|---|---|
| Light | `#25252a`: about 11.8:1 | `#524818` |
| Dark | `#f0f0f9`: about 6.5:1 | `#f1e1b9` |

Both pass WCAG's 4.5:1. Outside a banner, MedApp's dark `--color-warning-muted` is a light tan (`#d9c794`, `medapp.css:238`), where `#f0f0f9` would be about 1.5:1, which is why the banner's own buttons take the status colour rather than relying on the page's.
