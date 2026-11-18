import {screen, waitFor, within} from '@testing-library/react';
import {useState} from 'react';
import {openDialog, openSearch, pickFavourite, pickListOption, renderWithApp, searchBar} from '../../test/render';
import {
  configFor,
  dayFromToday,
  filtersFrom,
  ListFilterBar,
  valuesFrom,
  type ListFilterFavourite,
  type ListFilterField,
  type ListFilterValues,
} from './ListFilterBar';

const fields: ListFilterField[] = [
  {kind: 'text', key: 'title', label: 'Pathway'},
  {kind: 'choice', key: 'status', label: 'Status', options: [{value: 'Open', label: 'Open'}, {value: 'Blocked', label: 'Blocked'}, {value: 'Done', label: 'Done'}], isMultiple: true},
  {kind: 'date', key: 'due', label: 'Due', fromKey: 'dueFrom', toKey: 'dueTo'},
  {kind: 'flag', key: 'overdue', label: 'Overdue', operator: 'only'},
];

describe('ListFilterBar', () => {
  it('turns the URL into a token per filter', () => {
    expect(
      filtersFrom(fields, {title: 'knee', status: '!Blocked,Done', dueFrom: '2026-12-01', dueTo: '2026-12-31', overdue: true, page: 2}),
    ).toEqual([
      {field: 'title', operator: 'contains', value: {type: 'string', value: 'knee'}},
      {field: 'status', operator: 'none_of', value: {type: 'enum_list', value: ['Blocked', 'Done']}},
      {field: 'due', operator: 'on_or_after', value: {type: 'date_absolute', unixSeconds: Date.UTC(2026, 11, 1) / 1000}},
      {field: 'due', operator: 'on_or_before', value: {type: 'date_absolute', unixSeconds: Date.UTC(2026, 11, 31) / 1000}},
      {field: 'overdue', operator: 'is', value: {type: 'empty'}},
    ]);
  });

  it('shows one choice as "is", several as "is any of"', () => {
    expect(filtersFrom(fields, {status: 'Open'})).toEqual([{field: 'status', operator: 'is', value: {type: 'enum', value: 'Open'}}]);
    expect(filtersFrom(fields, {status: 'Open,Done'})).toEqual([
      {field: 'status', operator: 'any_of', value: {type: 'enum_list', value: ['Open', 'Done']}},
    ]);
  });

  it('turns tokens back into the URL, clearing every field without one', () => {
    expect(
      valuesFrom(fields, [
        {field: 'title', operator: 'contains', value: {type: 'string', value: ' knee '}},
        {field: 'status', operator: 'none_of', value: {type: 'enum_list', value: ['Blocked']}},
        {field: 'due', operator: 'on_or_before', value: {type: 'date_absolute', unixSeconds: Date.UTC(2026, 11, 31) / 1000}},
      ]),
    ).toEqual({title: 'knee', status: '!Blocked', dueFrom: undefined, dueTo: '2026-12-31', overdue: undefined});
    expect(valuesFrom(fields, [{field: 'overdue', operator: 'is', value: {type: 'empty'}}])).toMatchObject({overdue: true, title: undefined});
  });

  it('shows the filters as tokens', async () => {
    renderWithApp(<ListFilterBar list="test" fields={fields} values={{title: 'knee', overdue: true}} onChange={() => {}} label="Search" placeholder="Search..." />);

    await vi.waitFor(() => expect(searchBar()).toHaveTextContent(/Pathway: contains\s*knee/));
    expect(searchBar()).toHaveTextContent(/Overdue: only/);
  });

  it('marks its text box as not for autofill, so a saved login is never filled in', async () => {
    const {user} = renderWithApp(<ListFilterBar list="test" fields={fields} values={{}} onChange={() => {}} label="Search users" placeholder="Search..." />);
    await openSearch(user);

    const input = await vi.waitFor(() => {
      const box = searchBar().querySelector('input');
      expect(box).not.toBeNull();
      return box!;
    });
    await vi.waitFor(() => expect(input).toHaveAttribute('data-1p-ignore', 'true'));
    expect(input).toHaveAttribute('autocomplete', 'off');
    expect(input).toHaveAttribute('data-lpignore', 'true');
    expect(input).toHaveAttribute('data-bwignore', 'true');
  });

  it("sets a tab's filters, clearing the others", async () => {
    const {user, onChange} = renderHarness({title: 'knee'}, [{label: 'Blocked', values: {status: 'Blocked'}}]);

    await pickFavourite(user, 'Blocked');

    expect(onChange).toHaveBeenCalledWith({title: undefined, status: 'Blocked', dueFrom: undefined, dueTo: undefined, overdue: undefined});
  });

  it('offers "is not any of" on a multiple choice, unless told it can not exclude', () => {
    const operators = (field: ListFilterField) => configFor([field]).fields[0].operators.map((o) => o.label);
    const options = [{value: 'a', label: 'A'}];

    expect(operators({kind: 'choice', key: 'x', label: 'X', options, isMultiple: true})).toEqual(['is', 'is any of', 'is not any of']);
    expect(operators({kind: 'choice', key: 'x', label: 'X', options, isMultiple: true, canExclude: false})).toEqual(['is', 'is any of']);
    expect(operators({kind: 'choice', key: 'x', label: 'X', options})).toEqual(['is']);
  });

  it('gives a day as the URL keeps it, from today', () => {
    expect(dayFromToday()).toMatch(/^\d{4}-\d{2}-\d{2}$/);
    expect(Number(dayFromToday({years: -18}).slice(0, 4))).toBe(new Date().getFullYear() - 18);
  });
});

