import schema from '../api/schema.d.ts?raw';
import {handledRoutes} from './api/http';
import {registerHandlers} from './handlers';
import {notInLightDemo} from './notInLightDemo';

/**
 * The light demo keeps up with the API: every path and method in the API's schema (`src/api/schema.d.ts`, generated
 * from Swagger) has a handler in the demo, or is listed in `notInLightDemo` with a reason. A PR that adds an endpoint
 * fails here until it gives the demo a handler or says why not (`docs/agents/light-demo.md`).
 */

/** Every operation in the schema's `paths`, as `METHOD /api/path`. */
function schemaOperations(): string[] {
  const paths = schema.slice(schema.indexOf('export interface paths {'), schema.indexOf('export type webhooks'));
  const operations: string[] = [];
  let path: string | null = null;
  for (const line of paths.split('\n')) {
    const pathMatch = /^ {4}"(\/[^"]+)": \{/.exec(line);
    if (pathMatch) {
      path = pathMatch[1];
      continue;
    }
    // A method the path has is an object; one it hasn't is `get?: never`.
    const methodMatch = /^ {8}(get|put|post|delete|patch): \{/.exec(line);
    if (methodMatch && path) operations.push(`${methodMatch[1].toUpperCase()} ${path}`);
  }
  return operations;
}

describe('the light demo’s parity with the API', () => {
  registerHandlers();
  const operations = schemaOperations();
  const handled = handledRoutes();

  it('reads the schema', () => {
    expect(operations.length).toBeGreaterThan(200);
    expect(operations).toContain('GET /api/patients/search');
    expect(operations).toContain('POST /api/auth/login');
  });

  it('handles every endpoint, or says why not', () => {
    const missing = operations.filter((op) => !handled.includes(op) && !(op in notInLightDemo));
    expect(
      missing,
      'Give each a handler in src/demo/handlers/, or list it in src/demo/notInLightDemo.ts with a reason (docs/agents/light-demo.md)',
    ).toEqual([]);
  });

  it('lists only endpoints the API has, and none that also has a handler', () => {
    const listed = Object.keys(notInLightDemo);
    expect(listed.filter((op) => !operations.includes(op)), 'Listed but not in the schema').toEqual([]);
    expect(listed.filter((op) => handled.includes(op)), 'Listed but handled').toEqual([]);
  });

  it('gives a reason for each one left out', () => {
    for (const [op, reason] of Object.entries(notInLightDemo)) expect(reason.trim(), op).not.toBe('');
  });

  it('handles only endpoints the API has, each once', () => {
    expect(handled.filter((op) => !operations.includes(op)), 'Handled but not in the schema').toEqual([]);
    expect(handled.filter((op, i) => handled.indexOf(op) !== i), 'Handled twice').toEqual([]);
  });
});
