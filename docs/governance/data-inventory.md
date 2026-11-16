# Data inventory and data flows

**Draft prepared for review. It is not legal advice and must be reviewed and approved by a qualified data protection professional and the organisation's Data Protection Officer before use.**

| | |
|---|---|
| **Version** | 0.2 (draft), 16 November 2026: adds staff calendars and appointments |
| **Owner** | [DPO name], with the lead developer |
| **Checked against** | The code on `main` as of 16 November 2026: `MedApp.Domain`, `MedApp.Infrastructure/Data`, `MedApp.Application`, `MedApp.Web/src/app` and `deploy/`. The calendar and appointment entries follow their design (`docs/calendar-plan.md`, branch `feature/staff-calendars`); check them against the code before approval. |
| **Next review** | With every change that adds or changes a stored field, and at least every 6 months |

Every place MedApp stores or sends personal data, field by field, from the code. The ROPA ([ropa.md](ropa.md)), the DPIA ([dpia.md](dpia.md)) and the retention schedule ([retention-schedule.md](retention-schedule.md)) refer to it.

**Who the data is about:**
- **Patients** of the customer.
- **Staff users:** the customer's staff with MedApp accounts, including their calendars: when they're booked with patients, and when they're away or busy (annual leave, training).
- **Supplier staff:** only in the supplier's own systems (Azure, GitHub), not in MedApp's database.

## 1. The database (SQL Server; Azure SQL in production)

