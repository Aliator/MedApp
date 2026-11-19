import { rmSync } from 'node:fs';
import { expect, test as setup, type Browser, type Page, type TestInfo } from '@playwright/test';
import { authFile, field, settle, writeData, type Account, type ProjectMeta } from './shared';

/** Each account's .env keys: the admin and the clinic's member of staff are needed, the lab's is optional. */
const ACCOUNTS: { account: Account; username: string; password: string; required: boolean }[] = [
  { account: 'admin', username: 'PARITY_USERNAME', password: 'PARITY_PASSWORD', required: true },
  { account: 'clinic', username: 'PARITY_STAFF_USERNAME', password: 'PARITY_STAFF_PASSWORD', required: true },
  { account: 'lab', username: 'PARITY_LAB_USERNAME', password: 'PARITY_LAB_PASSWORD', required: false },
];

setup('sign in and find shared records', async ({ page, browser }, info) => {
  const { app, apiUrl } = info.project.metadata as ProjectMeta;

  // Each account signs in, in a browser of its own, and its session is saved for the screens captured as it.
  const pages: Partial<Record<Account, Page>> = {};
  for (const { account, username: usernameKey, password: passwordKey, required } of ACCOUNTS) {
    const username = process.env[usernameKey];
    const password = process.env[passwordKey];
    if (!username || !password) {
      if (required) throw new Error(`Set ${usernameKey} and ${passwordKey} in tools/ui-parity/.env (copy .env.example).`);
      rmSync(authFile(app, account), { force: true });
      continue;
    }
    const signedIn = account === 'admin' ? page : await newPage(browser, info);
    await signIn(signedIn, username, password, usernameKey);
    await signedIn.context().storageState({ path: authFile(app, account) });
    pages[account] = signedIn;
  }
  const admin = pages.admin!;
  // The clinical records are read as the clinic's member of staff, so the screens captured as them can open them.
  const clinic = pages.clinic!;

  // Pick the same patient in both apps: both read the same API and database.
  const response = await clinic.request.get(`${apiUrl}/api/patients`);
  expect(response.ok(), `GET ${apiUrl}/api/patients returned ${response.status()}`).toBeTruthy();
  const patients = (await response.json()) as { id: string; firstName: string; lastName: string }[];
  if (patients.length === 0) throw new Error('No patients in the database: the patient screens need at least one.');
  patients.sort((a, b) => `${a.lastName} ${a.firstName}`.localeCompare(`${b.lastName} ${b.firstName}`) || a.id.localeCompare(b.id));
  // And the first in-progress pathway by title of the clinic's, for the pathway page.
  const pathways = await clinic.request.get(
    `${apiUrl}/api/pathways/patient-pathways/search?Statuses=InProgress&Sort=title&PageSize=1`,
  );
  const pathwayId = pathways.ok() ? ((await pathways.json()) as { items: { id: string }[] }).items[0]?.id : undefined;
  // And a pathway of the first restricted patient (the demo data has some), for a page with a long page message (the
  // admin's screens: the admin sees every team's).
  const restricted = (patients as { id: string; isRestricted?: boolean }[]).find((p) => p.isRestricted);
  const restrictedPathways = restricted
    ? await admin.request.get(`${apiUrl}/api/pathways/patient-pathways/search?PatientId=${restricted.id}&Sort=title&PageSize=1`)
    : undefined;
  const restrictedPathwayId = restrictedPathways?.ok()
    ? ((await restrictedPathways.json()) as { items: { id: string }[] }).items[0]?.id
    : undefined;

  // And a meeting of the parity admin's own, declined, for the meeting screens (best effort: they skip without it).
  const meeting = await parityMeeting(admin, apiUrl, process.env.PARITY_USERNAME!).catch(() => undefined);

  // And a stage and a team to filter by (best effort: those screens show the list unfiltered without them).
  const stageId = await byName(clinic, `${apiUrl}/api/pathways/patient-pathway-stage/definitions`, 'Histology (lab)');
  const teamId = await byName(clinic, `${apiUrl}/api/teams`, 'Dermatology');

  // And a template that branches, with a pathway made from it (best effort: the branching screens skip without them).
  const branching = await branchingPathway(clinic, apiUrl).catch(() => undefined);

  writeData(app, {
    patientId: patients[0].id,
    pathwayId,
    restrictedPathwayId,
    meetingId: meeting?.id,
    meetingDate: meeting?.date,
    stageId,
    teamId,
    branchingTemplateId: branching?.templateId,
    branchingPathwayId: branching?.pathwayId,
  });
});

