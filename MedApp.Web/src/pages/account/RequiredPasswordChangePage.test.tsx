import {screen, waitFor} from '@testing-library/react';
import {calls, findAlert, json, mockApi, mustChangePasswordUser, renderWithApp} from '../../test/render';
import {NO_ROLE, RequiredPasswordChangePage} from './RequiredPasswordChangePage';

const OWN_PASSWORD = `/api/auth/users/${mustChangePasswordUser.username}/update-password`;

async function fill(user: ReturnType<typeof renderWithApp>['user'], current: string, next: string, confirm: string) {
  await user.type(await screen.findByLabelText(/^password you were given/i), current);
  await user.type(screen.getByLabelText(/^new password/i), next);
  await user.type(screen.getByLabelText(/^confirm new password/i), confirm);
  await user.click(screen.getByRole('button', {name: 'Change password'}));
}

const render = () => renderWithApp(<RequiredPasswordChangePage />, {path: '/change-password', user: mustChangePasswordUser});

describe('RequiredPasswordChangePage', () => {
  it('says plainly that they must choose their own password before continuing', async () => {
    mockApi({});
    render();

    expect(await screen.findByRole('heading', {name: 'Choose your password', level: 1})).toBeInTheDocument();
    expect(screen.getByText(/signed in as test-new-user with a password you were given/i)).toBeInTheDocument();
    expect(screen.getByText(/before you continue, choose your own password/i)).toBeInTheDocument();
    expect(document.title).toBe('Choose your password - MedApp');
  });

  it('changes their own password, then asks who they are again and opens the start page', async () => {
    const fetch = mockApi({
      [`PUT ${OWN_PASSWORD}`]: () => new Response(null, {status: 204}),
      'GET /api/auth/whoami': () => json({isAuthenticated: true, name: mustChangePasswordUser.username, roles: ['User'], mustChangePassword: false}),
    });
    const {user, router} = render();

    await fill(user, 'Given-Synthetic1', 'Chosen-Synthetic1', 'Chosen-Synthetic1');

    await waitFor(() => expect(router.state.location.pathname).toBe('/'));
    expect(await (fetch.mock.calls[0][0] as Request).json()).toEqual({oldPassword: 'Given-Synthetic1', newPassword: 'Chosen-Synthetic1'});
    // The cached whoami (which said they must change it) is gone, so it's asked again; then the account's settings
    // (signing out cleared them from this browser) are loaded before the start page opens.
    await waitFor(() => expect(calls(fetch)).toEqual([`PUT ${OWN_PASSWORD}`, 'GET /api/auth/whoami', 'GET /api/auth/me/settings']));
  });

  it('opens the start page chosen in Account Settings', async () => {
    localStorage.setItem('medapp.startPage', 'procedures');
    mockApi({
      [`PUT ${OWN_PASSWORD}`]: () => new Response(null, {status: 204}),
      'GET /api/auth/whoami': () => json({isAuthenticated: true, name: mustChangePasswordUser.username, roles: ['User']}),
    });
    const {user, router} = render();

    await fill(user, 'Given-Synthetic1', 'Chosen-Synthetic1', 'Chosen-Synthetic1');

    await waitFor(() => expect(router.state.location.pathname).toBe('/pathways'));
  });

  it("shows the server's reasons on the fields, and stays", async () => {
    mockApi({
      [`PUT ${OWN_PASSWORD}`]: () =>
        json(
          {
            title: 'One or more validation errors occurred.',
            status: 400,
            errors: {NewPassword: ['The new password must be different from the current one.']},
          },
          400,
        ),
    });
    const {user, router} = render();

    await fill(user, 'Given-Synthetic1', 'Given-Synthetic1', 'Given-Synthetic1');

    expect(await screen.findByText('The new password must be different from the current one.')).toBeInTheDocument();
    expect(screen.getByLabelText(/^new password/i)).toHaveAttribute('aria-invalid', 'true');
    expect(router.state.location.pathname).toBe('/change-password');
  });

  it("lists Identity's reasons, which belong to no field", async () => {
    mockApi({
      [`PUT ${OWN_PASSWORD}`]: () => json([{code: 'PasswordMismatch', description: 'Incorrect password.'}], 400),
    });
    const {user} = render();

    await fill(user, 'Wrong-Synthetic1', 'Chosen-Synthetic1', 'Chosen-Synthetic1');

    await findAlert('Incorrect password.');
  });

  it('checks the new passwords match before asking the API', async () => {
    const fetch = mockApi({});
    const {user} = render();

    await fill(user, 'Given-Synthetic1', 'Chosen-Synthetic1', 'Different-Synthetic1');

    await findAlert('New passwords do not match.');
    expect(fetch).not.toHaveBeenCalled();
  });

  it('explains a refusal in words', async () => {
    mockApi({[`PUT ${OWN_PASSWORD}`]: () => json({}, 401)});
    const {user} = render();

    await fill(user, 'Wrong-Synthetic1', 'Chosen-Synthetic1', 'Chosen-Synthetic1');

    await findAlert('Current password is incorrect.');
  });

  it('says an account with no role needs one from an admin, when the API refuses the change by role', async () => {
    mockApi({[`PUT ${OWN_PASSWORD}`]: () => new Response(null, {status: 403})});
    const {user} = render();

    await fill(user, 'Given-Synthetic1', 'Chosen-Synthetic1', 'Chosen-Synthetic1');

    await findAlert(NO_ROLE);
    expect(NO_ROLE).toBe('This account has no role yet. Ask an admin to give it one, then sign in again.');
  });

  it('asks for the password they were given by name', async () => {
    mockApi({});
    render();

    expect(await screen.findByLabelText(/^password you were given/i)).toHaveAttribute('placeholder', 'Enter the password you were given');
  });

  it('signs out', async () => {
    const fetch = mockApi({'POST /api/auth/logout': () => new Response(null, {status: 204})});
    const {user, router} = render();

    await user.click(await screen.findByRole('button', {name: 'Sign out'}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/login'));
    expect(calls(fetch)[0]).toBe('POST /api/auth/logout');
  });
});
