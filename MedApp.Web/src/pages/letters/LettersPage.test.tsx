import {screen, waitFor, within} from '@testing-library/react';
import {calls, json, mockApi, renderApp, renderWithApp, staffUser, userWithRoles} from '../../test/render';
import {letterPage, testLetterSummary} from '../../test/letters';
import {HalfViewContext} from '../../components/pages/halfView';
import {LettersPage} from './LettersPage';
import type {LettersListSearch} from './lettersSearch';

const clinician = userWithRoles('test-clinician', 'Clinician');

function render(search: LettersListSearch, items = [testLetterSummary()], user = clinician, total = items.length) {
  const fetch = mockApi({'GET /api/letters/search': () => json(letterPage(items, total))});
  const onSearchChange = vi.fn();
  return {fetch, onSearchChange, ...renderWithApp(<LettersPage search={search} onSearchChange={onSearchChange} />, {user})};
}

describe('LettersPage', () => {
  afterEach(() => vi.unstubAllGlobals());

  it("opens a writer on My drafts, listing each letter's patient, what it is, who and where it is", async () => {
    const letter = testLetterSummary({status: 'AwaitingApproval', approverUsername: 'd.clinician', submittedAtUtc: '2026-11-17T09:00:00Z'});
    const {fetch} = render({}, [letter, testLetterSummary({pathwayTitle: null, status: 'Draft', isReturned: true})], clinician, 2);

    const link = (await screen.findAllByRole('link', {name: 'Freya Ahmed'}))[0];
    expect(link).toHaveAttribute('href', `/letters/${letter.id}`);
    expect(screen.getByRole('tab', {name: 'My drafts'})).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByText('My drafts: 2 letters')).toBeInTheDocument();
    const row = within(link.closest('tr')!);
    expect(row.getByText('Skin check')).toBeInTheDocument();
    expect(row.getByText('Clinic letter')).toBeInTheDocument();
    expect(row.getByText('Clinic follow-up')).toBeInTheDocument();
    expect(row.getByText('d.clinician')).toBeInTheDocument();
    expect(row.getByText('Awaiting approval')).toBeInTheDocument();
    expect(row.getByText(/^Submitted /)).toBeInTheDocument();
    expect(screen.getByText('Returned')).toBeInTheDocument();
    expect(calls(fetch)).toEqual(['GET /api/letters/search?View=MyDrafts&Page=1&PageSize=10']);
  });

  it('says what each step was: approved, withdrawn, or last updated', async () => {
    render({view: 'Sent'}, [
      testLetterSummary({status: 'Approved', unsentCount: 0, approvedAtUtc: '2026-11-17T11:00:00Z'}),
      testLetterSummary({status: 'Withdrawn', withdrawnAtUtc: '2026-11-18T11:00:00Z'}),
    ]);
    expect(await screen.findByText(/^Approved 17 Nov 2026/)).toBeInTheDocument();
    expect(screen.getByText(/^Withdrawn 18 Nov 2026/)).toBeInTheDocument();
    expect(screen.getByText('Sent: 2 letters')).toBeInTheDocument();
  });

  it('switches views and pages through the address', async () => {
    const {user, onSearchChange} = render({view: 'ToSend'}, [testLetterSummary()], clinician, 25);

    await screen.findAllByRole('link', {name: 'Freya Ahmed'});
    await user.click(screen.getByRole('tab', {name: 'Awaiting my approval'}));
    expect(onSearchChange).toHaveBeenCalledWith({view: 'AwaitingMyApproval'});
    await user.click(screen.getByRole('button', {name: /Next page|Go to page 2|^2$/}));
    expect(onSearchChange).toHaveBeenCalledWith({view: 'ToSend', page: 2});
  });

  it("says when a view is empty, or the list can't be loaded", async () => {
    const {unmount} = render({view: 'ReturnedToMe'}, [], staffUser);
    expect(await screen.findByText('Nothing returned')).toBeInTheDocument();
    expect(screen.getByText('Returned to me: 0 letters')).toBeInTheDocument();
    unmount();

    mockApi({'GET /api/letters/search': () => json({title: 'Boom'}, 500)});
    renderWithApp(<LettersPage search={{}} onSearchChange={vi.fn()} />, {user: staffUser});
    expect(await screen.findByText('The letters could not be loaded. Please try again.')).toBeInTheDocument();
  });

  it('drops the author and approver in a narrow window or half view', async () => {
    mockApi({'GET /api/letters/search': () => json(letterPage([testLetterSummary({lastUpdated: undefined})]))});
    renderWithApp(
      <HalfViewContext.Provider value={{isHalf: true}}>
        <LettersPage search={{view: 'ToSend'}} onSearchChange={vi.fn()} />
      </HalfViewContext.Provider>,
      {user: staffUser},
    );
    expect(await screen.findByText('Freya Ahmed')).toBeInTheDocument();
    expect(screen.queryByRole('columnheader', {name: 'Author'})).not.toBeInTheDocument();
  });

  it('opens at /letters, its view from the address (the route)', async () => {
    const fetch = mockApi({'GET /api/letters/search': () => json(letterPage([testLetterSummary()]))});
    renderApp({path: '/letters?view=sent', user: staffUser});
    expect(await screen.findByRole('heading', {name: 'Letters', level: 1})).toBeInTheDocument();
    await waitFor(() => expect(calls(fetch)).toContain('GET /api/letters/search?View=Sent&Page=1&PageSize=10'));
  });
});
