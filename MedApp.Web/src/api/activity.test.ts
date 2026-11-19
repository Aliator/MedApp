import {ACCESS_ACTIONS, ACCOUNT_ACTIONS, ACTIVITY_ACTIONS, activityActionLabel, activitySearchQuery, describeActivity, FILE_ACTIONS} from './activity';

const describe_ = (action: string, details: unknown) => describeActivity({action, details: JSON.stringify(details)});

describe('activityActionLabel', () => {
  it('puts actions into words', () => {
    expect(activityActionLabel('PatientListed')).toBe('In search results');
    expect(activityActionLabel('SignInFailed')).toBe('Sign-in refused');
    expect(activityActionLabel('SomethingNew')).toBe('SomethingNew');
    expect(activityActionLabel(null)).toBe('');
    expect(activityActionLabel(undefined)).toBe('');
  });

  it('names the actions on files, and counts them as access to the record', () => {
    expect(FILE_ACTIONS.map(activityActionLabel)).toEqual(['Files viewed', 'File downloaded']);
    expect(ACCESS_ACTIONS).toEqual(expect.arrayContaining(['AttachmentsViewed', 'AttachmentDownloaded']));
  });

  it('names the data request actions, and counts downloading a pack as access to the record', () => {
    expect(activityActionLabel('DataRequestPackDownloaded')).toBe('Data request pack downloaded');
    expect(activityActionLabel('DataRequestChanged')).toBe('Data request changed');
    expect(ACCESS_ACTIONS).toContain('DataRequestPackDownloaded');
    expect(ACCOUNT_ACTIONS).not.toContain('DataRequestChanged');
  });

  it('has words for every action', () => {
    for (const action of ACTIVITY_ACTIONS) expect(activityActionLabel(action)).not.toBe(action);
  });
});

