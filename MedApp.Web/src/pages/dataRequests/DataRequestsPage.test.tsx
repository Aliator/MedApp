import {screen, within} from '@testing-library/react';
import {adminUser, calls, json, mockApi, pageOf, pickFavourite, renderWithApp, searchBar} from '../../test/render';
import {DATA_REQUEST_ID, DATA_REQUEST_PATIENT_ID, testDataRequestSummary} from '../../test/dataRequests';
import {HalfViewContext} from '../../components/pages/halfView';
import {DataRequestsPage} from './DataRequestsPage';
import type {DataRequestsListSearch} from './dataRequestsSearch';

function render(search: DataRequestsListSearch, items = [testDataRequestSummary()], total = items.length) {
  const fetch = mockApi({'GET /api/data-requests/search': () => json(pageOf(items, total))});
  const onSearchChange = vi.fn();
  return {fetch, onSearchChange, ...renderWithApp(<DataRequestsPage search={search} onSearchChange={onSearchChange} />, {user: adminUser})};
}

describe('DataRequestsPage', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('opens on the open requests, listing the patient, the request, when it is due, and what is done', async () => {
    const overdue = testDataRequestSummary({
      id: '000000d1-0000-0000-0000-000000000009',
      patientName: 'Ivy Example',
      requestedByPatient: false,
      isOverdue: true,
      isIdentityChecked: true,
    });
    const {fetch} = render({}, [testDataRequestSummary(), overdue]);

    const patient = await screen.findByRole('link', {name: 'Freya Ahmed'});
    expect(patient).toHaveAttribute('href', `/patients/${DATA_REQUEST_PATIENT_ID}`);
    expect(screen.getByRole('tab', {name: 'Open'})).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByText('Open: 2 requests')).toBeInTheDocument();
    const row = within(patient.closest('tr')!);
    expect(row.getByRole('link', {name: 'Received 2 Nov 2026'})).toHaveAttribute('href', `/data-requests/${DATA_REQUEST_ID}`);
    expect(row.getByText('Requested by the patient')).toBeInTheDocument();
    expect(row.getByText('2 Dec 2026')).toBeInTheDocument();
    expect(row.queryByText('Overdue')).not.toBeInTheDocument();
    expect(row.getAllByText('Not yet')).toHaveLength(2);

    const other = within(screen.getByRole('link', {name: 'Ivy Example'}).closest('tr')!);
    expect(other.getByText('Overdue')).toBeInTheDocument();
    expect(other.getByText('Requested on their behalf')).toBeInTheDocument();
    expect(other.getByText('Yes')).toBeInTheDocument();
    expect(calls(fetch)).toEqual(['GET /api/data-requests/search?Status=Open&Page=1&PageSize=10']);
  });

  it('asks for the open requests not yet reviewed in Awaiting review, and every request in All', async () => {
    const {fetch, unmount} = render({view: 'AwaitingReview'}, []);
    expect(await screen.findByText('Nothing to review')).toBeInTheDocument();
    expect(calls(fetch)).toEqual(['GET /api/data-requests/search?AwaitingReview=true&Page=1&PageSize=10']);
    unmount();

    const all = render({view: 'All'}, []);
    expect(await screen.findByText('No data requests')).toBeInTheDocument();
    expect(calls(all.fetch)).toEqual(['GET /api/data-requests/search?Page=1&PageSize=10']);
  });

  it('switches views and pages through the address', async () => {
    const {user, onSearchChange} = render({view: 'Sent'}, [testDataRequestSummary({status: 'Sent'})], 25);

    await screen.findByRole('link', {name: 'Freya Ahmed'});
    await user.click(screen.getByRole('tab', {name: 'Withdrawn'}));
    expect(onSearchChange).toHaveBeenCalledWith({view: 'Withdrawn'});
    await user.click(screen.getByRole('button', {name: /Next page|Go to page 2|^2$/}));
    expect(onSearchChange).toHaveBeenCalledWith({view: 'Sent', page: 2});
  });

  it('drops the page from the address on the way back to the first', async () => {
    const {user, onSearchChange} = render({view: 'Refused', page: 2}, [testDataRequestSummary({status: 'Refused'})], 11);

    await screen.findByRole('link', {name: 'Freya Ahmed'});
    await user.click(screen.getByRole('button', {name: /Previous page|Go to page 1|^1$/}));
    expect(onSearchChange).toHaveBeenCalledWith({view: 'Refused', page: undefined});
  });

  it('asks for the patient searched for and the column sorted by', async () => {
    const {fetch} = render({view: 'Sent', patient: 'Freya', sort: 'due', asc: false}, [testDataRequestSummary({status: 'Sent'})]);

    await screen.findByRole('link', {name: 'Freya Ahmed'});
    expect(calls(fetch)).toEqual(['GET /api/data-requests/search?Status=Sent&Search=Freya&Sort=due&Ascending=false&Page=1&PageSize=10']);
    expect(searchBar()).toHaveTextContent(/Patient: contains\s*Freya/);
  });

  it('shows every request from its All tab, and sorts by a column, back to the first page', async () => {
    const {user, onSearchChange} = render({view: 'Open', page: 2});

    await screen.findByRole('link', {name: 'Freya Ahmed'});
    await pickFavourite(user, 'All');
    expect(onSearchChange).toHaveBeenLastCalledWith({view: 'All', patient: undefined, sort: undefined, asc: undefined});

    await user.click(screen.getByRole('button', {name: /^Patient/}));
    expect(onSearchChange).toHaveBeenLastCalledWith({view: 'Open', page: undefined, sort: 'patient', asc: true});
  });

  it('offers to clear a search that matches nothing', async () => {
    const {user, onSearchChange} = render({patient: 'Nobody'}, []);

    await user.click(await screen.findByRole('button', {name: 'Clear Search'}));

    expect(onSearchChange).toHaveBeenLastCalledWith({page: undefined, patient: undefined});
  });

  it("says when the list can't be loaded", async () => {
    mockApi({'GET /api/data-requests/search': () => json({title: 'Boom'}, 500)});
    renderWithApp(<DataRequestsPage search={{}} onSearchChange={vi.fn()} />, {user: adminUser});
    expect(await screen.findByText('The data requests could not be loaded. Please try again.')).toBeInTheDocument();
  });

  it('drops the identity check and review columns in a narrow window or half view', async () => {
    mockApi({'GET /api/data-requests/search': () => json(pageOf([testDataRequestSummary()]))});
    renderWithApp(
      <HalfViewContext.Provider value={{isHalf: true}}>
        <DataRequestsPage search={{}} onSearchChange={vi.fn()} />
      </HalfViewContext.Provider>,
      {user: adminUser},
    );
    expect(await screen.findByText('Freya Ahmed')).toBeInTheDocument();
    expect(screen.queryByRole('columnheader', {name: 'Identity checked'})).not.toBeInTheDocument();
  });
});
