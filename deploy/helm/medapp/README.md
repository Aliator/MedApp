# MedApp's Helm chart

Steps 2 and 3 of the Kubernetes plan (`docs/todo.md`). It deploys:
- **The API** from its image (`MedApp.API/Dockerfile`): it serves the React app, renders its pages with Node and answers `/api`, including the notifications WebSocket.
- **SQL Server** in the cluster (`sql.enabled`, on by default), or an **external database** such as Azure SQL.
- **An ingress** in front of the API (`ingress.enabled`), with TLS from cert-manager if you like.
- **The EF migrations** as a Job, from the migrations bundle in the same image (`/app/efbundle`). The same Job then makes the **first admin**.
- **Valkey**, the backplane that lets several copies of the API share live notifications (`backplane`).
- **Mailpit**, if you like (`mailpit.enabled`): a mail catcher for clusters that mustn't send real email. Otherwise the API sends through a mail server you name (`email.smtp`), or sends nothing.
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

The chart needs three Secrets, and a fourth for a mail server that wants a password:
- **The database:** the API's connection string under `connection-string`, plus SQL Server's `sa-password` when it runs in the cluster.
- **The first admin:** `username` and `password`.
- **The backplane:** the API's connection string to it under `connection-string`, plus Valkey's `password` when it runs in the cluster.
- **The mail server** (only with `email.smtp.auth.enabled`): `password`, plus `username` unless `email.smtp.username` is set.

Each comes from one of three places, in this order:
1. **An existing Secret** (`database.existingSecret`, `firstAdmin.existingSecret`, `backplane.existingSecret`, `email.smtp.auth.existingSecret`), made some other way.
2. **A secret store** (`externalSecrets.enabled`): External Secrets Operator fills the Secrets from a SecretStore or ClusterSecretStore, such as Azure Key Vault. `externalSecrets.database`, `externalSecrets.firstAdmin`, `externalSecrets.backplane` and `externalSecrets.email` say where each key comes from in the store. This is the way for real environments, and for Argo CD.
3. **The chart itself**, as a fallback: random values (username `admin` for the first admin), kept across upgrades and on uninstall, like SQL Server's volume. It uses Helm's `lookup`, which doesn't work under `helm template` or Argo CD. Not for the mail server: the chart can't make up its password, so it refuses to render without 1 or 2.

An external database (`sql.enabled=false`) needs its connection string from 1 or 2.

## Email

The API sends password resets, email address confirmations and security notices over SMTP. Without a mail server and a from address it sends nothing, and the app hides its email features. The chart sets the API's `Email__*` settings from:
- **`email.linkBaseUrl`:** the address people open MedApp at, which links in emails start with. It's never taken from a request's `Host` header, so a forged header can't point a reset link elsewhere. Left empty, it's `https://<ingress.host>` when the ingress is on with a host, and otherwise there's none, so emails that need a link aren't sent. Set it when people reach MedApp on another port or through another proxy (kind uses `https://localhost:8443`).
- **`email.fromAddress`** and **`email.fromName`:** the sender. The name defaults to MedApp.
- **`email.smtp`:** a mail server outside the cluster: `host`, `port` (587), `security` (`StartTls` here, so TLS is required; also `SslOnConnect`, `Auto` or `None`) and `timeoutSeconds`. To sign in, set `auth.enabled`, with the password from a Secret (above).

### Mailpit

