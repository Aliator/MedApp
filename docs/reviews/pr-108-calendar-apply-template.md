# Review: `feature/calendar-apply-template`, Apply Template and the fixes to the narrow calendar's review

**Branch:** `feature/calendar-apply-template` (no PR yet). **Commits reviewed:** `492f8f9` to `c37782a` (`git diff 9e747d0..c37782a`, 16 commits). **Base:** `9e747d0`, the last of the three narrow-calendar commits, which have their own review ([calendar-narrow.md](pr-108-calendar-narrow.md)). The commits after `c37782a` only merge #99's latest work in and aren't covered.

**Reviewed:** 17 November 2026, independently of the author's conclusions. Each claim below was checked against the code, a test run, or a temporary test. Line numbers are at `c37782a`. **No finding is high or critical:** 1 is medium and 6 are low. **All seven are fixed** (each says how), and so are the 7 found by the browser check (see Browser check).

## Summary

The Calendar's "This day's template" menu, which only appeared in the day view, is now an **Apply Template** button beside New. It opens a dialog that puts one of a person's day templates (or none, or the default week's) on any working days of the week shown. The branch also fixes every finding of the narrow calendar's review and its browser check: drags, the compact week, contrast, the Rows menu, scrolling, phone layouts, and two app-wide changes (the page header wraps, and the phone's top bar takes the app's colours).

## What changed, and why

29 files, 1,737 lines added and 327 removed, in 16 commits.

- **Apply Template** (`492f8f9`, `ApplyTemplateDialog.tsx`, `CalendarPage.tsx:427-437, 479-489`):
  - **The request:** "Instead of the 'this days template' it should be an 'apply template' button at the top, also it doesnt appear on the full view mode."
  - **The button:** in the page header between Needs Booking and New, in the day, week and compact week views, when the calendar's `canEdit` is true (its owner, or an admin). Never in the team view.
  - **The dialog:** a Template choice (the person's active day templates, then No template and The default week's), and a checkbox for each weekday of the week, each saying what applies now. Past days show but can't be ticked. The day shown (day view) or today (week view) starts ticked.
  - **Applying:** one API call per day, all at once (`Promise.allSettled`, `:118-124`). "The default week's" sends a `DELETE` only for days with an override of their own (`:114-115`). If some fail, the dialog stays open and names each one with the API's reason (`:126-137`).
  - **An analogy:** a rubber stamp. Before, you could only stamp the page in front of you; now you can tick several pages and stamp them all at once.
- **Drags keep an item's own start and length** (`cecdeed`, `CalendarGrid.tsx:592-645`): an item moves a row for each row the pointer moves from where it was picked up, and keeps its exact length. A press that never takes it anywhere else (`hasLeft`) is a click.
- **Drags redraw less** (`1dbff42`): the free slots are a memoised component (`SlotRows`) with stable handlers, and a move that changes nothing sets no state.
- **Quarter-hour times at full contrast** (`54af5e8`): no opacity, a smaller size instead.
- **The Rows menu** (`01fa01e`, `d9a8863`, `CalendarPage.tsx:620-631`): "Rows: 15 min" replaces the unlabelled 15 min / 5 min switch. The last choice is saved with the account (`listSettings.tsx:50, 55`), and `rows` in the address overrides it.
- **Keeping the time when rows change** (`0c25568`): the Calendar route navigates with `resetScroll: false` (`routes/_app/calendar/index.tsx:22`), so the router's scroll restoration no longer puts back the old pixel position. The keyboard's place is kept as a time, not a row.
  - **What `resetScroll: false` does:** the router normally scrolls everything back to where it was for that address (or the top) after each navigation. Here it leaves the page alone, because the grid scrolls itself.
- **A click selects 15 minutes** in 5-minute rows too (`aeda60f`, `CalendarGrid.tsx:446-451`), and the booking dialog lists quarter hours plus 5-minute times near the chosen one (`shownStarts`, `BookAppointmentDialog.tsx:131-137`).
- **Compact by the card's width** (`e051199`, `useNarrowerThan.ts`): a `ResizeObserver` measures the grid's card, and the week is compact only when the card is narrower than the full week (39rem). It's false until measured, so the server's full week matches the first render.
  - **An analogy:** choosing a folding table by measuring the alcove, not the whole room.
