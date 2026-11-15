import type {TableColumn} from '@astryxdesign/core/Table';
import {screen, waitFor, within} from '@testing-library/react';
import {renderWithApp} from '../../test/render';
import {RowCards} from './RowCards';

type Row = {id: string; name: string; status: string};

const columns: TableColumn<Row>[] = [
  {key: 'name', header: 'Procedure', sortable: true, renderCell: (r) => r.name},
  {key: 'status', header: 'Status', sortable: true, renderCell: (r) => r.status},
  {key: 'actions', header: 'Actions', renderCell: (r) => <button type="button">View {r.name}</button>},
];
const rows: Row[] = [
  {id: '1', name: 'Biopsy', status: 'In progress'},
  {id: '2', name: 'Colonoscopy', status: 'Blocked'},
];

describe('RowCards', () => {
  it('shows each row as a card: its title and actions, then the other columns by name', async () => {
    renderWithApp(<RowCards data={rows} columns={columns} idKey="id" skeletonRows={3} />);

    const cards = await screen.findAllByRole('listitem');
    expect(cards).toHaveLength(2);
    const first = within(cards[0]);
    expect(first.getByText('Biopsy')).toBeInTheDocument();
    expect(first.getByRole('button', {name: 'View Biopsy'})).toBeInTheDocument();
    expect(first.getByText('Status')).toBeInTheDocument();
    expect(first.getByText('In progress')).toBeInTheDocument();
    // Its title and actions aren't repeated as labelled values.
    expect(first.queryByText('Procedure')).not.toBeInTheDocument();
  });

  it('sorts from a menu, turning the sorted column round when chosen again', async () => {
    const onSortChange = vi.fn();
    const {user} = renderWithApp(
      <RowCards data={rows} columns={columns} idKey="id" skeletonRows={3} sort={{column: 'status', ascending: true}} onSortChange={onSortChange} />,
    );

    const sort = await screen.findByRole('button', {name: 'Sort by Status, ascending'});
    await user.click(sort);
    const menu = await waitFor(() => document.querySelector<HTMLElement>(`[aria-labelledby="${CSS.escape(sort.id)}"]`)!);
    await user.click(within(menu).getByRole('menuitem', {name: /^Status/, hidden: true}));
    expect(onSortChange).toHaveBeenLastCalledWith({column: 'status', ascending: false});
  });

  it('shows the empty state when there are no rows, and placeholders while loading', async () => {
    const {unmount} = renderWithApp(<RowCards data={[]} columns={columns} idKey="id" skeletonRows={3} emptyState={<p>Nothing here</p>} />);
    expect(await screen.findByText('Nothing here')).toBeInTheDocument();
    unmount();

    renderWithApp(<RowCards data={[]} columns={columns} idKey="id" skeletonRows={3} isLoading />);
    await waitFor(() => expect(document.querySelector('[aria-busy="true"]')).not.toBeNull());
  });
});
