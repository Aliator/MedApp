import { test } from '@playwright/test';
import { screens } from './screens';
import { isSignedIn, readData, settle, shotPath, storageStateFor, useSettings, type ProjectMeta } from './shared';

for (const screen of screens) {
  test.describe(screen.name, () => {
    if (screen.signedOut) test.use({ storageState: { cookies: [], origins: [] } });
    else if (screen.as) test.use({ storageState: storageStateFor(screen.as) });

    test(screen.title, async ({ page }, info) => {
      const { app } = info.project.metadata as ProjectMeta;
      test.skip(!!screen.as && !isSignedIn(app, screen.as), `No ${screen.as} account: set it in .env (README.md).`);
      if (app === 'react') await useSettings(page, screen.settings, { signedIn: !screen.signedOut });
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
