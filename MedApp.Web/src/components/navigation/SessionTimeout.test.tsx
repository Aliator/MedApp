import {act, waitFor, within} from '@testing-library/react';
import {calls, json, mockApi, openDialog, renderWithApp, staffUser} from '../../test/render';
import {SessionTimeout, timeLeftText} from './SessionTimeout';

const SESSION = 'GET /api/auth/session';
const EXTEND = 'POST /api/auth/session/extend';
const session = (expiresInSeconds: number, canExtend = true) =>
  json({expiresAtUtc: '2026-11-16T17:00:00Z', expiresInSeconds, canExtend});
const unauthorized = () => new Response(null, {status: 401});

const renderTimeout = () => renderWithApp(<SessionTimeout />, {user: staffUser, path: '/patients'});
const warning = () => waitFor(() => within(openDialog()!).getByRole('heading', {name: 'Your session is ending'}));

describe('SessionTimeout', () => {
  it('warns five minutes before the end, counting down, and stays signed in for another session length', async () => {
    const fetch = mockApi({[SESSION]: () => session(4 * 60 + 30), [EXTEND]: () => session(8 * 60 * 60)});
    const {user} = renderTimeout();

    await warning();
    expect(openDialog()).toHaveTextContent(/signed out in 4 minutes (30|29|28) seconds/);
    await user.click(within(openDialog()!).getByRole('button', {name: 'Stay signed in'}));

    await waitFor(() => expect(openDialog()).toBeNull());
    expect(calls(fetch)).toContain(EXTEND);
  });

  it('says nothing while the end is further off than that', async () => {
    mockApi({[SESSION]: () => session(60 * 60)});
    renderTimeout();

    await new Promise((resolve) => setTimeout(resolve, 100));
    expect(openDialog()).toBeNull();
  });

  it('checks again as the warning is due, and stays quiet when the session was extended elsewhere', async () => {
    let answers = 0;
    const fetch = mockApi({[SESSION]: () => session(answers++ === 0 ? 5 * 60 + 0.3 : 8 * 60 * 60)});
    renderTimeout();

    await waitFor(() => expect(calls(fetch).filter((c) => c === SESSION)).toHaveLength(2), {timeout: 2000});
    await new Promise((resolve) => setTimeout(resolve, 50));
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

  it('stays closed once closed, until the end moves', async () => {
    mockApi({[SESSION]: () => session(4 * 60)});
    const {user} = renderTimeout();

    await warning();
    await user.click(within(openDialog()!).getByRole('button', {name: /close/i}));
    await waitFor(() => expect(openDialog()).toBeNull());

    // It counts on, closed.
    await new Promise((resolve) => setTimeout(resolve, 1100));
    expect(openDialog()).toBeNull();
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

  it('goes to the login page, saying why, once the session has ended', async () => {
    let answers = 0;
    mockApi({[SESSION]: () => (answers++ === 0 ? session(1) : unauthorized())});
    const {router} = renderTimeout();

    await warning();
    expect(openDialog()).toHaveTextContent(/signed out in 1 second\./);

    await waitFor(() => expect(router.state.location.pathname).toBe('/login'), {timeout: 4000});
    expect(router.state.location.search).toEqual({expired: true});
  });

  it('ends it too when the check at the end fails some other way', async () => {
    let answers = 0;
    mockApi({[SESSION]: () => (answers++ === 0 ? session(0.2) : json({title: 'Unavailable'}, 503))});
    const {router} = renderTimeout();

    await waitFor(() => expect(router.state.location.pathname).toBe('/login'), {timeout: 4000});
  });

  it('goes straight to the login page when the session has already ended', async () => {
    mockApi({[SESSION]: unauthorized});
    const {router} = renderTimeout();

    await waitFor(() => expect(router.state.location.pathname).toBe('/login'));
  });

  it('checks again when the tab is shown, as timers sleep in hidden tabs', async () => {
    const fetch = mockApi({[SESSION]: () => session(60 * 60)});
    renderTimeout();
    await waitFor(() => expect(calls(fetch)).toEqual([SESSION]));

    act(() => {
      Object.defineProperty(document, 'visibilityState', {value: 'hidden', configurable: true});
      document.dispatchEvent(new Event('visibilitychange'));
    });
    expect(calls(fetch)).toHaveLength(1);
    act(() => {
      Object.defineProperty(document, 'visibilityState', {value: 'visible', configurable: true});
      document.dispatchEvent(new Event('visibilitychange'));
    });

    await waitFor(() => expect(calls(fetch)).toEqual([SESSION, SESSION]));
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
