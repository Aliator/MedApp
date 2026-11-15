# MedApp's technology, in plain terms

A short guide to how MedApp is built and run, and why, for someone who wants the picture without reading every review. Each section says what a piece is, what it does here, and why it was chosen, then links to the detailed review ([README.md](README.md) lists them all). Technical words are explained the first time they appear.

## The app itself

MedApp keeps track of patients and the procedures done for them. It has two halves:
- **The API (.NET 10, C#)** holds the rules and the data. Everything the app does, from signing in to finishing a stage of a procedure, is a request to it. It stores data in **SQL Server**, a database, through **Entity Framework Core**, which maps C# objects to database tables.
- **The web app (React 19, TypeScript)** is what people see and click. It runs in the browser and asks the API for data.

Two cross-cutting parts:
- **Sign-in** is a cookie: after you sign in, the browser carries a session ID that the API checks on every request.
- **Live notifications** use **SignalR**, a library that keeps a connection open between the browser and the API, so being assigned a procedure shows up at once, with no reload.

## Server-side rendering: pages arrive ready to read

*Detail: [server-side-rendering.md](server-side-rendering.md)*

- **What it is:** normally a React app arrives as an empty page plus code, and the browser builds the page itself, fetching its data, before you see anything. **Server-side rendering (SSR)** builds the finished page on the server instead, with its data already in it, and sends that. The browser then "hydrates" it: it attaches the app's behaviour to the HTML that's already there, so it becomes interactive without being rebuilt.
- **Why:** pages appear faster, screen readers get real content straight away, and slow devices do less work.
- **How:** React renders in **Node.js** (JavaScript run outside a browser). Rather than run Node as a separate service, the API starts a small Node page server itself and passes page requests to it. If Node is missing, slow or fails, the API sends the plain browser-built app instead, so a broken renderer degrades the app rather than taking it down.
- **What the reviews changed** ([#51](pr-51-ssr-security-fixes.md)):
  - **An open proxy:** a crafted address could make the API fetch another website using the visitor's cookie. It's now blocked twice over.
  - **A broken fallback:** a hidden retry layer stopped the fallback working under failure. It's removed.
  - **Pages kept in caches:** server-rendered pages hold patient data, so they're now never cached.
  - **Node had the database password:** Node now gets only the settings it needs.

## The container image: one box that runs anywhere

*Detail: [container-image.md](container-image.md)*

- **What it is:** a **container image** is a sealed package holding a program and everything it needs to run (the .NET runtime, Node, the built web app), so it runs the same on a laptop, in CI and in the cloud. **Docker** builds it from a recipe, `MedApp.API/Dockerfile`.
- **What's in it:** the API, the built React app, Node for SSR, and the **migrations bundle**. Migrations are the steps that bring a database's tables up to date with the code, and the bundle is a small program that runs them. Keeping them in the same image means the database schema always matches the code that's running.
- **Why one image:** one thing to build, version and deploy, rather than a separate web server, renderer and migration tool.
- **Security:** the image runs as a non-root user, and the files it doesn't need aren't copied in.

## Kubernetes: running it reliably

*Detail: [kubernetes.md](kubernetes.md)*

**What it is:** **Kubernetes** runs containers across a group of machines (a **cluster**). You describe what should be running ("two copies of the API, one database"), and it keeps it that way: restarting what crashes, replacing what's updated, and spreading load. It's the industry standard for this, and Azure, AWS and Google all offer it.

Five pieces build on it:
- **kind (Kubernetes in Docker):** a whole Kubernetes cluster on one laptop, inside Docker. `deploy/kind/up.sh` builds it, so the production setup can be tried locally, for free.
- **Helm:** a packaging tool for Kubernetes. MedApp's **chart** (`deploy/helm/medapp`) is a template of everything MedApp needs:
  - the API, SQL Server and Valkey (below)
  - the network rules
  - the migrations job
  - the web address (**ingress**)

  It's filled in with different settings for each place it runs (`values.yaml` files): kind and Azure use the same chart with different values. So what's tested locally is what runs in production.
- **Traefik:** the **ingress controller**, the front door. It receives web traffic, handles HTTPS, and passes requests to the API. It was chosen because the long-standard alternative, ingress-nginx, has been retired.
- **cert-manager:** gets and renews **TLS certificates**, the padlock in the browser. Locally it signs its own certificate (the browser warns); on Azure it gets real ones from **Let's Encrypt**, a free, trusted issuer.
- **External Secrets Operator:** copies passwords from a secure **secret store** into the cluster, so no password is ever written into git. Locally the "store" is a stand-in inside the cluster; on Azure it's **Key Vault**.

**How it's locked down** (because this is medical data):
- **Network policies:** each part may only talk to what it needs. Only the front door reaches the API, and only the API reaches the database and Valkey. Everything else is refused.
- **The "restricted" Pod Security Standard:** Kubernetes refuses any container that runs as the all-powerful root user or asks for extra privileges.
- **Read-only filesystems:** a compromised container can't change its own files.
- **Health checks on a private port:** only Kubernetes can ask whether the API is alive; the public can't.
- **A deliberate first admin:** on a brand-new database, the first administrator account is made from a password in the secret store. It's never made again once anyone exists, and there are no default passwords.

## Several copies of the API: the SignalR backplane

*Detail: [signalr-backplane.md](signalr-backplane.md)*

- **The problem:** running two copies of the API keeps the app up if one fails, and spreads the load. But each browser's live connection is to one copy, and the copy that sends a notification may be the other one, so some notifications went missing. Measured: 4 of 8 arrived.
- **The fix:** a **backplane**, a shared message relay that every copy sends through, so each notification reaches whichever copy holds the connection. Measured after: 8 of 8.
- **Why Valkey:** the backplane is **Valkey**, the open-source continuation of Redis (Redis itself is no longer open-source licensed). It runs inside the cluster and stores nothing.
- **Connections:** browsers connect straight to a WebSocket, skipping SignalR's usual "negotiate" step. That step and the connection could otherwise reach different copies, which breaks the connection.

## GitOps: git decides what runs

*Detail: [gitops.md](gitops.md)*

- **What it is:** **GitOps** means the cluster runs exactly what a git branch says, and changes only when git does. Nobody deploys from their own machine, and every deploy is a recorded commit.
- **The flow:**
  1. A pull request is merged to `main`, after CI (the automated build and tests) passes. It's now required.
  2. The **Publish** workflow builds the image and stores it in a **container registry**: GHCR (GitHub's registry) for the local cluster, and Azure Container Registry for Azure.
  3. It records the deploy on an **environment branch** (`env/kind`, `env/azure`): `main` as of that commit, plus a small file naming the image to run.
  4. **Argo CD**, a tool running inside the cluster, watches that branch and applies whatever it says. It also undoes changes made by hand.
- **Why a separate branch:** `main` only accepts pull requests, so the workflow can't commit to it, and Argo CD needs the chart and the image tag from one place.
- **Rolling back:** revert the change on `main`, through a pull request; the next deploy carries it out.

## Azure: where it will live

*Detail: [azure.md](azure.md)*

**Bicep** (`deploy/azure/main.bicep`) is Microsoft's language for describing cloud resources as code. One file creates everything, and a "what-if" step shows the changes before any are made. It creates:
- **AKS (Azure Kubernetes Service):** Microsoft runs the cluster's control centre; MedApp runs on two or more virtual machines. Sign-in is through your organisation's accounts (Entra ID), with no shared master password, and **Cilium** enforces the network rules.
- **Azure SQL:** a managed database (backups and patching are Microsoft's job), reachable only from inside MedApp's private network, and locked against accidental deletion. It's the "serverless" kind, which pauses when idle to save money, so the app now retries briefly while it wakes.
- **Key Vault:** the secret store for the passwords.
- **Azure Container Registry:** where AKS gets the image.
- **Managed identities:** Azure's way for programs to prove who they are without passwords. External Secrets uses one, which can only read the vault. The Publish workflow uses another, which can only push images, and only from the `main` branch.

**Status:** written and checked, but **not deployed**. CI compiles and lints the Bicep, and renders every chart with Azure's settings, on every pull request. Deploying costs roughly £150 to £200 a month (see [azure.md](azure.md) and `deploy/azure/README.md`), so it waits for you to choose to. It's two commands, plus pointing your domain at the address it prints.

## Running it

| Where | How | What you get |
|---|---|---|
| **Development** | `dotnet run --project MedApp.AppHost` (with Docker running) | The app at http://localhost:5173, with live reloading, a database in Docker, and the Aspire dashboard (a local view of logs and health). Sign in as `admin` / `admin`. |
| **Local Kubernetes** | `deploy/kind/up.sh --demo-data kind-demo` | The production setup on your laptop at http://localhost:8080 and https://localhost:8443, with two API copies, Valkey, network rules and secrets. Add `--argocd` to have Argo CD deploy from git instead. |
| **Azure** | `deploy/azure/deploy.sh`, then `bootstrap.sh` | The real thing. See `deploy/azure/README.md` first: it costs money. |

## How changes are made and checked

- **Three stages:** each change is planned at high reasoning effort, written at a medium-high one, then reviewed at high effort by a separate reviewer that wasn't told the author's conclusions.
- **A document per merge:** every merge gets a review document (`pr-<number>-….md` here).
- **Blocking:** a serious finding stops the merge until it's fixed.
- **Required checks:** CI (build, tests, test coverage, chart checks, the Bicep and the container image) must pass before anything merges.
- **Protected branches:** `main` and the environment branches can't be deleted or rewritten.

The reviews so far:
- **The first pass:** a maximum-effort review of everything from SSR to Azure ([flaws-review.md](flaws-review.md)) found 31 issues: 4 high, 14 medium and 13 low.
- **Fixed:** the high ones and most of the mediums, in [#51](pr-51-ssr-security-fixes.md) and the Azure pull request.
- **Still open:** what's left is listed as "not yet" in `deploy/azure/README.md` and in `docs/todo.md`. The main items:
  - a passwordless database sign-in for the app, and a least-privilege database user
  - private network access to the registry and the vault
  - monitoring and alerts
  - a readiness check that includes the database
