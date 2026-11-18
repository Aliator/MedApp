// @vitest-environment node
import {readFileSync} from 'node:fs';
import {resolve} from 'node:path';
import {PERMISSION_CATALOGUE, PERMISSIONS} from './permissions';

/**
 * The web app's copy of the permission catalogue against the API's own (`Permissions.Catalogue`, read from its C#
 * source), so a permission added, renamed, regrouped or reworded there fails here until it's copied. In Node, for its
 * file system (tsconfig.ssr.json checks it, with Node's types).
 */
const source = readFileSync(resolve(__dirname, '../../../MedApp.Application/Common/Identity/Permissions.cs'), 'utf8');

/** Each permission in the API's catalogue, with its group and description. */
function apiCatalogue() {
  // C# string literals; none of the descriptions holds a quote.
  const entries = [...source.matchAll(/new\(Permission\.(\w+), PermissionGroup\.(\w+),\s*"([^"]*)"\)/g)];
  return entries.map(([, name, group, description]) => ({name, group, description}));
}

describe('the permission catalogue', () => {
  it("is the API's, in the same order, with the same groups and words", () => {
    const api = apiCatalogue();
    // Every permission the API has is found (a change to how the C# is written would otherwise pass unnoticed).
    expect(api.length).toBeGreaterThanOrEqual(25);
    expect(PERMISSIONS).toEqual(api.map((p) => p.name));
    for (const {name, group, description} of api) {
      expect(PERMISSION_CATALOGUE[name as keyof typeof PERMISSION_CATALOGUE]).toEqual({group, description});
    }
  });

  it("matches the API's enum, so a permission added there without a description is caught too", () => {
    const start = source.indexOf('public enum Permission');
    const body = source.slice(start, source.indexOf('}', start));
    const names = [...body.matchAll(/^\s*(\w+),\s*$/gm)].map(([, name]) => name);
    expect(names).toEqual([...PERMISSIONS]);
  });
});
