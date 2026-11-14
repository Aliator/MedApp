import { test } from '@playwright/test';
import { screens } from './screens';
import { readData, settle, shotPath, type ProjectMeta } from './shared';

for (const screen of screens) {
  test.describe(screen.name, () => {
    if (screen.signedOut) test.use({ storageState: { cookies: [], origins: [] } });

    test(screen.title, async ({ page }, info) => {
      const { app } = info.project.metadata as ProjectMeta;
      const data = readData(app);
      await page.goto(typeof screen.path === 'function' ? screen.path(data) : screen.path);
      await settle(page);
      if (screen.prepare) {
        await screen.prepare(page, { app, data });
        await settle(page);
      }

      await page.screenshot({
        path: shotPath(screen.name, app),
        fullPage: true,
        animations: 'disabled',
        caret: 'hide',
      });
    });
  });
}
