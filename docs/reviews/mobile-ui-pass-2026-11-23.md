# Mobile UI pass, 23 November 2026

The owner's request: "Do a full pass over the mobile UI, check that everything works, do a report of what is overflowing on the page and figure out a better way of doing every page."

**What was checked:** the light demo (`npm run dev:demo`, the working tree at `1bbb3662`, which has the touch-screen side nav fix `7532ef1d` and the bell beside the menu button), in Chromium through Playwright, as an iPhone 13 (390 × 664 px of page, touch, mobile) and a small Android-like phone (360 × 740 px, touch). Every one of the 84 routes in `routeTree.gen.ts`, filled in with the demo's fixed IDs, as the account that can see it (admin for setup and oversight; Nadia Thornbury for clinical pages; Ravi Aldous for the lab; Saira Varma for the Eye clinic's home, pathway, referral and calendar), 91 page loads per size. Then the side nav drawer (every item, both sizes), sign out, the bell's list, the notepad sheet, the list options menu and its search, and the dialogs: Book Appointment (from the Calendar, a pathway and a patient), New Meeting, Finish Stage (here "Finish Without a Visit?"), Record Consent, Accept Referral, Create New User, and the More menus.

**How overflow was measured:** in the page, after loading: whether the document scrolls sideways; every visible element whose box passes the right edge (or left of 0), split into those escaping the page, those cut off by an `overflow: hidden` parent, and those inside a scroller that scrolls sideways (each such scroller named); text cut with an ellipsis (`scrollWidth > clientWidth`); vertical scrollers under 200 px tall; interactive elements under 44 × 44 px. Then every screenshot was looked at, and the cards' contents scrolled and captured on the long pages. The scripts and screenshots are outside the repository (the session's scratchpad); numbers below are from them.

## 1. Summary

