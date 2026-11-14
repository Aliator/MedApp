import {act, renderHook} from '@testing-library/react';
import {MAX_PINS, pinsKey, usePins} from './pins';

const stored = (key: string) => JSON.parse(localStorage.getItem(key) ?? 'null') as unknown;

describe('usePins', () => {
  it('pins and unpins, keeping the pins in storage', () => {
    const {result} = renderHook(() => usePins('patients', 'test-user'));

    act(() => result.current.pin('p1'));
    act(() => result.current.pin('p2'));
    expect(result.current.ids).toEqual(['p1', 'p2']);
    expect(result.current.isPinned('p1')).toBe(true);
    expect(stored(pinsKey('patients', 'test-user'))).toEqual(['p1', 'p2']);

    act(() => result.current.unpin('p1'));
    expect(result.current.ids).toEqual(['p2']);
    expect(stored(pinsKey('patients', 'test-user'))).toEqual(['p2']);
  });

  it('reads the pins kept from an earlier visit', () => {
    localStorage.setItem(pinsKey('procedures', 'test-user'), JSON.stringify(['x1', 'x2']));

    const {result} = renderHook(() => usePins('procedures', 'test-user'));

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

  it("keeps each user's and each kind's pins separate", () => {
    localStorage.setItem(pinsKey('patients', 'someone-else'), JSON.stringify(['p9']));
    localStorage.setItem(pinsKey('procedures', 'test-user'), JSON.stringify(['x1']));

    const {result} = renderHook(() => usePins('patients', 'test-user'));

    expect(result.current.ids).toEqual([]);
  });

  it('keeps the storage key it had before procedures could be pinned', () => {
    expect(pinsKey('patients', 'test-user')).toBe('medapp.pinnedPatients.test-user');
    expect(pinsKey('procedures', 'test-user')).toBe('medapp.pinnedProcedures.test-user');
  });

  it('ignores stored pins it cannot read', () => {
    localStorage.setItem(pinsKey('patients', 'test-user'), '{not json');

    const {result} = renderHook(() => usePins('patients', 'test-user'));

    expect(result.current.ids).toEqual([]);
  });
});
