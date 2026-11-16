# MedApp threat model

**Status:** written 16 November 2026 for roadmap step 2 (`docs/architecture.md`), on branch `feature/security-baseline`: `main` up to #64 (`f083ec3`, merged in) plus that branch's changes. Updated on `feature/email-2fa` (16 November 2026) for email, password reset by link, and optional two-factor authentication (DF12, DF13, T-16 to T-19, T-35). It describes MedApp as built, not as planned: where a control is only planned, it says which roadmap step. Review it whenever a data flow, a trust boundary or a deployment changes, and at least once a year.

**Method:** STRIDE, applied to each element of the data flows below:
- **S**poofing: pretending to be someone or something else.
- **T**ampering: changing data or code without the right to.
- **R**epudiation: doing something and being able to deny it, because nothing records it.
- **I**nformation disclosure: data reaching someone who shouldn't see it.
- **D**enial of service: making MedApp unavailable.
- **E**levation of privilege: doing what your role doesn't allow.

Each threat has an ID (`T-…`), the controls in place, and what's left, with a rough rating (high, medium or low: how likely, times how bad for patients' data and care). The ASVS checklist (`docs/security/asvs.md`) lists the same controls requirement by requirement; the hazard log (`docs/clinical-safety/hazard-log.md`) covers the clinical side of the same risks.

## 1. What's being protected

| Asset | Why it matters |
|---|---|
| **Patient records** (name, date of birth, email) and **procedures** (titles, notes, stage notes, due dates) | Special category health data under UK GDPR. Disclosure harms patients; tampering can harm their care. |
| **The procedure audit log** | The record of who did what. Losing or changing it loses accountability (hazard H-18). |
| **Accounts:** usernames, password hashes, roles, the must-change-password flag | Whoever controls an account acts as that person. An admin account controls everyone's. |
| **Sessions** (`UserSessions` rows, and their IDs in the session cookie) | A session ID is as good as a password for up to 8 hours, or 24 if extended ("Stay signed in"). |
| **Generated passwords** (first passwords and resets) | Shown once to an admin, to pass on; work until changed. |
| **Secrets:** database and Valkey passwords, the first admin's password, Azure identities | Each opens a whole store. |
| **The build and deploy pipeline:** GitHub Actions, GHCR images, the `env/*` branches, Argo CD | Whoever controls them controls the code that runs. |

## 2. Who might attack it

| Actor | Can reach | Wants |
|---|---|---|
| **Someone on the internet** | The public URL (Traefik), the public repository and image | Data, or a foothold; or to stop the service |
| **A signed-in member of staff (role User)** | Every staff endpoint | To see or do more than their role allows; or to hide what they did |
| **A rogue or compromised admin** | Every endpoint | Everything; to cover their tracks |
| **Someone with a staff member's unlocked screen** | That browser's session | To act as that person (H-18) |
| **A malicious or compromised dependency** (npm, NuGet, a base image, a GitHub Action) | Wherever that code runs: the build, the browser, the API, Node | Secrets, data, or a backdoor in the image |
| **Someone with access to the cluster, the database or its backups** | The data at rest, sessions, secrets in pods | Bulk data |

## 3. The system and its trust boundaries

```
 Browser ──TLS──▶ Traefik (ingress) ──http──▶ API pod ─────────────────────────────┐
   ▲    ◀── WebSocket (SignalR) ──┘           │  Kestrel :8080 (app), :8081 (health) │
   │                                           │    │  loopback (127.0.0.1:8082)       │
   │                                           │    ▼                                  │
   │                                           │  pages container: Node page server   │
   │                                           └───────┬──────────────┬──────────────┘
   │                                                   │ SQL (1433)   │ RESP (6379)
   │                                                   ▼              ▼
   │                                            SQL Server /      Valkey (SignalR
   │                                            Azure SQL         backplane)
   │
 GitHub (repository, Actions) ──push image──▶ GHCR ──pull──▶ cluster nodes
         └──commit──▶ env/kind, env/azure ◀──watch── Argo CD ──apply──▶ cluster
 Key Vault / medapp-vault ──External Secrets──▶ Kubernetes Secrets ──env vars──▶ pods
 API pod ──SMTP (STARTTLS)──▶ mail server (Mailpit in kind, Azure Communication Services) ──▶ staff mailboxes
```

