# PR #194: The light demo catches up with every feature

https://github.com/Aliator/MedApp/pull/194 (`feature/light-demo-parity`), head `84266efa`, reviewed 18 November 2026 (quick review).

Brings `MedApp.Web/src/demo` level with `main` through #192: simplified handlers and seed (`SEED_VERSION` 2) for specimens and the Lab, results sign-off, letters (sending refused, recording as sent allowed), the MDT list, consent, branching and optional stages, every stage gate's 409, every setup page, monitoring with #188's service alerts (rules kept, nothing fires), restriction, the retention review and a read-only export. Five new tour steps by fixed ID. `src/demo/parity.test.ts` requires every path and method in `schema.d.ts` to have a handler or a reasoned entry in `notInLightDemo.ts` (failing on stale, doubled or duplicate entries); `AGENTS.md`'s light-demo row now requires the demo to change in the same PR.

Findings: none. Checked: every changed file is under `src/demo`, plus `AGENTS.md` and `docs/agents/light-demo.md`, so production is untouched (no new import of the demo); organisations marked "(synthetic)", 01632 960xxx phones, ZZ postcodes, every ten-digit number in the 999 range; `AGENTS.md` at 39,074 characters with existing rules' wording kept. The "adjusted #189 test" is the demo's own data-request test, not the production `DataRequestPage` test.

Remaining risk: the demo's timeline doesn't show specimens, results, letters, MDT or consent yet, and other areas don't refuse changes to a restricted record (both noted as stubs).
