import {EmptyState} from '@astryxdesign/core/EmptyState';
import {DEFAULT_MIN_COLUMN_WIDTH, pixel, proportional} from '@astryxdesign/core/Table';
import {act, screen, waitFor, within} from '@testing-library/react';
import {renderWithApp} from '../../test/render';
import {DataTable, MIN_ROW_HEIGHT, type DataTableColumn} from './DataTable';

interface PatientRow extends Record<string, unknown> {
  id: string;
  name: string;
  email: string;
}

const rows: PatientRow[] = [
  {id: 'p1', name: 'Freya Ahmed', email: 'freya.ahmed18@example.com'},
  {id: 'p2', name: 'Priya Ali', email: 'priya.ali84@example.com'},
];

const columns: DataTableColumn<PatientRow>[] = [
  {key: 'name', header: 'Patients', sortable: true},
  {key: 'email', header: 'Email'},
];

describe('DataTable', () => {
  it('shows a header and one row per item', async () => {
    renderWithApp(<DataTable data={rows} columns={columns} idKey="id" />);

    const table = await screen.findByRole('table');
    expect(within(table).getByRole('columnheader', {name: /patients/i})).toBeInTheDocument();
    expect(within(table).getByText('Freya Ahmed')).toBeInTheDocument();
    expect(within(table).getByText('priya.ali84@example.com')).toBeInTheDocument();
  });

  it('shows the empty state when there are no rows', async () => {
    renderWithApp(<DataTable data={[]} columns={columns} idKey="id" emptyState={<EmptyState title="No patients found" />} />);

    expect(await screen.findByText('No patients found')).toBeInTheDocument();
  });

  it('shows skeleton rows, marked busy, while loading', async () => {
    renderWithApp(<DataTable data={rows} columns={columns} idKey="id" isLoading skeletonRows={3} />);

    const table = await screen.findByRole('table');
    expect(table).toHaveAttribute('aria-busy', 'true');
    expect(within(table).queryByText('Freya Ahmed')).not.toBeInTheDocument();
    expect(within(table).getAllByRole('row')).toHaveLength(4); // header + 3 skeleton rows
  });

  it('marks the sorted column and reports header clicks as the next sort', async () => {
    const onSortChange = vi.fn();
    const {user} = renderWithApp(
      <DataTable data={rows} columns={columns} idKey="id" sort={{column: 'name', ascending: true}} onSortChange={onSortChange} />,
    );

    const header = await screen.findByRole('columnheader', {name: /patients/i});
    expect(header).toHaveAttribute('aria-sort', 'ascending');

    await user.click(within(header).getByRole('button'));

    expect(onSortChange).toHaveBeenCalledWith({column: 'name', ascending: false});
  });

  it('goes from a descending sort back to ascending, never to unsorted', async () => {
    const onSortChange = vi.fn();
    const {user} = renderWithApp(
      <DataTable data={rows} columns={columns} idKey="id" sort={{column: 'name', ascending: false}} onSortChange={onSortChange} />,
    );

    const header = await screen.findByRole('columnheader', {name: /patients/i});
    await user.click(within(header).getByRole('button'));

    expect(onSortChange).toHaveBeenCalledWith({column: 'name', ascending: true});
  });

  it('shows page controls for server-side paging and reports page changes', async () => {
    const onPageChange = vi.fn();
    const {user} = renderWithApp(
      <DataTable data={rows} columns={columns} idKey="id" pagination={{page: 1, pageSize: 2, totalItems: 5, onPageChange}} />,
    );

    const nav = await screen.findByRole('navigation', {name: /table pagination/i});
    expect(within(nav).getByRole('button', {name: /go to page 3/i})).toBeInTheDocument();

    await user.click(within(nav).getByRole('button', {name: /go to next page/i}));

    expect(onPageChange).toHaveBeenCalledWith(2);
  });

  it('numbers rows across pages for screen readers', async () => {
    renderWithApp(
      <DataTable data={rows} columns={columns} idKey="id" pagination={{page: 3, pageSize: 2, totalItems: 6, onPageChange: () => {}}} />,
    );

    const table = await screen.findByRole('table');
    expect(table).toHaveAttribute('aria-rowcount');
    expect(within(table).getByText('Freya Ahmed').closest('tr')).toHaveAttribute('aria-rowindex');
  });

  it('has no page controls without pagination, or when nothing matches', async () => {
    const {unmount} = renderWithApp(<DataTable data={rows} columns={columns} idKey="id" />);
    await screen.findByRole('table');
    expect(screen.queryByRole('navigation', {name: /table pagination/i})).not.toBeInTheDocument();
    unmount();

    renderWithApp(
      <DataTable data={[]} columns={columns} idKey="id" pagination={{page: 1, pageSize: 10, totalItems: 0, onPageChange: () => {}}} />,
    );
    await screen.findByRole('table');
    expect(screen.queryByRole('navigation', {name: /table pagination/i})).not.toBeInTheDocument();
  });

  it('leaves headers plain without a sort handler', async () => {
    renderWithApp(<DataTable data={rows} columns={columns} idKey="id" />);

    const header = await screen.findByRole('columnheader', {name: /patients/i});
    expect(within(header).queryByRole('button')).not.toBeInTheDocument();
  });
});

