import {screen, within} from '@testing-library/react';
import {adminUser, calls, json, mockApi, pageOf, pickFavourite, renderWithApp, findAlert, searchBar} from '../../test/render';
import type {PatientsListSearch} from '../listSearch';
import {PatientsListPage} from './PatientsListPage';

const freya = {id: 'f184a7b8-2585-4437-bcd4-e1700be38287', firstName: 'Freya', lastName: 'Ahmed', dateOfBirth: '1976-12-01', email: 'freya.ahmed18@example.com'};
const priya = {id: '7499c758-0000-4000-8000-000000000002', firstName: 'Priya', lastName: 'Ali', dateOfBirth: '1966-12-29', email: 'priya.ali84@example.com'};

function renderList(search: PatientsListSearch = {}, body = pageOf([freya, priya], 60)) {
  const fetch = mockApi({'GET /api/patients/search': () => json(body)});
  const onSearchChange = vi.fn();
  const result = renderWithApp(<PatientsListPage search={search} onSearchChange={onSearchChange} />, {
    path: '/patients',
    user: adminUser,
  });
  return {...result, fetch, onSearchChange};
}

describe('PatientsListPage', () => {
  it('shows a page of patients with links to view and edit them', async () => {
    renderList();

    expect(await screen.findByText('Freya Ahmed')).toBeInTheDocument();
    expect(screen.getByText('60 patients registered')).toBeInTheDocument();
    expect(screen.getByText('1 Dec 1976')).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'freya.ahmed18@example.com'})).toHaveAttribute('href', 'mailto:freya.ahmed18@example.com');
    expect(screen.getAllByRole('link', {name: 'View patient'})[0]).toHaveAttribute('href', `/patients/${freya.id}`);
    expect(screen.getAllByRole('link', {name: 'Edit patient'})[0]).toHaveAttribute('href', `/patients/${freya.id}/edit`);
    expect(screen.getByRole('link', {name: 'Add Patient'})).toHaveAttribute('href', '/patients/add');
  });

  it('asks the API for the page, sort and filters in the URL', async () => {
    const {fetch} = renderList({page: 2, sort: 'DateOfBirth', asc: false, last: 'Ahmed', dobFrom: '1970-01-01', dobTo: '1979-12-31'});

    await screen.findByText('Freya Ahmed');
    expect(calls(fetch)).toEqual([
      'GET /api/patients/search?PageSize=10&Page=2&Sort=dateOfBirth&Ascending=false&LastName=Ahmed&DateOfBirthFrom=1970-01-01&DateOfBirthTo=1979-12-31',
    ]);
    expect(screen.getByText('60 matching patients')).toBeInTheDocument();
  });

  it('changes page, and goes back to page 1 when the sort changes', async () => {
    const {user, onSearchChange} = renderList({last: 'Ahmed'});

    await user.click(await screen.findByRole('button', {name: /go to next page/i}));
    expect(onSearchChange).toHaveBeenLastCalledWith({last: 'Ahmed', page: 2});

    const header = screen.getByRole('columnheader', {name: /email/i});
    await user.click(within(header).getByRole('button'));
    expect(onSearchChange).toHaveBeenLastCalledWith({last: 'Ahmed', page: undefined, sort: 'Email', asc: true});
  });

  it('shows the search from the URL as tokens, and a favourite sets its filter, keeping the sort and starting on page 1', async () => {
    const {user, onSearchChange} = renderList({page: 3, sort: 'Email', last: 'Bennett', dobFrom: '1960-01-01'});

    await screen.findByText('Freya Ahmed');
    expect(searchBar()).toHaveTextContent(/Last name: contains\s*Bennett/);
    expect(searchBar()).toHaveTextContent(/Date of birth: is on or after\s*1 Jan 1960/);

    await pickFavourite(user, '65 and over');
    expect(onSearchChange).toHaveBeenLastCalledWith(
      expect.objectContaining({sort: 'Email', page: undefined, last: undefined, dobFrom: undefined, dobTo: expect.stringMatching(/^\d{4}-\d{2}-\d{2}$/)}),
    );
  });

  it('clears the search from the bar, keeping the sort', async () => {
    const {user, onSearchChange} = renderList({first: 'Freya', sort: 'Email'});

    await screen.findByText('Freya Ahmed');
    await user.click(within(searchBar()).getByRole('button', {name: /clear/i}));

    expect(onSearchChange).toHaveBeenLastCalledWith(expect.objectContaining({sort: 'Email', first: undefined, page: undefined}));
  });

  it('invites adding the first patient when there are none', async () => {
    renderList({}, pageOf([], 0));

    expect(await screen.findByText('No patients yet')).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Add Your First Patient'})).toHaveAttribute('href', '/patients/add');
  });

  it('says when a search matches nothing, and clears it keeping the sort', async () => {
    const {user, onSearchChange} = renderList({last: 'Nobody', sort: 'Email', asc: false, page: 2}, pageOf([], 0));

    expect((await screen.findAllByText('No patients match your search')).length).toBeGreaterThan(0);
    await user.click(screen.getByRole('button', {name: 'Clear Search'}));

    expect(onSearchChange).toHaveBeenLastCalledWith({sort: 'Email', asc: false});
  });

  it('counts one registered patient', async () => {
    renderList({}, pageOf([freya], 1));
    expect(await screen.findByText('1 patient registered')).toBeInTheDocument();
  });

  it('counts one matching patient', async () => {
    renderList({last: 'Ahmed'}, pageOf([freya], 1));
    expect(await screen.findByText('1 matching patient')).toBeInTheDocument();
  });

  it('keeps the first page out of the URL', async () => {
    const {user, onSearchChange} = renderList({page: 2});

    await user.click(await screen.findByRole('button', {name: 'Go to page 1'}));

    expect(onSearchChange).toHaveBeenLastCalledWith({page: undefined});
  });

  it('shows an error when the patients cannot be loaded', async () => {
    mockApi({'GET /api/patients/search': () => json({}, 500)});
    renderWithApp(<PatientsListPage search={{}} onSearchChange={() => {}} />, {user: adminUser});

    await findAlert('The patients could not be loaded');
  });
});
