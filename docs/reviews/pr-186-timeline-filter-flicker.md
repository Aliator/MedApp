# PR #186: Timeline filters no longer jump or flicker

https://github.com/Aliator/MedApp/pull/186 (`fix/timeline-filter-flicker`), reviewed 18 November 2026 (quick review).

`TimelineFeed` (`PatientTimeline.tsx`) swapped its entries for a full spinner on every new choice of filters, so the page shortened, the scroll clamped and the chips jumped under the pointer (another chip lit up) and flickered. The feed query now uses `placeholderData: keepPreviousData`: the last choice's entries stay (`aria-busy`, a spinner at the end) until the new ones arrive; "Load More" hides meanwhile.

Findings: two lows. (1) The previous choice's entries stay clickable while the new one loads; left as is, as each still opens its real record. (2) The old choice's "Older entries could not be loaded" alert could show while updating: fixed, gated on `!isUpdating`. A stale `focusEntryId` doesn't refocus (the effect runs only when it changes).

Remaining risk: none beyond the first low.
