import {screen, waitFor} from '@testing-library/react';
import {whoamiQuery} from '../../auth/auth';
import {calls, json, mockApi, mustSetUpTwoFactorUser, renderWithApp} from '../../test/render';
import {authenticatorSetup, recoveryCodes} from '../../test/twoFactor';
import {EMAIL_CODES_NOT_ENOUGH, RequiredTwoFactorSetupPage} from './RequiredTwoFactorSetupPage';

function renderPage(extra: Parameters<typeof mockApi>[0] = {}) {
  const fetch = mockApi({
    'POST /api/auth/me/two-factor/authenticator': () => json(authenticatorSetup),
    'POST /api/auth/me/two-factor/enable': () => json({recoveryCodes}),
    ...extra,
  });
  return {...renderWithApp(<RequiredTwoFactorSetupPage />, {path: '/set-up-two-factor', user: mustSetUpTwoFactorUser}), fetch};
}

describe('RequiredTwoFactorSetupPage', () => {
  it('says plainly that admins must set it up before continuing', async () => {
    renderPage();

    expect(await screen.findByRole('heading', {name: 'Set up two-factor authentication', level: 1})).toBeInTheDocument();
    expect(screen.getByText(/signed in as test-held-admin\. Admin accounts must use two-factor authentication here/)).toBeInTheDocument();
    expect(await screen.findByRole('img', {name: 'QR code for your authenticator app'})).toBeInTheDocument();
    expect(document.title).toBe('Set up two-factor authentication - MedApp');
  });

  it('sets it up, shows the recovery codes, then forgets who they were and opens the start page', async () => {
    localStorage.setItem('medapp.startPage', 'procedures');
    const {user, router, queryClient} = renderPage({
      'GET /api/auth/whoami': () => json({isAuthenticated: true, name: mustSetUpTwoFactorUser.username, roles: ['Admin', 'User']}),
    });

    await user.type(await screen.findByLabelText(/your password/i), 'Synthetic-Passw0rd');
    await user.type(screen.getByLabelText(/code from your authenticator app/i), '123456');
    await user.click(screen.getByRole('button', {name: 'Turn On Two-Factor'}));
    expect(await screen.findByText(recoveryCodes[0])).toBeInTheDocument();
    // Signing out now would lose the codes before they're saved: Continue is the way on.
    expect(screen.queryByRole('button', {name: 'Sign Out'})).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Continue'}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/pathways'));
    // Who they are is asked again (going in reloads the account's settings): the cached answer that said they must set
    // it up is gone, so the layout's guard lets them in.
    expect(queryClient.getQueryData(whoamiQuery.queryKey)).toMatchObject({isAuthenticated: true, mustSetUpTwoFactor: false});
  });

  it('says why an admin who uses codes by email must set up an app too', async () => {
    renderPage({
      'GET /api/auth/me/two-factor': () =>
        json({enabled: true, method: 'Email', recoveryCodesLeft: 10, required: true, canUseEmailCodes: true}),
    });

    expect(await screen.findByText(EMAIL_CODES_NOT_ENOUGH)).toBeInTheDocument();
    expect(await screen.findByRole('img', {name: 'QR code for your authenticator app'})).toBeInTheDocument();
    // Switching proves the current factor too: an emailed code, or a recovery code.
    expect(await screen.findByLabelText(/code from the email, or a recovery code/i)).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Email Me a Code'})).toBeInTheDocument();
  });

  it("doesn't mention codes by email to an admin without two-factor", async () => {
    renderPage({'GET /api/auth/me/two-factor': () => json({enabled: false, method: 'None', recoveryCodesLeft: 0, required: true})});

    expect(await screen.findByRole('img', {name: 'QR code for your authenticator app'})).toBeInTheDocument();
    expect(screen.queryByText(EMAIL_CODES_NOT_ENOUGH)).not.toBeInTheDocument();
  });

  it('signs out', async () => {
    const {user, router, fetch} = renderPage({'POST /api/auth/logout': () => new Response(null, {status: 204})});

    await user.click(await screen.findByRole('button', {name: 'Sign Out'}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/login'));
    expect(calls(fetch)).toContain('POST /api/auth/logout');
  });
});
