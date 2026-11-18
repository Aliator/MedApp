# Review: PR #123, light CI tiers, shards and a full run on main

**PR:** [#123](https://github.com/Aliator/MedApp/pull/123), branch `feature/faster-ci`, reviewed at `9faaea7` on 18 November 2026. A quick review of the diff only; nothing was run. No finding is high or critical.

`changes` runs `.github/scripts/ci-plan.sh` to choose a tier (none, light, full) from the PR's labels (`ci:none`, `ci:full`) and its whole diff against the base; jobs outside the tier are skipped by `if`, which satisfies the required checks. Vitest and the integration tests are sharded, with `web` and `integration-tests` kept as gate jobs over the shards. The full suite runs on every push to `main`, and `main-report` opens or updates one issue when it fails. Publish skips pushes that can't change the image or chart.

**Findings:**
- **Medium-low, fixed in the PR:** `main.yml:27`, `cancel-in-progress: true` applied to `main` too, so a second push or the nightly run cancelled a full run still going, and `main-report` skips cancelled runs: a failure could go unreported. It now cancels in progress only for pull requests.
- **Low, left:** `changes` runs `ci-plan.sh` from the PR's own checkout, so a PR editing it can choose its own tier. Editing workflows could already do that.
- **Low, left:** `publish.yml`'s `paths` excludes `**.md` and `tools/**`; a file of either kind read at image build time would not trigger a deploy. None is known.

**Remaining risk:** by design the light tier lets contract and cross-page breakage reach `main`, where the full run and its issue report it; Publish isn't gated on that run. `main-report` first runs on this PR's merge.
