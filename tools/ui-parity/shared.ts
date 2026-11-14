import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import type { Locator, Page } from '@playwright/test';

export type AppName = 'blazor' | 'react';
export const APPS: AppName[] = ['blazor', 'react'];

export interface ProjectMeta {
  app: AppName;
  apiUrl: string;
}

/** Data discovered at login time and shared by the screens, so both apps show the same records. */
export interface ParityData {
  patientId: string;
  /** An in-progress procedure, for the procedure page (React only: Blazor never had one). */
  procedureId?: string;
}

const root = __dirname;
export const authFile = (app: AppName) => path.join(root, '.auth', `${app}.json`);
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
