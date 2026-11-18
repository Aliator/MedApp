# Review: PR #122, dependency updates

**PR:** [#122](https://github.com/Aliator/MedApp/pull/122), branch `feature/dependency-updates`, reviewed at `1e96f51` on 18 November 2026. A quick review of the diff only; nothing was built or run. No finding is high or critical.

Combines 21 Dependabot PRs: NUnit 3.14 to 5.0.0, NUnit.Analyzers 4.15.0, Microsoft.NET.Test.Sdk 18.11.0 and coverlet.collector 10.1.0 in the three test projects (`AutoFixture.NUnit3` removed, unused and NUnit 3 only), two npm packages, and five GitHub Actions pinned by commit SHA. `@types/node` 26 and Lexical 0.53 are left out, with `ignore` rules in `.github/dependabot.yml`.

**Findings, all low, none fixed before merging:**
- **`marocchino/sticky-pull-request-comment` v2 to v3** (`.github/workflows/main.yml`, `coverage-comment`): the step's `recreate` and `path` inputs weren't checked against v3's `action.yml`. The step runs only on PRs.
- **`actions/upload-artifact` v7 beside `actions/download-artifact` v4.3.0:** assumed compatible (v7 still zips by default); `coverage-comment`'s download is the proof.
- **`NUnit3TestAdapter` 4.6.0 and `Microsoft.Playwright.NUnit` 1.64.1 beside NUnit 5:** the unit tests ran (4,532 passed); the integration and E2E projects were only built, listed or run for one fixture.

**Remaining risk:** the first full CI run on `main` is the test of the E2E job, the coverage comment and the integration tests under the new versions.
