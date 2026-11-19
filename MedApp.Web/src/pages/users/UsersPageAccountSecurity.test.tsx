import {screen, waitFor, within} from '@testing-library/react';
import {adminUser, calls, findAlert, isDisabled, json, mockApi, openDialog, pageOf, renderWithApp} from '../../test/render';
import type {UsersListSearch} from '../listSearch';
import {roleResponse} from '../../test/roles';
import {UsersPage} from './UsersPage';

/** The Users page's email addresses and two-factor authentication: shown in the list, set on create, and reset. */

const users = [
  {userId: 'u1', username: 'a.hartwell', roles: ['Admin', 'User'], email: 'a.hartwell@example.org', twoFactorEnabled: true},
  {userId: 'u2', username: 'l.northcott', roles: ['User'], pendingEmail: 'l.northcott@example.org', twoFactorEnabled: false},
  {
    userId: 'u3',
    username: 'm.ferrand',
    roles: ['User'],
    email: 'm.ferrand@example.org',
    pendingEmail: 'm.ferrand.new@example.org',
    twoFactorEnabled: true,
  },
  {userId: 'u4', username: adminUser.username, roles: ['Admin', 'User'], twoFactorEnabled: true},
];

function renderUsers({canSendEmail = true, extra = {}}: {canSendEmail?: boolean; extra?: Parameters<typeof mockApi>[0]} = {}) {
  const fetch = mockApi({
    'GET /api/auth/users/search': () => json(pageOf(users)),
    'GET /api/roles': () => json([roleResponse('Admin'), roleResponse('User')]),
    'GET /api/auth/features': () => json({canSendEmail}),
    ...extra,
  });
  const search: UsersListSearch = {};
  const result = renderWithApp(<UsersPage search={search} onSearchChange={vi.fn()} />, {user: adminUser});
  return {...result, fetch};
}

const rowOf = async (username: string) => (await screen.findByText(username)).closest('tr')!;
const postBodies = (fetch: ReturnType<typeof mockApi>) =>
  Promise.all(fetch.mock.calls.map(([r]) => r as Request).filter((r) => r.method === 'POST').map((r) => r.clone().json()));

type User = ReturnType<typeof renderUsers>['user'];

async function openCreate(user: User) {
  await user.click(await screen.findByRole('button', {name: 'Add User'}));
  return within(openDialog()!);
}

