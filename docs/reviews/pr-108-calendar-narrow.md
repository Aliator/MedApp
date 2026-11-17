# Review: `feature/calendar-narrow`, a week in narrow windows and 5-minute rows on the Calendar

**Branch:** `feature/calendar-narrow` (no PR yet). **Commits reviewed:** `8ffb563`, `0d5e7ed` and `9e747d0` (`git diff 7ef0cac..9e747d0`). **Base:** `7ef0cac`, PR #99's team calendar, which has its own review.

**Reviewed:** 17 November 2026, independently of the author's conclusions. Each claim below was checked against the code, a test run, or a temporary test. **No finding is high or critical:** 3 are medium and 7 are low.

**Fixes:** every finding is fixed on `feature/calendar-apply-template` (after `492f8f9`, Apply Template), with the browser check's findings too; each finding says where. See Browser check below.

## Summary

The Calendar now works in narrow windows and half views: Week is offered there too, shown as a compact Monday-to-Friday week. Anyone can switch the grid between 15-minute and 5-minute rows; 5-minute rows are as tall as 15-minute ones, so the day gets three times taller and scrolls. The Book or Block menu is now only as wide as its buttons.

## What changed, and why

14 files, 492 lines added and 86 removed, in three commits.

- **The menu's width** (`8ffb563`, `CalendarPage.tsx:480`): the Book or Block menu gets `width="max-content"`.
  - **The problem:** in a half view the menu was "way too wide". Astryx's `Popover`, left at its default, sets its *minimum* width to its anchor's width (`matchTrigger`, `Popover.tsx:462-469` in Astryx). The anchor is the selected time, which is a whole column wide. In a one-day view or the team view's "Everyone free", that's hundreds of pixels.
  - **The fix:** `max-content` means "as wide as the content needs". Astryx still caps it at the window's width.
  - **An analogy:** a sticky note that was cut to the width of the page it was stuck on, now cut to fit its words.
- **A compact week** (`0d5e7ed`):
  - **Before:** in a narrow window (`useIsNarrow()`: under 1400 px, or a half view), the week turned into a day, and Week disappeared from the View switch.
  - **Now:** Week is always offered. A narrow window still *opens* on a day, but `?view=week` is kept in the address (the parser used to drop it, `calendarSearch.ts:65`), and choosing Week there writes it (`CalendarPage.tsx:552`).
  - **The compact form** (`isCompact` on `CalendarGrid`, and `dayColumns(..., {weekdaysOnly: true})`): Monday to Friday only, short headings ("Mon 23", spoken as "Mon 23: show Monday 23 November"), a narrower hour gutter and columns (`CalendarGrid.tsx:980-982`), and every item on one line with its words in a tooltip (`:923, 947`).
  - **Weekends:** left out, because nothing can be on them. A weekend that somehow has an item is kept (`:66`).
  - **An analogy:** folding a newspaper to read on a crowded train. Same stories, less of each on show.
- **5-minute rows** (`9e747d0`):
  - **The control:** Row length (15 min or 5 min) in the toolbar, kept in the address as `rows=5` (`calendarSearch.ts:69`, `CalendarPage.tsx:559-562`).
  - **The grid:** each row is `rowMinutes` long instead of a fixed 15 (`CalendarGrid.tsx:274`). Rows keep their height, so 120 rows make the day 2,640 px tall instead of 880 (at 22 px a row, as the tests assume), and it scrolls inside its card. Quarter hours get lighter labels (`:590-599`), and 5-minute lines are fainter (`:998-1001`).
    - **An analogy:** a ruler marked in millimetres instead of centimetres, with the marks the same distance apart. The ruler gets longer.
  - **Selecting, the keyboard and dragging** all go a row at a time, because they already counted in rows.
  - **Switching keeps the same time at the top** (`:335-349`): the grid remembers how far down the day the view was, as a share of the day, and restores it after the switch.
  - **Opening on now** was reworked (`:316-333`): it scrolls only while the working day is under way, and leaves at most a third of the view above now.
  - **The dialogs:** the booking and block dialogs opened from the page take `stepMinutes` (`CalendarPage.tsx:443, 456`), so they offer 5-minute times. Without it, a selection starting at 10:05 would vanish from the booking dialog, which only keeps a start it offers (`BookAppointmentDialog.tsx:216`). `freeStarts` and `freeStartsByDay` take the step too (`calendarSlots.ts:111, 128`).
