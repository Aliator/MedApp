# MedApp's Helm chart

Steps 2 and 3 of the Kubernetes plan (`docs/todo.md`). It deploys:
- **The API** from its image (`MedApp.API/Dockerfile`): it serves the React app and answers `/api`, including the notifications WebSocket.
- **The page server** beside it, in the same pod (`api.pageServer`): Node, from the same image, rendering the pages the API forwards to it.
- **SQL Server** in the cluster (`sql.enabled`, on by default), or an **external database** such as Azure SQL.
- **An ingress** in front of the API (`ingress.enabled`), with TLS from cert-manager if you like.
- **The EF migrations** as a Job, from the migrations bundle in the same image (`/app/efbundle`). The same Job then makes the **first admin**.
- **Valkey**, the backplane that lets several copies of the API share live notifications (`backplane`).
- **Mailpit**, if you like (`mailpit.enabled`): a mail catcher for clusters that mustn't send real email. Otherwise the API sends through a mail server you name (`email.smtp`), or sends nothing.
- **File storage for attachments** (`attachments`): Azurite, Azure Storage's emulator, in the cluster (on by default, for kind and trying it out), or an Azure Storage account.
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

The chart needs four Secrets, and a fifth for a mail server that wants a password:
- **The database:** the API's connection string under `connection-string`, plus SQL Server's `sa-password` when it runs in the cluster.
- **The first admin:** `username` and `password`.
- **The backplane:** the API's connection string to it under `connection-string`, plus Valkey's `password` when it runs in the cluster.
- **The attachments' file storage:** the API's Azure Storage connection string under `connection-string`, plus Azurite's `account-key` when it runs in the cluster.
- **The mail server** (only with `email.smtp.auth.enabled`): `password`, plus `username` unless `email.smtp.username` is set.

Each comes from one of three places, in this order:
1. **An existing Secret** (`database.existingSecret`, `firstAdmin.existingSecret`, `backplane.existingSecret`, `attachments.existingSecret`, `email.smtp.auth.existingSecret`), made some other way.
2. **A secret store** (`externalSecrets.enabled`): External Secrets Operator fills the Secrets from a SecretStore or ClusterSecretStore, such as Azure Key Vault. `externalSecrets.database`, `externalSecrets.firstAdmin`, `externalSecrets.backplane`, `externalSecrets.attachments` and `externalSecrets.email` say where each key comes from in the store. This is the way for real environments, and for Argo CD.
3. **The chart itself**, as a fallback: random values (username `admin` for the first admin), kept across upgrades and on uninstall, like SQL Server's volume. It uses Helm's `lookup`, which doesn't work under `helm template` or Argo CD. Not for the mail server: the chart can't make up its password, so it refuses to render without 1 or 2.

An external database (`sql.enabled=false`) needs its connection string from 1 or 2. So does an Azure Storage account for attachments; without one (below), the API runs without file storage.

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

