import {QueryClient} from '@tanstack/react-query';
import {act, render, renderHook, screen} from '@testing-library/react';
import {useEffect} from 'react';
import {settingsQuery} from '../api/settings';
import {DEFAULT_PAGE_SIZE, ListSettingsProvider, listSettingsFor, MAX_PAGE_SIZE, useListSettings} from './listSettings';

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

describe('ListSettingsProvider', () => {
  let api: ReturnType<typeof useListSettings>;
  function Probe() {
    const value = useListSettings();
    // Handed to the test after rendering, as effects run.
    useEffect(() => {
      api = value;
    });
    return <output>{`${value.pageSize} ${value.startsOnMine} ${value.calendarRows}`}</output>;
  }
  const renderSettings = () =>
    render(
      <ListSettingsProvider>
        <Probe />
      </ListSettingsProvider>,
    );

  it('changes and remembers the page size and where procedure lists start', () => {
    renderSettings();
    expect(screen.getByRole('status')).toHaveTextContent(`${DEFAULT_PAGE_SIZE} false 15`);

    act(() => api.setPageSize(25));
    act(() => api.setStartsOnMine(true));

    expect(screen.getByRole('status')).toHaveTextContent('25 true 15');
    expect(localStorage.getItem('medapp.pageSize')).toBe('25');
    expect(localStorage.getItem('medapp.proceduresStartOnMine')).toBe('true');
  });

  it('keeps the page size to what the lists can show', () => {
    renderSettings();

    act(() => api.setPageSize(MAX_PAGE_SIZE + 1));

    expect(screen.getByRole('status')).toHaveTextContent(`${DEFAULT_PAGE_SIZE} false 15`);
  });

  it("remembers the Calendar's row length, keeping only 5 (15 is the default)", () => {
    renderSettings();

    act(() => api.setCalendarRows(5));
    expect(screen.getByRole('status')).toHaveTextContent(`${DEFAULT_PAGE_SIZE} false 5`);
    expect(localStorage.getItem('medapp.calendarRows')).toBe('5');

    act(() => api.setCalendarRows(15));
    expect(screen.getByRole('status')).toHaveTextContent(`${DEFAULT_PAGE_SIZE} false 15`);
    expect(localStorage.getItem('medapp.calendarRows')).toBeNull();
  });

  it('has the defaults outside its provider (a part tested on its own), and changes nothing there', () => {
    const {result} = renderHook(() => useListSettings());

    result.current.setPageSize(50);
    result.current.setStartsOnMine(true);
    result.current.setCalendarRows(5);

    expect(result.current).toMatchObject({pageSize: DEFAULT_PAGE_SIZE, startsOnMine: false, calendarRows: 15});
    expect(localStorage.length).toBe(0);
  });
});
