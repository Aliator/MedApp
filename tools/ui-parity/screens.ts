import { test, type Page } from '@playwright/test';
import { ALERT, DIALOG, field, rowAction, settle, waitVisible, type AppName, type ParityData } from './shared';

export interface Screen {
  /** Folder name under screenshots/, the same for both apps. */
  name: string;
  /** What the screen shows, for the report. */
  title: string;
  path: string | ((data: ParityData) => string);
  /** Signed out (login screens). Default: signed in. */
  signedOut?: boolean;
  /**
   * Steps after the page loads, such as opening a dialog. Selectors are role/label based so the same
   * steps work in both apps while the labels match; branch on `app` only where the two genuinely differ.
   * These screens never confirm a destructive action: dialogs are only opened.
   */
  prepare?: (page: Page, ctx: { app: AppName; data: ParityData }) => Promise<void>;
  /** The app settings the screen needs (React), e.g. a switch in Account Settings turned on (see `useSettings`). */
  settings?: Record<string, string>;
}

/**
 * Changing a pathway that isn't assigned to you first asks "Not assigned to you". Continue only opens the
 * next dialog; nothing is changed until that one is confirmed, which these screens never do.
 */
async function passNotAssignedCheck(page: Page) {
  const proceed = page.locator(DIALOG).getByRole('button', { name: 'Continue' });
  const asked = await proceed.waitFor({ state: 'visible', timeout: 1500 }).then(() => true, () => false);
  if (asked) await proceed.click();
}

/**
 * Opens the first referral in the list of every referral (`view=all`), as the parity data names none: React only, and
 * only when there is one (otherwise the screen is the list, saying there are none).
 */
async function openFirstReferral(page: Page) {
  const first = page.getByRole('table').getByRole('link').first();
  if (await first.waitFor({ state: 'visible', timeout: 5000 }).then(() => true, () => false)) {
    await first.click();
    await page.getByRole('heading', { name: /^Referral to/, level: 1 }).waitFor();
    await settle(page);
  }
}

/**
 * Opens the pathway of the first referral on the waiting list (accepted, its clock not yet stopped), through the
 * referral's "Open its pathway" link, as the parity data names none: React only, and only when there is one (otherwise
 * the screen is the list or the referral). Ends on the pathway's page, with its Referral and wait section.
 */
async function openFirstWaitingPathway(page: Page) {
  await openFirstReferral(page);
  const pathway = page.getByRole('link', { name: 'Open its pathway' });
  if (await pathway.waitFor({ state: 'visible', timeout: 3000 }).then(() => true, () => false)) {
    await pathway.click();
    await page.getByRole('heading', { name: 'Referral and wait' }).waitFor();
    await settle(page);
  }
}

/**
 * A day in the next Christmas week still to come: 28 December, whose Monday-to-Sunday week always holds Christmas Day,
 * Boxing Day or a substitute for one (England and Wales), so it always shows a closed day. This year's until the 28th.
 */
function christmasWeekDate(now: Date = new Date()): string {
  const year = now.getUTCMonth() === 11 && now.getUTCDate() > 28 ? now.getUTCFullYear() + 1 : now.getUTCFullYear();
  return `${year}-12-28`;
}