**Trust boundaries** (where data crosses from less trusted to more trusted):
- **B1 Internet to ingress:** everything from the browser is untrusted until the API has authenticated and validated it.
- **B2 Ingress to API:** the API trusts Traefik's `X-Forwarded-For` and `X-Forwarded-Proto` (`ASPNETCORE_FORWARDEDHEADERS_ENABLED`), which only holds because the network policy lets nothing else reach the API's port.
- **B3 API to Node, inside one pod:** in Kubernetes, the page server is a container of its own in the API's pod (`pages`), as its own user, with none of the API's secrets or settings. It gets page requests with the user's cookie on loopback, and calls the API back there with that cookie, and no key, so it can act as anyone who opens a page (T-23). The containers share the pod's network, but not processes, users or environment. Run on its own (`docker run`), the API starts Node inside its own container, with a per-process key: same user, same container, not a strong boundary (S17).
- **B4 API to data stores:** SQL Server and Valkey, behind network policies and passwords.
- **B5 Build to runtime:** what GitHub Actions builds and records on `env/*` is what Argo CD runs.
- **B6 Secret store to pods.**

## 4. Data flows

| ID | Flow | What crosses | Protection in transit |
|---|---|---|---|
| DF1 | Browser to API: sign in (`POST api/auth/login`) | Username and password; back, the session cookie | TLS to Traefik; plain http inside the cluster |
| DF2 | Browser to API: page requests | The session cookie; back, the server-rendered page with the user's data | As DF1 |
| DF3 | API to Node to API: server rendering | The page request (path, query, cookie, Accept headers, the CSP nonce); Node's API calls with the cookie (and, only when the API started Node, the SSR key) | Loopback only, inside the pod |
| DF4 | Browser to API: `/api` calls | JSON commands and queries; back, JSON with patient data | As DF1 |
| DF5 | Browser to API: live notifications (`/api/notifications/hub`) | A WebSocket authenticated by the session cookie; notifications (procedure titles) | As DF1 |
| DF6 | API to SQL Server | Every read and write | Azure SQL: encrypted. kind: plain, `TrustServerCertificate=True` |
| DF7 | API to Valkey to API | Notifications between API copies | Plain, with a password (S14) |
| DF8 | GitHub Actions to GHCR and `env/*` | The image; a commit naming its tag | GitHub's own TLS and tokens |
| DF9 | Argo CD to the cluster | Manifests from `env/*` | In the cluster |
| DF10 | Secret store to pods | Database, Valkey and first admin passwords | External Secrets; workload identity on Azure |
| DF11 | Migrations Job to SQL Server | The schema, the first admin | As DF6 |
| DF12 | API to the mail server to a staff mailbox | Reset and confirmation links (a token in the fragment), and security notices: the username and what changed, never clinical text or a password | SMTP with STARTTLS to Azure Communication Services; plain to Mailpit inside kind or the dev machine. Onward delivery to the mailbox is the mail providers' |
| DF13 | Browser to API: second sign-in step (`POST api/auth/login/two-factor`) | The challenge from the password step and a code; back, the session cookie | As DF1 |

## 5. Threats

### 5.1 The browser and the ingress (DF1, DF2, DF4, DF5)

