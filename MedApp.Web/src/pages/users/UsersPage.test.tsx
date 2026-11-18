import type {QueryClient} from '@tanstack/react-query';
import {act, screen, waitFor, within} from '@testing-library/react';
import {parsePasswordBody} from '../../api/users';
import {adminUser, calls, isDisabled, json, mockApi, openDialog, pageOf, openSearch, pickFavourite, renderWithApp, findAlert, searchBar} from '../../test/render';
import type {UsersListSearch} from '../listSearch';
import {roleResponse} from '../../test/roles';
import {staffDetailsLine, UsersPage} from './UsersPage';

const users = [
  {userId: 'u1', username: 'a.hartwell', roles: ['Admin', 'User']},
  {userId: 'u2', username: 'l.northcott', roles: ['User'], fullName: 'Dr Lee Test', jobTitle: 'Consultant dermatologist'},
  {userId: 'u3', username: adminUser.username, roles: ['Admin', 'User']},
];

function renderUsers(search: UsersListSearch = {}, extra: Parameters<typeof mockApi>[0] = {}) {
  const fetch = mockApi({
    'GET /api/auth/users/search': () => json(pageOf(users)),
    'GET /api/roles': () => json([roleResponse('Admin'), roleResponse('User')]),
    ...extra,
  });
  const onSearchChange = vi.fn();
  const result = renderWithApp(<UsersPage search={search} onSearchChange={onSearchChange} />, {user: adminUser});
  return {...result, fetch, onSearchChange};
}

const rowOf = async (username: string) => (await screen.findByText(username)).closest('tr')!;
const bodies = (fetch: ReturnType<typeof mockApi>, method: string) =>
  Promise.all(fetch.mock.calls.map(([r]) => r as Request).filter((r) => r.method === method).map((r) => r.clone().json()));

type User = ReturnType<typeof renderUsers>['user'];

const FIRST_PASSWORD = 'Synth3ticFirst';
const createdUser = {'POST /api/auth/users': () => json({username: 'new.user', password: FIRST_PASSWORD})};

/** Opens Add User, types the name and creates it, then waits for the generated password. */
async function createNewUser(user: User) {
  await user.click(await screen.findByRole('button', {name: 'Add User'}));
  await user.type(within(openDialog()!).getByLabelText(/username/i), 'new.user');
  await user.click(within(openDialog()!).getByRole('button', {name: 'Create User'}));
  await within(openDialog()!).findByText(FIRST_PASSWORD);
}

/** Waits out the moment "Done" ignores clicks after a password appears (PASSWORD_DONE_HOLD_MS). */
const afterTheHold = () => act(() => vi.advanceTimersByTimeAsync(1000));

