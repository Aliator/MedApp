import { existsSync } from 'node:fs';
import path from 'node:path';
import { defineConfig, type Project } from '@playwright/test';
import { authFile, type AppName, type ProjectMeta } from './shared';

const envFile = path.join(__dirname, '.env');
if (existsSync(envFile)) process.loadEnvFile(envFile);

const apps: { name: AppName; url: string; apiUrl: string }[] = [
  {
    name: 'blazor',
    url: process.env.BLAZOR_URL || 'https://localhost:7292',
    apiUrl: process.env.BLAZOR_API_URL || 'https://localhost:44305',
  },
  // MedApp.Web proxies /api to the API (same origin), so its API URL is its own URL.
  ...(process.env.WEB_URL ? [{ name: 'react' as const, url: process.env.WEB_URL, apiUrl: process.env.WEB_URL }] : []),
];

// Desktop only for now; mobile layouts are out of scope until after the migration.
const viewport = { width: 1440, height: 900 };

const projects: Project[] = apps.flatMap((app) => {
  const metadata: ProjectMeta = { app: app.name, apiUrl: app.apiUrl };
  return [
    { name: `setup-${app.name}`, testMatch: /auth\.setup\.ts/, use: { baseURL: app.url, viewport }, metadata },
    {
      name: app.name,
      testMatch: /capture\.spec\.ts/,
      dependencies: [`setup-${app.name}`],
      use: { baseURL: app.url, storageState: authFile(app.name), viewport },
      metadata,
    },
  ];
});

export default defineConfig({
  testDir: '.',
  outputDir: 'test-results',
  timeout: 60_000,
  retries: 0,
  workers: 4,
  reporter: [['list']],
  globalTeardown: './report.ts',
  use: {
    browserName: 'chromium',
    ignoreHTTPSErrors: true, // ASP.NET Core dev certificates
    locale: 'en-GB',
    timezoneId: 'Europe/London',
    colorScheme: 'light',
    reducedMotion: 'reduce',
    trace: 'retain-on-failure',
  },
  projects,
});
