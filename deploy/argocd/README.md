# Argo CD: MedApp deployed from git

Step 4 of the Kubernetes plan (`docs/todo.md`). With Argo CD, the kind cluster runs exactly what git says, and nothing deploys from anyone's machine.

## How a change reaches the cluster

1. **Merge to `main`.** A pull request merges, after CI (`main.yml`) has passed on it.
2. **Publish.** The **Publish** workflow (`.github/workflows/publish.yml`) builds the API's image and pushes it to GHCR as `ghcr.io/aliator/medapp-api:<commit>`.
3. **Record the deploy.** The workflow merges that commit of `main` into the **`env/kind`** branch, with the new tag in `deploy/argocd/kind-image.yaml`, as one commit ("Deployed medapp-api abc1234 to kind").
4. **Sync.** Argo CD notices, within about three minutes, and syncs:
   1. The Secrets come first.
   2. Then SQL Server.
   3. Then the migrations Job, which also makes the first admin.
   4. Then the new API, which rolls out with no downtime.

Argo CD follows `env/kind` only: the chart (`deploy/helm/medapp`), the cluster's values (`deploy/kind/values.yaml`) and the image (`deploy/argocd/kind-image.yaml`), all from one commit.
- **Why a branch:** `main` only changes through pull requests (its ruleset), so the workflow can't commit the tag there. And Argo CD can't mix two revisions of one repository in an Application, so the chart and the tag must come from the same branch.
- **What it buys:** `env/kind`'s history is the deploy history, and the chart and the image always move together.
- **Rolling back:** revert a deploy commit there with `git revert -m 1 <commit>`. That rolls back both the chart and the image together.

Argo CD also undoes changes made to the cluster by hand (`selfHeal`), and removes what's no longer in git (`prune`).

## Running it

```bash
deploy/kind/up.sh --argocd
```

This sets up the cluster as `up.sh` always does: Traefik, cert-manager, External Secrets and the local secret store. Then it installs Argo CD and applies `medapp-kind.yaml`, the Application, and waits for the first sync. It builds nothing locally.
- **MedApp:** http://localhost:8080 and https://localhost:8443. The first admin is `admin`, with the password from the local store.
- **Argo CD:** http://argocd.localhost:8080, as `admin`.

`up.sh` prints the commands for both passwords.

`--revision <branch>` has Argo CD follow a branch instead of `env/kind`, to try a chart change before merging it. The image is the one `env/kind` deploys now.

The two ways of running kind don't mix:
- **`up.sh`** builds the image here and installs the chart with Helm. It's for trying uncommitted code, and the demo data.
- **`up.sh --argocd`** follows git.

Each refuses to start while the other is in charge, and says how to hand over. The database is kept either way.

## Files

| File | What it is |
|---|---|
| `medapp-kind.yaml` | The Application: the chart, values and image from `env/kind`, synced automatically into the `medapp` namespace. The namespace gets the `restricted` Pod Security label. |
| `argocd-values.yaml` | Argo CD's own chart values: plain http behind Traefik, at `argocd.localhost`, with single sign-on and notifications turned off. |

The chart carries Argo CD sync waves, which plain Helm ignores:
- the Secrets in wave -1
- SQL Server in 0
- the migrations Job, as a sync hook, in 1
- the API in 2

So every sync migrates before the new API starts. Argo CD can't use the chart's `lookup` fallback for Secrets; kind's values take them from External Secrets.

## The image on GHCR

The image is public, like the repository. If the package was created private, the cluster can't pull it, and the pod shows `ImagePullBackOff`. Make it public once, in the package's settings on GitHub. A private registry would need `imagePullSecrets`, which Azure will handle with its own registry (step 5).
