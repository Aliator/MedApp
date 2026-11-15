# Choices made for you, 15 November 2026

You asked me to take the recommended option at every decision while you were out, and to list what I chose. Here's each one, with why. Anything you'd have chosen differently can be undone; the "To undo" notes say how.

## Shipping the pull requests

1. **#51's review: fixed the small findings in the PR, rather than carrying them.** The xhigh review found six low issues and nothing blocking. I fixed four:
   - the fallback dropping the HSTS header
   - a stream error crashing the Node page server
   - staff with only the Admin role not being able to change their own password
   - test constants

   I documented the other two: Node's environment allowlist isn't isolation, and the page server's files can be reached on a Windows host but not on Linux, where the image runs. Moving those files out of `wwwroot` is on the to-do list. *Why:* each fix was small and in the code the PR had just changed. ([pr-51 review](reviews/pr-51-ssr-security-fixes.md))
2. **#52's review: held the merge on its one high finding, and fixed it first.** GitHub refuses a push by the workflow's own token that changes workflow files, and every deploy commit carried `main`'s whole tree, so the first deploy after #52 would have failed. Each deploy commit now keeps the environment branch's own workflow files. *Proof:* the deploy after #52's merge succeeded, and its commit changes no workflow file. ([pr-52 review](reviews/pr-52-azure.md))
3. **#52's medium findings, fixed in the same PR:**
   - **Separate deploy jobs:** the kind and Azure deploys are separate jobs, so one failing doesn't stop the other.
   - **No accidental rollbacks:** re-running an old Publish run can't roll an environment back. It skips with a message instead of failing, because a deliberate rollback is a revert on `main`.
   - **The namespace:** `medapp` is created by the setup scripts rather than by Argo CD, so Argo CD's project has no permission over anything cluster-wide. *To undo:* restore `CreateNamespace` and `managedNamespaceMetadata` (which set the Pod Security labels) in the Applications, and `Namespace` in `deploy/argocd/project.yaml`.
4. **#52's low findings, also fixed:**
   - **`environmentName`:** the Bicep checks it is lower-case letters and digits, and fails with a clear message.
   - **The placeholder group:** `deploy.sh` refuses to run with the all-zero admin group still in place.
   - **Role delay:** `bootstrap.sh` waits for Azure's role assignment to take effect.
   - **The dev tool:** CI now builds it. Nothing built it before, which is how its break went unnoticed.
   - **Docs:** the doc corrections.
   - **Documented, not fixed:** a retry in `IdentityTransactions` that can fail a role change, and three open Azure risks (the public API server, the unreserved ingress IP, one Traefik pod). Both are in the Azure README's "Not yet".
5. **A new pull request for the default password (#53), which you didn't ask for.** The handbook found that every new account was created with `DefaultPassword1`, a password hard-coded in public source and shown in the UI. For a medical app, that blocks calling the build ready to deploy.
   - **What #53 does:** the server now generates each new account's first password, at least 16 characters, and shows it to the admin once.
   - **Forcing a change at first sign-in:** that needs a database migration, so it's on the to-do list rather than in this PR.
6. **#53's review: fixed one medium finding, documented the other, and fixed most lows.**
   - **Mediums:** fixed: screen readers are told about the password, with a copy button. Documented: accounts made before #53 may still have the old password, with a to-do item to find them.
   - **Lows:**
     - the password is cleared when the dialog closes
     - a double-click can't lose it
     - it can't end up in logs, and isn't cached
     - success always carries a password
     - the endpoint is tested as Admin-only
   - **Deferred:** making the dev tool's `create_user` all-or-nothing. ([pr-53 review](reviews/pr-53-generated-first-password.md))

## Repository settings (you approved changes earlier)

7. **Required checks on `main`:** `web`, `helm`, `unit-tests` and, once it existed on `main`, `image`, the container build. *To undo:* edit the "Protect" ruleset in the repository's settings.
8. **The environment branches (`env/*`)** can't be deleted or force-pushed. I couldn't also make the Publish workflow the only thing allowed to push to them, because GitHub only allows that for organisation-owned repositories. The alternative, a deploy-only GitHub App, is on the to-do list.

## Documents

9. **The handbook** ([architecture.md](architecture.md)) follows **arc42**, a widely used template for software architecture documents. It describes `main` as it is now, with every merged change. The roadmap is part of it, between the risks and the glossary, as you asked.
10. **The roadmap** was researched at max effort, as you asked, with its sources cited. I kept its order: licences and intellectual property first, because a buyer's due diligence checks those before anything else, and two of them need commercial licences: FluentAssertions 8 for any commercial use, and MediatR in production for any organisation above its free Community licence's limits.
11. **Review documents:** #51, #52 and #53 each have their own (`docs/reviews/pr-51…`, `pr-52…`, `pr-53…`), and the handbook pull request gets one too (`pr-54…`). #42 to #50, merged earlier the same day, are covered by the review notes in `docs/reviews/` instead.

## Things I deliberately didn't do

12. **Azure isn't deployed.** You chose earlier not to, because of the cost. Everything is ready: [deploy/azure/README.md](../deploy/azure/README.md) has the steps.
13. **The repository and its container image are still public.** The roadmap points out that a public `docs/reviews/flaws-review.md` lists the app's weaknesses, which a buyer would notice. I'd recommend making the repository private before showing it to anyone. I haven't done it, because it's outside what you asked, and two things read the image without signing in: the kind cluster, and the Publish workflow's copy to Azure. Making it private would need a pull secret on kind, and `packages: read` for the `azure` job.
14. **MediatR wasn't replaced or licensed.** It needs a licence in production: the free Community licence while the business is under its limits, a paid one above them. That's roadmap step 1, and it's your purchasing decision.

## How I checked the finished build

15. **Development:** I restarted the AppHost on `main` at `eef92dd`, after #53, with every change merged. At http://localhost:5173:
    - `admin` / `admin` signs in.
    - `/patients` comes back rendered on the server, with its rows.
    - A `//evil.example` path is refused with a 404.
    - The live notifications WebSocket connects without negotiating.
16. **The kind cluster, deployed by Argo CD from git.** I moved it from a local Helm install to Argo CD (`deploy/kind/up.sh --argocd`), so it runs exactly what `env/kind` says: the image Publish built from `eef92dd`, pulled from GHCR.
    - **The application:** Synced and Healthy, under the restricted `medapp` project, in a namespace with the `restricted` Pod Security label. Two API copies, SQL Server and Valkey are running.
    - **Pages:** at https://localhost:8443, `admin` signs in, and `/patients` is rendered on the server with `Cache-Control: no-store`. The `//evil.example` path gets a 404.
    - **New accounts:** creating one returns 200 with `no-store` and a 16-character generated password.
    - **Live notifications:** with that account, 8 of 8 arrived live across the two API copies.
    - *To switch back* to building locally: `kubectl --context kind-medapp -n argocd delete application medapp`, then `deploy/kind/up.sh`.
17. **The deploy pipeline:** the Publish runs after #51, #52 and #53 all succeeded. The one after #52 proved its high-finding fix: its deploy commit changed no workflow file.
18. **Azure:** checked as far as it can be without deploying. CI compiles and lints the Bicep and renders every chart with Azure's values, on every pull request.
