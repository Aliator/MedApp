import {screen, waitFor, within} from '@testing-library/react';
import {FULL_DEMO_VERSION, fullDemoReady, resetFullDemoForTests} from '../test/fullDemo';
import {isDisabled, json, mockFetch, renderWithApp, staffUser} from '../test/render';
import {FullDemoLoginPage} from './FullDemoLoginPage';
import {startAgain} from './FullDemoProgress';
import {setStartupStateForTests} from './startup';

const MB = 1024 * 1024;

describe('FullDemoLoginPage', () => {
  beforeEach(() => resetFullDemoForTests());
  afterEach(() => resetFullDemoForTests());

  it('lists the accounts at once, the demo four first, with sign-in disabled and the download’s progress', async () => {
    setStartupStateForTests({
      phase: 'downloading',
      progress: {backend: {loaded: 100, total: 242, bytes: 20 * MB}, database: {loaded: 0, total: 0}},
      version: FULL_DEMO_VERSION,
      size: {bytes: 40 * MB, compressedBytes: 16 * MB},
    });
    renderWithApp(<FullDemoLoginPage />, {path: '/login'});

    expect(await screen.findByText('Synthetic data, kept only in this browser')).toBeInTheDocument();
    expect(screen.getByRole('progressbar', {name: 'Getting MedApp ready: 8.0 of 16 MB'})).toBeInTheDocument();
    const buttons = screen.getAllByRole('button', {name: /^Sign in as /});
    expect(buttons.map((b) => b.textContent)).toEqual([
      'Sign in as the admin',
      'Sign in as Nadia Thornbury',
      'Sign in as Ravi Aldous',
      'Sign in as Saira Varma',
      'Sign in as Jo Whitlock',
    ]);
    expect(buttons.every(isDisabled)).toBe(true);
    expect(screen.getByText('Clinic admin (Dermatology). Clinician. Consultant dermatologist.')).toBeInTheDocument();
    expect(screen.getByRole('heading', {name: 'Other people at the centre'})).toBeInTheDocument();
    expect(isDisabled(screen.getByRole('button', {name: 'Reset the Demo'}))).toBe(true);
  });

  it('says it’s starting once everything has downloaded', async () => {
    setStartupStateForTests({phase: 'starting', progress: null, version: FULL_DEMO_VERSION, size: null});
    renderWithApp(<FullDemoLoginPage />, {path: '/login'});
    expect(await screen.findByRole('progressbar', {name: 'Starting…'})).toBeInTheDocument();
  });

  it('signs in through the API as the account chosen once the backend is ready', async () => {
    fullDemoReady();
    const fetch = mockFetch(async (request) => {
      if (request.url.endsWith('/api/auth/login')) return json({sessionId: 's'});
      if (request.url.includes('/api/auth/whoami')) return json({...staffUser, isAuthenticated: true, name: 'n.thornbury'});
      return json({});
    });
    const {user, router} = renderWithApp(<FullDemoLoginPage />, {path: '/login'});
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
    const nadia = await screen.findByRole('button', {name: 'Sign in as Nadia Thornbury'});
    expect(isDisabled(nadia)).toBe(false);
    await user.click(nadia);

    await waitFor(() => expect(router.state.location.pathname).toBe('/'));
    const login = fetch.mock.calls.map(([request]) => request as Request).find((r) => r.url.endsWith('/api/auth/login'))!;
    expect(await login.clone().json()).toEqual({username: 'n.thornbury', password: 'full-demo'});
  });

  it('says when MedApp couldn’t be downloaded, with Try Again', async () => {
    setStartupStateForTests({phase: 'failed', progress: null, version: null, size: null, error: 'offline'});
    const reload = vi.spyOn(startAgain, 'reload').mockImplementation(() => {});
    const {user} = renderWithApp(<FullDemoLoginPage />, {path: '/login'});
    const alert = await screen.findByRole('alert');
    expect(within(alert).getByText('MedApp couldn’t be downloaded')).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Try Again'}));
    expect(reload).toHaveBeenCalled();
    expect(isDisabled(screen.getByRole('button', {name: 'Reset the Demo'}))).toBe(false);
  });
});
