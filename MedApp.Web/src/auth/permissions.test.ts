import {can, isPermission, knownPermissions, needsPermissionText} from './permissions';

describe('permission helpers', () => {
  it('knows its own names only, as they are spelled', () => {
    expect(isPermission('ViewInsights')).toBe(true);
    expect(isPermission('viewinsights')).toBe(false);
    expect(isPermission('Admin')).toBe(false);
    expect(isPermission(3)).toBe(false);
  });

  it("keeps the permissions it knows from whoami's list, each once, in the catalogue's order", () => {
    expect(knownPermissions(['ViewPatients', 'UseApp', 'Newer', null, 'UseApp'])).toEqual(['UseApp', 'ViewPatients']);
    expect(knownPermissions(undefined)).toEqual([]);
  });

  it('says whether a person has a permission', () => {
    expect(can({permissions: ['UseApp', 'ViewInsights']}, 'ViewInsights')).toBe(true);
    expect(can({permissions: ['UseApp']}, 'ViewInsights')).toBe(false);
  });

  it("says what a permission is needed for, in the API's words", () => {
    expect(needsPermissionText('ViewInsights')).toBe('It needs the permission to: See Insights.');
  });
});
