import {act, waitFor, within} from '@testing-library/react';
import {SESSION_RETRY_MS} from '../../api/session';
import {calls, json, mockApi, openDialog, renderWithApp, staffUser} from '../../test/render';
import {SessionTimeout, timeLeftText, WARNING_MS} from './SessionTimeout';

const SESSION = 'GET /api/auth/session';
const EXTEND = 'POST /api/auth/session/extend';
const MINUTE = 60_000;
const session = (expiresInSeconds: number, canExtend = true) =>
  json({expiresAtUtc: '2026-11-16T17:00:00Z', expiresInSeconds, canExtend});
const unauthorized = () => new Response(null, {status: 401});
const unavailable = () => json({title: 'Unavailable'}, 503);

const renderTimeout = () => renderWithApp(<SessionTimeout />, {user: staffUser, path: '/patients'});
const warning = () => waitFor(() => within(openDialog()!).getByRole('heading', {name: 'Your session is ending'}));
/** Moves the clock (and every timer) on, letting what it sets off settle. */
const wait = (ms: number) => act(() => vi.advanceTimersByTimeAsync(ms));
const sessionChecks = (fetch: ReturnType<typeof mockApi>) => calls(fetch).filter((c) => c === SESSION).length;

describe('SessionTimeout', () => {
  // Fake timers, with the clock still moving on by itself, so user-event and React Query keep working.
  beforeEach(() => vi.useFakeTimers({shouldAdvanceTime: true}));
  afterEach(() => vi.useRealTimers());

  it('says nothing until five minutes before the end, then counts down, and stays signed in for another session length', async () => {
    let extended = false;
    // The server counts down to a fixed end.
    const end = Date.now() + 6 * MINUTE;
    const fetch = mockApi({
      [SESSION]: () => session(extended ? 8 * 60 * 60 : Math.round((end - Date.now()) / 1000)),
      [EXTEND]: () => {
        extended = true;
        return session(8 * 60 * 60);
      },
    });
    const {user} = renderTimeout();
    await waitFor(() => expect(sessionChecks(fetch)).toBe(1));

    await wait(6 * MINUTE - WARNING_MS - 5_000);
    expect(openDialog()).toBeNull();

    await wait(6_000);
    await warning();
    expect(openDialog()).toHaveTextContent(/signed out in 4 minutes 5\d seconds/);
    await wait(30_000);
    expect(openDialog()).toHaveTextContent(/signed out in 4 minutes 2\d seconds/);

    await user.click(within(openDialog()!).getByRole('button', {name: 'Stay signed in'}));
    await waitFor(() => expect(openDialog()).toBeNull());
    expect(calls(fetch)).toContain(EXTEND);
  });

  it('checks again as the warning is due, and stays quiet when the session was extended elsewhere', async () => {
    let answers = 0;
    const fetch = mockApi({[SESSION]: () => session(answers++ === 0 ? 10 * 60 : 8 * 60 * 60)});
    renderTimeout();
    await waitFor(() => expect(sessionChecks(fetch)).toBe(1));

    await wait(5 * MINUTE + 1_000);

    expect(sessionChecks(fetch)).toBe(2);
    await wait(2_000);
    expect(openDialog()).toBeNull();
  });

  it('says when a session has reached its longest, and lets the warning be closed', async () => {
    mockApi({[SESSION]: () => session(3 * 60, false)});
    const {user} = renderTimeout();

    await warning();
    expect(openDialog()).toHaveTextContent(/signed in for 24 hours, the longest a session can last/);
    expect(within(openDialog()!).queryByRole('button', {name: 'Stay signed in'})).not.toBeInTheDocument();
    await user.click(within(openDialog()!).getByRole('button', {name: 'OK'}));

    await waitFor(() => expect(openDialog()).toBeNull());
  });

  it('stays closed once closed, even when asked again, until the end really moves', async () => {
    let end = 4 * 60;
    const fetch = mockApi({[SESSION]: () => session(end)});
    const {user} = renderTimeout();

    await warning();
    await user.click(within(openDialog()!).getByRole('button', {name: /close/i}));
    await waitFor(() => expect(openDialog()).toBeNull());

    // Shown again a little later: the API is asked again, with the same end (counted from a new moment).
    await wait(1_500);
    end -= 1;
    act(() => document.dispatchEvent(new Event('visibilitychange')));
    await waitFor(() => expect(sessionChecks(fetch)).toBe(2));
    await wait(1_100);
    expect(openDialog()).toBeNull();

    // Extended to near its limit elsewhere: a new end, so it warns again.
    end = 3 * 60;
    act(() => document.dispatchEvent(new Event('visibilitychange')));
    await warning();
  });

  it('signs out from the warning', async () => {
    const fetch = mockApi({[SESSION]: () => session(60), 'POST /api/auth/logout': () => new Response(null, {status: 204})});
    const {user, router} = renderTimeout();

    await warning();
    await user.click(within(openDialog()!).getByRole('button', {name: 'Sign out'}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/login'));
    expect(calls(fetch)).toContain('POST /api/auth/logout');
  });

  it('says why when the session could not be extended', async () => {
    mockApi({[SESSION]: () => session(60), [EXTEND]: () => json({title: 'Server error'}, 500)});
    const {user} = renderTimeout();

    await warning();
    await user.click(within(openDialog()!).getByRole('button', {name: 'Stay signed in'}));

    expect(await within(openDialog()!).findByText('Your session could not be extended. Please try again.')).toBeInTheDocument();
  });

  it('goes to the login page, saying why, once the session has ended, clearing the storage as signing out does', async () => {
    localStorage.setItem('medapp.favourites', '{}');
    localStorage.setItem('medapp.colorMode', 'dark');
    let answers = 0;
    mockApi({[SESSION]: () => (answers++ === 0 ? session(30) : unauthorized())});
    const {router} = renderTimeout();

    await warning();
    expect(openDialog()).toHaveTextContent(/signed out in (30|29) seconds\./);
    await wait(32_000);

    await waitFor(() => expect(router.state.location.pathname).toBe('/login'));
    expect(router.state.location.search).toEqual({expired: true});
    await waitFor(() => expect(localStorage.getItem('medapp.favourites')).toBeNull());
    expect(localStorage.getItem('medapp.colorMode')).toBe('dark');
  });

  it('ends it too when the check at the end fails some other way, after trying again', async () => {
    let answers = 0;
    const fetch = mockApi({[SESSION]: () => (answers++ === 0 ? session(10) : unavailable())});
    const {router} = renderTimeout();
    await waitFor(() => expect(sessionChecks(fetch)).toBe(1));

    await wait(20_000);

    await waitFor(() => expect(router.state.location.pathname).toBe('/login'));
    // The check at the end, and its retries.
    expect(sessionChecks(fetch)).toBeGreaterThan(2);
  });

  it('keeps asking when the first check fails, then warns as usual', async () => {
    let answers = 0;
    const fetch = mockApi({[SESSION]: () => (answers++ < 4 ? unavailable() : session(4 * 60))});
    renderTimeout();

    // The first check and its three retries fail; it asks again a minute later.
    await wait(10_000);
    expect(sessionChecks(fetch)).toBe(4);
    expect(openDialog()).toBeNull();
    await wait(SESSION_RETRY_MS);

    expect(sessionChecks(fetch)).toBe(5);
    await warning();
  });

  it('goes straight to the login page when the session has already ended, without asking again', async () => {
    const fetch = mockApi({[SESSION]: unauthorized});
    const {router} = renderTimeout();

    await waitFor(() => expect(router.state.location.pathname).toBe('/login'));
    expect(sessionChecks(fetch)).toBe(1);
  });

  it('checks again when the tab is shown, as timers sleep in hidden tabs', async () => {
    const fetch = mockApi({[SESSION]: () => session(60 * 60)});
    renderTimeout();
    await waitFor(() => expect(sessionChecks(fetch)).toBe(1));

    act(() => {
      Object.defineProperty(document, 'visibilityState', {value: 'hidden', configurable: true});
      document.dispatchEvent(new Event('visibilitychange'));
    });
    expect(sessionChecks(fetch)).toBe(1);
    act(() => {
      Object.defineProperty(document, 'visibilityState', {value: 'visible', configurable: true});
      document.dispatchEvent(new Event('visibilitychange'));
    });

    await waitFor(() => expect(sessionChecks(fetch)).toBe(2));
  });
});

describe('timeLeftText', () => {
  it('says minutes and seconds as people do', () => {
    expect(timeLeftText(4 * 60_000 + 5_000)).toBe('4 minutes 5 seconds');
    expect(timeLeftText(60_000)).toBe('1 minute');
    expect(timeLeftText(61_000)).toBe('1 minute 1 second');
    expect(timeLeftText(45_000)).toBe('45 seconds');
    expect(timeLeftText(200)).toBe('1 second');
    expect(timeLeftText(-5_000)).toBe('0 seconds');
  });
});