- **Phone-width headings** (`848477e`): "Tue" over "17", and items show their start time alone in a card under 4.5rem wide (CSS container queries: rules that look at the element's own width, not the window's).
- **The Book or Block menu below a wide selection** (`60fd47c`, `selectionMenuPlacement`): beside it up to 320 px, below it when wider.
- **`PageHeader` wraps its actions** (`16e5ca7`, `PageHeader.tsx:47, 82`): on every page, the actions go under the title when they don't fit beside it.
- **The phone's top bar uses the app's colours** (`bd6fa7a`, `AppLayout.tsx` `SideNavTheme`): the side nav's dark theme is skipped where Astryx renders the nav's heading in the phone's top bar (`useSideNavRenderMode() === 'topbar'`). The column and the drawer stay dark.
- **Docs:** decisions 1, 3, 33, 67, 68 and 69, `docs/architecture.md`, both `AGENTS.md` files, a new UI parity screen, and the record of each finding in `pr-108-calendar-narrow.md`.

## Design choices

- **One call per day, rather than a batch endpoint:**
  - **Chosen because:** the API already has a per-day `PUT`/`DELETE`, and a week has at most five days.
  - **Worse at:** it isn't one transaction, so some days can change and others not (the dialog says which), and each success refreshes every calendar query (Remaining risks).
- **"The default week's" as one of the templates, rather than a separate "Reset" button:**
  - **Chosen because:** one control, one list of days.
  - **Worse at:** the dialog must decide which days need a `DELETE` from its own copy of the week (finding 6).
- **Reading the whole week from the day view, rather than offering only the day shown:**
  - **Chosen because:** the user asked for applying across days, and each day must say what applies now.
  - **Worse at:** the only read that gives each day's template also returns its appointments, so it records the week's patients as listed (finding 5).
- **Measuring the card, rather than a CSS container query alone:**
  - **Chosen because:** compact changes what the grid renders (five columns, other headings), not just how it looks, so React must know.
  - **Worse at:** a narrow window shows the full week for a frame before it goes compact. Decision 67 says so.
