import {screen, waitFor, within} from '@testing-library/react';
import {DEFAULT_NEW_USER_PASSWORD, parsePasswordBody} from '../../api/users';
import {adminUser, calls, isDisabled, json, mockApi, openDialog, pageOf, renderWithApp, findAlert} from '../../test/render';
import type {UsersListSearch} from '../listSearch';
import {UsersPage} from './UsersPage';

const users = [
  {userId: 'u1', username: 'a.hartwell', roles: ['Admin', 'User']},
  {userId: 'u2', username: 'l.northcott', roles: ['User']},
  {userId: 'u3', username: adminUser.username, roles: ['Admin', 'User']},
];

function renderUsers(search: UsersListSearch = {}, extra: Parameters<typeof mockApi>[0] = {}) {
  const fetch = mockApi({
    'GET /api/auth/users/search': () => json(pageOf(users)),
    'GET /api/auth/roles': () => json(['User', 'Admin']),
    ...extra,
  });
  const onSearchChange = vi.fn();
  const result = renderWithApp(<UsersPage search={search} onSearchChange={onSearchChange} />, {user: adminUser});
  return {...result, fetch, onSearchChange};
}

const rowOf = async (username: string) => (await screen.findByText(username)).closest('tr')!;
const bodies = (fetch: ReturnType<typeof mockApi>, method: string) =>
  Promise.all(fetch.mock.calls.map(([r]) => r as Request).filter((r) => r.method === method).map((r) => r.clone().json()));

describe('UsersPage', () => {
  it('lists users with their roles', async () => {
    renderUsers();

    const row = await rowOf('a.hartwell');
    expect(within(row).getByText('Admin')).toBeInTheDocument();
    expect(screen.getByText('3 users in the system')).toBeInTheDocument();
  });

  it("doesn't let admins change their own account", async () => {
    renderUsers();

    const own = await rowOf(adminUser.username);
    for (const name of ['Assign roles', 'Revoke roles', 'Reset password', 'Delete user']) {
      expect(isDisabled(within(own).getByRole('button', {name}))).toBe(true);
    }
    expect(isDisabled(within(await rowOf('a.hartwell')).getByRole('button', {name: 'Delete user'}))).toBe(false);
  });

  it('asks the API for the page and filters in the URL', async () => {
    const {fetch} = renderUsers({page: 2, asc: false, username: 'hart', role: 'Admin'});

    await rowOf('a.hartwell');
    expect(calls(fetch)).toContain('GET /api/auth/users/search?PageSize=10&Page=2&Sort=username&Ascending=false&Username=hart&Role=Admin');
    expect(screen.getByText('3 users found')).toBeInTheDocument();
  });

  it('creates a user with the default password', async () => {
    const {user, fetch} = renderUsers({}, {'POST /api/auth/users': () => new Response(null, {status: 204})});

    await user.click(await screen.findByRole('button', {name: 'Add User'}));
    expect(within(openDialog()!).getByText(`Default password will be: ${DEFAULT_NEW_USER_PASSWORD}`)).toBeInTheDocument();
    await user.type(within(openDialog()!).getByLabelText(/username/i), 'new.user');
    await user.click(within(openDialog()!).getByRole('button', {name: 'Create User'}));

    await waitFor(() => expect(openDialog()).toBeNull());
    expect(await bodies(fetch, 'POST')).toEqual([{username: 'new.user', password: DEFAULT_NEW_USER_PASSWORD}]);
  });

  it('assigns only roles the user lacks, one request per role', async () => {
    const {user, fetch} = renderUsers({}, {'POST /api/auth/roles/assign': () => new Response(null, {status: 200})});

    await user.click(within(await rowOf('l.northcott')).getByRole('button', {name: 'Assign roles'}));
    const dialog = openDialog()!;
    expect(within(dialog).queryByRole('checkbox', {name: 'User'})).not.toBeInTheDocument();
    await user.click(within(dialog).getByRole('checkbox', {name: 'Admin'}));
    await user.click(within(dialog).getByRole('button', {name: 'Assign Roles'}));

    await waitFor(() => expect(openDialog()).toBeNull());
    expect(await bodies(fetch, 'POST')).toEqual([{username: 'l.northcott', role: 'Admin'}]);
  });

  it("revokes only roles the user has", async () => {
    const {user, fetch} = renderUsers({}, {'POST /api/auth/roles/revoke': () => new Response(null, {status: 200})});

    await user.click(within(await rowOf('a.hartwell')).getByRole('button', {name: 'Revoke roles'}));
    const dialog = openDialog()!;
    await user.click(within(dialog).getByRole('checkbox', {name: 'Admin'}));
    await user.click(within(dialog).getByRole('checkbox', {name: 'User'}));
    await user.click(within(dialog).getByRole('button', {name: 'Revoke Roles'}));

    await waitFor(() => expect(openDialog()).toBeNull());
    expect(await bodies(fetch, 'POST')).toEqual([
      {username: 'a.hartwell', role: 'Admin'},
      {username: 'a.hartwell', role: 'User'},
    ]);
  });

  it('searches by username, starting on page 1', async () => {
    const {user, onSearchChange} = renderUsers({page: 2});

    await user.click(await screen.findByRole('button', {name: 'Search'}));
    await user.type(within(openDialog()!).getByLabelText(/username/i), 'hart');
    await user.click(within(openDialog()!).getByRole('button', {name: 'Search'}));

    expect(onSearchChange).toHaveBeenLastCalledWith({asc: undefined, username: 'hart', role: undefined});
  });

  it('clears an active search', async () => {
    const {user, onSearchChange} = renderUsers({role: 'Admin', asc: false});

    await user.click(await screen.findByRole('button', {name: 'Clear Search'}));

    expect(onSearchChange).toHaveBeenLastCalledWith({asc: false});
  });

  it('resets a password and shows the new one', async () => {
    const {user} = renderUsers({}, {'PUT /api/auth/users/*': () => new Response('Synth3tic', {headers: {'content-type': 'text/plain'}})});

    await user.click(within(await rowOf('a.hartwell')).getByRole('button', {name: 'Reset password'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Reset Password'}));

    expect(await within(openDialog()!).findByText('Synth3tic')).toBeInTheDocument();
    await user.click(within(openDialog()!).getByRole('button', {name: 'Done'}));
    expect(openDialog()).toBeNull();
  });

  it('deletes a user after confirming', async () => {
    const {user, fetch} = renderUsers({}, {'DELETE /api/auth/users/*': () => new Response(null, {status: 204})});

    await user.click(within(await rowOf('a.hartwell')).getByRole('button', {name: 'Delete user'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Delete User'}));

    await waitFor(() => expect(calls(fetch)).toContain('DELETE /api/auth/users/a.hartwell'));
  });

  it('shows an error when an action fails', async () => {
    const {user} = renderUsers({}, {'DELETE /api/auth/users/*': () => json({}, 500)});

    await user.click(within(await rowOf('a.hartwell')).getByRole('button', {name: 'Delete user'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Delete User'}));

    await findAlert('The user could not be deleted.');
  });
});

describe('parsePasswordBody', () => {
  it.each([
    ['"Synth3tic"', 'Synth3tic'],
    ['Synth3tic', 'Synth3tic'],
    ['  Synth3tic\n', 'Synth3tic'],
  ])('reads %j', (body, expected) => {
    expect(parsePasswordBody(body)).toBe(expected);
  });

  it.each([['""'], [''], [null], ['"unterminated']])('rejects %j', (body) => {
    expect(parsePasswordBody(body)).toBeNull();
  });
});
