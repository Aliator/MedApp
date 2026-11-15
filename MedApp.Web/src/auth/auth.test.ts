import {QueryClient} from '@tanstack/react-query';
import {json, mockApi} from '../test/render';
import {whoamiQuery} from './auth';

const whoami = (body: unknown, status = 200) => {
  mockApi({'GET /api/auth/whoami': () => json(body, status)});
  return new QueryClient().fetchQuery(whoamiQuery);
};

const signedOut = {isAuthenticated: false, username: '', roles: [], isAdmin: false};

describe('whoamiQuery', () => {
  it('reads who is signed in, and whether they are an admin whatever the case of the role', async () => {
    expect(await whoami({isAuthenticated: true, name: 'test-admin', roles: ['admin', 'User']})).toEqual({
      isAuthenticated: true,
      username: 'test-admin',
      roles: ['admin', 'User'],
      isAdmin: true,
    });
  });

  it('takes an account the API sends without a name or roles as a user with none', async () => {
    expect(await whoami({isAuthenticated: true})).toEqual({isAuthenticated: true, username: '', roles: [], isAdmin: false});
  });

  it('is signed out when the API says so, or cannot answer', async () => {
    expect(await whoami({isAuthenticated: false, name: 'test-user'})).toEqual(signedOut);
    expect(await whoami({title: 'Server error'}, 500)).toEqual(signedOut);
  });
});