describe('ListFilterBar, more', () => {
  const withSingle: ListFilterField[] = [...fields, {kind: 'choice', key: 'priority', label: 'Priority', options: [{value: 'High', label: 'High'}]}];

  it('leaves out tokens that set nothing, or that it has no field for', () => {
    expect(
      valuesFrom(withSingle, [
        {field: 'title', operator: 'contains', value: {type: 'string', value: '   '}},
        {field: 'status', operator: 'any_of', value: {type: 'enum_list', value: []}},
        {field: 'priority', operator: 'is', value: {type: 'enum', value: ''}},
        {field: 'retired', operator: 'is', value: {type: 'string', value: 'test'}},
      ]),
    ).toEqual({title: undefined, status: undefined, priority: undefined, dueFrom: undefined, dueTo: undefined, overdue: undefined});
  });

  it('reads a list of choices as "is any of", and a date on or after', () => {
    expect(
      valuesFrom(withSingle, [
        {field: 'status', operator: 'any_of', value: {type: 'enum_list', value: ['Open', 'Done']}},
        {field: 'priority', operator: 'is', value: {type: 'enum', value: 'High'}},
        {field: 'due', operator: 'on_or_after', value: {type: 'date_absolute', unixSeconds: Date.UTC(2026, 11, 1) / 1000}},
      ]),
    ).toMatchObject({status: 'Open,Done', priority: 'High', dueFrom: '2026-12-01'});
  });

  it('reports a filter removed from the bar', async () => {
    const onChange = vi.fn();
    const {user} = renderWithApp(
      <ListFilterBar list="test" fields={withSingle} values={{title: 'knee', priority: 'High'}} onChange={onChange} label="Search" placeholder="Search..." />,
    );

    await vi.waitFor(() => expect(searchBar()).toHaveTextContent(/Priority: is\s*High/));
    const remove = within(searchBar()).getAllByRole('button', {name: /remove/i});
    await user.click(remove[0]);

    expect(onChange).toHaveBeenCalledWith(expect.objectContaining({title: undefined, priority: 'High'}));
  });

  it('still marks its text box as not for autofill in a browser without MutationObserver', async () => {
    vi.stubGlobal('MutationObserver', undefined);
    renderWithApp(<ListFilterBar list="test" fields={fields} values={{title: 'x'}} onChange={() => {}} label="Search users" placeholder="Search..." />);

    await vi.waitFor(() => expect(searchBar().querySelector('input')).toHaveAttribute('data-1p-ignore', 'true'));
  });
});

