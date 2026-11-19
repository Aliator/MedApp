import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import AxeBuilder from '@axe-core/playwright';
import { test } from '@playwright/test';
import { screens } from './screens';
import { isSignedIn, readData, settle, storageStateFor, useSettings, type ProjectMeta } from './shared';

/**
 * The accessibility audit: axe (WCAG 2.2 AA and below) on every screen of the React app, in each mode and with a
 * colour theme, each screen's results written to `a11y-results/` for `a11y-report.ts` to sum up. Run it with
 * `npm run a11y` (the AppHost running).
 */

/** The ways a screen is checked: the app's settings for it (with the screen's own, see `useSettings`). */
const VARIANTS: { name: string; settings: Record<string, string> }[] = [
  { name: 'dark', settings: {} },
  { name: 'light', settings: { 'medapp.colorMode': 'light' } },
  // A colour theme (Butter, from Account Settings, Colours), for contrast against colours other than the app's own.
  {
    name: 'light-butter',
    settings: {
      'medapp.colorMode': 'light',
      'medapp.colours': JSON.stringify({
        light: { page: '#fdfbe4', card: '#ffffff', nav: '#261a13' },
        dark: { page: '#2e2117', card: '#3a2a1f', nav: '#261a13' },
      }),
    },
  },
];

export const resultsDir = path.join(__dirname, 'a11y-results');

for (const variant of VARIANTS) {
  for (const screen of screens) {
    test.describe(`${variant.name} ${screen.name}`, () => {
      if (screen.signedOut) test.use({ storageState: { cookies: [], origins: [] } });
      else if (screen.as) test.use({ storageState: storageStateFor(screen.as) });

      test(screen.title, async ({ page }, info) => {
        const { app } = info.project.metadata as ProjectMeta;
        test.skip(!!screen.as && !isSignedIn(app, screen.as), `No ${screen.as} account: set it in .env (README.md).`);
        await useSettings(page, { ...variant.settings, ...screen.settings }, { signedIn: !screen.signedOut });
        await page.goto(typeof screen.path === 'function' ? screen.path(readData(app)) : screen.path);
        await settle(page);
        if (screen.prepare) {
          await screen.prepare(page, { app, data: readData(app) });
          await settle(page);
        }

        const results = await new AxeBuilder({ page })
          .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'])
          .analyze();
        mkdirSync(resultsDir, { recursive: true });
        writeFileSync(
          path.join(resultsDir, `${variant.name}--${screen.name}.json`),
          JSON.stringify({ variant: variant.name, screen: screen.name, violations: results.violations }, null, 2),
        );
      });
    });
  }
}
