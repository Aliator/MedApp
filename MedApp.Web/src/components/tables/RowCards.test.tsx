import type {TableColumn} from '@astryxdesign/core/Table';
import {screen, waitFor, within} from '@testing-library/react';
import {givenWidth} from '../../test/layout';
import {renderWithApp} from '../../test/render';
import {AppIcon} from '../shared/icons';
import {RowCards} from './RowCards';
import {AvatarCell} from './AvatarCell';
import {TableActions} from './TableActions';

type Row = {id: string; name: string; status: string};

const columns: TableColumn<Row>[] = [
  {key: 'name', header: 'Pathway', sortable: true, renderCell: (r) => r.name},
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
    expect(first.queryByText('Pathway')).not.toBeInTheDocument();
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

describe('RowCards sorting', () => {
  /** The sort menu, opened from its button. */
  async function openSort(user: ReturnType<typeof renderWithApp>['user'], name: string | RegExp) {
    const sort = await screen.findByRole('button', {name});
    await user.click(sort);
    return within(await waitFor(() => document.querySelector<HTMLElement>(`[aria-labelledby="${CSS.escape(sort.id)}"]`)!));
  }

  it('says when unsorted, and sorts a column ascending first', async () => {
    const onSortChange = vi.fn();
    const {user} = renderWithApp(<RowCards data={rows} columns={columns} idKey="id" skeletonRows={3} sort={null} onSortChange={onSortChange} />);

    const menu = await openSort(user, 'Sort');
    await user.click(menu.getByRole('menuitem', {name: /^Pathway/, hidden: true}));

    expect(onSortChange).toHaveBeenLastCalledWith({column: 'name', ascending: true});
  });

  it('shows a descending sort, which turns round to ascending, and starts another column ascending', async () => {
    const onSortChange = vi.fn();
    const {user} = renderWithApp(
      <RowCards data={rows} columns={columns} idKey="id" skeletonRows={3} sort={{column: 'status', ascending: false}} onSortChange={onSortChange} />,
    );

    const menu = await openSort(user, 'Sort by Status, descending');
    expect(menu.getByRole('menuitem', {name: /^Status/, hidden: true})).toHaveTextContent('Descending (choose again for ascending)');
    await user.click(menu.getByRole('menuitem', {name: /^Status/, hidden: true}));
    expect(onSortChange).toHaveBeenLastCalledWith({column: 'status', ascending: true});

    const again = await openSort(user, 'Sort by Status, descending');
    await user.click(again.getByRole('menuitem', {name: /^Pathway/, hidden: true}));
    expect(onSortChange).toHaveBeenLastCalledWith({column: 'name', ascending: true});
  });

  it('names a column by its key when its header is not text, and has no menu without sortable columns', async () => {
    const plain: TableColumn<Row>[] = [
      {key: 'name', header: <strong>Pathway</strong>, sortable: true, renderCell: (r) => r.name},
      {key: 'status', header: <strong>Status</strong>, renderCell: (r) => r.status},
    ];
    const {unmount} = renderWithApp(
      <RowCards data={rows} columns={plain} idKey="id" skeletonRows={3} sort={{column: 'name', ascending: true}} onSortChange={() => {}} />,
    );
    expect(await screen.findByRole('button', {name: 'Sort by name, ascending'})).toBeInTheDocument();
    expect(within(screen.getAllByRole('listitem')[0]).getByText('status')).toBeInTheDocument();
    unmount();

    renderWithApp(<RowCards data={rows} columns={columns.map((c) => ({...c, sortable: false}))} idKey="id" skeletonRows={3} onSortChange={() => {}} />);
    await screen.findAllByRole('listitem');
    expect(screen.queryByRole('button', {name: /^Sort/})).not.toBeInTheDocument();
  });

  it('shows just a title when a table has one column', async () => {
    renderWithApp(<RowCards data={rows} columns={columns.slice(0, 1)} idKey="id" skeletonRows={3} />);

    const [card] = await screen.findAllByRole('listitem');
    expect(card).toHaveTextContent('Biopsy');
    expect(within(card).queryByText('Status')).not.toBeInTheDocument();
  });
});

describe('RowCards in a narrow card (a phone)', () => {
  afterEach(() => vi.unstubAllGlobals());

  const withActions = (count: number): TableColumn<Row>[] => [
    columns[0],
    {
      key: 'actions',
      header: 'Actions',
      renderCell: (r) => (
        <TableActions
          actions={['Edit', 'Archive', 'Delete'].slice(0, count).map((label) => ({label: `${label} ${r.name}`, icon: AppIcon.Edit, onClick: () => {}}))}
        />
      ),
    },
  ];

  it('puts the title first and its actions under it, more than two of them in one Actions menu', async () => {
    givenWidth(358);
    const {user} = renderWithApp(<RowCards data={rows} columns={withActions(3)} idKey="id" skeletonRows={3} />);

    const card = within((await screen.findAllByRole('listitem'))[0]);
    const menu = card.getByRole('button', {name: 'Actions'});
    expect(card.getByText('Biopsy').compareDocumentPosition(menu) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(card.queryByRole('button', {name: 'Edit Biopsy'})).not.toBeInTheDocument();
    await user.click(menu);
    expect(await screen.findByRole('menuitem', {name: 'Delete Biopsy'})).toBeInTheDocument();
  });

  it("lets a person's name and details shrink beside their avatar, so they're cut short rather than widening the card", async () => {
    const avatar: TableColumn<Row> = {key: 'name', header: 'User', renderCell: (r) => <AvatarCell primaryText={r.name} secondaryText="Synthetic Person, a long job title" />};
    givenWidth(358);
    const {unmount} = renderWithApp(<RowCards data={rows} columns={[avatar]} idKey="id" skeletonRows={3} />);
    const textBox = async () => (await screen.findByText('Biopsy')).parentElement!.parentElement!;
    expect(await textBox()).toHaveAttribute('data-size', 'fill');
    unmount();

    givenWidth(700);
    renderWithApp(<RowCards data={rows} columns={[avatar]} idKey="id" skeletonRows={3} />);
    expect(await textBox()).not.toHaveAttribute('data-size', 'fill');
  });

  it('keeps two actions as buttons', async () => {
    givenWidth(358);
    renderWithApp(<RowCards data={rows} columns={withActions(2)} idKey="id" skeletonRows={3} />);

    const card = within((await screen.findAllByRole('listitem'))[0]);
    expect(card.getByRole('button', {name: 'Archive Biopsy'})).toBeInTheDocument();
    expect(card.queryByRole('button', {name: 'Actions'})).not.toBeInTheDocument();
  });

  it('keeps every action as a button beside the title in a wider card', async () => {
    givenWidth(700);
    renderWithApp(<RowCards data={rows} columns={withActions(3)} idKey="id" skeletonRows={3} />);

    const card = within((await screen.findAllByRole('listitem'))[0]);
    expect(card.getByRole('button', {name: 'Delete Biopsy'})).toBeInTheDocument();
    expect(card.queryByRole('button', {name: 'Actions'})).not.toBeInTheDocument();
  });
});