export const screens: Screen[] = [
  { name: 'login', title: 'Login', path: '/login', signedOut: true },
  {
    name: 'login-error',
    title: 'Login with wrong credentials',
    path: '/login',
    signedOut: true,
    prepare: async (page) => {
      // An account that doesn't exist, so the real parity account can never be locked out.
      await field(page, /username/i).fill('parity-no-such-user');
      await field(page, /password/i).fill('not-the-password');
      await page.getByRole('button', { name: /sign in/i }).click();
      await waitVisible(page, ALERT);
    },
  },
  {
    name: 'forgot-password',
    title: 'Forgot your password: a reset link by email (or, without email, who to ask)',
    path: '/forgot-password',
    signedOut: true,
  },
  {
    name: 'reset-password-no-link',
    title: 'Choose a new password, opened without its emailed link',
    path: '/reset-password',
    signedOut: true,
    prepare: async (page) => {
      await waitVisible(page, ALERT);
    },
  },
  {
    name: 'confirm-email-no-link',
    title: 'Confirm your email address, opened without its emailed link',
    path: '/confirm-email',
    signedOut: true,
    prepare: async (page) => {
      await waitVisible(page, ALERT);
    },
  },
  {
    name: 'confirm-email-sign-in',
    title: 'Confirm your email address, opened from a (synthetic) link while signed out: asks to sign in first',
    path: '/confirm-email#user=00000000-0000-0000-0000-000000000000&token=synthetic',
    signedOut: true,
    prepare: async (page) => {
      await page.getByText('Sign in to confirm your email address').waitFor();
    },
  },
  { name: 'home', title: "Home: a welcome and today's calendar (the MedApp heading leads here)", path: '/' },
  {
    name: 'calendar-week',
    title: 'Calendar: my week, appointments and blocked time on the grid, with its toolbar',
    path: '/calendar',
    prepare: async (page) => {
      await page.getByRole('group', { name: /^My calendar,/ }).waitFor();
      await settle(page);
    },
  },
  {
    name: 'calendar-day',
    title: 'Calendar: my day, with Apply Template beside New, and Default week',
    path: '/calendar?view=day',
    prepare: async (page) => {
      await page.getByRole('group', { name: /^My calendar,/ }).waitFor();
      await settle(page);
    },
  },
  {
    name: 'calendar-narrow',
    title: 'Calendar on a narrow window: the day view, the toolbar wrapping',
    path: '/calendar',
    prepare: async (page) => {
      await page.setViewportSize({ width: 900, height: 900 });
      await page.getByRole('group', { name: /^My calendar,/ }).waitFor();
      await settle(page);
    },
  },
  {
    name: 'calendar-narrow-week',
    title: 'Calendar on a narrow window: the compact week, Monday to Friday with items on one line',
    path: '/calendar?view=week',
    prepare: async (page) => {
      await page.setViewportSize({ width: 900, height: 900 });
      await page.getByRole('group', { name: /^My calendar,/ }).waitFor();
      await settle(page);
    },
  },
  {
    name: 'calendar-closed-day-week',
    title: 'Calendar: the Christmas week, with its bank holiday greyed as "Closed: …" (the demo data saves bank holidays as closed days)',
    path: () => `/calendar?view=week&date=${christmasWeekDate()}`,
    prepare: async (page) => {
      await page.getByRole('group', { name: /^My calendar,/ }).waitFor();
      await page.getByText(/^Closed: /).first().waitFor();
      await settle(page);
    },
  },
  {
    name: 'calendar-5min-rows',
    title: 'Calendar with 5-minute rows: the week three times taller, scrolling, with quarter hours down the side',
    path: '/calendar?rows=5',
    prepare: async (page) => {
      await page.getByRole('group', { name: /^My calendar,/ }).waitFor();
      await settle(page);
    },
  },
  {
    name: 'calendar-book-dialog',
    title: 'Book Appointment from New: who, with whom and how long beside the free days and times (not saved)',
    path: '/calendar',
    prepare: async (page) => {
      await page.getByRole('button', { name: 'New' }).click();
      await page.getByRole('menuitem', { name: 'Book appointment' }).click();
      await waitVisible(page, DIALOG);
      await settle(page);
    },
  },
  {
    name: 'calendar-book-clinic-slots',
    title: 'Book Appointment from New, on Clinic slots: the sessions with free slots in the next four weeks (not saved)',
    path: '/calendar',
    prepare: async (page) => {
      await page.getByRole('button', { name: 'New' }).click();
      await page.getByRole('menuitem', { name: 'Book appointment' }).click();
      await waitVisible(page, DIALOG);
      await page.locator(DIALOG).getByRole('button', { name: 'Clinic slots' }).click();
      await settle(page);
    },
  },
  {
    name: 'clinics-week',
    title: "Clinics: this week's clinic sessions by day, time and room, with how full each is",
    path: '/calendar/clinics',
    prepare: async (page) => {
      await page.getByRole('heading', { name: 'Clinics', level: 1 }).waitFor();
      await settle(page);
    },
  },
  {
    name: 'clinics-session-dialog',
    title: "A clinic session's dialog: its details and each slot, free or with its patient (the first session this week, if any)",
    path: '/calendar/clinics',
    prepare: async (page) => {
      const open = page.getByRole('button', { name: /^Open the / }).first();
      if (await open.waitFor({ state: 'visible', timeout: 5000 }).then(() => true, () => false)) {
        await open.click();
        await waitVisible(page, DIALOG);
      }
      await settle(page);
    },
  },
  {
    name: 'clinics-new-session-dialog',
    title: 'New Clinic Session: clinician, day, start and end in their working day, slot length, room and specialty (not saved)',
    path: '/calendar/clinics',
    prepare: async (page) => {
      await page.getByRole('button', { name: 'New Session' }).click();
      await waitVisible(page, DIALOG);
      await settle(page);
    },
  },
  {
    name: 'clinic-patterns',
    title: 'Clinic Patterns: clinics that repeat, each with Edit and Create Sessions',
    path: '/calendar/clinics/patterns',
    prepare: async (page) => {
      await page.getByRole('heading', { name: 'Clinic Patterns', level: 1 }).waitFor();
      await settle(page);
    },
  },
  {
    name: 'clinic-pattern-dialog',
    title: 'New Clinic Pattern: clinician, day and times, slots, how often, from and until, room and specialty (not saved)',
    path: '/calendar/clinics/patterns',
    prepare: async (page) => {
      await page.getByRole('button', { name: 'New Pattern' }).click();
      await waitVisible(page, DIALOG);
      await settle(page);
    },
  },
  {
    name: 'calendar-record-past-dialog',
    title: 'Record Past Appointment from New: who, with whom, the day in the last 30 days, start, length, outcome and notes (not saved)',
    path: '/calendar',
    prepare: async (page) => {
      await page.getByRole('button', { name: 'New' }).click();
      await page.getByRole('menuitem', { name: 'Record past appointment' }).click();
      await waitVisible(page, DIALOG);
      await settle(page);
    },
  },
  {
    name: 'calendar-block-dialog',
    title: 'Block Time from New: activity, day, start, end and note (not saved)',
    path: '/calendar',
    prepare: async (page) => {
      await page.getByRole('button', { name: 'New' }).click();
      await page.getByRole('menuitem', { name: 'Block time' }).click();
      await waitVisible(page, DIALOG);
      await settle(page);
    },
  },
  {
    name: 'calendar-apply-template',
    title: "Apply Template: a day template, No template or the default week's, on ticked days of the week (not applied)",
    path: '/calendar',
    prepare: async (page) => {
      await page.getByRole('button', { name: 'Apply Template' }).click();
      await waitVisible(page, DIALOG);
      await page.locator(DIALOG).getByRole('group', { name: /Days/ }).waitFor();
      await settle(page);
    },
  },
  {
    name: 'calendar-team',
    title: "Calendar's team view (admins): everyone free, then a column per member of staff, scrolling sideways",
    path: '/calendar?view=team',
    prepare: async (page) => {
      await page.getByRole('group', { name: /^Team/ }).waitFor();
      await settle(page);
    },
  },
  {
    name: 'calendar-new-meeting',
    title: 'New Meeting from the team view: who, length, notes and suggested times (not saved)',
    path: '/calendar?view=team',
    prepare: async (page) => {
      await page.getByRole('button', { name: 'New' }).click();
      await page.getByRole('menuitem', { name: 'New meeting' }).click();
      await waitVisible(page, DIALOG);
      await page.locator(DIALOG).getByRole('group', { name: 'Suggested times' }).waitFor();
      await settle(page);
    },
  },
  {
    name: 'meeting-replies',
    title: "A meeting's dialog for an attendee: everyone's reply under a summary, and Accept and Decline (Decline pressed)",
    path: (d) => `/calendar?date=${d.meetingDate}&view=day&meeting=${d.meetingId}`,
    prepare: async (page, { data }) => {
      test.skip(!data.meetingId, "No meeting of the parity account's own (the setup makes one when it can).");
      await waitVisible(page, DIALOG);
      await page.locator(DIALOG).getByRole('group', { name: 'Your reply' }).waitFor();
      await settle(page);
    },
  },
  {
    name: 'calendar-declined-meeting',
    title: 'Calendar: a meeting I declined, faded at the end of its column and saying Declined, its time free to select',
    path: (d) => `/calendar?date=${d.meetingDate}&view=day`,
    prepare: async (page, { data }) => {
      test.skip(!data.meetingId, "No meeting of the parity account's own (the setup makes one when it can).");
      await page.getByRole('group', { name: /^My calendar,/ }).waitFor();
      await page.locator('[data-faded]').first().waitFor();
      await settle(page);
    },
  },
  { name: 'requires-appointment', title: 'Requires Appointment: appointments asked for but not yet booked, soonest due first', path: '/calendar/requires-appointment' },
  { name: 'my-tasks', title: 'My Worklist: my task lists (the Blazor app home page)', path: '/tasks' },
  {
    name: 'my-tasks-narrow',
    title: 'My Worklist on a narrow window: the same list, with its search',
    path: '/tasks',
    prepare: async (page) => {
      await page.setViewportSize({ width: 900, height: 900 });
      await settle(page);
    },
  },
  {
    name: 'my-tasks-favourites',
    title: "My Worklist's favourites open (the star beside the search): Not started, Started, ...",
    path: '/tasks',
    prepare: async (page) => {
      await page.getByRole('button', { name: 'Favourites' }).click();
      await page.getByRole('menuitem', { name: 'Started', exact: true }).waitFor();
    },
  },
  { name: 'my-tasks-started', title: 'My Worklist, the started ones (from the favourites)', path: '/tasks?progress=started' },
  { name: 'account', title: 'Account', path: '/account' },
  {
    name: 'notifications',
    title: "Notifications, open from the bell beside MedApp (the parity sign-in has none: the empty list)",
    path: '/',
    prepare: async (page) => {
      await page.getByRole('button', { name: /^Notifications/ }).click();
      await page.getByRole('dialog', { name: 'Notifications' }).waitFor();
    },
  },
  { name: 'accessibility-statement', title: 'Accessibility statement (from Account Settings, Accessibility)', path: '/accessibility' },
  {
    name: 'keyboard-shortcuts',
    title: 'Keyboard shortcuts, listed on ? (once turned on, in Account Settings, Accessibility)',
    path: '/',
    // They start off.
    settings: { 'medapp.accessibility': JSON.stringify({ shortcuts: true }) },
    prepare: async (page) => {
      await page.keyboard.press('?');
      await page.getByRole('dialog', { name: 'Keyboard shortcuts' }).waitFor();
    },
  },
  { name: 'patients-list', title: 'Patients list, page 1', path: '/patients' },
  {
    name: 'patients-search',
    title: 'Patients search: its filters as tokens, and the field list open to add another',
    path: '/patients?last=bennett&dobFrom=1950-01-01',
    prepare: async (page) => {
      await page.getByRole('combobox', { name: 'Search patients' }).click();
      await settle(page);
    },
  },
  { name: 'patients-sorted', title: 'Patients sorted by date of birth, newest first', path: '/patients?sort=DateOfBirth&asc=false' },
  { name: 'patient-view', title: 'View patient: details and files beside the timeline (what is due, filters, the feed by day)', path: (d) => `/patients/${d.patientId}` },
  {
    name: 'patient-view-narrow',
    title: 'View patient on a narrow window: one card, Details, Files and Timeline folding',
    path: (d) => `/patients/${d.patientId}`,
    prepare: async (page) => {
      await page.setViewportSize({ width: 900, height: 900 });
      await settle(page);
    },
  },
  {
    name: 'patient-timeline-filtered',
    title: 'View patient, the timeline filtered to stages',
    path: (d) => `/patients/${d.patientId}`,
    prepare: async (page) => {
      await page.getByRole('button', { name: 'Stages', exact: true }).click();
      await settle(page);
    },
  },
  { name: 'patient-pathways', title: "A patient's pathways", path: (d) => `/patients/${d.patientId}/pathways` },
  { name: 'pathways-list', title: 'All pathways, page 1', path: '/pathways' },
  {
    name: 'pathways-search',
    title: 'Pathways search: "is any of" and "is not any of" tokens, and the field list open',
    path: '/pathways?status=InProgress,Blocked&priority=!Low',
    prepare: async (page) => {
      await page.getByRole('combobox', { name: 'Search pathways' }).click();
      await settle(page);
    },
  },
  {
    name: 'pathways-stage-reached',
    title: 'Pathways waiting for histology: the Stage reached and Specialty filters ("Waiting for histology")',
    path: (d) => `/pathways?mine=false${d.stageId ? `&stage=${d.stageId}` : ''}${d.specialtyId ? `&specialty=${d.specialtyId}` : ''}`,
  },
  { name: 'referrals-list', title: 'Referrals: the inbox of referrals awaiting triage, longest wait first', path: '/referrals' },
  {
    name: 'referrals-waiting',
    title: 'Referrals: the waiting list (accepted, the clock not stopped), longest wait first, with each wait and target date',
    path: '/referrals?view=waiting',
  },
  {
    name: 'referrals-paused',
    title: 'Referrals: the paused clocks, by review date, past ones flagged Review overdue (hazard H-42)',
    path: '/referrals?view=paused',
  },
  {
    name: 'referral-register',
    title: 'Register referral for a patient: the banner, their referrals awaiting triage, and the details from the letter',
    path: (d) => `/referrals/new?patientId=${d.patientId}`,
  },
  {
    name: 'referral-view',
    title: 'A referral: the banner, its details, the letter, triage and its history (the first in the list of all)',
    path: '/referrals?view=all',
    prepare: async (page) => openFirstReferral(page),
  },
  {
    name: 'referral-withdraw',
    title: 'Withdrawing a referral: the dialog asks why, showing the patient and the referral (only opened, never confirmed)',
    path: '/referrals',
    prepare: async (page) => {
      await openFirstReferral(page);
      const more = page.getByRole('button', { name: 'More' });
      if (await more.waitFor({ state: 'visible', timeout: 3000 }).then(() => true, () => false)) {
        await more.click();
        await page.getByRole('menuitem', { name: /Withdraw/ }).click();
        await waitVisible(page, DIALOG);
      }
    },
  },
  {
    name: 'referral-accept',
    title:
      "Accepting a referral: the patient, the specialty's templates by name, an empty priority beside the one asked for (only opened, never confirmed; the page alone if the account can't triage it)",
    path: '/referrals',
    prepare: async (page) => {
      await openFirstReferral(page);
      // Only a member of the referral's specialty with Triage referrals sees Accept; the admin role doesn't have it.
      const accept = page.getByRole('button', { name: 'Accept', exact: true });
      if (await accept.waitFor({ state: 'visible', timeout: 3000 }).then(() => true, () => false)) {
        await accept.click();
        await waitVisible(page, DIALOG);
        await settle(page);
      }
    },
  },
  { name: 'pathway-view', title: 'Pathway page: details, current stage and files beside the stages', path: (d) => `/pathways/${d.pathwayId}` },
  {
    name: 'pathway-referral-wait',
    title:
      "A pathway from a referral: its Referral and wait section, with the clock (day N of the target, Breached, Paused), the pauses and Pause or Resume Clock (the waiting list's first)",
    path: '/referrals?view=waiting',
    prepare: async (page) => openFirstWaitingPathway(page),
  },
  {
    name: 'pathway-pause-clock',
    title:
      "Pausing a referral's waiting-time clock from its pathway: the patient, why (a fixed choice), a detail and the review date (only opened, never confirmed)",
    path: '/referrals?view=waiting',
    prepare: async (page) => {
      await openFirstWaitingPathway(page);
      const pause = page.getByRole('button', { name: 'Pause Clock' });
      if (await pause.waitFor({ state: 'visible', timeout: 3000 }).then(() => true, () => false)) {
        await pause.click();
        await waitVisible(page, DIALOG);
        await settle(page);
      }
    },
  },
  {
    name: 'pathway-notes',
    title: "Pathway page with its Notes: Add Note, then the notes newest first, each with who and when (Details folded to show them)",
    path: (d) => `/pathways/${d.pathwayId}`,
    prepare: async (page) => {
      await page.getByRole('button', { name: 'Hide details' }).click();
      await page.getByRole('button', { name: 'Add Note' }).waitFor({ state: 'visible' });
      await settle(page);
    },
  },
  {
    name: 'notepad-open',
    title: "The notepad docked on the right of a pathway's page (opened from the side nav), the page beside it (narrow below 1,784 px)",
    path: (d) => `/pathways/${d.pathwayId}`,
    prepare: async (page) => {
      await page.getByRole('navigation', { name: 'Side navigation' }).first().getByRole('button', { name: 'Notepad' }).click();
      await page.getByRole('region', { name: 'Notepad' }).getByRole('textbox', { name: 'Notepad' }).waitFor({ state: 'visible' });
      await settle(page);
    },
  },
  {
    name: 'notepad-add-note',
    title: "The notepad's Add Note to Pathway confirmation: the patient as the banner shows them, the pathway, the text, and Remove it from the notepad (only opened, never confirmed)",
    path: (d) => `/pathways/${d.pathwayId}`,
    prepare: async (page) => {
      await page.getByRole('navigation', { name: 'Side navigation' }).first().getByRole('button', { name: 'Notepad' }).click();
      const notepad = page.getByRole('region', { name: 'Notepad' });
      const box = notepad.getByRole('textbox', { name: 'Notepad' });
      await box.waitFor({ state: 'visible' });
      // Whatever the account's notepad holds is what would be added; type a line if it's empty, so there's something to show.
      if (!(await box.innerText()).trim()) {
        await box.click();
        await page.keyboard.type('Synthetic jotting for the parity screenshot.');
      }
      await notepad.getByRole('button', { name: 'Add Note to Pathway' }).click();
      await waitVisible(page, DIALOG);
      await settle(page);
    },
  },
  {
    name: 'notepad-half',
    title: "The notepad in a half view (the side nav's Notepad row's menu), beside a pathway: the notepad filling the right half",
    path: (d) => `/pathways/${d.pathwayId}`,
    prepare: async (page) => {
      await page.evaluate(() => sessionStorage.setItem('medapp.halfView', '/notepad'));
      await page.reload();
      await page.getByRole('region', { name: 'Notepad' }).getByRole('textbox', { name: 'Notepad' }).waitFor({ state: 'visible' });
      await settle(page);
    },
  },
  {
    name: 'pathway-narrow',
    title: 'Pathway page on a narrow window: one card, Details and Current stage open, Stages folded',
    path: (d) => `/pathways/${d.pathwayId}`,
    prepare: async (page) => {
      await page.setViewportSize({ width: 900, height: 900 });
      await settle(page);
    },
  },
  {
    name: 'pathway-narrow-stages',
    title: 'Pathway page on a narrow window, Stages opened too: the three sections scroll together',
    path: (d) => `/pathways/${d.pathwayId}`,
    prepare: async (page) => {
      await page.setViewportSize({ width: 900, height: 900 });
      await page.getByRole('button', { name: 'Show stages' }).click();
      await settle(page);
    },
  },
  {
    name: 'pathway-stage-files',
    title: "Pathway page, the current stage's files opened under its notes (the pathway's own files are open by default)",
    path: (d) => `/pathways/${d.pathwayId}`,
    prepare: async (page) => {
      await page.getByRole('button', { name: 'Files', exact: true }).first().click();
      await settle(page);
    },
  },
  {
    name: 'pathway-new',
    title: "New pathway for a patient, a template chosen: its stages listed and its name as the title",
    path: (d) => `/pathways/new?patientId=${d.patientId}`,
    prepare: async (page) => {
      await page.getByRole('combobox', { name: /Template/ }).click();
      await page.getByRole('option').first().click();
      await settle(page);
    },
  },
  {
    name: 'pathway-new-narrow',
    title: 'New pathway on a narrow window: one form card that scrolls, its footer pinned',
    path: (d) => `/pathways/new?patientId=${d.patientId}`,
    prepare: async (page) => {
      await page.setViewportSize({ width: 900, height: 900 });
      await page.getByRole('combobox', { name: /Template/ }).click();
      await page.getByRole('option').first().click();
      await settle(page);
    },
  },
  {
    name: 'pathway-visit',
    title:
      "Pathway page with its Appointments in the details and, if its current stage involves a visit, the Visit part above the notes (the parity data's first pathway: it shows a visit only when the demo data gives that pathway's stage one)",
    path: (d) => `/pathways/${d.pathwayId}`,
    prepare: async (page) => {
      await page.getByRole('button', { name: 'Appointments' }).waitFor();
      await settle(page);
    },
  },
  {
    name: 'pathway-branches',
    title: "Pathway page of a template that branches: its stages' Optional, Branch point and Branch tokens, and skipped ones struck through with the reason",
    path: (d) => `/pathways/${d.branchingPathwayId}`,
    prepare: async (page, { data }) => {
      test.skip(!data.branchingPathwayId, 'No pathway from a template that branches (the demo data has them from PR 28).');
      await page.getByRole('heading', { name: 'Stages' }).waitFor();
      await settle(page);
    },
  },
  {
    name: 'template-branches',
    title: 'Template editor: Optional and Branch point on each stage, a Branch name after a branch point, branches indented',
    path: (d) => `/admin/pathway-templates/${d.branchingTemplateId}`,
    prepare: async (page, { data }) => {
      test.skip(!data.branchingTemplateId, 'No template that branches (the demo data has them from PR 28).');
      await page.getByRole('list', { name: 'Stages in order' }).waitFor();
      await settle(page);
    },
  },
  {
    name: 'pathway-restricted',
    title:
      "A restricted patient's pathway at 1366px: the restricted label (\"Restricted record: changes paused\") beside the title, wrapping if it must, the actions on the same line",
    path: (d) => `/pathways/${d.restrictedPathwayId}`,
    prepare: async (page, { data }) => {
      test.skip(!data.restrictedPathwayId, 'No restricted patient with a pathway (reset_demo_data makes some).');
      await page.setViewportSize({ width: 1366, height: 768 });
      await settle(page);
    },
  },
  {
    name: 'pathway-restricted-half',
    title: "A restricted patient's pathway in a half view: the restricted label under the title, Close half view kept top right",
    path: (d) => `/pathways/${d.restrictedPathwayId}`,
    prepare: async (page, { data }) => {
      test.skip(!data.restrictedPathwayId, 'No restricted patient with a pathway (reset_demo_data makes some).');
      await page.setViewportSize({ width: 1366, height: 768 });
      await page.evaluate(() => sessionStorage.setItem('medapp.halfView', '/admin/audit'));
      await page.reload();
      await settle(page);
    },
  },
  {
    name: 'pathway-restricted-details',
    title: "A restricted patient's pathway with the restricted label focused: the whole message (the reason, since when, what can still be done) over the page",
    path: (d) => `/pathways/${d.restrictedPathwayId}`,
    prepare: async (page, { data }) => {
      test.skip(!data.restrictedPathwayId, 'No restricted patient with a pathway (reset_demo_data makes some).');
      await page.setViewportSize({ width: 1366, height: 768 });
      // Focus opens it, as the keyboard does; it stays open while nothing moves.
      await page.getByRole('button', { name: /^Restricted record.*, show details$/ }).focus();
      await page.getByRole('dialog', { name: 'Restricted record: details' }).waitFor();
      await settle(page);
    },
  },
  {
    name: 'pathway-restricted-phone',
    title: "A restricted patient's pathway on a phone, with the restricted label tapped: the label wraps, and the message fits the screen",
    path: (d) => `/pathways/${d.restrictedPathwayId}`,
    prepare: async (page, { data }) => {
      test.skip(!data.restrictedPathwayId, 'No restricted patient with a pathway (reset_demo_data makes some).');
      await page.setViewportSize({ width: 390, height: 844 });
      await page.getByRole('button', { name: /^Restricted record.*, show details$/ }).click();
      await page.getByRole('dialog', { name: 'Restricted record: details' }).waitFor();
      await settle(page);
    },
  },
  { name: 'pathway-edit', title: "Edit a pathway's details", path: (d) => `/pathways/${d.pathwayId}/edit` },
  {
    name: 'pathway-block',
    title: 'Put Pathway on Hold dialog, asking for a reason (not confirmed)',
    path: (d) => `/pathways/${d.pathwayId}`,
    prepare: async (page) => {
      await page.getByRole('button', { name: 'Put on Hold' }).click();
      await passNotAssignedCheck(page);
      await waitVisible(page, DIALOG);
    },
  },
  {
    name: 'pathway-finish',
    title: 'Finish stage confirmation (not confirmed)',
    path: (d) => `/pathways/${d.pathwayId}`,
    prepare: async (page) => {
      await page.getByRole('button', { name: 'Finish Stage' }).click();
      await passNotAssignedCheck(page);
      await waitVisible(page, DIALOG);
    },
  },
  { name: 'patient-add', title: 'Add patient, empty', path: '/patients/add' },
  {
    name: 'patient-add-errors',
    title: 'Add patient submitted empty (validation errors)',
    path: '/patients/add',
    prepare: async (page) => {
      await page.getByRole('button', { name: /add patient/i }).last().click();
      // Blazor asks for confirmation first; the empty form is then rejected by the API with a 400, so nothing is created.
      const confirm = page.locator(DIALOG).getByRole('button', { name: /confirm/i });
      if (await confirm.isVisible({ timeout: 3_000 }).catch(() => false)) await confirm.click();
      await waitVisible(page, ALERT);
      await settle(page);
    },
  },
  { name: 'patient-edit', title: 'Edit patient', path: (d) => `/patients/${d.patientId}/edit` },
  {
    name: 'patient-delete-confirm',
    title: 'Delete patient confirmation (not confirmed)',
    path: (d) => `/patients/${d.patientId}/edit`,
    prepare: async (page) => {
      await page.getByRole('button', { name: /delete/i }).first().click();
      await waitVisible(page, DIALOG);
    },
  },
  { name: 'users-list', title: 'Users list', path: '/admin/users' },
  {
    name: 'users-create',
    title: 'Create user dialog',
    path: '/admin/users',
    prepare: async (page) => {
      await page.getByRole('button', { name: /add user/i }).first().click();
      await waitVisible(page, DIALOG);
    },
  },
  {
    name: 'users-roles',
    title: "Edit roles dialog: the roles that aren't archived, with their descriptions, the user's ticked (not saved)",
    path: '/admin/users',
    prepare: async (page) => {
      await rowAction(page, /edit roles/i).click();
      await waitVisible(page, DIALOG);
      await settle(page);
    },
  },
  {
    name: 'users-specialties',
    title: "Edit specialties dialog: the active specialties, and archived ones the user has, theirs ticked (not saved)",
    path: '/admin/users',
    prepare: async (page) => {
      await rowAction(page, /edit specialties/i).click();
      await waitVisible(page, DIALOG);
      await page.getByRole('dialog').getByText('Loading specialties...').waitFor({ state: 'detached' });
      await settle(page);
    },
  },
  {
    name: 'users-reset-password',
    title: 'Reset password confirmation (not confirmed)',
    path: '/admin/users',
    prepare: async (page) => {
      await rowAction(page, /reset password/i).click();
      await waitVisible(page, DIALOG);
    },
  },
  {
    name: 'users-delete',
    title: 'Delete user confirmation (not confirmed)',
    path: '/admin/users',
    prepare: async (page) => {
      await rowAction(page, /delete/i).click();
      await waitVisible(page, DIALOG);
    },
  },
  {
    name: 'users-details',
    title: "Edit details dialog: a user's full name and job title for letters (not saved)",
    path: '/admin/users',
    prepare: async (page) => {
      await rowAction(page, /edit details/i).click();
      await waitVisible(page, DIALOG);
    },
  },
  { name: 'practices', title: 'GP practices: the directory of practices patients are registered with and referrals come from', path: '/practices' },
  {
    name: 'practice-dialog',
    title: 'Add Practice dialog: a name, ODS code, address, postcode, phone and secure email (not saved)',
    path: '/practices',
    prepare: async (page) => {
      await page.getByRole('button', { name: 'Add Practice' }).click();
      await waitVisible(page, DIALOG);
      },
    },
  { name: 'roles', title: 'Roles and Permissions (admin): each role, what it is for, who holds it, Admin first', path: '/admin/roles' },
  {
    name: 'role-edit',
    title: "A role's page: its permissions ticked in groups, the Administrator-only group read only, and who holds it",
    path: '/admin/roles',
    prepare: async (page) => {
      await page.getByRole('link', { name: 'Open Nurse' }).click();
      await page.getByRole('heading', { name: 'Edit Role', level: 1 }).waitFor();
      await settle(page);
    },
  },
  {
    name: 'role-save-confirm',
    title: 'Saving a role: what is added and removed, and how many people it affects (not saved)',
    path: '/admin/roles',
    prepare: async (page) => {
      await page.getByRole('link', { name: 'Open Nurse' }).click();
      await page.getByRole('heading', { name: 'Edit Role', level: 1 }).waitFor();
      await page.getByRole('checkbox', { name: 'Assign pathways' }).click();
      await page.getByRole('button', { name: 'Save', exact: true }).click();
      await waitVisible(page, DIALOG);
    },
  },
  {
    name: 'role-admin',
    title: 'The built-in Admin role, read only',
    path: '/admin/roles',
    prepare: async (page) => {
      await page.getByRole('link', { name: 'View Admin' }).click();
      await page.getByRole('heading', { name: 'Admin Role', level: 1 }).waitFor();
      await settle(page);
    },
  },
  { name: 'specialties', title: 'Specialties (admin): what templates belong to and staff work in, with what uses each', path: '/admin/specialties' },
  {
    name: 'specialty-dialog',
    title: 'Edit specialty dialog, with its waiting-time targets (not saved)',
    path: '/admin/specialties',
    prepare: async (page) => {
      await rowAction(page, /edit specialty/i).click();
      await waitVisible(page, DIALOG);
    },
  },
  { name: 'admin-stages', title: 'Stage Library (admin)', path: '/admin/stages' },
  {
    name: 'admin-stage-edit',
    title: 'Edit stage dialog, with its Visit choice (not saved)',
    path: '/admin/stages',
    prepare: async (page) => {
      await rowAction(page, /edit stage/i).click();
      await waitVisible(page, DIALOG);
    },
  },
  { name: 'admin-templates', title: 'Pathway Templates (admin), archived ones shown too (no status filter)', path: '/admin/pathway-templates?status=any' },
  {
    name: 'admin-template-edit',
    title: 'Edit a pathway template: its stages in order beside All Stages, to add from',
    path: '/admin/pathway-templates',
    prepare: async (page) => {
      // Edit is a link to the template's page.
      await page.getByRole('table').getByRole('link', { name: /edit template/i }).first().click();
      await page.getByRole('heading', { name: 'Edit Pathway Template' }).waitFor();
      await settle(page);
    },
  },
  {
    name: 'admin-template-new-narrow',
    title: 'New template on a narrow window: one form card',
    path: '/admin/pathway-templates/new',
    prepare: async (page) => {
      await page.setViewportSize({ width: 900, height: 900 });
      await settle(page);
    },
  },
  {
    name: 'letter-templates-list',
    title: "Letter templates (admin), archived ones shown too: every specialty's first, then by specialty",
    path: '/admin/letter-templates?status=any',
  },
  {
    name: 'letter-template-new',
    title: 'New letter template: details, text and preview, beside the merge fields',
    path: '/admin/letter-templates/new',
  },
  {
    name: 'letter-template-edit',
    title: 'Edit a letter template (needs at least one)',
    path: '/admin/letter-templates?status=any',
    prepare: async (page) => {
      // Edit is a link to the template's page.
      await page.getByRole('table').getByRole('link', { name: /edit letter template/i }).first().click();
      await page.getByRole('heading', { name: 'Edit Letter Template' }).waitFor();
      await settle(page);
    },
  },
  { name: 'activity-types', title: 'Activity types (admin): what staff block time for, each in its colour', path: '/admin/activity-types' },
  {
    name: 'activity-type-dialog',
    title: 'Edit activity type dialog: its name and colour (not saved)',
    path: '/admin/activity-types',
    prepare: async (page) => {
      await rowAction(page, /edit activity type/i).click();
      await waitVisible(page, DIALOG);
    },
  },
  { name: 'closed-days', title: "Closed days (admin): this year's bank holidays and other days the clinic is closed", path: '/admin/closed-days' },
  {
    name: 'closed-day-dialog',
    title: 'Add Closed Day dialog: a date from today on, where (everywhere or a site), and a name (not saved)',
    path: '/admin/closed-days',
    prepare: async (page) => {
      await page.getByRole('button', { name: 'Add Closed Day' }).click();
      await waitVisible(page, DIALOG);
    },
  },
  { name: 'sites', title: 'Sites and rooms (admin): the places the centre works from, each with its rooms', path: '/admin/sites' },
  {
    name: 'site-dialog',
    title: 'Add Site dialog: a name, an address, a postcode and a phone number (not saved)',
    path: '/admin/sites',
    prepare: async (page) => {
      await page.getByRole('button', { name: 'Add Site' }).click();
      await waitVisible(page, DIALOG);
    },
  },
  {
    name: 'site-rooms',
    title: "A site's rooms, with adding, renaming, archiving and deleting them (nothing changed)",
    path: '/admin/sites',
    prepare: async (page) => {
      await rowAction(page, /^rooms$/i).click();
      await waitVisible(page, DIALOG);
    },
  },
  { name: 'calendar-templates', title: 'My calendar templates: the day templates tab (an admin also picks whose)', path: '/calendar/templates' },
  { name: 'calendar-templates-week', title: 'My calendar templates: the week templates tab', path: '/calendar/templates?tab=week' },
  { name: 'calendar-templates-default', title: 'My calendar templates: my default week', path: '/calendar/templates?tab=default' },
  {
    name: 'day-template-editor',
    title: 'New day template with two blocks (not saved), beside the day it makes',
    path: '/calendar/templates/day/new',
    prepare: async (page) => {
      await page.getByRole('button', { name: 'Add Block' }).click();
      await page.getByRole('button', { name: 'Add Block' }).click();
      await settle(page);
    },
  },
  { name: 'week-template-editor', title: 'New week template: one of my day templates for each weekday', path: '/calendar/templates/week/new' },
  { name: 'admin-audit', title: 'Audit log (admin)', path: '/admin/audit' },
  {
    name: 'admin-insights',
    title: 'Insights (admin): headline counts, overdue work, throughput and time at each stage',
    path: '/admin/insights',
    prepare: async (page) => {
      await page.getByRole('heading', { name: 'Overdue by assignee' }).waitFor();
      await settle(page);
    },
  },
  {
    name: 'admin-insights-specialty',
    title: "Insights on one specialty: its pathways only, and only its templates to choose from",
    path: (d) => `/admin/insights${d.specialtyId ? `?specialty=${d.specialtyId}` : ''}`,
    prepare: async (page) => {
      await page.getByRole('heading', { name: 'Overdue by assignee' }).waitFor();
      await settle(page);
    },
  },
  {
    name: 'admin-insights-tables',
    title: "Insights with every chart's numbers as a table",
    path: '/admin/insights?weeks=4',
    prepare: async (page) => {
      // Each button becomes Show Chart once pressed, so press the first left until none are.
      const showTable = page.getByRole('button', { name: /^Show Table of / });
      while ((await showTable.count()) > 0) await showTable.first().click();
      await settle(page);
    },
  },
  { name: 'admin-monitoring', title: "Monitoring (admin): the admin's rules, as sentences", path: '/admin/monitoring' },
  {
    name: 'admin-monitoring-add',
    title: 'Monitoring: adding a rule, with its sentence previewed',
    path: '/admin/monitoring',
    prepare: async (page) => {
      await page.getByRole('button', { name: 'Add Rule' }).first().click();
      await page.getByText('Preview').waitFor();
      await settle(page);
    },
  },
  {
    name: 'admin-insights-narrow',
    title: 'Insights on a narrow window: one column of charts',
    path: '/admin/insights',
    prepare: async (page) => {
      await page.setViewportSize({ width: 900, height: 900 });
      await page.getByRole('heading', { name: 'Overdue by assignee' }).waitFor();
      await settle(page);
    },
  },
  {
    name: 'admin-audit-pathway',
    title: "One pathway's audit log, from its View Audit Log",
    path: (d) => `/admin/audit?pathwayId=${d.pathwayId}`,
  },
  { name: 'admin-activity', title: 'Activity log (admin)', path: '/admin/activity' },
  {
    name: 'admin-activity-patient',
    title: "One patient's activity, from their access report",
    path: (d) => `/admin/activity?patientId=${d.patientId}`,
  },
  { name: 'admin-retention', title: 'Retention review (admin)', path: '/admin/retention' },
  // Opening the export records it in the activity log, as any export does (synthetic demo data only).
  { name: 'patient-export', title: "A patient's subject access export (admin)", path: (d) => `/patients/${d.patientId}/export` },
  {
    name: 'half-view',
    title: "Half view: a pathway beside its patient's page, each in its narrow layout, the nav folded away",
    path: (d) => `/pathways/${d.pathwayId}`,
    prepare: async (page) => {
      // From the Patients row's menu (its button shows while the row is hovered, and opens it when rested on).
      await page.getByRole('navigation').getByRole('link', { name: 'Patients' }).hover();
      await page.getByRole('button', { name: 'Patients options' }).hover();
      await page.getByRole('menuitem', { name: 'Open in half view' }).click();
      await page.getByRole('heading', { name: 'Patients', level: 1 }).waitFor();
      await settle(page);
    },
  },
  { name: 'account-password', title: 'Change your password (from Account Settings)', path: '/account/password' },
  { name: 'account-email', title: 'Your email address (from Account Settings)', path: '/account/email' },
  { name: 'account-security', title: 'Security: two-factor authentication (from Account Settings)', path: '/account/security' },
  { name: 'admin-settings', title: 'Admin Settings: require two-factor for admins', path: '/admin/settings' },
  { name: 'admin-jobs', title: 'Scheduled Jobs (admin): each job with its schedule, status, last and next run', path: '/admin/jobs' },
  {
    name: 'admin-jobs-run-dialog',
    title: 'Scheduled Jobs: the Run Now confirmation, saying what the job does (not run)',
    path: '/admin/jobs',
    prepare: async (page) => {
      // The first job that can run now (running or switched off ones are disabled); never confirmed.
      await page.locator('button:not([aria-disabled="true"]):not([disabled])').filter({ hasText: 'Run Now' }).first().click();
      await waitVisible(page, DIALOG);
    },
  },
  { name: 'account-colours', title: 'Colours: a colour theme for each mode, or Custom', path: '/account/colours' },
  {
    name: 'account-colours-custom',
    title: 'Colours with Custom open for light mode: the themes folded away, the colour pickers shown',
    path: '/account/colours',
    prepare: async (page) => {
      await page.getByRole('list', { name: 'Light mode colour themes' }).getByText('Custom').click();
      await page.getByLabel(/Page background/).waitFor();
    },
  },
];
