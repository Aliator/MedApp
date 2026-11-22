/**
 * Every API endpoint the light demo deliberately doesn't answer (it gets the 501 "Not available in the light demo"),
 * each with a one-line reason. `parity.test.ts` fails unless every path and method in `src/api/schema.d.ts` either has
 * a handler (`src/demo/handlers/`) or is listed here, so a PR that adds an endpoint has to decide which, in the same
 * PR. Prefer a handler with simplified rules; list an endpoint here only when the demo can't do it (files, email,
 * background jobs, exports, real sign-in) or it's an action no page offers in the demo. Keys are `METHOD /api/path`,
 * as in the schema.
 */
export const notInLightDemo: Record<string, string> = {
  // Sign-in, two-factor and email: the demo has four accounts and no real sign-in or email.
  'POST /api/auth/email/confirm': 'No email in the light demo.',
  'PUT /api/auth/me/email': 'No email in the light demo.',
  'DELETE /api/auth/me/email': 'No email in the light demo.',
  'POST /api/auth/login/two-factor': 'No real sign-in: the demo accounts have no second step.',
  'POST /api/auth/login/two-factor/resend': 'No real sign-in, and no email.',
  'POST /api/auth/me/two-factor/authenticator': 'No real sign-in: two-factor isn’t set up in the demo.',
  'POST /api/auth/me/two-factor/enable': 'No real sign-in: two-factor isn’t set up in the demo.',
  'POST /api/auth/me/two-factor/email': 'No email in the light demo.',
  'POST /api/auth/me/two-factor/email/code': 'No email in the light demo.',
  'POST /api/auth/me/two-factor/email/enable': 'No email in the light demo.',
  'POST /api/auth/me/two-factor/disable': 'No real sign-in: two-factor isn’t set up in the demo.',
  'POST /api/auth/me/two-factor/recovery-codes': 'No real sign-in: two-factor isn’t set up in the demo.',
  'POST /api/auth/password/forgot': 'No passwords or email in the light demo.',
  'POST /api/auth/password/reset': 'No passwords or email in the light demo.',

  // Files: nothing is uploaded or stored in the light demo.
  'POST /api/attachments': 'No file uploads in the light demo.',
  'GET /api/attachments/{id}/content': 'No files in the light demo.',
  'GET /api/attachments/{id}/thumbnail': 'No files in the light demo.',
  'DELETE /api/attachments/{id}': 'No files in the light demo.',
  'PUT /api/attachments/{id}/clinical-photo': 'No files in the light demo: a clinical photo is an attached picture marked as one (its consent form is seeded).',

  // NHS links: no national services from a static site.
  'GET /api/fhir/metadata': 'FHIR is for other systems, not a page; no server in the light demo.',
  'GET /api/fhir/Patient': 'FHIR is for other systems, not a page; no server in the light demo.',
  'GET /api/fhir/Patient/{id}': 'FHIR is for other systems, not a page; no server in the light demo.',
  'POST /api/demographics/lookup': 'No demographics service (PDS) from the light demo.',

  // Pathways (the stage library, templates, stages, notes).

  // Lab: specimens, specimen types, lab tests and results (and referrals).
  'POST /api/specimens/{id}/report': 'No file uploads in the light demo: a sample’s report is a file the lab attaches.',
  'POST /api/results/external': 'No files in the light demo: a result from elsewhere is an attached file marked as one.',

  // Clinical: letters, letter templates, the MDT list, consent forms and records.
  'GET /api/letters/{id}/preview': 'No PDFs in the light demo: a draft’s preview is a PDF the server renders.',
  'POST /api/letter-templates/preview': 'No PDFs in the light demo: a template’s preview is a PDF the server renders.',
  'POST /api/consent-records/{id}/scan': 'No files in the light demo: a consent form’s scan is an uploaded file.',

  // Setup and admin: practices, sites, rooms, hours, clinics, teams, roles, users, monitoring, logs, jobs, privacy.
  'POST /api/auth/users': 'The demo centre’s people are fixed: no new account can sign in (no real sign-in or passwords).',
  'DELETE /api/auth/users/{username}': 'The demo centre’s people are fixed: accounts aren’t deleted.',
  'PUT /api/auth/users/{username}/update-password': 'No passwords in the light demo.',
  'PUT /api/auth/users/{username}/reset-password': 'No passwords in the light demo.',
  'POST /api/auth/users/{username}/reset-two-factor': 'No real sign-in: two-factor isn’t set up in the demo.',
  'DELETE /api/auth/users/{username}/email': 'No email in the light demo.',
  'POST /api/jobs/{name}/run': 'Jobs run on the server, in the background: the light demo has none.',
};
