import {screen, within} from '@testing-library/react';
import {adminUser, calls, json, mockApi, openDialog, pageOf, renderWithApp, findAlert} from '../../test/render';
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

  it('searches from the dialog, keeping the sort and starting on page 1', async () => {
    const {user, onSearchChange} = renderList({page: 3, sort: 'Email'});

    await user.click(await screen.findByRole('button', {name: 'Search'}));
    await user.type(within(openDialog()!).getByLabelText(/^last name/i), 'Bennett');
    await user.click(within(openDialog()!).getByRole('button', {name: 'Search'}));

    expect(onSearchChange).toHaveBeenLastCalledWith({
      sort: 'Email',
      asc: undefined,
      first: undefined,
      last: 'Bennett',
      email: undefined,
      dobFrom: undefined,
      dobTo: undefined,
    });
  });

  it('clears an active search', async () => {
    const {user, onSearchChange} = renderList({first: 'Freya', sort: 'Email'});

    await user.click(await screen.findByRole('button', {name: 'Clear Search'}));

    expect(onSearchChange).toHaveBeenLastCalledWith({sort: 'Email', asc: undefined});
  });

  it('invites adding the first patient when there are none', async () => {
    renderList({}, pageOf([], 0));

    expect(await screen.findByText('No patients yet')).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Add Your First Patient'})).toHaveAttribute('href', '/patients/add');
  });

  it('says when a search matches nothing', async () => {
    renderList({last: 'Nobody'}, pageOf([], 0));

    expect((await screen.findAllByText('No patients match your search')).length).toBeGreaterThan(0);
  });

  it('shows an error when the patients cannot be loaded', async () => {
    mockApi({'GET /api/patients/search': () => json({}, 500)});
    renderWithApp(<PatientsListPage search={{}} onSearchChange={() => {}} />, {user: adminUser});

    await findAlert('The patients could not be loaded');
  });
});
