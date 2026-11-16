# MedApp on Azure

Step 5 of the Kubernetes plan (`docs/todo.md`): MedApp on AKS, with Azure SQL, Key Vault and Azure Container Registry, deployed by Argo CD from git, as the kind cluster is.

**Nothing here has been deployed yet.** It's written and checked (CI compiles and lints the Bicep, and lints the chart with `values.yaml`), but never run against a subscription. Expect to fix small things on the first real run. Running it costs money from the moment `deploy.sh` finishes: see [Cost](#cost).

## What it makes

`main.bicep`, in one resource group:

| Resource | Notes |
|---|---|
| Virtual network | Subnets for AKS's nodes, and for private endpoints. |
| AKS | Azure CNI Overlay with Cilium, so the chart's NetworkPolicies are enforced. Workload identity. Entra ID sign-in only (local accounts off, Azure RBAC), with the admin group as cluster admins. Patch upgrades, and node images, automatically. Autoscaling, 2 to 4 nodes. Its control plane runs as a managed identity the template makes, which gets Network Contributor on the AKS subnet before the cluster is created. |
| Azure Container Registry (Basic) | The API's image. AKS pulls with its own identity; no admin user. |
| Azure SQL (serverless, General Purpose, 1 vCore) | Public access off: reachable only through its private endpoint in the virtual network. The admin group is its Entra admin. Pauses after an hour idle (`sqlAutoPauseDelay`); the app retries while it wakes. A `CanNotDelete` lock (`medapp-keep-database`) stops the server, and with it the database and its backups, being deleted by accident (see [Cost](#cost)). |
| Key Vault | RBAC, soft delete and purge protection. Named `kv-medapp-<environment>-<5 characters>`, at most 24 characters. Holds the chart's four secrets: `medapp-db`, `medapp-first-admin`, `medapp-backplane` and `medapp-email`, as JSON. And one key, `medapp-data-protection` (RSA 2048, wrap and unwrap only), which wraps the API's data protection keys. |
| Azure Communication Services Email | An Email Communication Service with an Azure-managed domain (`<random>.azurecomm.net`, no DNS to set up), linked to a Communication Services resource, whose SMTP relay (`smtp.azurecomm.net`) the API sends through. Email data is kept in the UK (`emailDataLocation`). Engagement tracking is off, so links in emails aren't rewritten. The SMTP user name, and a custom role for the Entra app that signs in (see [Email](#email)), are made only once that app is in `main.bicepparam`. |
| Managed identity for External Secrets | May only read Key Vault's secrets. It's federated with External Secrets' service account in AKS, so it uses no stored secret. |
| Managed identity for the API | May only wrap and unwrap with the data protection key (Key Vault Crypto Service Encryption User, on that key alone). It's federated with the API's service account (`medapp-api` in `medapp`), so it uses no stored secret. |
| Managed identity for GitHub Actions | May only push to the registry. It's federated with this repository's `main` branch (OIDC), for the Publish workflow. |

`bootstrap.sh` then sets up the cluster, with the same chart versions as kind (`deploy/versions.sh`):
- **Traefik:** the ingress controller, with a public IP. It redirects http to https, and keeps clients' own IP addresses (`externalTrafficPolicy: Local`).
- **cert-manager:** with a Let's Encrypt issuer (`cluster.yaml`).
- **External Secrets Operator:** signed in with workload identity, with a ClusterSecretStore for Key Vault (`cluster.yaml`) that only serves the `medapp` namespace.
- **Argo CD:** following the **`env/azure`** branch (`deploy/argocd/medapp-azure.yaml`), in the `medapp` project (`deploy/argocd/project.yaml`), which limits it to the `medapp` namespace and the kinds the chart uses. Its UI isn't exposed; reach it with a port-forward.

MedApp's chart values are in `values.yaml`:
- **Two API copies,** with Valkey as the backplane, in the cluster.
- **Azure SQL** instead of SQL Server in the cluster.
- **Every secret from Key Vault.**
- **Let's Encrypt TLS.**
- **A network policy rule** letting the API and the migrations Job reach Azure SQL's private endpoint.
- **Email through Azure Communication Services' SMTP relay** (`smtp.azurecomm.net`, port 587, STARTTLS), signed in with the user name and password from Key Vault (`medapp-email`). The chart's network policy lets the API reach public addresses on 587 for it.
- **Data protection keys wrapped with Key Vault's key,** with the API signed in to Key Vault through workload identity. The chart's network policy lets the API reach public addresses on 443 for it (Key Vault and Microsoft Entra ID).
- **Three values only `deploy.sh` knows:** the API identity's client ID, the sender address and the key's URI. Fill them in after deploying (step 4).

## Deploying it

What you need:
- **An Azure subscription,** where you can create resources and role assignments (Owner, or Contributor plus User Access Administrator).
- **An Entra ID group** for MedApp's admins, with you in it.
- **A domain** you can point at the cluster.
- **Tools:** the Azure CLI, kubectl, kubelogin and Helm. On Windows: `winget install Microsoft.AzureCLI Kubernetes.kubelogin Helm.Helm`.

Steps:

1. **Fill in `main.bicepparam`:**
   - **The admin group:** its object ID and name. `deploy.sh` refuses to run while the object ID is still the all-zero placeholder.
   - **`environmentName`:** 1 to 8 lower-case letters and digits, such as `prod`. Anything else fails the deployment, with a message saying so.
2. **Set the ingress host in `values.yaml`** to your domain (`ingress.host`), and merge that to `main`.
3. **Make the Entra app for email,** and put its IDs in `main.bicepparam` (see [Email](#email)). You can skip this and add it on a later run, but until then no email is sent.
4. **Choose the passwords** (below), then sign in and deploy:

   ```bash
   az login
   deploy/azure/deploy.sh rg-medapp-prod uksouth
   ```

   It shows what would change (what-if), asks, then deploys. Then it prints the repository variables for step 6, and three values for `values.yaml`:
   - `api.workloadIdentity.clientId`: the API identity's client ID.
   - `email.fromAddress`: the managed domain's sender, `DoNotReply@<...>.azurecomm.net`.
   - `dataProtection.keyVaultKeyUri`: the data protection key.

   Set them in `values.yaml` and merge that to `main` before MedApp's first deploy (step 6). Without them, the API sends no email and stores its data protection keys unwrapped, and keys made that way stay unwrapped in the database until they expire.
5. **Set up the cluster:**

   ```bash
   deploy/azure/bootstrap.sh rg-medapp-prod you@example.com
   ```

   Then point your domain's DNS at the IP it prints.
   - **Right after `deploy.sh`,** the admin group's role on the cluster can take a few minutes to apply. `bootstrap.sh` waits for it (up to ten minutes) before it starts. It's safe to run again if it stops partway.
   - **Your kube contexts:** `az aks get-credentials` makes the AKS cluster your current context, and it stays that way afterwards, so switch back (`kubectl config use-context kind-medapp`) before running kind's scripts. `kubelogin convert-kubeconfig` rewrites only this cluster's context (`--context`); without that, it would rewrite every Azure context in your kubeconfig. Every command in the scripts names its context, so none relies on the current one.
6. **In GitHub, set the repository variables** `deploy.sh` printed: Settings, Secrets and variables, Actions, Variables:
   - `AZURE_CLIENT_ID`
   - `AZURE_TENANT_ID`
   - `AZURE_SUBSCRIPTION_ID`
   - `AZURE_ACR_NAME`

   They're identifiers, not secrets. From the next merge to `main`, the Publish workflow's `azure` job:
   1. Copies the image to the registry.
   2. Records that commit on `env/azure`, with the image (`deploy/argocd/README.md`).
   3. Argo CD deploys it. The migrations Job creates the schema and the first admin.

   So the first deploy to `env/azure` happens on the next merge after the variables are set. To deploy straight away instead, run Publish by hand on `main`: Actions, Publish, Run workflow.
7. **Sign in** as `admin`, with the first admin password.

### Passwords

`deploy.sh` reads three passwords from environment variables. They never go into a file or git:
- `MEDAPP_SQL_ADMIN_PASSWORD`
- `MEDAPP_FIRST_ADMIN_PASSWORD`
- `MEDAPP_BACKPLANE_PASSWORD`

Make them once, for example:

```bash
export MEDAPP_SQL_ADMIN_PASSWORD="Aa1$(openssl rand -hex 16)"
export MEDAPP_FIRST_ADMIN_PASSWORD="Aa1$(openssl rand -hex 12)"
export MEDAPP_BACKPLANE_PASSWORD="$(openssl rand -hex 24)"
```

A fourth, `MEDAPP_SMTP_PASSWORD`, is the email app's client secret (see [Email](#email)): Entra ID makes it, not you, and `deploy.sh` needs it once the app is in `main.bicepparam`.

Keep them in a password manager. Every run of `deploy.sh` sets Azure SQL's login and Key Vault's secrets to whatever they are, so use the same ones each time. The first admin's password only matters once: the account is made on an empty database and never changed afterwards.

**Changing a password needs a restart.** After `deploy.sh` runs with a new password, External Secrets updates the cluster's Secrets within `refreshInterval` (an hour). But the API and Valkey read them as environment variables when they start, so running pods keep the old ones until they're restarted:
- **The SQL admin password** changes on Azure SQL at once, so the app can't open new connections to the database until its pods restart. That's an outage: do it in a quiet window.
- **The backplane password:** restarted API pods get the new one while the running Valkey still has the old one, so restart both together.
- **The SMTP password** (`MEDAPP_SMTP_PASSWORD`, below): the old client secret works until you delete it in Entra ID, so delete it only after the API has restarted.

So, straight after `deploy.sh`, have External Secrets fetch the new values now rather than within the hour, then restart:

```bash
kubectl -n medapp annotate externalsecret medapp-db medapp-backplane medapp-email force-sync=$(date +%s) --overwrite
kubectl -n medapp rollout restart deployment/medapp-api deployment/medapp-valkey
```

## Email

The API sends password resets, email address confirmations and security notices through **Azure Communication Services Email**, over its SMTP relay. `main.bicep` makes:
- **An Email Communication Service** with an **Azure-managed domain**: a random `<...>.azurecomm.net` domain Azure verifies itself, so there's no DNS to set up. Emails come from `DoNotReply@` it (the `emailSenderAddress` output).
- **A Communication Services resource** linked to that domain, which owns the SMTP relay.
- **The data location:** the UK (`emailDataLocation`), for email data at rest. It's a geography, not a region.

### The Entra app the relay signs in as

The relay doesn't take a key: it signs in as a **Microsoft Entra app**, with one of the app's client secrets as the SMTP password. Bicep can't make an Entra app, so make it once by hand, then give `main.bicep` its IDs. It makes the rest only when they're set:
- **A custom role,** `MedApp SMTP sender (<resource group>)`, with only the three permissions the relay needs (read and write on Communication Services, write on Email Services). It's narrower than the built-in Communication and Email Service Owner. It's assigned to the app on the Communication Services resource alone.
- **An SMTP user name,** `medapp-<environment>-<suffix>`, linked to the app.
- **The `medapp-email` secret** in Key Vault, with that user name and the app's secret, which the chart turns into the API's `Email__Smtp__Username` and `Email__Smtp__Password`. Without the app it's still made, with empty values, so the API starts; sending then fails, and is logged.

To make the app, as someone who may create app registrations:

```bash
app_id=$(az ad app create --display-name "MedApp SMTP (prod)" --query appId --output tsv | tr -d '\r')
principal_id=$(az ad sp create --id "$app_id" --query id --output tsv | tr -d '\r')
# The SMTP password. Entra ID shows it only now, so keep it in your password manager with the others.
export MEDAPP_SMTP_PASSWORD=$(az ad app credential reset --id "$app_id" --display-name smtp --years 1 \
  --query password --output tsv | tr -d '\r')
echo "smtpAppClientId: $app_id"; echo "smtpAppPrincipalId: $principal_id"
```

Put the two IDs in `main.bicepparam` (`smtpAppClientId`, `smtpAppPrincipalId`), then run `deploy.sh`. From then on, `deploy.sh` refuses to run without `MEDAPP_SMTP_PASSWORD`, because every run writes it to Key Vault. The app needs no API permissions or admin consent: its role on the resource is all the relay checks.

**The secret expires** (here after a year, at most two). Before it does, make a new one with `az ad app credential reset --id <app ID> --append` (`--append` keeps the old one working), run `deploy.sh` with it, sync and restart as in [Passwords](#passwords), then delete the old one.

### Limits of the managed domain

The Azure-managed domain is for getting started:
- **Very low sending limits:** 5 emails a minute and 10 an hour, for the whole subscription, and they can't be raised. Past them, the relay refuses emails, and the API logs the failures. Enough to try it out with a few staff; not for real use, where a burst of password resets or new accounts would hit them.
- **A sender nobody recognises.** `DoNotReply@<random>.azurecomm.net` can't be changed, and some mail filters treat it with suspicion.

For production, add a **custom domain** you own (an `emailServices/domains` resource with `domainManagement: 'CustomerManaged'`, plus the SPF, DKIM and DMARC records Azure gives you) and link it instead. Its limits start at 30 a minute and 100 an hour, and can be raised. That's in [Not yet](#not-yet).

## Data protection keys

The API's ASP.NET Core data protection keys seal password reset and email confirmation tokens. It keeps them in Azure SQL (`DataProtectionKeys`), so both copies share them. Each one is wrapped with Key Vault's `medapp-data-protection` key, so a copy of the database alone, such as a backup, can't unseal them:
- **The key** is RSA 2048, limited to wrapping and unwrapping. Its private half never leaves Key Vault. Bicep makes it once: later runs leave it, and its versions, as they are.
- **The API reaches it as its own managed identity,** through workload identity (the chart's `api.workloadIdentity.clientId`). The identity may use that one key, and nothing else in the vault.
- **Rotating the key:** make a new version (`az keyvault key rotate`, as someone with Key Vault Crypto Officer). New keys are wrapped with it, and old ones still unwrap with the version that wrapped them, so never delete or disable an old version while keys wrapped with it may still be used (90 days).
- **Losing the key** (deleting it and purging it) makes every wrapped key unreadable. Users then need new reset and confirmation links, and nothing else breaks. Purge protection stops it being purged for 90 days.

## Cost

These are rough UK South pay-as-you-go prices; check the [pricing calculator](https://azure.microsoft.com/pricing/calculator/).
- **AKS:** the Free tier costs nothing for the control plane. Two `Standard_D2s_v5` nodes cost about £120 a month, and the Standard tier, with an uptime SLA, adds about £60.
- **Azure SQL serverless:** you pay per second of use, with a small floor while it's paused. Count roughly £5 to £40 a month for light use, plus storage.
- **The load balancer and its public IP:** about £20.
- **The registry (Basic) and Key Vault:** a few pounds. The data protection key's operations are a fraction of a penny per 10,000.
- **Email:** pay per email sent and per MB transferred, each a small fraction of a penny (check the [Communication Services pricing](https://azure.microsoft.com/pricing/details/communication-services/)), so MedApp's account emails come to pennies a month. The resources and the managed domain have no monthly charge of their own.

**Deleting it** deletes the database and all its backups for good, so `main.bicep` puts a `CanNotDelete` lock (`medapp-keep-database`) on the SQL server. With it, `az group delete` fails and deletes nothing. To delete everything on purpose, remove the lock first:

```bash
lock=$(az lock list --resource-group rg-medapp-prod --query "[?name=='medapp-keep-database'].id" --output tsv | tr -d '\r')
az lock delete --ids "$lock"
az group delete --name rg-medapp-prod
```

Key Vault then stays recoverable for 90 days (purge protection), so its name can't be reused before then. Its name comes from the resource group's ID, so a new resource group with the same name gets the same vault name, and `deploy.sh` fails there until the 90 days are up.

## Not yet

- **Passwordless Azure SQL.** The app signs in with the SQL server's own admin login, held in Key Vault. The better way is workload identity for the API too, with a contained database user for its identity (`Authentication=Active Directory Workload Identity`), and then turning SQL authentication off. That needs the chart to give the API a federated service account, and a one-off `CREATE USER ... FROM EXTERNAL PROVIDER` by an Entra admin.
- **A least-privilege database user.** Even with a password, the app should have its own user, not the server admin.
- **A private registry and Key Vault.** Both still have public endpoints, guarded by Entra ID. Private endpoints for them need the registry's Premium tier.
- **Monitoring:** Container Insights and Log Analytics, and alerts.
- **Backups beyond Azure SQL's own** (point-in-time restore for 7 days by default).
- **A MediatR licence.** MediatR 14 logs a licence warning at startup ("If you are running in production you are required to have a licensed version"). Running on Azure is production use: get a licence key (a free community one may apply), or replace MediatR.
- **Readiness that checks the database and Valkey.** `/health` only says the process is up, so a pod stays ready while either is down.
- **Restarting Node.** If the page server exits, the API serves pages rendered in the browser until the pod restarts, and nothing reports it.
- **Pinning by digest.** The workflows' actions are pinned by tag, and the images (the Dockerfile's bases, Valkey) by tags that move. Both should be pinned by digest, with Dependabot keeping them current.
- **Restarting pods when a Secret changes** (above, Passwords), for example with a reloader.
- **A private AKS API server.** It's public, guarded only by Entra ID. Authorised IP ranges (`apiServerAccessProfile`), or a private cluster, would restrict who can reach it.
- **A custom email domain.** The Azure-managed domain sends at most 10 emails an hour, and its sender address is random (see [Email](#email)). Needed before real use.
- **Egress limited by name.** The API may reach any public address on 587 (the SMTP relay) and 443 (Key Vault and Microsoft Entra ID), because NetworkPolicies can't name hosts. Cilium's FQDN policies need Advanced Container Networking Services (paid); an Azure Firewall would do it too, at more cost.
- **Never deployed,** so these are untested: `UK` as the email data location, the SMTP user name and custom role (the relay may take a few minutes to see a new role), and workload identity with the API's `automountServiceAccountToken: false` (AKS's webhook adds its own token volume, so it should work).
- **A reserved ingress IP.** Traefik's public IP comes with its service, so it changes if that service is recreated (a reinstall), and DNS must follow. A static public IP from Bicep, given to Traefik, would keep it.
- **Ingress through node upgrades.** Traefik runs as one pod, with no disruption budget, and AKS has no maintenance window set. So each node image upgrade is a short outage, at a time Azure picks. Two replicas with a PodDisruptionBudget, and a `maintenanceConfigurations` window, would fix that.