| ID | STRIDE | Threat | In place | Left, and rating |
|---|---|---|---|---|
| T-01 | S | **Password guessing** against one account, or spraying common passwords across many. | Identity lockout: 5 wrong passwords lock an account for 5 minutes. **New:** sign-in throttling per client address (30 a minute, IPv6 by /64) and per username from anywhere (5 a minute), a 429 with `Retry-After`, each refusal logged with the client's address (`LoginThrottling`, `ThrottleLoginByUsernameAttribute`). A name with characters no account's name has (which SQL Server would otherwise match to a real one, such as a byte order mark in front) is unknown without a lookup, so it gets no allowance of its own; a name over 256 characters, or a body over 16 KB, is refused before anything counts it. **New:** the 10,000 most common passwords are refused when a password is set (`CommonPasswordValidator`). | Counters are per API copy, in memory, so two copies allow twice as many. No MFA (step 4). Existing weak passwords stay until changed. **Medium.** |
| T-02 | S | **Account enumeration:** a 423 says an account exists and is locked. | Same 401 for an unknown user and a wrong password. | The 423 still confirms the account (step 2, later; kept now so the login answers don't change). **Low.** |
| T-03 | S | **Session theft** from the browser (script, a shared machine). | HttpOnly cookie, so script can't read it. **New:** a nonce-based Content Security Policy on pages limits what an injected script could run. Sign-out revokes the session. **New:** an admin can sign a user out everywhere. | The login response body repeats the session ID (S5). No idle timeout (H-18; deliberately not added in this change). **Medium.** |
| T-04 | S, T | **Session fixation or cookie injection** from a sibling subdomain or over plain http. | **New:** outside Development the cookie is `__Host-medapp.session`: Secure, `Path=/`, no `Domain`, so only this host over https can set it. HSTS. A new session ID at every sign-in. | Development keeps `medapp.session` over plain localhost. **Low.** |
| T-05 | T | **Cross-site request forgery.** | `SameSite=Lax`, JSON bodies, no CORS policy (no other origin may read responses). | No antiforgery tokens: an old browser without SameSite support, or a same-site attacker (a sibling subdomain), could forge requests. **Low.** |
| T-06 | T, E | **Cross-site scripting** in rendered content (notes are Markdown). | React escapes text; Astryx `Markdown` never renders raw HTML and turns unsafe links into text (#41). **New:** the page CSP allows scripts only from the app's origin or with the response's nonce; no `unsafe-inline` or `unsafe-eval` for scripts, `object-src 'none'`, `base-uri 'none'`. | Styles allow `unsafe-inline` (server-rendered style attributes), so CSS injection isn't blocked. **Low.** |
| T-07 | T | **Clickjacking.** | **New:** `X-Frame-Options: DENY` and `frame-ancestors 'none'` everywhere. | None. |
| T-08 | I | **MIME sniffing** of an API response as script or HTML. | **New:** `X-Content-Type-Options: nosniff` on every response, and `default-src 'none'` on every `/api` response. | None. |
| T-09 | I | **Personal data left in caches** (browser back button, a proxy). | Rendered pages, and generated-password responses, were `no-store`. **New:** every `/api` response is `no-store`. | Static files are cacheable, which is intended. **Low.** |
| T-10 | I | **Data leaking in the Referer header** (patient IDs in paths). | **New:** `Referrer-Policy: strict-origin-when-cross-origin`. The app links nowhere else. | None. |
| T-11 | I | **Eavesdropping** on the way in. | TLS at Traefik; HSTS outside Development (30 days). | HSTS isn't preloaded and doesn't cover subdomains. Inside the cluster the hop to the API is plain http. **Low.** |
| T-12 | D | **Flooding** the API or the sign-in endpoint. | Sign-in throttling (above); resource limits on pods; two API copies. | No general rate limit per session (step 2, later), and none at the ingress. **Medium** for availability, low for data. |
| T-13 | E | **Using an endpoint above your role.** | Role attributes on every controller action; `PasswordChangeRequired` holds anyone with a generated password to changing it. `AuthorisationTests` (#65) checks every admin-only and staff endpoint for each role and signed out, through the real pipeline, including the new sign-out-everywhere endpoint. | No fallback policy, so a new endpoint without an attribute would be open until the matrix gains it. `GET api/patients` and `GET api/auth/users` return everything (planned removal, step 2). **Low.** |
| T-14 | E | **Keeping access after losing it:** a session that outlives a password change, a reset or a role change. | **New:** changing your own password ends your other sessions, and an admin's reset ends all of them, each in the same transaction as the change; an admin can sign a user out everywhere; deleting a user makes their sessions fail. Roles are re-read on every request, so a role change applies to the next request without ending the session. | An open live-notifications WebSocket isn't closed when its session ends: it keeps receiving notifications (procedure titles) until the page reloads or the connection drops (step 2, later). **Low.** |
| T-16 | S | **Taking over an account through "Forgot your password?"** (a guessed or stolen link, a link to the wrong place, a link that works for ever). | The link goes only to the account's confirmed address; its token is Identity's, sealed with the data protection keys, bound to the account's security stamp (so it works once) and valid 30 minutes. It's in the URL's fragment, so it stays out of server logs and the Referer header. Links are built from `Email:LinkBaseUrl`, never the request's Host header. Two-factor stays on after a reset. Every session ends, and the owner is told. Putting an address on an account takes its password (a session alone can't), the confirmed address is told when a new one is asked for, and an address is confirmed only by the account it's for, signed in (a stranger who gets a mistyped or shared address's link can't confirm it); an admin can remove an account's address, recorded with their name. At most 3 address changes per account in 15 minutes. | Whoever controls the mailbox controls the password (as with any email reset); two-factor is what stops them. A team mailbox shared by several accounts lets anyone who reads it reset each of them, so each account needs a personal address. **Low** with two-factor, **medium** without. |
| T-17 | I | **Learning which accounts and addresses exist** from the reset form, or flooding someone with emails. | The same 202 and message whatever matched; emails go through a background queue, so the time taken barely differs; requests throttled per address (30 a minute) and per username or address (3 in 15 minutes); a request is recorded without what was typed, by nobody, with the account as its target. Each copy sends at most `Email:MaxPerHour` emails an hour (8 on Azure, under the managed domain's 10), keeping a fifth for security notices, which go out before any link. | Each API copy counts on its own. Someone asking for resets in bulk can still use up an hour's links, though not the notices. **Low.** |
| T-18 | S | **Guessing or replaying an authenticator code** once the password is known, including many sent at the same moment. | A 5-minute, single-use challenge, stored as a hash, whose 5 attempts are each reserved in one SQL statement before the code is checked, so codes sent together get no more checks than codes sent one by one (tested with 30 at once); wrong codes count towards the account's lockout in one statement too (none is lost to a concurrency check, none ends in a 500), the lockout from codes doubles each time (to a day at most), and the account holder is emailed when codes lock it ("they know your password"); each account gets at most 10 code checks in 15 minutes from anywhere (429), and the code endpoints are limited per address as sign-in is; a right password doesn't clear the count until the code is right too; each code is accepted once (the step of the last one is kept); new recovery codes and turning two-factor off need the password and a code. | Two-factor is optional, so most accounts still have only a password (T-01). Recovery codes are stored in clear (ASVS 6.5.2). Each copy of the API counts the per-account and per-address limits on its own, so two copies allow twice as many; the lockout itself is in the database and shared. **Low** for accounts that use it. |
| T-19 | E | **An admin without two-factor** while admins must use it, or someone with an admin's password enrolling their own phone first. | `TwoFactorSetupRequired` holds them to setting it up from their next request, and it can't be turned off while required; turning the requirement on needs the admin's own two-factor first. Turning it on needs the password again. Every other admin with a confirmed address is emailed when an admin turns two-factor on, turns it off or has it reset, and Admin Settings names the admins who haven't set it up. An admin's reset of someone's two-factor ends their sessions, is recorded with the admin's name, and emails the owner. | Enrolment under the hold trusts the password alone: whoever has it and signs in first enrols their phone, and the real admin is locked out until another admin resets it (the other admins are told). The requirement covers admins only, and only when turned on. Admin reset relies on the admin checking who's asking (no written procedure yet). **Medium.** |
| T-15 | R | **Denying an action:** "I didn't change that". | Procedures are audited with the user's ID and name, in the same save (`ProcedureChange`). | Patients, accounts, roles, sign-ins and these new session actions aren't audited (S2, step 7). **High** for accountability. |

### 5.2 The API and the page server (DF3)

| ID | STRIDE | Threat | In place | Left, and rating |
|---|---|---|---|---|
| T-20 | S | **A request pretending to be Node** to skip the https redirect. | Node's calls must come from loopback with a random 256-bit key, compared in constant time (`SsrServer.IsFromRenderer`). The key only skips the redirect; the user's session is still needed. The page server in its own container has no key at all, and needs none: the API never redirects a request it received on a loopback address, which only something in the pod can make. | None. |
| T-21 | I | **The API as an open proxy** sending the user's cookie elsewhere (fixed in #51). | Only the path and query reach Node; any `//` path is a 404. | None. |
| T-22 | I | **Node's own files** (the page server, the build's manifest) served as static files. | They're in `/app/ssr/`, outside the web root, so no static file path reaches them on any host; `wwwroot` holds only the app's own files. `PageServerFiles` still refuses `/ssr` and `/.vite`, as defence in depth. | None (S18 resolved). |
| T-23 | I, E | **Code in Node** (a compromised npm package, a rendering flaw) reading the API's secrets, or acting as its visitors. | In Kubernetes, Node runs in a container of its own (`pages`), as another user (1655), with none of the API's secrets or settings and no service links; it can't see the API's process. Checked on kind: no process it can see has a connection string in its environment. Run on its own, the API gives Node an environment allowlist without connection strings. Against a compromised package: the lockfile (`npm ci`), `npm audit` in CI, Dependabot and dependency review. | What Node still holds: every page request brings the visitor's session cookie (as good as a password for up to 24 hours), so compromised code could call the API on loopback as anyone who opens a page while it's compromised, admins included (make an account, read or export every patient), and those calls are logged from `127.0.0.1` like every render's. The pod's network policy allows DNS, so data could leave as DNS lookups, and Node can reach the database and Valkey, though without credentials. Where the API has a Key Vault key and an external mail server (Azure), the pod also reaches those by name: a `CiliumNetworkPolicy` allows only the mail relay on 587 and the vault and Microsoft Entra ID on 443 (needs AKS's Advanced Container Networking Services); elsewhere a plain NetworkPolicy, which can't name hosts, would allow any public address on those ports, so the chart turns the by-name policy on only with Azure's values. Even by name, the page server shares the pod, so it can reach the same shared services (another tenant's vault behind the same addresses, the relay with someone else's credentials). Run on its own (`docker run`), Node shares the API's user and could read `/proc` (S17). **Medium.** |
| T-24 | T | **A forged CSP nonce** sent to Node. | Node's pages listen only on loopback, inside the pod (its health listener answers nothing but `/alive`); it accepts a nonce only in base64 form, and React and the router escape it when writing it. A browser in development that sends its own nonce only affects its own page. | None. |
| T-25 | I | **One user's data in another's rendered page.** | A new router and query cache for every request; the cookie travels in `AsyncLocalStorage`. | None. |
| T-26 | D | **A stalled render** holding requests. | `Ssr:RenderTimeout` for the start of a page; a failed render falls back to the client-rendered app. In Kubernetes, the page server has its own memory limit and liveness probe, and is restarted if it exits or stops answering. | A page that stalls part-way isn't timed out; run on its own, Node isn't restarted if it exits. **Low.** |

### 5.3 The data stores (DF6, DF7, DF11)

| ID | STRIDE | Threat | In place | Left, and rating |
|---|---|---|---|---|
| T-30 | T, I | **SQL injection.** | EF Core with parameters everywhere; search filters in LINQ. | None found. |
| T-31 | I | **Someone with the database** (a backup, a read replica) replaying live sessions. | Sessions expire after 8 hours (24 at most if extended); revoked ones are refused. | Session IDs are stored as they are, not hashed (S5; step 2, later, needs a migration). **Medium.** |
| T-32 | I | **Password hashes** taken from the database. | Identity's PBKDF2 hashes (salted, iterated). **New:** common passwords refused, so offline guessing of new passwords is harder. | Existing weak passwords stay until changed. **Low.** |
| T-33 | E | **The app's database login** doing more than the app needs. | Network policies: only the API and the migrations Job reach SQL Server. | On Azure the app signs in as the server admin (S3, step 5). **High** before real data. |
| T-34 | I, T | **Valkey traffic** read or forged inside the cluster. | Only the API reaches Valkey; it needs a password. | Plain text (S14). **Low.** |
| T-35 | I, S | **The data protection keys** (which seal reset and confirmation tokens) taken with the database, then used to forge tokens. | On Azure they're wrapped with a Key Vault key the API's own identity can use (`DataProtection:KeyVaultKeyUri`), so a copy of the database alone can't unwrap them. Every pod shares them, so a link from one pod opens on another. | Anywhere without a Key Vault key (kind, development, a customer's own cluster) they're stored unwrapped in the database, beside the security stamps, so a copy of the database could mint a reset token for any account: acceptable for local, synthetic data only. The API warns at startup outside Development when they aren't wrapped; wrapping with a certificate from a Secret, for clusters without Azure, is on `docs/todo.md`. Authenticator keys are stored in clear in `AspNetUserTokens`, as Identity stores them. **Low** on Azure; **medium** elsewhere with real data. |

### 5.4 The pipeline and the platform (DF8 to DF10)

| ID | STRIDE | Threat | In place | Left, and rating |
|---|---|---|---|---|
| T-40 | T | **A hijacked GitHub Action** (as happened to a popular scanner's tags in March 2026) stealing the token that pushes images and moves `env/*`. | **New:** every third-party action is pinned to a full commit SHA, with Dependabot proposing updates. Each job has only the permissions it needs; Publish's top-level permissions are none. | `pull-requests: write` is still granted to the whole PR workflow (only the comment job needs it). **Low.** |
| T-41 | T | **A vulnerable or malicious dependency** (npm, NuGet). | **New:** Dependabot (actions, NuGet, npm, Docker); dependency review on pull requests (high and critical fail); `npm audit --audit-level=high` in CI; NuGet audit NU1903 and NU1904 fail the build; CodeQL on C#, TypeScript and the workflows. Lock files; exact versions for Astryx and Lexical. | Dependency review, and Dependabot's alerts and security updates, need the repository's dependency graph, which is on (`docs/todo.md`, Repository settings). Moderate advisories only warn. No licence allow-list yet (step 1). **Medium.** |
| T-42 | T | **A vulnerable base image or OS package.** | **New:** Trivy scans the built image on every pull request and fails on a critical vulnerability that has a fix; results go to code scanning. Trivy itself runs from an image pinned by digest. | Base images are pinned by tag, not digest, and Dependabot can't read the Dockerfile's `ARG` tags. Highs only report. **Medium.** |
| T-43 | T, R | **An image that didn't come from this repository's build** deployed in its place. | **New:** each published image has a build provenance attestation and an SBOM attestation (Sigstore, through GitHub), pushed to GHCR beside it. `env/*` is moved only by Publish. | Nothing checks the attestation before deploy (Argo CD or an admission policy; step 2, later). The copy to Azure's registry doesn't carry the attestations. **Medium.** |
| T-44 | I | **Secrets in the wrong place.** | Never in git; External Secrets from Key Vault or `medapp-vault`; none in the page server's container. | Two secrets on process command lines (S13). **Low.** |
| T-45 | E | **Something on `env/*` that creates cluster-wide resources.** | Argo CD's `medapp` project allows only the chart's kinds, in the `medapp` namespace. | Anyone with push rights to `env/*` could still change MedApp's own resources (to-do: a deploy-only GitHub App). **Low.** |
| T-46 | S | **Traffic from inside the cluster pretending to be Traefik** (spoofed `X-Forwarded-For`, to dodge sign-in throttling or fake the scheme). | Network policies let only the ingress namespace reach the API's port; Traefik replaces untrusted forwarded headers, and ASP.NET Core takes only the last address. | The API trusts forwarded headers from any source (S12). **Low.** |

## 6. What this change added, in one place

- Security headers and Content Security Policies (`MedApp.API/Middleware/SecurityHeaders.cs`): T-03, T-06 to T-10.
- The `__Host-` session cookie (`SessionCookieService`): T-04.
- Sign-in throttling (`MedApp.API/Common/RateLimiting`): T-01, T-12.
- Ending sessions on a password change or reset, and signing a user out everywhere: T-03, T-14.
- The common-password deny list (`CommonPasswordValidator`): T-01, T-32.
- The pipeline: SHA-pinned actions, Dependabot, CodeQL, dependency review, npm and NuGet audits, Trivy, SBOM and provenance attestations: T-40 to T-43.
- `feature/email-2fa`: optional two-factor authentication with recovery codes and an admin requirement (T-01, T-18, T-19); "Forgot your password?" by a single-use emailed link (T-16, T-17); data protection keys shared in the database and wrapped by Key Vault on Azure (T-35); security notices by email (DF12). After its review: code attempts reserved and counted atomically, per-account limits and a growing lockout (T-18); the password again for changing the address and for two-factor changes, a notice when a change is asked for, an admin action to remove an address, and confirmation only by the signed-in account (T-16); admins told of each other's two-factor (T-19); the API pod's egress by name on Azure (T-23).

## 7. The biggest risks left

1. **Accountability (T-15):** patients, accounts and sign-ins aren't audited (step 7).
2. **The database login on Azure (T-33)** is the server admin (step 5).
3. **MFA optional (T-01, T-03, T-18):** an authenticator app is available and can be required for admins, but nobody else must use it; no passkeys or SSO (step 4).
4. **Sessions (T-03, T-31, T-14):** no idle timeout, IDs not hashed at rest, the ID repeated in the login body, and live connections not closed on revocation (step 2, later).
5. **Code in Node acting as its visitors (T-23):** the page server is in a container of its own, without the API's secrets, but it sees every visitor's session, can call the API on loopback, and can reach DNS. On Azure the pod also reaches the mail relay, the Key Vault and Microsoft Entra ID, allowed by name only (a Cilium FQDN policy), which are shared services an attacker's own credentials could use to carry cookies out. Containing it fully needs a page server with no route out: a Deployment of its own whose policy allows only the API.
6. **No fallback authorisation policy (T-13)** and the two return-everything endpoints (step 2, later).
7. **Deploys don't verify provenance (T-43),** and images aren't pinned by digest (T-42).