- **The row length as a saved default plus the address, rather than the address alone:**
  - **Chosen because:** it follows the person to the Calendar from anywhere (the narrow review's finding 7).
  - **Worse at:** two sources of truth. Back undoes the address but not the setting (finding 4).
- **Wrapping every page's header, rather than only in a narrow layout:**
  - **Chosen because:** one rule fixes the 390 px overflow everywhere.
  - **Worse at:** a wide page message now forces a wrap too, on wide screens (finding 1). And it can only wrap what it's given: the Calendar groups its own buttons in a row that doesn't wrap (finding 2).
- **Dropping the dark theme on the top bar, rather than giving the bar a dark background:**
  - **Chosen because:** the bar sits on the page's own background; matching it is the smaller change.
  - **Worse at:** nothing found. The drawer stays dark, as before.

## How it was checked

- **Read:** the whole diff; `ApplyTemplateDialog.tsx`, `CalendarPage.tsx`, `CalendarGrid.tsx`, `PageHeader.tsx`, `Page.tsx`, `AppLayout.tsx`, `AppSideNav.tsx`, `useNarrowerThan.ts`, `listSettings.tsx`, `BookAppointmentDialog.tsx`, `AppointmentDialog.tsx`, `src/api/calendar.ts`, `ConfirmDialog.tsx`, `AlertMessage.tsx`, `PatientFlagBanners.tsx`, `ProcedurePage.tsx`, `ViewPatientPage.tsx`.
- **The API:** `ApplyDayTemplateHandler` (takes the calendar lock, 15 s), `ClearDayTemplateHandler` (404 when the day has no override), `GetCalendarHandler` (records `PatientListed`), `CalendarMapper` and `AppointmentMapper` (when items are movable).
- **Library source:** TanStack Router's scroll restoration (`router-core` 1.171, `scroll-restoration.js`, and where `resetScroll` sets `_scroll.n`); Astryx `Stack`/`StackItem` (`fill` is only `flex-grow: 1` and `min-width: 0`), `Banner` (no maximum width), `AppShell` and `SideNav` (the `topbar`, `drawer` and `default` render modes).
- **Docs:** decisions 1, 3, 33, 34 and 67 to 69; `pr-108-calendar-narrow.md`; both `AGENTS.md` files; `docs/governance/dpia.md:156` and `data-inventory.md:31`.
- **Tests:** `npx vitest run` on the 9 changed test files (`ApplyTemplateDialog`, `useNarrowerThan`, `listSettings`, `AppLayout`, `CalendarGrid`, `CalendarPage`, `calendarSearch`, `BookAppointmentDialog`, `AppointmentDialog`): **155 passed**. ESLint on the 16 changed source and test files: clean. `npm run typecheck`: clean.
- **Temporary tests** (written, run and deleted):
  - **A wobble on an item that has started:** a block 09:00 to 12:00 today, at 10:30. A plain click opened it; a 6 px wobble then a click opened nothing and moved nothing (finding 3).
  - **The dialog opens fresh:** choose a template, tick Friday, Cancel, open again: nothing chosen, only the start day ticked. Works.
  - **"The default week's" on a stale copy:** the page's copy says Wednesday has an override, the API says it hasn't. The dialog showed "One day couldn't be changed. Wed 25: This day has no day template of its own." (finding 6).
  - **Back after choosing Rows,** through the real router with scroll restoration on: the address went back to `/calendar`, the grid stayed at 5-minute rows, and the scroll went from 660 px (10:30) to 220 px (08:50) (finding 4).
- **Not done:** no browser, Docker, AppHost, E2E, UI parity or axe run (a separate browser check covers the browser). The Vitest runs left `src/routeTree.gen.ts` with only line-ending changes; it was restored with `git checkout`.

## Findings

### 1. A page message now pushes the header's actions onto a line of their own, on wide screens too

- **Severity:** medium. **Confidence:** medium (from the CSS rules and Astryx's source; not seen in a browser).
- **What could go wrong:**
  - **A restricted patient's procedure page, at full width:** the header used to be one row: the title, the "Restricted record: …" banner squeezed in the middle, then Block and Cancel at the right. Now it's three rows: the title, the banner across the page, then Block and Cancel at the **left**, under it. On the patient's page, Edit Patient and their procedures list, the banner likewise drops under the title.
  - **The Calendar on a 1366 px laptop:** a drag that clashes shows "That time overlaps Lunch, 12:00 to 13:00." in the header. If the title, the message and the three buttons don't fit on one line, the buttons drop to a second line and the whole grid jumps down, just after the failed drag.
  - **In a half view,** the "Close half view" button is in the same group as the actions (`PageHeader.tsx:84`), so it leaves the top-right corner and moves to the start of the wrapped line.
  - **It breaks a documented rule:** `MedApp.Web/AGENTS.md:16` says a page's messages sit in the header "so they never push the content down".
  - **Nothing would catch it:** no UI parity screen shows a page message or a restricted record.
- **Why:**
  - **How wrapping decides:** a wrapping row first lays its items out at their natural, unsqueezed widths, and breaks the line when they don't fit. Only then does it stretch or squeeze each line.
  - **The message's natural width is its whole text on one line.** Its `StackItem` (`:74-80`) is `size="fill"`, which only sets `flex-grow: 1` and `min-width: 0`; its starting width is still its content's (`flex-basis: auto`). The restricted banner's title alone is about 250 characters (`PatientFlagBanners.tsx:38`), wider than the 1,600 px page.
  - **So it never fits beside anything,** and takes a line of its own. Before, without wrapping, it was the item that squeezed to make room.
  - **An analogy:** packing a shelf by each book's width when lying flat. A long book gets a shelf to itself, though it would have fitted standing up.
- **Evidence:** `MedApp.Web/src/components/pages/PageHeader.tsx:47, 74-80, 81-86`; `MedApp.Web/src/components/patients/PatientFlagBanners.tsx:38`; `MedApp.Web/src/pages/procedures/ProcedurePage.tsx:307-320`; `MedApp.Web/src/pages/patients/ViewPatientPage.tsx:98-108`; `MedApp.Web/AGENTS.md:16`.
- **Fix:**
  - **Let the message take only what's left:** `style={{flexBasis: 0, minInlineSize: '12rem'}}` on the message's `StackItem` (a layout value the component has no prop for). It then forces a wrap only when the title, 12rem and the actions don't fit, as on a phone.
  - **Keep "Close half view" on the title's line,** outside the group that wraps.
  - **Check it:** add UI parity screens for a restricted patient's procedure page and for the Calendar with a page message, and look at them at 1366 px and in a half view. Run the full parity set before merging, as the merge flow requires for a UI change.
- **What happened to it:** fixed. The message's `StackItem` now has `flexBasis: 0` and `minInlineSize: 12rem` (only when there's a message), so it takes what the title and actions leave and wraps its own words. "Close half view" is outside the part that wraps, top right on the title's line. Seen in a browser: a restricted patient's procedure page at 1366 and 1920 px keeps Block and Cancel Procedure on the title's line (at 1366, with that 505 px title, the banner is narrow and tall), and both halves' close buttons sit level with their titles. Two UI parity screens added (`procedure-restricted`, `procedure-restricted-half`, using a restricted patient the sign-in step now finds). A Calendar screen with a page message isn't added: the message only comes from a failed save, which the parity tool doesn't make.

### 2. On a phone, the Calendar's three header buttons still run off the side

- **Severity:** low. **Confidence:** medium (widths estimated from the theme's sizes, and checked against the browser check's 60 px overflow; not measured).
- **What could go wrong:**
  - **At 390 px** the page has about 340 px for its content. Needs Booking (2), Apply Template and New need about 420 px side by side.
  - **The header now wraps,** so the buttons go under the title. But they can't wrap among themselves, so the row is still about 80 px too wide. New, the page's main action, sits partly or wholly off the right edge.
  - **It's the overflow `16e5ca7` set out to fix,** on the page it was fixed for. Decision 69 says the actions wrap "among themselves" (`docs/calendar-plan.md:121`); on the Calendar they don't.
- **Why:**
  - **`PageHeader` wraps the group it's given** (`PageHeader.tsx:82`). The Calendar gives it one item: its own `HStack` of the three buttons, which doesn't wrap (`CalendarPage.tsx:419`).
  - **Before this branch** the row had two buttons (about 270 px), which fit once under the title. Apply Template made it three.
  - **An analogy:** a removal van that can split its load across trips, but was handed one crate already nailed shut.
- **Evidence:** `MedApp.Web/src/pages/calendar/CalendarPage.tsx:418-455`; `MedApp.Web/src/components/pages/PageHeader.tsx:81-86`; `docs/calendar-plan.md:121`. The only other page that groups its actions does so with `wrap="wrap"` (`CalendarTemplatesPage.tsx:97`).
- **Fix:** pass the buttons as a fragment, as `ProcedurePage` does, or add `wrap="wrap"` to the Calendar's `HStack`. Check it at 390 px.
- **What happened to it:** fixed. The Calendar passes its header buttons as a fragment, so the header wraps them among themselves; the export page's two buttons likewise (the only other page with a non-wrapping group; the templates page's already wrapped). Seen at 390 px: New is at x 196 to 297, inside the window.

