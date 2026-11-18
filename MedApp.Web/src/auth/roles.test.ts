import {rolesSummary, sortRoles} from './roles';

describe('roles', () => {
  it('sorts Admin first, then the rest by name', () => {
    expect(sortRoles(['Nurse', 'Admin', 'Clinician'])).toEqual(['Admin', 'Clinician', 'Nurse']);
    expect(sortRoles([])).toEqual([]);
  });

  it('sums roles up in a few words', () => {
    expect(rolesSummary([])).toBe('No role');
    expect(rolesSummary(['Nurse'])).toBe('Nurse');
    expect(rolesSummary(['Nurse', 'Admin', 'Lab'])).toBe('Admin +2');
  });
});
