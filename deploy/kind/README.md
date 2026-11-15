# MedApp on a local kind cluster

Step 1 of the Kubernetes plan (`docs/todo.md`): MedApp in a local cluster made by [kind](https://kind.sigs.k8s.io/) (Kubernetes in Docker), as close to production as one machine allows. It's deployed with the app's Helm chart (`deploy/helm/medapp`, step 2), behind Traefik as the ingress controller. The API runs from its production image (`MedApp.API/Dockerfile`: the API, the built React app, Node to render its pages and the migrations bundle), with SQL Server beside it.

This is a way to try MedApp in Kubernetes, not a replacement for the AppHost: day-to-day development still runs through `dotnet run --project MedApp.AppHost`. The two can run at the same time; their databases are separate.

## What you need

- **Docker Desktop**, running.
- **kind, kubectl and Helm.** On Windows: `winget install Kubernetes.kind Helm.Helm`. Docker Desktop includes kubectl. Open a new terminal afterwards so they're on the PATH, although the scripts find them anyway.
- **Bash:** Git Bash on Windows.
- **Memory and disk:** about 3 GB of memory for SQL Server, plus disk space for the images (SQL Server's alone is 1.5 GB).

## Running it

From the repo root:

```bash
deploy/kind/up.sh --demo-data kind-demo   # first save a kind-demo snapshot with medapp-dev (below)
deploy/kind/up.sh                         # or an empty database
```

Then open **http://localhost:8080** and sign in as `admin` / `admin` (with the demo data).

`up.sh` does the rest:
1. Creates the cluster if it's missing.
2. Installs Traefik.
3. Builds the API's image and loads it into the cluster, tagged by the image's ID.
4. Installs or upgrades the chart with `values.yaml`. The chart's migrations Job brings the database up to date.

Run it again after a code change: a changed image gets a new tag, and Helm rolls it out. The database is kept unless you pass `--demo-data`, which replaces it.

`deploy/kind/down.sh` deletes the cluster and its database. The AppHost's dev database isn't affected.

If your cluster was made by the first version of `up.sh` (plain manifests, before the chart), the script says so. Run `down.sh` first, once.

## The demo data

The app seeds no accounts, so an empty database has no way to sign in. `--demo-data <name>` restores a `medapp-dev` snapshot into the cluster's database. It scales the API to none while it restores, then upgrades the release, so the migrations Job brings the restored data up to date.

Save one from the dev database by asking for medapp-dev's `save_snapshot` with the name `kind-demo`. `reset_demo_data` fills the dev database with the standard synthetic data and the `admin` login. The snapshot lands in `tools/MedApp.DevMcp/.backups/kind-demo.bak`, which is git-ignored. A path to any other `.bak` file works too.

## What's in it

| File | What it is |
|---|---|
| `cluster.yaml` | The one-node cluster, with its ports mapped to this machine only: 8080 to Traefik, 14331 to SQL Server (for `dotnet ef` and debugging; the AppHost's is 14330). |
| `traefik-values.yaml` | Traefik's chart values: its http entry point on the node port mapped to 8080. |
| `values.yaml` | MedApp's chart values for this cluster: the local image, never pulled, the ingress, and SQL Server's node port. |
| `up.sh`, `down.sh` | Start or redeploy, and delete. |

- **The SQL password** is made by the chart, into the `medapp-db` Secret with the API's connection string, so none is in git.
- **Every command** in the scripts names the `kind-medapp` context, so they can't touch another cluster. To look around yourself: `kubectl --context kind-medapp -n medapp get pods`, `logs deploy/medapp-api`, and `helm --kube-context kind-medapp -n medapp history medapp`.
- **Sign-in works over plain http** here because browsers treat `localhost` as secure, so they keep the session cookie even though it's `Secure`. Anywhere else needs TLS on the ingress.

What the chart leaves for later steps is in `deploy/helm/medapp/README.md`.