/** A page in a browser of its own, as the project's (its app's URL and window). */
async function newPage(browser: Browser, info: TestInfo): Promise<Page> {
  const context = await browser.newContext({
    baseURL: info.project.use.baseURL,
    viewport: info.project.use.viewport,
    ignoreHTTPSErrors: true,
  });
  return context.newPage();
}

/** Signs in through the login page. */
async function signIn(page: Page, username: string, password: string, usernameKey: string) {
  await page.goto('/login');
  await settle(page);
  await field(page, /username/i).fill(username);
  await field(page, /password/i).fill(password);
  await page.getByRole('button', { name: /sign in/i }).click();
  await expect(page, `still on /login: check ${usernameKey} and its password in .env`).not.toHaveURL(/\/login/, { timeout: 20_000 });
  await settle(page);
}

/** The ID of the item a list endpoint gives with this name, or else its first; undefined if it can't be read. */
async function byName(page: Page, url: string, name: string): Promise<string | undefined> {
  const response = await page.request.get(url);
  if (!response.ok()) return undefined;
  const items = (await response.json()) as { id: string; name: string }[];
  return (items.find((i) => i.name === name) ?? items[0])?.id;
}

const PARITY_MEETING = 'Parity check meeting (synthetic)';

interface ParityMeeting {
  id: string;
  date: string;
  startUtc: string;
  endUtc: string;
  title: string;
  myReply?: string | null;
}

/**
 * The parity account's own meeting in the next four weeks, which it has declined: found, or else arranged at its first
 * free half hour with nobody else going (so nobody is told), and declined. Run again, it finds the same one until it's
 * past, so the tool doesn't fill the calendar. Its notes say it's safe to cancel.
 */
async function parityMeeting(page: Page, apiUrl: string, username: string): Promise<ParityMeeting | undefined> {
  const staff = (await (await page.request.get(`${apiUrl}/api/calendar/staff`)).json()) as { userId: string; username: string }[];
  const me = staff.find((s) => s.username === username);
  if (!me) return undefined;

  const day = (offset: number) => new Date(Date.now() + offset * 86_400_000).toISOString().slice(0, 10);
  const range = `From=${day(0)}&To=${day(27)}`;
  const calendar = (await (
    await page.request.get(`${apiUrl}/api/calendar?StaffUserId=${me.userId}&${range}&WithoutAppointments=true`)
  ).json()) as { meetings?: ParityMeeting[] };
  let meeting = calendar.meetings?.find((m) => m.title === PARITY_MEETING && Date.parse(m.startUtc) > Date.now());

  if (!meeting) {
    const availability = (await (
      await page.request.get(`${apiUrl}/api/calendar/availability?StaffUserIds=${me.userId}&${range}&DurationMinutes=30`)
    ).json()) as { suggestions?: { startUtc: string; endUtc: string }[] };
    const slot = availability.suggestions?.[0];
    if (!slot) return undefined;
    const created = await page.request.post(`${apiUrl}/api/calendar/meetings`, {
      data: {
        title: PARITY_MEETING,
        notes: 'Made by the UI parity tool for its screenshots. Safe to cancel.',
        startUtc: slot.startUtc,
        endUtc: slot.endUtc,
        attendeeUserIds: [me.userId],
      },
    });
    if (!created.ok()) return undefined;
    meeting = (await created.json()) as ParityMeeting;
  }

  if (meeting.myReply !== 'Declined') {
    await page.request.put(`${apiUrl}/api/calendar/meetings/${meeting.id}/reply`, {
      data: { reply: 'Declined', expectedStartUtc: meeting.startUtc, expectedEndUtc: meeting.endUtc },
    });
  }
  return meeting;
}

/** The first template with a branch point, and its first pathway by title (open or not); undefined without one. */
async function branchingPathway(page: Page, apiUrl: string): Promise<{ templateId: string; pathwayId?: string } | undefined> {
  const response = await page.request.get(`${apiUrl}/api/pathways/patient-pathway-stage/templates`);
  if (!response.ok()) return undefined;
  const templates = (await response.json()) as { id: string; stages?: { choosesBranch?: boolean }[] }[];
  const template = templates.find((t) => (t.stages ?? []).some((s) => s.choosesBranch));
  if (!template) return undefined;
  const pathways = await page.request.get(
    `${apiUrl}/api/pathways/patient-pathways/search?TemplateId=${template.id}&IncludeClosed=true&Sort=title&PageSize=1`,
  );
  const pathwayId = pathways.ok() ? ((await pathways.json()) as { items: { id: string }[] }).items[0]?.id : undefined;
  return { templateId: template.id, pathwayId };
}