### 3. A click with a wobble on an item that has already started does nothing

- **Severity:** low. **Confidence:** high (shown with a temporary test).
- **What could go wrong:**
  - **A nurse records an outcome:** a 10:00 appointment is still Booked at 10:20. Appointments stay movable while Booked (`AppointmentWorkflow.cs:39`), and blocks and meetings all day (`CalendarMapper.cs:106, 130`).
  - **She clicks it on a touchpad,** and the pointer moves 6 px during the press. Nothing opens and nothing moves. She has to click again.
  - **The fix's claim doesn't hold here:** decision 68 and `pr-108-calendar-narrow.md`'s finding 1 say "a press that wobbles on its own row is a click, which opens it". That's true only for items that haven't started.
- **Why:**
  - **"Has it left?" is worked out from where the item could go,** not from where the pointer is (`CalendarGrid.tsx:629`).
  - **For a started item, nowhere is allowed:** its own start is in the past, so `to` is null even on its own row (`:626`). Null counts as "left".
  - **So the release is treated as a drag** (`:639-640`): it sets `justDragged`, finds no target and returns. The click that follows is swallowed (`:668-670`).
  - **This predates the branch** (the old code did the same with `isMoving`), but the branch set out to fix exactly this.
- **Evidence:** `MedApp.Web/src/components/calendar/CalendarGrid.tsx:624-629, 635-645, 667-673`; `docs/calendar-plan.md:118`; `docs/reviews/pr-108-calendar-narrow.md:93`.
- **Fix:**
  - **Decide by the pointer:** `hasLeft = drag.hasLeft || col !== drag.from || row !== drag.pickRow` (the column and row under the pointer), and keep `to` for the ghost and the drop only. A started item still can't be dropped anywhere, but a wobble on its own row is a click.
  - **Test it:** a block 09:00 to 12:00 at 10:30 today, nudged 6 px, opens.
