/**
 * The full MedApp's architecture, in plain words, for the light demo's Read Me (`ReadMePage`): what the real app is
 * made of, beyond this browser-only version. Keep it true to the repository (AGENTS.md and docs/agents/).
 */
export interface ArchitecturePart {
  title: string;
  items: {label: string; description: string}[];
}

export const ARCHITECTURE: ArchitecturePart[] = [
  {
    title: 'The web app',
    items: [
      {label: 'React 19 and TypeScript, built with Vite', description: 'One app for every screen, typed end to end against the API.'},
      {label: 'Astryx design system', description: 'Every screen is built from its components, with colour themes, light and dark modes, and text sizes.'},
      {label: 'TanStack Router and Query', description: 'Pages and their data: lists keep their filters in the address, and data is cached and refreshed.'},
      {label: 'Rendered on a server first', description: 'A Node page server renders each page, then the browser takes over, so pages appear fast.'},
    ],
  },
  {
    title: 'The API',
    items: [
      {label: '.NET 10 Web API, Clean Architecture', description: 'Domain, Application, Infrastructure and API layers, with dependencies pointing inward.'},
      {label: 'CQRS with MediatR and FluentValidation', description: 'Each use case is one command or query, with its handler and validator; controllers stay thin.'},
      {label: 'SQL Server through Entity Framework Core', description: 'Every schema change is a reviewed migration; edits carry a version, so two people never overwrite each other.'},
      {label: 'Files in Azure Blob Storage', description: 'A file’s type is read from its contents, never its name, and files only ever leave as downloads.'},
      {label: 'Live notifications with SignalR', description: 'The bell updates the moment something happens, on any page.'},
    ],
  },
  {
    title: 'Security and privacy',
    items: [
      {label: 'Sign-in and sessions', description: 'ASP.NET Core Identity, HttpOnly session cookies, two-factor by authenticator app or emailed codes, and lockout.'},
      {label: 'Permissions, not job titles', description: 'Every endpoint names the permission it needs; roles are data admins edit; each clinic’s records sit behind its team’s wall.'},
      {label: 'Every patient read is logged', description: 'The activity log records each read and change of patient data, and refuses the request if it can’t.'},
      {label: 'Privacy by design', description: 'Record exports, anonymisation, a retention review, and a pack for a patient’s request for their data.'},
      {label: 'Locked-down pages', description: 'A strict content security policy: no inline scripts, and no free text in notifications or logs.'},
    ],
  },
  {
    title: 'Clinical safety and NHS standards',
    items: [
      {label: 'Clinical safety case', description: 'A hazard log and safety case (DCB0129 and DCB0160); MedApp records clinicians’ decisions and never makes them.'},
      {label: 'NHS numbers, FHIR and SNOMED CT', description: 'Checked NHS numbers, a FHIR UK Core patient endpoint, a demographics (PDS) lookup, and SNOMED CT codes on stages and tests.'},
      {label: 'Lab requests in NHS shapes', description: 'Hospital requests follow FHIR UK Core’s lab request, specimen and report, ready for a real hospital link.'},
    ],
  },
  {
    title: 'Running and shipping it',
    items: [
      {label: '.NET Aspire for local development', description: 'One command starts SQL Server, storage, the API and the web app, with a dashboard.'},
      {label: 'Docker, Helm and Kubernetes', description: 'One container image, a locked-down Helm chart, and a local Kubernetes cluster to try it on.'},
      {label: 'GitOps with Argo CD', description: 'Deploys come from environment branches that Argo CD keeps the cluster in step with; Azure is the cloud target.'},
      {label: 'GitHub Actions in tiers', description: 'Quick checks on every change; the full suite (unit, integration and end-to-end tests, an image scan) when asked.'},
      {label: 'Tests at every level', description: 'NUnit, Vitest and Testing Library, integration tests against real SQL Server in containers, Playwright journeys, and accessibility audits.'},
      {label: 'Accessible by aim', description: 'Built to WCAG 2.2 AA: keyboard use, screen reader announcements, high-contrast colours and larger text.'},
    ],
  },
];
