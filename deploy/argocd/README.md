# Argo CD: MedApp deployed from git

Step 4 of the Kubernetes plan (`docs/todo.md`). With Argo CD, the kind cluster runs exactly what git says, and nothing deploys from anyone's machine.

## How a change reaches the cluster

1. **Merge to `main`.** A pull request merges, after CI (`main.yml`) has passed on it.
2. **Publish.** The **Publish** workflow (`.github/workflows/publish.yml`) builds the API's image and pushes it to GHCR as `ghcr.io/aliator/medapp-api:<commit>`.
3. **Record the deploy.** The workflow adds one commit to the **`env/kind`** branch ("Deployed medapp-api abc1234 to kind"), whose files are exactly that commit of `main` plus the new tag in `deploy/argocd/kind-image.yaml` (`.github/scripts/deploy-to-env.sh`). Its parents are the branch's previous deploy (first) and the commit of `main` (second). Nothing is merged, so a deploy never conflicts, and nothing on `env/kind` outlives the next deploy. Only `main` is deployed: Publish started by hand on another branch builds and pushes its image, but moves no environment branch.
   - **Except `.github/workflows`,** which keeps the branch's own. GitHub refuses a push with the workflow's token (`GITHUB_TOKEN`) that changes a workflow file, and nothing on an environment branch runs them.
   - **Never backwards:** a deploy of a commit older than the one the branch runs (re-running an old Publish run) changes nothing and says so.
4. **Sync.** Argo CD notices, within about three minutes, and syncs:
   1. The Secrets come first.
   2. Then SQL Server.
   3. Then the migrations Job, which also makes the first admin.
   4. Then the new API, which rolls out with no downtime.

Argo CD follows `env/kind` only: the chart (`deploy/helm/medapp`), the cluster's values (`deploy/kind/values.yaml`) and the image (`deploy/argocd/kind-image.yaml`), all from one commit.
- **Why a branch:** `main` only changes through pull requests (its ruleset), so the workflow can't commit the tag there. And Argo CD can't mix two revisions of one repository in an Application, so the chart and the tag must come from the same branch.
- **What it buys:** `env/kind`'s history is the deploy history (`git log --first-parent env/kind`), and the chart and the image always move together.
- **Rolling back:** revert the change on `main`, through a pull request, like any other change. The Publish run for that merge deploys it, chart and image together. Re-running an older Publish run doesn't roll back: the script won't move a branch to an older commit.
- **In an emergency:** commit to the environment branch by hand (for example, put back an older `kind-image.yaml`, or the chart as it was). Argo CD deploys it, and it holds until the next deploy, which replaces the branch's files with `main`'s. So revert on `main` too, before anything else merges.
- **The database isn't rolled back.** The older image's migrations bundle runs and leaves the newer schema in place, which is safe as long as migrations only add before they remove (the chart's README).

Argo CD also undoes changes made to the cluster by hand (`selfHeal`), and removes what's no longer in git (`prune`).

## Running it

```bash
deploy/kind/up.sh --argocd
```

This sets up the cluster as `up.sh` always does: Traefik, cert-manager, External Secrets and the local secret store. Then it installs Argo CD, makes the `medapp` namespace with its Pod Security labels, applies `project.yaml` and `medapp-kind.yaml` (the project, then the Application), and waits for the first sync. It builds nothing locally.
- **MedApp:** http://localhost:8080 and https://localhost:8443. The first admin is `admin`, with the password from the local store.
- **Argo CD:** http://argocd.localhost:8080, as `admin`.

`up.sh` prints the commands for both passwords.

**Before merging a change that adds a secret store entry** (as attachments added `medapp-attachments`), run `up.sh --argocd` again on the existing cluster. Argo CD syncs the merge on its own, but it can't fill the local store: only `up.sh` does. Without the entry, the new ExternalSecret can't sync, and Argo CD's sync stops at the Secrets (wave -1), so the merge never reaches the cluster even though Publish succeeds. The script adds only what's missing, and changes no existing entry or password (`deploy/kind/README.md`).

`--revision <branch>` has Argo CD follow a branch instead of `env/kind`, to try a chart change before merging it. The image is the one `env/kind` deploys now. Run `up.sh --argocd` without it to go back to `env/kind`: that puts the Application back as `medapp-kind.yaml` has it, image included.

The two ways of running kind don't mix:
- **`up.sh`** builds the image here and installs the chart with Helm. It's for trying uncommitted code, and the demo data.
- **`up.sh --argocd`** follows git.

Each refuses to start while the other is in charge, and says how to hand over. The database is kept either way.

## Files

| File | What it is |
|---|---|
| `project.yaml` | The `medapp` project both Applications belong to. It limits what Argo CD deploys from the environment branches: only this repository, only into the `medapp` namespace on its own cluster, and only the namespaced kinds the chart renders. Nothing cluster-wide, not even a Namespace. Anything else on a branch fails the sync. A new kind in the chart has to be added here first. |
| `medapp-kind.yaml` | The Application: the chart, values and image from `env/kind`, synced automatically into the `medapp` namespace. Argo CD doesn't create that namespace: `up.sh --argocd` does, with the `restricted` Pod Security labels. |
| `medapp-azure.yaml` | The same for AKS, from `env/azure` (`deploy/azure/README.md`). |
| `argocd-values.yaml` | Argo CD's own chart values: plain http behind Traefik, at `argocd.localhost`, with single sign-on and notifications turned off. |

The chart carries Argo CD sync waves, which plain Helm ignores:
- the Secrets in wave -1
- SQL Server in 0
- the migrations Job, as a sync hook, in 1
- the API in 2

So every sync migrates before the new API starts. Argo CD can't use the chart's `lookup` fallback for Secrets; kind's values take them from External Secrets.

## Who can deploy

Anyone who can push to an `env/*` branch can deploy to that environment: Argo CD syncs whatever lands there. The project limits that to the chart's kinds in the `medapp` namespace, and since it allows no Namespace, a commit there can't relax the namespace's Pod Security labels. But it can still run any image in `medapp`, with that namespace's secrets.

So the branches need protecting too:
- **Done:** the `env/*` ruleset blocks deleting a branch and force-pushing.
- **Still to do** (`docs/todo.md`): let only a deploy-only GitHub App push to `env/*`, and have Publish push as that App. A personal repository can't make GitHub Actions the only pusher, so for now anything with write access to the repository (people, and any workflow job with `contents: write`) can push there.

## The image on GHCR

The image is public, like the repository. If the package was created private, the cluster can't pull it, and the pod shows `ImagePullBackOff`. Make it public once, in the package's settings on GitHub. A private registry would need `imagePullSecrets`, which Azure will handle with its own registry (step 5).
