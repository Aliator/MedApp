import { existsSync, statSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { screens } from './screens';
import { APPS, shotFile, shotPath } from './shared';

const escape = (s: string) => s.replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c]!);

function cell(screen: string, app: (typeof APPS)[number]) {
  const file = shotPath(screen, app);
  if (!existsSync(file)) return `<div class="missing">missing in ${app}</div>`;
  const src = `screenshots/${encodeURIComponent(screen)}/${shotFile(app)}?v=${statSync(file).mtimeMs}`;
  return `<a href="${src}" target="_blank"><img loading="lazy" src="${src}" alt="${escape(`${screen} (${app})`)}"></a>`;
}

/** Runs after every capture (Playwright globalTeardown) and writes report.html beside the screenshots. */
export default function writeReport() {
  const rows = screens.map(
    (s) => `<section>
  <h2>${escape(s.title)} <small>${escape(s.name)} · desktop 1440×900</small></h2>
  <div class="pair">${APPS.map((a) => `<figure><figcaption>${a === 'blazor' ? 'Blazor (MedApp.Client)' : 'React (MedApp.Web)'}</figcaption>${cell(s.name, a)}</figure>`).join('')}</div>
</section>`,
  );

  const html = `<!doctype html>
<html lang="en-GB">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>UI parity</title>
<style>
  :root { color-scheme: light dark; --bg: #f4f6f8; --card: #fff; --text: #111827; --muted: #6b7280; --border: #e5e7eb; }
  @media (prefers-color-scheme: dark) { :root { --bg: #0b0f14; --card: #151b23; --text: #e5e7eb; --muted: #9ca3af; --border: #2a3441; } }
  body { margin: 0; padding: 24px 16px; font: 14px/1.5 system-ui, sans-serif; background: var(--bg); color: var(--text); }
  h1 { margin: 0 0 4px; font-size: 20px; } p { margin: 0 0 24px; color: var(--muted); }
  section { background: var(--card); border: 1px solid var(--border); border-radius: 12px; padding: 16px; margin: 0 0 16px; }
  h2 { margin: 0 0 12px; font-size: 15px; } small { color: var(--muted); font-weight: 400; margin-left: 8px; }
  .pair { display: grid; grid-template-columns: repeat(auto-fit, minmax(min(100%, 420px), 1fr)); gap: 16px; }
  figure { margin: 0; } figcaption { color: var(--muted); margin-bottom: 6px; }
  img { width: 100%; border: 1px solid var(--border); border-radius: 8px; display: block; }
  .missing { display: grid; place-items: center; min-height: 160px; border: 1px dashed var(--border); border-radius: 8px; color: var(--muted); }
</style>
</head>
<body>
<h1>UI parity: Blazor vs React</h1>
<p>Generated ${new Date().toLocaleString('en-GB', { timeZone: 'Europe/London' })}. Compare by eye; the apps should look alike, not identical. Click a screenshot to open it full size.</p>
${rows.join('\n')}
</body>
</html>`;
  writeFileSync(path.join(__dirname, 'report.html'), html);
  console.log(`UI parity report: ${path.join(__dirname, 'report.html')}`);
}
