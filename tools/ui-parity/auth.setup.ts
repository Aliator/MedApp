import { expect, test as setup, type Page } from '@playwright/test';
import { authFile, field, settle, writeData, type ProjectMeta } from './shared';

setup('sign in and find shared records', async ({ page }, info) => {
  const { app, apiUrl } = info.project.metadata as ProjectMeta;
  const username = process.env.PARITY_USERNAME;
  const password = process.env.PARITY_PASSWORD;
  if (!username || !password) {
    throw new Error('Set PARITY_USERNAME and PARITY_PASSWORD in tools/ui-parity/.env (copy .env.example).');
  }

  await page.goto('/login');
  await settle(page);
  await field(page, /username/i).fill(username);
  await field(page, /password/i).fill(password);
  await page.getByRole('button', { name: /sign in/i }).click();
  await expect(page, 'still on /login: check the credentials in .env').not.toHaveURL(/\/login/, { timeout: 20_000 });
  await settle(page);
  await page.context().storageState({ path: authFile(app) });

  // Pick the same patient in both apps: both read the same API and database.
  const response = await page.request.get(`${apiUrl}/api/patients`);
  expect(response.ok(), `GET ${apiUrl}/api/patients returned ${response.status()}`).toBeTruthy();
  const patients = (await response.json()) as { id: string; firstName: string; lastName: string }[];
  if (patients.length === 0) throw new Error('No patients in the database: the patient screens need at least one.');
  patients.sort((a, b) => `${a.lastName} ${a.firstName}`.localeCompare(`${b.lastName} ${b.firstName}`) || a.id.localeCompare(b.id));
  // And the first in-progress pathway by title, for the pathway page.
  const pathways = await page.request.get(
    `${apiUrl}/api/pathways/patient-pathways/search?Statuses=InProgress&Sort=title&PageSize=1`,
  );
  const pathwayId = pathways.ok() ? ((await pathways.json()) as { items: { id: string }[] }).items[0]?.id : undefined;
  // And a pathway of the first restricted patient (the demo data has some), for a page with a long page message.
  const restricted = (patients as { id: string; isRestricted?: boolean }[]).find((p) => p.isRestricted);
  const restrictedPathways = restricted
    ? await page.request.get(`${apiUrl}/api/pathways/patient-pathways/search?PatientId=${restricted.id}&Sort=title&PageSize=1`)
    : undefined;
  const restrictedPathwayId = restrictedPathways?.ok()
    ? ((await restrictedPathways.json()) as { items: { id: string }[] }).items[0]?.id
    : undefined;

  // And a meeting of the parity account's own, declined, for the meeting screens (best effort: they skip without it).
  const meeting = await parityMeeting(page, apiUrl, username).catch(() => undefined);

  writeData(app, { patientId: patients[0].id, pathwayId, restrictedPathwayId, meetingId: meeting?.id, meetingDate: meeting?.date });
});

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
