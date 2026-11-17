# Review: PR #101, the colour warning kept clear of the colour, and the picker opened from anywhere on its row

**PR:** [#101](https://github.com/Aliator/MedApp/pull/101), branch `feature/colour-field`. Reviewed at `ba946b9`, compared with `main` at `1f6f79c` (also the merge base).

**Reviewed:** 17 November 2026, as an independent, adversarial review, without the author's conclusions. Each claim below was checked against the code, Astryx 0.7.15's own files, the HTML standard, CI's results, or temporary Vitest probes (written, run and deleted). No browser, AppHost or Docker was started, by the project's rules. **No finding is high or critical:** all 4 are low, and one of them is from before this PR.

## Summary

On Account Settings, Colours, Custom, each colour field's "hard to read" warning now sits below the colour instead of sliding up behind it. A click anywhere on the field's row now opens the colour picker, not only a click on the swatch or the label. Both work as described, and each click opens the picker once. The gaps are small: a click that ends a text selection (copying the hex value) opens the picker too, the tests leave out two of the six places to click, and the colour input still isn't linked to its warning for screen readers (from before).

## What changed, and why

2 files, 48 lines added and 5 removed: `MedApp.Web/src/components/forms/ColourField.tsx` and its test.

- **The warning covered the colour.**
  - **Why:** Astryx's `Field` can show a status message in three styles. The default, *attached*, is made for bordered text boxes. Its box slides up by half a control's height (16 px, or 22 px with the "bigger targets" setting) and tucks behind the text box, like a tab sewn onto the bottom edge of a folder (`node_modules/@astryxdesign/core/dist/FieldStatus/FieldStatus.js:41, 55-63`; `Field/Field.js:133, 177`).
  - **Under a colour swatch,** there's no border to tuck behind. The yellow box showed behind the lower half of the swatch (32 px tall) and the hex value.
  - **The fix:** `statusVariant="detached"` (`ColourField.tsx:60`). Astryx documents it for controls that aren't text boxes, such as checkboxes, switches and sliders (`Field/Field.d.ts`, `statusVariant`). The message becomes its own box below the colour, in the flow, with a warning icon that screen readers skip.
  - **Side effects:**
    - The attached box ignored the pointer (`pointer-events: none`, `FieldStatus.js:62`). The detached one doesn't, so a click on the warning now reaches the row, and opens the picker.
    - The message sits 8 px lower than before, and the row grows 8 px more when a warning appears.
    - How it's announced doesn't change: both styles speak through the same hook (`FieldStatus/useFieldStatusAnnouncement.js`), politely for a warning.
- **Only the swatch and the label opened the picker.**
  - **The fix:** an `onClick` on the `Field` (`ColourField.tsx:47-51, 61`) calls `input.click()`. That's the colour input's own way in: clicking it opens its picker.
  - **Some clicks already reach the input, and the row leaves them alone:**
    - **The swatch** is the input, and **the label** is a `<label for>`, which hands its click on to the input once the click is done (the browser's default). `closest('input, label')` skips both.
    - **The description:** Astryx forwards a click on it to the input itself (`Field/FieldLabel.js:125-152`), during the same click, before the row sees it. As the forwarded click passes, the input sets `reachedInputRef` (`ColourField.tsx:68-73`), and the row skips. A *microtask* (a job the browser runs as soon as the current script has finished) clears it again.
  - **An analogy:** a doorbell across the whole front of the house, plus a note so that a visitor who has already pressed the button by the door doesn't ring twice.
- **Tests** (`ColourField.test.tsx:18-41`):
  - the warning isn't in the attached style
  - one click each on the description, the hex value, the swatch and the label reaches the input exactly once.

## Design choices

- **Detached, rather than restyling attached in the theme:**
  - **Instead:** keep attached and cancel its pull-up for this field, through its CSS variable (`--_field-status-overlap`).
  - **Chosen because:** detached is Astryx's own style for controls without a border. It needs no CSS, and its icon means the warning isn't shown by colour alone.
  - **Worse at:** the row grows a little more when a warning appears, so the fields below move a little further. They moved before too: ColourField has always dropped the theme's reserved status line.
- **`input.click()`, rather than `showPicker()`:**
  - **What the HTML standard says:** a colour input's activation runs "show the picker, if applicable", also for `el.click()`. That step needs *transient user activation* (a real click or key press a moment ago), and it *consumes* it, so a second open in the same click does nothing. Inside the user's click, the activation is there, so a forwarded `click()` opens the picker in Chrome, Firefox and Safari.
  - **`showPicker()`** shows the same picker without sending a click through the page.
    - **Its costs:** it throws when there's no activation or the input is disabled, older browsers lack it (Safari before 16), and jsdom has none, so tests would need a stub.
    - **What it doesn't remove:** the row would still have to skip the swatch, the label and the description, which open the picker themselves.
  - **Verdict:** `click()` is fine here. `showPicker()` would be slightly cleaner, not safer.
- **A flag cleared by a microtask, rather than a rule about where the click landed:**
  - **Does it hold? Yes, in every browser.**
    - React 19 handles every click from one listener on its root. Astryx's forwarded click is sent from inside that listener, so it's handled there too, nested. The input's handler and the row's run within the same call.
    - A microtask runs only when no script is running (the *microtask checkpoint*), so not until that listener returns. That timing is part of the HTML standard, not a browser's choice.
    - A label's own click reaches the input after the row's handler has run, but the row skips labels anyway.
  - **Could it block a real click? No.** A new click is a new task, and the flag is cleared before it.
  - **Instead:** a rule about the target. Astryx marks the description `data-pressable-container`, and exports `hasInteractiveAncestor` (`@astryxdesign/core/hooks`), which treats inputs and marked areas as "someone else's click". `if (target.closest('label') || hasInteractiveAncestor(target, row)) return;` would cover all three cases, with no timing.
  - **Chosen because:** the flag works for anything that forwards to the input, whatever Astryx marks.
  - **Worse at:** it relies on Astryx forwarding during the same click, and the reader needs the comment to follow it. The stakes are small, though: by the standard, a second open within one click finds the activation used up and does nothing. So the guard mainly keeps it to one click event. The unit test would catch a change (3 calls instead of 2).
- **The row works with the mouse only:**
  - **What that means:** the row isn't a tab stop or a button. Keyboard and screen reader users use the colour input, which is a tab stop and opens with Space or Enter (a probe confirmed one click for Space).
  - **Why that's right:** one control, not two with the same job.

## How it was checked

- **Read:**
  - the diff and the PR description
  - `ColourField.tsx` and its test, `ColoursPage.tsx` (the only user), `fieldSpace.ts`
  - the theme's status rules: `MedApp.Web/src/themes/medapp/medapp.css:546-548, 730-732` and `medappTheme.ts:22-26, 77-83`
  - Astryx 0.7.15's `Field`, `FieldLabel`, `FieldStatus`, `useFieldStatusAnnouncement`, `useInputContainer` and `useClickableContainer`
  - the HTML standard's input section (activation, "show the picker, if applicable", `showPicker()`)
  - `tools/ui-parity/screens.ts:486-495`, the Colours screens.
- **CI:** all 13 checks pass at `ba946b9` (run 44019944471, and CodeQL in run 44019944469).
- **Unit tests:** `npx vitest run src/components/forms/ColourField.test.tsx src/pages/account/ColoursPage.test.tsx`: 2 files, 12 tests passed.
- **Temporary probes,** in one file, run and then deleted. Each passed:
  - a click on the warning: the input is clicked once
  - a click on the row's padding (the `Field` itself): once
  - Space on the focused input: once
  - a selection over the hex value, then a click on it: the input is clicked (finding 1)
  - a selection over the description, then a click on it: Astryx's forwarder holds back, then the row opens the picker anyway (finding 1)
  - a double-click on the hex value: the input is clicked twice
  - the input has no `aria-describedby` and an empty accessible description, while the description and warning have IDs (`…-desc`, `…-status`) (finding 3)
  - the warning renders `data-variant="detached"`, and its icon `aria-hidden="true"`
  - the row's rendered style is `padding: var(--spacing-3); --medapp-field-status-line: 0px; …`, with no `padding-bottom: 0` (finding 4).
- **By reasoning, not run:**
  - that the flag holds in real browsers (React 19's single root listener and the microtask checkpoint)
  - that `click()` opens the picker in each browser (the HTML standard's activation steps).
- **Not done:**
  - **No browser,** by the project's rules. The PR lists a before-and-after screenshot and the axe audit, but not a click on each part of the row in a browser. Firefox and Safari weren't tried.
  - **No screen reader.**

## Findings

### 1. Selecting the hex value, or the description, opens the picker

- **Severity:** low. **Confidence:** high (shown with a probe).
- **What could go wrong:** someone wants to copy their custom card colour's hex code, to use it somewhere else.
  - **Dragging over `#25252a`** to select it opens the picker when the mouse is released.
  - **Double-clicking it** sends two clicks, and each one opens the picker. What happens in between depends on the browser (the second press may close the picker as a press outside it, and its click open it again). Not tried in a browser.
  - **Either way,** the picker covers the page, and Ctrl+C may go to the picker rather than copy.
  - **Before this PR,** the hex value was plain text, and selecting it did nothing else.
- **Why:** `openPicker` acts on every click that isn't on the input or the label (`ColourField.tsx:47-51`). A click that ends a text selection is still a click. Astryx's own clickable areas skip it (`hasTextSelection`, `node_modules/@astryxdesign/core/dist/hooks/useClickableContainer.js:126, 202`). That's why, in the probe, the description held back and then the row opened the picker anyway.
- **Evidence:** `ColourField.tsx:47-51`; `useClickableContainer.js:126, 202`; the probes.
- **Fix:**
  - **Skip a click that ends a selection in the row,** as Astryx does. Import `hasTextSelection` from `@astryxdesign/core/hooks` (the app already imports `useClipboard` and `useMediaQuery` from there). Make `if (hasTextSelection(event.currentTarget)) return;` the first line of `openPicker`, with the event typed `MouseEvent<HTMLDivElement>`.
  - **What stays:** the first click of a double-click still opens the picker, because nothing is selected yet. Every clickable card behaves like that.
  - **A test:** select the hex value's text, click it, and expect no click on the input.
- **What happened to it:** fixed in the PR. `openPicker` returns first when `hasTextSelection(event.currentTarget)`, as Astryx's clickable rows do. A test drags across the hex value: it's selected, and the picker doesn't open.

### 2. The tests leave out the warning and the row's padding, and don't check for "detached"

- **Severity:** low. **Confidence:** high.
- **What could go wrong:**
  - **The warning** is where this PR changed behaviour most: until now it ignored the pointer. If a later Astryx version, or a theme rule, made the detached style ignore it too, or someone went back to attached, a click on the warning would do nothing. No test would notice. The PR says the test covers "each place", but it clicks four of the six.
  - **The padding:** a click on the `Field` itself is the plainest case, and is untested too.
  - **The style check** (`ColourField.test.tsx:23`) says what the warning isn't (`attached`), not what it is (`detached`).
  - **No screen shows the warning:** the UI parity and axe screens for Colours (`tools/ui-parity/screens.ts:486-495`) open Custom with the account's own colours, which are readable, so neither shows a warning. The axe run the PR cites didn't check the one element whose look changed. Its colours are the same theme tokens as before, so a contrast problem is unlikely.
- **Evidence:** `ColourField.test.tsx:23, 26-41`; `screens.ts:486-495`.
- **Fix:**
  - In the click test, render with a warning, click the warning and the row itself, and count both.
  - Assert `toHaveAttribute('data-variant', 'detached')`.
  - **Optionally,** add an `account-colours-warning` screen that sets a card colour that's hard to read (for example, black in light mode) with the colour input's `fill`. Then the screenshot and the axe audit cover the warning.
- **What happened to it:** fixed in the PR. The click test now also clicks the warning (the one on screen, not its live-region copy) and the row's padding, and the warning test checks for `data-variant="detached"`.

### 3. The colour input isn't linked to its description or its warning (from before this PR)

- **Severity:** low. **Confidence:** high (the probe found an empty accessible description).
- **What could go wrong:** a screen reader user opens Custom with a card colour that's too dark.
  - **On opening:** the warning is spoken once, politely, among the page's own announcements, where it's easy to miss.
  - **Later,** when they Tab to the Cards colour: they hear its name and value, but neither the description nor "Text on this colour is hard to read".
  - **While picking in the browser's picker:** a warning that appears may be spoken while the picker has the focus, or lost. Not tested.
- **Why:** Astryx's `Field` gives the description and the warning IDs, but leaves it to the control to point at them (`Field/Field.d.ts:31, 74`; its own example at `:134` is `<input id={id} aria-describedby={descID} />`). ColourField's input has no `aria-describedby` (`ColourField.tsx:66-79`).
- **Why it's here:** this PR doesn't cause it, but it's the field the PR is about, and the brief asked about screen readers. ColourField's own comment says "Screen readers say it as it appears" (`:28`), which is true only at that moment. WCAG 1.3.1 (Info and Relationships) expects a field's message to be tied to it, and axe doesn't check this.
- **Evidence:** `ColourField.tsx:28, 66-79`; `Field.d.ts:31, 74, 134`; the probe.
- **Fix:**
  - Give the `Field` explicit IDs made from `id` (its `descriptionID`, and `messageID` in `status`), so nothing depends on Astryx's default names.
  - Point the input at them: `aria-describedby` listing the description's ID, and the warning's when there is one.
  - **A test:** `expect(input).toHaveAccessibleDescription(/hard to read/)`.
- **What happened to it:** fixed in the PR. The Field gets explicit `descriptionID` and `messageID`, and the colour input's `aria-describedby` names both. A test checks its accessible description is the description followed by the warning.

### 4. Two style values in `ROW` no longer do anything

- **Severity:** low. **Confidence:** high (the rendered style shows it).
- **What:** `ROW` spreads `NO_STATUS_LINE` and `NO_FIELD_STATUS_LINE` (`ColourField.tsx:12-18`).
  - **`NO_STATUS_LINE`** is `paddingBottom: 0`. The `padding: var(--spacing-3)` straight after it, in the same object, replaces it: React sets them in order, and the rendered style has no `padding-bottom: 0`. That's from before this PR.
  - **`NO_FIELD_STATUS_LINE`** sets the variable whose job was to cancel the attached message's negative bottom margin (`medapp.css:546-548`). With detached, there's no attached message, and the row's own padding is set inline, so the variable now acts on nothing.
- **What could go wrong:** nothing at run time. A reader may think they hold or drop space for the status line, or copy them onto another detached field and wonder why they change nothing.
- **Evidence:** `ColourField.tsx:12-18`; `medapp.css:546-548, 730-732`; the probe's rendered style.
- **Fix:** drop both spreads, and say in the comment that the row's own `padding` replaces the theme's status line. The theme's `padding-bottom` rule can't come back, because inline `padding` wins over it.
- **What happened to it:** fixed in the PR. `ROW` keeps only its padding, rounding and pointer; `NO_STATUS_LINE` and `NO_FIELD_STATUS_LINE` are gone from it.

## Remaining risks

- **The clicks weren't tried in a browser.** "Once from each place" rests on jsdom and the reasoning above. A one-minute check in Chrome, Firefox and Safari would settle it: the swatch, the label, the description, the hex value, the warning and the padding.
- **The focus doesn't follow a click on the row.** A click on the row opens the picker without focusing the colour input; a click on the swatch focuses it (in Chrome). After the picker closes, Space doesn't reopen it, and Tab starts from where the click landed. Small. `inputRef.current?.focus()` before `click()` would make the two the same.
- **A warning that comes and goes while dragging in the picker** moves the fields below (from before this PR; now 8 px further), and is spoken each time it appears.
- **The guard relies on Astryx forwarding during the same click.** An Astryx upgrade that changed that would mean two clicks on the input. Browsers would most likely ignore the second, and the unit test would fail, so it would be noticed.

## Say it like this

"The colour fields' 'hard to read' warning used Astryx's attached style, which slides up to join a text box's border, so under a colour swatch it slid behind the colour; switching to Astryx's detached style puts it below. The PR also makes the whole row open the picker, with a small flag so a click Astryx already forwards to the picker isn't sent twice. The trade-off is a timing-based guard where a rule about the click's target would also do, and a row that now reacts when someone selects its text, which Astryx's own clickable areas ignore."

## Detail

### Where a click lands, and what reaches the input

| Click on | What runs | Clicks on the input |
|---|---|---|
| The swatch (the input) | The input's own click; the row skips it (`closest('input')`) | 1 |
| The label | The row skips it (`closest('label')`); then the browser hands the click to the input | 1 |
| The description | Astryx forwards it to the input (the flag is set); the row sees the flag and skips | 1 |
| The hex value | The row calls `input.click()` | 1 |
| The warning | The row calls `input.click()` (the attached style used to ignore the pointer) | 1 |
| The padding | The row calls `input.click()` | 1 |
| Any of the last four, ending a text selection | The row calls `input.click()` (finding 1) | 1 |

### A click on the description, step by step

1. The browser sends the click to React's listener on its root.
2. React runs the handlers from the description up to the row, in order.
3. **The description's handler** (Astryx) calls `input.click()`. The browser sends that click at once, nested, back into React's listener:
   - the input's handler sets the flag and queues a microtask to clear it
   - the row's handler sees the input as the target and skips
   - the input's default action opens the picker.
4. **Back in the outer click,** the row's handler sees the flag and skips.
5. React's listener returns. No script is running now, so the microtask runs and clears the flag.

### The HTML standard's steps

- **A colour input's activation** (a click, including `el.click()`): "show the picker, if applicable".
- **"Show the picker, if applicable":** if there's no transient activation, return; if the element isn't mutable, return; consume user activation; then show the picker.
- **`showPicker()`:** throws `InvalidStateError` if the input isn't mutable, `NotAllowedError` without transient activation, and `SecurityError` in a cross-origin frame, except for file and colour inputs. Then it runs the same "show the picker, if applicable".

### The fixes for findings 1 and 3, together

```tsx
import {hasTextSelection} from '@astryxdesign/core/hooks';

const openPicker = (event: MouseEvent<HTMLDivElement>) => {
  // A click that ends a text selection (copying the hex value) isn't a request for the picker.
  if (hasTextSelection(event.currentTarget)) return;
  if (event.target instanceof Element && event.target.closest('input, label')) return;
  if (reachedInputRef.current) return;
  inputRef.current?.click();
};

const descriptionID = `${id}-description`;
const warningID = `${id}-warning`;
// …
<Field
  descriptionID={description ? descriptionID : undefined}
  status={warning ? {type: 'warning', message: warning, messageID: warningID} : undefined}
  …>
  <input
    aria-describedby={[description && descriptionID, warning && warningID].filter(Boolean).join(' ') || undefined}
    …
  />
```
