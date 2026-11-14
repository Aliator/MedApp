import {EmptyState} from '@astryxdesign/core/EmptyState';
import {screen, within} from '@testing-library/react';
import {renderWithApp} from '../../test/render';
import {DataTable, type DataTableColumn} from './DataTable';

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
