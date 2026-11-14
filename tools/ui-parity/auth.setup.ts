import { expect, test as setup } from '@playwright/test';
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
  writeData(app, { patientId: patients[0].id });
});
