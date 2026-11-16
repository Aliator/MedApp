# MedApp's Helm chart

Steps 2 and 3 of the Kubernetes plan (`docs/todo.md`). It deploys:
- **The API** from its image (`MedApp.API/Dockerfile`): it serves the React app and answers `/api`, including the notifications WebSocket.
- **The page server** beside it, in the same pod (`api.pageServer`): Node, from the same image, rendering the pages the API forwards to it.
- **SQL Server** in the cluster (`sql.enabled`, on by default), or an **external database** such as Azure SQL.
- **An ingress** in front of the API (`ingress.enabled`), with TLS from cert-manager if you like.
- **The EF migrations** as a Job, from the migrations bundle in the same image (`/app/efbundle`). The same Job then makes the **first admin**.
- **Valkey**, the backplane that lets several copies of the API share live notifications (`backplane`).
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

The chart needs three Secrets:
- **The database:** the API's connection string under `connection-string`, plus SQL Server's `sa-password` when it runs in the cluster.
- **The first admin:** `username` and `password`.
- **The backplane:** the API's connection string to it under `connection-string`, plus Valkey's `password` when it runs in the cluster.

Each comes from one of three places, in this order:
1. **An existing Secret** (`database.existingSecret`, `firstAdmin.existingSecret`, `backplane.existingSecret`), made some other way.
2. **A secret store** (`externalSecrets.enabled`): External Secrets Operator fills the Secrets from a SecretStore or ClusterSecretStore, such as Azure Key Vault. `externalSecrets.database`, `externalSecrets.firstAdmin` and `externalSecrets.backplane` say where each key comes from in the store. This is the way for real environments, and for Argo CD.
3. **The chart itself**, as a fallback: random values (username `admin` for the first admin), kept across upgrades and on uninstall, like SQL Server's volume. It uses Helm's `lookup`, which doesn't work under `helm template` or Argo CD.

An external database (`sql.enabled=false`) needs its connection string from 1 or 2.

## Migrations and the first admin

The migrations Job is a Helm hook:
- **Before each upgrade:** the schema is migrated before the new API rolls out, and a failure fails the upgrade, so the old API keeps running.
- **After the first install:** once the chart's SQL Server exists. On a new database the API starts before the schema exists, briefly.

After migrating, the Job runs the API's `create-first-admin` command. It adds the Admin and User roles if they're missing, then the admin from the first admin Secret, but only while the database has no accounts at all. So it's safe on every upgrade: once anyone exists, it changes nothing, and it never resets a password. Turn it off with `firstAdmin.enabled=false`.

The Job is deleted when it succeeds. A failed one stays, so you can read its logs (`kubectl logs job/medapp-migrations`), until the next run replaces it.

Because migrations run before the new API is up, a migration that removes or renames something the old API still uses breaks it during the rollout. Make such changes in two releases: first add the new thing, then remove the old.

## More than one copy of the API

Set `api.replicas` above 1. Live notifications then go through the **backplane** (`backplane.enabled`, on by default).
- **How it works:** each copy of the API passes its SignalR messages through a shared Redis-compatible server. So a notification sent by one copy reaches a browser connected to another.
- **Valkey in the cluster** (`backplane.valkey`), the open-source Redis, is the default. It saves nothing, so it needs no volume, and the API reconnects by itself after a restart.
- **Another server,** such as Azure Cache for Redis: turn Valkey off, and give the connection string through `backplane.existingSecret` or `externalSecrets`.
- **Connecting:** browsers connect straight to a WebSocket, without SignalR's negotiate request, so no sticky sessions are needed.
- **Disruption budget:** with more than one copy, a PodDisruptionBudget keeps one running while nodes are drained.
- **Spread across nodes:** with more than one copy, the scheduler is asked to put them on different nodes (a topology spread over `kubernetes.io/hostname`). It's a preference, not a rule, so a one-node cluster such as kind still runs them all.
- **The chart refuses** more than one copy without the backplane.

## The page server

