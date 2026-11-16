import {ACTIVITY_ACTIONS, activityActionLabel, activitySearchQuery, describeActivity} from './activity';

const describe_ = (action: string, details: unknown) => describeActivity({action, details: JSON.stringify(details)});

describe('activityActionLabel', () => {
  it('puts actions into words', () => {
    expect(activityActionLabel('PatientListed')).toBe('In search results');
    expect(activityActionLabel('SignInFailed')).toBe('Sign-in refused');
    expect(activityActionLabel('SomethingNew')).toBe('SomethingNew');
    expect(activityActionLabel(null)).toBe('');
    expect(activityActionLabel(undefined)).toBe('');
  });

  it('has words for every action', () => {
    for (const action of ACTIVITY_ACTIONS) expect(activityActionLabel(action)).not.toBe(action);
  });
});

describe('describeActivity', () => {
  it('names the fields an edit changed, never their values', () => {
    expect(describe_('PatientUpdated', {fields: ['firstName', 'email', 'dateOfBirth', 'somethingElse']})).toBe(
      'Changed first name, email, date of birth, somethingElse',
    );
    expect(describe_('PatientUpdated', {fields: []})).toBe('Nothing changed');
    expect(describe_('PatientUpdated', {})).toBe('Nothing changed');
  });

  it('says which filters a search used, and how many it found', () => {
    expect(describe_('PatientsSearched', {filters: ['lastName', 'dateOfBirthFrom', 'dateOfBirthTo'], resultCount: 3})).toBe(
      'By last name, born from, born to: 3 found',
    );
    expect(describe_('PatientsSearched', {filters: ['all'], resultCount: 40})).toBe('All patients: 40 found');
    expect(describe_('PatientsSearched', {filters: ['email']})).toBe('By email');
  });

  it('says why a sign-in was refused', () => {
    expect(describe_('SignInFailed', {reason: 'LockedOut'})).toBe('Account locked');
    expect(describe_('SignInFailed', {reason: 'InvalidCredentials'})).toBe('Wrong username or password');
  });

  it('gives roles changed, added and removed', () => {
    expect(describe_('RolesChanged', {from: ['User'], to: ['Admin', 'User']})).toBe('User → Admin, User');
    expect(describe_('RolesChanged', {from: [], to: ['User']})).toBe('none → User');
    expect(describe_('RolesChanged', {added: 'Admin'})).toBe('Added Admin');
    expect(describe_('RolesChanged', {removed: 'Admin'})).toBe('Removed Admin');
    expect(describe_('RoleCreated', {role: 'Auditor'})).toBe('Auditor');
    expect(describe_('RoleDeleted', {role: 'Auditor'})).toBe('Auditor');
    expect(describe_('RoleDeleted', {})).toBe('');
  });

  it('gives a retention decision: until when, and why', () => {
    expect(describe_('RetentionKept', {keepUntil: '2027-11-16', reason: 'Legal hold'})).toBe('Until 16 Nov 2027: Legal hold');
    expect(describe_('RetentionKept', {reason: 'Legal hold'})).toBe('Legal hold');
  });

  it.each(['PatientRestricted', 'PatientRestrictionLifted', 'PatientDisputed', 'PatientDisputeCleared', 'PatientAnonymised'])(
    'gives the reason for %s',
    (action) => {
      expect(describe_(action, {reason: '  Synthetic reason  '})).toBe('Synthetic reason');
      expect(describe_(action, {})).toBe('');
    },
  );

  it('says nothing more for other actions, or details it cannot read', () => {
    expect(describe_('PatientViewed', {})).toBe('');
    expect(describeActivity({action: 'PatientUpdated', details: '{not json'})).toBe('Nothing changed');
    expect(describeActivity({action: 'SignInFailed', details: '[1]'})).toBe('Wrong username or password');
    expect(describeActivity({action: 'RoleCreated', details: null})).toBe('');
  });
});

describe('activitySearchQuery', () => {
  it('leaves out empty filters, and keys the page under activity', () => {
    const query = activitySearchQuery({Page: 2, Search: '', PatientId: undefined, From: '2026-11-01'});

    expect(query.queryKey).toEqual(['activity', {PageSize: 10, Page: 2, From: '2026-11-01'}]);
  });
});
