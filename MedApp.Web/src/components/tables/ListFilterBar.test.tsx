import {within} from '@testing-library/react';
import {renderWithApp, pickFavourite, searchBar} from '../../test/render';
import {dayFromToday, filtersFrom, ListFilterBar, valuesFrom, type ListFilterField} from './ListFilterBar';

const fields: ListFilterField[] = [
  {kind: 'text', key: 'title', label: 'Procedure'},
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

    await vi.waitFor(() => expect(searchBar()).toHaveTextContent(/Procedure: contains\s*knee/));
    expect(searchBar()).toHaveTextContent(/Overdue: only/);
  });

  it('marks its text box as not for autofill, so a saved login is never filled in', async () => {
    renderWithApp(<ListFilterBar list="test" fields={fields} values={{}} onChange={() => {}} label="Search users" placeholder="Search..." />);

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

  it("sets a favourite's filters, clearing the others", async () => {
    const onChange = vi.fn();
    const {user} = renderWithApp(
      <ListFilterBar
        list="test"
        fields={fields}
        values={{title: 'knee'}}
        onChange={onChange}
        label="Search"
        placeholder="Search..."
        favourites={[{label: 'Blocked', values: {status: 'Blocked'}}]}
      />,
    );

    await pickFavourite(user, 'Blocked');

    expect(onChange).toHaveBeenCalledWith({title: undefined, status: 'Blocked', dueFrom: undefined, dueTo: undefined, overdue: undefined});
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
    renderWithApp(<ListFilterBar list="test" fields={fields} values={{}} onChange={() => {}} label="Search users" placeholder="Search..." />);

    await vi.waitFor(() => expect(searchBar().querySelector('input')).toHaveAttribute('data-1p-ignore', 'true'));
  });
});
