# Review: PR #121, AGENTS.md split into a core and area guides

**PR:** [#121](https://github.com/Aliator/MedApp/pull/121), branch `feature/condensed-agents-md`, reviewed at `0b2bc23` on 18 November 2026. A quick review of the `.github/` diff and the new core `AGENTS.md`; the area guides and the Git workflow section were not read line by line. No findings.

Splits `AGENTS.md` (over Claude Code's 150,000-character limit) into a 34,380-character core and 17 guides under `docs/agents/`, read per area; rewrites the Git workflow rules for working while MedApp isn't live (one quick review, merging with the admin bypass, checking `main`'s run afterwards); reworks `docs/todo.md` around the community specialist centre plan; adds a 40,000-character check on the core to `main.yml`'s `changes` job and `.github/scripts/check-agents-md.py`.

**Checked:** the size step (`.github/workflows/main.yml:66-69`) finds `AGENTS.md` under the cone-mode sparse checkout, counts characters, interpolates nothing untrusted and widens no permissions. Every guide the core's "Read before you change" table names exists. The core still states that stored enum numbers are never renumbered, migrations aren't applied at startup, test data is synthetic, `env/*` branches aren't committed to by hand and Azure's scripts aren't run unasked.

**Remaining risk:** a rule that moved to a guide is only seen by an agent that opens the guide; the core's table carries each area's costliest rules to cover that.
