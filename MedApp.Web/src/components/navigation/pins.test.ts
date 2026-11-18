import {act, renderHook, waitFor} from '@testing-library/react';
import {appSettings} from '../../app/settingsStore';
import {legacyPinsKey, MAX_PINS, PINS_KEY, usePins} from './pins';

/** The pins as the account's settings keep them. */
const stored = () => JSON.parse(localStorage.getItem(PINS_KEY) ?? 'null') as unknown;

describe('usePins', () => {
  it("pins and unpins, keeping the pins with the account's settings", () => {
    const {result} = renderHook(() => usePins('patients', 'test-user'));

    act(() => result.current.pin('p1'));
    act(() => result.current.pin('p2'));
    expect(result.current.ids).toEqual(['p1', 'p2']);
    expect(result.current.isPinned('p1')).toBe(true);
    expect(stored()).toEqual({patients: ['p1', 'p2'], pathways: []});

    act(() => result.current.unpin('p1'));
    expect(result.current.ids).toEqual(['p2']);
    expect(stored()).toEqual({patients: ['p2'], pathways: []});
  });

  it('reads the pins kept from an earlier visit', () => {
    localStorage.setItem(PINS_KEY, JSON.stringify({pathways: ['x1', 'x2']}));

    const {result} = renderHook(() => usePins('pathways', 'test-user'));

    expect(result.current.ids).toEqual(['x1', 'x2']);
  });

  it(`allows at most ${MAX_PINS} pins`, () => {
    const {result} = renderHook(() => usePins('patients', 'test-user'));

    for (let i = 1; i <= MAX_PINS + 1; i++) act(() => result.current.pin(`p${i}`));

    expect(result.current.ids).toHaveLength(MAX_PINS);
    expect(result.current.isFull).toBe(true);
    expect(result.current.isPinned(`p${MAX_PINS + 1}`)).toBe(false);
  });

  it('pins an item only once', () => {
    const {result} = renderHook(() => usePins('patients', 'test-user'));

    act(() => result.current.pin('p1'));
    act(() => result.current.pin('p1'));

    expect(result.current.ids).toEqual(['p1']);
  });

  it("keeps each kind's pins separate", () => {
    localStorage.setItem(PINS_KEY, JSON.stringify({pathways: ['x1']}));

    const {result} = renderHook(() => usePins('patients', 'test-user'));
    act(() => result.current.pin('p1'));

    expect(result.current.ids).toEqual(['p1']);
    expect(stored()).toEqual({patients: ['p1'], pathways: ['x1']});
  });

  it('counts several changes in a row, each from the latest pins', () => {
    localStorage.setItem(PINS_KEY, JSON.stringify({patients: ['p1', 'p2', 'p3']}));
    const {result} = renderHook(() => usePins('patients', 'test-user'));

    act(() => {
      result.current.unpin('p1');
      result.current.unpin('p3');
    });

    expect(result.current.ids).toEqual(['p2']);
  });

  it('carries over the pins this browser kept for the user before they moved to the account', async () => {
    localStorage.setItem(legacyPinsKey('patients', 'test-user'), JSON.stringify(['p9']));
    localStorage.setItem(legacyPinsKey('pathways', 'test-user'), JSON.stringify(['x9']));
    localStorage.setItem(legacyPinsKey('patients', 'someone-else'), JSON.stringify(['p8']));

    const {result} = renderHook(() => usePins('patients', 'test-user'));

    await waitFor(() => expect(result.current.ids).toEqual(['p9']));
    expect(stored()).toEqual({patients: ['p9'], pathways: ['x9']});
  });

  it('carries nothing over once the account has pins, or when there were none', () => {
    localStorage.setItem(PINS_KEY, JSON.stringify({patients: ['p1']}));
    localStorage.setItem(legacyPinsKey('patients', 'test-user'), JSON.stringify(['p9']));
    expect(renderHook(() => usePins('patients', 'test-user')).result.current.ids).toEqual(['p1']);

    localStorage.clear();
    appSettings.reset();
    renderHook(() => usePins('patients', 'test-user'));
    expect(localStorage.getItem(PINS_KEY)).toBeNull();
  });

  it('keeps the old keys it carries pins over from', () => {
    expect(legacyPinsKey('patients', 'test-user')).toBe('medapp.pinnedPatients.test-user');
    expect(legacyPinsKey('pathways', 'test-user')).toBe('medapp.pinnedProcedures.test-user');
  });

  it('ignores stored pins it cannot read', () => {
    localStorage.setItem(PINS_KEY, '{not json');
    localStorage.setItem(legacyPinsKey('pathways', 'test-user'), '{not json');

    const {result} = renderHook(() => usePins('patients', 'test-user'));

    expect(result.current.ids).toEqual([]);
  });

  it('ignores stored pins that are not a list, or not IDs, and repeats', () => {
    localStorage.setItem(PINS_KEY, JSON.stringify({patients: {p1: true}, pathways: ['x1', '', 7, 'x1', 'x2']}));

    expect(renderHook(() => usePins('patients', 'test-user')).result.current.ids).toEqual([]);
    expect(renderHook(() => usePins('pathways', 'test-user')).result.current.ids).toEqual(['x1', 'x2']);
  });

  it('shows pins made in another tab', () => {
    const {result} = renderHook(() => usePins('patients', 'test-user'));

    localStorage.setItem(PINS_KEY, JSON.stringify({patients: ['p1']}));
    act(() => {
      window.dispatchEvent(new StorageEvent('storage', {key: PINS_KEY, newValue: JSON.stringify({patients: ['p1']})}));
    });

    expect(result.current.ids).toEqual(['p1']);
  });

  it('keeps pins for this visit when storage is unavailable', () => {
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('Storage is blocked.');
    });
    const {result} = renderHook(() => usePins('patients', 'test-user'));

    act(() => result.current.pin('p1'));

    expect(result.current.ids).toEqual(['p1']);
  });
});