- **Docs:** decisions 67 and 68 in `docs/calendar-plan.md`, both `AGENTS.md` files, and two new UI parity screens.

## Design choices

- **Compact by the page's "narrow" flag, rather than by the grid's own width:**
  - **Instead:** measure the card (a `ResizeObserver`, or a CSS container query) and go compact only when the full week doesn't fit.
  - **Chosen because:** `useIsNarrow()` already exists, and the page already used it to pick a day.
  - **Worse at:** every window under 1400 px gets the compact week, including common laptops where the full week fits (finding 2). Decision 67 names this, but undersells it.
- **Hide weekends in the compact week, rather than keep them as slim columns:**
  - **Chosen because:** nothing can be booked, blocked or met on a weekend (`WorkingHours`), and templates put nothing there. Every pixel goes to working days.
  - **Worse at:** the heading says "23 to 29 November" over five columns. Minor.
- **Taller rows that scroll, rather than 120 thin rows in the same height:**
  - **Chosen because:** it's what was asked for ("so it maintains the same tick height"), and thin rows would be too small to press.
  - **Worse at:** three times as many free-time buttons (600 in a week), and scrolling to see a whole day.
- **The row length in the address, rather than a saved setting:**
  - **Chosen because:** a link carries it, a reload keeps it, and the server renders the same page the browser shows first, with no new storage.
  - **Worse at:** it's forgotten whenever someone arrives from the side nav, Home or a notification (finding 7).
- **5-minute steps in the dialogs, rather than 15-minute steps plus the selection's own start:**
  - **Chosen because:** one option (`stepMinutes`) covers both the selection and choosing by hand.
  - **Worse at:** the booking dialog lists about 115 start times on a free day (finding 6). The block dialog already kept an off-step time (`BlockDialog.tsx:141-146`), which would have been enough for the selection.
- **`max-content` for the menu, rather than a fixed width in pixels:**
  - **Chosen because:** it fits its labels at any text size, and Astryx caps it at the window.
  - **Worse at:** nothing found.

## How it was checked

- **Read:** the whole diff; `CalendarGrid.tsx`, `CalendarPage.tsx`, the calendar route, `calendarSearch.ts`, `BlockDialog.tsx`, `BookAppointmentDialog.tsx`, `AppointmentDialog.tsx`, `calendarItems.ts`, `Page.tsx`, `listSettings.tsx`; `CalendarProjection.cs` (weekends get working hours too, so an item on one would still be placed); decisions 1, 3, 33, 62, 67 and 68; `docs/architecture.md`.
- **Astryx's source:** `Popover` (how `width` and the default are applied), `Button` (labels truncate with an ellipsis), `SegmentedControl` (its `label` is never shown), `Tooltip`.
- **Tests:** `npx vitest run` on `CalendarGrid.test.tsx`, `CalendarPage.test.tsx`, `calendarSearch.test.ts`, `calendarSlots.test.ts`, `BlockDialog.test.tsx` and `BookAppointmentDialog.test.tsx`: 6 files, 129 tests passed.
- **Temporary tests** (written, run and deleted):
  - **A 10:05 to 10:25 block in 15-minute rows:** a press that moved 6 px on its own row called `onItemMove` with 10:00 to 10:15. A drag to Tuesday gave 10:00 to 10:15 too (finding 1).
  - **The keyboard's place after a switch:** the tab stop moved from "Tuesday 24 November, 10:00" to "Tuesday 24 November, 08:40" (finding 4).
  - **A compact item's description:** no duplicate of its name (no `aria-describedby` on the card), so screen readers don't hear it twice.
  - **Rough timings in jsdom** (the simulated browser tests use) for a 20-person team view (finding 8).
- **Contrast:** worked out with the WCAG formula from the theme's tokens (finding 3, and Detail).
- **Not done:** no browser, Docker, AppHost, E2E or full suite (another check covers the browser). `npm run lint` and `npm run typecheck` weren't run. The Vitest run rewrote `src/routeTree.gen.ts` with other line endings; it was restored, unchanged.

## Findings

### 1. Dragging in 15-minute rows snaps a 5-minute item to the quarter hour and changes its length

