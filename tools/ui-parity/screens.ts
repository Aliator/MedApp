import type { Page } from '@playwright/test';
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
 * Changing a procedure that isn't assigned to you first asks "Not assigned to you". Continue only opens the
 * next dialog; nothing is changed until that one is confirmed, which these screens never do.
 */
async function passNotAssignedCheck(page: Page) {
  const proceed = page.locator(DIALOG).getByRole('button', { name: 'Continue' });
  const asked = await proceed.waitFor({ state: 'visible', timeout: 1500 }).then(() => true, () => false);
  if (asked) await proceed.click();
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
    title: "Calendar: my day, with This day's template and Default week",
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
  { name: 'needs-booking', title: 'Needs Booking: appointments asked for but not yet booked, soonest due first', path: '/calendar/needs-booking' },
  { name: 'my-tasks', title: 'My Tasks: my task lists (the Blazor app home page)', path: '/tasks' },
  {
    name: 'my-tasks-narrow',
    title: 'My Tasks on a narrow window: the same list, with its search',
    path: '/tasks',
    prepare: async (page) => {
      await page.setViewportSize({ width: 900, height: 900 });
      await settle(page);
    },
  },
  {
    name: 'my-tasks-favourites',
    title: "My Tasks' favourites open (the star beside the search): Not started, Started, ...",
    path: '/tasks',
    prepare: async (page) => {
      await page.getByRole('button', { name: 'Favourites' }).click();
      await page.getByRole('menuitem', { name: 'Started', exact: true }).waitFor();
    },
  },
  { name: 'my-tasks-started', title: 'My Tasks, the started ones (from the favourites)', path: '/tasks?progress=started' },
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
  { name: 'patient-view', title: 'View patient: details beside the timeline (what is due, filters, the feed by day)', path: (d) => `/patients/${d.patientId}` },
  {
    name: 'patient-view-narrow',
    title: 'View patient on a narrow window: one card, Details and Timeline folding',
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
  { name: 'patient-procedures', title: "A patient's procedures", path: (d) => `/patients/${d.patientId}/procedures` },
  { name: 'procedures-list', title: 'All procedures, page 1', path: '/procedures' },
  {
    name: 'procedures-search',
    title: 'Procedures search: "is any of" and "is not any of" tokens, and the field list open',
    path: '/procedures?status=InProgress,Blocked&priority=!Low',
    prepare: async (page) => {
      await page.getByRole('combobox', { name: 'Search procedures' }).click();
      await settle(page);
    },
  },
  { name: 'procedure-view', title: 'Procedure page: details and current stage beside the stages', path: (d) => `/procedures/${d.procedureId}` },
  {
    name: 'procedure-narrow',
    title: 'Procedure page on a narrow window: one card, Details and Current stage open, Stages folded',
    path: (d) => `/procedures/${d.procedureId}`,
    prepare: async (page) => {
      await page.setViewportSize({ width: 900, height: 900 });
      await settle(page);
    },
  },
  {
    name: 'procedure-narrow-stages',
    title: 'Procedure page on a narrow window, Stages opened too: the three sections scroll together',
    path: (d) => `/procedures/${d.procedureId}`,
    prepare: async (page) => {
      await page.setViewportSize({ width: 900, height: 900 });
      await page.getByRole('button', { name: 'Show stages' }).click();
      await settle(page);
    },
  },
  {
    name: 'procedure-new',
    title: "New procedure for a patient, a template chosen: its stages listed and its name as the title",
    path: (d) => `/procedures/new?patientId=${d.patientId}`,
    prepare: async (page) => {
      await page.getByRole('combobox', { name: /Template/ }).click();
      await page.getByRole('option').first().click();
      await settle(page);
    },
  },
  {
    name: 'procedure-new-narrow',
    title: 'New procedure on a narrow window: one form card that scrolls, its footer pinned',
    path: (d) => `/procedures/new?patientId=${d.patientId}`,
    prepare: async (page) => {
      await page.setViewportSize({ width: 900, height: 900 });
      await page.getByRole('combobox', { name: /Template/ }).click();
      await page.getByRole('option').first().click();
      await settle(page);
    },
  },
  {
    name: 'procedure-visit',
    title:
      "Procedure page with its Appointments in the details and, if its current stage involves a visit, the Visit part above the notes (the parity data's first procedure: it shows a visit only when the demo data gives that procedure's stage one)",
    path: (d) => `/procedures/${d.procedureId}`,
    prepare: async (page) => {
      await page.getByRole('button', { name: 'Appointments' }).waitFor();
      await settle(page);
    },
  },
  { name: 'procedure-edit', title: "Edit a procedure's details", path: (d) => `/procedures/${d.procedureId}/edit` },
  {
    name: 'procedure-block',
    title: 'Block procedure dialog, asking for a reason (not confirmed)',
    path: (d) => `/procedures/${d.procedureId}`,
    prepare: async (page) => {
      await page.getByRole('button', { name: 'Block' }).click();
      await passNotAssignedCheck(page);
      await waitVisible(page, DIALOG);
    },
  },
  {
    name: 'procedure-finish',
    title: 'Finish stage confirmation (not confirmed)',
    path: (d) => `/procedures/${d.procedureId}`,
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
    title: "Edit roles dialog: every role, the user's ticked (not saved)",
    path: '/admin/users',
    prepare: async (page) => {
      await rowAction(page, /edit roles/i).click();
      await waitVisible(page, DIALOG);
      await settle(page);
    },
  },
  {
    name: 'users-reset-password',
    title: 'Reset password confirmation (not confirmed)',
    path: '/admin/users',
    prepare: async (page) => {
      await rowAction(page, /reset/i).click();
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
  { name: 'admin-stages', title: 'Stages (admin)', path: '/admin/stages' },
  {
    name: 'admin-stage-edit',
    title: 'Edit stage dialog, with its Visit choice (not saved)',
    path: '/admin/stages',
    prepare: async (page) => {
      await rowAction(page, /edit stage/i).click();
      await waitVisible(page, DIALOG);
    },
  },
  { name: 'admin-templates', title: 'Templates (admin), archived ones shown too (no status filter)', path: '/admin/templates?status=any' },
  {
    name: 'admin-template-edit',
    title: 'Edit a template: its stages in order beside All Stages, to add from',
    path: '/admin/templates',
    prepare: async (page) => {
      // Edit is a link to the template's page.
      await page.getByRole('table').getByRole('link', { name: /edit template/i }).first().click();
      await page.getByRole('heading', { name: 'Edit Template' }).waitFor();
      await settle(page);
    },
  },
  {
    name: 'admin-template-new-narrow',
    title: 'New template on a narrow window: one form card',
    path: '/admin/templates/new',
    prepare: async (page) => {
      await page.setViewportSize({ width: 900, height: 900 });
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
  { name: 'calendar-templates', title: 'Calendar templates (admin): the day templates tab', path: '/admin/calendar-templates' },
  { name: 'calendar-templates-week', title: 'Calendar templates (admin): the week templates tab', path: '/admin/calendar-templates?tab=week' },
  { name: 'calendar-templates-staff', title: "Calendar templates (admin): each member of staff's default week", path: '/admin/calendar-templates?tab=staff' },
  {
    name: 'day-template-editor',
    title: 'New day template with two blocks (not saved), beside the day it makes',
    path: '/admin/calendar-templates/day/new',
    prepare: async (page) => {
      await page.getByRole('button', { name: 'Add Block' }).click();
      await page.getByRole('button', { name: 'Add Block' }).click();
      await settle(page);
    },
  },
  { name: 'week-template-editor', title: 'New week template: a day template for each weekday', path: '/admin/calendar-templates/week/new' },
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
    name: 'admin-audit-procedure',
    title: "One procedure's audit log, from its View Audit Log",
    path: (d) => `/admin/audit?procedureId=${d.procedureId}`,
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
    title: "Half view: a procedure beside its patient's page, each in its narrow layout, the nav folded away",
    path: (d) => `/procedures/${d.procedureId}`,
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