/** The bar as a page holds it: its search in state (the URL), changed by what the bar reports. */
function renderHarness(start: ListFilterValues = {}, favourites?: ListFilterFavourite[], list = 'test') {
  const onChange = vi.fn();
  function Harness() {
    const [values, setValues] = useState<ListFilterValues>(start);
    return (
      <ListFilterBar
        list={list}
        fields={fields}
        values={values}
        onChange={(next) => {
          onChange(next);
          setValues(next);
        }}
        label="Search pathways"
        placeholder="Search..."
        favourites={favourites}
      />
    );
  }
  return {...renderWithApp(<Harness />), onChange};
}

const TABS: ListFilterFavourite[] = [
  {label: 'Open', values: {status: 'Open'}},
  {label: 'Overdue', values: {overdue: true}},
  {label: 'Everything', values: {}},
];

/** An element's visible words, leaving out the copies kept for layout (a tab holds its label twice, one hidden). */
function shownText(element: HTMLElement) {
  const copy = element.cloneNode(true) as HTMLElement;
  copy.querySelectorAll('[aria-hidden="true"]').forEach((hidden) => hidden.remove());
  return copy.textContent;
}
const tabNames = () => screen.getAllByRole('tab').map(shownText);
const selectedTab = () => shownText(screen.getByRole('tab', {selected: true}));
const dialog = () => within(openDialog()!);

describe('ListFilterBar, tabs', () => {
  it('opens on its tabs, the one showing selected and named on the menu', async () => {
    const {user, onChange} = renderHarness({status: 'Open'}, TABS);

    await screen.findAllByRole('tab');
    expect(tabNames()).toEqual(['Open', 'Overdue', 'Everything']);
    expect(selectedTab()).toBe('Open');
    expect(searchBar()).toBeNull();
    expect(screen.getByRole('button', {name: 'List options (showing Open)'})).toBeInTheDocument();

    await user.click(screen.getByRole('tab', {name: 'Overdue'}));
    expect(onChange).toHaveBeenLastCalledWith(expect.objectContaining({overdue: true, status: undefined}));
    expect(selectedTab()).toBe('Overdue');
  });

  it('has one tab, All, when the list has none of its own', async () => {
    renderHarness();

    await screen.findAllByRole('tab');
    expect(tabNames()).toEqual(['All']);
    expect(selectedTab()).toBe('All');
  });

  it('swaps the tabs for the search, and back to them', async () => {
    const {user, onChange} = renderHarness({status: 'Open'}, TABS);

    await pickListOption(user, 'Search');
    await waitFor(() => expect(searchBar()).toHaveTextContent(/Status: is\s*Open/));
    expect(screen.queryByRole('tab')).toBeNull();

    await pickListOption(user, 'Tabs');
    expect(selectedTab()).toBe('Open');
    expect(onChange).not.toHaveBeenCalled();
  });

  it('shows a search no tab shows as the bar, and going back to the tabs takes the first', async () => {
    const {user, onChange} = renderHarness({title: 'knee'}, TABS);

    await waitFor(() => expect(searchBar()).toHaveTextContent(/Pathway: contains\s*knee/));
    expect(screen.getByRole('button', {name: 'List options'})).toBeInTheDocument();

    await pickListOption(user, 'Tabs');

    expect(onChange).toHaveBeenLastCalledWith(expect.objectContaining({status: 'Open', title: undefined}));
    expect(selectedTab()).toBe('Open');
  });

  it('opens, unfiltered, on the first tab the person moved there', async () => {
    localStorage.setItem('medapp.favourites', JSON.stringify({test: {added: [], edited: {}, removed: [], order: ['default:Overdue']}}));

    const {onChange} = renderHarness({}, TABS);

    await waitFor(() => expect(onChange).toHaveBeenCalledWith(expect.objectContaining({overdue: true})));
    expect(selectedTab()).toBe('Overdue');
  });
});