- **Severity:** medium. **Confidence:** high (shown with a temporary test).
- **What could go wrong:**
  - In 5-minute rows, a nurse blocks 10:05 to 10:25 for admin time.
  - The next day she opens the Calendar from the side nav. The address has no `rows`, so the grid is back to 15-minute rows.
  - She clicks the block to open it, on a touchpad. The pointer moves 6 px during the press. The grid counts that as a drag (more than 4 px, `CalendarGrid.tsx:534`).
  - On release the block is saved as **10:00 to 10:15**, at once and without asking (blocks: `CalendarPage.tsx:272-282`). The page announces "Blocked time moved", and the dialog she wanted doesn't open.
  - A real drag to another day shrinks it to 15 minutes as well. A meeting an admin drags is saved the same way (`:262-270`). An appointment asks first (`:258-260`), but the question only shows the new range, so 20 minutes becoming 15 is easy to miss.
- **Why:**
  - **Its start snaps:** the item's row is rounded (`Math.round(...)`, `CalendarGrid.tsx:527`), and the new start is a whole number of rows from 08:00 (`:565`). So 10:05 becomes 10:00. Like a magnet pulling it to the nearest quarter-hour line.
  - **"Dropped where it was" never matches:** that check compares the snapped start with the real one (`:557`). For an item off the quarter hour they always differ, so any wobble over 4 px moves it.
  - **Its length is rounded to whole rows:** `lengthRows` (`:518-521`) turns 20 minutes into one 15-minute row (`:566`).
- **Why it's this branch's problem:** the code predates it, but before it, only meetings typed by hand and template blocks could sit off the quarter hour. 5-minute rows make such items an everyday thing. And decision 68 says dragged items "keep their length".
- **Evidence:** `MedApp.Web/src/components/calendar/CalendarGrid.tsx:518-530, 534, 557, 562-567`; `MedApp.Web/src/pages/calendar/CalendarPage.tsx:255-283`; `docs/calendar-plan.md:118`.
- **Fix:**
  - **Keep its own length:** the ghost's end is its start plus the item's real length, not `lengthRows(item) * SLOT`.
  - **Move it by whole rows from where it is:** start = the item's start + (target row − its row) × row length. Then 10:05 dragged one row down lands at 10:20, and a drop on its own row is "where it was".
  - **Test it:** in 15-minute rows, a 10:05 to 10:25 item dragged one row down lands at 10:20 to 10:40, and a 6 px wobble moves nothing.
- **What happened to it:** fixed in `cecdeed`. A dragged item now keeps its own start and exact length, and moves a row for each row the pointer moves from where it was picked up (`CalendarGrid.tsx`, `Drag.offset`, `pickRow`, `length`). A press that never takes it anywhere else is a click (`hasLeft`), so a wobble opens it; a drag away and back moves nothing and opens nothing. Tested: a 10:05 to 10:25 block in 15-minute rows, nudged 6 px (opens, no move), a row down (10:20 to 10:40), to Tuesday (10:05 to 10:25), and away and back.

### 2. The compact week turns on in every window under 1400 px, where the full week fits

- **Severity:** medium. **Confidence:** medium (the threshold is in the code; the widths are worked out, not measured in a browser).
- **What could go wrong:**
  - **Common screens are "narrow":** a 1366 x 768 laptop, or a 1920 x 1080 screen at 150% scaling (1280 px as the browser counts them). Both are under 1400 px.
  - **The full week fits there:** its minimum is about 608 px (a 48 px gutter, five 96 px days and two 40 px weekends). After the side nav and the page's padding, roughly 1,000 px are left.
  - **They get the reduced view anyway:** one-line items and no weekend. The request was for a reduced mode "for if it's too small to display".
  - **A related surprise:** a week chosen in a wide window isn't written to the address (`CalendarPage.tsx:552`). So when a half view opens, or the window narrows, it falls back to a day.
- **Evidence:** `MedApp.Web/src/components/pages/Page.tsx:48, 54-57` (the 1400 px query); `CalendarPage.tsx:150, 378, 552`; `CalendarGrid.tsx:979-982`; decision 67's last sentence (`docs/calendar-plan.md:116`).
- **Fix:**
  - **Go compact by the grid's own width:** a `ResizeObserver` on the card, or a CSS container query, comparing it with the full week's minimum (`--mc-min-inline` for seven columns). It would still apply after mount, as now, so the server's render still matches.
  - **Keep `useIsNarrow()` for the default view only.** Consider opening on the week wherever it fits.
  - **Optionally, always write `view=week`** when someone chooses Week, so their choice outlives a half view.
