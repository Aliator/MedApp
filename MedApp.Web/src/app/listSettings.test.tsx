import {QueryClient} from '@tanstack/react-query';
import {settingsQuery} from '../api/settings';
import {DEFAULT_PAGE_SIZE, listSettingsFor} from './listSettings';

describe('listSettingsFor', () => {
  it("in the browser, reads this browser's list settings", () => {
    localStorage.setItem('medapp.pageSize', '30');
    localStorage.setItem('medapp.proceduresStartOnMine', 'true');

    expect(listSettingsFor(new QueryClient())).toEqual({pageSize: 30, startsOnMine: true});
  });

  it('falls back to the defaults for anything unset or unreadable', () => {
    localStorage.setItem('medapp.pageSize', '500');

    expect(listSettingsFor(new QueryClient())).toEqual({pageSize: DEFAULT_PAGE_SIZE, startsOnMine: false});
  });

  it("rendering on the server, reads the account's saved settings", () => {
    vi.stubGlobal('window', undefined);
    const queryClient = new QueryClient();
    const saved: Record<string, unknown> = {'medapp.pageSize': '20', 'medapp.proceduresStartOnMine': 'true'};
    queryClient.setQueryData(settingsQuery.queryKey, saved);

    expect(listSettingsFor(queryClient)).toEqual({pageSize: 20, startsOnMine: true});
    expect(listSettingsFor(new QueryClient())).toEqual({pageSize: DEFAULT_PAGE_SIZE, startsOnMine: false});
  });
});
