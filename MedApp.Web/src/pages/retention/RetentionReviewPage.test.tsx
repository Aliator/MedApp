import {screen, waitFor, within} from '@testing-library/react';
import {adminUser, calls, findAlert, json, mockApi, openDialog, pageOf, renderWithApp} from '../../test/render';
import type {RetentionListSearch} from '../listSearch';
import {RetentionReviewPage} from './RetentionReviewPage';

const SEARCH = '/api/patients/retention-review';
const adult = 'f184a7b8-2585-4437-bcd4-e1700be38301';
const child = 'f184a7b8-2585-4437-bcd4-e1700be38302';

const items = [
  {
    patientId: adult,
    firstName: 'Tamsin',
    lastName: 'Rowcroft',
    dateOfBirth: '1940-02-02',
    conclusionOfTreatment: '2016-05-01',
    retentionClass: 'Adult',
    dueOn: '2024-05-01',
    keptUntil: null,
    keptReason: null,
  },
  {
    patientId: child,
    firstName: 'Bryn',
    lastName: 'Ottery',
    dateOfBirth: '2000-01-10',
    conclusionOfTreatment: '2010-01-01',
    retentionClass: 'Child',
    dueOn: '2025-01-10',
    keptUntil: '2027-01-01',
    keptReason: 'Synthetic legal hold',
  },
];

function renderList(search: RetentionListSearch = {}, body: unknown = pageOf(items, 12), status = 200) {
  const fetch = mockApi({[`GET ${SEARCH}`]: () => json(body, status)});
  const onSearchChange = vi.fn();
  return {fetch, onSearchChange, ...renderWithApp(<RetentionReviewPage search={search} onSearchChange={onSearchChange} />, {user: adminUser})};
}

describe('RetentionReviewPage', () => {
  it('lists the records past their retention period: who, the class, when care ended, when due, and any decision to keep', async () => {
    const {fetch} = renderList();

    const row = (await screen.findByRole('link', {name: 'Tamsin Rowcroft'})).closest('tr')!;
    expect(screen.getByRole('link', {name: 'Tamsin Rowcroft'})).toHaveAttribute('href', `/patients/${adult}`);
    expect(within(row).getByText('Born 2 February 1940')).toBeInTheDocument();
    expect(within(row).getByText('Adult')).toBeInTheDocument();
    expect(within(row).getByText('1 May 2016')).toBeInTheDocument();
    expect(within(row).getByText('1 May 2024')).toBeInTheDocument();
    expect(within(row).getByRole('link', {name: 'Review'})).toHaveAttribute('href', `/patients/${adult}`);
    const kept = screen.getByRole('link', {name: 'Bryn Ottery'}).closest('tr')!;
    expect(within(kept).getByText('Child')).toBeInTheDocument();
    expect(within(kept).getByText('Kept until 1 January 2027: Synthetic legal hold')).toBeInTheDocument();
    expect(screen.getByText('12 records past their retention period')).toBeInTheDocument();
    expect(calls(fetch)).toEqual([`GET ${SEARCH}?PageSize=10&Page=1`]);
  });

  it('lists records being kept too, from its switch, and asks for them', async () => {
    const {user, fetch, onSearchChange} = renderList({kept: true});

    await screen.findByRole('link', {name: 'Tamsin Rowcroft'});
    expect(calls(fetch)).toEqual([`GET ${SEARCH}?PageSize=10&Page=1&IncludeKept=true`]);
    await user.click(screen.getByRole('switch', {name: 'Include records being kept'}));

    expect(onSearchChange).toHaveBeenLastCalledWith({kept: undefined});
  });

  it('turns the switch on, starting from the first page', async () => {
    const {user, onSearchChange} = renderList({page: 2});

    await screen.findByRole('link', {name: 'Tamsin Rowcroft'});
    await user.click(screen.getByRole('switch', {name: 'Include records being kept'}));

    expect(onSearchChange).toHaveBeenLastCalledWith({kept: true});
  });

  it('keeps a record, from its dialog', async () => {
    const {user} = renderList();

    const row = (await screen.findByRole('link', {name: 'Tamsin Rowcroft'})).closest('tr')!;
    await user.click(within(row).getByRole('button', {name: 'Keep'}));
    const dialog = await waitFor(() => openDialog()!);
    expect(within(dialog).getByRole('combobox', {name: /review again on/i})).toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', {name: 'Cancel'}));

    await waitFor(() => expect(openDialog()).toBeNull());
  });

  it('anonymises a record, from its dialog', async () => {
    const {user} = renderList();

    const row = (await screen.findByRole('link', {name: 'Bryn Ottery'})).closest('tr')!;
    await user.click(within(row).getByRole('button', {name: 'Anonymise'}));
    expect(await screen.findByRole('textbox', {name: /type bryn ottery to confirm/i})).toBeInTheDocument();
    await user.click(within(openDialog()!).getByRole('button', {name: 'Cancel'}));

    await waitFor(() => expect(openDialog()).toBeNull());
  });

  it('closes each dialog once its decision is made, and the list is asked for again', async () => {
    const fetch = mockApi({
      [`GET ${SEARCH}`]: () => json(pageOf(items, 12)),
      [`POST /api/patients/${adult}/retention/keep`]: () => new Response(null, {status: 204}),
      [`POST /api/patients/${child}/anonymise`]: () => json({id: child, isAnonymised: true}),
    });
    const {user} = renderWithApp(<RetentionReviewPage search={{}} onSearchChange={() => {}} />, {user: adminUser});

    const adultRow = (await screen.findByRole('link', {name: 'Tamsin Rowcroft'})).closest('tr')!;
    await user.click(within(adultRow).getByRole('button', {name: 'Keep'}));
    let dialog = await waitFor(() => openDialog()!);
    await user.type(within(dialog).getByRole('textbox', {name: /reason/i}), 'Synthetic legal hold');
    await user.click(within(dialog).getByRole('button', {name: 'Keep Record'}));
    await waitFor(() => expect(openDialog()).toBeNull());

    const childRow = screen.getByRole('link', {name: 'Bryn Ottery'}).closest('tr')!;
    await user.click(within(childRow).getByRole('button', {name: 'Anonymise'}));
    dialog = await waitFor(() => openDialog()!);
    await user.type(within(dialog).getByRole('textbox', {name: /reason/i}), 'Past retention, reviewed');
    await user.type(within(dialog).getByRole('textbox', {name: /to confirm/i}), 'Bryn Ottery');
    await user.click(within(dialog).getByRole('button', {name: 'Anonymise Record'}));
    await waitFor(() => expect(openDialog()).toBeNull());

    expect(calls(fetch).filter((c) => c.startsWith(`GET ${SEARCH}`)).length).toBeGreaterThan(1);
  });

  it('pages, keeping the first page out of the URL', async () => {
    const {user, onSearchChange} = renderList({page: 2, kept: true});

    await screen.findByRole('link', {name: 'Tamsin Rowcroft'});
    await user.click(screen.getByRole('button', {name: 'Go to page 1'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({kept: true, page: undefined});
  });

  it('says when no records are due', async () => {
    renderList({}, pageOf([], 0));

    expect((await screen.findAllByText('No records are due for review')).length).toBeGreaterThan(0);
    expect(screen.getByText('0 records past their retention period')).toBeInTheDocument();
  });

  it('says when the list could not be loaded', async () => {
    renderList({}, {title: 'Server error'}, 500);

    expect(await findAlert('The records due for review could not be loaded. Please try again.')).toBeInTheDocument();
  });
});