- **What happened to it:** fixed in `e051199`. The page measures the grid's card (`useNarrowerThan`, a `ResizeObserver`, false until measured, so the server's full week matches the first render) and goes compact only when it's under the full week's minimum (`FULL_WEEK_MIN_REM`, 38rem, plus 1rem for a scrollbar). The window's width no longer matters: a 1366 px laptop gets the full week, a half view of a wide screen too. Choosing Week now always writes `view=week`, so a week chosen in a wide window stays a week. `useIsNarrow()` still picks the default view (a day) when the URL names none.

### 3. The quarter-hour times fail text contrast

- **Severity:** medium. **Confidence:** high (worked out from the theme's colours).
- **What could go wrong:** in 5-minute rows the gutter shows 08:15, 08:30 and 08:45 at 70% opacity (`CalendarGrid.tsx:1000`). On the card's background:
  - **Light theme:** 2.92:1.
  - **Dark theme:** 4.01:1.
  - **Needed:** 4.5:1 for text (WCAG 1.4.3). At full strength the same colour gives 5.30:1 and 6.62:1.
  - **Someone with low vision** loses the times they'd use to place a 5-minute booking.
  - **The gutter is `aria-hidden`,** but that doesn't exempt visible text from contrast.
  - **The accessibility statement** aims for WCAG 2.2 AA (`AccessibilityStatementPage.tsx:29`).
  - **Custom colours** can make it worse: the colour check warns about secondary text at full strength (`src/app/colours.tsx`), not at 70%.
- **Evidence:** `MedApp.Web/src/components/calendar/CalendarGrid.tsx:1000-1001`; `MedApp.Web/src/themes/medapp/medapp.css:206, 230`.
- **Fix:** drop the opacity. The smaller size alone marks the quarters, at full secondary colour. If the hours need to stand out more, make their labels semibold.
- **What happened to it:** fixed in `54af5e8`. No opacity: the quarter-hour times are in the full secondary colour (5.30:1 light, 6.62:1 dark), at 10 px (between `--font-size-xs` and `-sm`) rather than 9. A test checks the CSS has no opacity on them.

### 4. After switching row length, the keyboard's place jumps to another time

- **Severity:** low. **Confidence:** high (shown with a temporary test).
- **What could go wrong:** a keyboard user moves to Tuesday 10:00, tabs to Row length and picks 5 min. Tabbing back into the grid lands on **08:40**. The grid remembers its place as a row number (row 8), and row 8 is 10:00 in 15-minute rows but 08:40 in 5-minute ones. A screen reader user hears a different time and has lost their place.
- **Evidence:** `CalendarGrid.tsx:365, 372, 376-379`.
- **Fix:** in the effect that runs on a row-length change (`:343-349`), convert the place (row × old length ÷ new length), or remember it as a time, not a row.
- **What happened to it:** fixed in `0c25568`. The tab stop is kept as minutes from the column's start, not as a row, so it stays on 10:00 when the rows change (10:10 in 5-minute rows becomes 10:00, the row it's in). Tested both ways.

### 5. Switching to 5-minute rows doesn't show the time now if the 15-minute day fitted without scrolling

- **Severity:** low. **Confidence:** medium (from the code; not run in a browser).
- **What could go wrong:**
  - On a tall screen (1440 px high, say) the 15-minute day (about 920 px with its headings) fits, so nothing scrolls.
  - At 15:00 someone switches to 5-minute rows. The grid keeps "the same time at the top", which is 08:00. They see 08:00 to about 10:30, and must scroll to now.
  - Opening `?rows=5` directly scrolls to now (`:319-333`). So the same address shows a different part of the day depending on how they got there.
- **Why:** the remembered position (`topShare`) only changes when the grid scrolls (`:337-341`), and the open-on-now step runs once per mount.
- **Evidence:** `CalendarGrid.tsx:319-349`.
- **Fix:** when the old view didn't scroll and today is shown during the working day, run the open-on-now step after the switch instead. Share its code.
- **What happened to it:** fixed in `0c25568`. On a switch, the grid works out whether the old day scrolled at all (rows keep their height, so its old height is known); if it didn't, and today is under way, it goes to the time now, sharing the opening code (`scrollToNow`). Tested with a 1,000 px tall view at 15:00. The browser check found the real cause of the scroll problem too: the router's scroll restoration put back the old pixel position after the grid had set the new one (see Browser check, 1).

