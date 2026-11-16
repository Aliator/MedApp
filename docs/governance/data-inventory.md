# Data inventory and data flows

**Draft prepared for review. It is not legal advice and must be reviewed and approved by a qualified data protection professional and the organisation's Data Protection Officer before use.**

| | |
|---|---|
| **Version** | 0.2 (draft), 16 November 2026: the privacy features of roadmap step 7 |
| **Owner** | [DPO name], with the lead developer |
| **Checked against** | The code on `main` as of 16 November 2026, with branch `feature/privacy-features` (roadmap step 7): `MedApp.Domain`, `MedApp.Infrastructure/Data`, `MedApp.Application`, `MedApp.Web/src/app` and `deploy/` |
| **Next review** | With every change that adds or changes a stored field, and at least every 6 months |

Every place MedApp stores or sends personal data, field by field, from the code. The ROPA ([ropa.md](ropa.md)), the DPIA ([dpia.md](dpia.md)) and the retention schedule ([retention-schedule.md](retention-schedule.md)) refer to it.

**Who the data is about:**
- **Patients** of the customer.
- **Staff users:** the customer's staff with MedApp accounts.
- **Supplier staff:** only in the supplier's own systems (Azure, GitHub), not in MedApp's database.

## 1. The database (SQL Server; Azure SQL in production)

