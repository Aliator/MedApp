import {act, waitFor} from '@testing-library/react';
import {json, mockApi, renderWithApp, staffUser} from '../test/render';
import {SAVE_DELAY_MS, SettingsSync} from './SettingsSync';
import {appSettings, defineSetting} from './settingsStore';

const SETTINGS = 'GET /api/auth/me/settings';
const SAVE = 'PUT /api/auth/me/settings';
const MODE = defineSetting('medapp.colorMode');
const SIZE = defineSetting('medapp.textSize');

/** What was saved: each PUT's settings, parsed. */
async function saves(fetch: ReturnType<typeof mockApi>) {
  const requests = fetch.mock.calls.map(([r]) => r as Request).filter((r) => r.method === 'PUT');
  return Promise.all(requests.map(async (r) => JSON.parse(((await r.clone().json()) as {settings: string}).settings)));
}

describe('SettingsSync', () => {
  it("replaces this browser's settings with the account's, as the app opens", async () => {
    localStorage.setItem(MODE, 'dark');
    localStorage.setItem(SIZE, 'small');
    mockApi({[SETTINGS]: () => json({settings: JSON.stringify({[MODE]: 'light'}), updatedAtUtc: '2026-11-14T12:00:00Z'})});

    renderWithApp(<SettingsSync />, {user: staffUser});

    await waitFor(() => expect(appSettings.get(MODE)).toBe('light'));
    // The account has no text size: back to the default.
    expect(appSettings.get(SIZE)).toBeNull();
    expect(localStorage.getItem(MODE)).toBe('light');
  });

  it("gives an account with no settings saved this browser's", async () => {
    localStorage.setItem(SIZE, 'large');
    const fetch = mockApi({[SETTINGS]: () => json({settings: null, updatedAtUtc: null}), [SAVE]: () => new Response(null, {status: 204})});

    renderWithApp(<SettingsSync />, {user: staffUser});

    await waitFor(async () => expect(await saves(fetch)).toEqual([{[SIZE]: 'large'}]));
  });

  it('saves a change with the account, a moment after it', async () => {
    const fetch = mockApi({
      [SETTINGS]: () => json({settings: JSON.stringify({[MODE]: 'dark'}), updatedAtUtc: null}),
      [SAVE]: () => new Response(null, {status: 204}),
    });
    renderWithApp(<SettingsSync />, {user: staffUser});
    await waitFor(() => expect(appSettings.get(MODE)).toBe('dark'));

    act(() => {
      appSettings.set(MODE, 'light');
      appSettings.set(SIZE, 'larger');
    });

    // The two changes save once, together.
    await waitFor(async () => expect(await saves(fetch)).toEqual([{[MODE]: 'light', [SIZE]: 'larger'}]), {timeout: 3000});
  });

  it("keeps this browser's settings when the account can't be reached", async () => {
    localStorage.setItem(MODE, 'light');
    mockApi({[SETTINGS]: () => json({title: 'Server error'}, 500)});

    renderWithApp(<SettingsSync />, {user: staffUser});

    await new Promise((resolve) => setTimeout(resolve, 50));
    expect(appSettings.get(MODE)).toBe('light');
  });
  it("keeps a change in this browser when saving it with the account fails", async () => {
    vi.useFakeTimers({shouldAdvanceTime: true});
    const fetch = mockApi({
      [SETTINGS]: () => json({settings: JSON.stringify({[MODE]: 'dark'}), updatedAtUtc: null}),
      [SAVE]: () => {
        throw new TypeError('Failed to fetch');
      },
    });
    renderWithApp(<SettingsSync />, {user: staffUser});
    await waitFor(() => expect(appSettings.get(MODE)).toBe('dark'));

    act(() => appSettings.set(MODE, 'light'));
    await act(() => vi.advanceTimersByTimeAsync(SAVE_DELAY_MS + 50));

    expect(fetch.mock.calls.some(([r]) => (r as Request).method === 'PUT')).toBe(true);
    expect(appSettings.get(MODE)).toBe('light');
    expect(localStorage.getItem(MODE)).toBe('light');
  });

  it('saves nothing for a change made before the account has answered', async () => {
    vi.useFakeTimers({shouldAdvanceTime: true});
    const fetch = mockApi({
      [SETTINGS]: () => new Promise<Response>(() => {}),
      [SAVE]: () => new Response(null, {status: 204}),
    });
    renderWithApp(<SettingsSync />, {user: staffUser});
    // It's asked the account for its settings, which haven't come.
    await waitFor(() => expect(fetch.mock.calls.some(([r]) => (r as Request).method === 'GET')).toBe(true));

    act(() => appSettings.set(MODE, 'light'));
    await act(() => vi.advanceTimersByTimeAsync(SAVE_DELAY_MS + 50));

    expect(fetch.mock.calls.some(([r]) => (r as Request).method === 'PUT')).toBe(false);
  });

  it("keeps this browser's settings when giving them to an account with none fails", async () => {
    localStorage.setItem(SIZE, 'large');
    const fetch = mockApi({
      [SETTINGS]: () => json({settings: null, updatedAtUtc: null}),
      [SAVE]: () => json({title: 'Server error'}, 500),
    });

    renderWithApp(<SettingsSync />, {user: staffUser});

    await waitFor(() => expect(fetch.mock.calls.some(([r]) => (r as Request).method === 'PUT')).toBe(true));
    expect(appSettings.get(SIZE)).toBe('large');
  });
});