### 6. With 5-minute rows, the booking dialog lists every 5 minutes, and a single click books 5 minutes

- **Severity:** low. **Confidence:** high.
- **What could go wrong:**
  - **A wall of buttons:** New, then Book appointment, with `rows=5`. A free day says "115 free times", and the dialog shows about 115 time buttons (48 in the morning, 67 in the afternoon) instead of 39 (`BookAppointmentDialog.tsx:202-205, 557-586`).
  - **5-minute appointments by default:** one click on a free row selects 5 minutes. The dialog then suggests a 5-minute length, which isn't one of its usual lengths (`APPOINTMENT_LENGTHS`, 15 to 120). A hurried "Book" makes a 5-minute appointment.
  - **Not consistent:** Reschedule, from an appointment opened on the same page, still offers 15-minute steps (`AppointmentDialog.tsx:275-279` passes no `stepMinutes`).
- **Evidence:** `CalendarPage.tsx:443`; `BookAppointmentDialog.tsx:159, 202-205, 216, 297-299`; `AppointmentDialog.tsx:275-279`.
- **Fix:** keep 15-minute steps and add the selection's own start to the list, as the block dialog does with `keep` (`BlockDialog.tsx:141-146`). Consider suggesting at least 15 minutes from a single click. If 5-minute steps stay, pass them to the reschedule dialog too.
- **What happened to it:** fixed in `aeda60f`. A click, or Enter on a slot with nothing extended, selects 15 minutes whatever the row length (as far as the time is free); Shift with the arrows, or a drag, still goes a row at a time. The booking dialog lists times every 15 minutes as before and, with a 5-minute step, adds the 5-minute times within a quarter of an hour of the chosen one (`shownStarts`): a 10:05 selection shows 09:55, 10:00, 10:05, 10:10 and 10:15 among the quarters, and choosing 11:30 moves the finer times there. Each day still counts its quarter hours. Chosen over a fixed list plus the selection's own start because it lets someone nudge a time by 5 minutes either way without the calendar. Reschedule and Book Another from an appointment opened on the page take the same step (`AppointmentDialog`'s `stepMinutes`).

### 7. The row length is forgotten whenever someone comes to the Calendar from elsewhere

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** someone who books in 5-minute steps has to switch again on every visit. The side nav's Calendar link, Home's day calendar, Needs booking and notification links (`/calendar?appointment=…`, `/calendar?date=…&view=day`) don't carry `rows`.
- **The precedent:** lists keep a saved default that the address can override: "Whether the procedure lists start on 'Assigned to me' (their URL can still say otherwise)" (`src/app/listSettings.tsx:23`).
- **Evidence:** `calendarSearch.ts:69`; `CalendarPage.tsx:151`; decision 68's last sentence.
- **Fix:** a saved "calendar rows" setting (remember the last choice, or a choice in Account Settings) as the default, with `rows` in the address overriding it. It must apply the same way on the server, or after mount like the narrow switch. Or accept it, as decision 68 does knowingly.
- **What happened to it:** fixed in `d9a8863`. The last row length chosen is the person's default (`useListSettings().calendarRows`, a `defineSetting` kept with the account, as where the procedure lists start is), and the URL overrides it: `rows=5`, or `rows=15`, which the parser now keeps. Only 5 is stored (15 is the default). The server renders it from the account's settings, as the lists do.

### 8. A drag re-renders the whole grid on every pointer move, and 5-minute rows triple what that costs

- **Severity:** low. **Confidence:** medium (timed only in jsdom).
- **What could go wrong:**
  - **Every move redraws everything:** dragging an item calls `setDrag` with a new object on every pointer move (`CalendarGrid.tsx:547`), even when the target row hasn't changed.
  - **5-minute rows triple the work:** a 20-person team view has 2,400 rows instead of 800. In jsdom a move took about 28 ms instead of 13. A browser is faster, but on an older PC with 30 or more people the drag could stutter.
- **Evidence:** `CalendarGrid.tsx:532-548, 715-749`.
- **Fix:** skip `setDrag` when `isMoving` and `to` haven't changed. Memoise each column's rows, so a drag redraws only the ghost.
- **What happened to it:** fixed in `1dbff42`. A pointer move that doesn't change where the item would go no longer sets state, and the free slots are a memoised component (`SlotRows`) with stable handlers, so a drag redraws the items and the ghost, not every slot.

