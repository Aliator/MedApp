# Review: PR #94, chart cards kept to one height

**PR:** [#94](https://github.com/Aliator/MedApp/pull/94), branch `feature/insights-card-scroll`, reviewed at `fedd512` against `main` at `246beac`.

**Reviewed:** 16 November 2026, independently of the PR description's conclusions. Every claim below was checked against the code, a test run, or the running app (measured with Playwright). **No finding is high or critical;** 2 are medium and 4 are low.

## Summary

Every chart card on the Insights page now has a fixed height of 480 px, in both its chart view and its table view, and whatever doesn't fit scrolls inside the card. It fixes a real layout problem, but it turns the tables into small scroll boxes that the keyboard can't always reach, and in the default view most of "Time at each stage" sits out of sight.

## What changed, and why

3 files, 49 lines added and 18 removed.

- **The problem:** a card was as tall as its content, and cards in the same row of the grid stretch to match each other.
  - In the default view, "Time at each stage" draws one chart per template (1,160 px of content). "Created and completed", beside it, was stretched to the same 1,219 px, with about 800 px of empty card under its chart.
  - Pressing **Show Table** on a 26-week table made that card, and its row, as tall as the table.
- **The change** (`MedApp.Web/src/components/charts/ChartCard.tsx:12-16, 42`): `ChartCard` wraps its `PanelCard` in a `VStack` 480 px high (`CHART_CARD_HEIGHT`). `PanelCard` already fills the height it's given and scrolls its body, so the card now stays 480 px and its body scrolls.
  - Like a picture frame of a fixed size: a big picture scrolls inside the frame instead of getting a bigger frame.
- **Measured effect** at 1440 x 900, default view: the scrolling area under the filters went from 1,834 px of content to 1,112 px. The empty half-card is gone.
- **A test** (`ChartCard.test.tsx:35-53`) checks that a card showing a chart and a card showing a long table both sit in a 480 px box.
- **Docs:** `MedApp.Web/AGENTS.md:47` says a chart card has one fixed height, so a long chart or table scrolls inside it.

## Design choices

- **A fixed height, rather than a maximum height:**
  - **Instead:** `max-height: 480px`, so a card is as tall as its content, up to the cap.
  - **Chosen because:** the card is the same height in both views. Pressing Show Table never moves the button or the cards below it.
  - **Worse at:** small charts get blank space (in one column, "Overdue by priority" goes from 319 px to 480 px), and every card is a scroll box even when it has little to show. Measured, a cap gives the same level rows in two columns with no blank space in one column. The cost is that the row below jumps when a card switches view.
- **A fixed height, rather than cards that fill the window:**
  - **Instead:** a card as tall as the visible scrolling area, up to a limit (`min(30rem, 100cqh)`, with that area as a CSS size container).
  - **Chosen because:** it's simple, and it needs no CSS beyond Astryx's own props.
  - **Worse at:** at high zoom, the card is taller than the space it scrolls in (finding 4). The fill-the-window option keeps a card fully on screen, but at 200% zoom its body would be only 113 px high.
- **A fixed height, rather than one sized to the chart:**
  - **Instead:** work the height out from the chart (`BarChart` already knows it: 36 px a row, 240 px for columns).
  - **Chosen because:** the table view has no such size, and text size changes the rest of the card.
  - **Worse at:** nothing important. This option would be brittle.
- **Pixels, not rem:**
  - **Instead:** `30rem`, which is 480 px at Standard text size and grows with the text size setting. A rem is a length tied to the page's base font size, so it grows when the text does.
  - **Worse at (the chosen pixels):** with larger text, the card's header and summary grow but the card doesn't, so its charts no longer fit (finding 3).
- **The height lives in `ChartCard`, not in the page:**
  - **Instead:** a prop on `ChartCard`, or a height set by `InsightsPage`.
  - **Chosen because:** every chart card behaves the same.
  - **Worse at:** a page can't drop the height where it doesn't help, such as one column on a narrow window (finding 4).
- **Is 480 the right number?** At Standard text size it fits a column chart with a two-line summary, and a bar chart of up to 8 rows. 8 people with overdue work exactly fills the "Overdue by assignee" body (421 of 421 px). A ninth row, or a 9-stage template's chart, doesn't fit. So 480 fits today's demo data, with no room to spare.

## How it was checked

- **Read:** the diff and PR description; `PanelCard`, `InsightsPage`, `BarChart`, `ChartTable`, `Page` (the narrow layout starts below 1400 px) and `textSize.tsx`; Astryx's `StackItem` scroll region code; `MedApp.Web/AGENTS.md` on layout, half views, server rendering and accessibility. `ChartCard` is used only on the Insights page.
- **Tests:** `npx vitest run src/components/charts src/pages/insights`: 6 files and 48 tests passed.
- **CI:** every check on the PR passed (web, unit, integration and E2E tests, image, helm, CodeQL, Trivy).
- **Parity screenshots:** `npm run parity:react -- -g "admin-insights"`: all 3 screens captured. The assignee chart (8 people) exactly fills its card, and "Overdue by priority" has about 200 px of empty card.
- **The project's accessibility audit:** `npm run a11y -- -g "admin-insights"` reported a **serious** `scrollable-region-focusable` failure on `admin-insights-tables` in all three colour modes (finding 1). axe is the automated accessibility checker the audit runs.
- **Measured in the running app** with a throwaway Playwright script (signed in as the parity tool's account, with its settings saves blocked so nothing was written):
  - **Layouts:** wide (1440 x 900), a narrow window (900 x 900), a half view beside Patients, the app's Larger text size (125%) and two larger root font sizes (25 px and 30 px), 200% zoom (640 x 400) and 400% zoom (320 x 256), one template against all templates, and charts against tables at 4, 12 and 26 weeks.
  - **In every layout,** every card measured 480 px in both views, and its body scrolled what didn't fit. The half view showed one column, as it should.
  - **Keyboard:** pressed Tab through the page with every table shown, recording each stop.
  - **Server rendering:** the server's HTML already holds the 480 px height. No console errors or hydration warnings appeared on load.
  - **Alternatives:** the "before", maximum-height and fill-the-window options were simulated by changing the cards' styles in the live page and measuring them (Detail).
- **Not done:**
  - Safari and WebKit weren't tested; only Chromium 156 is installed here.
  - Nothing was checked on macOS or a touch device, where scrollbars are hidden until you scroll.

## Findings

Most severe first. None is high or critical.

### 1. The tables, the accessible way to read a chart, are now scroll boxes the keyboard can't always reach

- **Severity:** medium. **Confidence:** high for the audit failure and Chromium's behaviour (both observed). Medium for Safari (from WebKit's known behaviour, not tested).
- **What could go wrong:** the table view exists for people who can't see or point at a chart (WCAG 1.1.1): keyboard and screen reader users. The PR puts the longer tables in a scroll box inside the card.
  - **Nothing inside can take focus.** "Created and completed" and "Time at each stage" have no links in their tables. Tabbing to a link would normally scroll it into view, but here there's nothing to tab to.
  - **Chrome, Edge and Firefox** make such a box a Tab stop by themselves. Observed in Chromium 156: Tab stops on it right after "Show Chart of Created and completed". A screen reader announces it only as **"Content, group"**, Astryx's default name, the same for every card.
  - **Safari** doesn't make scroll boxes focusable, as far as is known. A keyboard user there can't scroll the 26-week throughput table past its first ten or so rows, or see most of the stage times. That fails WCAG 2.1.1 (Keyboard: everything must work from the keyboard).
  - **The same in the default chart view:** "Time at each stage" scrolls with nothing focusable inside, so on Safari the other templates' charts can't be reached by keyboard. axe doesn't flag this one.
  - **The project's own audit now fails:** `npm run a11y` reports `scrollable-region-focusable` (serious, WCAG 2.1.1) on `admin-insights-tables`, in dark, light and Butter. Before the PR, these card bodies never overflowed, so the rule didn't apply.
- **Evidence:**
  - `MedApp.Web/src/components/charts/ChartCard.tsx:42` (the fixed height)
  - `MedApp.Web/src/components/views/PanelCard.tsx:111` (the scrolling body: no name, no `tabIndex`)
  - `MedApp.Web/src/pages/insights/InsightsPage.tsx:229-236` and `:262-268` (tables with no `hrefFor`, so no links: `ChartTable.tsx:54-55`)
  - `node_modules/@astryxdesign/core/dist/hooks/useScrollRegionProps.js` (an overflowing `StackItem` becomes a group named "Content"). `AppShell/observeMainOverflow.js` says Astryx makes the app's main region a keyboard stop while it overflows, but does nothing like that for a `StackItem`.
- **Fix:** either of these.
  - **Make the body reachable and named:** in `PanelCard`, give the scrolling `StackItem` `aria-label={title}`, so it's announced as "Created and completed, group". Make it a Tab stop (`tabIndex={0}`) while its content overflows, which takes a small `ResizeObserver` hook since Astryx doesn't do it for `StackItem`. This helps every `PanelCard` in the app, not just Insights.
  - **Don't put the table in a scroll box:** keep the fixed height for the chart view only, and let the table view grow. That brings back the long row the PR removed, but only for people who chose the table.
  - Either way, run `npm run a11y -- -g admin-insights` before merging.
- **What happened to it:** fixed in the PR. `PanelCard` has a new `isBodyKeyboardScrollable`: while the body overflows (`useOverflows`, a small `ResizeObserver` hook), it's a Tab stop named by the card's title. `ChartCard` turns it on; other `PanelCard`s are unchanged, as their fields and links already scroll them. Checked afterwards: `npm run a11y -- -g admin-insights` reports no violations on all three screens in dark, light and Butter. In Chromium, Tab from "Show Chart of Created and completed" lands on its body, announced as "Created and completed, group", and the arrow keys and Page Down scroll it. A body that fits adds no stop.

### 2. The default view hides three of the four stage charts, and one template's chart hides its key

- **Severity:** medium. **Confidence:** high (measured and seen in screenshots).
- **What could go wrong:** the card's edge cuts the content off with no sign that there's more, apart from a scrollbar.
  - **Default view (all templates):** "Time at each stage" has 1,160 px of content in a 421 px body. Only the first template's chart shows, and its legend (which colour is "Waiting to start" and which is "Being worked on") is cut in half at the edge. The summary above names the longest stage, "Procedure of Minor procedure (clinic)", but that template's chart is out of sight below.
  - **One template** ("Standard surgical pathway", 9 stages): the content is 468 px in 421 px, and the cut-off part is exactly the legend. A stacked chart with no key is left: an admin sees blue bars and thin teal tips, and can't tell which is waiting time.
  - **Where scrollbars are hidden** until you scroll (macOS, iOS, Android), nothing shows that the card holds more.
  - Like a window blind that stops halfway: what's behind it is still there, but nothing says so.
- **Evidence:**
  - `MedApp.Web/src/components/charts/ChartCard.tsx:12-16, 42` (480 px)
  - `MedApp.Web/src/components/charts/BarChart.tsx:42, 139` (36 px a row) and `:205` (the legend comes after the bars, so it's what the edge cuts first)
  - `MedApp.Web/src/pages/insights/InsightsPage.tsx:239-274` (a chart per template, each with its legend)
- **Fix:**
  - **Put the legend above the bars** in `BarChart` (move `ChartLegend` before the chart, `BarChart.tsx:205`). The key is then never what's cut off, for every chart.
  - **For several templates,** say in the summary how many are charted ("4 templates; scroll for the others"), or give this card a taller height through a prop on `ChartCard`.
- **What happened to it:** fixed in part. `BarChart` now draws the key above the bars, so a card's edge never cuts it off, for every chart. With all templates, the other templates' charts are still below the edge in the default view: that's the card scrolling as intended, and the body is now a named Tab stop while it does. Saying how many templates are charted in the summary was left out.

### 3. The height is in pixels, so it doesn't grow with the text size setting

- **Severity:** low. **Confidence:** high (measured).
- **What could go wrong:** Account Settings' text size scales the page's root font size, and Astryx sizes text and spacing in rem. So the header, padding and summary grow, but the 480 px card doesn't.
  - **At Larger (125%), the default view:** the header bar grows from 57 to 71 px and the summary wraps. The "Overdue by assignee" chart then overflows by 43 px, cutting off its value axis, and "Created and completed" overflows by 23 px.
  - **With a larger browser font as well** (a 25 px root): the title and button wrap, the header grows to 138 px, the body shrinks to 340 px, and the assignee chart overflows by 143 px.
  - Everything can still be reached by scrolling, so WCAG 1.4.4 (Resize text: text can be enlarged to 200% without losing content) isn't failed. But the comment's promise that "the usual charts fit" holds only at Standard size.
- **Evidence:** `MedApp.Web/src/components/charts/ChartCard.tsx:16, 42`; `MedApp.Web/src/app/textSize.tsx:4-10, 38`.
- **Fix:** `<VStack height="30rem">` (`CHART_CARD_HEIGHT = '30rem'`). That's 480 px at Standard and 600 px at Larger. Measured at Larger: all three single charts fit (527 of 527 px).
- **What happened to it:** fixed in the PR: `CHART_CARD_HEIGHT` is `'30rem'`, 480 px at Standard text size.

### 4. In one column, and at high zoom, the fixed height adds a scroll box inside a scrolling area

- **Severity:** low. **Confidence:** high (measured). The usability judgement is the reviewer's.
- **What could go wrong:** the area under the filters already scrolls (`InsightsPage.tsx:140`), at every window size. Now each card scrolls inside it too: nested scrolling, where the mouse wheel scrolls whichever box is under the pointer.
  - **The reason doesn't apply in one column.** The fixed height keeps cards level side by side. On a narrow window or in a half view there's one column, so nothing sits beside a card. The fixed height then adds only blank space ("Overdue by priority": 161 px empty with its chart, 212 px with its table) and a scroll box inside the scrolling area.
  - **At 200% zoom** (a 1280 x 800 laptop, 640 x 400 in CSS pixels), the scrolling area is 172 px high and each card 480 px. A card is never fully on screen: its header, with Show Chart, scrolls out of view while its table is still scrolling inside. Before the PR, the cards had no scroll of their own, so there was one scroll. Scrolling stays vertical, so WCAG 1.4.10 (Reflow: no scrolling in two directions) isn't failed, but it's harder to use, for exactly the people who zoom.
  - `MedApp.Web/AGENTS.md:18` prefers sections "scrolling together as one (not each on its own)" (`FoldingCard`).
- **Evidence:** `MedApp.Web/src/components/charts/ChartCard.tsx:42`; `MedApp.Web/src/pages/insights/InsightsPage.tsx:140, 175`; `MedApp.Web/src/components/pages/Page.tsx:48`.
- **Fix:** use the fixed height only in two columns. `InsightsPage` knows (`isNarrow`), so it can pass a height, or none, to `ChartCard`. Optionally, cap a card at the visible area as well (`min(30rem, 100cqh)` with the scrolling area as a size container), so a card is never taller than the space it scrolls in.
- **What happened to it:** kept, as a trade-off. The cards scroll inside in every layout, which is the behaviour the change was made for. Capping a card at the visible area would leave its body about 113 px high at 200% zoom, which is worse. Insights at high zoom is a to-do (`docs/todo.md`, Accessibility), together with the 400% zoom problem under Remaining risks.

### 5. The new test checks a style, not scrolling or the page's length

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** the test's name says "a long table scrolls inside the card rather than lengthening the page". What it checks is that each heading has an ancestor whose inline style says `height: 480px`.
  - It would still pass if the body didn't scroll (`isBodyScrollable={false}`), with the 40 rows spilling out past the card's edge.
  - It renders two cards, one in each view, rather than switching one card between them.
  - jsdom, the simulated browser Vitest runs in, has no layout, so no unit test can show scrolling. The real checks were by hand: the parity screenshots, which assert nothing, and the measurements in the PR description.
- **Evidence:** `MedApp.Web/src/components/charts/ChartCard.test.tsx:35-53`.
- **Fix:**
  - Name the test for what it checks ("keeps one height in both views"), and re-render one card with the other view.
  - Put the behaviour in an end-to-end test (`MedApp.E2ETests`, Playwright): at 26 weeks, press Show Table on "Created and completed", then check that the card's height is unchanged, that its body's `scrollHeight` is greater than its `clientHeight`, and that the last row can be scrolled into view.
- **What happened to it:** fixed in the PR. The test now switches one card from chart to table and checks it's the same fixed-height box. New `PanelCard` tests cover the Tab stop: none while the body fits, a named stop while it overflows, and none for a card that doesn't ask for one. No end-to-end test was added: the accessibility audit and the measurements in the running app cover the behaviour.

### 6. The comments and docs promise more than the layout gives

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** the next change is made on the docs' word.
  - **`ChartCard.tsx:12-16`** says 480 is "tall enough for a column chart and its summary, so the usual charts fit". That's true only at Standard text size, and for bar charts of up to 8 rows (findings 2 and 3).
  - **`MedApp.Web/AGENTS.md:12`** says "A card's open parts scroll inside it; the page never does." The Insights area scrolls as a whole at every size (720 px shown of 1,112 px at 1440 x 900), and its cards now scroll inside it. Neither `AGENTS.md:47` nor the component's comment says Insights is an exception.
  - **The PR description** says the whole page "grew and scrolled". The header and filters always stayed put; the area under them scrolled.
- **Evidence:** `MedApp.Web/src/components/charts/ChartCard.tsx:12-16, 35-36`; `MedApp.Web/AGENTS.md:12, 47`.
- **Fix:** say in `ChartCard`'s comment what fits (Standard text, a column chart, up to 8 bars). In `AGENTS.md:47`, say that Insights is a scrolling column of fixed-height cards: an exception to "the page never scrolls", kept because four charts don't fit one window.
- **What happened to it:** fixed in the PR. `ChartCard`'s comment says what fits (Standard text, a column chart, up to 8 bars). `MedApp.Web/AGENTS.md` names Insights as the exception to "the page never scrolls", and describes `isBodyKeyboardScrollable`.

## Remaining risks

- **Insights can't be used at 400% zoom (from before this PR).** At 320 x 256 CSS pixels (WCAG 1.4.10's test: 1280 x 1024 at 400%), the header and filters take the whole window. The scrolling area is 0 px high and the document doesn't scroll, so no tile or chart can be reached. The cards don't affect that area's height, so this PR neither causes nor fixes it. Other `fill` pages probably share it; not checked.
- **A scrolled table loses its column headings.** After about ten rows, the 26-week throughput table shows two columns of numbers with no "Created" or "Completed" above them. The same happened before in the larger scrolling area, after more rows. Astryx's `Table` has no sticky header option.
- **Safari, macOS and touch** weren't tested (findings 1 and 2).
- **480 fits today's data with nothing to spare.** A ninth person with overdue work, or a template with more stages, scrolls in the default view.

## Say it like this

"Each chart card on Insights is now one fixed height, so showing a long table scrolls inside the card instead of stretching the row, and a short chart is no longer stretched to match a tall one beside it. The choice was a fixed height over a maximum one, so the layout never jumps when you switch views. The trade-off is that what doesn't fit is hidden behind the card's edge: a chart's key or other templates' charts, and, most importantly, tables that keyboard users can't always scroll."

## Detail

### Card bodies, measured (shown px / content px)

The body is the card's scrolling part: 421 px of the 480 at Standard text size. "Before" was simulated by removing the fixed height in the live page.

| Layout, 26 weeks unless stated | Assignee | Priority | Created and completed | Time at each stage | Scrolling area |
|---|---|---|---|---|---|
| Wide, charts | 421 / 421 | 421 / 421 | 421 / 421 | 421 / **1,160** | 720 / 1,112 |
| Wide, tables | 421 / 421 | 421 / 421 | 421 / **889** | 421 / **875** | 720 / 1,112 |
| Wide, charts, one template (9 stages) | 421 / 421 | 421 / 421 | 421 / 421 | 421 / **468** | 720 / 1,112 |
| Wide, charts, Larger text (125%) | 407 / **450** | 407 / 407 | 407 / **430** | 407 / **1,246** | 683 / 1,150 |
| Wide, charts, 25 px root font | 340 / **483** | 340 / 340 | 340 / **478** | 340 / **1,385** | 636 / 1,254 |
| Narrow 900 x 900, charts | 421 / 421 | 421 / 421 | 421 / 421 | 421 / **1,160** | 720 / 2,256 |
| 200% zoom (640 x 400), charts | 421 / 421 | 421 / 421 | 421 / 421 | 421 / **1,160** | **172** / 2,256 |
| 400% zoom (320 x 256), charts | 389 / **444** | 389 / 389 | 389 / **412** | 389 / **1,200** | **0** / 2,512 |

### The alternatives, simulated (card heights in px, charts / tables)

| Option | Wide | Narrow 900 x 900 | 200% zoom | Larger text |
|---|---|---|---|---|
| Before the PR | 463, 463, **1,219**, **1,219** / 388, 388, 948, 948 | 463, 319, 431, 1,219 / 388, 268, 948, 934 | as narrow | 523, 523, 1,319, 1,319 |
| This PR (480 px) | all 480 | all 480 | all 480, in a 172 px area | all 480, three overflow |
| `max-height: 30rem` | 463, 463, 480, 480 / 388, 388, 480, 480 | 463, 319, 431, 480 / 388, 268, 480, 480 | as narrow | 523, 523, 600, 600; none overflow but the stage chart |
| `min(30rem, 100cqh)` | all 480 | all 480 | all 172 (bodies 113 px) | all 600 |

- **Before the PR:** in the default view, "Created and completed" was stretched to 1,219 px beside the stage chart. The PR fixes that.
- **A maximum height** keeps the pairs level in two columns, through the grid's stretching, and leaves no blank space in one column. But a card's height changes with its view whenever it's under the cap.
- **Filling the visible area** keeps every card on screen at 200% zoom, but leaves bodies only 113 px high there.

### The Tab order with every table shown (Chromium 156)

Period, Template, the three linked tiles, then:
- **Overdue by assignee:** "Show Chart", then the 8 assignee links.
- **Overdue by priority:** "Show Chart", then the 4 priority links.
- **Created and completed:** "Show Chart", then **"Content, group"** (the scroll box itself).
- **Time at each stage:** "Show Chart", then **"Content, group"**.

Then the skip link and the side nav. The two "Content" stops come from Chromium making scroll boxes focusable on its own. Without that, there's no way to scroll those two tables from the keyboard.
