import {describe, expect, it} from 'vitest';
import {accountTypeOf, isAccountType, isTeamMemberRole} from './accountTypes';

describe('accountTypeOf', () => {
  it('makes anyone holding Admin an admin, whatever their teams', () => {
    expect(accountTypeOf(['Admin'], [])).toBe('Admin');
    expect(accountTypeOf(['Admin', 'Clinician'], ['ClinicAdmin'])).toBe('Admin');
  });

  it('makes staff who are a clinic admin of any team a clinic admin', () => {
    expect(accountTypeOf(['Clinician'], ['Member', 'ClinicAdmin'])).toBe('ClinicAdmin');
  });

  it('makes everyone else staff, with a team or without', () => {
    expect(accountTypeOf(['Lab'], ['Member'])).toBe('Staff');
    expect(accountTypeOf([], [])).toBe('Staff');
  });
});

describe('isAccountType and isTeamMemberRole', () => {
  it('know only the API’s names', () => {
    expect(isAccountType('ClinicAdmin')).toBe(true);
    expect(isAccountType('Clinic admin')).toBe(false);
    expect(isTeamMemberRole('Member')).toBe(true);
    expect(isTeamMemberRole('Lead')).toBe(false);
  });
});
