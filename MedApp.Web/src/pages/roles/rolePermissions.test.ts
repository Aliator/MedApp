import {PERMISSIONS} from '../../auth/permissions';
import {
  inCatalogueOrder,
  isAdminOnly,
  PERMISSION_GROUPS,
  permissionChanges,
  permissionGroups,
  permissionLabel,
} from './rolePermissions';

describe('rolePermissions', () => {
  it('names a permission by its hand-written label, or one it does not know in words', () => {
    expect(permissionLabel('ManageAnyCalendar')).toBe('Manage any calendar');
    expect(permissionLabel('ViewPatients')).toBe('View patients');
    expect(permissionLabel('RecordMdtOutcomes')).toBe('Record MDT outcomes');
    expect(permissionLabel('ManageClinics')).toBe('Set working hours');
    expect(permissionLabel('SomethingNew')).toBe('Something new');
  });

  it('groups every permission but UseApp, in the catalogue order, Admin only last', () => {
    const groups = permissionGroups('');
    expect(groups.map((g) => g.title)).toEqual(PERMISSION_GROUPS.map((g) => g.title));
    expect(groups.at(-1)?.title).toBe('Admin only');
    expect(groups.flatMap((g) => g.permissions)).toEqual(PERMISSIONS.filter((p) => p !== 'UseApp'));
    expect(groups.find((g) => g.group === 'AppointmentsAndCalendars')?.permissions).toContain('ManageClinics');
  });

  it('filters by name, words or description, ignoring case, leaving out empty groups', () => {
    expect(permissionGroups('MEETINGS').flatMap((g) => g.permissions)).toEqual(['ArrangeMeetings']);
    expect(permissionGroups('any calendar').flatMap((g) => g.permissions)).toEqual(['ManageAnyCalendar']);
    expect(permissionGroups('insights').map((g) => g.group)).toEqual(['Oversight']);
    expect(permissionGroups('nothing like this')).toEqual([]);
  });

  it("says what's added and removed, in the catalogue order", () => {
    expect(permissionChanges(['WorkPathways', 'ViewPatients'], ['AttachFiles', 'ViewPatients', 'EditPatients'])).toEqual({
      added: ['EditPatients', 'AttachFiles'],
      removed: ['WorkPathways'],
    });
  });

  it('puts ticks in the catalogue order, each once, dropping names it does not know', () => {
    expect(inCatalogueOrder(['AttachFiles', 'ViewPatients', 'AttachFiles', 'Unknown'])).toEqual(['ViewPatients', 'AttachFiles']);
  });

  it('knows which are for the Admin role only', () => {
    expect(isAdminOnly('ManageRoles')).toBe(true);
    expect(isAdminOnly('ViewPatients')).toBe(false);
  });
});
