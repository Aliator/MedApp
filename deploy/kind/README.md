# MedApp on a local kind cluster

Step 1 of the Kubernetes plan (`docs/todo.md`): MedApp in a local cluster made by [kind](https://kind.sigs.k8s.io/) (Kubernetes in Docker), as close to production as one machine allows.
- **The chart:** it's deployed with the app's Helm chart (`deploy/helm/medapp`), locked down as it would be anywhere.
- **The image:** the API runs from its production image (`MedApp.API/Dockerfile`: the API, the built React app, Node to render its pages and the migrations bundle), with SQL Server beside it.
- **What a real cluster would provide:**
  - **Traefik** as the ingress controller.
  - **cert-manager** for TLS.
  - **External Secrets Operator**, reading from a local stand-in for a secret store.

This is a way to try MedApp in Kubernetes, not a replacement for the AppHost: day-to-day development still runs through `dotnet run --project MedApp.AppHost`. The two can run at the same time; their databases are separate.

## What you need

- **Docker Desktop**, running.
- **kind, kubectl and Helm.** On Windows: `winget install Kubernetes.kind Helm.Helm`. Docker Desktop includes kubectl. Open a new terminal afterwards so they're on the PATH, although the scripts find them anyway.
- **Bash and openssl:** Git Bash on Windows has both.
- **Memory and disk:** about 4 GB of memory, most of it for SQL Server, plus disk space for the images (SQL Server's alone is 1.5 GB).

## Running it

From the repo root:

```bash
deploy/kind/up.sh                         # an empty database, with a first admin
deploy/kind/up.sh --demo-data kind-demo   # or the demo data (save a kind-demo snapshot with medapp-dev first, below)
```

Then open **http://localhost:8080**, or **https://localhost:8443**, where the browser warns about the self-signed certificate.
- **On an empty database,** sign in as `admin` with the password `up.sh` prints the command for:

  ```bash
  kubectl --context kind-medapp -n medapp-vault get secret medapp-first-admin -o jsonpath='{.data.password}' | base64 -d
  ```

- **With the demo data,** sign in as `admin` / `admin`.

`up.sh` does the rest:
1. Creates the cluster if it's missing.
2. Installs Traefik, cert-manager and External Secrets Operator, and sets up the local issuer and secret store (`local-cluster.yaml`).
3. Fills the store with random passwords, once.
4. Builds the API's image and loads it into the cluster, tagged by the image's ID.
5. Installs or upgrades the chart with `values.yaml`. The chart's migrations Job brings the database up to date and makes the first admin.

Run it again after a code change: a changed image gets a new tag, and Helm rolls it out. The database is kept unless you pass `--demo-data`, which replaces it.

To have the cluster follow git instead, use `up.sh --argocd`: Argo CD deploys the image the Publish workflow built from `main`. See `deploy/argocd/README.md`.

`deploy/kind/down.sh` deletes the cluster, its database and its secret store. The AppHost's dev database isn't affected.

If your cluster was made before https://localhost:8443 was added, the script says so. Run `down.sh` first, once.

## The demo data

`--demo-data <name>` restores a `medapp-dev` snapshot into the cluster's database:
1. It scales the API to none while it restores.
2. It upgrades the release, so the migrations Job brings the restored data up to date.

The first admin step then sees the demo accounts and changes nothing.

Save a snapshot from the dev database by asking for medapp-dev's `save_snapshot` with the name `kind-demo`. `reset_demo_data` fills the dev database with the standard synthetic data and the `admin` login. The snapshot lands in `tools/MedApp.DevMcp/.backups/kind-demo.bak`, which is git-ignored. A path to any other `.bak` file works too.

## What's in it

| File | What it is |
|---|---|
| `cluster.yaml` | The one-node cluster, with Traefik's ports mapped to this machine only: 8080 (http) and 8443 (https). |
| `traefik-values.yaml` | Traefik's chart values: its entry points on the node ports mapped to 8080 and 8443. |
| `local-cluster.yaml` | The local stand-ins: a self-signed cert-manager issuer, and a secret store. The store is Secrets in the `medapp-vault` namespace, read by External Secrets, where Azure will use Key Vault. |
| `values.yaml` | MedApp's chart values for this cluster: the local image (never pulled), the ingress with TLS, and secrets from the local store. |
| `up.sh`, `down.sh` | Start or redeploy, and delete. |

- **The `medapp` namespace enforces the `restricted` Pod Security Standard,** so a pod that runs as root, or asks for more than the chart's do, is refused.
- **The network policies are enforced** (kind's network plugin supports them). Nothing outside the pods that need it can reach SQL Server or the API. To query the database by hand, use `kubectl exec` into `medapp-sql-0` and its `sqlcmd`.
- **Every command** in the scripts names the `kind-medapp` context, so they can't touch another cluster. To look around yourself:
  - `kubectl --context kind-medapp -n medapp get pods`
  - `kubectl --context kind-medapp -n medapp logs deploy/medapp-api`
  - `helm --kube-context kind-medapp -n medapp history medapp`
- **Sign-in works over plain http** here because browsers treat `localhost` as secure, so they keep the session cookie even though it's `Secure`. Anywhere else needs https.

What the chart does, and leaves for later steps, is in `deploy/helm/medapp/README.md`.