/** jsdom has no layout: gives every element this width and height, until the test ends. */
function stubSize(size: {width?: number; height?: number}) {
  const box = {width: size.width ?? 0, height: size.height ?? 0};
  vi.spyOn(HTMLElement.prototype, 'clientWidth', 'get').mockImplementation(() => box.width);
  vi.spyOn(HTMLElement.prototype, 'clientHeight', 'get').mockImplementation(() => box.height);
  return box;
}

/** A stand-in ResizeObserver: `resize()` tells every observer its element changed size. */
function stubResizeObserver() {
  const observers: {callback: ResizeObserverCallback; disconnect: ReturnType<typeof vi.fn>}[] = [];
  vi.stubGlobal(
    'ResizeObserver',
    class {
      disconnect = vi.fn();
      constructor(callback: ResizeObserverCallback) {
        observers.push({callback, disconnect: this.disconnect});
      }
      observe() {}
      unobserve() {}
    },
  );
  return {observers, resize: () => act(() => observers.forEach((o) => o.callback([], {} as ResizeObserver)))};
}

const widthOf = (name: string) => screen.getByRole('columnheader', {name}).style.width;
const rowHeightOf = (container: HTMLElement) =>
  container.querySelector<HTMLElement>('[style*="--medapp-table-row-height"]')?.style.getPropertyValue('--medapp-table-row-height');
const paged = (totalItems: number, page = 1, pageSize = 10) => ({page, pageSize, totalItems, onPageChange: () => {}});

