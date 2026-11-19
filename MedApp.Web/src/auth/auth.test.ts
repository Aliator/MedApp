import {QueryClient} from '@tanstack/react-query';
import {json, mockApi} from '../test/render';
import {whoamiQuery} from './auth';

const whoami = (body: unknown, status = 200) => {
  mockApi({'GET /api/auth/whoami': () => json(body, status)});
  return new QueryClient().fetchQuery(whoamiQuery);
};

const signedOut = {isAuthenticated: false, username: '', roles: [], permissions: [], mustChangePassword: false, mustSetUpTwoFactor: false, teams: []};

describe('whoamiQuery', () => {
  it('reads who is signed in, their roles, and what their roles allow (only permissions it knows, each once)', async () => {
    expect(
      await whoami({
        isAuthenticated: true,
        name: 'test-admin',
        roles: ['Admin', 'Clinician'],
        permissions: ['ViewPatients', 'UseApp', 'SomethingNewer', 'UseApp'],
      }),
    ).toEqual({
      isAuthenticated: true,
      username: 'test-admin',
      roles: ['Admin', 'Clinician'],
      permissions: ['UseApp', 'ViewPatients'],
      mustChangePassword: false,
      mustSetUpTwoFactor: false,
      // Worked out from the roles when the API doesn't say (an admin is in no team).
      accountType: 'Admin',
      teams: [],
    });
  });

  it('says when the user must change the password they were given', async () => {
    expect(await whoami({isAuthenticated: true, name: 'test-new-user', roles: ['User'], mustChangePassword: true})).toEqual({
      isAuthenticated: true,
      username: 'test-new-user',
      roles: ['User'],
      permissions: [],
      mustChangePassword: true,
      mustSetUpTwoFactor: false,
      accountType: 'Staff',
      teams: [],
    });
  });

  it('says when an admin must set up two-factor authentication', async () => {
    expect(await whoami({isAuthenticated: true, name: 'test-admin', roles: ['Admin'], mustSetUpTwoFactor: true})).toMatchObject({
      mustChangePassword: false,
      mustSetUpTwoFactor: true,
    });
  });

  it('takes an account the API sends without a name or roles as a user with none', async () => {
    expect(await whoami({isAuthenticated: true})).toEqual({
      isAuthenticated: true,
      username: '',
      roles: [],
      permissions: [],
      mustChangePassword: false,
      mustSetUpTwoFactor: false,
      accountType: 'Staff',
      teams: [],
    });
  });

  it('is signed out when the API says so, or cannot answer', async () => {
    expect(await whoami({isAuthenticated: false, name: 'test-user'})).toEqual(signedOut);
    expect(await whoami({title: 'Server error'}, 500)).toEqual(signedOut);
  });
});
