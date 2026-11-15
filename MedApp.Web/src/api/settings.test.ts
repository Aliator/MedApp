import {QueryClient} from '@tanstack/react-query';
import {json, mockApi} from '../test/render';
import {saveSettings, settingsQuery} from './settings';

const SETTINGS = 'GET /api/auth/me/settings';

const load = () => new QueryClient().fetchQuery(settingsQuery);

describe('settingsQuery', () => {
  it("reads the account's saved settings", async () => {
    mockApi({[SETTINGS]: () => json({settings: JSON.stringify({'medapp.pageSize': '25'})})});

    expect(await load()).toEqual({'medapp.pageSize': '25'});
  });

  it('is null when nothing has been saved', async () => {
    mockApi({[SETTINGS]: () => json({settings: null})});

    expect(await load()).toBeNull();
  });

  it.each([['not json'], ['["medapp.pageSize"]'], ['"medapp.pageSize"'], ['null']])('is null for saved settings it cannot read: %s', async (saved) => {
    mockApi({[SETTINGS]: () => json({settings: saved})});

    expect(await load()).toBeNull();
  });
});

describe('saveSettings', () => {
  it('saves the settings as one JSON string', async () => {
    const fetch = mockApi({'PUT /api/auth/me/settings': () => new Response(null, {status: 204})});

    await saveSettings({'medapp.colorMode': 'light'});

    expect(await (fetch.mock.calls[0][0] as Request).json()).toEqual({settings: JSON.stringify({'medapp.colorMode': 'light'})});
  });
});
