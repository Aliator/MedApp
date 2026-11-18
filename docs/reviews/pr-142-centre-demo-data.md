# Review: PR #142, one seeding script and the community specialist centre demo clinic

**PR:** [#142](https://github.com/Aliator/MedApp/pull/142), branch `feature/centre-demo-data`, reviewed at `5496ea9` on 18 November 2026. A quick review of the scripts and the tool's entry points; the story content (about 5,000 lines) was only searched for patterns, and nothing was run. No finding is high or critical.

Adds `tools/seed-demo-data.sh` and `.ps1` and a `seed-demo` command-line mode to `tools/MedApp.DevMcp` (the same `DemoDataReset` code as `reset_demo_data`), and rewrites the demo story as a referral-led outpatient centre: 15 staff, seven pathway templates (one archived), a 14-stage library, new calendars, notes, files and searches.

**Checked:** `DevGuard.Check` runs before anything else on the configuration the seeder uses (`SeedDemoCli.cs:165`), and both scripts run `seed-demo --check` before `dotnet ef database update`, so neither the seed nor the migrations can reach a server that isn't local; no flag skips it. Arguments are quoted, the `.sh` is executable, no workflow calls the seed, and no NHS number outside the 999 range, real organisation or non-example email domain was found.

**Findings:**
- **Medium, left as a to-do:** the scripts apply migrations before the seeder takes its pre-reset snapshot (`tools/seed-demo-data.sh:59-63`, `DemoDataReset.cs:42-47`), so a destructive migration leaves no backup of the earlier state. Until fixed, take a `save_snapshot` before running the script on a database worth keeping.
- **Low, left:** a bad `--save-snapshot` name fails after the wipe and the summary (generated passwords) is never printed (`SeedDemoCli.cs:181-185`).
- **Low, fixed in the PR:** only `/demo-summary.json` was git-ignored; any `*demo-summary*.json` now is.
- **Low, left:** the sa password is concatenated into the connection string unescaped (`seed-demo-data.sh:47`).

**Remaining risk:** `localhost` counts as local, so a tunnel to a remote SQL Server would pass the guard (existing behaviour).
