# Deploying

kind, Argo CD, Azure and the Publish workflow: read before changing how MedApp is built into an image or deployed (`deploy/`, the Dockerfile, the Publish workflow).

**Kubernetes (kind):** `deploy/kind/up.sh --demo-data kind-demo` runs the production image on a local kind cluster at http://localhost:8080 (and https://localhost:8443), beside the AppHost, with its own database, using the Helm chart in `deploy/helm/medapp`. Its migrations Job runs the image's EF migrations bundle, then makes the first admin. It also runs Azurite (the Azure Storage emulator, port 10000) for attached files, unless `attachments.azurite.enabled` is off.
- **The chart is locked down** (network policies, the `restricted` Pod Security Standard, read-only filesystems, secrets from External Secrets), so a new container, port or connection needs a matching change there.
- Each API pod runs `api` and `pages` (the Node page server, without the API's secrets) from one image (see Server-side rendering in [Local dev gotchas](local-dev.md)).
- `up.sh --argocd` has Argo CD deploy from git. Each merge to `main` runs the Publish workflow, which pushes the image to GHCR and records the commit and image tag on the `env/kind` branch Argo CD follows (`deploy/argocd/README.md`). Roll back by reverting on `main`. Never commit to an `env/*` branch by hand except in an emergency; the next deploy replaces it.

**Azure** (AKS, Azure SQL, Key Vault, Azure Container Registry, a storage account for files) is in `deploy/azure`: written and checked (CI compiles and lints `main.bicep`), but **not deployed**. It costs money, so never run `deploy.sh` or `bootstrap.sh` without the user asking. Once set up, Publish's `azure` job deploys each merge through `env/azure`; until then it's skipped. Chart versions for both clusters are in `deploy/versions.sh`; see each folder's `README.md`. After changing the chart run `helm lint --strict deploy/helm/medapp --set image.tag=ci` (CI's `helm` job lints it).

`.github/workflows/publish.yml` runs on every push to `main` except docs-only changes:
1. `image` pushes the image to GHCR (`ghcr.io/aliator/medapp-api:<commit>`), makes an SPDX SBOM (`anchore/sbom-action`, also the `medapp-api-sbom` artifact) and attests the build provenance and the SBOM to GHCR (`actions/attest`). It's the only job with `id-token: write` and `attestations: write`. Verify with `gh attestation verify oci://ghcr.io/aliator/medapp-api:<commit> --repo Aliator/MedApp`; nothing checks attestations before a deploy yet.
2. `deploy-kind` commits to `env/kind` exactly `main`'s files plus the tag in `deploy/argocd/kind-image.yaml`, except `.github/workflows`, which keeps the branch's own (`.github/scripts/deploy-to-env.sh`); Argo CD deploys that branch (`deploy/argocd/README.md`). `azure` does the same for `env/azure` once Azure is set up. Each needs only `image`, so one failing doesn't stop the other. Run by hand on another branch, it pushes the image but deploys nothing. Re-running an older run doesn't move a branch back.

After a merge, its run is the deploy: check it as you check CI.

### Checking CI on PRs you opened or pushed to

After opening a PR, or pushing to one you opened, check its CI result before calling the work done. Don't stop at "pushed".

**Don't block the user while CI runs.** Hand steps 1–3 to a background subagent (Claude Code's `Agent` tool, which notifies you when done), tell the user it's watching CI, and carry on with what they ask next. Give it the branch, the pushed commit SHA and this section. The subagent's rules:
- **Read-only:** no edits, commits or pushes. It reports; it doesn't fix.
- **Waiting:** poll about every 30 seconds with the harness's wait mechanism (a background command or monitor; foreground `sleep` may be blocked). A run usually takes about 9 minutes, under one for a docs-only push after a passing run. After 15 minutes, report it as still pending, with its URL.
- **One PR each:** a watcher waits for one PR's run, never several, so a ready PR isn't held up by another's CI.
- **Report:** the run URL and result; for a failure, the failing step, the error, the cause, whether it's already broken on `main`, and a proposed fix as an exact diff.

When it reports, do step 4 yourself without interrupting the user's current task: mention the result at the next natural break, or at once if it failed.

1. **Find the run for your commit.** The repo is public, so the GitHub REST API needs no auth; use `gh` if it's installed and logged in.
   - `GET https://api.github.com/repos/Aliator/MedApp/actions/runs?branch=<branch>`. Trust only a run whose `head_sha` is the commit you pushed. A new run can take a minute to appear, and until then the API returns the previous one: say the check is pending and look again later. Never report the old run's result as the new one.
   - Then read its jobs (`/actions/runs/<run_id>/jobs`) for each step's `conclusion`.
2. **If it passed**, say so in one line, with any coverage problems from the sticky PR comment (handlers, validators or React files under 80%).
3. **If it failed**, find the cause before reporting:
   - Note the failing step. A failure in "Set up job" means the workflow is broken (bad action ref, runner image), not the code.
   - Read the annotations (`GET /repos/Aliator/MedApp/check-runs/<job_id>/annotations`); full logs need auth (`gh run view <run_id> --log-failed`).
   - Reproduce only the failing tests locally, as CI runs them: `--configuration Release` with a `--filter`, or the `web` job's npm script on the failing files.
   - Check whether it's yours or already broken on `main` (say, when your branch doesn't change the workflow).
4. **Ask the user before fixing:** the failing step and error, the cause, whether your change caused it, and the fix as an exact diff. Wait for approval before committing or pushing it, unless they've already told you to fix it. After pushing a fix, check the new run (step 1).
