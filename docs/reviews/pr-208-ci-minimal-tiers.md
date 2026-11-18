# PR #208: CI builds and type checks only; phase and full runs by hand

Light tier (PRs, pushes to `main`, workflow changes) = builds, lint, type checks; `workflow_dispatch` `tier=phase|full` runs the tests. Quick review: no critical or high findings; two lows fixed (stale `AGENTS.md` lines). `main-report` still opens the failure issue for any failed run on `main`.
