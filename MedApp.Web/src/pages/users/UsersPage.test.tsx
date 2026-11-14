import {screen, waitFor, within} from '@testing-library/react';
import {DEFAULT_NEW_USER_PASSWORD, parsePasswordBody} from '../../api/users';
import {adminUser, calls, isDisabled, json, mockApi, openDialog, pageOf, pickFavourite, renderWithApp, findAlert, searchBar} from '../../test/render';
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
    for (const name of ['Edit roles', 'Reset password', 'Delete user']) {
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

  it("edits a user's roles in one dialog, starting from theirs, saved in one request", async () => {
    const {user, fetch} = renderUsers({}, {'PUT /api/auth/users/l.northcott/roles': () => new Response(null, {status: 204})});

    await user.click(within(await rowOf('l.northcott')).getByRole('button', {name: 'Edit roles'}));
    const dialog = within(openDialog()!);
    expect(dialog.getByRole('checkbox', {name: 'User'})).toBeChecked();
    expect(dialog.getByRole('checkbox', {name: 'Admin'})).not.toBeChecked();
    // Nothing changed yet, so nothing to save.
    expect(isDisabled(dialog.getByRole('button', {name: 'Save Roles'}))).toBe(true);

    await user.click(dialog.getByRole('checkbox', {name: 'Admin'}));
    await user.click(dialog.getByRole('checkbox', {name: 'User'}));
    await user.click(dialog.getByRole('button', {name: 'Save Roles'}));

    await waitFor(() => expect(openDialog()).toBeNull());
    expect(await bodies(fetch, 'PUT')).toEqual([{roles: ['Admin']}]);
  });

  it('can take every role away', async () => {
    const {user, fetch} = renderUsers({}, {'PUT /api/auth/users/l.northcott/roles': () => new Response(null, {status: 204})});

    await user.click(within(await rowOf('l.northcott')).getByRole('button', {name: 'Edit roles'}));
    const dialog = within(openDialog()!);
    await user.click(dialog.getByRole('checkbox', {name: 'User'}));
    await user.click(dialog.getByRole('button', {name: 'Save Roles'}));

    await waitFor(() => expect(openDialog()).toBeNull());
    expect(await bodies(fetch, 'PUT')).toEqual([{roles: []}]);
  });

  it('says when the roles could not be saved', async () => {
    const {user} = renderUsers({}, {'PUT /api/auth/users/l.northcott/roles': () => json([{code: 'RoleNotFound', description: 'Role not found.'}], 400)});

    await user.click(within(await rowOf('l.northcott')).getByRole('button', {name: 'Edit roles'}));
    await user.click(within(openDialog()!).getByRole('checkbox', {name: 'Admin'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Save Roles'}));

    expect(await findAlert('The roles could not be saved.')).toBeInTheDocument();
  });

  it('shows the search as tokens, and the Admins favourite filters by role, starting on page 1', async () => {
    const {user, onSearchChange} = renderUsers({page: 2, username: 'hart'});

    await waitFor(() => expect(searchBar()).toHaveTextContent(/User: contains\s*hart/));
    await pickFavourite(user, 'Admins');

    expect(onSearchChange).toHaveBeenLastCalledWith(expect.objectContaining({role: 'Admin', username: undefined, page: undefined}));
  });

  it('clears the search from the bar, keeping the order', async () => {
    const {user, onSearchChange} = renderUsers({role: 'Admin', asc: false});

    await waitFor(() => expect(searchBar()).toHaveTextContent(/Role: is/));
    await user.click(within(searchBar()).getByRole('button', {name: /clear/i}));

    expect(onSearchChange).toHaveBeenLastCalledWith(expect.objectContaining({asc: false, role: undefined, page: undefined}));
  });

  it('resets a password and shows the new one', async () => {
    const {user} = renderUsers({}, {'PUT /api/auth/users/*': () => new Response('Synth3tic', {headers: {'content-type': 'text/plain'}})});

    await user.click(within(await rowOf('a.hartwell')).getByRole('button', {name: 'Reset password'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Reset Password'}));

    expect(await within(openDialog()!).findByText('Synth3tic')).toBeInTheDocument();
    await user.click(within(openDialog()!).getByRole('button', {name: 'Done'}));
    expect(openDialog()).toBeNull();
  });

  it('deletes a user once the admin enters their own password', async () => {
    const {user, fetch} = renderUsers({}, {'DELETE /api/auth/users/*': () => new Response(null, {status: 204})});

    await user.click(within(await rowOf('a.hartwell')).getByRole('button', {name: 'Delete user'}));
    const dialog = within(openDialog()!);
    expect(isDisabled(dialog.getByRole('button', {name: 'Delete User'}))).toBe(true);
    await user.type(dialog.getByLabelText(/your password/i), 'admin-password');
    await user.click(dialog.getByRole('button', {name: 'Delete User'}));

    await waitFor(() => expect(calls(fetch)).toContain('DELETE /api/auth/users/a.hartwell'));
    expect(await bodies(fetch, 'DELETE')).toEqual([{password: 'admin-password'}]);
  });

  it('keeps the dialog open and says so when the password is wrong', async () => {
    const {user} = renderUsers({}, {
      'DELETE /api/auth/users/*': () => json([{code: 'PasswordIncorrect', description: 'Your password is incorrect.'}], 400),
    });

    await user.click(within(await rowOf('a.hartwell')).getByRole('button', {name: 'Delete user'}));
    await user.type(within(openDialog()!).getByLabelText(/your password/i), 'wrong');
    await user.click(within(openDialog()!).getByRole('button', {name: 'Delete User'}));

    expect((await within(openDialog()!).findAllByText('Your password is incorrect.')).length).toBeGreaterThan(0);
  });

  it('shows an error when an action fails', async () => {
    const {user} = renderUsers({}, {'DELETE /api/auth/users/*': () => json({}, 500)});

    await user.click(within(await rowOf('a.hartwell')).getByRole('button', {name: 'Delete user'}));
    await user.type(within(openDialog()!).getByLabelText(/your password/i), 'admin-password');
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
