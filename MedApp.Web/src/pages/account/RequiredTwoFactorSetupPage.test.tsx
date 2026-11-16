import {screen, waitFor} from '@testing-library/react';
import {whoamiQuery} from '../../auth/auth';
import {calls, json, mockApi, mustSetUpTwoFactorUser, renderWithApp} from '../../test/render';
import {authenticatorSetup, recoveryCodes} from '../../test/twoFactor';
import {RequiredTwoFactorSetupPage} from './RequiredTwoFactorSetupPage';

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

    await user.type(await screen.findByLabelText(/code from your authenticator app/i), '123456');
    await user.click(screen.getByRole('button', {name: 'Turn on two-factor'}));
    expect(await screen.findByText(recoveryCodes[0])).toBeInTheDocument();
    // Signing out now would lose the codes before they're saved: Continue is the way on.
    expect(screen.queryByRole('button', {name: 'Sign out'})).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Continue'}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/procedures'));
    // The cached whoami (which said they must set it up) is gone, for the layout's guard to ask again.
    expect(queryClient.getQueryState(whoamiQuery.queryKey)).toBeUndefined();
  });

  it('signs out', async () => {
    const {user, router, fetch} = renderPage({'POST /api/auth/logout': () => new Response(null, {status: 204})});

    await user.click(await screen.findByRole('button', {name: 'Sign out'}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/login'));
    expect(calls(fetch)).toContain('POST /api/auth/logout');
  });
});