- **What happened to it:** fixed, by the pointer: a press whose pointer moves less than 5 px (`DRAG_START_PX`), or never leaves the row and column it pressed, is a click. A new test covers a started block (a 6 px wobble opens it), a 4 px nudge across a row line (opens, doesn't move) and 6 px across one (moves a row). The browser found a second cause: a wobble across an item's words selected some, and Astryx's `ClickableCard` ignores a click while its text is selected, so movable items now have `user-select: none`.

### 4. Back after choosing Rows doesn't undo it, and moves the view to another time

- **Severity:** low. **Confidence:** high (shown with a temporary test through the real router).
- **What could go wrong:**
  - Someone has 10:30 at the top of the week and chooses 5-minute rows. The view keeps 10:30.
  - They press the browser's Back. The address goes back to `/calendar`, but the grid stays at 5-minute rows, because the saved default is now 5.
  - The router puts back the scroll position it saved for `/calendar`: 220 px, which in 5-minute rows is **08:50**. The view jumps, and Back seems to have done nothing else.
- **Why:**
  - **Rows writes twice:** the setting and the address (`CalendarPage.tsx:629-630`). Back only undoes the address.
  - **`resetScroll: false` covers only the page's own navigations** (`routes/_app/calendar/index.tsx:22`). Back and Forward still restore saved pixel positions, which belong to the old row length.
- **Evidence:** `MedApp.Web/src/pages/calendar/CalendarPage.tsx:186-187, 624-631`; `MedApp.Web/src/routes/_app/calendar/index.tsx:22`; `MedApp.Web/src/app/listSettings.tsx:50, 55`.
- **Fix:** treat the row length as a preference, not a step in history. Change the address with `replace: true` when Rows is chosen (an option on `onSearchChange`), so Back goes to where the person was before the Calendar's last real move. Test it with the router, as the scroll test does: choose 5, go Back, and expect the time at the top kept.
- **What happened to it:** fixed. Rows changes the address with `replace` (`onSearchChange(next, {replace: true})`, passed through to the router), so Back goes to where the person was before. The router test now checks the history has no new entry.

### 5. Apply Template from the day view logs the whole week's patients as listed

- **Severity:** low. **Confidence:** high.
- **What could go wrong:**
  - A nurse in the day view opens Apply Template to make Friday an empty day.
  - The dialog reads the week's calendar (`ApplyTemplateDialog.tsx:77`). That read records `PatientListed` for every patient with an appointment that week (`GetCalendarHandler.cs:31`).
  - The dialog shows no patients, only days and template names. But each of those patients' access reports now says the nurse listed them.
  - **The governance pack says** appointment reads are recorded "as reads of the patients they show" (`docs/governance/dpia.md:156`). These weren't shown.
  - **The project's precedent:** reads that show no patient record nothing (the side nav's pin labels). An access report is used in investigations ("who looked at my record?"), so entries for patients nobody saw are misleading.
  - **Decision 69 accepts it knowingly** (`docs/calendar-plan.md:126`), which is why this is low.
