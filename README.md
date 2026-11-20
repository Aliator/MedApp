# MedApp

MedApp is a clinical workflow application for managing patients, referrals, pathways, appointments and laboratory work. The full application is server-backed; a browser-only lite demo is available on [GitHub Pages](https://aliator.github.io/MedApp/).

> **Demo safety:** The lite demo uses synthetic, made-up patient and staff data. Do not enter real patient details. Changes stay in the current browser and are not shared or synced to another device. Use **Reset** on the demo’s Read Me page to restore its starting data.

## Lite demo

The lite demo is a static build of the React web app. It has no server: API calls are answered by an in-browser stand-in, and demo state is stored in this browser (IndexedDB when available, otherwise in memory). Rules are simplified compared with the full app.

The demo offers three fixed accounts:

| Account | Username | What it can do |
|---|---|---|
| Centre administrator | `admin` | Read-only clinical access; can view centre setup, Insights and every team’s records. |
| Nadia Thornbury | `n.thornbury` | Dermatology clinic administrator and clinician; can do clinical work for Dermatology. |
| Ravi Aldous | `r.aldous` | Lab histopathologist; can work on lab tasks and authorise reports. |

The full application’s real sign-in, passwords and two-factor authentication are not part of the demo. The walkthrough on the demo’s **Read Me** page guides you through the app and these sample accounts.

### What the lite demo does not include

- **Real-time notifications:** the full app updates notifications over a live SignalR connection.
- **Real sign-in and two-factor authentication:** the full app has passwords, expiring sessions, lockout and authenticator- or email-based codes.
- **Email:** password reset, address confirmation and email sign-in codes are unavailable in the demo.
- **Reports as files and file uploads:** the full app supports lab and external reports, attachments, thumbnails, file validation and virus scanning.
- **Sharing across users or devices:** demo changes remain in the current browser; the full app shares records through its server.
- **Full server-side validation and concurrency controls:** the demo simplifies rules. The full app checks permissions, calendar clashes, locks and concurrent changes on the server.
- **Server rendering:** the full app renders pages with their data on the server; the Pages demo is client-only.
- **Activity logs, exports and data-request packs:** these full-app privacy features require the server.
- **Background jobs:** reminders, service alerts and data-retention jobs do not run in the demo.
- **Sending or printing letters:** the demo can record a letter as sent by hand, but cannot send or print it or generate PDFs.
- **Changing accounts and roles:** the demo’s three accounts are fixed, though their team memberships can be changed.

## Architecture

### Web app

- **React 19 and TypeScript, built with Vite:** one typed web app for the application’s screens.
- **Astryx design system:** shared UI components, themes, light and dark modes, and text sizes.
- **TanStack Router and Query:** page routing and data fetching; list filters are reflected in URLs, and data is cached and refreshed.
- **Server-rendered in the full app:** a Node page server renders each page before the browser takes over.

### API and data

- **.NET 10 Web API with Clean Architecture:** Domain, Application, Infrastructure and API layers with dependencies pointing inward.
- **CQRS with MediatR and FluentValidation:** use cases are handled by commands and queries with validators; controllers remain thin.
- **SQL Server and Entity Framework Core:** schema changes are made through reviewed migrations; edits use versions to prevent overwrites.
- **Azure Blob Storage:** file types are identified from file contents, not filenames.
- **SignalR:** live notifications in the full application.

### Security, privacy and clinical standards

- **Sign-in and sessions:** ASP.NET Core Identity, HttpOnly session cookies, two-factor authentication and lockout.
- **Permissions and team boundaries:** endpoints require named permissions; roles are configurable, and clinic records are protected by team boundaries.
- **Patient activity logging:** reads and changes are logged, and requests are refused if the activity cannot be recorded.
- **Privacy features:** record exports, anonymisation, retention review and data-request packs.
- **Content security policy:** pages use a strict policy that disallows inline scripts.
- **Clinical safety:** the project maintains a clinical safety case and hazard log (DCB0129 and DCB0160); the app records clinicians’ decisions and does not make them.
- **NHS standards:** NHS number checks, FHIR UK Core patient support, demographics lookup and SNOMED CT codes.
- **Lab requests:** hospital requests use FHIR UK Core lab request, specimen and report formats.

### Running and shipping

- **.NET Aspire:** starts SQL Server, storage, the API and web app for local development.
- **Docker, Helm and Kubernetes:** container image, Helm chart and local Kubernetes deployment support.
- **GitOps with Argo CD:** environment branches drive deployments; Azure is the cloud target.
- **Tests:** NUnit, Vitest and Testing Library, integration tests against SQL Server in containers, Playwright journeys and accessibility audits.
- **Accessibility:** the app is built toward WCAG 2.2 AA, including keyboard use, screen-reader announcements, high-contrast colours and larger text.

## Development

The React web app lives in [`MedApp.Web`](MedApp.Web/README.md). For instructions to run the full app locally, see the repository’s [`AGENTS.md`](AGENTS.md) and the web project README.

The lite demo can be run and built from `MedApp.Web`:

```bash
npm run dev:demo
npm run build:demo
```

The Pages workflow builds and deploys the demo from the `main` branch. Technical details, parity requirements and maintenance guidance are in [`docs/agents/light-demo.md`](docs/agents/light-demo.md).
