import {screen, waitFor, within} from '@testing-library/react';
import {testPattern, testSession} from '../../test/clinics';
import {expectAnnounced, json, managerUser, mockApi, openDialog, renderWithApp, withAnnouncements} from '../../test/render';
import {ClinicPatternsPage, madeWords, patternRepeat, patternWhen} from './ClinicPatternsPage';

const PATTERNS = '/api/clinics/patterns';

function renderPage(patterns = [testPattern()], handlers: Parameters<typeof mockApi>[0] = {}) {
  const fetch = mockApi({[`GET ${PATTERNS}`]: () => json(patterns), ...handlers});
  return {fetch, ...renderWithApp(<ClinicPatternsPage />, {path: '/calendar/clinics/patterns', user: managerUser})};
}

const dialog = () => within(openDialog() ?? document.body);

describe('ClinicPatternsPage', {timeout: 30_000}, () => {
  beforeEach(() => {
    vi.useFakeTimers({toFake: ['Date'], now: new Date('2026-11-23T10:00:00Z')});
  });

  it('lists each pattern with when, how often, who, where and the specialty', async () => {
    renderPage([testPattern(), testPattern({id: 'pattern-2', everyWeeks: 2, untilDate: '2026-11-01', hasEnded: true})]);

    expect(await screen.findByRole('heading', {name: 'Clinic Patterns', level: 1})).toBeInTheDocument();
    expect(await screen.findByText('2 patterns')).toBeInTheDocument();
    expect(screen.getAllByText('Tuesdays, 09:00 to 12:00')).toHaveLength(2);
    expect(screen.getByText('Every week from 24 Nov 2026 · 20-minute slots')).toBeInTheDocument();
    expect(screen.getByText('Ended')).toBeInTheDocument();
    expect(screen.getAllByText('Room 2')).toHaveLength(2);
  });

  it('creates sessions for the weeks chosen, and says what it made and skipped', async () => {
    withAnnouncements();
    const made = {
      created: [testSession(), testSession({id: 'session-2', date: '2026-12-01'})],
      skipped: [{date: '2026-12-08', reason: 'Closed: Staff training day.'}],
    };
    const {user, fetch} = renderPage([testPattern()], {[`POST ${PATTERNS}/pattern-1/create-sessions`]: () => json(made)});

    await user.click(await screen.findByRole('button', {name: /^Create sessions: Tuesdays/}));
    expect(await screen.findByRole('heading', {name: 'Create Sessions'})).toBeInTheDocument();
    await user.click(dialog().getByRole('combobox', {name: /For the next/}));
    await user.click(await screen.findByRole('option', {name: '3 weeks'}));
    await user.click(dialog().getByRole('button', {name: 'Create Sessions'}));

    await expectAnnounced('Made 2 sessions; skipped 1 date.');
    expect(within(dialog().getByRole('list', {name: 'Sessions made'})).getAllByRole('listitem')).toHaveLength(2);
    expect(dialog().getByText(/Closed: Staff training day\./)).toBeInTheDocument();
    const request = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'POST')!;
    expect(await request.clone().json()).toEqual({weeks: 3});
  });

  it('says when there are no patterns, and when they could not be loaded', async () => {
    const {unmount} = renderPage([]);
    expect(await screen.findByText('No clinic patterns')).toBeInTheDocument();
    unmount();
    mockApi({[`GET ${PATTERNS}`]: () => json({title: 'Error'}, 500)});
    renderWithApp(<ClinicPatternsPage />, {user: managerUser});
    expect(await screen.findByText('The clinic patterns could not be loaded. Please try again.')).toBeInTheDocument();
  });

  it('words patterns and what Create sessions made', () => {
    expect(patternWhen(testPattern())).toBe('Tuesdays, 09:00 to 12:00');
    expect(patternRepeat(testPattern({everyWeeks: 3, untilDate: '2027-03-01'}))).toBe('Every 3 weeks from 24 Nov 2026 until 1 Mar 2027');
    expect(madeWords({created: [], skipped: []})).toBe('Made no sessions.');
    expect(madeWords({created: [testSession()], skipped: []})).toBe('Made 1 session.');
    expect(madeWords({created: [], skipped: [{}, {}]})).toBe('Made no sessions; skipped 2 dates.');
  });

  it('opens New Pattern, Edit and Create Sessions, each closing again, and shows a refusal', async () => {
    const {user} = renderPage([testPattern()], {
      'GET /api/calendar/staff': () => json([]),
      'GET /api/rooms': () => json([]),
      'GET /api/specialties': () => json([]),
      [`POST ${PATTERNS}/pattern-1/create-sessions`]: () => json({title: 'Clinic pattern not found.'}, 404),
    });

    await user.click(await screen.findByRole('button', {name: 'New Pattern'}));
    expect(await screen.findByRole('heading', {name: 'New Clinic Pattern'})).toBeInTheDocument();
    await user.click(dialog().getByRole('button', {name: 'Cancel'}));
    await waitFor(() => expect(openDialog()).toBeNull());

    await user.click(screen.getByRole('button', {name: /^Edit: Tuesdays/}));
    expect(await screen.findByRole('heading', {name: 'Edit Clinic Pattern'})).toBeInTheDocument();
    await user.click(dialog().getByRole('button', {name: 'Cancel'}));
    await waitFor(() => expect(openDialog()).toBeNull());

    await user.click(screen.getByRole('button', {name: /^Create sessions: Tuesdays/}));
    await user.click(await dialog().findByRole('button', {name: 'Create Sessions'}));
    expect(await dialog().findByText('Clinic pattern not found.')).toBeInTheDocument();
    await user.click(dialog().getByRole('button', {name: 'Cancel'}));
    await waitFor(() => expect(openDialog()).toBeNull());
  });
});
