# OWASP ASVS 5.0 Level 2 checklist

**Status:** written 16 November 2026 for roadmap step 2 (`docs/architecture.md`, "Step 2: Close the remaining review findings and set an ASVS Level 2 baseline"). It describes branch `feature/security-baseline` at commit `c8b3d9a`: `main` at `ea827ef`, plus the branch's six commits (security headers and a nonce-based CSP, the `__Host-` session cookie, sign-in throttling, ending sessions when a password or roles change, the common-password list, and the supply chain checks in CI). `main` has since gained the governance pack (#61, `docs/governance/`). Where that pack meets a requirement, the row says "on `main` since #61" and counts it, because this branch will take `main` before it merges. The branch then merged `main` up to #64 (`f083ec3`), which also brought the session expiry warning and "Stay signed in" (#63) and the patient timeline (#64); the session rows (7.1.1, 7.3.2) were updated for #63, and #64's endpoint (`GET api/patients/{id}/timeline`, staff only) wasn't checked against every row again. **Updated for file attachments (#85),** after that branch's review: V5 File Handling filled in from the code (8 met, malware scanning planned), and rows 1.1.1, 1.3.4 and 1.5.1 corrected; the summary counts follow.

## What this is

The OWASP Application Security Verification Standard (ASVS) is a list of security requirements for web applications, in three levels. ASVS 5.0 says Level 2 is what "most applications should be striving to achieve"; the previous version put applications that handle health data at Level 2, and those holding sensitive medical data at Level 3. The roadmap aims for Level 2, plus the Level 3 controls that matter most here ([Beyond Level 2](#beyond-level-2)).

This checklist takes every Level 1 and Level 2 requirement in ASVS 5.0.0 (253 of its 345: 70 at Level 1 and 183 more at Level 2) and says whether MedApp meets it, with the evidence. The requirement texts and levels come from the standard's own files, at the `v5.0.0` tag of [OWASP/ASVS](https://github.com/OWASP/ASVS/tree/v5.0.0/5.0/en). Each chapter links to its file there.

It sits beside the threat model (`docs/security/threat-model.md`), which looks at the same controls from the attacker's side: what could go wrong, and which control stops it.

## How to read it

- **ID:** the requirement's number in ASVS 5.0.0, exactly as the standard gives it (chapter, section, requirement).
- **Requirement (paraphrased):** a few words to say what it's about. Read the standard for the full text before relying on a row.
- **L:** the requirement's level. Level 2 includes everything at Level 1.
- **Status:** exactly one of:
  - **Met:** in place, with evidence.
  - **Partly met:** something real is in place, and the row says what's missing. Where a roadmap step covers the rest, the row names it.
  - **Planned:** essentially nothing is in place yet, and a roadmap step will do it. "Not yet named in its scope" means it falls under step 2's aim of closing every Level 2 gap, but the step's scope doesn't list it yet: those need adding.
  - **Accepted risk:** a gap left on purpose, with the reason. The owner is the Product owner. Both rows are proposals by this review until the Product owner signs them off.
  - **Not applicable:** MedApp has nothing the requirement could apply to (no OAuth, no WebRTC), with when it will start to apply, if ever.
- **Evidence or reason:** paths from the repository root, with the class, member or setting that does the work. "Risk S5" and the like refer to the risk table in `docs/architecture.md` 11.1; "Step 4" and the like to the roadmap in the same document; "T-01" to the threat model.

A few terms used throughout:
- **CSP** (Content Security Policy): a response header that tells the browser what a page may load and run. A **nonce** is a random value, new for each response, that marks the page's own inline scripts as allowed.
- **HSTS** (HTTP Strict Transport Security): a header that tells browsers to use only https for the site, for a set time.
- **CSRF** (cross-site request forgery): another site making the user's browser send a request to MedApp, with the user's session cookie.
- **CSPRNG:** a cryptographically secure random number generator.

## How it was checked

- **Against the code,** not the documentation: the API, Application and Infrastructure projects, the React app and its page server, the Helm chart, the Azure Bicep and setup, the Dockerfile and the CI workflows, at `c8b3d9a`. Where the handbook and the code disagree, the code wins, and [Where the handbook was out of date](#where-the-handbook-was-out-of-date) says what was.
- **Mostly by reading.** No scan or penetration test was run for this checklist, and nothing has been deployed to Azure to look at. Since #65, integration tests run the real pipeline for many rows (the headers and policies, the cookie's flags, throttling, session ending and the authorisation matrix: `SecurityHeadersTests`, `SignInTests`, `LoginThrottlingTests`, `SessionEndingTests`, `AuthorisationTests`), and those rows cite them. Rows about Traefik's TLS settings and request framing rest on those products' defaults, and say so.
- **The browser controls were also run once,** separately from this checklist: the production image, against a throwaway SQL Server, loaded in Chromium with Playwright. Eleven server-rendered pages (nine app pages, then a patient's page and its procedures after #63 and #64 were merged in) hydrated with no CSP violations, every inline script carried the nonce, the notifications WebSocket connected, the cookie was `__Host-medapp.session` (Secure, HttpOnly, `Path=/`), a common password was refused, a password change and "Sign out everywhere" ended the right sessions, and the sixth sign-in attempt at one username got a 429 with `Retry-After`.
- **Honestly, rather than generously.** Where a requirement is only partly met, it says "Partly met", even when the missing part is small.

## Summary

Of the 253 requirements, 67 don't apply to MedApp today: 33 because it has no OAuth, OIDC or other identity provider (V10 and 6.8), 1 because it has no phone factor, 7 each for WebRTC and self-contained tokens, and 19 for things it doesn't use, such as LDAP, XML and GraphQL. Of the 186 that apply, **111 are met (60%)**, 56 partly met, 17 planned and 2 accepted as risks. (`feature/email-2fa-codes` added two-factor codes by email, which brought the two out-of-band rows, 6.6.2 and 6.6.3, into scope, both met, and made 6.4.3 partly met: for those accounts a reset link and a sign-in code reach the same mailbox.) (`feature/email-2fa` added optional two-factor authentication and a reset link by email, which brought six MFA rows into scope and met five of them; after its review, 16.3.1 and 16.3.3, which the activity log of sign-ins and security events covers, moved from planned to partly met.) At Level 1 alone, 39 of the 60 that apply are met, and the one planned is removing the password composition rules (6.2.5). File handling (V5) applies since file attachments (#85): 8 of its 9 rows are met, and malware scanning is planned.

The strongest chapters are encoding and sanitisation (V1), the browser protections this branch added (V3) and configuration (V13). The weakest are authentication (V6: MFA optional rather than required, composition rules, no breached-password check), session management (V7: a GUID token, no idle timeout) and security logging (V16: sign-ins and refusals aren't recorded). The roadmap already plans most of what's missing; [Gaps, in priority order](#gaps-in-priority-order) lists the ones that matter most, and the ones the roadmap doesn't name yet.

| Chapter | Met | Partly met | Planned | Accepted risk | Not applicable | Total |
|---|---:|---:|---:|---:|---:|---:|
| [V1 Encoding and Sanitization](#v1-encoding-and-sanitization) | 17 | 1 | 0 | 0 | 9 | 27 |
| [V2 Validation and Business Logic](#v2-validation-and-business-logic) | 4 | 6 | 0 | 0 | 1 | 11 |
| [V3 Web Frontend Security](#v3-web-frontend-security) | 14 | 4 | 0 | 0 | 1 | 19 |
| [V4 API and Web Service](#v4-api-and-web-service) | 3 | 3 | 0 | 1 | 3 | 10 |
| [V5 File Handling](#v5-file-handling) | 8 | 0 | 1 | 0 | 0 | 9 |
| [V6 Authentication](#v6-authentication) | 18 | 8 | 4 | 0 | 5 | 35 |
| [V7 Session Management](#v7-session-management) | 8 | 5 | 3 | 0 | 2 | 18 |
| [V8 Authorization](#v8-authorization) | 3 | 2 | 1 | 0 | 1 | 7 |
| [V9 Self-contained Tokens](#v9-self-contained-tokens) | 0 | 0 | 0 | 0 | 7 | 7 |
| [V10 OAuth and OIDC](#v10-oauth-and-oidc) | 0 | 0 | 0 | 0 | 29 | 29 |
| [V11 Cryptography](#v11-cryptography) | 8 | 3 | 2 | 0 | 1 | 14 |
| [V12 Secure Communication](#v12-secure-communication) | 2 | 5 | 0 | 1 | 1 | 9 |
| [V13 Configuration](#v13-configuration) | 9 | 2 | 2 | 0 | 0 | 13 |
| [V14 Data Protection](#v14-data-protection) | 3 | 5 | 1 | 0 | 0 | 9 |
| [V15 Secure Coding and Architecture](#v15-secure-coding-and-architecture) | 7 | 6 | 0 | 0 | 0 | 13 |
| [V16 Security Logging and Error Handling](#v16-security-logging-and-error-handling) | 7 | 6 | 3 | 0 | 0 | 16 |
| [V17 WebRTC](#v17-webrtc) | 0 | 0 | 0 | 0 | 7 | 7 |
| **All chapters** | **111** | **56** | **17** | **2** | **67** | **253** |
| Of which Level 1 | 39 | 20 | 1 | 0 | 10 | 70 |
| Of which Level 2 only | 72 | 36 | 16 | 2 | 57 | 183 |

## V1 Encoding and Sanitization

[ASVS chapter](https://github.com/OWASP/ASVS/blob/v5.0.0/5.0/en/0x10-V1-Encoding-and-Sanitization.md). 27 requirements at Levels 1 and 2: 17 met, 1 partly met, 9 not applicable.

Injection is handled by the frameworks: EF Core parameterises every query, React escapes every text node, and `System.Text.Json` writes every JSON body. Most of the chapter's interpreters (LDAP, XPath, LaTeX, JNDI, SMTP) aren't used at all.

| ID | Requirement (paraphrased) | L | Status | Evidence or reason |
|---|---|---|---|---|
| 1.1.1 | Input decoded to a canonical form once, before validation | 2 | Met | ASP.NET Core decodes the path, query and JSON body once, before model binding and FluentValidation. One input is decoded in app code, also once and before validation: an upload's file name, which travels percent-encoded in the `X-File-Name` header (Kestrel refuses headers that aren't ASCII), is percent-decoded once in `AttachmentResponseHeaders.DecodeFileName`, then cleaned and validated (`AttachmentFileName`, `UploadAttachmentValidator`). Nothing else decodes again (searched for `UrlDecode`, `HtmlDecode` and `Unescape`). |
| 1.1.2 | Output encoded as the last step, or by the interpreter | 2 | Met | React escapes text as it renders, `System.Text.Json` encodes as it writes, and EF Core sends values as SQL parameters. Nothing is stored pre-encoded. |
| 1.2.1 | Output encoding fits its context (HTML, attributes, headers) | 1 | Met | React's escaping; no `dangerouslySetInnerHTML` or `innerHTML` anywhere in `MedApp.Web/src`. Response header values are constants or set through the framework. |
| 1.2.2 | URLs built from untrusted data are encoded; only safe protocols | 1 | Met | API paths are built by `openapi-fetch` from typed path parameters, which it encodes (`MedApp.Web/src/api/client.ts`). Links with unsafe protocols in notes become plain text (Astryx `Markdown`, #41, `src/components/shared/RichText.tsx`). |
| 1.2.3 | JavaScript and JSON built with encoding | 1 | Met | JSON only through `System.Text.Json`, which escapes `<`, `>` and `&` by default, and through the router's own serialiser for the page's hydration data (`src/router.tsx`). No script is built from strings. |
| 1.2.4 | Database queries parameterised | 1 | Met | EF Core LINQ throughout `MedApp.Infrastructure/Data/Repositories`; no `FromSql`, `ExecuteSqlRaw` or `SqlQueryRaw` outside migrations. |
| 1.2.5 | No OS command injection | 1 | Met | The only process the API starts is Node (and in Kubernetes, not even that), with a fixed argument list and no request data (`SsrServer.StartInfo`, `MedApp.API/ServerRendering/SsrServer.cs`). |
| 1.2.6 | No LDAP injection | 2 | Not applicable | No LDAP. |
| 1.2.7 | No XPath injection | 2 | Not applicable | No XML or XPath. |
| 1.2.8 | LaTeX processors locked down | 2 | Not applicable | No LaTeX. |
| 1.2.9 | Regular expression metacharacters escaped | 2 | Not applicable | No regular expression is built from input. The only one in the backend is fixed (`ProcedureWorkflow.WordBoundary`). |
| 1.3.1 | HTML from rich text editors sanitised | 1 | Met | The rich text editor stores Markdown, not HTML (`src/components/forms/RichTextField.tsx`), and it's shown with Astryx `Markdown`, which never renders raw HTML (#41, `RichText.tsx`). |
| 1.3.2 | No `eval()` or dynamic code execution | 1 | Met | None in `MedApp.Web/src` (searched for `eval(` and `new Function`), and the page CSP has no `'unsafe-eval'` (`SecurityHeaders.PagePolicy`). |
| 1.3.3 | Data sanitised before it reaches a dangerous context | 2 | Met | The CSP nonce header must match a base64 pattern before the page server uses it (`cspNonce` in `MedApp.Web/server/server.mjs`). Request paths sent to Node must stay on Node's host (`SsrPages.Forward`). |
| 1.3.4 | User-supplied SVG sanitised | 2 | Not applicable | Users can't supply SVG: attachments refuse SVG, HTML and anything starting with `<`, found from the contents, not the name (`FileTypeSniffer`). |
| 1.3.5 | User-supplied Markdown, CSS or similar sanitised | 2 | Met | Notes are Markdown, rendered without raw HTML, with unsafe links turned into text (#41, `RichText.tsx`). |
| 1.3.6 | Protection against server-side request forgery (SSRF) | 2 | Met | The only request the server builds from a request is the page forward. Its scheme, host and port are Node's, only the path and query come from the request, and the result is checked before sending (`SsrPages.Forward`). Paths containing `//` are refused (`PageServerFiles.IsBlocked`). No feature takes a URL from users. |
| 1.3.7 | No templates built from untrusted input | 2 | Met | No server-side template engine: pages are React components, rendered with data, never compiled from it. |
| 1.3.8 | JNDI queries sanitised | 2 | Not applicable | Not Java. |
| 1.3.9 | Content sanitised before it goes to memcache | 2 | Not applicable | No memcache. Valkey is used only by SignalR's backplane, which builds its own commands. |
| 1.3.10 | Format strings sanitised | 2 | Met | Format strings and log message templates are constants; user data only fills their placeholders. |
| 1.3.11 | Input sanitised before it goes to mail systems | 2 | Not applicable | MedApp sends no email. An email provider arrives with Step 4's password reset. |
| 1.4.1 | Memory-safe strings and buffers | 2 | Met | Managed code only (.NET and JavaScript); no `unsafe` blocks in the solution. |
| 1.4.2 | Range checks prevent integer overflow | 2 | **Partly met** | Page sizes are capped at 100, but `Page` has no upper bound, and `Paging.Skip` computes `(page - 1) * pageSize` unchecked (`MedApp.Application/Common/Paging/Paging.cs`). A page of 2,147,483,647 overflows to a negative offset and the query fails with a 500. No data leaks, but cap `Page` in the `Search…Validator` classes. |
| 1.4.3 | Memory and resources released | 2 | Met | Garbage-collected runtimes; disposable resources are released with `using` (for example `SsrPages.Forward`, `ThrottleLoginByUsernameAttribute`). |
| 1.5.1 | XML parsers protected against external entities (XXE) | 1 | Not applicable | MedApp parses no XML. The Word check reads a `.docx`'s `[Content_Types].xml` and `.rels` parts as text, at most 64 KB each, and searches them with string and pattern matching, never an XML parser (`FileTypeSniffer`). |
| 1.5.2 | Safe deserialisation of untrusted data | 2 | Met | `System.Text.Json` into fixed DTO types (`MedApp.Contracts`), with no type names taken from the input. Settings are stored as an opaque JSON string of at most 64 KB (`SaveMySettingsValidator`). |

## V2 Validation and Business Logic

[ASVS chapter](https://github.com/OWASP/ASVS/blob/v5.0.0/5.0/en/0x11-V2-Validation-and-Business-Logic.md). 11 requirements at Levels 1 and 2: 4 met, 6 partly met, 1 not applicable.

Validation is strong in the code: every command with free input has a FluentValidation validator, run by the MediatR pipeline, and the procedure rules live in one place (`ProcedureWorkflow`, `ProcedureChange`). What's thin is the writing down, and limits on how often anything but sign-in can be called.

| ID | Requirement (paraphrased) | L | Status | Evidence or reason |
|---|---|---|---|---|
| 2.1.1 | Documented input validation rules | 1 | **Partly met** | The rules are the validators beside each command (`MedApp.Application/**/*Validator.cs`), and `docs/architecture.md` 8.3 describes the approach. There's no written list of the rules, and the Swagger document doesn't carry them. |
| 2.1.2 | Documented checks that combined data items are consistent | 2 | **Partly met** | The procedure workflow's rules (stage order, what can change in which status) are written down in `docs/procedures-ui-plan.md`. Other combinations aren't documented. |
| 2.1.3 | Documented business limits, per user and overall | 2 | **Partly met** | Some are written down: page size 100 (`AGENTS.md`, Lists), and sign-in throttling (`LoginThrottlingOptions`). Others exist only in code (notes 4,000 characters, settings 64 KB). There are no per-user limits other than sign-in. |
| 2.2.1 | All input validated against expected values or structure | 1 | **Partly met** | Every command with free input has a validator (`ValidationBehavior`), and IDs are checked by route constraints (`{id:guid}`). Gaps: `ResetUserPasswordCommand` and eight ID-only commands have no validator; the patient email has no length limit in its validator, so one over the column's 256 characters fails as a 500 (`CreatePatientValidator`, `PatientConfiguration`); and `Page` has no upper bound (1.4.2). |
| 2.2.2 | Validation enforced at a trusted service layer | 1 | Met | In the API's MediatR pipeline (`MedApp.Application/Common/Behaviors/ValidationBehavior.cs`). The React app repeats no rules (`docs/architecture.md` 8.3). |
| 2.2.3 | Related data items checked together | 2 | Met | For example, a date of birth can't be in the future (`CreatePatientValidator`), and stage and status combinations go through `ProcedureWorkflow` and `ProcedureChange` (`MedApp.Application/Procedures/Common`). |
| 2.3.1 | Business flows run in order, without skipping steps | 1 | Met | Stages are worked strictly in order (`ProcedureWorkflow`). A user with a generated password can do nothing but change it (`PasswordChangeRequired`). |
| 2.3.2 | Business limits implemented as documented | 2 | Met | Page size (`Paging.MaxPageSize`), sign-in throttling (`LoginThrottling`), and notes and settings sizes (the validators). |
| 2.3.3 | Business operations succeed or fail as a whole | 2 | **Partly met** | A procedure change and its audit entries are saved in one `SaveChangesAsync` (`SaveAsync(auditEntries)`); the first admin, a change of roles, and a password change or reset with the sessions it ends each run in one transaction (`IdentityTransactions`; `UpdateUserPasswordHandler`, `ResetUserPasswordHandler`). Not yet as a whole: creating a patient and its audit entry (there's none until step 7), and other account changes made in several saves. |
| 2.3.4 | Limited resources can't be double-booked | 2 | Not applicable | MedApp books nothing in limited supply. (There's no optimistic concurrency either: when two people edit the same notes at once, the last save wins.) |
| 2.4.1 | Anti-automation against excessive calls | 2 | **Partly met** | Sign-in is throttled (`LoginThrottling`), and lists return at most 100 rows. Nothing limits the rate of other calls, per session or per address. Step 2 plans a per-session limit. |

## V3 Web Frontend Security

[ASVS chapter](https://github.com/OWASP/ASVS/blob/v5.0.0/5.0/en/0x12-V3-Web-Frontend-Security.md). 19 requirements at Levels 1 and 2: 14 met, 4 partly met, 1 not applicable.

This branch added most of this chapter: the security headers, the nonce-based Content Security Policy (CSP, the browser's allowlist of what a page may load and run) and the `__Host-` cookie. What's left is HSTS's settings, the session ID in the login response, and cross-site request forgery (CSRF) defences that rest on `SameSite` alone.

| ID | Requirement (paraphrased) | L | Status | Evidence or reason |
|---|---|---|---|---|
| 3.2.1 | Browsers can't render responses in the wrong context | 1 | Met | `/api` responses carry `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'` and `nosniff` (`SecurityHeaders.ApiPolicy`), so an API response opened as a page runs nothing. No user files are served. |
| 3.2.2 | Text shown with safe rendering functions | 1 | Met | React renders text as text; no `dangerouslySetInnerHTML` or `innerHTML` in `MedApp.Web/src`. |
| 3.3.1 | Cookies are `Secure`, with a `__Host-` or `__Secure-` name | 1 | Met | The only cookie, `__Host-medapp.session`, is `Secure` outside Development (`SessionCookieService.BuildCookieOptions`, `SessionAuthenticationDefaults.HostCookieName`). In Development it's `medapp.session`, on plain http. |
| 3.3.2 | `SameSite` set to fit each cookie's purpose | 2 | Met | `SameSite=Lax` (`SessionCookieService`): the app is same-origin, and Lax keeps the cookie off cross-site POSTs while links into the app still work. |
| 3.3.3 | `__Host-` prefix unless a cookie is shared with other hosts | 2 | Met | `__Host-medapp.session`, with `Path=/` and no `Domain`, outside Development. |
| 3.3.4 | Session cookies are `HttpOnly`, and the token travels only in `Set-Cookie` | 2 | **Partly met** | `HttpOnly`, yes. But the login response's body repeats the session ID (`LoginResponse`, `AuthController.Login`), where script can read it (risk S5). Drop it from the body. |
| 3.4.1 | HSTS on every response, for at least a year, and for subdomains at Level 2 | 1 | **Partly met** | Sent outside Development (`UseHsts` in `ApplicationBuilderExtensions.UseApi`), but with ASP.NET Core's defaults: 30 days, and no `includeSubDomains`. Configure `AddHsts` with 365 days and subdomains. |
| 3.4.2 | CORS `Access-Control-Allow-Origin` fixed or allowlisted | 1 | Met | No CORS policy at all, so no `Access-Control-Allow-Origin` header (`ServiceExtensions.AddApi`). |
| 3.4.3 | A CSP with `object-src 'none'`, `base-uri 'none'`, and an allowlist or nonces | 2 | Met | Pages outside Development get a new nonce per request for scripts, `object-src 'none'` and `base-uri 'none'` (`SecurityHeaders.PagePolicy`); `/api` gets `default-src 'none'`. Caveats: styles allow `'unsafe-inline'`; Development sends only `frame-ancestors`; and the `SecurityHeaders:PageCspReportOnly` switch can turn enforcement off (off by default). |
| 3.4.4 | `X-Content-Type-Options: nosniff` on every response | 2 | Met | `SecurityHeaders` runs first in the pipeline and sets it on every response. |
| 3.4.5 | A referrer policy | 2 | Met | `Referrer-Policy: strict-origin-when-cross-origin` (`SecurityHeaders`): other sites see only the origin, never paths or queries. |
| 3.4.6 | CSP `frame-ancestors` on every response | 2 | Met | `frame-ancestors 'none'` in the page, API and Development policies, and `X-Frame-Options: DENY` as well (`SecurityHeaders`). |
| 3.5.1 | CSRF defence: anti-forgery tokens or a non-safelisted header, unless relying on preflight | 1 | **Partly met** | No anti-forgery token or custom header. The defence is `SameSite=Lax` plus JSON bodies (3.5.2). That doesn't cover a sibling subdomain of the same site, which counts as same-site and so gets the cookie sent, posting to a body-less endpoint. Requiring a custom header (for example `X-Requested-With`) on unsafe methods would close it. |
| 3.5.2 | If relying on CORS preflight, no sensitive call works without one | 1 | **Partly met** | Endpoints with a body accept only `application/json` (`[ApiController]` refuses anything else with a 415), so a cross-origin call needs a preflight, which no CORS policy allows. Body-less unsafe endpoints need no preflight, and rest on `SameSite` alone: for example `POST api/auth/logout`, `POST api/notifications/read`, `POST …/stages/{stageId}/finish` and `DELETE api/patients/{id}`. |
| 3.5.3 | Sensitive actions use unsafe methods, not GET | 1 | Met | Every change is a POST, PUT, PATCH or DELETE; GET endpoints only read (`MedApp.API/Controllers`). |
| 3.5.4 | Separate applications on separate hostnames | 2 | Met | One app per host. Argo CD's UI isn't exposed (port-forward only, `deploy/azure/README.md`), and Swagger runs only in Development. |
| 3.5.5 | `postMessage` messages checked for origin and syntax | 2 | Not applicable | The app neither sends nor listens for `postMessage`. |
| 3.7.1 | Only supported client-side technologies | 2 | Met | React 19 in current browsers; no plugins (`object-src 'none'`). |
| 3.7.2 | Automatic redirects to other hosts only to an allowlist | 2 | Met | The app redirects only to its own routes (the guards in `src/auth/auth.ts`) and takes no return URL. Node's redirects pass through unchanged, but come from the same route code. |

## V4 API and Web Service

[ASVS chapter](https://github.com/OWASP/ASVS/blob/v5.0.0/5.0/en/0x13-V4-API-and-Web-Service.md). 10 requirements at Levels 1 and 2: 3 met, 3 partly met, 1 accepted risk, 3 not applicable.

A plain JSON API behind Traefik, with one WebSocket (the notifications hub). The gaps are small: charsets on problem responses, trust in forwarded headers, and no `Origin` check on the WebSocket.

| ID | Requirement (paraphrased) | L | Status | Evidence or reason |
|---|---|---|---|---|
| 4.1.1 | Every body has a matching Content-Type with a charset | 1 | **Partly met** | JSON from MVC carries `charset=utf-8`. But the middleware's problem responses (`ExceptionHandling`, `PasswordChangeRequired`, the 429 in `LoginThrottling`) send `application/problem+json` with no charset, and static files go out without one. |
| 4.1.2 | Only user-facing endpoints redirect HTTP to HTTPS | 2 | **Accepted risk** | `UseHttpsRedirection` (and Traefik's redirect on Azure) redirect `/api` too. The only API client is the app itself, on the same https origin and under HSTS, so a mistaken plain-http API call is unlikely and would be seen. Owner: Product owner. |
| 4.1.3 | Headers set by intermediaries can't be overridden by users | 2 | **Partly met** | The API trusts `X-Forwarded-For` and `X-Forwarded-Proto` from any caller (`ASPNETCORE_FORWARDEDHEADERS_ENABLED`, `deploy/helm/medapp/templates/api.yaml`). It's safe only because network policies let nothing but Traefik reach the API, and Traefik, trusting no one's forwarded headers by default (no `forwardedHeaders.trustedIPs` in `deploy/*/traefik-values.yaml`), replaces the client's own (risk S12). Step 2 plans trusting only the ingress. |
| 4.2.1 | Every component frames HTTP messages the same way (no request smuggling) | 2 | Met | Framing is done by Traefik and Kestrel, both standard HTTP servers; MedApp parses no HTTP itself. Not tested here. |
| 4.3.1 | GraphQL query cost limited | 2 | Not applicable | No GraphQL. |
| 4.3.2 | GraphQL introspection off in production | 2 | Not applicable | No GraphQL. |
| 4.4.1 | WebSockets over TLS (WSS) | 1 | Met | The notifications hub is on the page's own origin, so it's `wss:` behind Traefik's TLS (`src/app/LiveNotifications.tsx`, `NotificationsHub.Path`). Plain inside the cluster (12.3.3). |
| 4.4.2 | WebSocket handshake's `Origin` checked against an allowlist | 2 | **Partly met** | SignalR doesn't check `Origin`, and nothing else does. A cross-site page can't use the session, because browsers don't send a `SameSite=Lax` cookie on a cross-site WebSocket handshake, but a same-site page could. Check `Origin` on `/api/notifications/hub`. |
| 4.4.3 | Dedicated WebSocket tokens meet the session rules, if the standard session can't be used | 2 | Met | The hub uses the standard session cookie and the `Staff` policy (`NotificationsHub`). |
| 4.4.4 | Dedicated WebSocket tokens obtained through the authenticated HTTPS session | 2 | Not applicable | No dedicated WebSocket tokens. |

## V5 File Handling

[ASVS chapter](https://github.com/OWASP/ASVS/blob/v5.0.0/5.0/en/0x14-V5-File-Handling.md). 9 requirements at Levels 1 and 2: 8 met, 1 planned.

Since file attachments (#85), staff upload scans, photos, results and letters to a patient, a procedure or a stage, and download them again (`MedApp.API/Controllers/AttachmentsController.cs`). The bytes go to blob storage under keys made from IDs; the details go to SQL. Checked against the branch's code after the fixes from its review (`docs/reviews/pr-85-file-attachments.md`, findings 1, 4, 8 and 9). The subject access export is JSON and a printable page, not a file upload, so it adds nothing here.

| ID | Requirement (paraphrased) | L | Status | Evidence or reason |
|---|---|---|---|---|
| 5.1.1 | Documented file types, extensions and sizes for each upload | 2 | Met | One upload feature, with its types and limit in one place (`AttachmentLimits`, `AttachmentFileTypes`): PDF, PNG, JPEG, HEIC/HEIF, Word `.docx` and plain UTF-8 text, at most 20 MiB. The upload area states them (`Attachments.tsx`, with the limits every list returns), and `AGENTS.md` (Attachments) documents them. |
| 5.2.1 | Files accepted only at sizes the app can process | 1 | Met | A request is at most 20 MiB, checked against the declared length before reading and while streaming (`RequestSizeLimit`, `UploadAttachmentHandler`); the body is streamed to a temporary file, never held whole in memory (`TempFileUploadStaging`). What the app does with a file is bounded too: no thumbnail when the source image is over 24 megapixels or 10,000 pixels on a side, read from its header before decoding (`SkiaThumbnailGenerator`); a `.docx` whose ZIP lists more than 5,000 entries refused before it's opened (`FileTypeSniffer.MaxZipEntries`); and at most two thumbnails or Word inspections at a time per API copy (a `SemaphoreSlim(2)`). |
| 5.2.2 | File extensions and contents checked | 1 | Met | The type comes from the contents (magic bytes, the HEIF `ftyp` box, a `.docx`'s ZIP directory and content types, UTF-8 text with no control characters), never the extension or the browser's type, and the name's extension must match it (`FileTypeSniffer.Identify`). SVG, HTML, macro-enabled Word and anything else are refused, and so is the commonest active content in the allowed types: Word files with external relationships, embedded objects or ActiveX, and PDFs containing `/JavaScript`, `/JS`, `/Launch`, `/EmbeddedFile` or `/RichMedia`. |
| 5.2.3 | Compressed files checked against size and file-count limits | 2 | Met | The only archive accepted is a `.docx`. Its entry count is read from the ZIP's end-of-central-directory record (ZIP64's when present) and over 5,000 is refused before the archive is opened; nothing is unpacked but `[Content_Types].xml` and the `.rels` parts, each read to at most 64 KB (`FileTypeSniffer`). The file is stored as uploaded, never extracted. |
| 5.3.1 | Uploaded files never executed as server code | 1 | Met | Files go to blob storage (Azure Blob Storage, or Azurite), never under `wwwroot` or anywhere the server runs or serves files from; they come back only through the download endpoint, as attachments (`BlobFileStore`, `AttachmentResponseHeaders`). |
| 5.3.2 | File paths built from trusted data, not user file names | 1 | Met | The app opens only fixed files (`index.html`, the page server's script). Static files go through ASP.NET Core's `PhysicalFileProvider`, which refuses paths outside `wwwroot`; the page server and the build's manifest are in `/app/ssr/`, outside it (`MedApp.API/Dockerfile`). `/ssr` and `/.vite` are still blocked as defence in depth (`PageServerFiles.IsBlocked`). Attachments' storage keys are made from the attachment's ID only (`AttachmentStorageKeys`), and staged uploads get random names in the temporary folder (`TempFileUploadStaging`); the user's file name is stored only as data. |
| 5.4.1 | User file names ignored, and `Content-Disposition` names set | 2 | Met | The uploaded name is cleaned (no folders, control or direction characters, at most 255 characters) and made to end in the extension of the type found (`AttachmentFileName`); every download sets `Content-Disposition: attachment` with that name (`AttachmentResponseHeaders.Apply`). |
| 5.4.2 | Served file names encoded | 2 | Met | `Content-Disposition` carries an ASCII fallback (`filename`, without quotes or backslashes) and the exact name as RFC 5987 percent-encoded UTF-8 (`filename*`), so nothing in a name can break out of the header (`AttachmentResponseHeaders.ContentDisposition`). |
| 5.4.3 | Untrusted files scanned for malware | 2 | **Planned** | No scanner yet: every file is stored and shown as "Not scanned" (`NoMalwareScanner`, behind `IMalwareScanner`). Microsoft Defender for Storage on Azure, and a scanner such as ClamAV elsewhere, are planned in step 9 and brought forward to before go-live by the DPIA (risk 22) and the hazard log (H-25) (`docs/todo.md`). Until then, files are only ever downloaded, never shown or run in the app. |

## V6 Authentication

[ASVS chapter](https://github.com/OWASP/ASVS/blob/v5.0.0/5.0/en/0x15-V6-Authentication.md). 35 requirements at Levels 1 and 2: 17 met, 7 partly met, 4 planned, 7 not applicable.

Still the weakest chapter: two-factor authentication (an authenticator app, TOTP, or, weaker, codes by email) exists but is optional, and required only for admins when an admin turns that on (the app only); passkeys and single sign-on are still Step 4; composition rules remain (Step 2 removes them), and there's no breached-password check. The basics are sound: Identity's hashing, a common-password deny list, throttling and lockout (which wrong codes count towards), generated first passwords that must be changed, and a single-use, 30-minute reset link to a confirmed address.

| ID | Requirement (paraphrased) | L | Status | Evidence or reason |
|---|---|---|---|---|
| 6.1.1 | Documented defences against credential stuffing and brute force, including against malicious lockout | 1 | **Partly met** | Throttling and lockout are documented (`LoginThrottlingOptions`, `docs/agents/auth.md` with sizing guidance; `docs/security/threat-model.md`, T-01; `docs/architecture.md` 8.1). Wrong two-factor codes count towards the same lockout, and with two-factor on a right password no longer clears the count, so codes can't be guessed by re-entering the password. Each challenge's five attempts are reserved in one SQL statement before a code is checked, and failures are counted in one statement, so codes sent at the same moment get no more checks than codes sent one by one (`SignInChallengeRepository.TryReserveAttemptAsync`, `AccountLockoutRepository`, `ParallelCodeTests`, 30 at once); each account gets at most 10 code checks in 15 minutes from any address (`AccountAttemptLimiter`); a lockout caused by codes doubles each time, to a day at most, and emails the account holder (`IdentityAuthenticationService`, `LoginTwoFactorHandler`, `TwoFactorTests`). Reset requests are throttled per address and per username or address (`ThrottleResetRequestsAttribute`). The controls don't prevent malicious lockout: anyone can lock a known username (such as `admin`, risk S16) for 5 minutes with 5 attempts, since the per-username limit equals the lockout count. |
| 6.1.2 | Documented list of context-specific words to refuse in passwords | 2 | **Planned** | None. Step 2 (its aim of closing every Level 2 gap; not yet named in its scope). For example: MedApp, the customer's name, NHS, and ward and role names. |
| 6.1.3 | Every authentication pathway documented, with its controls | 2 | Met | Three, in `docs/agents/auth.md` and `docs/agents/email-2fa.md`: username and password, then the session cookie; for an account with two-factor, the password gives a short-lived challenge and the code (or a recovery code) the session (`LoginHandler`, `LoginTwoFactorHandler`); and a forgotten password, reset by a single-use link to the confirmed address, which ends every session and doesn't bypass two-factor (`ForgotPasswordHandler`, `ResetPasswordHandler`). The page server's key isn't authentication: it only skips the https redirect for Node's calls on loopback when the API started Node, and those calls still need the user's session (`SsrServer.IsFromRenderer`). The page server in its own container has no key. |
| 6.2.1 | Passwords at least 8 characters (15 recommended) | 1 | Met | 8 (`Identity:PasswordRequiredLength`, `MedApp.API/appsettings.json`). Step 2 plans a longer minimum. |
| 6.2.2 | Users can change their password | 1 | Met | `PUT api/auth/users/{username}/update-password` (`AuthController.UpdateUserPassword`) and the change-password page. |
| 6.2.3 | Changing a password needs the current one and the new one | 1 | Met | `IdentityUserService.UpdateUserPasswordAsync` checks the current password first. |
| 6.2.4 | New passwords checked against at least the 3,000 most common | 1 | Met | The NCSC's 10,000 most common, ignoring case, whenever a password is set (`CommonPasswordValidator`, `MedApp.Infrastructure/Common/Identity/Passwords/common-passwords.txt`). |
| 6.2.5 | No composition rules (any characters, no required mix) | 1 | **Planned** | Upper case, lower case and a digit are still required (`Identity` section of `MedApp.API/appsettings.json`). Step 2 removes them. |
| 6.2.6 | Password fields use `type=password` | 1 | Met | `src/pages/login/LoginPage.tsx`, `src/components/forms/PasswordChangeFields.tsx`, and the delete-user dialog in `src/pages/users/UsersPage.tsx`. |
| 6.2.7 | Paste and password managers allowed | 1 | Met | Nothing blocks paste, and the fields carry `autoComplete` values `username`, `current-password` and `new-password`. |
| 6.2.8 | Password checked exactly as entered | 1 | Met | No trimming, truncation or change of case between the form and Identity's hasher (`AuthController.Login`, `LoginHandler`, `IdentityAuthenticationService`). |
| 6.2.9 | Passwords of at least 64 characters allowed | 2 | Met | No maximum length in Identity or the validators. |
| 6.2.10 | No periodic password rotation | 2 | Met | Passwords never expire. A generated password must be replaced once, at first sign-in, which isn't rotation. |
| 6.2.11 | The context-specific word list is enforced | 2 | **Planned** | Not done (6.1.2). Step 2 (not yet named in its scope). |
| 6.2.12 | New passwords checked against breached passwords | 2 | **Partly met** | The NCSC list is the passwords most used in breaches, but there's no check against a full breach corpus (such as Have I Been Pwned's range API). Step 2. |
| 6.3.1 | Brute-force controls in place, as documented | 1 | **Partly met** | Sign-in allows 30 attempts a minute per client address (IPv6 by /64) and 5 a minute per username, then answers 429 (`LoginThrottling`, `ThrottleLoginByUsernameAttribute`), with the client address from the forwarded headers behind the ingress (`LoginThrottlingTests`); a name no account could have is unknown without a lookup and gets no allowance of its own (`UsernameRules`); 5 wrong passwords lock an account for 5 minutes. Two-factor codes: at most 5 per challenge (reserved atomically), 10 per account in 15 minutes, and the code endpoints are limited per address as sign-in is; wrong ones count towards the lockout. Gaps: the counts are in each API copy's memory, so two copies allow twice as many; and the current-password check when changing a password is neither throttled nor counted towards lockout (`IdentityUserService.UpdateUserPasswordAsync`), so someone holding a session can guess its user's password without limit. |
| 6.3.2 | No default accounts such as `admin` | 1 | **Partly met** | No account has a default password: the first admin's comes from a secret (`FirstAdminSetup`). But it's named `admin` wherever the chart generates it and in the Azure instructions, so its name is known (risk S16). Step 4 lets the first admin be named. |
| 6.3.3 | Multi-factor authentication (MFA) | 2 | **Partly met** | An authenticator app (TOTP) through Identity's authenticator support, with recovery codes, optional for everyone (`TwoFactorService`, Account Settings, Security); or, for someone with a confirmed address, codes by email (`EmailCodeService`), offered as the weaker choice: NIST SP 800-63B doesn't accept email for out-of-band authentication, because it proves access to a mailbox rather than possession of a device. Admins can require two-factor for admin accounts (`SecuritySettings.RequireTwoFactorForAdmins`), which needs the app: an admin without it, or with codes by email, is held until they set it up (`TwoFactorSetupRequired`). Missing: it's off by default, staff aren't required to use it, and there are no passkeys. Step 4. |
| 6.3.4 | No undocumented authentication pathways | 2 | Met | Only `POST api/auth/login` (for an account without two-factor) and `POST api/auth/login/two-factor` create a session (`SessionService.CreateSessionAsync` is called only by `LoginHandler` and `LoginTwoFactorHandler`). A password reset ends sessions and creates none. `medapp-dev`'s test admin exists only in local databases. |
| 6.4.1 | Generated first passwords random, policy-compliant, short-lived, and never long-term | 1 | **Partly met** | Generated from a cryptographic random number generator, at least 16 characters, meeting the policy (`IdentityUserService.GeneratePassword`), and must be changed at first sign-in (`PasswordChangeRequired`), so can't become long-term. But an unused one never expires, and the first admin isn't made to change theirs. |
| 6.4.2 | No password hints or secret questions | 1 | Met | None. |
| 6.4.3 | A secure forgotten-password process that doesn't bypass MFA | 2 | **Partly met** | "Forgot your password?" emails a link to the account's confirmed address (put there only with the account's password, and confirmed only by the account itself, signed in, so a mistyped or shared address can't be confirmed by whoever reads it), with the same answer whether or not anything matched; the link is Identity's reset token, single-use and valid for 30 minutes, in the URL's fragment; the new password must meet the rules, avoid the common ones and differ from the current one; every session ends, and the owner is told by email (`ForgotPasswordHandler`, `ResetPasswordHandler`, `PasswordResetService`, `PasswordResetTests`). Two-factor stays on: the next sign-in still needs a code. An admin can still reset a password instead (`ResetUserPasswordHandler`). **But for an account using codes by email,** the reset link and the sign-in code both go to its mailbox, so whoever controls the mailbox gets past both factors; met only for accounts with the app (threat model T-52, accepted by the product owner on 18 November 2026 for this optional method, to be looked at again before first live use). |
| 6.4.4 | Identity proofing when an MFA factor is lost | 2 | **Partly met** | Someone who has lost their app uses a recovery code; without those, an admin resets their two-factor, which ends every session, is recorded with the admin's name and emails the owner, and every other admin when the account is an admin's (`ResetUserTwoFactorHandler`). New recovery codes need the password and a code. What the admin must check before resetting (seeing the person, or calling them back on a known number) isn't written down yet: it belongs in each customer's procedures. |
| 6.5.1 | Lookup secrets, out-of-band codes and TOTPs usable only once | 2 | Met | Each authenticator code is accepted once: the step of the last one is kept, and an earlier or equal step is refused, guarded by Identity's concurrency stamp (`TwoFactorService.VerifyAuthenticatorCodeAsync`, `TwoFactorServiceTests`, `TwoFactorTests.SignIn_TheSameCodeTwice_IsRefusedTheSecondTime`). Recovery codes are removed when used (Identity's `RedeemTwoFactorRecoveryCodeAsync`). The sign-in challenge is deleted when used, and so is an emailed code, in one statement that also checks it wasn't replaced meanwhile, so of two requests with the same code one succeeds (`EmailCodeRepository.ConsumeAsync`, `EmailCodeServiceTests`, `ParallelCodeTests`). |
| 6.5.2 | Low-entropy lookup secrets stored hashed and salted | 2 | **Planned** | Recovery codes are stored as Identity stores them, in clear, in `AspNetUserTokens`. Hash them (a custom `IUserTwoFactorRecoveryCodeStore`), in a later step (`docs/todo.md`). The sign-in challenge, by contrast, is stored only as a SHA-256 hash, and so is each emailed code (bound to its row, `EmailCodes.Hash`), though six digits could be worked out from a copy of the hash. |
| 6.5.3 | MFA secrets and seeds from a CSPRNG | 2 | Met | Identity makes the authenticator key (160 bits) and the recovery codes from `RandomNumberGenerator`; the sign-in challenge is 256 bits from `RandomNumberGenerator` (`SignInChallengeTokens`); emailed codes are 6 digits from `RandomNumberGenerator.GetInt32` (`EmailCodes.Generate`). |
| 6.5.4 | Lookup secrets and out-of-band codes have at least 20 bits of entropy | 2 | Met | Recovery codes are 10 characters from Identity's 26-character alphabet, about 47 bits. Emailed codes are 6 random digits (about 20 bits, which ASVS names as enough). |
| 6.5.5 | Out-of-band codes and TOTPs have a defined, short lifetime | 2 | Met | Authenticator codes are valid for their 30-second step and one either side (`Totp.Window`); the sign-in challenge lasts 5 minutes and allows 5 wrong codes (`SignInChallenge`). Emailed codes last 10 minutes, and a sign-in with them 10 minutes too (`EmailCode.Lifetime`, `SignInChallenge.EmailLifetime`). |
| 6.6.1 | Phone or SMS codes only with validated numbers and stronger options | 2 | Not applicable | No phone or SMS factors, and none planned (Step 4 plans passkeys). Codes by email follow its spirit: only to an address the account confirmed, with the stronger authenticator app offered beside them and the weakness explained in Account Settings, Security. |
| 6.6.2 | Out-of-band codes bound to their own request | 2 | Met | A sign-in's emailed code belongs to its challenge (`EmailCode.ChallengeId`) and goes with it; codes for turning codes by email on and for confirming a change each have their own purpose, and a new one replaces the last (`EmailCodeRepository`, `EmailCodeServiceTests.Check_AnotherPurposeOrChallenge_IsWrong`). |
| 6.6.3 | Out-of-band codes protected by rate limiting | 2 | Met | As authenticator codes (6.1.1, 6.3.1): five tries a sign-in (reserved in SQL), 10 code checks per account in 15 minutes, the growing lockout; codes for turning them on or confirming a change allow five tries each. Sending is limited too: once a minute and three times a sign-in, and 10 emails per account in 15 minutes (`EmailCodeService`, `AccountAttemptLimiter`). The codes are 6 digits, not 64 bits, so the limits are what protect them. |
| 6.8.1 | With several identity providers (IdPs), identities can't be spoofed across them | 2 | Not applicable | No identity providers. Applies from Step 4 (OIDC single sign-on), and per organisation from Step 8. |
| 6.8.2 | Signatures on authentication assertions always validated | 2 | Not applicable | No federated sign-in yet (Step 4). |
| 6.8.3 | SAML assertions used only once | 2 | Not applicable | No SAML. |
| 6.8.4 | Required authentication strength checked from the IdP's claims | 2 | Not applicable | No identity provider yet (Step 4). |

## V7 Session Management

[ASVS chapter](https://github.com/OWASP/ASVS/blob/v5.0.0/5.0/en/0x16-V7-Session-Management.md). 18 requirements at Levels 1 and 2: 8 met, 5 partly met, 3 planned, 2 not applicable.

Sessions are reference tokens: a database row, checked on every request, so revocation is immediate. This branch added ending sessions when a password is changed or reset (in the same transaction as the change) and signing a user out everywhere. A role change doesn't end sessions: roles are read on every request. Still missing: an idle timeout, a stronger token, sessions ended on re-sign-in, and users seeing their own sessions.

| ID | Requirement (paraphrased) | L | Status | Evidence or reason |
|---|---|---|---|---|
| 7.1.1 | Idle timeout and absolute lifetime documented and justified against NIST SP 800-63B | 2 | **Partly met** | The 8-hour lifetime (extendable on request to 24 hours from sign-in, since #63) and the lack of an idle timeout are documented (`docs/architecture.md` 6.1 and 8.1), but without a justification against NIST's re-authentication guidance. Step 2. |
| 7.1.2 | Documented limit on concurrent sessions, and what happens at the limit | 2 | **Planned** | Not documented; in practice unlimited (each sign-in adds one). Step 2 (not yet named in its scope). |
| 7.1.3 | Federated session systems documented | 2 | Not applicable | No single sign-on yet (Step 4). |
| 7.2.1 | Session tokens verified by a trusted backend | 1 | Met | On every request, `SessionAuthenticationHandler` looks the session up in the database (`SessionService.ValidateSessionAsync`). |
| 7.2.2 | Session tokens generated per session, not static keys | 1 | Met | A new `UserSession` for each sign-in (`SessionService.CreateSessionAsync`). |
| 7.2.3 | Reference tokens unique, from a CSPRNG, with at least 128 bits of entropy | 1 | **Partly met** | The token is the row's GUID (`UserSession.Id = Guid.NewGuid()`): from the operating system's secure generator, but only 122 random bits, and stored as it is, so anyone who can read the database can use live sessions. Step 2 plans a random 256-bit token, stored hashed. |
| 7.2.4 | New token at every authentication, and the current one ended | 1 | **Partly met** | Each sign-in creates a new session, but signing in again from a browser that still holds a valid session leaves the old one live until it expires (`LoginHandler` doesn't revoke it). |
| 7.3.1 | Inactivity timeout | 2 | **Planned** | None, deliberately left out of this branch. Step 2: a configurable idle timeout (30 minutes to start), with a warning before it ends (`docs/todo.md`, WCAG 2.2.1). |
| 7.3.2 | Absolute maximum session lifetime | 2 | Met | 8 hours from sign-in (`UserSession.Lifetime`); "Stay signed in" (`POST api/auth/session/extend`, from `main` since #63) extends it, but never past 24 hours from sign-in (`UserSession.MaxLifetime`), the absolute limit. Its justification is still to write (7.1.1). |
| 7.4.1 | A terminated session can't be used again | 1 | **Partly met** | Sign-out and revocation mark the row (`UserSession.Revoke`), and the next request fails. But an open notifications connection (`NotificationsHub`) isn't closed, so it keeps receiving that user's notifications until the page closes or the connection drops. |
| 7.4.2 | All sessions end when an account is disabled or deleted | 1 | Met | Deleting a user ends every session at its next request (`ValidateSessionAsync` finds no user). Accounts can't be disabled yet (Step 4 adds deactivation). The open-connection caveat in 7.4.1 applies. |
| 7.4.3 | Option to end other sessions after an authentication factor changes | 2 | Met | Automatic: changing your password ends your other sessions (`UpdateUserPasswordHandler`), and an admin's reset ends all of them (`ResetUserPasswordHandler`), each in the change's own transaction. Tested through the API (`SessionEndingTests`, `PasswordChangeRequiredTests`). |
| 7.4.4 | Sign-out easy to find on every authenticated page | 2 | Met | In the side navigation (`AppSideNav.tsx`, Logout) and on the forced password change page (`RequiredPasswordChangePage.tsx`). |
| 7.4.5 | Admins can end the sessions of one user, or of all users | 2 | **Partly met** | One user: `POST api/auth/users/{username}/sign-out-everywhere` and the Users page's action (`AuthController.SignOutEverywhere`), tested through the API (`SessionEndingTests`). All users at once: no. |
| 7.5.1 | Re-authentication before changing sensitive account attributes | 2 | Met | Changing the password needs the current one. Changing or removing the email address and turning two-factor on (with the app or codes by email) need the password; turning two-factor off and new recovery codes need the password and a code (an emailed one for codes by email); and for an account using codes by email, switching to the app and moving the address need the current factor too (an emailed code, sent only after the password, or a recovery code), so a session and the password can't swap the second factor (`EnableTwoFactorHandler`, `ChangeMyEmailHandler`; the review of #118, finding 2) (`PasswordConfirmation`, `EmailAddressTests`, `ParallelCodeTests`). A wrong password counts towards the lockout. The confirmed address is told when a change is asked for, and admins can remove an account's address (`RemoveUserEmailHandler`). Deleting a user needs the admin's own password (`DeleteUserHandler`). |
| 7.5.2 | Users can see and end their own active sessions | 2 | **Planned** | No. Step 4: a Security page listing active sessions, with "sign out everywhere". |
| 7.6.1 | Session lifetimes between relying parties and IdPs behave as documented | 2 | Not applicable | No federation yet (Step 4). |
| 7.6.2 | Sessions created only by an explicit user action | 2 | Met | Only by submitting the sign-in form (`POST api/auth/login`). |

## V8 Authorization

[ASVS chapter](https://github.com/OWASP/ASVS/blob/v5.0.0/5.0/en/0x17-V8-Authorization.md). 7 requirements at Levels 1 and 2: 3 met, 2 partly met, 1 planned, 1 not applicable.

Two roles, enforced only by the API, on every endpoint. The design choice that matters most is that every member of staff can see every patient; restriction by organisation comes with Step 8.

| ID | Requirement (paraphrased) | L | Status | Evidence or reason |
|---|---|---|---|---|
| 8.1.1 | Documented function-level and data-level access rules | 1 | **Partly met** | `docs/architecture.md` 5.7 gives the role for each group of endpoints, including sign-out-everywhere, and 8.1 says every member of staff sees every patient; `AuthorisationTests` (#65) lists and tests the admin-only and staff endpoints. Not yet a written rule per endpoint (data-level rules don't exist: every member of staff sees every patient). Step 2. |
| 8.1.2 | Documented field-level access rules | 2 | **Planned** | None written (today no field differs by role). Step 2, with 8.1.1. |
| 8.2.1 | Function-level access needs explicit permission | 1 | Met | Every action has `[Authorize(Policy = AuthPolicies.Staff)]` or `[Authorize(Roles = "Admin")]`, except sign-in, sign-out and who-am-I, and the session endpoints from #63 (`GET api/auth/session`, `POST api/auth/session/extend`), which are `[Authorize]` only: any signed-in user, for their own session (`MedApp.API/Controllers`); the hub needs `Staff`. `AuthorisationTests` (#65) checks every admin-only and staff endpoint for each role and signed out. Weakness: no fallback policy, so a new endpoint without an attribute would be open until the matrix gains it. |
| 8.2.2 | Data-level access needs explicit permission (no insecure direct object references) | 1 | **Partly met** | A user's own data is scoped to their ID: notifications (`NotificationRepository`), settings, and their own password (`SelfUserOnly`). Patients and procedures are open to all staff by design (`docs/architecture.md` 8.1). Restriction by organisation is Step 8; by team or care relationship isn't planned (risk S11). |
| 8.2.3 | Field-level access restricted | 2 | Met | Each action takes its own request DTO and returns a mapped response (`MedApp.Contracts`, the `…Mapper` classes). No field differs by role; admin-only changes (assignments, unlocking a stage) have their own admin endpoints. |
| 8.3.1 | Authorisation enforced at a trusted service layer | 1 | Met | Only the API enforces it. The React app's guards and hidden buttons are conveniences (`docs/architecture.md` 8.1). |
| 8.4.1 | Cross-tenant controls in multi-tenant apps | 2 | Not applicable | One deployment serves one organisation today. Applies from Step 8 (multi-tenancy). |

## V9 Self-contained Tokens

[ASVS chapter](https://github.com/OWASP/ASVS/blob/v5.0.0/5.0/en/0x18-V9-Self-contained-Tokens.md). 7 requirements at Levels 1 and 2: 7 not applicable.

Sessions are reference tokens (a database row). MedApp issues and accepts no self-contained tokens such as JSON Web Tokens (JWTs). Identity's password reset token is made and used within the same request (`IdentityUserService.ResetUserPasswordAsync`), so never leaves the server.

| ID | Requirement (paraphrased) | L | Status | Evidence or reason |
|---|---|---|---|---|
| 9.1.1 | Self-contained tokens' signature or MAC checked before use | 1 | Not applicable | No self-contained tokens. |
| 9.1.2 | Only allowlisted token algorithms, never `none` | 1 | Not applicable | No self-contained tokens. |
| 9.1.3 | Token keys only from trusted, pre-configured sources | 1 | Not applicable | No self-contained tokens. |
| 9.2.1 | Token validity period (`nbf`, `exp`) checked | 1 | Not applicable | No self-contained tokens. |
| 9.2.2 | Token type and purpose checked | 2 | Not applicable | No self-contained tokens. |
| 9.2.3 | Token audience checked | 2 | Not applicable | No self-contained tokens. |
| 9.2.4 | Tokens from a shared key carry an audience restriction | 2 | Not applicable | No self-contained tokens. |

## V10 OAuth and OIDC

[ASVS chapter](https://github.com/OWASP/ASVS/blob/v5.0.0/5.0/en/0x19-V10-OAuth-and-OIDC.md). 29 requirements at Levels 1 and 2: 29 not applicable.

MedApp uses no OAuth or OpenID Connect (OIDC) today. Step 4 makes it an OIDC client (a "relying party") for single sign-on, and then 10.1, 10.2 and 10.5 apply. It accepts no access tokens (10.3), and isn't, and isn't planned to be, an authorisation server or OpenID provider (10.4, 10.6, 10.7).

| ID | Requirement (paraphrased) | L | Status | Evidence or reason |
|---|---|---|---|---|
| 10.1.1 | Tokens sent only to the components that need them | 2 | Not applicable | No OAuth tokens. Applies from Step 4. |
| 10.1.2 | Authorisation responses accepted only for the flow this browser started (PKCE, `state`, `nonce`) | 2 | Not applicable | No OAuth flows. Applies from Step 4. |
| 10.2.1 | Code flow protected against CSRF (PKCE or `state`) | 2 | Not applicable | Not an OAuth client yet. Applies from Step 4. |
| 10.2.2 | Defence against mix-up attacks with several authorisation servers | 2 | Not applicable | Not an OAuth client yet. Applies from Step 4 if more than one IdP is configured (Step 8). |
| 10.3.1 | Resource server checks the access token's audience | 2 | Not applicable | Accepts no access tokens. |
| 10.3.2 | Resource server enforces the token's delegated claims | 2 | Not applicable | Accepts no access tokens. |
| 10.3.3 | Users identified by claims that can't be reassigned (`iss` and `sub`) | 2 | Not applicable | Accepts no access tokens. |
| 10.3.4 | Required authentication strength checked in access tokens | 2 | Not applicable | Accepts no access tokens. |
| 10.4.1 | Redirect URIs matched exactly against a per-client allowlist | 1 | Not applicable | Not an authorisation server. |
| 10.4.2 | Authorisation codes usable once | 1 | Not applicable | Not an authorisation server. |
| 10.4.3 | Authorisation codes short-lived | 1 | Not applicable | Not an authorisation server. |
| 10.4.4 | Each client gets only the grants it needs; no implicit or password grants | 1 | Not applicable | Not an authorisation server. |
| 10.4.5 | Refresh token replay mitigated for public clients | 1 | Not applicable | Not an authorisation server. |
| 10.4.6 | PKCE required, without the `plain` method | 2 | Not applicable | Not an authorisation server. |
| 10.4.7 | Unauthenticated dynamic client registration controlled | 2 | Not applicable | Not an authorisation server. |
| 10.4.8 | Refresh tokens have an absolute expiry | 2 | Not applicable | Not an authorisation server. |
| 10.4.9 | Users can revoke refresh and reference tokens | 2 | Not applicable | Not an authorisation server. |
| 10.4.10 | Confidential clients authenticated on back-channel requests | 2 | Not applicable | Not an authorisation server. |
| 10.4.11 | Clients assigned only the scopes they need | 2 | Not applicable | Not an authorisation server. |
| 10.5.1 | ID token replay prevented (`nonce`) | 2 | Not applicable | Not an OIDC client yet. Applies from Step 4. |
| 10.5.2 | Users identified by the ID token's `sub` | 2 | Not applicable | Not an OIDC client yet. Applies from Step 4. |
| 10.5.3 | Provider metadata's issuer must match the configured issuer | 2 | Not applicable | Not an OIDC client yet. Applies from Step 4. |
| 10.5.4 | ID token's audience is this client | 2 | Not applicable | Not an OIDC client yet. Applies from Step 4. |
| 10.5.5 | Back-channel logout tokens checked | 2 | Not applicable | Not an OIDC client yet. Applies from Step 4 if back-channel logout is used. |
| 10.6.1 | OpenID provider allows only safe response modes | 2 | Not applicable | Not an OpenID provider. |
| 10.6.2 | OpenID provider prevents forced logout | 2 | Not applicable | Not an OpenID provider. |
| 10.7.1 | User consents to each authorisation request | 2 | Not applicable | Not an authorisation server. |
| 10.7.2 | Consent prompts explain what's being granted | 2 | Not applicable | Not an authorisation server. |
| 10.7.3 | Users can review, change and revoke consents | 2 | Not applicable | Not an authorisation server. |

## V11 Cryptography

[ASVS chapter](https://github.com/OWASP/ASVS/blob/v5.0.0/5.0/en/0x20-V11-Cryptography.md). 14 requirements at Levels 1 and 2: 8 met, 3 partly met, 2 planned, 1 not applicable.

MedApp writes no cryptography of its own: it uses .NET's libraries, Identity, Data Protection, Traefik and Azure. The gaps are documentation (a key policy and an inventory), the password hasher's iteration count, the session token's size, and the certificate's key size.

| ID | Requirement (paraphrased) | L | Status | Evidence or reason |
|---|---|---|---|---|
| 11.1.1 | Documented key management policy and key lifecycle | 2 | **Planned** | None. Keys in use: TLS certificates (cert-manager), ASP.NET Core Data Protection keys (in the database since `feature/email-2fa`, shared by every pod, wrapped with a Key Vault key on Azure), the page server's key (per process), and Azure's own. Step 2 (not yet named in its scope; Step 5 adds a customer-managed key for the database's encryption). |
| 11.1.2 | A maintained cryptographic inventory | 2 | **Planned** | None beyond the notes in `docs/architecture.md` 8.1. Step 2 (not yet named in its scope). |
| 11.2.1 | Industry-validated cryptographic implementations | 2 | Met | Platform libraries only: .NET's `System.Security.Cryptography` (OpenSSL on Linux), ASP.NET Core Identity and Data Protection, Traefik's Go TLS, and Azure SQL's TLS and transparent data encryption (TDE). No custom cryptography. |
| 11.2.2 | Crypto agility: algorithms, keys and passwords can be changed | 2 | Met | Identity's hash format is versioned, and rehashes a password at sign-in when the settings change; Data Protection's algorithms are configurable; TLS is set at the ingress; every secret can be replaced in Key Vault (`deploy/azure/README.md`, Passwords). |
| 11.2.3 | At least 128-bit security for every primitive | 2 | **Partly met** | AES-256 and HMAC-SHA256 (Data Protection), PBKDF2-HMAC-SHA512, 128-bit nonces and a 256-bit page server key. But cert-manager's default certificate key is RSA 2048, about 112-bit security, and nothing sets another (`deploy/helm/medapp/templates/ingress.yaml`, `deploy/azure/cluster.yaml`). Use ECDSA P-256 or RSA 3072. |
| 11.3.1 | No ECB mode or weak padding | 1 | Met | MedApp encrypts nothing itself. Data Protection uses AES-256-CBC with HMAC-SHA256, and TLS is negotiated by Traefik. |
| 11.3.2 | Only approved ciphers and modes | 1 | Met | As 11.3.1. Azure SQL's TDE is AES-256. |
| 11.3.3 | Encrypted data protected against modification | 2 | Met | Data Protection payloads carry an HMAC, and TLS uses authenticated ciphers. |
| 11.4.1 | Only approved hash functions; never MD5 | 1 | Met | SHA-256 and SHA-512 (Identity, Data Protection). No MD5 or SHA-1 in the code. |
| 11.4.2 | Passwords stored with an approved, suitably slow key derivation function | 2 | **Partly met** | Identity's version 3 format: PBKDF2-HMAC-SHA512 with a random salt, at its default 100,000 iterations (no `PasswordHasherOptions` are set). OWASP's current guidance for PBKDF2-HMAC-SHA512 is 210,000. Raise `IterationCount`; each existing hash is upgraded at its user's next sign-in. |
| 11.4.3 | Collision-resistant hashes of 256 bits or more where needed | 2 | Met | HMAC-SHA256 in Data Protection; SHA-256 digests for images and attestations (`.github/workflows/publish.yml`). |
| 11.4.4 | Approved, stretched KDFs for keys derived from passwords | 2 | Not applicable | No keys are derived from passwords; passwords are only hashed (11.4.2). |
| 11.5.1 | Unguessable values from a CSPRNG, with 128 bits or more | 2 | **Partly met** | The CSP nonce (128 bits, `SecurityHeaders.NewNonce`), the page server's key (256 bits) and generated passwords use `RandomNumberGenerator`. Session IDs are GUIDs, which ASVS names as not meeting this (7.2.3). Step 2. |
| 11.6.1 | Approved algorithms for key generation and signatures | 2 | Met | Keys are made by cert-manager, Sigstore (the image attestations) and Azure, with standard algorithms. (The certificate's key size is the gap in 11.2.3.) |

## V12 Secure Communication

[ASVS chapter](https://github.com/OWASP/ASVS/blob/v5.0.0/5.0/en/0x21-V12-Secure-Communication.md). 9 requirements at Levels 1 and 2: 2 met, 5 partly met, 1 accepted risk, 1 not applicable.

TLS ends at Traefik, with Let's Encrypt certificates on Azure. Inside the cluster, traffic to the API and to Valkey is plain, behind network policies. Nothing pins or scans the TLS settings yet, and nothing has been deployed to Azure to scan.

| ID | Requirement (paraphrased) | L | Status | Evidence or reason |
|---|---|---|---|---|
| 12.1.1 | Only TLS 1.2 and 1.3, with 1.3 preferred | 1 | **Partly met** | Azure SQL requires TLS 1.2 or later (`minimalTlsVersion`, `deploy/azure/main.bicep`). Traefik runs with its default TLS options, which its documentation gives as TLS 1.2 minimum, 1.3 preferred; nothing in the repo pins them, and no scan has checked them. Pin a `TLSOption`, and scan once deployed (Step 5). |
| 12.1.2 | Only recommended cipher suites, strongest first | 2 | **Partly met** | Traefik's (Go's) default suites, not pinned or scanned (as 12.1.1). |
| 12.1.3 | Mutual TLS client certificates validated | 2 | Not applicable | No client certificates. |
| 12.2.1 | TLS for every external HTTP connection, with no fallback | 1 | Met | On Azure, Traefik redirects http to https, and the API sends HSTS (`deploy/azure/traefik-values.yaml`). The kind cluster is local, for testing, and serves http too. |
| 12.2.2 | Publicly trusted certificates | 1 | Met | Let's Encrypt through cert-manager on Azure (`deploy/azure/values.yaml`, `deploy/azure/cluster.yaml`). kind's is self-signed, for local use only. |
| 12.3.1 | Encrypted connections to and from the app, including databases | 2 | **Partly met** | Azure SQL: `Encrypt=True` (`deploy/azure/main.bicep`). Plain: the API to Valkey, which carries procedure titles (risk S14). No monitoring export yet (Step 5). Valkey's encryption isn't planned. |
| 12.3.2 | TLS clients validate certificates | 2 | **Partly met** | Azure SQL: `TrustServerCertificate=False`. The chart's own SQL Server (on kind, and the chart's default) uses `TrustServerCertificate=True` (`deploy/helm/medapp/templates/secret.yaml`). |
| 12.3.3 | TLS between internal HTTP services | 2 | **Accepted risk** | Traefik to the API, and Node to the API on loopback, are plain http inside the cluster and the pod, by design (`docs/architecture.md` 8.1, Transport); network policies let only Traefik reach the API. A service mesh, or TLS to the pod, would close it. Owner: Product owner. |
| 12.3.4 | Internal TLS uses trusted certificates, or pinned internal ones | 2 | **Partly met** | Only the chart's SQL Server uses TLS inside the cluster, and its self-signed certificate is trusted without checks (12.3.2). |

## V13 Configuration

[ASVS chapter](https://github.com/OWASP/ASVS/blob/v5.0.0/5.0/en/0x22-V13-Configuration.md). 13 requirements at Levels 1 and 2: 9 met, 2 partly met, 2 planned.

The platform is locked down well (network policies, the restricted Pod Security Standard, secrets from Key Vault). The big gap is the database login: the app and the migrations Job use the SQL admin, which Step 5 replaces.

| ID | Requirement (paraphrased) | L | Status | Evidence or reason |
|---|---|---|---|---|
| 13.1.1 | Every communication need documented | 2 | Met | `docs/architecture.md` 3.2 and 8.1 (the network policy table), and the chart's network policies list every allowed connection (`deploy/helm/medapp/templates/networkpolicy.yaml`). No user can make the server connect anywhere. |
| 13.2.1 | Backend connections authenticated with individual, short-lived or certificate credentials | 2 | **Planned** | The API signs in to SQL with a password (the server admin on Azure, `sa` in the cluster) and to Valkey with a shared password. Step 5 moves SQL to workload identity; Valkey's password isn't planned to change. |
| 13.2.2 | Backend accounts have least privilege | 2 | **Planned** | The API and the migrations Job both use SQL's admin login (risk S3). Step 5: a data-only user for the API, and schema rights for the Job. (The pods themselves are locked down: non-root, no capabilities, read-only filesystems, `deploy/helm/medapp/templates/_helpers.tpl`.) |
| 13.2.3 | No default credentials between services | 2 | Met | Every service password is generated or chosen per environment (`deploy/azure/README.md`, Passwords; the chart's random fallback in `deploy/helm/medapp/templates/secret.yaml`). |
| 13.2.4 | An allowlist of the external systems the app may reach | 2 | Met | Default-deny network policies with explicit egress: DNS, SQL Server (or Azure SQL's subnet) and Valkey only (`networkpolicy.yaml`, `networkPolicy.extraEgress`). |
| 13.2.5 | The server allowlisted for where it may send requests or load files | 2 | Met | As 13.2.4, at the network. In the app, the only outbound HTTP client calls the local Node server, checked per request (`SsrPages.Forward`). |
| 13.3.1 | Secrets in a secrets manager, never in code or build artefacts | 2 | Met | Key Vault through External Secrets on Azure, and a stand-in vault on kind (`deploy/helm/medapp/templates/externalsecrets.yaml`); none in git or the image (`.dockerignore`). Caveat: two appear on process command lines (risk S13; Step 2). |
| 13.3.2 | Least-privilege access to secrets | 2 | **Partly met** | External Secrets reads Key Vault with a read-only identity, serving only the `medapp` namespace (`deploy/azure/main.bicep`, `deploy/azure/cluster.yaml`). The page server runs in a container of its own in the API's pod, as another user, with none of the API's secrets (`deploy/helm/medapp/templates/api.yaml`, the `pages` container), so code in Node can't read them. But the API itself holds the SQL admin's connection string (13.2.2). |
| 13.4.1 | No source control metadata deployed | 1 | Met | `.git/` is kept out of the image (`.dockerignore`), and only build output is copied in (`MedApp.API/Dockerfile`). |
| 13.4.2 | Debug modes off in production | 2 | Met | Swagger and the developer exception page run only in Development (`ApplicationBuilderExtensions.UseApi`); Node runs with `NODE_ENV=production` (`SsrServer.StartInfo`, and the chart's `pages` container). |
| 13.4.3 | No directory listings | 2 | Met | Static files without directory browsing (`UseStaticFiles` only). |
| 13.4.4 | HTTP TRACE not supported | 2 | **Partly met** | TRACE never echoes the request, but isn't refused either: `/api` paths answer 404, and any other path gets the app's `index.html` with a 200, because the page fallback accepts every method (`SsrPages.Handle`). Answer 405 there for anything but GET and HEAD. |
| 13.4.5 | Internal documentation and monitoring endpoints not exposed | 2 | Met | Swagger only in Development. `/health` and `/alive` only on port 8081, which neither the Service nor the ingress exposes (`deploy/helm/medapp/templates/api.yaml`). |

## V14 Data Protection

[ASVS chapter](https://github.com/OWASP/ASVS/blob/v5.0.0/5.0/en/0x23-V14-Data-Protection.md). 9 requirements at Levels 1 and 2: 3 met, 5 partly met, 1 planned.

Caching is handled (`no-store` on every API response and rendered page). The governance pack on `main` (#61) identifies the personal data; the product doesn't yet carry out its retention and minimisation rules, and the browser keeps saved searches after sign-out.

| ID | Requirement (paraphrased) | L | Status | Evidence or reason |
|---|---|---|---|---|
| 14.1.1 | Sensitive data identified and classified into protection levels | 2 | **Partly met** | On `main` since #61, `docs/governance/data-inventory.md` lists every personal field and marks special category data. No protection levels beyond that. In this branch alone, only `docs/architecture.md` 8.2. |
| 14.1.2 | Protection requirements documented for each level | 2 | **Partly met** | Retention, logging and access are covered by `docs/governance/retention-schedule.md` and `docs/governance/dpia.md` (on `main` since #61), and encryption by `docs/architecture.md` 8.2, but not as one set of requirements per level. |
| 14.2.1 | Sensitive data never in URLs or query strings | 1 | **Partly met** | Session tokens never are. But list pages keep their filters in the URL (`AGENTS.md`, Lists), so searching for a patient puts their name in the page's URL and in the `api/patients/search` query string, and so in browser history and any access log. |
| 14.2.2 | Sensitive data not cached by server components | 2 | Met | `Cache-Control: no-store` on every `/api` response (`SecurityHeaders`) and on rendered pages (`SsrPages.Forward`); no server-side caches. |
| 14.2.3 | Sensitive data not sent to untrusted parties | 2 | Met | No analytics or trackers, and the page CSP allows connections only to the app's own origin (`connect-src 'self'`). |
| 14.2.4 | Data protection controls implemented as documented | 2 | **Planned** | The governance pack (on `main`) sets retention and logging rules the product doesn't carry out yet: no retention engine, and reads aren't recorded. Step 7. |
| 14.3.1 | Authenticated data cleared from the browser when the session ends | 1 | **Partly met** | Signing out clears the query cache (`useLogout`, `src/auth/auth.ts`), but not `localStorage` (settings, saved searches, pinned patients' IDs), and sends no `Clear-Site-Data`. Step 7 clears `localStorage` at sign-out. |
| 14.3.2 | Anti-caching headers on sensitive responses | 2 | Met | `no-store` on `/api` and on rendered pages (14.2.2). Static assets hold no user data. |
| 14.3.3 | No sensitive data in browser storage, except session tokens | 2 | **Partly met** | `localStorage` holds settings, including saved searches whose terms can be patient names, and pinned patients' IDs (`src/app/settingsStore.ts`, `src/components/navigation/pins.ts`). Step 7. |

## V15 Secure Coding and Architecture

[ASVS chapter](https://github.com/OWASP/ASVS/blob/v5.0.0/5.0/en/0x24-V15-Secure-Coding-and-Architecture.md). 13 requirements at Levels 1 and 2: 7 met, 6 partly met.

This branch's supply chain work (SHA-pinned actions, Dependabot, CodeQL, dependency review, audits as errors, an image scan, an SBOM and provenance) covers most of the dependency requirements. Defensive coding is sound; the gaps are availability limits and leftovers like the "return everything" endpoints.

| ID | Requirement (paraphrased) | L | Status | Evidence or reason |
|---|---|---|---|---|
| 15.1.1 | Documented time frames for fixing vulnerable components | 1 | Met | On `main` since #61: `docs/governance/security-policies.md`, section 6 (critical and high within 14 days of a fix, medium within 30, base images rebuilt monthly). Arrives here when `main` is merged. |
| 15.1.2 | An SBOM of third-party components, from trusted repositories | 2 | Met | An SBOM and a provenance attestation for every published image (`.github/workflows/publish.yml`); packages only from nuget.org and npm, with a lock file for npm (`MedApp.Web/package-lock.json`). |
| 15.1.3 | Documented resource-heavy functions, and how to protect them | 2 | **Partly met** | Page rendering's timeout and fallback (`docs/agents/local-dev.md`; `docs/architecture.md` 6.2) and list sizes are documented; there's no overall list of costly operations. |
| 15.2.1 | Components kept within the documented time frames | 1 | **Partly met** | CI fails on known high and critical issues: NuGet audit (NU1903 and NU1904 as errors), `npm audit --audit-level=high`, dependency review, and Trivy on the image for fixable criticals (`.github/workflows/main.yml`; since #123 the npm audit runs on `main` and on pull requests labelled `ci:full`, and Trivy also on pull requests that change the Dockerfile or a package manifest), with Dependabot proposing updates weekly and security updates as soon as an advisory is published (the repository's dependency graph, Dependabot alerts and security updates are on: `docs/todo.md`, Repository settings). Time-limited exceptions are documented (`AGENTS.md`, supply chain rules). Nothing tracks medium issues against the 30-day rule, or rescans images already deployed. |
| 15.2.2 | Defences against availability loss from costly functions | 2 | **Partly met** | Sign-in throttling, lists capped at 100, the render timeout (`Ssr:RenderTimeout`), and resource limits on every pod. No general rate limit (Step 2), and a page that stalls part-way holds its request with no timeout (`docs/architecture.md` 6.2). |
| 15.2.3 | Production holds only the functionality it needs | 2 | **Partly met** | No Swagger, tests or development tools in the image (`.dockerignore`). But `GET api/patients` and `GET api/auth/users` return every row, for the deprecated client only (Step 2 removes them), and the health port, 8081, serves the whole app (risk S15). |
| 15.3.1 | Only the required fields returned | 1 | **Partly met** | Responses are DTOs built by mappers (`MedApp.Contracts`), never entities. Exception: the login response repeats the session ID (3.3.4). |
| 15.3.2 | Server-side calls don't follow redirects unless intended | 2 | Met | The only outbound HTTP client has `AllowAutoRedirect = false` (`ServiceExtensions.AddApi`). |
| 15.3.3 | Protection against mass assignment | 2 | Met | Each action binds its own request DTO and maps it field by field into a command (`MedApp.API/Controllers`); entities are never bound. |
| 15.3.4 | The client's IP address passed through trusted fields, and used correctly | 2 | **Partly met** | Traefik keeps the client's address (`externalTrafficPolicy: Local` on Azure), and the API reads it from `X-Forwarded-For` for throttling and the session record. But the API accepts that header from any caller (4.1.3, risk S12). Step 2. |
| 15.3.5 | Correct types and strict comparisons | 2 | Met | C# is statically typed, and the web app is TypeScript in `strict` mode (`MedApp.Web/tsconfig.app.json`). |
| 15.3.6 | JavaScript safe from prototype pollution | 2 | Met | No merging of untrusted objects in `MedApp.Web/src` (`Object.assign` is used only on fixed objects), and settings are held in a `Map` (`settingsStore.ts`). |
| 15.3.7 | Protection against HTTP parameter pollution | 2 | Met | Each parameter binds from one declared source (`[FromQuery]`, `[FromBody]` or the route), under `[ApiController]`. |

## V16 Security Logging and Error Handling

[ASVS chapter](https://github.com/OWASP/ASVS/blob/v5.0.0/5.0/en/0x25-V16-Security-Logging-and-Error-Handling.md). 16 requirements at Levels 1 and 2: 7 met, 4 partly met, 5 planned.

Error handling is good; security logging is the second-weakest area. Only procedure changes are audited. Sign-ins, failures, refusals and account changes leave no record, and logs never leave the cluster. Steps 5 and 7 fix this.

| ID | Requirement (paraphrased) | L | Status | Evidence or reason |
|---|---|---|---|---|
| 16.1.1 | An inventory of logging: events, formats, storage, access and retention | 2 | **Partly met** | On `main` since #61, `docs/governance/data-inventory.md` describes the application logs, and `docs/governance/retention-schedule.md` their planned retention; `docs/architecture.md` 8.4 describes telemetry. No single inventory, and nothing on access. Step 5. |
| 16.2.1 | Log entries say when, where, who and what | 2 | **Partly met** | Procedure audit entries do (`PatientProcedureAuditEntry`). Application logs have the time and source, but not the user. |
| 16.2.2 | Synchronised clocks, and UTC timestamps | 2 | Met | Timestamps are UTC throughout (`AGENTS.md`, Data access; `TimeProvider`), and OpenTelemetry and the container runtime stamp logs in UTC. Node clocks are the platform's. |
| 16.2.3 | Logs go only to documented destinations | 2 | Met | The container console, plus an OTLP endpoint only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set (`MedApp.ServiceDefaults/Extensions.cs`), both documented (`docs/architecture.md` 8.4). |
| 16.2.4 | Logs in a format the log processor can read | 2 | **Partly met** | OpenTelemetry when exported (in development only, today); in Kubernetes, plain-text console logs. Step 5 sends them to Application Insights. |
| 16.2.5 | Sensitive data logged only as its protection level allows | 2 | Met | EF Core logs SQL without parameter values; records that carry a password print `***` (`CreateUserResult`, `CreateUserResponse`, `ResetUserPasswordResponse`); no code logs patient fields or session IDs. |
| 16.3.1 | Every authentication attempt logged, successful or not | 2 | **Partly met** | The activity log (`ActivityEntries`) records each sign-in, failed sign-in (with its reason: wrong password, locked, wrong code, expired challenge, too many codes) and sign-out, each step of two-factor and each password reset by link, with the request's address and never what was typed. Throttling refusals before a handler runs are logged at Warning in the application log only, with the limit and the client's address, never the username (`LoginThrottling.LogRefusal`). |
| 16.3.2 | Failed authorisation logged | 2 | **Planned** | Not logged: ASP.NET Core's own messages are at Information, below the `Microsoft.AspNetCore: Warning` level (`MedApp.API/appsettings.json`). Step 7. |
| 16.3.3 | Documented security events logged, including attempts to bypass controls | 2 | **Partly met** | Account security events (`SecurityEventKind`: two-factor turned on or off, codes by email turned on or off, reset, recovery codes, reset links, address changes) go to the activity log, by who did them (nobody, with the account as target, for a request nobody was signed in for), and to the application log without addresses or codes. Failed validation and most 429s aren't logged. Step 7. |
| 16.3.4 | Unexpected errors and security control failures logged | 2 | Met | Unhandled exceptions are logged by ASP.NET Core at Error; render failures and Node exiting by `SsrPages` and `SsrServer`; failed live pushes by `SignalRNotificationPublisher`. Nothing alerts on them yet (Step 5). |
| 16.4.1 | Log data encoded to prevent log injection | 2 | **Partly met** | Structured logging with message templates, but in Kubernetes the console formatter writes plain text, so a request path logged by `SsrPages` that carries an encoded line break could forge a line. Use the JSON console formatter. |
| 16.4.2 | Logs protected from unauthorised access and change | 2 | **Planned** | Console logs stay on the nodes, readable by cluster admins, and the audit table can be changed by anyone with the SQL admin login. Step 5 (Log Analytics) and Step 7 (an append-only ledger table). |
| 16.4.3 | Logs sent securely to a separate system | 2 | **Planned** | No: logs stay in the cluster. Step 5 (Log Analytics), and Step 7 for the audit trail. |
| 16.5.1 | Generic messages for unexpected errors; nothing internal exposed | 2 | Met | Known errors become `ProblemDetails` with a plain title (`ExceptionHandling`); anything else is a bare 500, since the developer exception page runs only in Development. |
| 16.5.2 | Secure operation when an external resource fails | 2 | Met | Page rendering falls back to the client-rendered app (`SsrPages`); live pushes are best effort; database calls retry transient errors (`EnableRetryOnFailure`); and sign-in fails closed when the database can't be reached. |
| 16.5.3 | Fails securely, never open | 2 | Met | Validation throws before the handler runs (`ValidationBehavior`); a bad or unknown session cookie fails authentication (`SessionAuthenticationHandler`); `PasswordChangeRequired` refuses by default, with a short allowlist. |

## V17 WebRTC

[ASVS chapter](https://github.com/OWASP/ASVS/blob/v5.0.0/5.0/en/0x26-V17-WebRTC.md). 7 requirements at Levels 1 and 2: 7 not applicable.

MedApp has no WebRTC (real-time audio, video or data between browsers): no TURN server, media server or signalling server.

| ID | Requirement (paraphrased) | L | Status | Evidence or reason |
|---|---|---|---|---|
| 17.1.1 | TURN server relays only to non-reserved addresses | 2 | Not applicable | No WebRTC. |
| 17.2.1 | DTLS certificate key managed under the key policy | 2 | Not applicable | No WebRTC. |
| 17.2.2 | Approved DTLS cipher suites and SRTP protection profile | 2 | Not applicable | No WebRTC. |
| 17.2.3 | SRTP authentication checked at the media server | 2 | Not applicable | No WebRTC. |
| 17.2.4 | Media server survives malformed SRTP packets | 2 | Not applicable | No WebRTC. |
| 17.3.1 | Signalling server rate-limits floods | 2 | Not applicable | No WebRTC. |
| 17.3.2 | Signalling server survives malformed messages | 2 | Not applicable | No WebRTC. |

## Gaps, in priority order

The Level 1 and 2 requirements not met that matter most for MedApp, most important first. "Not yet named" means step 2's scope should be extended to cover it.

1. **MFA is optional** (6.3.3, 6.5.2, 6.4.3). An authenticator app (TOTP) can be set up by anyone and required for admins, but it's off by default, there are no passkeys, and recovery codes are stored in clear. Codes by email are a weaker option: for those accounts the mailbox alone is enough, through a reset link and a code. The ICO has fined health suppliers for missing MFA, and NHS buyers' DTAC and DSPT reviews ask for it. **Step 4.**
2. **Security events aren't recorded, and logs stay in the cluster** (16.3.1, 16.3.2, 16.3.3, 16.4.2, 16.4.3, 16.1.1). Sign-ins, failures, lockouts, throttling, refused requests and account changes leave no trace, so misuse can't be investigated. **Steps 7 and 5.**
3. **The app uses the database's admin login** (13.2.1, 13.2.2, 13.3.2). Anyone who gets the API's environment holds full control of the database. In Kubernetes, code running in Node no longer can: the page server runs in a container of its own, without the API's secrets. **Step 5** (workload identity and a data-only user).
4. **Session tokens and lifecycle** (7.2.3, 11.5.1, 7.3.1, 7.1.1, 7.2.4, 7.4.1, 3.3.4, 15.3.1). The token is a 122-bit GUID stored in clear; there's no idle timeout; signing in again leaves the old session live; open live connections survive revocation; and the login response repeats the session ID in its body. **Step 2** for the token and the idle timeout; the other three are not yet named.
5. **Passwords** (6.2.5, 6.2.12, 6.1.2, 6.2.11, 11.4.2, 6.3.1). Composition rules remain; there's no breached-password check or context-specific word list; PBKDF2 runs at 100,000 iterations against OWASP's 210,000; and the current-password check when changing a password is neither throttled nor counted towards lockout. **Step 2** for the first two; the rest not yet named.
6. **Authorisation rules not written per endpoint, and no fallback policy** (8.1.1, 8.1.2, 8.2.1). `AuthorisationTests` (#65) now tests every admin-only and staff endpoint for each role, but a new endpoint without an attribute would be open until the matrix gains it. **Step 2.** Add a fallback policy that requires sign-in: not yet named.
7. **CSRF and WebSocket origin rest on `SameSite` alone** (3.5.1, 3.5.2, 4.4.2). Body-less unsafe endpoints and the notifications hub would accept a request from a same-site page, such as a compromised sibling subdomain. Require a custom header on unsafe methods, and check `Origin` on the hub. **Not yet named.**
8. **Forwarded headers trusted from anyone** (4.1.3, 15.3.4). Throttling and the session record depend on `X-Forwarded-For`, which the API accepts from any caller that gets past the network policy. **Step 2.**
9. **Patient data in the browser and in URLs** (14.3.1, 14.3.3, 14.2.1). Saved searches with patient names stay in `localStorage` after sign-out, and patient searches put names in URLs and query strings. **Step 7** for storage; URLs are not yet planned.
10. **No general rate limit** (2.4.1, 15.2.2). Only sign-in is throttled; a stolen session can page through every patient at full speed. **Step 2.**
11. **A known first admin name, and malicious lockout** (6.3.2, 6.1.1). The first admin is called `admin`, and anyone can lock it for 5 minutes. **Step 4.**
12. **Cryptography and TLS undocumented and unpinned** (11.1.1, 11.1.2, 11.2.3, 12.1.1, 12.1.2, 12.3.1). No key policy or inventory; the certificate key is RSA 2048; TLS settings are Traefik's defaults, never scanned; Valkey traffic is plain. **Step 2** for the documents (not yet named), Step 5 for the scan; Valkey is not yet planned.
13. **Uploaded files aren't scanned for malware** (5.4.3). Every attached file is "Not scanned", and staff open them on clinical workstations. The type checks stop the commonest active content, not exploits in the formats themselves. **Step 9, brought forward to before go-live** (DPIA risk 22, H-25).

### Small fixes

Each of these is a few lines, and could go in this branch or the next:
- **HSTS for a year, with subdomains** (3.4.1): `services.AddHsts(o => { o.MaxAge = TimeSpan.FromDays(365); o.IncludeSubDomains = true; })`.
- **No session ID in the login response body** (3.3.4, 15.3.1): `LoginResponse` keeps only the expiry.
- **Revoke the old session on sign-in** (7.2.4): the login action reads the cookie it's replacing and revokes that session.
- **Raise the password hashing work** (11.4.2): `PasswordHasherOptions.IterationCount` to at least 210,000; Identity rehashes at each user's next sign-in.
- **Count wrong current passwords** (6.3.1): `UpdateUserPasswordAsync` calls `AccessFailedAsync` on a wrong current password, as `VerifyPasswordAsync` already does.
- **Cap `Page`** (1.4.2) and **the patient email's length** (2.2.1) in their validators.
- **A charset on problem responses** (4.1.1): `application/problem+json; charset=utf-8`.
- **405 for other methods on the page fallback** (13.4.4).
- **ECDSA certificate keys** (11.2.3): the `cert-manager.io/private-key-algorithm: ECDSA` annotation on the ingress.
- **JSON console logs** in Kubernetes (16.4.1, 16.2.4): `Logging__Console__FormatterName=json` in the chart.
- **A fallback authorisation policy** (8.2.1) that requires a signed-in user, with `AllowAnonymous` on what must stay open: sign-in, sign-out, who-am-I, the page fallback, the `/api` 404 and the health checks.

## Beyond Level 2

Level 3 controls that matter for an app holding medical records, and where MedApp stands on each:
- **Email as an authentication factor** (6.3.6 at Level 3, as we read ASVS 5.0: email shouldn't be used as a single-factor or multi-factor authentication mechanism). MedApp checks against Level 2, so this isn't in the checklist above, but codes by email wouldn't meet it: at Level 3 they'd be turned off, leaving the authenticator app and, later, passkeys.
- **Phishing-resistant MFA for admins** (6.3.3 at Level 3): Step 4 plans passkeys; make them compulsory for admins.
- **No account enumeration** (6.3.8): not met. A locked account answers 423 even to a wrong password, which confirms the account exists, and an unknown username skips password hashing, so it answers faster. Step 2 plans one answer for all three cases; the timing needs a dummy hash as well.
- **Telling users about suspicious sign-ins and credential changes** (6.3.5, 6.3.7): not met; needs email or in-app notices (Step 4).
- **Step-up authentication for highly sensitive operations** (7.5.3): partly. Deleting a user needs the admin's password; resetting a password and changing roles don't.
- **Authorisation changes applied at once** (8.3.2): met for requests, since roles are read on every request (a role change applies to the user's next request, without ending the session); an open live connection isn't closed (7.4.1).
- **Layered protection for admin interfaces** (8.4.2): only the role today; MFA for admins (Step 4) is the first layer to add.
- **Logging every access to sensitive data** (16.3.2 at Level 3): reads of patient records aren't recorded. Step 7 audits every view and search.
- **Returning only the minimum sensitive data** (14.2.6): `GET api/patients` returns every patient; Step 2 removes it.
- **Automatic retention** (14.2.7): nothing is ever deleted; Step 7 builds a retention engine.
- **Two-person approval for high-value actions** (2.3.5): none; Step 7's approved erasure workflow is the natural first use.
- **CSP violation reports and `Cross-Origin-Opener-Policy`** (3.4.7, 3.4.8): neither is sent; both are small additions to `SecurityHeaders`.
- **CSV and formula injection** (1.2.10): no exports yet; applies to Step 7's subject access export.

## Where the handbook was out of date

When this checklist was written, `docs/architecture.md` still described the session cookie, session ending, throttling, the common-password list, the supply chain, risks S5 and S6, and the endpoint table (5.7) as they were before this branch. They were brought up to date in the same branch. One thing the handbook says that the code doesn't quite bear out:
- **"FluentValidation on every command":** every command with free input has a validator, but nine have none: eight that take only IDs (checked by route constraints) and `ResetUserPasswordCommand`, which takes a username.
