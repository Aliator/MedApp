# PR #286 quick review: Eye clinic demo team

- PR: https://github.com/Aliator/MedApp/pull/286, branch `claude/eloquent-feynman-wysbc1` into `main`
- Commit reviewed: `4ab2b62ab8b25d42eb5a017c5fd56080ddc05b9b`. Date: 22 November 2026. Quick review (effort low), diff only.

**What it does:** adds a third team, the Eye clinic, with a fourth account (`s.varma`), four patients, draft ophthalmology templates (`docs/pathways/ophthalmology.yaml`) and a story. They go into the light demo (`MedApp.Web/src/demo`) and the DevMcp seeder (`DemoEyeClinic`, `DemoOphthalmology`, with template mapping shared in `DemoTemplates`). It also brings the seeder's calendars and data request up to the light demo's, removes the leftover Read Me tour code, and adds docs, including `docs/full-browser-demo-plan.md`.

**Findings:** none.

Checked:
- **Seeder wipe:** the wipe logic and its guards are unchanged. `DemoDataSeeder.cs` only adds the `DemoEyeClinic.RunAsync` and `CreateDataRequestAsync` calls, and `DemoDataTools.cs` only changes the tool's description text.
- **NHS numbers:** the new ones (9991060006, 9991061371, 9991062742, 9991064117) are all in the 999 range with valid modulus 11 check digits.
- **Production build:** the changes stay within `src/demo` and test files (`src/test/otherClinic.ts`), so no demo code reaches it.
- **Credentials:** none committed. Passwords are generated at run time, and the demo login has none.
- **Database:** no migrations and no stored enum changes.

**Remaining risk:**
- The seeder change runs only in integration tests (`DemoDataSeedTests`) and wasn't run here.
- `DemoDermatology.cs` lost about 260 lines, moved into `DemoTemplates`, so the template mapping could drift. This review didn't check that the refactor preserves it.

Author's note on the remaining risk: the seeder was run for real against SQL Server 2022 in Docker, at the real time and under faketime on a Saturday, with no errors and the Eye clinic's records checked by SQL; `DemoDermatologyTests`, `DemoOphthalmologyTests` and `DemoReferenceDataTests` passed after the move (171 unit tests in all).
