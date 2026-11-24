# PR 289 review: always show the side nav's row menus

- PR: https://github.com/Aliator/MedApp/pull/289 (`claude/eloquent-feynman-wysbc1` into `main`)
- Commit reviewed: a0aab67805b7b5944310955af1089c6e4466f10b
- Date: 24 November 2026

**What it does:** adds the UI preference `medapp.navAlwaysShowRowMenus` (`navSettings.tsx`, off by default, through `useAppSetting`). It is a Switch on the Account page. When it is on, `RowMenu` renders on touch screens too, and `isActiveRow` in `AppSideNav.tsx` treats every row as active, so each menu button shows all the time. Two Vitest cases cover touch and mouse.

**Findings:** none. The setting holds a UI preference only, with no patient or clinical data. No endpoint, schema or stored enum changes. Hook order in `RowMenu` stays unconditional.

**Remaining risk:** low. The change is visual only, and the touch and pointer layout with every button showing hasn't been looked at in a browser. The new tests set `appSettings` and may leak that state into later tests if the store isn't reset between them.