/** Whether a password is still anywhere: in the page (closed dialogs stay in it) or in the mutation cache. */
const passwordKept = (queryClient: QueryClient, password: string) =>
  document.body.textContent!.includes(password) ||
  queryClient.getMutationCache().getAll().some((m) => JSON.stringify(m.state.data ?? null).includes(password));

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
    for (const name of ['Edit roles', 'Reset password', 'Sign out everywhere', 'Delete user']) {
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

  it('creates a user with only a username, then shows the generated password once, focused and announced', async () => {
    vi.useFakeTimers({shouldAdvanceTime: true});
    localStorage.setItem('medapp.accessibility', JSON.stringify({announcements: true}));
    const {user, fetch, queryClient} = renderUsers({}, createdUser);

    await user.click(await screen.findByRole('button', {name: 'Add User'}));
    expect(within(openDialog()!).getByText('A password is generated for the account and shown to you once.')).toBeInTheDocument();
    await user.type(within(openDialog()!).getByLabelText(/username/i), 'new.user');
    await user.click(within(openDialog()!).getByRole('button', {name: 'Create User'}));

    const dialog = within(openDialog()!);
    expect(await dialog.findByText(FIRST_PASSWORD)).toBeInTheDocument();
    expect(dialog.getByText('Account created for new.user.')).toBeInTheDocument();
    expect(dialog.getByText(/shown only this once, and works until new\.user changes it/)).toBeInTheDocument();
    await waitFor(() => expect(dialog.getByRole('button', {name: 'Copy password'})).toHaveFocus());
    expect(await bodies(fetch, 'POST')).toEqual([{username: 'new.user'}]);
    await afterTheHold();
    expect(screen.getByTestId('announcer')).toHaveTextContent(/Account created for new\.user\. Its password is shown once/);
    expect(screen.getByTestId('announcer')).not.toHaveTextContent(FIRST_PASSWORD);

    await user.click(dialog.getByRole('button', {name: 'Done'}));
    expect(openDialog()).toBeNull();
    await waitFor(() => expect(passwordKept(queryClient, FIRST_PASSWORD)).toBe(false));

    // Opening it again starts afresh.
    await user.click(screen.getByRole('button', {name: 'Add User'}));
    expect(within(openDialog()!).getByLabelText(/username/i)).toHaveValue('');
  });

  it('copies the new password', async () => {
    const {user} = renderUsers({}, createdUser);

    await createNewUser(user);
    await user.click(within(openDialog()!).getByRole('button', {name: 'Copy password'}));

    expect(await within(openDialog()!).findByRole('button', {name: 'Copied'})).toBeInTheDocument();
    expect(await navigator.clipboard.readText()).toBe(FIRST_PASSWORD);
  });

  it("doesn't let the second click of a double-click on Create User close the dialog", async () => {
    vi.useFakeTimers({shouldAdvanceTime: true});
    const {user} = renderUsers({}, createdUser);

    await createNewUser(user);
    // Done now sits where Create User was: a click on it straight away does nothing.
    await user.click(within(openDialog()!).getByRole('button', {name: 'Done'}));
    expect(within(openDialog()!).getByText(FIRST_PASSWORD)).toBeInTheDocument();

    await afterTheHold();
    await user.click(within(openDialog()!).getByRole('button', {name: 'Done'}));
    expect(openDialog()).toBeNull();
  });

  it.each([
    ['Escape', (user: User) => user.keyboard('{Escape}')],
    ['the close button', (user: User) => user.click(within(openDialog()!).getByRole('button', {name: /close/i}))],
    ['Cancel', (user: User) => user.click(within(openDialog()!).getByRole('button', {name: 'Cancel'}))],
  ])('forgets the new password when the dialog is closed by %s', async (_, dismiss) => {
    const {user, queryClient} = renderUsers({}, createdUser);

    await createNewUser(user);
    await dismiss(user);

    expect(openDialog()).toBeNull();
    await waitFor(() => expect(passwordKept(queryClient, FIRST_PASSWORD)).toBe(false));
  });

  it('says a new user could not be created when the API sends back no password', async () => {
    const {user} = renderUsers({}, {'POST /api/auth/users': () => json({username: 'new.user'})});

    await user.click(await screen.findByRole('button', {name: 'Add User'}));
    await user.type(within(openDialog()!).getByLabelText(/username/i), 'new.user');
    await user.click(within(openDialog()!).getByRole('button', {name: 'Create User'}));

    expect((await within(openDialog()!).findAllByText('The user could not be created. Please try again.')).length).toBeGreaterThan(0);
  });

  it("sets a user's teams, their own admin's too, and says it's done", async () => {
    const {user, fetch} = renderUsers(
      {},
      {
        'GET /api/teams': () => json([{id: 's1', name: 'Dermatology', isArchived: false}]),
        [`GET /api/auth/users/${adminUser.username}/teams`]: () => json([]),
        [`PUT /api/auth/users/${adminUser.username}/teams`]: () => json([{id: 's1', name: 'Dermatology', isArchived: false}]),
      },
    );

    await user.click(within(await rowOf(adminUser.username)).getByRole('button', {name: 'Edit teams'}));
    await user.click(await within(openDialog()!).findByRole('checkbox', {name: 'Dermatology'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Save Teams'}));

    await waitFor(() => expect(openDialog()).toBeNull());
    expect(await bodies(fetch, 'PUT')).toEqual([{teamIds: ['s1']}]);
    expect(await screen.findByText(`${adminUser.username}'s teams have been saved.`)).toBeInTheDocument();
  });

  it("shows each user's full name and job title, and edits them, starting from theirs", async () => {
    const {user, fetch} = renderUsers({}, {'PUT /api/auth/users/l.northcott/details': () => json({fullName: 'Dr Lee Test', jobTitle: 'Consultant'})});

    const row = await rowOf('l.northcott');
    expect(within(row).getByText('Dr Lee Test, Consultant dermatologist')).toBeInTheDocument();
    await user.click(within(row).getByRole('button', {name: 'Edit details'}));
    const dialog = within(openDialog()!);
    expect(dialog.getByRole('textbox', {name: /^Full name/})).toHaveValue('Dr Lee Test');
    const title = dialog.getByRole('textbox', {name: /^Job title/});
    await user.clear(title);
    await user.type(title, 'Consultant');
    await user.click(dialog.getByRole('button', {name: 'Save Details'}));

    await waitFor(() => expect(openDialog()).toBeNull());
    expect(await bodies(fetch, 'PUT')).toEqual([{fullName: 'Dr Lee Test', jobTitle: 'Consultant'}]);
    expect(await screen.findByText("l.northcott's details have been saved.")).toBeInTheDocument();
  });

  it('puts a full name and job title on one line', () => {
    expect(staffDetailsLine({fullName: 'Dr Lee Test', jobTitle: null})).toBe('Dr Lee Test');
    expect(staffDetailsLine({fullName: ' ', jobTitle: 'Nurse'})).toBe('Nurse');
    expect(staffDetailsLine({})).toBeUndefined();
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

  it('offers the roles that are not archived, with what each is for, keeping an archived one the user holds', async () => {
    const {user} = renderUsers(
      {},
      {
        'GET /api/roles': () =>
          json([
            roleResponse('Admin'),
            roleResponse('Nurse', {description: 'Nurses who work on pathways.'}),
            roleResponse('User', {isArchived: true}),
            roleResponse('Lab', {isArchived: true}),
          ]),
      },
    );

    await user.click(within(await rowOf('l.northcott')).getByRole('button', {name: 'Edit roles'}));
    const dialog = within(openDialog()!);
    expect(dialog.getByRole('checkbox', {name: 'Nurse'})).not.toBeChecked();
    expect(dialog.getByText('Nurses who work on pathways.')).toBeInTheDocument();
    // They hold User, archived since: it stays, to keep or take away, but Lab, archived too, isn't offered.
    expect(dialog.getByRole('checkbox', {name: 'User (archived)'})).toBeChecked();
    expect(dialog.queryByRole('checkbox', {name: /Lab/})).toBeNull();
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

    await openSearch(user);
    await waitFor(() => expect(searchBar()).toHaveTextContent(/Role: is/));
    await user.click(within(searchBar()).getByRole('button', {name: /clear/i}));

    expect(onSearchChange).toHaveBeenLastCalledWith(expect.objectContaining({asc: false, role: undefined, page: undefined}));
  });

  it('resets a password and shows the new one, then forgets it', async () => {
    vi.useFakeTimers({shouldAdvanceTime: true});
    const {user, queryClient} = renderUsers({}, {'PUT /api/auth/users/*': () => new Response('Synth3tic', {headers: {'content-type': 'text/plain'}})});

    await user.click(within(await rowOf('a.hartwell')).getByRole('button', {name: 'Reset password'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Reset Password'}));

    expect(await within(openDialog()!).findByText('Synth3tic')).toBeInTheDocument();
    expect(within(openDialog()!).getByText(/works until a\.hartwell changes it/)).toBeInTheDocument();
    await waitFor(() => expect(within(openDialog()!).getByRole('button', {name: 'Copy password'})).toHaveFocus());
    // Done takes Reset Password's place, and ignores a click straight away.
    await user.click(within(openDialog()!).getByRole('button', {name: 'Done'}));
    expect(openDialog()).not.toBeNull();
    await afterTheHold();
    await user.click(within(openDialog()!).getByRole('button', {name: 'Done'}));
    expect(openDialog()).toBeNull();
    await waitFor(() => expect(passwordKept(queryClient, 'Synth3tic')).toBe(false));
  });

  it('says when a reset gives back no new password', async () => {
    const {user} = renderUsers({}, {'PUT /api/auth/users/*': () => new Response('', {headers: {'content-type': 'text/plain'}})});

    await user.click(within(await rowOf('a.hartwell')).getByRole('button', {name: 'Reset password'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Reset Password'}));

    expect(await findAlert('The password could not be reset.')).toBeInTheDocument();
    expect(openDialog()).toBeNull();
  });

  it('signs a user out everywhere once the admin confirms, and says so', async () => {
    const {user, fetch} = renderUsers({}, {'POST /api/auth/users/*': () => new Response(null, {status: 204})});

    await user.click(within(await rowOf('a.hartwell')).getByRole('button', {name: 'Sign out everywhere'}));
    const dialog = within(openDialog()!);
    expect(dialog.getByText(/Sign a\.hartwell out everywhere\?/)).toBeInTheDocument();
    expect(calls(fetch)).not.toContain('POST /api/auth/users/a.hartwell/sign-out-everywhere');
    await user.click(dialog.getByRole('button', {name: 'Sign Out Everywhere'}));

    await waitFor(() => expect(calls(fetch)).toContain('POST /api/auth/users/a.hartwell/sign-out-everywhere'));
    expect(await screen.findByText('a.hartwell has been signed out everywhere.')).toBeInTheDocument();
    expect(openDialog()).toBeNull();
  });

  it('signs no one out when the admin cancels', async () => {
    const {user, fetch} = renderUsers({}, {'POST /api/auth/users/*': () => new Response(null, {status: 204})});

    await user.click(within(await rowOf('a.hartwell')).getByRole('button', {name: 'Sign out everywhere'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Cancel'}));

    expect(openDialog()).toBeNull();
    expect(calls(fetch).some((call) => call.includes('sign-out-everywhere'))).toBe(false);
  });

  it('says when a user could not be signed out everywhere', async () => {
    const {user} = renderUsers({}, {'POST /api/auth/users/*': () => json({title: 'Not Found', status: 404}, 404)});

    await user.click(within(await rowOf('a.hartwell')).getByRole('button', {name: 'Sign out everywhere'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Sign Out Everywhere'}));

    expect(await findAlert('a.hartwell could not be signed out.')).toBeInTheDocument();
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

  it('says when a user has no roles, and counts one user', async () => {
    renderUsers({}, {'GET /api/auth/users/search': () => json(pageOf([{userId: 'u4', username: 'n.oroles', roles: []}]))});

    expect(within(await rowOf('n.oroles')).getByText('No roles')).toBeInTheDocument();
    expect(screen.getByText('1 user in the system')).toBeInTheDocument();
  });

  it("shows the API's reasons on the username when a new user is refused", async () => {
    const {user} = renderUsers({}, {'POST /api/auth/users': () => json([{code: 'DuplicateUserName', description: "Username 'new.user' is already taken."}], 400)});

    await user.click(await screen.findByRole('button', {name: 'Add User'}));
    await user.type(within(openDialog()!).getByLabelText(/username/i), 'new.user');
    await user.click(within(openDialog()!).getByRole('button', {name: 'Create User'}));

    expect((await within(openDialog()!).findAllByText("Username 'new.user' is already taken.")).length).toBeGreaterThan(0);
  });

  it('says when a new user could not be created for another reason', async () => {
    const {user} = renderUsers({}, {'POST /api/auth/users': () => json({title: 'Server error'}, 500)});

    await user.click(await screen.findByRole('button', {name: 'Add User'}));
    await user.type(within(openDialog()!).getByLabelText(/username/i), 'new.user');
    await user.click(within(openDialog()!).getByRole('button', {name: 'Create User'}));

    expect((await within(openDialog()!).findAllByText('The user could not be created. Please try again.')).length).toBeGreaterThan(0);
  });

  it('says when the users could not be loaded', async () => {
    renderUsers({}, {'GET /api/auth/users/search': () => json({title: 'Server error'}, 500)});

    expect(await findAlert('The users could not be loaded. Please try again.')).toBeInTheDocument();
  });

  it('invites adding the first user when there are none', async () => {
    const {user} = renderUsers({}, {'GET /api/auth/users/search': () => json(pageOf([], 0))});

    await user.click(await screen.findByRole('button', {name: 'Add Your First User'}));

    expect(within(openDialog()!).getByLabelText(/username/i)).toBeInTheDocument();
  });

  it('offers to clear a search that matches nobody, keeping the order', async () => {
    const {user, onSearchChange} = renderUsers({username: 'nobody', asc: false}, {'GET /api/auth/users/search': () => json(pageOf([], 0))});

    await user.click(await screen.findByRole('button', {name: 'Clear Search'}));

    expect(onSearchChange).toHaveBeenLastCalledWith({asc: false});
  });

  it('sorts by username from its header, back to page 1', async () => {
    const {user, onSearchChange} = renderUsers({page: 2, role: 'Admin'});

    await rowOf('a.hartwell');
    await user.click(screen.getByRole('button', {name: /^user/i}));

    expect(onSearchChange).toHaveBeenLastCalledWith({role: 'Admin', page: undefined, sort: 'User', asc: false});
  });

  it('pages through the users, keeping the first page out of the URL', async () => {
    const {user, onSearchChange} = renderUsers({page: 2}, {'GET /api/auth/users/search': () => json(pageOf(users, 25, 2))});

    await rowOf('a.hartwell');
    await user.click(screen.getByRole('button', {name: 'Go to page 1'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({page: undefined});
    await user.click(screen.getByRole('button', {name: 'Go to page 3'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({page: 3});
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
