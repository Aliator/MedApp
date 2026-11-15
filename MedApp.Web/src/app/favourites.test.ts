import {act, renderHook} from '@testing-library/react';
import {useSavedFavourites} from './favourites';

const KEY = 'medapp.favourites';
const stored = () => JSON.parse(localStorage.getItem(KEY) ?? 'null') as unknown;

describe('useSavedFavourites', () => {
  it('saves, renames and deletes a search, one list apart from another', () => {
    const {result} = renderHook(() => useSavedFavourites('patients'));

    act(() => result.current.add('  Test search  ', {name: 'test', isArchived: false}, false));
    const [saved] = result.current.saved;
    expect(saved).toMatchObject({name: 'Test search', values: {name: 'test', isArchived: false}});
    expect(result.current.start).toBeUndefined();

    act(() => result.current.rename(saved.id, 'Renamed search'));
    expect(result.current.saved.map((f) => f.name)).toEqual(['Renamed search']);
    expect(renderHook(() => useSavedFavourites('procedures')).result.current.saved).toEqual([]);

    act(() => result.current.remove(saved.id));
    expect(result.current.saved).toEqual([]);
    expect(stored()).toEqual({patients: {saved: []}});
  });

  it('starts the list on a chosen search, and forgets that when it is deleted', () => {
    const {result} = renderHook(() => useSavedFavourites('patients'));

    act(() => result.current.add('First', {name: 'a'}, true));
    act(() => result.current.add('Second', {name: 'b'}, false));
    expect(result.current.start?.name).toBe('First');

    act(() => result.current.setStart(result.current.saved[1].id));
    expect(result.current.start?.name).toBe('Second');

    act(() => result.current.remove(result.current.saved[0].id));
    expect(result.current.start?.name).toBe('Second');

    act(() => result.current.remove(result.current.saved[0].id));
    expect(result.current.start).toBeUndefined();
  });

  it('still names new searches apart without crypto.randomUUID', () => {
    vi.stubGlobal('crypto', {});
    const {result} = renderHook(() => useSavedFavourites('patients'));

    act(() => result.current.add('First', {}, false));
    act(() => result.current.add('Second', {}, false));

    const [first, second] = result.current.saved;
    expect(first.id).toEqual(expect.any(String));
    expect(first.id).not.toBe(second.id);
  });

  it('ignores stored searches it cannot read', () => {
    localStorage.setItem(
      KEY,
      JSON.stringify({
        patients: {
          saved: [
            {id: 'f1', name: 'Kept', values: {name: 'test', isArchived: true}},
            {id: 'f2', name: 'Bad values', values: {page: 2}},
            {id: 'f3', values: {}},
            null,
          ],
          startId: 'f2',
        },
        procedures: {saved: 'not a list', startId: 'f1'},
        users: null,
      }),
    );

    const patients = renderHook(() => useSavedFavourites('patients')).result.current;
    expect(patients.saved.map((f) => f.id)).toEqual(['f1']);
    // It started on one it couldn't read: it starts on none.
    expect(patients.start).toBeUndefined();
    expect(renderHook(() => useSavedFavourites('procedures')).result.current.saved).toEqual([]);
    expect(renderHook(() => useSavedFavourites('users')).result.current.saved).toEqual([]);
  });

  it('starts with none when what is stored is not a set of lists', () => {
    localStorage.setItem(KEY, '{not json');
    expect(renderHook(() => useSavedFavourites('patients')).result.current.saved).toEqual([]);
  });

  it('starts with none when what is stored is not an object', () => {
    localStorage.setItem(KEY, '7');
    expect(renderHook(() => useSavedFavourites('patients')).result.current.saved).toEqual([]);
  });
});