- **Evidence:** `MedApp.Web/src/components/calendar/ApplyTemplateDialog.tsx:76-77`; `MedApp.Application/Calendar/StaffCalendars/Queries/GetCalendar/GetCalendarHandler.cs:31`; `docs/governance/dpia.md:156`; `docs/governance/data-inventory.md:31`.
- **Fix:** give the dialog a read of the days alone: each day's template, override and past flag, without appointments, recording nothing (a new endpoint, or a flag on `GET api/calendar` that leaves out the items and the log). Classify it in `EndpointCoverageTests`. Or, to keep it, change `dpia.md:156` to say a template change reads the week.
- **What happened to it:** fixed properly, not just documented. `GET api/calendar` takes `withoutAppointments`, which leaves the appointments out and records nothing (`GetCalendarHandler`, with a unit test and an integration test in `EndpointCoverageTests`). The dialog reads the week that way (`calendarDaysQuery`). `dpia.md`, decision 69, both `AGENTS.md` files and `architecture.md` say so.

### 6. "The default week's" trusts the page's copy of which days have overrides

- **Severity:** low. **Confidence:** high (one case shown with a temporary test).
- **What could go wrong:**
  - **Removed meanwhile:** an admin removes Wednesday's override of a nurse's calendar. The nurse's page still has the old copy. She chooses "The default week's" for Wednesday. The dialog sends a `DELETE`, the API answers 404, and the dialog says "One day couldn't be changed. Wed 25: This day has no day template of its own." The day is already as she wanted.
  - **Added meanwhile:** the other way round, the dialog sends nothing for a day whose new override it doesn't know about, announces "Back to the default week's template on Wed 25" and closes. The override stays.
- **Why:** the days to change are picked from `isDayTemplateOverridden` in the cached week (`ApplyTemplateDialog.tsx:114-115`), and the API refuses to clear a day with nothing to clear (`ClearDayTemplateHandler.cs:17`).
- **Evidence:** `MedApp.Web/src/components/calendar/ApplyTemplateDialog.tsx:112-115, 126-130`; `MedApp.Application/Calendar/StaffCalendars/Commands/ClearDayTemplate/ClearDayTemplateHandler.cs:16-17`.
- **Fix:** make clearing idempotent on the server (a 204 when the day already follows the week: the goal is reached), and send the `DELETE` for every ticked day. Update the handler's unit and integration tests.
- **What happened to it:** fixed in the web app rather than the API (which keeps its 404, a true answer). The dialog reads the week afresh each time it opens, and Apply waits until it's current; "The default week's" sends the `DELETE` for every ticked day and takes a 404 as done; afterwards it reads the days again and names any day that still has a template of its own. Tests cover all three; the browser showed the 404 closing the dialog as done.

### 7. The booking dialog's times move when one is chosen