| Entity (table) | Personal data | About | Special category? | Notes |
|---|---|---|---|---|
| `Patient` (`Patients`) | `FirstName`, `LastName`, `DateOfBirth`, `Email`, `CreatedAt`, `LastUpdated`; the privacy flags `RestrictedAtUtc` and `RestrictionReason` (Article 18), `DisputedAtUtc` and `DisputeReason` (Article 16), and `AnonymisedAtUtc`; the retention decision `RetentionKeptUntil` and `RetentionKeptReason` | Patients | No on its own; yes in combination with the procedures below. **The reasons are free text** an admin types, and can say what the patient disputes. | `MedApp.Domain/Patients/Patient.cs`. No NHS number, address, phone or sex today. An anonymised patient keeps the row with placeholders: the name "Anonymised Patient", a placeholder email, and only the year of birth (`PatientAnonymiser`). |
| `PatientProcedure` (`PatientProcedures`) | `Title`, `Notes` (Markdown free text), `DueDateUtc`, `Priority`, `Status`, `TemplateName`, `PatientId`, timestamps | Patients | **Yes:** what procedure a patient is having, and clinical free text | `MedApp.Domain/Procedures/PatientProcedures/PatientProcedure.cs` |
| `PatientProcedureStage` (`PatientProcedureStages`) | `Notes` (clinical free text); `StartedByUserId`, `CompletedByUserId` and times; a copy of the stage's name, description and instructions | Patients; staff (who did what) | **Yes** (notes) | `PatientProcedureStage.cs`. The name, description and instructions are copied from the stage definition and hold no personal data unless an admin wrote some into the definition. |
| `PatientProcedureAssignment` (`PatientProcedureAssignments`) | `UserId`, `AssignedByUserId`, `AssignedAtUtc` | Staff; links to a patient | Indirectly | `PatientProcedureAssignment.cs` |
| `PatientProcedureAuditEntry` (`PatientProcedureAuditEntries`) | `PatientId`, `ProcedureTitle`, `StageName`, `UserId`, `Username`, `AtUtc`, `Action`, and `Details` (JSON) | Patients; staff | **Yes** | `PatientProcedureAuditEntry.cs`. **`Details` holds copies of clinical text:** `StageNotesUpdated` stores the old and new stage notes (`ProcedureChange.UpdateStageNotes`), and `DetailsUpdated` stores the old and new title and procedure notes (`UpdatePatientProcedureHandler`). Status changes store the reason for a block or cancellation. No foreign keys: entries outlive the procedure and the user. Anonymising the patient replaces the titles and every piece of free text in `Details` with placeholders. |
| `ActivityEntry` (`ActivityEntries`) | `AtUtc`, `Action`, `UserId`, `Username`, `IpAddress`, `PatientId`, `ProcedureId`, `TargetUsername`, `Details` (JSON) | Staff (who did what, from which address); patients by ID | Indirectly: that a patient's record was opened. **No patient names, details or clinical text:** edits record the names of the fields changed, searches the names of the filters used and the number found (never the terms), and refused sign-ins the username tried (and the account's ID when it exists). Reasons for restricting, disputing, keeping or anonymising are the only free text, and anonymising replaces the earlier ones. | `MedApp.Domain/Activity/ActivityEntry.cs`, written through `IActivityLog`: patients viewed (once per person every 5 minutes), shown in search results, searched, created, edited, deleted, exported, anonymised, restricted and disputed; procedures viewed; sign-ins, refused sign-ins and sign-outs; account, password and role changes. Admins only. No foreign keys: entries outlive the patient and the account. Never deleted yet: a scheduled job to purge sign-in events at 12 months is to come (retention schedule, section 3). Not in the subject access export: the export gives the patient each access by username, action and time, without the staff's account IDs or addresses. |
| `Notification` (`Notifications`) | `UserId`, `Title` (the procedure's title), `Message` (for example "[username] assigned you to this procedure."), `ProcedureId`, `CreatedAtUtc`, `ReadAtUtc` | Staff; patients indirectly | Possibly: a procedure title can name a condition or treatment | `MedApp.Domain/Notifications/Notification.cs`; written by `AssignPatientProcedureHandler`. No foreign keys. |
| `UserSession` (`UserSessions`) | `UserId`, `CreatedAtUtc`, `ExpiresAtUtc`, `RevokedAtUtc`, `CreatedByIp` (IP address), `UserAgent` (browser) | Staff | No | `MedApp.Domain/Authentication/UserSession.cs`. The row's ID is the session cookie's value. Rows are never deleted today. |
| `UserSettings` (`UserSettings`) | `UserId`, `Json`, `UpdatedAtUtc` | Staff; patients indirectly | Possibly | `UserSettings.cs`. The JSON includes **saved searches ("favourites")**, whose terms can be patient names, and the IDs of the patients and procedures the user has pinned (`medapp.pins`). |
| ASP.NET Core Identity (`AspNetUsers`, `AspNetUserRoles`, `AspNetRoles` and the other Identity tables) | `UserName`, `NormalizedUserName`, `PasswordHash`, `SecurityStamp`, `AccessFailedCount`, `LockoutEnd`, `CreatedAt`; `Email` and `PhoneNumber` columns exist but MedApp doesn't fill them | Staff | No | `MedApp.Infrastructure/Common/Identity/ApplicationUser.cs`. Passwords are stored only as Identity's salted hashes. |
| `PatientProcedureStageDefinition`, `PatientProcedureStageTemplate` and its map | Names, descriptions, instructions | None, by design | No | Configuration. Admins should not write patient or staff details into them. |

## 2. Outside the database

| Where | What | Personal data | Kept for |
|---|---|---|---|
| **The session cookie** `__Host-medapp.session` (`medapp.session` in development) | The session's ID | Pseudonymous identifier for a staff user | Until the session ends (8 hours, extendable to 24 at most from sign-in) |
| **Browser `localStorage`** (`medapp.*` keys, `MedApp.Web/src/app`) | The account's settings (colour mode, colours, text size, list settings, accessibility), saved searches (`medapp.favourites`), and pinned patients and procedures as IDs (`medapp.pins`, `MedApp.Web/src/components/navigation/pins.ts`) | Saved search terms can be patient names; pinned items are IDs | **Until sign-out,** which clears every `medapp.` key but the four visual settings (colour mode, colours, text size, accessibility); the account keeps the rest for the next sign-in (`clearStorageAtSignOut`, `MedApp.Web/src/app/settingsStore.ts`). Pins kept under the old per-user keys (`medapp.pinnedPatients.<username>`) move to the account and are cleared too. A session that only expires leaves them until the next sign-out. Full list: [cookies-and-storage.md](cookies-and-storage.md). |
| **Browser `sessionStorage`** (`medapp.halfView`) | The page open in the half view | Can include a patient or procedure ID | Until the tab closes or the user signs out |
| **Browser history and address bar suggestions** | Every page URL visited. List pages keep their filters in the URL (`MedApp.Web/src/pages/listSearch.ts`), and the patient list's filters are first name, last name, email and a date of birth range, so a search becomes a URL like `/patients?last=…&dobFrom=…` | Patient names, emails and dates of birth searched for, and record IDs; on a shared computer, the next user can see who was searched for | Until the browser's history is cleared or expires. **Signing out doesn't clear it.** The filters stay in the URL by the product owner's decision (16 November 2026), because shareable list links are a feature; the mitigations are the customer's: separate browser profiles, or history cleared, on shared computers ([dpia.md](dpia.md), risk 10). |
| **The browser's memory** (TanStack Query cache) | Data of the pages viewed | Patients, procedures | Until sign-out, which clears it, or the tab closes |
| **Server-rendered pages** | HTML with the page's data | Patients, procedures | Not kept: sent with `Cache-Control: no-store` (`MedApp.API/ServerRendering/SsrPages.cs`) |
| **The Node page server** | Renders each page in memory, with a new router and cache per request | Transient | Not kept |
| **Valkey** (the SignalR backplane, in the cluster) | Notifications in transit between API copies | Procedure title, the assigner's username | Not kept: Valkey saves nothing to disk (`deploy/helm/medapp/templates/valkey.yaml`). Traffic inside the cluster isn't encrypted. |
| **Application logs** (container console; Log Analytics once step 5 is done) | The API's logs, at the levels in `MedApp.API/appsettings.json` (`Default: Information`, `Microsoft.AspNetCore: Warning`) | EF Core logs every SQL command at Information level **without parameter values** (sensitive data logging is off). ASP.NET Core's own request logs are below the configured level. Error messages can include record IDs. The Node page server's output is logged as `SSR: …` lines. | Console logs: as long as the node keeps them. Planned: 90 days ([retention-schedule.md](retention-schedule.md)) |
| **OpenTelemetry traces and metrics** | Request timings, paths and status codes | Paths include record IDs; list pages keep their filters in the URL, so a search term (which can be a patient's name) can appear in a traced URL. [Check what the instrumentation records before exporting traces.] | Exported only where `OTEL_EXPORTER_OTLP_ENDPOINT` is set: in development, to the local Aspire dashboard. Not exported in Kubernetes today. |
| **Azure SQL backups** | The whole database | Everything above | 7 days of point-in-time restore today (`deploy/azure/main.bicep`); planned 35 days plus long-term retention |
| **Kubernetes Secrets and Key Vault** | Connection strings and the first admin's password | The first admin's credentials | While deployed |
| **GitHub** | Source code, CI logs, container images, documentation | **No customer personal data.** Commit author names and emails of the supplier's developers. Test data is synthetic only (`AGENTS.md`). | For the life of the repository |
| **Developers' machines** | The local development database | **Synthetic data only**, from `medapp-dev`'s `reset_demo_data` (invented names, `@example.com` addresses) | Until reset |

## 3. Data flows

```mermaid
flowchart LR
    staff(["Customer's staff, in a browser"])
    subgraph uk["Microsoft Azure, UK South"]
      ingress["Traefik ingress: TLS ends here"]
      api["MedApp API pods, with the Node page server inside"]
      valkey["Valkey: messages in transit only"]
      sql[("Azure SQL: private endpoint, encrypted at rest")]
      backups[("Azure SQL backups")]
      logs["Container logs; Log Analytics (planned)"]
    end
    gh["GitHub: code and images, no customer data"]
    staff -->|"HTTPS: pages, /api, WebSocket"| ingress
    ingress -->|"HTTP inside the cluster"| api
    api -->|"TDS, encrypted"| sql
    api -->|"Redis protocol, not encrypted"| valkey
    sql --> backups
    api --> logs
    gh -->|"image copied to the Azure registry"| api
```

| Flow | Data | Protection | Status |
|---|---|---|---|
| Browser to ingress | Everything a user sees or enters | TLS (Let's Encrypt certificate, http redirected to https); HSTS | Built; never deployed to Azure |
| Ingress to API | The same | Plain HTTP inside the cluster; network policy allows only the ingress namespace | Built |
| API to Azure SQL | All stored data | TLS (`Encrypt=True`), private endpoint, public access off | Built in Bicep; never deployed |
| API to Valkey | Notifications | Password, not encrypted; network policy | Built |
| API to logs | Logs | Inside Azure | Logging built; export planned (step 5) |
| GitHub to Azure | Container images | OIDC federation; an identity limited to the registry (`AcrPush`, which can push and pull images), with no access to the database or Key Vault | Built |
| Customer to supplier support | Support requests | Email (planned helpdesk); **no patient data in tickets** ([support-access-policy.md](support-access-policy.md)) | Policy only |
| Supplier to customer at exit | Full export | Planned: an encrypted export (roadmap steps 7 and 8) | Planned |

**There are no other recipients.** MedApp has no integrations, email, SMS, analytics, advertising or third-party scripts today ([architecture handbook, 3.3](../architecture.md#33-scope-what-medapp-doesnt-do)).

## 4. Volumes (to fill per customer)

Article 35 asks whether processing is large scale. Fill these in for each customer:
- **Patients:** [number] a year, [number] in total.
- **Staff users:** [number].
- **Procedures:** [number] a year.
- **Geography:** [area served].

A single clinic may not be large scale on its own, but the supplier's processing across all customers is, which is why the supplier treats it as large scale.

## 5. What will change

The roadmap adds data. Each addition needs this inventory and the DPIA updated first:
- **Step 4:** staff display name, email, job title, professional registration number, MFA secrets and passkeys, last sign-in time; an email provider (a new sub-processor).
- **Step 7, built:** the activity log (who viewed, searched or changed which patient, with the filters used but not the search terms; sign-ins with IP addresses; account and role changes), restriction and dispute flags, the anonymised marker, and retention decisions (above). **Still to come:** note versions, contact preferences, a destruction log.
- **Step 8:** an organisation on every record.
- **Step 9:** NHS number, local hospital number, sex and gender, address, phone, registered GP practice, a deceased flag, documents and images.
