import {screen, waitFor, within} from '@testing-library/react';
import {
  adminUser,
  calls,
  expectAnnounced,
  findAlert,
  isDisabled,
  json,
  mockApi,
  openDialog,
  pickFavourite,
  renderWithApp,
  withAnnouncements,
} from '../../test/render';
import type {SetupListSearch} from '../listSearch';
import {ActivityTypesPage, colourName, freeColour} from './ActivityTypesPage';

const BASE = '/api/calendar/activity-types';

const activities = [
  {id: 'a1', name: 'Lunch', colour: 'green', isArchived: false, canBeDeleted: false},
  {id: 'a2', name: 'Training', colour: 'purple', isArchived: false, canBeDeleted: true},
  {id: 'a3', name: 'Old meeting', colour: 'red', isArchived: true, canBeDeleted: false},
];

function renderPage(search: SetupListSearch = {}, extra: Parameters<typeof mockApi>[0] = {}) {
  const fetch = mockApi({[`GET ${BASE}`]: () => json(activities), ...extra});
  const onSearchChange = vi.fn();
  return {fetch, onSearchChange, ...renderWithApp(<ActivityTypesPage search={search} onSearchChange={onSearchChange} />, {user: adminUser})};
}

const rowOf = async (name: string) => (await screen.findByText(name)).closest('tr')!;
const sent = (fetch: ReturnType<typeof mockApi>, method: string) =>
  fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === method)!;

