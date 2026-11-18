import {act, renderHook} from '@testing-library/react';
import {defaultTabId, parseTabChanges, resolveTabs, useListTabs, type DefaultTab} from './favourites';

const KEY = 'medapp.favourites';
const stored = () => JSON.parse(localStorage.getItem(KEY) ?? 'null') as unknown;

const DEFAULTS: DefaultTab[] = [
  {label: 'Active', values: {status: 'active'}},
  {label: 'Archived', values: {status: 'archived'}},
  {label: 'All', values: {}},
];

const labels = (tabs: {label: string}[]) => tabs.map((t) => t.label);

describe('useListTabs', () => {
  it("shows the list's own tabs, storing nothing, until the person changes them", () => {
    const {result} = renderHook(() => useListTabs('patients', DEFAULTS));

    expect(labels(result.current.tabs)).toEqual(['Active', 'Archived', 'All']);
    expect(result.current.tabs[0]).toEqual({id: defaultTabId('Active'), label: 'Active', values: {status: 'active'}, isDefault: true});
    expect(result.current.isCustomised).toBe(false);
    expect(stored()).toBeNull();
  });

  it('adds, edits and removes tabs, one list apart from another', () => {
    const {result} = renderHook(() => useListTabs('patients', DEFAULTS));

    act(() => void result.current.add('  Smiths  ', {last: 'Smith'}));
    const added = result.current.tabs[3];
    expect(added).toMatchObject({label: 'Smiths', values: {last: 'Smith'}, isDefault: false});

    act(() => result.current.edit(added.id, 'Smith family', {last: 'Smith', first: 'A'}));
    act(() => result.current.edit(defaultTabId('Archived'), 'Old ones', {status: 'archived'}));
    expect(labels(result.current.tabs)).toEqual(['Active', 'Old ones', 'All', 'Smith family']);
    expect(result.current.tabs[3].values).toEqual({last: 'Smith', first: 'A'});
    expect(labels(renderHook(() => useListTabs('pathways', DEFAULTS)).result.current.tabs)).toEqual(['Active', 'Archived', 'All']);

    act(() => result.current.remove(defaultTabId('Archived')));
    act(() => result.current.remove(added.id));
    expect(labels(result.current.tabs)).toEqual(['Active', 'All']);
    expect(stored()).toEqual({patients: {added: [], edited: {}, removed: [defaultTabId('Archived')]}});
  });

  it('reorders the tabs, keeping a new tab last', () => {
    const {result} = renderHook(() => useListTabs('patients', DEFAULTS));

    act(() => result.current.move(defaultTabId('All'), 0));
    expect(labels(result.current.tabs)).toEqual(['All', 'Active', 'Archived']);

    act(() => void result.current.add('Mine', {mine: true}));
    expect(labels(result.current.tabs)).toEqual(['All', 'Active', 'Archived', 'Mine']);

    act(() => result.current.move(result.current.tabs[3].id, 1));
    expect(labels(result.current.tabs)).toEqual(['All', 'Mine', 'Active', 'Archived']);

    act(() => result.current.remove(result.current.tabs[1].id));
    expect(labels(result.current.tabs)).toEqual(['All', 'Active', 'Archived']);

    act(() => result.current.move('gone', 0));
    expect(labels(result.current.tabs)).toEqual(['All', 'Active', 'Archived']);
  });

  it('resets to the defaults by forgetting every change', () => {
    localStorage.setItem(KEY, JSON.stringify({pathways: {added: [], edited: {}, removed: ['x']}}));
    const {result} = renderHook(() => useListTabs('patients', DEFAULTS));
    act(() => void result.current.add('Mine', {mine: true}));
    expect(result.current.isCustomised).toBe(true);

    act(() => result.current.reset());

    expect(labels(result.current.tabs)).toEqual(['Active', 'Archived', 'All']);
    expect(result.current.isCustomised).toBe(false);
    expect(stored()).toEqual({pathways: {added: [], edited: {}, removed: ['x']}});

    const other = renderHook(() => useListTabs('pathways', DEFAULTS)).result;
    act(() => other.current.reset());
    expect(stored()).toBeNull();
  });

  it('still names new tabs apart without crypto.randomUUID', () => {
    vi.stubGlobal('crypto', {});
    const {result} = renderHook(() => useListTabs('patients', DEFAULTS));

    act(() => void result.current.add('First', {}));
    act(() => void result.current.add('Second', {}));

    const [first, second] = result.current.tabs.slice(3);
    expect(first.id).toEqual(expect.any(String));
    expect(first.id).not.toBe(second.id);
  });
});

describe('parseTabChanges', () => {
  it('turns searches saved before tabs into tabs of the person, the one the list started on first', () => {
    const changes = parseTabChanges(
      JSON.stringify({
        patients: {
          saved: [
            {id: 'f1', name: 'Smiths', values: {last: 'Smith'}},
            {id: 'f2', name: 'Under 18', values: {dobFrom: '2008-11-19'}},
            {id: 'f3', values: {}},
            null,
          ],
          startId: 'f2',
        },
        empty: {saved: []},
        notAList: {saved: 'x', startId: 'f1'},
      }),
    );

    expect(changes).toEqual({
      patients: {
        added: [
          {id: 'f1', name: 'Smiths', values: {last: 'Smith'}},
          {id: 'f2', name: 'Under 18', values: {dobFrom: '2008-11-19'}},
        ],
        edited: {},
        removed: [],
        order: ['f2'],
      },
    });
    expect(labels(resolveTabs(DEFAULTS, changes.patients))).toEqual(['Under 18', 'Active', 'Archived', 'All', 'Smiths']);
  });

  it('keeps saved searches without a starting one in their order, after the defaults', () => {
    const changes = parseTabChanges(JSON.stringify({patients: {saved: [{id: 'f1', name: 'Smiths', values: {}}], startId: 'gone'}}));

    expect(changes.patients.order).toBeUndefined();
    expect(labels(resolveTabs(DEFAULTS, changes.patients))).toEqual(['Active', 'Archived', 'All', 'Smiths']);
  });

  it('leaves out what it cannot read', () => {
    expect(parseTabChanges('{not json')).toEqual({});
    expect(parseTabChanges('7')).toEqual({});
    expect(parseTabChanges(null)).toEqual({});
    expect(
      parseTabChanges(
        JSON.stringify({
          broken: 'x',
          users: null,
          patients: {
            added: [{id: 'a', name: 'Kept', values: {x: 'y', on: true}}, {id: 'b', name: 'Bad', values: {x: 1}}, {name: 'No ID', values: {}}],
            edited: {e1: {name: 'Kept', values: {}}, e2: {name: 3, values: {}}, e3: null},
            removed: ['r1', 4],
            order: 'not a list',
          },
          other: {edited: 'x'},
        }),
      ),
    ).toEqual({
      patients: {added: [{id: 'a', name: 'Kept', values: {x: 'y', on: true}}], edited: {e1: {name: 'Kept', values: {}}}, removed: ['r1'], order: undefined},
      other: {added: [], edited: {}, removed: [], order: undefined},
    });
  });
});