`mailpit.enabled` runs [Mailpit](https://mailpit.axllent.org/) (`axllent/mailpit`, pinned by digest) and points the API at it, with no TLS and no sign-in, and a from address of `medapp@localhost` unless you set one. Leave `email.smtp.host` empty: the chart refuses both.
- **Nothing leaves the cluster.** Mailpit keeps every email (the newest `mailpit.maxMessages`) and relays none, and its network policy lets it connect nowhere.
- **A restart empties it.** It keeps them in a temporary database on a scratch volume, not a persistent one.
- **Read the inbox** with a port-forward, then open http://localhost:8025:

  ```bash
  kubectl --namespace medapp port-forward service/medapp-mailpit 8025:8025
  ```

  Nothing else reaches the inbox: no ingress route, and its network policy admits only the API, on SMTP only. A port-forward comes in through the pod's own loopback, where network policies don't apply. The inbox has no password, so anyone who can port-forward in the namespace can read the mail.
- **It meets the `restricted` standard** like the rest: it runs as user 1000 (the image has no user of its own), with a read-only root filesystem and `/tmp` as a scratch volume for its database.

## Data protection keys and workload identity

The API's ASP.NET Core data protection keys seal password reset and email confirmation tokens. The API keeps them in the database (`DataProtectionKeys`), so every copy shares them, and a link made by one copy opens on any other.

`dataProtection.keyVaultKeyUri` names an Azure Key Vault key that wraps them, so a copy of the database alone can't unseal them. The API signs in to Key Vault with `DefaultAzureCredential`. On AKS that's Microsoft Entra Workload ID, through `api.workloadIdentity.clientId`, the client ID of a managed identity:
- **The service account:** the chart gives the API its own, `<release>-api`, annotated with `azure.workload.identity/client-id`.
- **The pods** get the `azure.workload.identity/use: "true"` label. AKS's webhook then adds a token for the identity, as a projected volume, and the `AZURE_*` variables that point to it. That token is only for Microsoft Entra ID: the pods still get no Kubernetes API token.
- **The identity** needs a federated credential for `system:serviceaccount:<namespace>:<release>-api`, and permission to wrap and unwrap with the key (`deploy/azure/main.bicep` does both).

Without a client ID, the API keeps the namespace's default service account, as before. Set the key URI before the API first runs: keys made while it's unset are stored unwrapped, and stay that way until they expire.

The migrations Job doesn't need any of this: it never seals anything.

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

## How it's locked down

- **Network policies** (`networkPolicy.enabled`, on by default):
  - Only the ingress controller reaches the API, on its app port.
  - Only the API and the migrations Job reach SQL Server, and SQL Server reaches nothing.
  - Only the API reaches Valkey, and Valkey reaches nothing. Valkey also needs a password.
  - Only the API reaches Mailpit, on SMTP only, and Mailpit reaches nothing. Its inbox is for `kubectl port-forward` only.
  - The API and the Job reach only DNS and the database, plus anything in `networkPolicy.extraEgress`. Use that for an external database, which the policies otherwise block.
  - The API also reaches the services it knows by name, when they're set: an external mail server (`email.smtp.host`) on its port only, and Key Vault with Microsoft Entra ID (`dataProtection.keyVaultKeyUri`) on 443 only. Policies can't name hosts, so these rules allow any address in `networkPolicy.internet`: by default every address except the private (10/8, 172.16/12, 192.168/16), shared (100.64/10) and link-local (169.254/16) ranges. That keeps out the cluster's own pods and services, and the cloud's instance metadata endpoint. Narrow it if the addresses you need are fixed.
- **Health checks** are on their own port, 8081, which neither the Service nor the ingress exposes, so only the kubelet's probes reach them:
  - `/alive` for the startup and liveness probes.
  - `/health` for readiness.
- **Pods:**
  - Every pod meets the `restricted` Pod Security Standard: non-root users, the default seccomp profile, no privilege escalation, and all capabilities dropped. SQL Server keeps `NET_BIND_SERVICE`, which `sqlservr` needs to start.
  - No pod gets a Kubernetes API token, because none calls the API. With workload identity, the API's pods get a token for Microsoft Entra ID only.
  - Root filesystems are read-only. `/tmp` is a small scratch volume, and SQL Server writes its data to its own volume.
- **HTTPS:** the API sends HSTS outside Development, so browsers stay on https once they've used it. Have the ingress controller redirect http to https.
- **Resource limits** on every container.

## Not yet

Argo CD (`deploy/argocd`) and Azure (`deploy/azure`) use this chart; what Azure still lacks is in `deploy/azure/README.md`. In the chart and the app:
- **Readiness ignores the database and Valkey.** `/health` only says the process is up, so a pod stays ready while either is down. A database check needs care: it would hold up the first install, where the API starts before the migrations Job has made the schema.
- **Node is never restarted.** If the page server exits, the API logs it once and serves pages rendered in the browser until the pod restarts, and nothing reports it.
- **Images by tag, not digest.** SQL Server (`2022-latest`) and Valkey are pulled by tags that move, and so are the Dockerfile's base images, so a new node can pull something newer than the rest. Only Mailpit is pinned by digest.
- **Egress by name.** The rules for the mail server and Key Vault allow any public address on their ports, because NetworkPolicies can't name hosts. Cilium's FQDN policies (on AKS, Advanced Container Networking Services) or an egress firewall could limit them to `smtp.azurecomm.net`, the vault and Microsoft Entra ID.
- **Passwords changed in the store reach running pods only when they restart.** External Secrets updates the Secrets (`externalSecrets.refreshInterval`), but the API and Valkey read them as environment variables at start, and nothing restarts them.
