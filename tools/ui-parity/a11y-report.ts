import { spawnSync } from 'node:child_process';
import { existsSync, readdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';

/**
 * Sums up the accessibility audit (`a11y.spec.ts`): each axe rule that failed, how badly, on which screens and
 * modes, and a few of the elements, in `a11y-report.json` and the console. `npm run a11y` runs the audit first
 * (`--run`, with any further arguments passed to Playwright); without it, this sums up the last run's results.
 * Exits non-zero if a screen failed to load or anything failed.
 */

interface Violation {
  id: string;
  impact: string | null;
  help: string;
  helpUrl: string;
  nodes: { target: string[]; failureSummary?: string }[];
}

const dir = path.join(import.meta.dirname, 'a11y-results');

const [flag, ...playwrightArgs] = process.argv.slice(2);
let runFailed = false;
if (flag === '--run') {
  rmSync(dir, { recursive: true, force: true });
  const run = spawnSync('npx', ['playwright', 'test', '--project=a11y', ...playwrightArgs.map((arg) => (/\s/.test(arg) ? JSON.stringify(arg) : arg))], { stdio: 'inherit', shell: true });
  runFailed = run.status !== 0;
  if (runFailed) console.log('Some screens failed to load: see above. The results below are for the rest.');
}
if (!existsSync(dir)) {
  console.log('No results: run `npm run a11y` first.');
  process.exit(runFailed ? 1 : 0);
}

const byRule = new Map<string, { impact: string | null; help: string; helpUrl: string; screens: Set<string>; nodes: number; examples: string[] }>();
for (const file of readdirSync(dir).filter((f) => f.endsWith('.json'))) {
  const { variant, screen, violations } = JSON.parse(readFileSync(path.join(dir, file), 'utf8')) as {
    variant: string;
    screen: string;
    violations: Violation[];
  };
  for (const v of violations) {
    const rule = byRule.get(v.id) ?? { impact: v.impact, help: v.help, helpUrl: v.helpUrl, screens: new Set(), nodes: 0, examples: [] };
    rule.screens.add(`${variant}/${screen}`);
    rule.nodes += v.nodes.length;
    for (const node of v.nodes) {
      const example = `${variant}/${screen}: ${node.target.join(' ')}${node.failureSummary ? ` (${node.failureSummary.replace(/\s+/g, ' ').slice(0, 160)})` : ''}`;
      if (rule.examples.length < 8 && !rule.examples.includes(example)) rule.examples.push(example);
    }
    byRule.set(v.id, rule);
  }
}

const order = ['critical', 'serious', 'moderate', 'minor'];
const rules = [...byRule.entries()]
  .map(([id, r]) => ({ id, impact: r.impact, help: r.help, helpUrl: r.helpUrl, screens: [...r.screens].sort(), nodes: r.nodes, examples: r.examples }))
  .sort((a, b) => order.indexOf(a.impact ?? 'minor') - order.indexOf(b.impact ?? 'minor') || b.nodes - a.nodes);

writeFileSync(path.join(import.meta.dirname, 'a11y-report.json'), JSON.stringify(rules, null, 2));
if (rules.length === 0) console.log('No accessibility violations found.');
if (rules.length > 0 || runFailed) process.exitCode = 1;
for (const r of rules) {
  console.log(`\n[${r.impact}] ${r.id}: ${r.help} (${r.nodes} elements, ${r.screens.length} screens)`);
  for (const e of r.examples) console.log(`    ${e}`);
}