describe('DataTable sizing', () => {
  it('gives each column its narrowest width and an equal share of the room left', async () => {
    stubSize({width: 1000});
    const sized: DataTableColumn<PatientRow>[] = [
      {key: 'name', header: 'Patients', width: pixel(100)},
      {key: 'email', header: 'Email'},
      {key: 'id', header: 'Id', width: proportional(1, {minWidth: 90})},
      {key: 'notes', header: 'Notes', width: proportional(1)},
    ];
    renderWithApp(<DataTable data={rows} columns={sized} idKey="id" />);

    await screen.findByRole('table');
    const share = Math.floor((1000 - (100 + 60 + 90 + DEFAULT_MIN_COLUMN_WIDTH)) / 4);
    expect(widthOf('Patients')).toBe(`${100 + share}px`);
    expect(widthOf('Email')).toBe(`${60 + share}px`);
    expect(widthOf('Id')).toBe(`${90 + share}px`);
    expect(widthOf('Notes')).toBe(`${DEFAULT_MIN_COLUMN_WIDTH + share}px`);
  });

  it('shows each row as a card when too narrow for its columns, or for any table', async () => {
    const box = stubSize({width: 600});
    const wide = ['a', 'b', 'c', 'd', 'e', 'f', 'g'].map((key) => ({key, header: key.toUpperCase(), width: pixel(100)}));
    const {unmount} = renderWithApp(<DataTable data={rows} columns={[...columns, ...wide]} idKey="id" />);
    expect(await screen.findAllByRole('listitem')).toHaveLength(2);
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
    unmount();

    box.width = 400;
    renderWithApp(<DataTable data={rows} columns={columns} idKey="id" sort={{column: 'name', ascending: true}} onSortChange={() => {}} />);
    expect(await screen.findAllByRole('listitem')).toHaveLength(2);
    // Sorting moves to a menu above the cards.
    expect(screen.getByRole('button', {name: 'Sort by Patients, ascending'})).toBeInTheDocument();
  });

  it('shows a whole page of placeholder cards while loading, when narrow', async () => {
    stubSize({width: 400});
    renderWithApp(<DataTable data={[]} columns={columns} idKey="id" isLoading pagination={paged(0, 1, 4)} />);

    await waitFor(() => expect(document.querySelector('[aria-busy="true"]')!.children).toHaveLength(4));
  });

  it('shares the height equally between a page of rows, never making them shorter than two lines', async () => {
    const box = stubSize({height: 1000});
    const {container, unmount} = renderWithApp(<DataTable data={rows} columns={columns} idKey="id" pagination={paged(2)} />);
    await screen.findByRole('table');
    await waitFor(() => expect(rowHeightOf(container)).toBe('100px'));
    unmount();

    box.height = 300;
    const short = renderWithApp(<DataTable data={rows} columns={columns} idKey="id" pagination={paged(2)} />);
    await screen.findByRole('table');
    await waitFor(() => expect(rowHeightOf(short.container)).toBe(`${MIN_ROW_HEIGHT}px`));
  });

  it('measures again as its space changes size, and stops when it goes', async () => {
    const box = stubSize({width: 1000, height: 1000});
    const {resize, observers} = stubResizeObserver();
    const {unmount} = renderWithApp(<DataTable data={rows} columns={columns} idKey="id" pagination={paged(2)} />);
    await screen.findByRole('table');

    box.width = 300;
    resize();

    expect(await screen.findAllByRole('listitem')).toHaveLength(2);
    unmount();
    expect(observers.length).toBeGreaterThan(0);
    expect(observers.every((o) => o.disconnect.mock.calls.length > 0)).toBe(true);
  });
});

describe('DataTable surroundings', () => {
  it('shows content above the table, a search above the card, and a panel at either side', async () => {
    const {unmount} = renderWithApp(
      <DataTable
        data={rows}
        columns={columns}
        idKey="id"
        top={<p>Test list switch</p>}
        search={<input aria-label="Search the list" />}
        edge={{side: 'start', content: <button type="button">Test edge</button>}}
      />,
    );
    expect(await screen.findByText('Test list switch')).toBeInTheDocument();
    expect(screen.getByRole('textbox', {name: 'Search the list'})).toBeInTheDocument();
    // At the start: before the table.
    expect(screen.getByRole('button', {name: 'Test edge'}).compareDocumentPosition(screen.getByRole('table')) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    unmount();

    renderWithApp(<DataTable data={rows} columns={columns} idKey="id" edge={{side: 'end', content: <button type="button">Test edge</button>}} />);
    const end = await screen.findByRole('button', {name: 'Test edge'});
    expect(end.compareDocumentPosition(screen.getByRole('table')) & Node.DOCUMENT_POSITION_PRECEDING).toBeTruthy();
  });

  it('tells screen readers what a paged list found', async () => {
    vi.useFakeTimers({shouldAdvanceTime: true});
    localStorage.setItem('medapp.accessibility', JSON.stringify({announcements: true}));
    const said = async (totalItems: number, page = 1) => {
      const {unmount} = renderWithApp(<DataTable data={totalItems ? rows : []} columns={columns} idKey="id" pagination={paged(totalItems, page)} />);
      await act(() => vi.advanceTimersByTimeAsync(200));
      const message = screen.getByTestId('announcer').textContent;
      unmount();
      return message;
    };

    expect(await said(0)).toBe('No results');
    expect(await said(1)).toBe('1 result');
    expect(await said(25, 2)).toBe('25 results, page 2 of 3');
  });
});