| Entity (table) | Personal data | About | Special category? | Notes |
|---|---|---|---|---|
| `Patient` (`Patients`) | `FirstName`, `LastName`, `DateOfBirth`, `Email`, `CreatedAt`, `LastUpdated` | Patients | No on its own; yes in combination with the procedures below | `MedApp.Domain/Patients/Patient.cs`. No NHS number, address, phone or sex today. |
| `PatientProcedure` (`PatientProcedures`) | `Title`, `Notes` (Markdown free text), `DueDateUtc`, `Priority`, `Status`, `TemplateName`, `PatientId`, timestamps | Patients | **Yes:** what procedure a patient is having, and clinical free text | `MedApp.Domain/Procedures/PatientProcedures/PatientProcedure.cs` |
| `PatientProcedureStage` (`PatientProcedureStages`) | `Notes` (clinical free text); `StartedByUserId`, `CompletedByUserId` and times; a copy of the stage's name, description and instructions | Patients; staff (who did what) | **Yes** (notes) | `PatientProcedureStage.cs`. The name, description and instructions are copied from the stage definition and hold no personal data unless an admin wrote some into the definition. |
| `PatientProcedureAssignment` (`PatientProcedureAssignments`) | `UserId`, `AssignedByUserId`, `AssignedAtUtc` | Staff; links to a patient | Indirectly | `PatientProcedureAssignment.cs` |
| `PatientProcedureAuditEntry` (`PatientProcedureAuditEntries`) | `PatientId`, `ProcedureTitle`, `StageName`, `UserId`, `Username`, `AtUtc`, `Action`, and `Details` (JSON) | Patients; staff | **Yes** | `PatientProcedureAuditEntry.cs`. **`Details` holds copies of clinical text:** `StageNotesUpdated` stores the old and new stage notes (`ProcedureChange.UpdateStageNotes`), and `DetailsUpdated` stores the old and new title and procedure notes (`UpdatePatientProcedureHandler`). Status changes store the reason for a block or cancellation. A stage finished without a visit stores that, the visit requirement and, when an admin finished one whose visit was required, the admin's reason (free text). No foreign keys: entries outlive the procedure and the user. |
| `Appointment` (`Appointments`) | `PatientId`, `ProcedureId`, `StageId`, `StaffUserId`, `StartUtc`, `EndUtc`, `Status` (requested, booked, attended, did not attend, cancelled), `Notes` (free text, 2,000 characters), `CancelReason` (free text, 500 characters), `IsAutomatic`, `CreatedByUserId`, `BookedByUserId`, `OutcomeByUserId` and their times | Patients; staff (who the patient is booked with, and who booked it) | **Yes:** that a patient is being seen, for which procedure and stage, and whether they attended. Notes and cancellation reasons are free text about the patient, treated like procedure notes | `MedApp.Domain/Appointments`. Foreign keys to the patient, procedure and stage; none to staff. A patient with appointments can't be deleted; deleting a procedure detaches its appointments. |
| `AppointmentEvent` (`AppointmentEvents`) | `AppointmentId`, `PatientId`, `ProcedureId`, `ProcedureTitle`, `StageName`, `Action`, `UserId`, `Username`, `AtUtc`, `StaffUserId`, `StaffUsername`, `StartUtc`, `EndUtc`, and `Details` (JSON) | Patients; staff | **Yes** | The appointment's history: requested, booked, rescheduled (with the old time and member of staff), attended, did not attend, cancelled (**with the reason**) and notes changed (**with the old and new notes**). **So it keeps copies of appointment notes and cancellation reasons,** like the procedure audit. No foreign keys: entries outlive the appointment, the procedure and the user. |
| `CalendarBlock` (`CalendarBlocks`) | `StaffUserId`, `ActivityTypeId`, `StartUtc`, `EndUtc`, `Note` (free text, 200 characters), `CreatedByUserId`, `CreatedAtUtc`, `LastUpdated` | Staff | Not by design. But **a block reveals when someone is away** (annual leave) or busy, and a note could reveal a staff member's health (for example "hospital appointment"). The UI tells staff not to put patient details in a note | `MedApp.Domain/Calendar`. Time blocked off on a member of staff's calendar. |
| `StaffWeekTemplate`, `CalendarDayOverride`, `CalendarBlockException` | `StaffUserId`, a week or day template, dates | Staff | No | A person's default week, and one day's changes to it. |
| `Notification` (`Notifications`) | `UserId`, `Title` (the procedure's title, or "Appointment"), `Message` (for example "[username] assigned you to this procedure.", or who booked, moved or cancelled an appointment, and its time), `ProcedureId`, `AppointmentId`, `CreatedAtUtc`, `ReadAtUtc` | Staff; patients indirectly | Possibly: a procedure title can name a condition or treatment | `MedApp.Domain/Notifications/Notification.cs`; written by `AssignPatientProcedureHandler` and the appointment handlers. **An appointment's notification never holds the patient's name or the appointment's notes** (`docs/calendar-plan.md`, decision 20). No foreign keys. |
| `UserSession` (`UserSessions`) | `UserId`, `CreatedAtUtc`, `ExpiresAtUtc`, `RevokedAtUtc`, `CreatedByIp` (IP address), `UserAgent` (browser) | Staff | No | `MedApp.Domain/Authentication/UserSession.cs`. The row's ID is the session cookie's value. Rows are never deleted today. |
| `UserSettings` (`UserSettings`) | `UserId`, `Json`, `UpdatedAtUtc` | Staff; patients indirectly | Possibly | `UserSettings.cs`. The JSON includes **saved searches ("favourites")**, whose terms can be patient names. |
| ASP.NET Core Identity (`AspNetUsers`, `AspNetUserRoles`, `AspNetRoles` and the other Identity tables) | `UserName`, `NormalizedUserName`, `PasswordHash`, `SecurityStamp`, `AccessFailedCount`, `LockoutEnd`, `CreatedAt`; `Email` and `PhoneNumber` columns exist but MedApp doesn't fill them | Staff | No | `MedApp.Infrastructure/Common/Identity/ApplicationUser.cs`. Passwords are stored only as Identity's salted hashes. |
| `PatientProcedureStageDefinition`, `PatientProcedureStageTemplate` and its map | Names, descriptions, instructions, whether a stage involves a visit | None, by design | No | Configuration. Admins should not write patient or staff details into them. |
| `CalendarActivityType`, `DayTemplate`, `DayTemplateBlock`, `WeekTemplate`, `WeekTemplateDay` | Names, colours, times, template block notes | None, by design | No | Calendar configuration. Admins should not write patient or staff details into template block notes. |

## 2. Outside the database

| Where | What | Personal data | Kept for |
|---|---|---|---|
| **The session cookie** `medapp.session` | The session's ID | Pseudonymous identifier for a staff user | Until the session ends (8 hours, extendable to 24 at most from sign-in) |
| **Browser `localStorage`** (`medapp.*` keys, `MedApp.Web/src/app`) | The account's settings (colour mode, colours, text size, list settings, accessibility), saved searches (`medapp.favourites`), and pinned patients and procedures as IDs (`medapp.pinnedPatients.<username>`, `MedApp.Web/src/components/navigation/pins.ts`) | Saved search terms can be patient names; pinned items are IDs; the pin keys contain the username | Until cleared. **Signing out doesn't clear it today** (roadmap step 7). Full list: [cookies-and-storage.md](cookies-and-storage.md). |
| **Browser `sessionStorage`** (`medapp.halfView`) | The page open in the half view | Can include a patient or procedure ID | Until the tab closes |
| **Browser history and address bar suggestions** | Every page URL visited. List pages keep their filters in the URL (`MedApp.Web/src/pages/listSearch.ts`), and the patient list's filters are first name, last name, email and a date of birth range, so a search becomes a URL like `/patients?last=…&dobFrom=…` | Patient names, emails and dates of birth searched for, and record IDs; on a shared computer, the next user can see who was searched for | Until the browser's history is cleared or expires. **Signing out doesn't clear it.** Planned (step 7): keep identifying filters out of the URL ([dpia.md](dpia.md), risk 10) |
| **The browser's memory** (TanStack Query cache) | Data of the pages viewed | Patients, procedures, calendars and appointments | Until sign-out, which clears it, or the tab closes |
| **Server-rendered pages** | HTML with the page's data | Patients, procedures | Not kept: sent with `Cache-Control: no-store` (`MedApp.API/ServerRendering/SsrPages.cs`) |
| **The Node page server** | Renders each page in memory, with a new router and cache per request | Transient | Not kept |
| **Valkey** (the SignalR backplane, in the cluster) | Notifications in transit between API copies | Procedure title, the username of whoever assigned, booked, moved or cancelled, an appointment's time; never a patient's name | Not kept: Valkey saves nothing to disk (`deploy/helm/medapp/templates/valkey.yaml`). Traffic inside the cluster isn't encrypted. |
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
| API to Valkey | Notifications (assignments and appointments) | Password, not encrypted; network policy | Built |
| API to logs | Logs | Inside Azure | Logging built; export planned (step 5) |
| GitHub to Azure | Container images | OIDC federation; an identity limited to the registry (`AcrPush`, which can push and pull images), with no access to the database or Key Vault | Built |
| Customer to supplier support | Support requests | Email (planned helpdesk); **no patient data in tickets** ([support-access-policy.md](support-access-policy.md)) | Policy only |
| Supplier to customer at exit | Full export | Planned: an encrypted export (roadmap steps 7 and 8) | Planned |

**There are no other recipients.** Appointments go nowhere outside MedApp: patients aren't sent reminders or confirmations. MedApp has no integrations, email, SMS, analytics, advertising or third-party scripts today ([architecture handbook, 3.3](../architecture.md#33-scope-what-medapp-doesnt-do)).

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
- **Step 7:** access audit (who viewed which patient, search terms), sign-in events with IP addresses, note versions, restriction and dispute flags, contact preferences, a destruction log.
- **Step 8:** an organisation on every record.
- **Step 9:** NHS number, local hospital number, sex and gender, address, phone, registered GP practice, a deceased flag, documents and images.
- **Calendar follow-ups** (`docs/todo.md`): reminders to patients by email or SMS need their phone numbers and contact preferences, and an email or SMS provider (a new sub-processor); self-booking needs patients to sign in.

## 6. Appointments and PR #68 (privacy features)

PR #68 adds anonymisation and the subject access export, and was written before appointments existed. **When it merges, it must also cover appointments:**
- **Anonymising a patient** clears their appointments' `Notes` and `CancelReason`, and the notes and reasons copied into their appointment history (`AppointmentEvent.Details`).
- **The subject access export** includes the patient's appointments and their appointment history.

Until then, appointments are outside both, and an export or anonymisation of a patient who has appointments needs the manual procedure in [data-subject-rights.md](data-subject-rights.md) for that part.
