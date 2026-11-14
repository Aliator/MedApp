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
  { name: 'home', title: 'Home: my task lists', path: '/' },
  {
    name: 'home-narrow',
    title: 'Home on a narrow window: one list, with a strip to switch',
    path: '/',
    prepare: async (page) => {
      await page.setViewportSize({ width: 900, height: 900 });
      await settle(page);
    },
  },
  {
    name: 'home-narrow-hover',
    title: 'Home on a narrow window, hovering the switch to the started list',
    path: '/',
    prepare: async (page) => {
      await page.setViewportSize({ width: 900, height: 900 });
      await settle(page);
      await page.getByText('Started', { exact: true }).hover();
    },
  },
  {
    name: 'home-narrow-started',
    title: 'Home on a narrow window, switched to the started list',
    path: '/',
    prepare: async (page) => {
      await page.setViewportSize({ width: 900, height: 900 });
      // The card's own button is a visually hidden overlay, so click its visible label.
      await page.getByText('Started', { exact: true }).click();
      await settle(page);
    },
  },
  { name: 'account', title: 'Account', path: '/account' },
  { name: 'patients-list', title: 'Patients list, page 1', path: '/patients' },
  {
    name: 'patients-search',
    title: 'Patients search dialog',
    path: '/patients',
    prepare: async (page) => {
      await page.getByRole('button', { name: /^search$/i }).first().click();
      await waitVisible(page, DIALOG);
    },
  },
  { name: 'patients-sorted', title: 'Patients sorted by date of birth, newest first', path: '/patients?sort=DateOfBirth&asc=false' },
  { name: 'patient-view', title: 'View patient', path: (d) => `/patients/${d.patientId}` },
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
    title: 'Assign roles dialog',
    path: '/admin/users',
    prepare: async (page) => {
      await rowAction(page, /assign/i).click();
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
];