Pages are rendered on the server by Node (`MedApp.Web/server/server.mjs`). With `api.pageServer.enabled` (the default, and what kind and Azure use), it runs as a second container, `pages`, in each API pod:
- **The same image,** with its own command: `node /app/ssr/server.mjs`. The image keeps the page server in `/app/ssr`, outside the API's `wwwroot`.
- **None of the API's secrets or settings.** Its environment is only where it listens (`HOST=127.0.0.1`, `PORT=8082`), its health port (`HEALTH_PORT=8083`) and the API's address (`MEDAPP_API_URL=http://127.0.0.1:8080`), besides what the image and Kubernetes always set. The pod has `enableServiceLinks: false`, so it doesn't get every Service's address either. It runs as its own user (1655, not the API's 1654), in its own container, so code running in Node can't see the API's process or read its connection strings.
- **Talking to the API:** the API forwards page requests to `http://127.0.0.1:8082` (`Ssr:ServerUrl`), with the page's CSP nonce in `X-MedApp-Csp-Nonce`. The page server calls the API back on `http://127.0.0.1:8080` with the visitor's cookie, so each call is authorised as the visitor's own. Pages listen on loopback only, so nothing outside the pod reaches them.
- **Failures don't take the API out of service.** The container's first process is a supervisor: it runs the page server in a worker process, and starts a new one (after 1 s, doubling up to 30 s while it keeps failing soon after starting) whenever the worker crashes, is killed for running out of memory, or misses its heartbeat for 15 s. The container keeps running, so the pod stays ready, and pages are rendered in the browser until the worker is back.
- **Probes:** only liveness, `GET /alive` on 8083, answered by the supervisor: Kubernetes restarts the container if the supervisor itself stops answering (5 s timeout, then 5 s to stop). There's deliberately no readiness or startup probe, because Kubernetes counts a pod as ready only while every container in it is, and either would take the API out of service whenever the page server is.
- **Logs:** the API pod's default container is `api` (`kubectl.kubernetes.io/default-container`), so the page server's own log, with its render errors, is `kubectl logs deploy/medapp-api -c pages`. The API's log says only which page fell back.
- **Stopping:** like the API, it waits 5 seconds (`preStop`), then finishes the pages it's sending and exits.
- **Its own limits:** `api.pageServer.resources` (128 MiB requested, a 512 MiB limit for both its processes; about 76 MiB in use on kind, supervisor and worker together).
- **Network policies** apply to the whole pod, so the page server could make the API's connections too (DNS, the database, the backplane), though it has none of their passwords.
- **No workload identity:** the pod is annotated `azure.workload.identity/skip-containers: pages`, so if the API is ever given a federated identity (passwordless Azure SQL), Azure's webhook won't put its token into the page server.

With `api.pageServer.enabled=false`, the API starts Node inside its own container instead, as it does outside Kubernetes. Node then gets an allowlist of the API's environment, but runs as the same user, so it could read the API's secrets from `/proc`; and if it stops, it isn't restarted until the pod is.

## How it's locked down

- **Network policies** (`networkPolicy.enabled`, on by default):
  - Only the ingress controller reaches the API, on its app port.
  - Only the API and the migrations Job reach SQL Server, and SQL Server reaches nothing.
  - Only the API reaches Valkey, and Valkey reaches nothing. Valkey also needs a password.
  - The API and the Job reach only DNS and the database, plus anything in `networkPolicy.extraEgress`. Use that for an external database, which the policies otherwise block.
- **Health checks** are on their own ports, which neither the Service nor the ingress exposes, so only the kubelet's probes reach them:
  - The API's, on 8081: `/alive` for the startup and liveness probes, and `/health` for readiness.
  - The page server's, on 8083: `/alive` for liveness only.
- **Pods:**
  - The page server runs in a container of its own, as its own user, with none of the API's secrets ([The page server](#the-page-server)).
  - Every pod meets the `restricted` Pod Security Standard: non-root users, the default seccomp profile, no privilege escalation, and all capabilities dropped. SQL Server keeps `NET_BIND_SERVICE`, which `sqlservr` needs to start.
  - No pod gets a Kubernetes API token, because none calls the API.
  - Root filesystems are read-only. The API's `/tmp` is a small scratch volume (the page server has none), and SQL Server writes its data to its own volume.
- **HTTPS:** the API sends HSTS outside Development, so browsers stay on https once they've used it. Have the ingress controller redirect http to https.
- **Resource limits** on every container.

## Not yet

Argo CD (`deploy/argocd`) and Azure (`deploy/azure`) use this chart; what Azure still lacks is in `deploy/azure/README.md`. In the chart and the app:
- **Readiness ignores the database and Valkey.** `/health` only says the process is up, so a pod stays ready while either is down. A database check needs care: it would hold up the first install, where the API starts before the migrations Job has made the schema.
- **Images by tag, not digest.** SQL Server (`2022-latest`) and Valkey are pulled by tags that move, and so are the Dockerfile's base images, so a new node can pull something newer than the rest.
- **Passwords changed in the store reach running pods only when they restart.** External Secrets updates the Secrets (`externalSecrets.refreshInterval`), but the API and Valkey read them as environment variables at start, and nothing restarts them.
