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
| Key Vault | RBAC, soft delete and purge protection. Named `kv-medapp-<environment>-<5 characters>`, at most 24 characters. Holds the chart's three secrets: `medapp-db`, `medapp-first-admin` and `medapp-backplane`, as JSON. |
| Managed identity for External Secrets | May only read Key Vault's secrets. It's federated with External Secrets' service account in AKS, so it uses no stored secret. |
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
3. **Choose the passwords** (below), then sign in and deploy:

   ```bash
   az login
   deploy/azure/deploy.sh rg-medapp-prod uksouth
   ```

   It shows what would change (what-if), asks, then deploys, and prints the repository variables for step 5.
4. **Set up the cluster:**

   ```bash
   deploy/azure/bootstrap.sh rg-medapp-prod you@example.com
   ```

   Then point your domain's DNS at the IP it prints.
   - **Right after `deploy.sh`,** the admin group's role on the cluster can take a few minutes to apply. `bootstrap.sh` waits for it (up to ten minutes) before it starts. It's safe to run again if it stops partway.
   - **Your kube contexts:** `az aks get-credentials` makes the AKS cluster your current context, and it stays that way afterwards, so switch back (`kubectl config use-context kind-medapp`) before running kind's scripts. `kubelogin convert-kubeconfig` rewrites only this cluster's context (`--context`); without that, it would rewrite every Azure context in your kubeconfig. Every command in the scripts names its context, so none relies on the current one.
5. **In GitHub, set the repository variables** `deploy.sh` printed: Settings, Secrets and variables, Actions, Variables:
   - `AZURE_CLIENT_ID`
   - `AZURE_TENANT_ID`
   - `AZURE_SUBSCRIPTION_ID`
   - `AZURE_ACR_NAME`

   They're identifiers, not secrets. From the next merge to `main`, the Publish workflow's `azure` job:
   1. Copies the image to the registry.
   2. Records that commit on `env/azure`, with the image (`deploy/argocd/README.md`).
   3. Argo CD deploys it. The migrations Job creates the schema and the first admin.

   So the first deploy to `env/azure` happens on the next merge after the variables are set. To deploy straight away instead, run Publish by hand on `main`: Actions, Publish, Run workflow.
6. **Sign in** as `admin`, with the first admin password.

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

Keep them in a password manager. Every run of `deploy.sh` sets Azure SQL's login and Key Vault's secrets to whatever they are, so use the same ones each time. The first admin's password only matters once: the account is made on an empty database and never changed afterwards.

**Changing a password needs a restart.** After `deploy.sh` runs with a new password, External Secrets updates the cluster's Secrets within `refreshInterval` (an hour). But the API and Valkey read them as environment variables when they start, so running pods keep the old ones until they're restarted:
- **The SQL admin password** changes on Azure SQL at once, so the app can't open new connections to the database until its pods restart. That's an outage: do it in a quiet window.
- **The backplane password:** restarted API pods get the new one while the running Valkey still has the old one, so restart both together.

So, straight after `deploy.sh`, have External Secrets fetch the new values now rather than within the hour, then restart:

```bash
kubectl -n medapp annotate externalsecret medapp-db medapp-backplane force-sync=$(date +%s) --overwrite
kubectl -n medapp rollout restart deployment/medapp-api deployment/medapp-valkey
```

## Cost

These are rough UK South pay-as-you-go prices; check the [pricing calculator](https://azure.microsoft.com/pricing/calculator/).
- **AKS:** the Free tier costs nothing for the control plane. Two `Standard_D2s_v5` nodes cost about £120 a month, and the Standard tier, with an uptime SLA, adds about £60.
- **Azure SQL serverless:** you pay per second of use, with a small floor while it's paused. Count roughly £5 to £40 a month for light use, plus storage.
- **The load balancer and its public IP:** about £20.
- **The registry (Basic) and Key Vault:** a few pounds.

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
- **Pinning by digest.** The workflows' actions are pinned by tag, and the images (the Dockerfile's bases, Valkey) by tags that move. Both should be pinned by digest, with Dependabot keeping them current.
- **Restarting pods when a Secret changes** (above, Passwords), for example with a reloader.
- **A private AKS API server.** It's public, guarded only by Entra ID. Authorised IP ranges (`apiServerAccessProfile`), or a private cluster, would restrict who can reach it.
- **A reserved ingress IP.** Traefik's public IP comes with its service, so it changes if that service is recreated (a reinstall), and DNS must follow. A static public IP from Bicep, given to Traefik, would keep it.
- **Ingress through node upgrades.** Traefik runs as one pod, with no disruption budget, and AKS has no maintenance window set. So each node image upgrade is a short outage, at a time Azure picks. Two replicas with a PodDisruptionBudget, and a `maintenanceConfigurations` window, would fix that.