describe('UsersPage email addresses and two-factor', () => {
  it('shows each email address, marking one still to be confirmed, and whether two-factor is on', async () => {
    renderUsers();

    const confirmed = await rowOf('a.hartwell');
    expect(within(confirmed).getByText('a.hartwell@example.org')).toBeInTheDocument();
    expect(within(confirmed).getByText('On')).toBeInTheDocument();
    const pending = await rowOf('l.northcott');
    expect(within(pending).getByText('l.northcott@example.org')).toBeInTheDocument();
    expect(within(pending).getByText('Unconfirmed')).toBeInTheDocument();
    expect(within(pending).getByText('Off')).toBeInTheDocument();
    const changing = await rowOf('m.ferrand');
    expect(within(changing).getByText('m.ferrand@example.org')).toBeInTheDocument();
    expect(within(changing).getByText('Changing to m.ferrand.new@example.org (unconfirmed)')).toBeInTheDocument();
    expect(within(await rowOf(adminUser.username)).getByText('None')).toBeInTheDocument();
    expect(screen.getByRole('columnheader', {name: 'Email'})).toBeInTheDocument();
    expect(screen.getByRole('columnheader', {name: 'Two-factor'})).toBeInTheDocument();
  });

  it('offers to reset two-factor only for someone else who has it on', async () => {
    renderUsers();

    expect(isDisabled(within(await rowOf('a.hartwell')).getByRole('button', {name: 'Reset two-factor'}))).toBe(false);
    expect(isDisabled(within(await rowOf('l.northcott')).getByRole('button', {name: 'Reset two-factor'}))).toBe(true);
    expect(isDisabled(within(await rowOf(adminUser.username)).getByRole('button', {name: 'Reset two-factor'}))).toBe(true);
  });

  it('resets two-factor once the admin confirms what it does, and says so', async () => {
    const {user, fetch} = renderUsers({extra: {'POST /api/auth/users/*': () => new Response(null, {status: 204})}});

    await user.click(within(await rowOf('a.hartwell')).getByRole('button', {name: 'Reset two-factor'}));
    const dialog = within(openDialog()!);
    expect(dialog.getByRole('heading', {name: 'Reset Two-Factor'})).toBeInTheDocument();
    expect(dialog.getByText(/turns off their two-factor authentication .* and signs them out everywhere/)).toBeInTheDocument();
    expect(calls(fetch)).not.toContain('POST /api/auth/users/a.hartwell/reset-two-factor');
    await user.click(dialog.getByRole('button', {name: 'Reset Two-Factor'}));

    await waitFor(() => expect(calls(fetch)).toContain('POST /api/auth/users/a.hartwell/reset-two-factor'));
    expect(await screen.findByText("Two-factor authentication has been reset for a.hartwell, and they've been signed out everywhere.")).toBeInTheDocument();
    expect(openDialog()).toBeNull();
  });

  it("says why a reset was refused, in the API's words", async () => {
    const {user} = renderUsers({
      extra: {'POST /api/auth/users/*': () => json([{code: 'OwnTwoFactor', description: "You can't reset your own two-factor authentication."}], 400)},
    });

    await user.click(within(await rowOf('a.hartwell')).getByRole('button', {name: 'Reset two-factor'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Reset Two-Factor'}));

    expect(await findAlert("You can't reset your own two-factor authentication.")).toBeInTheDocument();
  });

  it('says when a reset failed for another reason', async () => {
    const {user} = renderUsers({extra: {'POST /api/auth/users/*': () => json({title: 'Server error'}, 500)}});

    await user.click(within(await rowOf('a.hartwell')).getByRole('button', {name: 'Reset two-factor'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Reset Two-Factor'}));

    expect(await findAlert('Two-factor authentication could not be reset for a.hartwell.')).toBeInTheDocument();
  });

  it("offers to remove an email address only for someone else who has one, confirmed or waiting", async () => {
    renderUsers();

    const action = async (username: string) => within(await rowOf(username)).getByRole('button', {name: 'Remove email address'});
    expect(isDisabled(await action('a.hartwell'))).toBe(false);
    expect(isDisabled(await action('l.northcott'))).toBe(false);
    expect(isDisabled(await action('m.ferrand'))).toBe(false);
    expect(isDisabled(await action(adminUser.username))).toBe(true);
  });

  it('removes an email address once the admin confirms what it does, says so, and refreshes the list', async () => {
    const {user, fetch} = renderUsers({extra: {'DELETE /api/auth/users/*': () => new Response(null, {status: 204})}});

    await user.click(within(await rowOf('a.hartwell')).getByRole('button', {name: 'Remove email address'}));
    const dialog = within(openDialog()!);
    expect(dialog.getByRole('heading', {name: 'Remove Email Address'})).toBeInTheDocument();
    expect(dialog.getByText(/Remove a\.hartwell's email address \(a\.hartwell@example\.org\), and any waiting to be confirmed\?/)).toBeInTheDocument();
    expect(dialog.getByText(/Password reset links can't be sent to it any more/)).toBeInTheDocument();
    expect(dialog.getByText(/If the address was confirmed, we email it to say it was removed/)).toBeInTheDocument();
    expect(calls(fetch)).not.toContain('DELETE /api/auth/users/a.hartwell/email');
    await user.click(dialog.getByRole('button', {name: 'Remove Email Address'}));

    await waitFor(() => expect(calls(fetch)).toContain('DELETE /api/auth/users/a.hartwell/email'));
    expect(await screen.findByText("a.hartwell's email address has been removed.")).toBeInTheDocument();
    expect(openDialog()).toBeNull();
    await waitFor(() => expect(calls(fetch).filter((c) => c.startsWith('GET /api/auth/users/search')).length).toBe(2));
  });

  it('names the address waiting to be confirmed when there is no confirmed one', async () => {
    const {user} = renderUsers();

    await user.click(within(await rowOf('l.northcott')).getByRole('button', {name: 'Remove email address'}));

    expect(within(openDialog()!).getByText(/Remove l\.northcott's email address \(l\.northcott@example\.org\)/)).toBeInTheDocument();
  });

  it.each([
    ['in its words for an unknown user', () => json({title: 'User not found.', status: 404}, 404), 'User not found.'],
    ['for another reason', () => json({title: 'Server error'}, 500), 'The email address could not be removed for a.hartwell.'],
  ])('says when an email address could not be removed, %s', async (_, respond, message) => {
    const {user} = renderUsers({extra: {'DELETE /api/auth/users/*': respond}});

    await user.click(within(await rowOf('a.hartwell')).getByRole('button', {name: 'Remove email address'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Remove Email Address'}));

    expect(await findAlert(message)).toBeInTheDocument();
    expect(openDialog()).toBeNull();
  });

  it('creates a user with an email address, saying the password is never emailed', async () => {
    const {user, fetch} = renderUsers({extra: {'POST /api/auth/users': () => json({username: 'new.user', password: 'Synth3ticFirst'})}});

    const dialog = await openCreate(user);
    expect(dialog.getByText('Enter a username for the new user, and their email address if you have it.')).toBeInTheDocument();
    expect(dialog.getByText("We'll email a link to confirm it. The password is never emailed.")).toBeInTheDocument();
    await user.type(dialog.getByLabelText(/username/i), 'new.user');
    await user.type(dialog.getByLabelText(/email address/i), ' new.user@example.org ');
    await user.click(dialog.getByRole('button', {name: 'Create User'}));

    expect(await within(openDialog()!).findByText('Synth3ticFirst')).toBeInTheDocument();
    expect(await postBodies(fetch)).toEqual([{username: 'new.user', email: 'new.user@example.org'}]);
  });

  it("doesn't ask for an email address when the server can't send email", async () => {
    const {user} = renderUsers({canSendEmail: false});

    const dialog = await openCreate(user);

    expect(dialog.getByText('Enter a username for the new user.')).toBeInTheDocument();
    expect(dialog.queryByLabelText(/email address/i)).not.toBeInTheDocument();
  });

  it("shows a refused email address on its field, and the username's own on the username", async () => {
    const {user} = renderUsers({
      extra: {
        'POST /api/auth/users': () =>
          json({title: 'One or more validation errors occurred.', errors: {Email: ['Email must be a valid email address.'], Username: ['Too short.']}}, 400),
      },
    });

    const dialog = await openCreate(user);
    await user.type(dialog.getByLabelText(/username/i), 'n');
    await user.type(dialog.getByLabelText(/email address/i), 'not-an-address');
    await user.click(dialog.getByRole('button', {name: 'Create User'}));

    await waitFor(() => expect(dialog.getByLabelText(/email address/i)).toHaveAttribute('aria-invalid', 'true'));
    expect(dialog.getAllByText('Email must be a valid email address.').length).toBeGreaterThan(0);
    expect(dialog.getAllByText('Too short.').length).toBeGreaterThan(0);
  });

  it('leaves the username alone when only the email address was refused', async () => {
    const {user} = renderUsers({
      extra: {'POST /api/auth/users': () => json({title: 'One or more validation errors occurred.', errors: {Email: ['Email is not valid.']}}, 400)},
    });

    const dialog = await openCreate(user);
    await user.type(dialog.getByLabelText(/username/i), 'new.user');
    await user.type(dialog.getByLabelText(/email address/i), 'x');
    await user.click(dialog.getByRole('button', {name: 'Create User'}));

    await waitFor(() => expect(dialog.getByLabelText(/email address/i)).toHaveAttribute('aria-invalid', 'true'));
    expect(dialog.getByLabelText(/username/i)).not.toHaveAttribute('aria-invalid', 'true');
  });

  it("shows a 400 that names no field on the username, in the API's title", async () => {
    const {user} = renderUsers({extra: {'POST /api/auth/users': () => json({title: 'The request was refused.'}, 400)}});

    const dialog = await openCreate(user);
    await user.type(dialog.getByLabelText(/username/i), 'new.user');
    await user.click(dialog.getByRole('button', {name: 'Create User'}));

    expect((await dialog.findAllByText('The request was refused.')).length).toBeGreaterThan(0);
  });
});