describe('ActivityTypesPage', () => {
  it('lists the active ones by name, in their colours', async () => {
    renderPage();

    await screen.findByText('Lunch');
    const rows = screen.getAllByRole('row');
    expect(rows.slice(1).map((r) => within(r).getAllByRole('cell')[0].textContent)).toEqual(['Lunch', 'Training']);
    expect(within(await rowOf('Lunch')).getByText('Green')).toBeInTheDocument();
    expect(screen.queryByText('Old meeting')).not.toBeInTheDocument();
    expect(screen.getByText('2 active activity types')).toBeInTheDocument();
  });

  it('adds one, suggesting a colour no active one has, and shows how it will look', async () => {
    withAnnouncements();
    const {user, fetch} = renderPage({}, {[`POST ${BASE}`]: () => json({id: 'a9'}, 201)});

    await user.click(await screen.findByRole('button', {name: 'Add Activity Type'}));
    const dialog = within(openDialog()!);
    // Red is only on an archived one, so it's free.
    expect(dialog.getByRole('radio', {name: 'Red'})).toBeChecked();
    expect(isDisabled(dialog.getByRole('button', {name: 'Add Activity Type'}))).toBe(true);
    await user.type(dialog.getByRole('textbox', {name: /Name/}), 'Ward round');
    await user.click(dialog.getByRole('radio', {name: 'Teal'}));
    expect(dialog.getAllByText('Ward round')).toHaveLength(1);
    await user.click(dialog.getByRole('button', {name: 'Add Activity Type'}));

    await waitFor(() => expect(openDialog()).toBeNull());
    expect(await sent(fetch, 'POST').json()).toEqual({name: 'Ward round', colour: 'teal'});
    await expectAnnounced('Activity type added');
  });

  it("edits one, starting from what it is, and shows the API's errors on the fields", async () => {
    const {user, fetch} = renderPage(
      {},
      {[`PATCH ${BASE}/a1`]: () => json({title: 'Validation failed', status: 400, errors: {Name: ['Name is already used.']}}, 400)},
    );

    await user.click(within(await rowOf('Lunch')).getByRole('button', {name: 'Edit activity type'}));
    const dialog = within(openDialog()!);
    expect(dialog.getByRole('textbox', {name: /Name/})).toHaveValue('Lunch');
    expect(dialog.getByRole('radio', {name: 'Green'})).toBeChecked();
    await user.type(dialog.getByRole('textbox', {name: /Name/}), '!');
    await user.click(dialog.getByRole('button', {name: 'Save'}));

    expect((await dialog.findAllByText('Name is already used.')).length).toBeGreaterThan(0);
    expect(await sent(fetch, 'PATCH').json()).toEqual({name: 'Lunch!', colour: 'green'});
  });

  it('announces a saved edit', async () => {
    withAnnouncements();
    const {user} = renderPage({}, {[`PATCH ${BASE}/a2`]: () => json(activities[1])});

    await user.click(within(await rowOf('Training')).getByRole('button', {name: 'Edit activity type'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Save'}));

    await expectAnnounced('Activity type saved');
  });

  it('archives after a confirmation, and restores an archived one', async () => {
    const {user, fetch} = renderPage({status: 'any'}, {[`POST ${BASE}/a1/archive`]: () => json({}), [`POST ${BASE}/a3/restore`]: () => json({})});

    await user.click(within(await rowOf('Lunch')).getByRole('button', {name: 'Archive activity type'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Archive'}));
    await user.click(within(await rowOf('Old meeting')).getByRole('button', {name: 'Restore activity type'}));

    await waitFor(() => expect(calls(fetch)).toEqual(expect.arrayContaining([`POST ${BASE}/a1/archive`, `POST ${BASE}/a3/restore`])));
  });

  it('deletes only ones never used', async () => {
    const {user, fetch} = renderPage({}, {[`DELETE ${BASE}/a2`]: () => new Response(null, {status: 204})});

    expect(isDisabled(within(await rowOf('Lunch')).getByRole('button', {name: /can be archived but not deleted/}))).toBe(true);
    await user.click(within(await rowOf('Training')).getByRole('button', {name: 'Delete activity type'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Delete Activity Type'}));

    await waitFor(() => expect(calls(fetch)).toContain(`DELETE ${BASE}/a2`));
  });

  it('says why an archive failed, with the dialog closed', async () => {
    const {user} = renderPage({}, {[`POST ${BASE}/a1/archive`]: () => json({title: 'Someone else changed it.'}, 409)});

    await user.click(within(await rowOf('Lunch')).getByRole('button', {name: 'Archive activity type'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Archive'}));

    expect(await findAlert('Someone else changed it.')).toBeInTheDocument();
    expect(openDialog()).toBeNull();
  });

  it('says why a restore or delete failed', async () => {
    const {user} = renderPage(
      {status: 'any'},
      {[`POST ${BASE}/a3/restore`]: () => json({title: 'Down'}, 500), [`DELETE ${BASE}/a2`]: () => json({title: 'In use now.'}, 409)},
    );

    await user.click(within(await rowOf('Old meeting')).getByRole('button', {name: 'Restore activity type'}));
    expect(await findAlert('A server error occurred. Please try again later.')).toBeInTheDocument();

    await user.click(within(await rowOf('Training')).getByRole('button', {name: 'Delete activity type'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Delete Activity Type'}));
    expect(await findAlert('In use now.')).toBeInTheDocument();
  });

  it('says when an add failed for a reason other than its fields', async () => {
    const {user} = renderPage({}, {[`POST ${BASE}`]: () => json({title: 'Server error'}, 500)});

    await user.click(await screen.findByRole('button', {name: 'Add Activity Type'}));
    const dialog = within(openDialog()!);
    await user.type(dialog.getByRole('textbox', {name: /Name/}), 'Travel');
    await user.click(dialog.getByRole('button', {name: 'Add Activity Type'}));

    expect(await dialog.findByText('A server error occurred. Please try again later.')).toBeInTheDocument();
  });

  it('says when they could not be loaded', async () => {
    renderPage({}, {[`GET ${BASE}`]: () => json({title: 'Server error'}, 500)});

    expect(await findAlert('The activity types could not be loaded. Please try again.')).toBeInTheDocument();
  });

  it('offers to add the first one when there are none', async () => {
    const {user} = renderPage({}, {[`GET ${BASE}`]: () => json([])});

    await user.click(await screen.findByRole('button', {name: 'Add Your First Activity Type'}));
    expect(within(openDialog()!).getByRole('radio', {name: 'Red'})).toBeChecked();
  });

  it('offers the archived ones when the search matches no active one', async () => {
    const {user, onSearchChange} = renderPage({name: 'meeting'});

    await user.click(await screen.findByRole('button', {name: 'Show Archived'}));

    expect(onSearchChange).toHaveBeenCalledWith({name: 'meeting', page: undefined, status: 'any'});
  });
});

describe('ActivityTypesPage: its search', () => {
  it('filters from the favourites, and pages', async () => {
    const many = Array.from({length: 12}, (_, i) => ({id: `m${i}`, name: `Activity ${String(i).padStart(2, '0')}`, colour: 'blue', isArchived: false}));
    mockApi({[`GET ${BASE}`]: () => json(many)});
    const onSearchChange = vi.fn();
    const {user} = renderWithApp(<ActivityTypesPage search={{}} onSearchChange={onSearchChange} />, {user: adminUser});

    await user.click(await screen.findByRole('button', {name: 'Go to next page'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({page: 2});
    await pickFavourite(user, 'Archived');
    expect(onSearchChange).toHaveBeenLastCalledWith({name: undefined, status: 'archived', page: undefined});
  });
});

describe('ActivityTypesPage: sparse data', () => {
  it('copes with activity types without their optional fields, editing one as the first colour', async () => {
    mockApi({[`GET ${BASE}`]: () => json([{id: 'x1', colour: 'not-a-colour'}])});
    const {user} = renderWithApp(<ActivityTypesPage search={{}} onSearchChange={vi.fn()} />, {user: adminUser});

    await user.click(await screen.findByRole('button', {name: 'Edit activity type'}));

    expect(within(openDialog()!).getByRole('radio', {name: 'Red'})).toBeChecked();
    expect(within(openDialog()!).getByRole('textbox', {name: /Name/})).toHaveValue('');
  });
});

describe('colourName and freeColour', () => {
  it('names the palette colours as people say them', () => {
    expect(colourName('gray')).toBe('Grey');
    expect(colourName('teal')).toBe('Teal');
    expect(colourName(null)).toBe('');
  });

  it('picks the first colour no active type has, or the first when all are taken', () => {
    expect(freeColour([{colour: 'red'}, {colour: 'orange', isArchived: true}])).toBe('orange');
    const all = ['red', 'orange', 'yellow', 'green', 'teal', 'cyan', 'blue', 'purple', 'pink', 'gray'].map((colour) => ({colour}));
    expect(freeColour(all)).toBe('red');
  });
});
