# PR #199: Shift- and Ctrl-click open pages in the half view

https://github.com/Aliator/MedApp/pull/199 (`feature/shift-click-half-view`), head `b8a90744`, reviewed 18 November 2026 (quick review).

`HalfViewClicks` (rendered once in `AppShell`) adds one capture-phase `click` and `keydown` listener: Shift-click or Shift+Enter on an in-app link opens it on the right (`halfViews.open`); Ctrl or Cmd opens it on the left and moves the old left page right (`halfViews.openOnLeft`); `preventDefault` and `stopPropagation` only for those. Plain, Alt and middle clicks, external, `mailto:`, `#`, downloads, `target`ed and disabled links, and phones are left to the browser; the Notepad toggle is marked `data-half-view-href`. The "Nav items close the half view" setting is removed (a plain click opens on the left). The light demo's first tour step becomes open via the menu, close, Shift-click; the notepad step stays on the Read Me.

Findings: two lows, both fixed. (1) `halfViewHref` refused `//host` but not `/\host`, which browsers read the same way: it now refuses that too and checks the resolved origin is the page's own. (2) A Shift- or Ctrl-click on a control nested in a link (a button, a field) was taken: such controls now keep their own click. Tests added for both.

Checked: Enter is acted on only inside `a[href]`, so Shift+Enter in text areas and the Ctrl+Enter save shortcut are untouched; listeners are removed on cleanup; the re-sent plain click for "already on the left" carries no modifiers; the old stored setting is simply no longer read.

Remaining risk: none beyond the owner's accepted trade-off that Ctrl/Cmd-click no longer opens a new browser tab for in-app links (middle-click and the context menu still do).
