import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import type { Locator, Page, TestInfo } from '@playwright/test';

export type AppName = 'blazor' | 'react';
export const APPS: AppName[] = ['blazor', 'react'];

/**
 * Who a screen is captured as: the parity admin (the default: setup, accounts and the read-only view), or, for screens
 * that need clinical access (admins are read only, and records sit behind team walls), a member of staff in their
 * team: the clinic's (Dermatology) or the lab's. See README.md for the accounts.
 */
export type Account = 'admin' | 'clinic' | 'lab';

export interface ProjectMeta {
  app: AppName;
  apiUrl: string;
}

/** Data discovered at login time and shared by the screens, so both apps show the same records. */
export interface ParityData {
  patientId: string;
  /** An in-progress pathway, for the pathway page (React only: Blazor never had one). */
  pathwayId?: string;
  /** A pathway of a restricted patient, for a page with a long page message (the restricted banner). */
  restrictedPathwayId?: string;
  /**
   * The parity account's own synthetic meeting, which it has declined, and its UK date: for the meeting dialog's replies
   * (with Accept and Decline) and a declined meeting faded on the calendar. Made once by the setup (React only).
   */
  meetingId?: string;
  meetingDate?: string;
  /** The demo data's Histology (lab) stage definition, or else the first, for the stage reached filter (React only). */
  stageId?: string;
  /** The demo data's Dermatology team, or else the first, for the team filters (React only). */
  teamId?: string;
  /**
   * A template with a branch point, and a pathway made from it, for the branching screens (React only; the demo data
   * has them from Phase 4's, PR 28: until then those screens skip).
   */
  branchingTemplateId?: string;
  branchingPathwayId?: string;
}

const root = __dirname;
export const authFile = (app: AppName, account: Account = 'admin') =>
  path.join(root, '.auth', account === 'admin' ? `${app}.json` : `${app}-${account}.json`);

/** Whether the setup signed this account in (the lab account is optional). */
export const isSignedIn = (app: AppName, account: Account) => existsSync(authFile(app, account));

/**
 * The browser session a screen runs with, as a `storageState` fixture: the account's saved by the setup, or nobody's
 * when that account isn't set up (the screen then skips itself, see `skipUnlessSignedIn`).
 */
export const storageStateFor =
  (account: Account) =>
  async ({}, use: (state: string | { cookies: []; origins: [] }) => Promise<void>, info: TestInfo) => {
    const { app } = info.project.metadata as ProjectMeta;
    await use(isSignedIn(app, account) ? authFile(app, account) : { cookies: [], origins: [] });
  };
const dataFile = (app: AppName) => path.join(root, '.auth', `${app}-data.json`);

export function writeData(app: AppName, data: ParityData) {
  mkdirSync(path.dirname(dataFile(app)), { recursive: true });
  writeFileSync(dataFile(app), JSON.stringify(data, null, 2));
}

/** Data saved by the login setup. Empty when it hasn't run (e.g. signed-out screens run with --no-deps). */
export function readData(app: AppName): ParityData {
  return existsSync(dataFile(app)) ? (JSON.parse(readFileSync(dataFile(app), 'utf8')) as ParityData) : ({} as ParityData);
}

export const screenshotsDir = path.join(root, 'screenshots');
export const shotFile = (app: AppName) => `${app}.png`;
export const shotPath = (screen: string, app: AppName) => path.join(screenshotsDir, screen, shotFile(app));

/**
 * Loading indicators in either app. Blazor: the WASM boot screen, LoadingState and skeleton rows.
 * React/Astryx: anything marked aria-busy. Extend this when MedApp.Web adds its own.
 */
const BUSY = '.app-loading, .loading-state, .skeleton, [aria-busy="true"]';

/** A dialog in either app: Blazor's Modal has no role, Astryx's Dialog is a native <dialog>. */
export const DIALOG = '[role="dialog"], [role="alertdialog"], dialog[open], .modal-container';

/** An alert/banner in either app: Blazor's AlertMessage uses .alert (the login page its own .auth-error), Astryx's Banner uses role="alert". */
export const ALERT = '[role="alert"], .alert, .auth-error';

/** Visible "Loading…" text, e.g. Blazor's "Loading patients..." subtitle, which stays up after the network is idle while the table sizes itself. */
const LOADING_TEXT = /^\s*loading\b/i;

/** Waits until the page has stopped loading data and drawing, so screenshots are stable. */
export async function settle(page: Page) {
  await page.waitForLoadState('networkidle');
  await page.locator(BUSY).first().waitFor({ state: 'hidden', timeout: 20_000 });
  await page.getByText(LOADING_TEXT).first().waitFor({ state: 'hidden', timeout: 20_000 });
  await page.evaluate(() => document.fonts.ready);
  await domQuiet(page);
}

/**
 * Waits until the DOM has stopped changing for `quietMs` (max `timeoutMs`). Catches renders that happen after the
 * network is idle and show no loading text, e.g. Blazor's DataTable drawing rows only after it measures the viewport.
 */
async function domQuiet(page: Page, quietMs = 400, timeoutMs = 10_000) {
  await page.evaluate(
    ({ quietMs, timeoutMs }) =>
      new Promise<void>((resolve) => {
        let timer = setTimeout(done, quietMs);
        const hardStop = setTimeout(done, timeoutMs);
        const observer = new MutationObserver(() => {
          clearTimeout(timer);
          timer = setTimeout(done, quietMs);
        });
        observer.observe(document.body, { childList: true, subtree: true, attributes: true, characterData: true });
        function done() {
          observer.disconnect();
          clearTimeout(timer);
          clearTimeout(hardStop);
          requestAnimationFrame(() => requestAnimationFrame(() => resolve()));
        }
      }),
    { quietMs, timeoutMs },
  );
}

export async function waitVisible(page: Page, selector: string) {
  await page.locator(selector).first().waitFor({ state: 'visible', timeout: 15_000 });
}

/** A form field found by its label, falling back to its placeholder (Blazor's labels aren't always linked). */
export const field = (page: Page, name: RegExp): Locator =>
  page.getByLabel(name).or(page.getByPlaceholder(name)).first();

/** The first enabled button in the main table whose accessible name matches (row actions for the signed-in user are disabled). */
export const rowAction = (page: Page, name: RegExp): Locator =>
  page.getByRole('table').getByRole('button', { name, disabled: false }).first();

/**
 * Gives a screen the app settings it's checked with (none: the app's defaults). The app keeps its settings with the
 * account, and the server renders pages with the account's, so this saves them to the signed-in account (the
 * tools' own, see .env.example) through the API, and puts them in this browser too, for signed-out screens.
 * The page's own saves during the run are dropped, so the screen keeps the settings it was given.
 */
export async function useSettings(page: Page, settings: Record<string, string> = {}, { signedIn = true } = {}) {
  await page.route('**/api/auth/me/settings', (route) =>
    route.request().method() === 'GET' ? route.fallback() : route.fulfill({ status: 204 }),
  );
  if (signedIn) {
    const response = await page.request.put('/api/auth/me/settings', { data: { settings: JSON.stringify(settings) } });
    if (!response.ok()) throw new Error(`Couldn't set the account's settings: PUT api/auth/me/settings returned ${response.status()}.`);
  }
  await page.addInitScript((values) => {
    for (const [key, value] of Object.entries(values)) localStorage.setItem(key, value);
  }, settings);
}