describe('ListFilterBar, configuration', () => {
  const configure = async (user: ReturnType<typeof renderHarness>['user']) => {
    await pickListOption(user, 'Configuration');
    await waitFor(() => expect(openDialog()).not.toBeNull());
  };
  const listed = () => dialog().getAllByRole('listitem').map((item) => item.textContent ?? '');

  it('adds a tab with its name and search, starting from the search showing', async () => {
    const {user} = renderHarness({status: 'Open'}, TABS);
    await configure(user);

    await user.click(dialog().getByRole('button', {name: 'Add Tab'}));
    expect(dialog().getByRole('button', {name: 'Save Tab'})).toHaveAttribute('aria-disabled', 'true');
    await waitFor(() => expect(openDialog()!.querySelector('.astryx-power-search')).toHaveTextContent(/Status: is\s*Open/));
    await user.type(dialog().getByRole('textbox', {name: /Name/}), 'Open ones');
    await user.click(dialog().getByRole('button', {name: 'Save Tab'}));

    await waitFor(() => expect(listed().some((t) => t.includes('4. Open ones') && t.includes('Added by you'))).toBe(true));
    expect(tabNames()).toEqual(['Open', 'Overdue', 'Everything', 'Open ones']);
    expect(JSON.parse(localStorage.getItem('medapp.favourites')!).test.added[0]).toMatchObject({name: 'Open ones', values: {status: 'Open'}});
  });

  it('edits and removes a tab, keeping one at least', async () => {
    const {user} = renderHarness({}, [{label: 'Open', values: {status: 'Open'}}, {label: 'Everything', values: {}}]);
    await configure(user);

    await user.click(dialog().getByRole('button', {name: 'Edit Open'}));
    const name = dialog().getByRole('textbox', {name: /Name/});
    await user.clear(name);
    await user.type(name, 'Still open');
    await user.click(dialog().getByRole('button', {name: 'Save Tab'}));
    await waitFor(() => expect(tabNames()).toEqual(['Still open', 'Everything']));

    await user.click(dialog().getByRole('button', {name: 'Edit Still open'}));
    await user.click(dialog().getByRole('button', {name: 'Back'}));
    expect(tabNames()).toEqual(['Still open', 'Everything']);

    await user.click(dialog().getByRole('button', {name: 'Remove Still open'}));
    await waitFor(() => expect(tabNames()).toEqual(['Everything']));
    expect(dialog().getByRole('button', {name: 'Remove Everything'})).toHaveAttribute('aria-disabled', 'true');
  });

  it('moves a tab with the keyboard from its handle', async () => {
    const {user} = renderHarness({}, TABS);
    await configure(user);

    await user.click(dialog().getByRole('button', {name: /Reorder.*1\. Open/}));
    await user.keyboard('{ArrowDown}');

    await waitFor(() => expect(tabNames()).toEqual(['Overdue', 'Open', 'Everything']));
  });

  it('resets to the defaults after asking, and only once something changed', async () => {
    localStorage.setItem('medapp.favourites', JSON.stringify({test: {added: [{id: 'a1', name: 'Mine', values: {}}], edited: {}, removed: ['default:Overdue']}}));
    const {user} = renderHarness({status: 'Open'}, TABS);
    await screen.findAllByRole('tab');
    expect(tabNames()).toEqual(['Open', 'Everything', 'Mine']);
    await configure(user);

    await user.click(dialog().getByRole('button', {name: 'Reset to Default'}));
    await user.click(dialog().getByRole('button', {name: 'Back'}));
    await user.click(dialog().getByRole('button', {name: 'Reset to Default'}));
    await user.click(dialog().getByRole('button', {name: 'Reset'}));

    await waitFor(() => expect(tabNames()).toEqual(['Open', 'Overdue', 'Everything']));
    expect(dialog().getByRole('button', {name: 'Reset to Default'})).toHaveAttribute('aria-disabled', 'true');
    expect(localStorage.getItem('medapp.favourites')).toBeNull();

    await user.click(dialog().getByRole('button', {name: 'Done'}));
    await waitFor(() => expect(openDialog()).toBeNull());
  });
});