Changes that leave the old API partly working, until every pod runs the new one (the end of the rollout, or after a rollback, which doesn't roll the database back, the next deploy):
- **Personal calendar templates and meetings** (`MadeCalendarTemplatesPersonal` and `AddedMeetings`, #99):
  - **Old pods can't create day or week templates.** Their insert gives no owner, and the column has no default, so it fails (a 500) rather than making a template nobody owns.
  - **Old pods list everyone's copies** as the organisation's templates, several with the same name, and an admin there could set someone's default week to another person's copy.
  - **Old pods don't know meetings.** They don't show them, and can book appointments and block time over them.
- **Several notes per procedure** (`AddedProcedureNotes`, #110): the migration copies each procedure's single notes field into its first note, and keeps the old column (`PatientProcedures.Notes`) for old pods.
  - **Old pods read and write only the old column.** A procedure created on one with notes, or an Edit Procedure save there that changes them, writes only that column: the new pods don't show it as a note (only in the deprecated `notes` string of the procedure's response, for a procedure with no notes). The migration has already run, so a later deploy doesn't copy it again.
  - **After a rollback** (a revert on `main`, which leaves the database as it is), staff see each procedure's notes as they were before the move, none added since, and their edits go into the old column. Before rolling forward again, or dropping the column, copy any old-column text that differs from the first note into a note (`docs/todo.md`).
  - **Pages loaded before the deploy** keep working: the response still has `notes` as a string (the first note's text), beside the new `noteList`, and an old Edit Procedure page that still sends `notes` gets a 400 saying notes are now added on the procedure's page, rather than having the change silently dropped.
- **Deleting notes** (`AddedNoteDeletion`): new columns on `PatientProcedureNotes` and `Notifications`, and a new table, so nothing old pods use changes.
  - **Old pods don't know a note is deleted.** Until every pod runs the new API, a procedure page served by an old pod still shows a note an admin has deleted, and its author can still edit it there. After a rollback this lasts until the next deploy: deleted notes show again (still marked deleted in the database, and hidden again once the new API is back). Admins should hold off deleting notes during a rollout.
  - **Old pods don't anonymise what they don't know.** Anonymising a patient on an old pod (during a rollout, or after a rollback until the next deploy) leaves the deletion details on their notes and the details of requests to delete them as they were, and leaves waiting requests open; and an anonymised record can't be anonymised again. Hold off anonymising as well. A clean-up for any record this happened to is a to-do for the next release (`docs/todo.md`).
  - **On an old pod's web app, a "Note deletion requested" notification opens its procedure's page without the request's dialog.**
- **Two-factor codes by email** (`AddedEmailTwoFactorCodes` and `MadeEmailCodesOnePerPurpose`): a new column on the users table (`TwoFactorMethod`) and a new table, so nothing old pods use changes. But old pods don't know the method:
  - **Old pods ask for an app code.** An account that turns codes by email on during the rollout is asked by an old pod for an authenticator code at sign-in, which it doesn't have; a recovery code still works.
  - **Old pods don't protect the address.** On an old pod, an account using codes by email can change its address with the password alone (no emailed code), or remove it, leaving it with nowhere to send codes.
  - **So nobody should turn on codes by email until the rollout has finished.** Afterwards, look for accounts using codes by email with no confirmed address (`TwoFactorEnabled = 1`, `TwoFactorMethod = 2`, and `EmailConfirmed = 0` or no `Email`), and reset their two-factor from the Users page so they can set it up again. After a rollback, the same applies until the next deploy (`AGENTS.md`, two-factor, has the check for rolling forward again).

## More than one copy of the API

Set `api.replicas` above 1. Live notifications then go through the **backplane** (`backplane.enabled`, on by default).
- **How it works:** each copy of the API passes its SignalR messages through a shared Redis-compatible server. So a notification sent by one copy reaches a browser connected to another.
- **Valkey in the cluster** (`backplane.valkey`), the open-source Redis, is the default. It saves nothing, so it needs no volume, and the API reconnects by itself after a restart.
- **Another server,** such as Azure Cache for Redis: turn Valkey off, and give the connection string through `backplane.existingSecret` or `externalSecrets`.
- **Connecting:** browsers connect straight to a WebSocket, without SignalR's negotiate request, so no sticky sessions are needed.
- **Disruption budget:** with more than one copy, a PodDisruptionBudget keeps one running while nodes are drained.
- **Spread across nodes:** with more than one copy, the scheduler is asked to put them on different nodes (a topology spread over `kubernetes.io/hostname`). It's a preference, not a rule, so a one-node cluster such as kind still runs them all.
- **The chart refuses** more than one copy without the backplane.

## Attachments

Staff attach files (scans, photos, results, letters, up to 20 MiB each) to patients, procedures and stages. Their details are in the database; the files themselves are in Azure Blob Storage, in the container `attachments.container` (`attachments` by default), which the API makes if it's missing. The API reads the storage account's connection string from the attachments Secret (`ConnectionStrings__Attachments`).
- **Azurite** (`attachments.azurite`, on by default) is Azure Storage's emulator, run in the cluster for kind and for trying the chart out:
  - **Blob storage only,** on port 10000, as one pod (a StatefulSet) with its files on a volume (`attachments.azurite.storage`).
  - **Its own account,** `medapp` (`attachments.azurite.accountName`), with a random 64-byte key. Azurite's well-known `devstoreaccount1`, whose key is published, is then off. The chart makes the key and keeps it across upgrades, as it does the other passwords, unless it comes from an existing Secret or the secret store (`account-key`). Azurite doesn't store the key with the files, so a new key still opens them.
  - **Plain http inside the cluster,** as for SQL Server and Valkey: only the API may connect (the network policy), and it needs the account key.
  - **Not production storage.** It's one pod and one volume, with no redundancy, no backups and no encryption of its own, and the chart backs nothing up. Deleting its volume (`data-medapp-azurite-0`) deletes every attached file. Patients' files in production belong in an Azure Storage account.
- **An Azure Storage account** (`deploy/azure`): turn Azurite off (`attachments.azurite.enabled=false`) and give the connection string through `attachments.existingSecret` or `externalSecrets` (with `externalSecrets.attachments.account-key: null`). Allow the API to reach it through `networkPolicy.extraEgress`, on 443.
- **No file storage at all:** with Azurite off and no connection string (no `attachments.existingSecret`, and `externalSecrets` off or `externalSecrets.attachments.connection-string: null`), the API still starts and everything else works; only uploads and downloads answer 503 ("File storage isn't set up"). The install notes say so. The API's reference to the attachments Secret is `optional`, so the same goes for a Secret that's missing (say, a secret store entry not made yet): no file storage, rather than an API that can't start. Azurite's reference isn't optional: it can't run without its key.
- **Scratch space:** the API streams each upload (up to 20 MiB) into a temporary file in `/tmp` while it checks it, deleted straight after, so the API's `/tmp` volume allows 1 GiB.

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
- **Network policies** apply to the whole pod, so the page server could make the API's connections too (DNS, the database, the backplane, Azurite, and the mail server, Key Vault and Microsoft Entra ID when they're set), though it has none of their passwords, keys or tokens. Where the API reaches those last three, the pod may reach any public address on their ports, unless egress is limited by name ([How it's locked down](#how-its-locked-down)).
- **No workload identity:** the pod is annotated `azure.workload.identity/skip-containers: pages`, so when the API has a workload identity (`api.workloadIdentity.clientId`, for the data protection key), Azure's webhook doesn't put its token into the page server.

With `api.pageServer.enabled=false`, the API starts Node inside its own container instead, as it does outside Kubernetes. Node then gets an allowlist of the API's environment, but runs as the same user, so it could read the API's secrets from `/proc`; and if it stops, it isn't restarted until the pod is.

## How it's locked down

- **Network policies** (`networkPolicy.enabled`, on by default):
  - Only the ingress controller reaches the API, on its app port.
  - Only the API and the migrations Job reach SQL Server, and SQL Server reaches nothing.
  - Only the API reaches Valkey, and Valkey reaches nothing. Valkey also needs a password.
  - Only the API reaches Azurite, on 10000, and Azurite reaches nothing. Azurite also needs its account key.
  - Only the API reaches Mailpit, on SMTP only, and Mailpit reaches nothing. Its inbox is for `kubectl port-forward` only.
  - The API reaches only DNS, the database, Valkey, Azurite and Mailpit (when they run in the cluster); the Job only DNS and the database. Both also reach anything in `networkPolicy.extraEgress`. Use that for an external database or an Azure Storage account, which the policies otherwise block.
  - The API also reaches the services it knows by name, when they're set: an external mail server (`email.smtp.host`) on its port only, and Key Vault with Microsoft Entra ID (`dataProtection.keyVaultKeyUri`) on 443 only. Policies can't name hosts, so these rules allow any address in `networkPolicy.internet`: by default every address except the private (10/8, 172.16/12, 192.168/16), shared (100.64/10) and link-local (169.254/16) ranges. That keeps out the cluster's own pods and services, and the cloud's instance metadata endpoint. Narrow it if the addresses you need are fixed.
  - **Egress by name** (`networkPolicy.fqdnEgress.enabled`, off by default, on in Azure's values): on a cluster whose Cilium enforces FQDN rules (on AKS, Advanced Container Networking Services' security, which `deploy/azure/main.bicep` turns on), a `CiliumNetworkPolicy` (`<release>-api-by-name`) allows those services by name instead, and the API's NetworkPolicy leaves out its internet rules. Policies add up, so with both, any public address would still be allowed. It allows, each on its port only:
    - **The mail server,** `email.smtp.host` on `email.smtp.port`, when it's outside the cluster. It must be a name: the chart fails on an IP address.
    - **The vault,** the host in `dataProtection.keyVaultKeyUri` (`<vault>.vault.azure.net`), on 443: the API wraps and unwraps its data protection keys there.
    - **Microsoft Entra ID,** `networkPolicy.fqdnEgress.authorityHost` (`login.microsoftonline.com`, AKS's `AZURE_AUTHORITY_HOST`), on 443, with the vault: `DefaultAzureCredential` swaps the pod's workload identity token for a Key Vault token there. Key Vault's sign-in challenge may name another Entra ID host (`login.windows.net`), but the SDK takes only the tenant from it and signs in at the authority host; not yet seen on a real cluster.
    - **DNS through Cilium's DNS proxy,** to kube-dns, for any name: that's how Cilium learns the names' addresses. Only those addresses are allowed, while the answers last. Any name may still be looked up, as before, because each lookup first tries the pod's search domains.
    - **More rules,** if something turns out to be missing (Cilium's drops name it): `networkPolicy.fqdnEgress.extraEgress`, as Cilium egress rules.

    Without Cilium the `CiliumNetworkPolicy` kind doesn't exist, so the install fails; with Cilium but no DNS proxy, the names never resolve to allowed addresses, so email and the data protection key fail. The database, the backplane, Mailpit and `networkPolicy.extraEgress` stay in the NetworkPolicy, unchanged.
- **Health checks** are on their own ports, which neither the Service nor the ingress exposes, so only the kubelet's probes reach them:
  - The API's, on 8081: `/alive` for the startup and liveness probes, and `/health` for readiness.
  - The page server's, on 8083: `/alive` for liveness only.
- **Pods:**
  - The page server runs in a container of its own, as its own user, with none of the API's secrets ([The page server](#the-page-server)).
  - Every pod meets the `restricted` Pod Security Standard: non-root users, the default seccomp profile, no privilege escalation, and all capabilities dropped. SQL Server keeps `NET_BIND_SERVICE`, which `sqlservr` needs to start.
  - No pod gets a Kubernetes API token, because none calls the API. With workload identity, the API's pods get a token for Microsoft Entra ID only.
  - Root filesystems are read-only. The API's `/tmp` is a scratch volume (1 GiB, for uploads; the page server has none), and SQL Server and Azurite write their data to their own volumes.
- **HTTPS:** the API sends HSTS outside Development, so browsers stay on https once they've used it. Have the ingress controller redirect http to https.
- **Resource limits** on every container.

## Not yet

Argo CD (`deploy/argocd`) and Azure (`deploy/azure`) use this chart; what Azure still lacks is in `deploy/azure/README.md`. In the chart and the app:
- **Readiness ignores the database and Valkey.** `/health` only says the process is up, so a pod stays ready while either is down. A database check needs care: it would hold up the first install, where the API starts before the migrations Job has made the schema.
- **Images by tag, not digest.** SQL Server (`2022-latest`) and Valkey are pulled by tags that move, and so are the Dockerfile's base images, so a new node can pull something newer than the rest. Only Mailpit is pinned by digest; Azurite is pinned to a version (`3.37.0`), but by tag.
- **Backups of Azurite's files.** None: it's for kind and trying the chart out. Production uses an Azure Storage account, with its own soft delete (`deploy/azure`).
- **Egress by name, without Cilium.** With `networkPolicy.fqdnEgress` off (the default, and kind), the rules for the mail server and Key Vault allow any public address on their ports, because NetworkPolicies can't name hosts. An egress firewall would limit them on other clusters.
- **The page server shares the API's pod, and so its egress.** Even with egress by name, code running in the page server (a compromised npm package, say) can reach the mail relay, the vault and Microsoft Entra ID, as well as look up any name. It can't use MedApp's access to them: the relay needs the API's SMTP password, and the vault and Entra ID need the workload identity token, which `skip-containers` keeps out of the pages container. But they're shared services, so it could use an attacker's own: sign in to the relay with their Communication Services account and mail what it sees (visitors' session cookies) to them, or, since Cilium allows addresses rather than names and the vault's addresses serve other vaults too, write it to the attacker's own vault through Entra ID. It can also still leak it slowly as DNS lookups. So egress by name stops posting to any server on the internet, and leaves exfiltration only through Azure accounts, which can be traced; it doesn't close it. The robust fix is the page server in a Deployment of its own, whose policy lets it reach only the API.
- **Passwords changed in the store reach running pods only when they restart.** External Secrets updates the Secrets (`externalSecrets.refreshInterval`), but the API, Valkey and Azurite read them as environment variables at start, and nothing restarts them.