- **84 routes checked** (91 loads per size, as some pages were opened as two accounts). Three can't be seen on their own in the light demo: `/change-password` and `/set-up-two-factor` go to Home when no change is due, and `/notepad` opens the notepad sheet over Home on a phone (by design). `/calendar/needs-booking` and `/admin/calendar-templates` are redirects to `/calendar/requires-appointment` and `/calendar/templates`.
- **No page scrolls the whole window sideways**, at either size, and there were **no console errors and no error states** on any page.
- **Content overflows sideways on 8 pages at 390 px and 14 at 360 px** (cut off by a parent, or making a card or the page's main area scroll sideways): Patients, a pathway, MDT, Edit Referral, Hospital Requests, Register Hospital Request, Retention Review and Users at both sizes; at 360 px also Home, Results, Lab Cases, Audit Log, Insights and Account Settings. Worst: a pathway's MDT actions (up to 258 px off the card), Users (158 px), Register Hospital Request (119 px), Hospital Requests (72 px, the whole page area scrolls sideways).
- **Squeezed rather than overflowing**, which measurement alone misses: two-column forms whose first column collapses (Add and Edit Patient's First name box is 0 px wide), Hospital Request's details one letter per line, card titles wrapped to four lines by the buttons beside them.
- **13 lists' tabs** are a sideways scroller showing 302 of up to 1,020 px (a design choice, but later tabs are out of sight).
- **Touch targets:** nearly every button is 28 to 32 px tall (the bell 28 × 28, the menu button 32 × 32, the drawer's rows 32 px, row card icons 28 px). "Bigger buttons and fields" exists in Account Settings but is off.
- **The side nav fix holds:** on touch there are no row menus or half view buttons, and a tap on any item opens its page and closes the drawer, at both sizes (every clinician item, and Samples for the lab).

**Broken (can't be done on a phone):** all seven fixed on 24 November 2026 (see [5](#5-fixed-24-november-2026)).
1. **Sign Out from the drawer does nothing visible.** The "Are you sure?" dialog opens inside the desktop side nav column, which is `display: none` on a phone: it's open but 0 × 0 px, the drawer closes, and nobody can confirm. Both sizes. **Fixed** (`dd702b7a`): the dialog is the app's (`AppLayout`), outside the shell.
2. **Add Patient and Edit Patient:** the First name and Date of birth column is 10 px wide (the First name box 0 px), with the labels overlapping the next column's. A patient can't be added on a phone. **Fixed** (`0d35662d`): one column on a phone.
3. **A pathway's MDT section:** Record Outcome overlaps the meeting's text, and Set Presenter, Move and Withdraw are cut off by up to 228 px (258 px at 360), out of reach. **Fixed** (`327a77ab`): the actions are under the item, Record Outcome and an Actions menu.
4. **The MDT page:** its title, description, Print, Team and Meeting leave the meeting's card a 41 px window (85 px at 360) on 6,678 px of content. **Fixed** (`16b9d9c9`, with `327a77ab`): on a phone the page scrolls as a whole, without the description.
5. **Hospital Request:** two columns that never stack, so the patient's name, date of birth and NHS number show a few letters per line, and the lab case number one character per line. **Fixed** (`fc6f32bb`): the cards stack, and their labels go above the values.
6. **Users:** each card's eight icon buttons sit on top of the username; the row is 128 px (158 px) wider than the card. **Fixed** (`327a77ab`, `34bfec2b`): one Actions menu under the username, and the name and details cut short beside the avatar rather than widening the card.
7. **Retention Review:** the card's Review, Keep and Anonymise buttons take the title's place, so the patient's name doesn't show at all (and Anonymise is cut 27 to 57 px). Nobody can tell whose record they'd anonymise. **Fixed** (`327a77ab`): the name first, then Review and an Actions menu (Keep, Anonymise).

## 2. Shared causes, and a better design for each

Fixing these components fixes most pages. Each names the pages it affects.

### 2.1 `FormSection`'s two fixed columns (`src/components/forms/FormSection.tsx`)

**What happens:** `useGrid` (the default) renders `<Grid columns={2}>`, which Astryx makes exactly two `1fr` tracks that never reflow; a field's minimum content widens one track and starves the other (Add Patient's tracks measured 10 px and 266 px). Pages: Add Patient and Edit Patient (broken), Edit Referral (Received on date box cut 18 px, 33 px at 360; Clinic "Dermatol…"), Register Hospital Request (Hospital number, Surgeon and the urgent checkbox 89 to 119 px off the card, which scrolls sideways), Edit Pathway (Title box and Target date squeezed).

**Better:** `columns={{minWidth: 240, max: 2}}`, Astryx's own advice for columns that reflow: one column on a phone or half view, two where they fit. Check that no form relies on two columns for meaning (the date and NHS number pairs don't).

**Fixed** (`0d35662d`): in a narrow layout (`useIsNarrow`) `FormSection` uses `{minWidth: 240, max: 2}`; wide, it keeps its two fixed columns, so desktop is unchanged.

### 2.2 Rows with a title and actions side by side

**What happens:** four components put a title and its buttons in one row that doesn't wrap, so the buttons keep their width and the title gets what's left, which on a phone is little or nothing:
- `RowCards` (`src/components/tables/RowCards.tsx`): the title column beside the `actions` column. Users (usernames under the icons), Retention Review (no patient name at all), Consent Forms ("Consent to a proc…" twice, so the two can't be told apart), Hospitals, Sites and Rooms, Pathway Templates, Roles, Stage Library, Activity Types, Teams, Sample Types (names and their lines cut with an ellipsis).
- `PanelHeader` (`src/components/views/PanelCard.tsx`): title, summary and actions in one `HStack`. Home's Today card ("Today, Monday 23 November" on four lines beside Open Calendar, which is cut 23 px at 360), Clinic Patterns' card ("Tuesda / ys," beside Edit and Create Sessions).
- `MdtItemList`'s `ListItem` `endContent` (`src/components/mdt/MdtItemList.tsx`): the pathway's MDT section and the MDT page (broken, above).
- `FormCard`'s footer (`src/components/forms/FormCard.tsx`): one `HStack`, no wrap. Edit Role ("Arch…", "Can…", "Sa…"), New Day, Week and Letter Template ("Can…", "Create Day Temp…").

**Better, one rule for all four:** below about 480 px (or when the row's own width is short, `useNarrowerThan`), the title takes the whole first line and the actions go on a line of their own under it, wrapping. When a row has more than two actions, they become one "Actions" button (`AppIcon.Menu`) opening a menu, with at most the primary one left as a button. A card footer wraps, the primary button last and full width. Names and titles on cards wrap to two lines rather than being cut with an ellipsis.

**Fixed** (`327a77ab`, `a9f2c65c`), except the wrapping of names and the primary button full width:
- `RowCards`: on a card narrower than 30rem (`COMPACT_ROW_REM`, measured with `useNarrowerThan`), the title has the first line and the actions go under it; `TableActions` folds more than two into one `ActionsMenu` (new, `src/components/shared/`). Retention Review's own cell does the same (`useIsCompactRow`): Review, then Actions (Keep, Anonymise).
- `MdtItemList`: in a list narrower than 30rem, the actions go under the item, the first a button and the rest in an Actions menu when there are more than two.
- `PanelHeader` and `FormCard`'s footer wrap in a narrow layout (`useIsNarrow`): Home's Today title on two lines with Open Calendar under it; Edit Role's footer Archive, Cancel, then Save, uncut.
- The setup lists that are `List`s rather than tables (Consent Forms, Hospitals, Specimen Setup) use the new `ActionsList`, with the same rule. Sites (deprecated) is left as it is.

### 2.3 `TwoColumns` doesn't stack by itself (`src/components/pages/TwoColumns.tsx`)

**What happens:** it always renders two equal halves; each page is meant to ask `useIsNarrow()` and stack or show one column instead. Two pages don't: Hospital Request (broken) and Colours (Light Mode and Dark Mode squeezed side by side, "Standard" spilling out of its swatch, the Dark Mode badge cut). Every other page using it does it by hand, 13 times.

**Better:** `TwoColumns` asks `useIsNarrow()` itself and stacks (or takes a `narrow="stack" | "tabs"` prop), and the pages drop their own copies.

**Fixed** (`fc6f32bb`): `TwoColumns` stacks in a narrow layout, each card as tall as its content, and the page scrolls. The other pages' narrow layouts aren't copies of stacking (they merge the columns into one card of folding sections), so they stay. Hospital Request's details also put their labels above the values in a card under 30rem. As `useIsNarrow` is true below 1,400 px, Hospital Request and Colours now stack on a 1,280 px laptop too, as the other two-column pages already change layout there.

### 2.4 `Page` with `fill`: the chrome eats the screen (`src/components/pages/Page.tsx`)

**What happens:** on a phone, a `fill` page's header (a title often on two lines, Back, a row or two of actions), then the card's header bar and its pinned footer, leave the card's scrolling body a small window: about 260 of 664 px on a pathway, about 300 px of calendar grid, 280 px of a letter (its body box 104 px tall), 120 px of a patient's pathway list, 118 px on Closed Days, 39 px of Home's Today calendar (115 px at 360), 41 px on MDT. Lists fare better, but two or three row cards a screen.

**Better:** on a phone (`useIsNarrowWindow`), `fill` turns off: the page scrolls as a whole, cards grow to their content, and the header is sticky and compact (the title on one line with an ellipsis, Back as an icon beside it, the primary action as a button and the rest in a More menu). A card's footer (Save and Cancel, Edit Details, View Patient) becomes a bar pinned to the bottom of the screen. That's one change in `Page` plus `PanelCard`, `FormCard` and `FoldingCard` reading it.

### 2.5 `PageHeader` actions

**What happens:** actions wrap under the title (good), but each one is a full labelled button, so a page has two or three rows of them before any content: Calendar (Requires Appointment, MDT, Apply Template, New: two rows, three at 360), a pathway (Put on Hold, Cancel Pathway), a referral, letter, specimen or data request (two buttons and More). Hospital Requests passes its hospital filter and Register Request in an `HStack` that doesn't wrap, so Register Request is cut and the page's whole main area scrolls 42 px (72 px at 360) sideways.

**Better:** below 480 px the header keeps one primary action (New, Register Request, Approve) and puts the rest in one More menu; destructive ones (Cancel Pathway) only in the menu. Filters (the hospital select) move into the list's `ListFilterBar` menu, not the header.

### 2.6 `ListFilterBar`'s tabs (`src/components/tables/ListFilterBar.tsx`)

**What happens:** the tabs scroll sideways (302 px shown of 522 to 1,020 px) on Patient Pathways, Pathways, My Worklist, Referrals, Letters, Results, Lab Cases, Samples, Calendar Templates (two rows of tabs there), Activity Log, Audit Log, Admin Calendar Templates and Data Requests; the last tabs are out of sight ("Information reque…", "Awaiting sign…").

**Better:** on a phone, the tabs become one select ("Showing: Awaiting triage ▾") beside the options menu, listing every tab; the power search opens as a full-screen sheet.

### 2.7 `RowCards` contents (`src/components/tables/RowCards.tsx`)

**What happens:** every column after the first is listed under the title, so a card is 160 to 220 px tall and a screen shows two or three (Patients, Pathways, Referrals, Lab Cases, Hospital Requests). Empty values still take a line (Activity Log's "About" and "Details", Audit Log's "Details"). Internal IDs show (Patients: "ID: 000000d4").

**Better:** a column says whether it shows on a phone's card (`card: 'title' | 'subtitle' | 'meta' | 'hidden'`). A card shows the title (the patient's name or the record's title), one subtitle line (NHS number, or the patient for a record about one), and at most two status tokens on one line (status, overdue, priority); the rest is behind a tap on the card (it opens the record, which has them all). Empty values are left out.

### 2.8 Touch targets

**What happens:** buttons are Astryx's default 28 to 32 px tall, icon buttons 28 × 28; the bell 28 × 28 and the menu button 32 × 32 beside each other in the top bar; the drawer's rows 32 px apart. The median page has 12 interactive elements under 44 px.

**Better:** turn on the existing "Bigger buttons and fields" (`biggerTargets`, `src/app/accessibility.tsx`) by default on touch screens (`(pointer: coarse)`), unless the person turned it off; at least make the top bar's two buttons and the drawer's rows 44 px.

### 2.9 Dialogs (`DialogFrame`)

**What happens:** every dialog checked fits the width (358 of 390 px), nothing overflows, the body scrolls inside, and Escape, Close and Cancel close it. But they stop well short of the screen's height (the long ones 498 of 664 px), so the scrolling body is 280 to 376 px: Book Appointment's 822 to 958 px of content (patient, with, length, then the week's days and times, all below the fold), New Meeting's 1,234 px in 320 px, Accept Referral's 989 px in 376 px. Book Appointment's day buttons ("Mon 23, 3 free") are cut with an ellipsis.

**Better:** on a phone, every dialog is a full-screen sheet: title and Close at the top, the body scrolling, the footer pinned at the bottom. Book Appointment and New Meeting become steps (who and how long, then the day, then the time) rather than one long form.

### 2.10 The side nav, the bell and the notepad

- **Sign Out (broken):** `AppSideNav`'s confirm dialog is rendered with the side nav column, which a phone hides; render it at the app's level (or in the drawer), as the drawer is what's shown. **Fixed** (`dd702b7a`): `AppLayout` gives the nav `AskToSignOutContext` and renders `SignOutDialog` itself (the drawer's copy of the nav was inside a hidden `Activity` once the drawer closed).
- **The drawer:** works. Tapping MedApp while on Home leaves the drawer open (no navigation happens); close it on any tap of a link.
- **The bell's list:** opens as a 632 px popover with its Close at the bottom, below the screen; a tap outside it (the 16 px margins) closes it. Better as a full-screen sheet with Close at the top, like the notepad.
- **The notepad sheet:** works (full screen, Close at the top). It remembers being open (`medapp.notepadOpen`), so a page opened or reloaded on a phone comes back under the full-screen sheet; on a phone, don't reopen it on load.

### 2.11 `DetailsPatient` and two-column `MetadataList`s

Fine at 390 px: names wrap ("HOLLOWAY, / Priya"), dates of birth over two lines. Keep two columns for short values; give the patient's name the whole width on a phone.

## 3. Page by page

Priority: **broken** (can't be done on a phone), **bad** (works, but hard or hidden), **cosmetic**. Overflow numbers are px past the card or page edge at 390 / 360 px. "Better" is what the page should do on a phone.

### Home

- **`/` (all four accounts)**: bad. No overflow at 390; Open Calendar cut 23 px at 360 (2.2). The Today card's title wraps to four lines beside Open Calendar, leaving the day's calendar 39 px tall for Nadia (121 px admin, 144 px lab); appointment labels cut ("Harriet Quill, Basal cell carcinoma, left tem…"). Better: Needs your attention first (it's good: one line each), then Today as a list of the day's appointments and blocks (time, who, what), not a grid, with Open Calendar as a link under it. The lab's home is fine (lists, one line each).

### Patients

- **`/patients/`**: bad. Email cut 6 / 36 px; 2 cards a screen; internal ID shown. Better: card = name, NHS number, date of birth with age; three icons behind one Actions menu (2.2, 2.7); Add Patient stays.
- **`/patients/$id/`**: bad. No overflow. The Details card shows about 300 px between the header and a footer of three buttons. Better: the page scrolls (2.4); patient banner, then Pathways, Appointments, Timeline as folding sections; Book Appointment as the one header action, Pathways and Edit Patient in More.
- **`/patients/$id/edit`**: broken (First name 0 px wide, labels overlapping; 2.1). Better: one column. **Fixed** (`0d35662d`).
- **`/patients/$id/export`**: cosmetic. No overflow; scrolls as a page, which suits it. Print and Download JSON fine.
- **`/patients/$id/pathways`**: bad. Tabs scroll sideways; the list gets 120 px under the banner and New Pathway. Better: page scrolls, banner compact (name and NHS number on one line).
- **`/patients/add`**: broken (as Edit; 2.1). **Fixed** (`0d35662d`).
- **`/practices`** (admin): cosmetic. "Harbour View Medical Practice (synth…" cut. Better: wrap names; card = name, ODS code, status.

### Pathways

- **`/pathways/`**: cosmetic. Tabs scroll; titles wrap well. Better: card = title, patient, status and target date tokens; the rest behind a tap.
- **`/pathways/$id/`**: broken (MDT actions cut 228 / 258 px and overlapping, 2.2; **fixed**, `327a77ab`). Also bad: about 260 px of content between a header of two rows and a footer of two rows (Add Branch, Edit Details, View Patient). Finish Stage, Book Appointment and Record Consent open and work. Better: the page scrolls (2.4); the current stage first with its one primary action (Start Stage or Finish Stage) as a pinned bottom button; Stages as a progress bar that opens the list; Put on Hold, Cancel Pathway, Add Branch, Edit Details in More; MDT, Notes, Consults, Samples, Results, Consent, Files, Letters folded, each with its actions under its title.
- **`/pathways/$id/` (Eye clinic, Saira Varma)**: bad, for the same space; no MDT items, so nothing cut.
- **`/pathways/$id/edit`**: bad. Title box and Target date label squeezed (2.1). Better: one column.
- **`/pathways/new`**: cosmetic. With no patient chosen it's a single banner (correct: a pathway starts from a patient).
- **`/tasks`** (My Worklist): cosmetic. Tabs scroll (1,020 px of tabs); empty state fine.
- **`/team/`**: cosmetic. Second lines cut with an ellipsis ("· Target 20 Novemb…"). Better: wrap to two lines.
- **`/mdt`**: broken (41 / 85 px window; 2.2, 2.4). **Fixed** (`16b9d9c9`, `327a77ab`): the page scrolls on a phone, with no description; items' actions under each. Better: Team and Meeting as one select row (or in a menu), the description dropped on phones, Print in More; the meeting's items as cards (patient, pathway, presenter, outcome), actions under each.
- **`/admin/pathway-templates/`**: cosmetic. Stage summaries cut ("11 stages: Triage, Eczema assessment, Take bi…"); icons beside names. Better: name, team, status; the stage summary behind a tap.
- **`/admin/pathway-templates/$id`** and **`/new`**: bad. Each stage row's parts sit in narrow columns ("2. Dermosco…", a 120 px column of description, "1 sele…" for what it comes after); 74 and 50 texts cut. Better: on a phone, the editor's stages as a list of one card each (number and name, then its settings stacked full width); editing routes (Draw Routes) desktop only, saying so.
- **`/admin/stages`**: cosmetic. Descriptions cut; three icons beside each name (2.2).

### Referrals

- **`/referrals/`**: cosmetic. Tabs scroll ("Information reque…"); cards tall but readable.
- **`/referrals/$id/`** (both clinics): bad. Details readable; triage (Accept, Reject, Redirect, Ask for More Information) is far down the card. Accept Referral opens and works (989 px of form in a 376 px body, 2.9). Better: the page scrolls; triage's four buttons as a pinned bottom bar while it awaits triage.
- **`/referrals/$id/edit`**: bad. Date box cut 18 / 33 px, Clinic "Dermatol…", Source "None (as typed b…" (2.1).
- **`/referrals/new`**: no problem found.

### Lab

- **`/lab/`** (Ravi Aldous): cosmetic. Second lines cut with an ellipsis (24 of them). Better: wrap to two lines.
- **`/lab/cases/`**: cosmetic. Tabs scroll; hospital name 14 px over at 360.
- **`/lab/cases/$id`**: bad. Details readable; Enter Results and Authorise, the case's main action, is inside "What's next", not in the header. Better: that action as the pinned bottom button.
- **`/lab/samples`**: cosmetic. Tabs scroll ("Repo…").
- **`/specimens/$id/`**: cosmetic. Print Label, Print Request Sheet and More on two rows (2.5).
- **`/specimens/$id/print`**: cosmetic. The label preview is fine; printing from a phone is unlikely.
- **`/hospital-requests/`**: bad. Register Request cut, the main area scrolls 42 / 72 px sideways (2.5). Better: Register Request as the header's one action, the hospital filter in the list's menu.
- **`/hospital-requests/$id`**: broken (2.3). **Fixed** (`fc6f32bb`). Better: one column: Details, then the sample and lab case.
- **`/hospital-requests/new`**: bad. Fields 89 / 119 px off the card (2.1); selects cut ("Choose the t…").
- **`/admin/specimen-setup`**: cosmetic. Two lists in one page; lines cut; footer buttons on two rows.
- **`/admin/hospitals`**: cosmetic. Names cut ("Hillcrest Communi…"), three icons and a status beside them (2.2).

### Results

- **`/results/`**: cosmetic. Tabs scroll; "Sample MA26-000107" 20 px over at 360; the result's name breaks mid-word ("MA26-…").
- **`/results/$id/`**: cosmetic. Read-only banner and details fine; sign-off (when awaiting) would be the pinned action.

### Letters

- **`/letters/`**: cosmetic. Tabs scroll; cards fine.
- **`/letters/$id`**: bad. Writing a letter in a 104 px box inside a 280 px window, with Save at the bottom of the card. Better: page scrolls, the letter's text box grows with its text, Save pinned; Submit for Approval or Approve as the one header action.
- **`/admin/letter-templates/`**: cosmetic. Two-line names cut ("Onward referral to hospital · Dermatolo…").
- **`/admin/letter-templates/$id`** and **`/new`**: bad. Footer buttons truncated ("Ca…", "Create Letter Te…"; 2.2); body boxes 128 px.

### Calendar

- **`/calendar/`** (day, both clinics): bad. Four header buttons on two rows (three at 360), then the date row, before about 300 px of grid; it opens near the current time. Book Appointment opens and works (2.9). Better: on a phone, the header's New is the only button, the rest (Requires Appointment, MDT, Apply Template, View options) in More; the day as an agenda list (times down the side, items full width), with the grid one tap away.
- **`/calendar/?view=week`**: bad. Five day columns of 60 px: "Lunc / h" breaks, times cut ("09:…"); 136 interactive elements under 44 px. Better: on a phone, Week is a list of days, each with its items; or three days at a time, swiped.
- **`/calendar/clinics/`**: cosmetic. Fine; Site select sits alone on the right.
- **`/calendar/clinics/patterns`** (admin): bad. The pattern's title in a narrow column ("Tuesda / ys, / 09:00 / to 12:00") beside Edit and Create Sessions (2.2). **Fixed** (`327a77ab`).
- **`/calendar/needs-booking`** → **`/calendar/requires-appointment`**: cosmetic. The To book / Did not attend switch and a single "All" tab: drop the tab row when there's one tab.
- **`/calendar/templates/`** and **`/admin/calendar-templates`** (→ the same page): cosmetic. Two rows of tabs (Day / Week / Default, then Active / Archived / All), the first scrolling. Better: one select for both.
- **`/calendar/templates/day/$id`**, **`/day/new`**, **`/week/$id`**, **`/week/new`**: bad for the new ones (footer "Can…", "Create Day Temp…"), cosmetic for edits. Blocks and days are long forms in a short window (2.4).
- **`/admin/activity-types`**, **`/admin/closed-days`**: cosmetic. Closed Days is a page of explanation before the list (118 px of it scrolls); move the explanation behind an "i".

### Setup

- **`/admin/users`**: broken (2.2). **Fixed** (`327a77ab`, `34bfec2b`).
- **`/admin/roles/`**: cosmetic. Descriptions cut.
- **`/admin/roles/$id`**, **`/admin/roles/new`**: bad. Footer "Arch…", "Can…", "Sa…" (2.2; the footer **fixed**, `327a77ab`); every permission's description cut to one line (30 of them), and it's what the permission does. Better: wrap descriptions; footer wraps.
- **`/admin/teams`**: cosmetic. The "Nobody can triage: Lab" banner squeezes the title; waiting-time targets cut ("Medium 63,…").
- **`/admin/sites`** (deprecated): cosmetic. Names cut beside five icons (2.2).
- **`/admin/consent-forms`**: bad. Two forms both read "Consent to a proc…" (2.2). **Fixed** (`a9f2c65c`): names in full, status and Actions under them.
- **`/admin/settings`**: no problem found; a long card that scrolls.
- **`/admin/monitoring`**: no problem found; rules as sentences read well.
- **`/admin/jobs`**: cosmetic. Run Now beside the job's name pushes its description into a narrow column.

### Oversight

- **`/admin/insights`**: cosmetic. Three filter selects stacked; "Show Table" buttons cut; charts' rows 24 px over at 360. Better: the filters in one menu; each chart a card that opens its table.
- **`/admin/audit`**: cosmetic. Tabs scroll; a pathway title 12 px over at 360; empty "Details" lines.
- **`/admin/activity`**: cosmetic. Tabs scroll; empty "About" and "Details" lines (2.7).

### Privacy

- **`/admin/retention`**: broken (no patient name, Anonymise cut 27 / 57 px; 2.2). **Fixed** (`327a77ab`): name, then Review and Actions. Better: card = patient's name, date of birth, retention class and due date; Review as the card's tap, Keep and Anonymise in the record's review.
- **`/data-requests/`**: cosmetic. Tabs scroll; cards fine.
- **`/data-requests/$id`**: cosmetic. Review, Record as Sent and More on two rows (2.5).

### Account

- **`/account/`**: cosmetic. Scrolls as a page (right for it); the card footer's three buttons on two rows; an "Apply" button cut to "Ap…".
- **`/account/colours`**: bad (2.3). **Fixed** (`fc6f32bb`): the two modes stack.
- **`/account/email`**, **`/account/password`**, **`/account/security`**: no problem found.
- **`/accessibility`**: bad. The statement's list of settings cuts each description to one line ("Buttons and fields at least 44 pixels high, e…"), hiding part of the statement. Better: wrap them.
- **`/no-permission`**: no problem found.
- **`/change-password`**, **`/set-up-two-factor`**: not reached (they go to Home when nothing is due).
- **`/notepad`**: works as a full-screen sheet (2.10).
- **`/login`** (the light demo's account picker): bad. The accounts' names cut to "N", "Centre…", "Sair…" beside full-width Sign in buttons. Better: name and description above the button, the button full width. `/forgot-password`, `/reset-password`, `/confirm-email`: no problem found.

## 4. Suggested order of work

1. **Sign Out's confirm dialog** (2.10): small, and the only broken thing outside a page. Done (`dd702b7a`).
2. **`FormSection`'s columns** (2.1): one line; fixes Add and Edit Patient, Edit Referral, Register Hospital Request and Edit Pathway. Done (`0d35662d`).
3. **Title and actions rows** (2.2), in `RowCards`, `PanelHeader`, `MdtItemList` and `FormCard`'s footer: fixes Users, Retention Review, the pathway's MDT section, Consent Forms, Clinic Patterns, Home's Today header, the role and template editors' footers, and the cut names on about ten setup lists. Done (`327a77ab`, `a9f2c65c`), but for names wrapping rather than being cut.
4. **`TwoColumns` stacking itself** (2.3): fixes Hospital Request and Colours, and removes 13 hand-written copies. Done (`fc6f32bb`), with no copies removed (none was plain stacking).
5. **Bigger targets on touch by default** (2.8): a default, not new UI.
6. **A phone mode for `Page`** (2.4, 2.5): no `fill`, a compact sticky header with one primary action and More, card footers as a pinned bottom bar. This is the change that makes the pathway, letter, MDT, calendar and patient pages usable rather than cramped; it touches `Page`, `PageHeader`, `PanelCard`, `FormCard`, `FoldingCard`.
7. **Dialogs as full-screen sheets** (2.9), and the bell's list as one (2.10).
8. **Lists:** tabs as a select (2.6), shorter row cards (2.7).
9. **Then page by page:** the MDT page, the pathway page's stage-first layout, the Calendar's agenda day and week, Home's Today as a list, the pathway template editor's stages, Hospital Requests' header, the demo's login.

## 5. Fixed, 24 November 2026

The seven broken tasks, re-measured with the same scripts in the light demo, iPhone 13 (390 × 664) / 360 × 740, and each done by tap:

| Task | Before | After |
|---|---|---|
| Sign out from the drawer | no dialog shown; Sign Out can't be tapped | dialog 358 × 178 (328 × 178); tapped, at the login page |
| Add Patient | First name box 0 px, First name and Date of birth can't be tapped | every field 220 to 274 px (220 to 244); filled, saved, the patient added |
| The pathway's MDT section | Record Outcome over the meeting's text; Set Presenter, Move, Withdraw 58 / 152 / 277 px off the card (88 / 182 / 307) | Record Outcome and Actions, none off the card or over text; each of the four opens its dialog |
| The MDT page | the meeting's list a 41 px window (85) on 6,678 px | the page scrolls (616 px of 1,217); the card 889 px tall, its list whole; Set Presenter opens its dialog |
| Hospital Request | two 159 px cards (144); NHS number on 3 lines, lab case number on 10 | one 342 px card under another (312); each on one line |
| Users | the username under eight icon buttons; the card scrolls sideways (340 px showing 468) | the username clear (258 px; 246), one Actions menu of eight; nothing sideways; Edit Teams opens |
| Retention Review | the patient's name 0 px wide; Anonymise 26 px off the card (56) | the name shown (129 px); Review and Actions; Anonymise opens its dialog |

The whole audit again (every route, both sizes, before `34bfec2b`): pages with content past a card or page edge, leaving out the tabs' scrollers, went from 11 to 7 at 390 px and 14 to 9 at 360 (Users is out of both since `34bfec2b`); text cut with an ellipsis, 340 to 312 and 410 to 377. What's left is mostly an alert's headline clipped beside its "i" (Export, New Pathway, a result, Teams, a pathway's consent note), Patients' email (6 / 36 px), Hospital Requests' header (the main area still scrolls 42 px sideways, 2.5), Account Settings' Pathways label (2 px) and Audit Log's sort row.

Every change applies only in a narrow layout (`useIsNarrow`, a card under 30rem, or the phone query `useIsTouchNav` for the MDT page). At 1,440 × 900, screenshots of 21 affected pages before and after are identical but for the clock (Home's now line, Lab Case's times). At 1,280 × 800, Hospital Request and Colours stack (`useIsNarrow`), as above.

Left from section 4: bigger touch targets (5), the phone mode for `Page` (6; only the MDT page has it so far), dialogs as sheets and the bell's list (7), the lists' tabs and shorter row cards (8), and the pages in 9 (the MDT page's selectors are still two rows; Hospital Requests' header still scrolls sideways). Card names still cut with an ellipsis rather than wrapping (2.2).
