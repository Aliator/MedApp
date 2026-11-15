# MedApp's Helm chart

Steps 2 and 3 of the Kubernetes plan (`docs/todo.md`). It deploys:
- **The API** from its image (`MedApp.API/Dockerfile`): it serves the React app, renders its pages with Node and answers `/api`, including the notifications WebSocket.
- **SQL Server** in the cluster (`sql.enabled`, on by default), or an **external database** such as Azure SQL.
- **An ingress** in front of the API (`ingress.enabled`), with TLS from cert-manager if you like.
- **The EF migrations** as a Job, from the migrations bundle in the same image (`/app/efbundle`). The same Job then makes the **first admin**.
- **Network policies** around all of it.

The quickest way to see it running is the local kind cluster: `deploy/kind/up.sh` installs this chart with `deploy/kind/values.yaml`. See `deploy/kind/README.md`.

## Installing

Build and push the image somewhere the cluster can pull from, then:

```bash
helm upgrade --install medapp deploy/helm/medapp --namespace medapp --create-namespace \
  --set image.repository=<registry>/medapp-api --set image.tag=<tag> \
  --set ingress.enabled=true --set ingress.host=<host> \
  --set ingress.tls.enabled=true --set ingress.tls.clusterIssuer=<issuer> --wait
```

`image.tag` is required, and never `latest`, so each release names exactly what it runs. Every setting is in `values.yaml`, with what it does.

What the cluster needs:
- **An ingress controller.** Set `networkPolicy.ingressController.namespace` to its namespace, or the network policies will refuse its requests.
- **A network plugin that enforces NetworkPolicies.** Without one, the policies are silently ignored.
- **cert-manager**, for `ingress.tls.clusterIssuer`.
- **External Secrets Operator**, for `externalSecrets`.

Label the namespace `pod-security.kubernetes.io/enforce=restricted`: everything in the chart meets that standard.

## Secrets

The chart needs two Secrets:
- **The database:** the API's connection string under `connection-string`, plus SQL Server's `sa-password` when it runs in the cluster.
- **The first admin:** `username` and `password`.

Each comes from one of three places, in this order:
1. **An existing Secret** (`database.existingSecret`, `firstAdmin.existingSecret`), made some other way.
2. **A secret store** (`externalSecrets.enabled`): External Secrets Operator fills both Secrets from a SecretStore or ClusterSecretStore, such as Azure Key Vault. `externalSecrets.database` and `externalSecrets.firstAdmin` say where each key comes from in the store. This is the way for real environments, and for Argo CD.
3. **The chart itself**, as a fallback: random values (username `admin` for the first admin), kept across upgrades and on uninstall, like SQL Server's volume. It uses Helm's `lookup`, which doesn't work under `helm template` or Argo CD.

An external database (`sql.enabled=false`) needs its connection string from 1 or 2.

## Migrations and the first admin

The migrations Job is a Helm hook:
- **Before each upgrade:** the schema is migrated before the new API rolls out, and a failure fails the upgrade, so the old API keeps running.
- **After the first install:** once the chart's SQL Server exists. On a new database the API starts before the schema exists, briefly.

After migrating, the Job runs the API's `create-first-admin` command. It adds the Admin and User roles if they're missing, then the admin from the first admin Secret, but only while the database has no accounts at all. So it's safe on every upgrade: once anyone exists, it changes nothing, and it never resets a password. Turn it off with `firstAdmin.enabled=false`.

The Job is deleted when it succeeds. A failed one stays, so you can read its logs (`kubectl logs job/medapp-migrations`), until the next run replaces it.

Because migrations run before the new API is up, a migration that removes or renames something the old API still uses breaks it during the rollout. Make such changes in two releases: first add the new thing, then remove the old.

## How it's locked down

- **Network policies** (`networkPolicy.enabled`, on by default):
  - Only the ingress controller reaches the API, on its app port.
  - Only the API and the migrations Job reach SQL Server, and SQL Server reaches nothing.
  - The API and the Job reach only DNS and the database, plus anything in `networkPolicy.extraEgress`. Use that for an external database, which the policies otherwise block.
- **Health checks** are on their own port, 8081, which neither the Service nor the ingress exposes, so only the kubelet's probes reach them:
  - `/alive` for the startup and liveness probes.
  - `/health` for readiness.
- **Pods:**
  - Every pod meets the `restricted` Pod Security Standard: non-root users, the default seccomp profile, no privilege escalation, and all capabilities dropped. SQL Server keeps `NET_BIND_SERVICE`, which `sqlservr` needs to start.
  - No pod gets a Kubernetes API token, because none calls the API.
  - Root filesystems are read-only. `/tmp` is a small scratch volume, and SQL Server writes its data to its own volume.
- **HTTPS:** the API sends HSTS outside Development, so browsers stay on https once they've used it. Have the ingress controller redirect http to https.
- **Resource limits** on every container.

## Not yet

- **One copy of the API only** (`api.replicas`): more need a SignalR backplane, so live notifications reach every copy.
- **Argo CD** and **Azure** (AKS, Azure SQL, Key Vault as the External Secrets store, a container registry): steps 4 and 5 in `docs/todo.md`.