- **Severity:** low. **Confidence:** high (from the code).
- **What could go wrong:**
  - In 5-minute rows, a click at 10:05 opens booking with 09:55, 10:00, 10:05, 10:10 and 10:15 among the quarter hours.
  - The receptionist taps 14:00. The finer times now gather round 14:00: the three in the morning go (which can take the morning's group up a line), and 13:50, 13:55, 14:05 and 14:10 appear. So 14:00 moves two places along, and perhaps up a line, under her finger. A quick second tap to adjust lands on another time.
  - **Screen reader users** find the set of times changed after each choice.
- **Why:** the finer times are worked out around the *latest* choice (`chosenStart`, `BookAppointmentDialog.tsx:236`), and the list is rebuilt on every choice (`:131-137`).
- **Evidence:** `MedApp.Web/src/components/calendar/BookAppointmentDialog.tsx:131-137, 236, 601`.
- **Fix:** anchor the finer times on the time the dialog opened with (the selection's start), so the list never moves. If nudging another time by 5 minutes is wanted, add "5 minutes earlier" and "5 minutes later" beside the chosen time instead.
- **What happened to it:** fixed. The finer times gather round the time selected on the calendar (or the appointment's own when rescheduling; on a day with neither, the first time chosen there), and stay put as other times are chosen. The tests check the list doesn't change.

## Browser check

A separate check looked at the branch in Chromium (Playwright), on a throwaway copy of the demo data, at 390, 960, 1366 and 1920 px, light and dark. It found seven more things; all are fixed and were looked at again in the browser.

| # | Severity | What the browser showed | What happened |
|---|---|---|---|
| 8 | medium | **At 390 px the Calendar's toolbar took seven lines,** leaving the grid about 320 of 844 px. | Fixed. Under 44rem the toolbar keeps Previous, Today, Next, the view and the date in view, at the small size, and puts whose calendar, Rows, the default week and the templates link under **View options** (a menu). Two lines at 390 px (grid 494 px), one at 960 px (grid 774 of 1000). Astryx's date field is at least 180 px, which decided the order: the day buttons and the view share the first line. |
| 9 | low to medium | **Apply Template in the week view always ticked today,** even with another day in the address, so ticking Thursday and Friday changed today too unless unticked. | Fixed. A week ticks the address's date when it's in that week and can change, and nothing without one. Seen: `date=2026-11-19` ticks only Thu 19; no date ticks nothing. |
| 10 | low | **The templates page's picker sat 16 px above New Day Template**, keeping a field's empty status line. | Fixed with `NO_FIELD_STATUS_LINE`. Both are now centred on the same line at 1366 px (y 54). |
| 11 | low | **With 5-minute rows, "Not a working day" was centred** in the three-times-taller weekend column, so only "g day" showed. | Fixed. It sits at the top, sticky below the headings; it stayed in view at the top, middle and bottom of the scroll. |
| 12 | low | **At 960 px the toolbar's last line started 26 px right** of the lines above. | Fixed. The spacer between the two groups is gone; the toolbar uses `justify="between"`, so a wrapped group starts on the left (seen at 1100 px). At 960 the toolbar is now the narrow one, on one line. |
| 13 | low | **At 390 px, tall items in the compact week showed only their start time.** | Fixed. Items two rows or more tall show their name under the time ("14:30 / Rhys Kaur"). |
| 14 | low (on `main` too) | **Axe's `scrollable-region-focusable` on Home's day calendar** when nothing inside can take the focus. | Fixed. When nothing in the grid's scrolling area can take the focus, the area is a tab stop of its own, named "<label>, scrollable". |
| 3 | low | **Small wobbles:** a 3 to 6 px sideways wobble did nothing, and a 2 to 4 px nudge across a row line did nothing or offered to move the item. | Fixed (finding 3). Seen: a plain click, a 6 px sideways wobble, and 4 px and diagonal nudges across a row line open the item, on a started item too; 6 px or more across a row line, or 30 px, offers to move it. |

**The axe audit** (`calendar`, `home`, `patient-view`, `procedure-view` and the two new restricted screens, in three colour themes): one finding, `label-content-name-mismatch`, on the Audit Log page that the new half-view screen opens beside the procedure: `RowCards`' sort button shows "Sort: When" but is named "Sort by When, descending". It's on `main` too, untouched by this branch, and left as a to-do.

## Remaining risks

- **Not seen in a browser by this review:** the header change on every page, the top bar in light and dark (and the drawer, unchanged in code), the container queries on a phone, and the frame of full week before the compact one. The parallel browser check covers these.
- **Applying refreshes a lot:** each successful day refreshes every calendar query and every template list (`useRefreshCalendars`), so five days can mean five rounds of refetches, most cancelled by the next. One refresh after `allSettled` would do.
- **A past week still shows Apply Template:** its dialog has every day disabled. Harmless, but the old menu was hidden on past days.
- **The Rows menu marks the current choice with an icon only,** not as a checked menu item (`menuitemradio`). The button's name ("Rows: 5 min") says it, so screen readers aren't left without it.
- **`selectionMenuPlacement`'s wiring isn't tested:** jsdom lays nothing out, so the slot's width is always 0 and the menu always "beside". A tall selection that runs past the visible grid relies on Astryx flipping the menu "below" to above.
- **Not one transaction:** a partial failure leaves some days changed (documented, and the dialog names them).
- **Tests not written for:** the dialog opening fresh after a close (it does). (The header with a message, and "The default week's" on a day with nothing of its own, now have tests.)
- **After the fixes:**
  - **A long page message beside a long title is narrow and tall** (at 1366 px, the restricted banner is about 180 px wide and 200 px tall beside a 505 px procedure title). The actions stay on the title's line, which was the point, but the header is taller than before.
  - **View options lists everyone as a submenu,** without the staff picker's search. Fine for a team of a dozen; a large site would want the search back (a dialog, or the picker inside the menu).
  - **The narrow toolbar is chosen after mount,** like the compact week, so a phone shows the full toolbar for a frame first.
  - **The axe audit's one finding** (`RowCards`' sort button name, on `main` too) is left as a to-do.

## Say it like this

"The calendar's per-day template menu became an Apply Template button that stamps a day template onto any days of the week, one API call per day, reporting exactly which days failed. The trade-off is no transaction across days, chosen because the per-day endpoint already existed and a week has five days. The review's main catch is that making every page header wrap also lets a long error or warning banner push the buttons onto their own line on wide screens, so the fix should let the message take only the space that's left."

## Detail

### Why the banner takes its own line (finding 1)

The header is one flex row with `flex-wrap: wrap`: `[title] [message: StackItem fill] [actions]`.

| Step | Rule | The restricted banner |
|---|---|---|
| Starting width | `flex-basis: auto` means the content's max-content width | about 1,900 px (250 characters at body size, plus icon and padding) |
| Clamped by | `min-width: 0` (StackItem's reset), no max | still about 1,900 px |
| Line breaking | items are placed by that width; one that doesn't fit starts a new line, alone if it never fits | its own line |
| Then | the actions don't fit after it either | a third line, at the start (left) |

With `flex-basis: 0` and a `min-inline-size` of 12rem, its starting width is 12rem, so it shares the title's line whenever the title, 12rem and the actions fit.

### The wobble, worked through (finding 3)

A block 09:00 to 12:00 today, now 10:30, pressed on its 10:30 row:

| Step | Code | Value |
|---|---|---|
| A 6 px move | `isMoving` (`:615`) | true |
| Where it would go | start = 09:00 + 0 rows (`:624`) | 09:00 |
| Allowed? | `start > nowMs` (`:626`) | no, so `to = null` |
| Has it left? | `!(to !== null && …)` (`:629`) | **true** |
| Release | `hasLeft`, so `justDragged = true`; no target, return (`:639-642`) | nothing moves |
| The click | `justDragged` swallows it (`:668-670`) | nothing opens |

### Back after Rows (finding 4)

| Moment | Address | Rows | Scroll | Top of view |
|---|---|---|---|---|
| Before | `/calendar` | 15 (880 px day) | 220 px | 10:30 |
| Choose 5-minute rows | `/calendar?rows=5` | 5 (2,640 px day) | 660 px | 10:30 |
| Back | `/calendar` | still 5 (the setting) | 220 px, restored by the router | 08:50 |

### The commits

| Commit | What |
|---|---|
| `492f8f9` | Apply Template replaces the day's template menu |
| `68a9b95` | The narrow calendar's review |
| `cecdeed` | A dragged item keeps its own start and length |
| `1dbff42` | A drag doesn't redraw the slots |
| `54af5e8` | Quarter-hour times at full contrast |
| `01fa01e` | The Rows menu |
| `0c25568` | Same time on a row change; `resetScroll: false` |
| `aeda60f` | A click selects 15 minutes; 5-minute booking times near the chosen one |
| `d9a8863` | The row length remembered as a default |
| `e051199` | Compact by the card's width; a chosen week kept in the address |
| `848477e` | Phone-width headings, wrapping items, start times when narrow |
| `60fd47c` | The Book or Block menu below a wide selection |
| `16e5ca7` | `PageHeader` wraps its actions |
| `bd6fa7a` | The phone's top bar in the app's colours |
| `892f7b7` | Docs and comments |
| `c37782a` | The record of each finding's fix |
