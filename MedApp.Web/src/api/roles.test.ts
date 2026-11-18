import {roleResponse} from '../test/roles';
import {people, roleChoices, roleNames} from './roles';

const roles = [
  roleResponse('Nurse', {description: 'Nurses.'}),
  roleResponse('Admin'),
  roleResponse('Lab', {isArchived: true}),
  roleResponse('Booking', {description: ''}),
];

describe('roles', () => {
  it('lists every name, archived ones too, sorted, for the Users filter', () => {
    expect(roleNames(roles)).toEqual(['Admin', 'Booking', 'Lab', 'Nurse']);
  });

  it('offers only roles that are not archived, with their descriptions, keeping an archived one already held', () => {
    expect(roleChoices(roles)).toEqual([
      {value: 'Nurse', label: 'Nurse', description: 'Nurses.'},
      {value: 'Admin', label: 'Admin', description: 'Synthetic Admin role.'},
      {value: 'Booking', label: 'Booking', description: undefined},
    ]);
    expect(roleChoices(roles, ['Lab']).map((c) => c.label)).toContain('Lab (archived)');
  });

  it('counts people', () => {
    expect(people(1)).toBe('1 person');
    expect(people(6)).toBe('6 people');
  });
});
