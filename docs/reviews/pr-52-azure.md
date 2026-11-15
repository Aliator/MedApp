# Review: PR #52, the Azure setup and the deploy fixes

**PR:** [#52](https://github.com/Aliator/MedApp/pull/52), branch `feature/azure` at `313c5ec`, against `main` at `6cd0aa3`.

**Reviewed:** 15 November 2026, independently of the author's notes. Every claim below was checked against the code, a tool, or the GitHub API, unless it's marked suspected.

## What the PR changes, and why

This is step 5 of the Kubernetes plan, with the deploy, chart and Azure findings from `docs/reviews/flaws-review.md`. The SSR findings are in #51.

- **Azure (`deploy/azure`), written but never deployed.**
  - **`main.bicep`:**
    - a virtual network
    - AKS: CNI Overlay with Cilium, workload identity, Entra ID only, and a user-assigned control-plane identity that gets its subnet role first
    - Azure SQL serverless behind a private endpoint, with a delete lock
    - Key Vault, holding the chart's three secrets
    - ACR
    - federated identities for External Secrets and for GitHub Actions
  - **`deploy.sh`** runs what-if, then deploys.
  - **`bootstrap.sh`** installs Traefik, cert-manager with Let's Encrypt, External Secrets reading Key Vault, and Argo CD following `env/azure`.
- **Deploys:**
  - `.github/scripts/deploy-to-env.sh` builds each deploy commit with git plumbing: `main`'s tree plus the image file, never a merge.
  - Publish deploys only from `main`.
  - A new `azure` job copies the image to ACR, signing in with OIDC, and moves `env/azure`.
- **Argo CD:** a `medapp` AppProject, limited to this repository, the `medapp` namespace and the chart's kinds.
- **CI:**
  - `npm run build`
  - a new `image` job that builds the Dockerfile
  - the Bicep compiled and linted
  - the chart linted with Azure's values
  - renders of the four cluster-service charts
- **App and tools:**
  - EF Core retries transient SQL errors.
  - `IdentityTransactions` and the dev tool's wipe run inside the execution strategy.
  - The dev tool builds again.
- **Docs:**
  - the review notes in `docs/reviews/`
  - `AGENTS.md`'s staged effort and review step
  - the READMEs and `docs/todo.md`

## How it was checked

**The PR's own checks.** CI run [43764683304](https://github.com/Aliator/MedApp/actions/runs/43764683304) on `313c5ec` passed every job:
- `web`, including `npm run build`
- `image`
- `helm`, including the Bicep and the four cluster-service renders
- `unit-tests`
- `coverage-comment`

So the new CI steps do run as written on ubuntu-latest.

**What I ran**, in the worktree or in a scratch folder:
- **.NET:**
  - `dotnet build MedApp.sln -c Release` succeeds, with one warning: CS0414 in `MedApp.Client.Deprecated`, which this PR doesn't touch.
  - `dotnet test MedApp.UnitTests -c Release`: 708 passed.
  - `dotnet build tools/MedApp.DevMcp -c Release` succeeds, with no warnings.
- **Helm 4.3.2, the chart:**
  - `helm lint --strict` passes with the defaults, kind's values and Azure's.
  - `helm template` with kind's and Azure's values: every kind rendered is on `project.yaml`'s whitelist.
- **Helm 4.3.2, the cluster services:**
  - Traefik 42.3.2 renders with both values files. Azure's gives `--entryPoints.web.http.redirections.entryPoint.to=:443`, `--entryPoints.websecure.http.tls=true` and `externalTrafficPolicy: Local`.
  - cert-manager v1.21.2 renders.
  - External Secrets 2.14.0 renders with `bootstrap.sh`'s two `--set-string` values.
  - Argo CD 10.17.2 (v3.6.1) renders with both values files.
- **External Secrets:**
  - The service account is `external-secrets` in `external-secrets`, with the client ID annotation, and the label lands on the controller's pod.
  - 2.14.0's v1 `ClusterSecretStore` CRD has the four fields `cluster.yaml` uses: `conditions[].namespaces`, `azurekv.authType: WorkloadIdentity`, `serviceAccountRef` and `vaultUrl`.
- **Bicep 0.49.2:**
  - `build`, `lint` (no warnings) and `build-params` with dummy passwords all pass.
  - I read the compiled JSON for the order of the subnet role, AKS and the role assignments.
- **Scripts:** `bash -n` passes on every script, and git records the four executable ones as `100755`.
- **`deploy-to-env.sh`,** in a scratch repository with a bare origin and a detached checkout, as in Actions:
  - **The first deploy** creates the branch, with `main` as its only parent.
  - **A re-run** changes nothing.
  - **The next deploy** has the previous tip and `main` as parents, and its diff holds only what changed.
  - **A run for an older commit** moves the branch back.
  - **One change to the tree** (finding 1's fix) works as described.
- **The GitHub API, read-only:**
  - the two rulesets
  - the OIDC subject, which is GitHub's default (`repo:Aliator/MedApp:ref:refs/heads/main`)
  - an anonymous pull token for `ghcr.io/aliator/medapp-api`, so the image is public
  - `env/kind`'s history
- **Sources:**
  - Microsoft's [Private Link page for Azure SQL](https://learn.microsoft.com/azure/azure-sql/database/private-endpoint-overview)
  - cert-manager's HTTP-01 solver: `pkg/issuer/acme/http/pod.go` and `http.go`
  - Argo CD's AppProject CRD

**Not done:** nothing against Azure; nothing on kind, Docker or any database; and no local `npm run build` (CI ran it).

## Findings, most severe first

### 1. Deploy pushes will probably be refused whenever `main` changed a workflow, starting with this merge

- **Severity:** high. **Confidence:** suspected. It can't be tested without pushing with `GITHUB_TOKEN`, but the evidence is strong.
- **Evidence:**
  - **The token:** Publish pushes the environment branches with `GITHUB_TOKEN` (`publish.yml:17-19` and `:66`; `deploy-to-env.sh:69`). GitHub refuses a push by a GitHub App token, `GITHUB_TOKEN` included, that creates or updates anything under `.github/workflows/`, unless the token has the `workflows` permission. A workflow can't give `GITHUB_TOKEN` that permission.
  - **What GitHub checks:** what the ref ends up containing, not just the new commits. GitHub refused a `GITHUB_TOKEN` push of tags that pointed at commits already on GitHub, when their workflow files differed from the current ones ([community discussion 26164](https://github.com/orgs/community/discussions/26164)).
  - **The deploy commit:** its tree is `main`'s (`deploy-to-env.sh:43-53`), workflows included. `env/kind`'s tip (`1d4c33f`) has the workflows from before this PR, and this PR changes `main.yml` and `publish.yml`. `git diff --stat 1d4c33f 313c5ec -- .github/workflows` shows 129 lines changed.
  - **Never tried:** neither Publish deploy so far (`2e9a004`, `1d4c33f`) changed a workflow file on `env/kind`. The one commit that did (`362b6de`) was pushed by hand.
- **Failure scenario:**
  1. This PR merges, and Publish builds and pushes the image.
  2. "Deploy to kind (env/kind)" fails with "refusing to allow a GitHub App to create or update workflow `.github/workflows/main.yml` without `workflows` permission".
  3. The `azure` job needs that job, so it's skipped too (finding 2).
  4. The same happens on every later merge that touches `.github/workflows/`, on both branches once `env/azure` exists.
- **Fix:** keep the branch's own `.github/workflows` in each deploy commit, so a deploy never changes them. Nothing on an environment branch runs them: `main.yml` runs on pull requests to `main`, and Publish on pushes to `main`. Add this after `git read-tree "$commit"`:

  ```bash
  # GITHUB_TOKEN may not change workflow files, and nothing on an environment branch runs them: keep the branch's own.
  if [[ -n $parent ]]; then
    git rm -r -q --cached --ignore-unmatch -- .github/workflows
    if git cat-file -e "$parent:.github/workflows" 2>/dev/null; then
      git read-tree --prefix=.github/workflows/ "$parent:.github/workflows"
    fi
  fi
  ```

  In the scratch repository, the resulting tree matches `main`'s except `.github/workflows`, which matches the previous tip's. The alternative is a GitHub App token with `workflows` write, which finding 4 suggests anyway.
- **If it happens anyway:** run the script from your own machine, so it pushes with your credentials:

  ```bash
  GITHUB_SHA=<merge commit> .github/scripts/deploy-to-env.sh env/kind deploy/argocd/kind-image.yaml ghcr.io/aliator/medapp-api kind
  ```

### 2. Azure deploys still depend on the kind deploy

- **Severity:** medium. **Confidence:** confirmed.
- **Evidence:**
  - The `azure` job `needs: image` (`publish.yml:61`), and the `image` job ends with the kind deploy (`:54-56`).
  - `docs/reviews/gitops.md` (risk 4) raised this, and it's unchanged.
- **Failure scenario:** anything that fails the `env/kind` push stops production deploys:
  - finding 1
  - a hand commit to `env/kind` that lands between the fetch and the push
  - a GitHub error on that one push

  kind is a local cluster that may not even be running, but its branch now gates Azure.
- **Fix:**
  - Move the kind deploy into its own `kind` job.
  - Have `kind` and `azure` both need only the image build, each with its own `contents: write`.
  - The build job then needs only `packages: write`.

### 3. Re-running an older Publish run silently moves an environment back

- **Severity:** medium. **Confidence:** confirmed in the scratch repository.
- **Evidence:**
  - **The script:** `deploy-to-env.sh:55-59` skips a deploy only when the tree is unchanged. Given an older `GITHUB_SHA`, it pushes a deploy commit of that older `main`. In the scratch repository it printed "env/azure now runs 3c36a49…", one commit behind `main`.
  - **The comment:** `publish.yml:21-23` says each branch "always ends on the newest commit".
- **Failure scenario:**
  1. Merge A's `azure` job fails for a passing reason.
  2. Merge B deploys.
  3. Days later, someone presses "Re-run failed jobs" on A's run.
  4. `env/azure` goes back to A: chart, image and migrations bundle together. `env/kind` stays on B, and nothing says production was rolled back.

  The documented rollback is a revert on `main`, so this is never what anyone means.
- **Fix:**
  - **Refuse to deploy a commit older than the one deployed,** unless asked to:

    ```bash
    # The commit of main a deploy runs is its second parent (a hand commit has none).
    if [[ -n $parent ]] && deployed=$(git rev-parse --verify --quiet "$parent^2"); then
      if [[ $deployed != "$commit" ]] && git merge-base --is-ancestor "$commit" "$deployed"; then
        echo "$branch already runs $deployed, which is newer than $commit: not moving it back." >&2
        exit 0
      fi
    fi
    ```

    `env/kind`'s current tip, a merge from the old script, also has its commit of `main` as its second parent, so this works on it too.
  - **Correct the comment** in `publish.yml`.

### 4. Write access to an `env/*` branch is still production deploy access, and the project lets it weaken the namespace

- **Severity:** medium. **Confidence:** confirmed from the ruleset and the manifests (not run against Argo CD).
- **Evidence:**
  - **The ruleset:** "Environment branches" blocks only deletion and non-fast-forward pushes, and has no bypass list (GitHub API). Anything with `contents: write` can push a fast-forward commit to `env/azure`. That includes any step in a job holding Publish's token, and those jobs run `docker/*` and `azure/login` actions pinned by tag (`publish.yml:30-52`, `:71-81`). That's `flaws-review.md` finding 14, still open.
  - **Argo CD:** syncs whatever lands, automatically (`medapp-azure.yaml:27-30`). The project allows Deployments, Jobs and Secrets in `medapp`, so a commit can run any image with the database's admin connection string.
  - **The namespace:** `project.yaml:24-26` allows any `Namespace`, matched by kind only. A commit can add a `Namespace` named `medapp` labelled `pod-security.kubernetes.io/enforce: privileged`, and a Deployment with a privileged pod that mounts the host. That gives the node, then the cluster: Argo CD's credentials, and External Secrets' identity.
- **Failure scenario:** a moved action tag in the `image` job pushes such a commit to `env/azure`. The project, which is meant to stop exactly this, lets it through.
- **Fix:**
  - **Don't let Argo CD create the namespace.** `bootstrap.sh` and `up.sh` already run as cluster admin:
    - Create `medapp` there, with its Pod Security labels.
    - Drop `CreateNamespace` and `managedNamespaceMetadata`.
    - Leave `clusterResourceWhitelist` empty.

    At the least, add `name: medapp` to the entry; Argo CD 3.6's AppProject CRD supports it. That still lets a `medapp` Namespace manifest change the labels.
  - **Restrict updates to `env/*`** in the ruleset, with a GitHub App that only the deploy steps use as the one bypass. Giving that App `workflows` write also fixes finding 1.
  - **Pin the actions** in `publish.yml` by SHA.

### 5. A retry inside `IdentityTransactions` can make `SetRolesAsync` fail when it shouldn't

- **Severity:** low. **Confidence:** confirmed by reading Identity's `UserStore`; not run.
- **Evidence:**
  - **The user is loaded outside the transaction:** `IdentityRoleService.SetRolesAsync` loads it at `IdentityRoleService.cs:66`, and changes its roles inside the transaction (`:88-101`).
  - **A retry clears the change tracker** (`IdentityTransactions.cs:28-29`), which detaches that user.
  - **The failed attempt has already changed the user in memory:** Identity's `UserStore.UpdateAsync` sets a new `ConcurrencyStamp` before `SaveChanges`. The rollback restores the row, not the object.
  - **The retry then conflicts with itself:** Identity attaches the user with that new stamp as its original value. `UPDATE … WHERE ConcurrencyStamp = <new stamp>` matches no row, and the result is `ConcurrencyFailure` instead of the roles changing.
  - **A commit can succeed but look failed,** when its acknowledgement is lost. The retry then repeats work that already happened:
    - `SetRolesAsync` reports "user not in role".
    - `FirstAdminSetup` logs "No first admin needed", having just made one.
    - Elsewhere, a retried `SaveChanges` with client-generated GUIDs reports a duplicate key for a row that was saved.
  - **Untested:** `IdentityTransactions` is excluded from coverage as needing a database, so the retry path has no test.
- **Failure scenario:** an Azure SQL failover hits during an admin's role change. The retry fails it with a misleading concurrency error, and the admin has to try again.
- **Fix:**
  - **Have the work load everything it changes,** so each attempt starts clean. Move the user lookup, the role lookups and `GetRolesAsync` into the delegate passed to `RunAsync`.
  - **Document the rule:** the comment at `IdentityTransactions.cs:16-19` should say that callers mustn't capture tracked entities.
  - **Where a lost commit matters,** use EF's `ExecuteInTransactionAsync` with a `verifySucceeded` check.

### 6. CI still doesn't build the dev tool, which is how it broke

- **Severity:** low. **Confidence:** confirmed.
- **Evidence:** `tools/MedApp.DevMcp` isn't in `MedApp.sln`, and no CI job builds it. It stopped compiling in `9fcb750`, when `DeleteUserCommand` gained the admin's password, and nothing noticed until this PR.
- **Fix:** add `dotnet build tools/MedApp.DevMcp -c Release` to the `unit-tests` job.

The `delete_user` change itself is sound. `DeleteUserHandler` only checked the acting admin's password, then called `IIdentityUserService.DeleteUserAsync`, which the tool now calls directly. The tool keeps its own guards: protected accounts are refused, and it needs `confirm: true`.

### 7. First-run rough edges in the Azure scripts

- **Severity:** low. **Confidence:** suspected, since nothing has run.
- **The placeholder group:**
  - **What happens:** `main.bicepparam:8` ships an all-zero object ID, and nothing checks it. `deploy.sh` would create the cluster (about ten minutes), then fail on the group's role assignment, and possibly on SQL's Entra admin.
  - **Fix:** have `deploy.sh` refuse the all-zero ID.
- **`environmentName` isn't validated:**
  - **What happens:** a hyphen or a capital letter breaks the registry's name, which allows only letters and digits, and the SQL server's, which must be lower case (`main.bicep:63-64`).
  - **Fix:** lower-case it with `toLower`, and say "letters and digits" in its description.
- **Role propagation:**
  - **What happens:** a new Azure RBAC role assignment can take a few minutes to apply to Kubernetes. `bootstrap.sh`, run straight after `deploy.sh`, may get a 403 at its first `helm`.
  - **Fix:** the script is safe to run again; the README could say so.
- **kubeconfig side effects:**
  - **What happens:** `az aks get-credentials` makes AKS the current context, and `kubelogin convert-kubeconfig` (`bootstrap.sh:34`) converts every Azure context in the kubeconfig, not just this one.
  - **Fix:** pass `--context "$aks"` to the conversion, and mention the context switch.
- **The first `env/azure`:**
  - **What happens:** it only appears with the next merge after the variables are set (`README.md:67`).
  - **Fix:** the README could say that starting Publish by hand on `main` does it too.

### 8. Open Azure risks that aren't written down anywhere

- **Severity:** low. **Confidence:** confirmed from the code.
- **Evidence:** `docs/reviews/azure.md` raised these as risks 7, 10 and 17. Neither the README's "Not yet" nor `docs/todo.md` mentions them.
  - **The API server is public:** `main.bicep:153-217` has no `apiServerAccessProfile`, so only Entra ID protects it.
  - **The ingress IP isn't reserved,** so reinstalling Traefik can change it.
  - **There's one Traefik pod,** with no PodDisruptionBudget and no AKS maintenance window. With `externalTrafficPolicy: Local`, the load balancer sends nothing until the rescheduled pod passes its health probe. So every node image upgrade is a short outage, at a time Azure picks.
- **Fix:** add them to "Not yet", or fix them:
  - authorised IP ranges for the API server
  - a static public IP from Bicep, given to Traefik
  - two Traefik replicas with a PDB
  - a `maintenanceConfigurations` window

### 9. Docs that don't match the code or the repository

- **Severity:** low. **Confidence:** confirmed.
- **`docs/reviews/overview.md:22-26`:**
  - **The problem:** it links to `pr-51-ssr-security-fixes.md`, which isn't on this branch. It also describes #51's SSR fixes as done, but on this branch `SsrPages.cs:52` still builds the URL with `new Uri(address, …)`.
  - **Fix:** merge #51 first, or reword it.
- **`docs/reviews/README.md`, `azure.md` and `kubernetes.md`:**
  - **The problem:** they still call the Azure work uncommitted. The "Read these first" list includes problems this PR fixes, without saying so.
  - **Fix:** mark which ones this PR fixes, as `gitops.md` does, or add a status column to the index.
- **`docs/todo.md:36`:**
  - **The problem:** it lists the repository settings as still to do. `main` already requires `web`, `helm` and `unit-tests`, and `env/*` already blocks deletion and force pushes. Only `image` isn't required.
  - **Fix:** make `image` required too, so a broken Dockerfile can't merge, then update the line.
- **Left over from `flaws-review.md` finding 31:**
  - `deploy/kind/traefik-values.yaml:2` still says there's no TLS locally.
  - `deploy/helm/medapp/templates/secret.yaml:12` still says the volume "only opens with" its password.
  - `deploy/kind/up.sh:198` says "Back to one copy", but kind runs two (`deploy/kind/values.yaml:9`).

## Checked and found sound

What the brief asked about, and that holds up:
- **Bicep resources:**
  - **API versions and properties:** they compile with no BCP warnings against Bicep 0.49.2's types.
  - **Role definition IDs:** all five are the right built-in roles (AcrPull, AcrPush, Key Vault Secrets User, Network Contributor, Azure Kubernetes Service RBAC Cluster Admin).
  - **Role assignments:** `principalType` is `ServicePrincipal` for the identities and `Group` for the admins, and the scopes are the subnet, the registry, the vault and the cluster.
  - **Order:** the subnet role depends on the identity and the network, and AKS depends on that role. So the user-assigned control-plane identity has its subnet rights before the cluster exists, which fixes the earlier "AKS identity" question.
- **Bicep configuration:**
  - **AKS:** `disableLocalAccounts` comes with managed Entra ID and Azure RBAC, as it must. Cilium has both dataplane and policy set to `cilium`, with overlay and a pod CIDR that doesn't overlap the virtual network or the service CIDR.
  - **SQL:** the server has the Entra admin group and SQL authentication (`azureADOnlyAuthentication: false`), with public access off.
  - **The private endpoint:** `groupIds: ['sqlServer']`, with a DNS zone group to a `privatelink.database.windows.net` zone linked to the virtual network.
  - **The connection policy:** on Default, Microsoft documents that private endpoint connections use Proxy on 1433, which is exactly what the network policy allows.
  - **The lock:** it's on the server, so it covers the database and stops `az group delete`.
- **Secrets and identities:**
  - **Secrets:** they come only from `@secure()` parameters into Key Vault's write-only `value`. Nothing secret is in the outputs, and the template stores only expressions. Whether what-if prints them is still open (see below).
  - **Federated credentials:** the issuer is the cluster's own `issuerURL`. The subjects match what's presented: `system:serviceaccount:external-secrets:external-secrets` (the chart's service account, verified by rendering), and GitHub's default subject for `main`, which this repository uses.
  - **Names:** with an 8-character environment, the vault's name is exactly 24 characters. Every other name is within its limit.
- **Scripts:**
  - **Context name:** for an Entra cluster, `az aks get-credentials` (non-admin) names the context after the cluster, which is what `bootstrap.sh` uses.
  - **External Secrets values:** `serviceAccount.annotations` and `podLabels` are the right keys in chart 2.14.0.
  - **Tenant ID:** the store doesn't set one. External Secrets falls back to `AZURE_TENANT_ID`, which the workload identity webhook adds to the labelled pod. Setting `tenantId` in `cluster.yaml` would remove that dependency.
  - **Carriage returns:** stripped wherever `az` output is captured.
  - **Email:** escaped for `sed`.
- **Let's Encrypt over HTTP-01:**
  - **The redirect:** Traefik redirects the challenge to https, and Let's Encrypt follows it without checking the certificate.
  - **On https:** `websecure` applies TLS to every router, including the solver's Ingress.
  - **The self-check:** cert-manager's follows redirects with `InsecureSkipVerify`.
  - **Pod Security:** the solver pod meets the `restricted` standard: non-root, RuntimeDefault seccomp, no privilege escalation, all capabilities dropped.
  - **Network policies:** the chart's select only its own pods, so none blocks the solver.
- **Argo CD:**
  - **The certificate:** the Certificate and Secret cert-manager makes from the Ingress aren't tracked by Argo CD (v3.6 tracks by annotation), so the project needn't allow them and prune leaves them alone.
  - **The namespace:** `CreateNamespace` with `managedNamespaceMetadata` works with the Namespace whitelist, though see finding 4.
- **Workflows:**
  - **Permissions:** the `azure` job has `id-token: write` and `contents: write`. Job-level permissions replace the workflow's, which is fine: the GHCR image is public, so reading it needs no `packages` permission.
  - **Registry sign-in:** `az acr login` writes Docker credentials that `docker buildx imagetools create` uses for ACR. GHCR is read anonymously.
  - **Ref guards:** `github.ref == 'refs/heads/main'` is correct for both a push and a dispatch on `main`.
- **`deploy-to-env.sh`:**
  - **The temporary index:** built at a new path, never touching the checkout.
  - **Parents and pushes:** the parents keep every push a fast-forward, which the `env/*` ruleset requires, and the first deploy creates the branch, which the ruleset allows.
  - **Re-runs:** idempotent.
  - **In Actions:** it works on a detached checkout at `GITHUB_SHA`.
  - **A failed fetch:** if the branch exists but the fetch fails, the push is refused as non-fast-forward rather than replacing history.
- **The EF retry:**
  - **No other transactions:** no other code path opens a transaction, uses `TransactionScope`, raw SQL or a manual connection.
  - **The dev tool:** its one transaction is now inside the strategy.
  - **The migrations bundle:** it's built against `MedApp.API`'s startup (`Dockerfile:45`), so it gets the same retrying options.

## Not verified

- **What-if and secrets:** whether `az deployment group what-if` prints the Key Vault secret values, and the SQL password inside the connection string. What-if is free: run it once against a scratch resource group with dummy passwords before the real one.
- **The EF retry budget:**
  - **The numbers:** five retries with EF's default backoff wait about 26 s in all, plus whatever SqlClient's own connection retries add. A serverless database takes about a minute to resume.
  - **The risk:** whether the first request after a pause succeeds, or fails after the retries, is untested. A paused database on the first real deploy will tell.
- **`azure/login`:** with a role only on the registry, the subscription should still be listed, as with resource-group-scoped identities. If the login reports "No subscriptions found", set `allow-no-subscriptions: true`.
- **`az acr login`:** with only AcrPush, it can't read the registry through Azure Resource Manager, and falls back to `<name>.azurecr.io` with a warning. That's expected.
- **Redeploying AKS:** whether a second `deploy.sh` run objects to `count` while the autoscaler has changed the node count.

## Earlier findings: what this PR really fixes

Against `docs/reviews/flaws-review.md`. Findings 1, 3, 6, 10, 19 and 24 belong to #51 (SSR and the first admin), so they aren't assessed here.

| # | Finding | After this PR |
|---|---|---|
| 2 | A `git revert -m 1` rollback is lost at the next deploy | **Fixed.** Deploy commits replace the tree (verified), and the docs now say to revert on `main`. New gap: finding 3. |
| 4 | Traefik values use the wrong key | **Fixed.** They render at 42.3.2, with the redirect and TLS on `websecure`, and the comment is right. CI renders all four charts. |
| 5 | A password change causes an outage | **Documented, not fixed.** The README now explains it and gives the right commands (the `force-sync` annotation, then restarting both Deployments). Bicep still owns the values, and nothing restarts the pods. |
| 7 | CI isn't required | **Mostly fixed, in the repository settings:** `main` requires `web`, `helm` and `unit-tests`. `image` isn't required. |
| 8 | Environment branches are unprotected, under the default project | **Partly fixed.** The deploys are guarded to `main`, `env/*` can't be deleted or force-pushed, and the project limits the repository, namespace and kinds. But any writer can still deploy anything into `medapp`, and can weaken the namespace (finding 4). |
| 9 | No EF retries | **Fixed,** with the retry flaw in finding 5. |
| 11 | Scripts committed without the executable bit | **Fixed** (`100755`). |
| 13 | CI doesn't build what ships | **Mostly fixed:** `npm run build` and an image build, both green on this PR. CI's Node (24) still differs from the image's (`NODE_VERSION=22`), and `image` isn't required. |
| 14 | Supply chain | **Not fixed.** It's listed under "Not yet". |
| 15 | The admin login; any namespace can read the store | **The store is fixed:** `conditions` on both stores, with the schema checked. The admin login is documented, not fixed. |
| 16 | A teardown destroys the database | **Fixed.** A `CanNotDelete` lock on the server, and the README explains removing it and the vault name's 90 days. |
| 17 | Carriage returns from `az` | **Fixed** in both scripts and in the README's lock command. |
| 20 | Key Vault name too long | **Fixed** for length (exactly 24). The characters are still unchecked (finding 7). |
| 21 | Readiness | **Documented** under "Not yet". |
| 22 | Nothing spreads the copies; one Traefik pod | **Partly fixed.** The API now has a topology spread, as a preference. Traefik still has one pod, with no PDB and no maintenance window (finding 8). |
| 26 | Forwarded headers; client IPs | **Partly fixed.** `externalTrafficPolicy: Local` keeps clients' IPs. `KnownNetworks` is still unset. |
| 29 | Publish ordering | **Not fixed;** see finding 3. |
| 30 | `--revision` leaves the image pinned | **Fixed:** the parameters are removed with a JSON patch. |
| 31 | Docs that don't match the code | **Partly fixed:** the chart, Argo CD and Azure READMEs and `medapp-kind.yaml` are updated. Three are left (finding 9). |
| 12, 23, 25, 27, 28 | Node restarts, memory, reconnects, secrets in arguments, disposing the host | **Not in this PR.** Finding 12 is documented under "Not yet". |

From that review's "What I couldn't verify":
- **The AKS identity:** fixed.
- **Let's Encrypt through the redirect:** checked, and it works, as described above.
- **kubeconfig:** still open (finding 7).
- **What-if and secrets:** still unverified.

## What's worth keeping

- **Deploy commits built with plumbing:**
  - the exact tree of `main` plus one file
  - a temporary index
  - no merge, so nothing to conflict
  - pushes never forced, so a race fails instead of dropping a commit
  - idempotent
  - the deploy history kept as first parents
- **The Azure design:**
  - a user-assigned control-plane identity, with its subnet role granted first
  - Entra ID only, with Azure RBAC
  - Cilium enforcing the chart's policies
  - SQL only through a private endpoint, with a delete lock
  - Key Vault with RBAC and purge protection
  - OIDC federation limited to `main`, and to External Secrets' own service account
  - every role scoped to a single resource
  - no registry admin user
  - no secrets in the outputs
  - what-if and a prompt before every deploy
- **Copying the image to ACR,** by `imagetools create`, rather than rebuilding it.
- **The CI additions,** which would have caught the earlier Traefik bug:
  - cluster-service renders with exactly the values and `--set`s the scripts use, at the versions in `versions.sh`
  - the Bicep build and lint
  - the image build
- **A restricted AppProject,** even if it needs finding 4's changes, and ClusterSecretStores limited to `medapp`.
- **Careful scripts:**
  - every `kubectl` and `helm` call pinned to a context
  - `az` output stripped of carriage returns
  - the email escaped for `sed`
  - versions shared in one file
- **Honest docs:** "not deployed" said plainly, cost figures, the password restart procedure, the lock removal steps, and a long "Not yet".

## Verdict

**One high finding should be dealt with before merging:** finding 1.
- **It's suspected, not confirmed,** but the evidence is strong, and it would break the deploy on this PR's own merge.
- **The fix is about ten lines** in `deploy-to-env.sh`, tested in a scratch repository.
- **Do finding 2's job split at the same time,** so a kind failure can't hold back Azure.
- **If you merge as it stands,** be ready to push the first `env/kind` deploy by hand (the command in finding 1).

**Nothing is critical,** and the Bicep shows no first-run blocker I could find.

**The medium findings (2, 3 and 4) don't block,** but they should be fixed before Azure holds real data:
- the coupling of Azure to kind
- accidental rollbacks by re-run
- an environment branch that's still a way into the cluster

**The low findings** are hardening and documentation.

## Resolution

How each finding was dealt with in #52, after this review.

| # | Finding | Outcome |
|---|---|---|
| 1 | Deploy pushes refused after a workflow change | **Fixed.** `deploy-to-env.sh` keeps the environment branch's own `.github/workflows` in each deploy commit, with the branch's previous tip as the first parent, so a deploy never changes a workflow file. A first deploy keeps `main`'s, which changes nothing against its one parent. It uses plumbing (`ls-files` into `update-index --force-remove`) rather than `git rm --cached`, which refuses when the checkout isn't at the deployed commit, as when the script is run by hand. |
| 2 | Azure deploys depend on the kind deploy | **Fixed.** Publish has three jobs: `image` (contents read, packages write), then `deploy-kind` (contents write) and `azure` (contents write, id-token write), each needing only `image`. The workflow itself grants nothing. Both deploy jobs keep the `refs/heads/main` guard and check out all history. |
| 3 | Re-running an old Publish run moves an environment back | **Fixed.** Before building the commit, the script finds the commit of `main` the branch runs (its tip's second parent, or else the image tag in the values file, for a hand commit or an older branch). If the commit being deployed is a strict ancestor of it, it says so and exits 0. A revert on `main` is newer, so it still deploys. The comment in `publish.yml` now says this. |
| 4 | Write access to `env/*` is deploy access, and the project allowed any Namespace | **Partly fixed.** The project's `clusterResourceWhitelist` is empty, and the Applications no longer use `CreateNamespace` or `managedNamespaceMetadata`. `up.sh --argocd` and `bootstrap.sh` make the `medapp` namespace with its `restricted` labels before applying the Application, as plain `up.sh` already did. **Deferred:** limiting pushes to `env/*` to a deploy-only GitHub App (`docs/todo.md`, repository settings; explained in `deploy/argocd/README.md`, "Who can deploy"), and pinning actions by SHA (`docs/todo.md`, and "Not yet" in `deploy/azure/README.md`). |
| 5 | A retry inside `IdentityTransactions` can fail `SetRolesAsync` | **Documented, not fixed:** in "Not yet" in `deploy/azure/README.md`. |
| 6 | CI doesn't build the dev tool | **Fixed.** The `unit-tests` job builds `tools/MedApp.DevMcp` in Release first. `AGENTS.md`'s CI section says so. |
| 7 | First-run rough edges in the Azure scripts | **Fixed.** `main.bicep` checks `environmentName` (1 to 8 lower-case letters and digits) and fails the deployment with a clear message otherwise, using `fail()`, since Bicep has no pattern decorator. `deploy.sh` refuses the all-zero `adminGroupObjectId`. `bootstrap.sh` waits up to ten minutes for the cluster role to apply, converts only its own context with `kubelogin --context`, and says that `get-credentials` changes the current context. The README covers all of this, and how to start the first `env/azure` deploy by hand. |
| 8 | Open Azure risks not written down | **Documented** in "Not yet" in `deploy/azure/README.md`: the public API server, the unreserved ingress IP, and one Traefik pod with no disruption budget or maintenance window. |
| 9 | Docs that don't match | **Fixed:** the three slips from `flaws-review.md` finding 31 (`deploy/kind/traefik-values.yaml`, `secret.yaml`, `up.sh`); the status lines in `docs/reviews/README.md`, `azure.md`, `kubernetes.md` and `gitops.md`; the index's rows for `pr-51-ssr-security-fixes.md` and this review; and `overview.md`, whose #51 links resolve now that `main` is merged in. `overview.md` no longer says the image check is required. **Deferred:** making the `image` check required, a repository setting (`docs/todo.md`). |

How the fixes were checked:
- **`deploy-to-env.sh`,** in scratch repositories with a bare origin and a fresh clone per run, detached at the commit as in Actions. Commits on `main` changed, added and deleted files under `.github/workflows`. After each deploy, the diff against the first parent touched no workflow file, and the rest of the tree was `main`'s plus the values file. Re-runs changed nothing. First deploys, to `env/kind` and to `env/azure` after workflow changes, kept `main`'s workflows. Older commits were refused and the branch left alone, both with a second parent and with only a values file to go on (a hand commit, an old-style branch). A revert on `main` deployed. A run from a checkout at another commit worked. Against a copy of the real history, deploying this branch over `env/kind`'s tip (whose workflows differ by 129 lines) changed no workflow file.
- **Bicep 0.49.2:** `build`, `lint` and `build-params` pass. `bicep snapshot` evaluates the template locally: `prod` and `dev2` give the expected names, `Prod-1` and `prod_x` fail with the message, and an empty name fails at compile time (`@minLength(1)`).
- **Helm and YAML:** `helm lint --strict` passes with the defaults, kind's values and Azure's. Neither rendering has a cluster-scoped kind. Both workflows and the Argo CD manifests parse.
- **Scripts:** `bash -n` passes on every script.
- **.NET:** `dotnet build MedApp.sln -c Release` and `dotnet build tools/MedApp.DevMcp -c Release` succeed with no warnings, and `dotnet test MedApp.UnitTests -c Release` passes 747 tests. No C# changed in these fixes.