### 9. Docs and comments that disagree with the code

- **Severity:** low. **Confidence:** high.
- **Where:**
  - **"Keep their length":** decision 68 (`docs/calendar-plan.md:118`) says dragged items keep their length, and `MedApp.Web/AGENTS.md:35` that dragging goes a row at a time. Not in 15-minute rows (finding 1).
  - **"The week becomes the day":** decision 33 (`docs/calendar-plan.md:74`) and `docs/architecture.md:645` still say a narrow window turns the week into a day.
  - **"15-minute slots":** decision 3 (`docs/calendar-plan.md:14`) says the browser steps through 15-minute slots.
  - **In `CalendarGrid.tsx`:** `:81` ("a click on an empty slot (15 minutes)"), `:86` ("in 15-minute steps") and `:835-836` (`itemFit` counts "rows (15 minutes each)").
- **Fix:** update them, and point decision 33 and `architecture.md` at decision 67.
- **What happened to it:** fixed in `892f7b7`: decisions 1, 3, 33, 67, 68 and 69, `docs/architecture.md`, both `AGENTS.md` files (including the root's "Apply a day template to this day", now Apply Template, and its 15-minute rows), and the comments in `CalendarGrid.tsx` (`onSelectRange`, `onItemMove`, `isCompact`, `itemFit`, `GridItem`).

### 10. "15 min" and "5 min" have no visible label

- **Severity:** low. **Confidence:** medium.
- **What could go wrong:** Astryx's `SegmentedControl` `label` is only spoken, "never rendered visually". So sighted users see "15 min | 5 min" beside a New button that books appointments by length, and could read it as the appointment length. Screen reader users hear "Row length", which is clear.
- **Evidence:** `CalendarPage.tsx:559-562`; `node_modules/@astryxdesign/core/dist/SegmentedControl/SegmentedControl.d.ts:29`.
- **Fix:** name the choices "15-min rows" and "5-min rows", or add a tooltip.
- **What happened to it:** fixed in `01fa01e`. The control is now a small menu named by what it shows, "Rows: 15 min", with "15-minute rows" and "5-minute rows" (a check on the current one), in the toolbar's right-hand group, where it shares a line with the default week in a narrow window (the browser check's point 6).

## Browser check

A later check ran the first three commits in Chromium at 1920, 1366, 960 and 390 px. Its findings, and what happened to each:

1. **(medium) Switching the row length kept the pixel position, not the time.** The cause: the grid set the right scroll position, then TanStack Router's scroll restoration put back the one it had saved for the old address. Fixed in `0c25568`: the Calendar route navigates with `resetScroll: false`, so the grid keeps its own place. A page test goes through the real router with scroll restoration on, and fails without the fix.
2. **(serious in axe) The quarter-hour times failed contrast.** Finding 3: fixed in `54af5e8`.
3. **(low to medium) The compact week at 390 px was barely readable.** Fixed in `848477e`: each day is headed by its weekday over its date ("Tue" over "17"), so Tue and Thu differ; an item, a selection or a dragged item in a card under 4.5rem wide shows its start time instead of a few letters (CSS container queries on each card's width).
4. **(low) Compact items stayed on one line when tall, and 10-minute ones were smaller.** Fixed in `848477e`: compact items wrap to the lines their height allows, and short ones use the supporting text size, as the rest do.
5. **(low) In the day view the Book or Block menu sat far from the selection,** flipped to the far side over the side nav. Fixed in `60fd47c`: a selection over 320 px wide gets its menu below it (`selectionMenuPlacement`); narrower ones keep it beside.
6. **(low) The Row length control added a toolbar row on narrow windows.** Fixed in `01fa01e` (finding 10): a small Rows menu in the right-hand group, beside the default week.
7. **The page header overflowed at 390 px, and the phone top bar's "MedApp" was white on a light bar.** Fixed in `16e5ca7`: `PageHeader` wraps its actions under the title, and among themselves, when they don't fit (every page). Fixed in `bd6fa7a`: the side nav's dark theme no longer applies on the phone's top bar (`SideNavTheme`, by Astryx's `useSideNavRenderMode()`), which sits on the page's own background, so its name and bell take the app's colours there.

## Remaining risks

- **Not seen in a browser by this review:** scroll anchoring on a switch, the sticky headings, the menu's width in Firefox and Safari, and touch. Another check covers the browser.
- **Phones:** at 320 px each compact column is about 46 px wide. Day headings likely truncate to "M…" (Astryx truncates button labels). Not measured. The date range is in the page's subtitle, and each heading speaks its full date.
- **Keyboard at 5 minutes:** 08:00 to 16:00 is 96 presses of the down arrow. Home and End exist, but there's no Page Up or Page Down to jump an hour.
- **Screen readers:** browsing a 5-minute day passes 120 free-time buttons per column. Tab still reaches the grid in one stop.
- **Server pages grow:** a 5-minute week renders 600 slot buttons, roughly 100 KB of HTML before compression. A big team view is several times that.
- **Tooltips on touch:** compact items show their full words in a tooltip, which touch screens don't show. A tap opens the item, which says everything.
- **A visible jump after loading:** a narrow window gets the full week from the server, then the compact one after mount. Decision 67 says so.
- **After the fixes:** the compact switch is measured, so a change of text size alone is noticed at the next resize. The 5-minute times in the booking dialog are offered only around the chosen one. None of the fixes has been seen in a browser yet.

## Say it like this

"On narrow screens the calendar can now show the working week squeezed into five slim columns, and anyone can switch between 15- and 5-minute rows, which stay the same height, so the day gets taller and scrolls. The main choice was keeping both in the address bar rather than a saved setting: links and server rendering stay consistent, but the 5-minute choice resets when you come in from elsewhere. The review's main catch was that dragging a 5-minute item in 15-minute rows snaps it to the quarter hour and changes its length."

## Detail

### The drag, worked through (finding 1)

A block from 10:05 to 10:25, in 15-minute rows starting at 08:00:

| Step | Code | Value |
|---|---|---|
| Its row | `Math.round((10:05 − 08:00) / 15 min)` (`:527`) | round(8.33) = 8, so 10:00 |
| Its length in rows | `Math.round(20 / 15)` (`:520`) | 1 row, so 15 minutes |
| Pressed on row 8, moved 6 px on row 8 | target = 8 − 0 (`:542`) | row 8 |
| The ghost | 08:00 + 8 × 15 min, plus 1 row (`:565-566`) | 10:00 to 10:15 |
| "Where it was"? | 10:00 against 10:05 (`:557`) | No, so `onItemMove` |

The temporary test printed `{"startUtc":"2026-11-23T10:00:00.000Z","endUtc":"2026-11-23T10:15:00.000Z"}` for the wobble, and the same times on Tuesday for a drag there. In 5-minute rows the same block's row is exactly 25 and its length exactly 4 rows, so nothing is lost there.

### Contrast (finding 3)

| Theme | Text | Card | Full strength | At 70% |
|---|---|---|---|---|
| Light | `#5f5f64` | `#e4ebf8` | 5.30:1 | 2.92:1 |
| Dark | `#ababb3` | `#212634` | 6.62:1 | 4.01:1 |

"At 70%" is the text colour mixed 70:30 with the card's, which is what `opacity: 0.7` paints on a solid background.

### The widths (finding 2)

- **Full week's minimum:** `--mc-gutter` (3rem) + 5 × `--mc-day-min` (6rem) + 2 × `--mc-narrow` (2.5rem) = 608 px at the default text size.
- **Compact week's minimum:** 2.5rem + 5 × 2.5rem = 240 px.
- **Common "narrow" windows:** 1366 px (a 1366 x 768 laptop) and 1280 px (1920 x 1080 at 150%, or 1600 x 900 at 125%).

### Timings in jsdom (finding 8)

A 20-person team view, with a press on a block and 20 small moves on its row:

| Rows | Row elements | Per move |
|---|---|---|
| 15 minutes | 800 | about 13 ms |
| 5 minutes | 2,400 | about 28 ms |

jsdom is slower than a browser, so only the ratio matters.

### The commits

| Commit | What | Files |
|---|---|---|
| `8ffb563` | The Book or Block menu sized to its buttons | 2 |
| `0d5e7ed` | The compact week, and Week offered at every width | 9 |
| `9e747d0` | 15 or 5-minute rows, `stepMinutes` in the dialogs | 14 |