describe('describeActivity', () => {
  it("says what a data request's change was, in words", () => {
    const dataRequestId = '000000d1-0000-0000-0000-000000000001';
    expect(describe_('DataRequestChanged', {dataRequestId, change: 'Recorded'})).toBe('Request logged');
    expect(describe_('DataRequestChanged', {dataRequestId, change: 'IdentityChecked'})).toBe('Identity checked');
    expect(describe_('DataRequestChanged', {dataRequestId, change: 'Extended'})).toBe('Due date extended');
    expect(describe_('DataRequestChanged', {dataRequestId, change: 'Reviewed'})).toBe('Clinician review recorded');
    expect(describe_('DataRequestChanged', {dataRequestId, change: 'Sent'})).toBe('Response sent');
    expect(describe_('DataRequestChanged', {dataRequestId, change: 'Refused'})).toBe('Request refused');
    expect(describe_('DataRequestChanged', {dataRequestId, change: 'Withdrawn'})).toBe('Request withdrawn');
    expect(describe_('DataRequestChanged', {dataRequestId})).toBe('');
    expect(describe_('DataRequestPackDownloaded', {dataRequestId})).toBe('');
  });

  it('names the fields an edit changed, never their values', () => {
    expect(describe_('PatientUpdated', {fields: ['firstName', 'email', 'dateOfBirth', 'nhsNumber', 'somethingElse']})).toBe(
      'Changed first name, email, date of birth, NHS number, somethingElse',
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
    expect(describe_('PatientsSearched', {filters: ['nhsNumber'], resultCount: 1})).toBe('By NHS number: 1 found');
  });

  it('says when a search came through the FHIR API', () => {
    expect(describe_('PatientsSearched', {filters: ['nhsNumber'], resultCount: 0, via: 'fhir'})).toBe('By NHS number (FHIR API): 0 found');
  });

  it('says whether an NHS number look-up found anyone', () => {
    expect(activityActionLabel('DemographicsLookedUp')).toBe('NHS number looked up');
    expect(describe_('DemographicsLookedUp', {found: true})).toBe('Found');
    expect(describe_('DemographicsLookedUp', {found: false})).toBe('Nobody found');
    expect(describe_('DemographicsLookedUp', {})).toBe('');
  });

  it('says why a sign-in was refused', () => {
    expect(describe_('SignInFailed', {reason: 'LockedOut'})).toBe('Account locked');
    expect(describe_('SignInFailed', {reason: 'InvalidCredentials'})).toBe('Wrong username or password');
    expect(describe_('SignInFailed', {reason: 'InvalidCode', secondStep: true})).toBe('Wrong two-factor code');
    expect(describe_('SignInFailed', {reason: 'ChallengeExpired'})).toBe('Code step timed out');
    expect(describe_('SignInFailed', {reason: 'TooManyCodes', secondStep: true})).toBe('Too many codes for the account');
    expect(describe_('SignedIn', {secondStep: 'recoveryCode'})).toBe('With a recovery code');
    expect(describe_('SignedIn', {secondStep: 'authenticator'})).toBe('With an authenticator code');
    expect(describe_('SignedIn', {secondStep: 'email'})).toBe('With an emailed code');
    expect(describe_('SignInFailed', {reason: 'InvalidCodeLockedOut', secondStep: true})).toBe('Wrong two-factor code: account locked');
    expect(describe_('TwoFactorChallenged', {method: 'email'})).toBe('Code emailed');
    expect(describe_('TwoFactorChallenged', {})).toBe('');
    expect(describe_('TwoFactorEnabled', {switchedFrom: 'email'})).toBe('Switched from codes by email');
    expect(describe_('TwoFactorEnabled', {})).toBe('');
    expect(activityActionLabel('EmailCodesEnabled')).toBe('Codes by email turned on');
    expect(activityActionLabel('EmailCodesDisabled')).toBe('Codes by email turned off');
    expect(describe_('SignedIn', {})).toBe('');
    expect(describe_('RecoveryCodeUsed', {codesLeft: 9})).toBe('9 left');
    expect(describe_('RecoveryCodeUsed', {})).toBe('');
    expect(describe_('PasswordResetRequested', {matched: true})).toBe('Link sent');
    expect(describe_('PasswordResetRequested', {matched: false})).toBe('No account matched');
    expect(describe_('PasswordResetRequested', {})).toBe('');
    expect(describe_('TwoFactorRequirementChanged', {requireTwoFactorForAdmins: true})).toBe('Required for admins');
    expect(describe_('TwoFactorRequirementChanged', {requireTwoFactorForAdmins: false})).toBe('Not required');
    expect(describe_('TwoFactorRequirementChanged', {})).toBe('');
    expect(activityActionLabel('TwoFactorChallenged')).toBe('Code requested');
  });

  it('gives roles changed, added and removed', () => {
    expect(describe_('RolesChanged', {from: ['User'], to: ['Admin', 'User']})).toBe('User → Admin, User');
    expect(describe_('RolesChanged', {from: [], to: ['User']})).toBe('none → User');
    expect(describe_('RolesChanged', {added: 'Admin'})).toBe('Added Admin');
    expect(describe_('RolesChanged', {removed: 'Admin'})).toBe('Removed Admin');
    expect(describe_('RoleCreated', {role: 'Auditor'})).toBe('Auditor');
    expect(describe_('RoleDeleted', {role: 'Auditor'})).toBe('Auditor');
    expect(describe_('RoleDeleted', {})).toBe('');
    expect(describe_('RoleCreated', {role: 'Auditor', permissions: ['ViewPatients', 'ViewInsights']})).toBe(
      'Auditor: ViewPatients, ViewInsights',
    );
  });

  it('gives what a change to a role did, by permission name, never its description', () => {
    expect(activityActionLabel('RoleChanged')).toBe('Role changed');
    expect(ACCOUNT_ACTIONS).toContain('RoleChanged');
    expect(
      describe_('RoleChanged', {
        role: 'Nurse',
        renamedFrom: 'Nurses',
        descriptionChanged: true,
        added: ['AssignPathways'],
        removed: ['DeletePatients', 'EditPatients'],
      }),
    ).toBe('Nurse: renamed from Nurses; description changed; added AssignPathways; removed DeletePatients, EditPatients');
    expect(describe_('RoleChanged', {role: 'Lab', archived: true})).toBe('Lab: archived');
    expect(describe_('RoleChanged', {role: 'Lab', archived: false})).toBe('Lab: restored');
    expect(describe_('RoleChanged', {})).toBe('');
  });

  it("gives a member of staff's teams before and after, by name, as an account change", () => {
    expect(activityActionLabel('TeamMembershipChanged')).toBe('Teams changed');
    expect(describe_('TeamMembershipChanged', {from: [], to: ['Dermatology', 'Urology'], fromIds: [], toIds: ['s1', 's2']})).toBe(
      'none → Dermatology, Urology',
    );
    expect(describe_('TeamMembershipChanged', {from: ['Urology'], to: []})).toBe('Urology → none');
    expect(ACCOUNT_ACTIONS).toContain('TeamMembershipChanged');
  });

  it("gives which of a member of staff's details for letters changed, never their values, as an account change", () => {
    expect(activityActionLabel('StaffDetailsChanged')).toBe('Staff details changed');
    expect(describe_('StaffDetailsChanged', {fields: ['fullName', 'jobTitle']})).toBe('Changed full name, job title');
    expect(describe_('StaffDetailsChanged', {})).toBe('');
    expect(ACCOUNT_ACTIONS).toContain('StaffDetailsChanged');
  });

  it("names a patient's contact details and GP practice among the fields edited", () => {
    expect(describe_('PatientUpdated', {fields: ['addressLines', 'postcode', 'mobilePhone', 'practice']})).toBe('Changed address, postcode, mobile, GP practice');
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

describe('describeActivity: files', () => {
  it("says whether the files were the patient's own or a pathway's (or a stage's), never which file", () => {
    const pathwayId = '00000037-0000-0000-0000-000000000001';
    expect(describeActivity({action: 'AttachmentsViewed', details: '{}'})).toBe("All the patient's files");
    expect(describeActivity({action: 'AttachmentsViewed', details: '{}', pathwayId})).toBe("A pathway's or stage's files");
    expect(describeActivity({action: 'AttachmentDownloaded', details: '{}', pathwayId: null})).toBe('A file on the patient');
    expect(describeActivity({action: 'AttachmentDownloaded', details: '{}', pathwayId})).toBe('A file on a pathway or stage');
  });
});

describe('activitySearchQuery', () => {
  it('leaves out empty filters, and keys the page under activity', () => {
    const query = activitySearchQuery({Page: 2, Search: '', PatientId: undefined, From: '2026-11-01'});

    expect(query.queryKey).toEqual(['activity', {PageSize: 10, Page: 2, From: '2026-11-01'}]);
  });
});
