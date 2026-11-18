import {fireEvent, screen, waitFor, within} from '@testing-library/react';
import {SaveShortcutProvider} from '../../components/forms/SaveShortcut';
import {adminUser, calls, expectAnnounced, isDisabled, json, mockApi, openDialog, renderWithApp, withAnnouncements} from '../../test/render';
import {bankHolidaysOutcome, closedDayOutcome, ClosedDaysPage, parseClosedDaysSearch, shownYear, type ClosedDaysSearch} from './ClosedDaysPage';

const BASE = '/api/calendar/closed-days';

const SPRING = {id: '0000003c-0000-0000-0000-000000000001', date: '2026-05-25', name: 'Spring bank holiday', isPast: true};
const CHRISTMAS = {id: '0000003c-0000-0000-0000-000000000002', date: '2026-12-25', name: 'Christmas Day', isPast: false};

/** What's on a day: 14 appointments, a block and a meeting, for two people. */
const BUSY = {
  date: '2026-12-24',
  appointments: 14,
  blocks: 1,
  meetings: 1,
  staff: [
    {userId: '0000003d-0000-0000-0000-000000000001', username: 'k.ashford', appointments: 9, blocks: 1, meetings: 1},
    {userId: '0000003d-0000-0000-0000-000000000002', username: 's.brightwell', appointments: 5, blocks: 0, meetings: 1},
  ],
};
const EMPTY = {date: '2026-12-24', appointments: 0, blocks: 0, meetings: 0, staff: []};

function renderPage(search: ClosedDaysSearch = {}, extra: Parameters<typeof mockApi>[0] = {}) {
  const fetch = mockApi({[`GET ${BASE}`]: () => json([SPRING, CHRISTMAS]), ...extra});
  const onSearchChange = vi.fn();
  return {
    fetch,
    onSearchChange,
    ...renderWithApp(
      <SaveShortcutProvider>
        <ClosedDaysPage search={search} onSearchChange={onSearchChange} />
      </SaveShortcutProvider>,
      {user: adminUser},
    ),
  };
}

const rowOf = async (name: string) => (await screen.findByText(name)).closest('tr')!;
const dialog = () => within(openDialog()!);
const lastBody = (fetch: ReturnType<typeof mockApi>, method: string) =>
  fetch.mock.calls
    .map(([r]) => r as Request)
    .filter((r) => r.method === method)
    .at(-1)!
    .clone()
    .json();

describe('ClosedDaysPage', () => {
  beforeEach(() => {
    // Wednesday 18 November 2026.
    vi.useFakeTimers({toFake: ['Date'], now: new Date('2026-11-18T10:00:00Z')});
  });

  it("lists this year's closed days by date, saying which are past, and what a closed day means", async () => {
    const {fetch} = renderPage();

    const christmas = await rowOf('Christmas Day');
    expect(within(christmas).getByText('Fri 25 Dec 2026')).toBeInTheDocument();
    expect(within(christmas).getByText('To come')).toBeInTheDocument();
    expect(within(await rowOf('Spring bank holiday')).getByText('Past')).toBeInTheDocument();
    expect(screen.getByText('2026: 2 closed days')).toBeInTheDocument();
    expect(screen.getByText(/like a weekend on every calendar/)).toBeInTheDocument();
    expect(screen.getByText(/England and Wales/)).toBeInTheDocument();
    expect(calls(fetch)).toEqual([`GET ${BASE}?From=2026-01-01&To=2026-12-31`]);
  });

  it('moves a year at a time, keeping this year out of the address', async () => {
    const {user, onSearchChange, fetch} = renderPage({year: 2027});

    expect(await screen.findByText('2027: 2 closed days')).toBeInTheDocument();
    expect(calls(fetch)).toEqual([`GET ${BASE}?From=2027-01-01&To=2027-12-31`]);
    await user.click(screen.getByRole('button', {name: 'Next year'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({year: 2028});
    await user.click(screen.getByRole('button', {name: 'Previous year'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({});
  });

  it('says when a year has none, or they could not be loaded', async () => {
    renderPage({}, {[`GET ${BASE}`]: () => json([])});
    expect(await screen.findByText('No closed days in 2026')).toBeInTheDocument();
  });

  it("says when they can't be loaded", async () => {
    renderPage({}, {[`GET ${BASE}`]: () => json({title: 'Server error'}, 500)});
    expect(await screen.findByText('The closed days could not be loaded. Please try again.')).toBeInTheDocument();
  });

  it("adds one by hand, from today on, announcing it, and shows the API's errors in the dialog", async () => {
    withAnnouncements();
    let attempt = 0;
    const {user, fetch} = renderPage(
      {},
      {
        [`POST ${BASE}`]: () =>
          ++attempt === 1
            ? json({title: 'Validation failed', status: 400, errors: {Name: ['Name must be 100 characters or fewer.']}}, 400)
            : attempt === 2
              ? json({title: 'That date is already a closed day.', status: 409}, 409)
              : json({id: '0000003c-0000-0000-0000-000000000003', date: '2026-12-24', name: 'Training day', isPast: false}, 201),
      },
    );

    await user.click(await screen.findByRole('button', {name: 'Add Closed Day'}));
    expect(isDisabled(dialog().getByRole('button', {name: 'Add Closed Day'}))).toBe(true);
    await user.type(dialog().getByRole('combobox', {name: /Date/}), '24 December 2026{Enter}');
    await user.type(dialog().getByRole('textbox', {name: /Name/}), 'Training day');
    await user.click(dialog().getByRole('button', {name: 'Add Closed Day'}));

    expect((await dialog().findAllByText('Name must be 100 characters or fewer.')).length).toBeGreaterThan(0);
    expect(await lastBody(fetch, 'POST')).toEqual({date: '2026-12-24', name: 'Training day'});
    await user.click(dialog().getByRole('button', {name: 'Add Closed Day'}));
    expect(await dialog().findByText('That date is already a closed day.')).toBeInTheDocument();
    await user.click(dialog().getByRole('button', {name: 'Add Closed Day'}));

    await waitFor(() => expect(openDialog()).toBeNull());
    await expectAnnounced('Training day added as a closed day');
  });

  it("says what's already on the date before closing it, and after, until dismissed", async () => {
    withAnnouncements();
    const {user, fetch} = renderPage(
      {},
      {
        [`GET ${BASE}/bookings`]: () => json(BUSY),
        [`POST ${BASE}`]: () => json({id: '0000003c-0000-0000-0000-000000000006', date: '2026-12-24', name: 'Training day', isPast: false, bookings: BUSY}, 201),
      },
    );

    await user.click(await screen.findByRole('button', {name: 'Add Closed Day'}));
    await user.type(dialog().getByRole('combobox', {name: /Date/}), '24 December 2026{Enter}');
    const before = 'On Thu 24 Dec: 14 appointments, 1 block and 1 meeting, for k.ashford and s.brightwell. They stay; each of them will be told.';
    expect(await dialog().findByText(before)).toBeInTheDocument();
    expect(calls(fetch)).toContain(`GET ${BASE}/bookings?date=2026-12-24`);
    await user.type(dialog().getByRole('textbox', {name: /Name/}), 'Training day');
    await user.click(dialog().getByRole('button', {name: 'Add Closed Day'}));

    await waitFor(() => expect(openDialog()).toBeNull());
    const after = 'On Thu 24 Dec: 14 appointments, 1 block and 1 meeting, for k.ashford and s.brightwell. They stay; each of them has been told.';
    expect(await screen.findByText('Training day added as a closed day.')).toBeInTheDocument();
    expect(screen.getByText(after)).toBeInTheDocument();
    await expectAnnounced(`Training day added as a closed day. ${after}`);
    await user.click(screen.getByRole('button', {name: /^Dismiss/}));
    await waitFor(() => expect(screen.queryByText('Training day added as a closed day.')).not.toBeInTheDocument());
  });

  it('says when nothing is booked on the date, or it could not be checked', async () => {
    let attempt = 0;
    const {user} = renderPage({}, {[`GET ${BASE}/bookings`]: () => (++attempt === 1 ? json(EMPTY) : json({title: 'Server error'}, 500))});

    await user.click(await screen.findByRole('button', {name: 'Add Closed Day'}));
    await user.type(dialog().getByRole('combobox', {name: /Date/}), '24 December 2026{Enter}');
    expect(await dialog().findByText('Nothing is booked then.')).toBeInTheDocument();
    await user.clear(dialog().getByRole('combobox', {name: /Date/}));
    await user.type(dialog().getByRole('combobox', {name: /Date/}), '23 December 2026{Enter}');
    expect(await dialog().findByText("What's already booked then could not be checked.")).toBeInTheDocument();
  });

  it('adds nothing when Add Closed Day is cancelled, or Ctrl+S is pressed without a name', async () => {
    const {user, fetch} = renderPage({}, {[`GET ${BASE}/bookings`]: () => json(EMPTY)});

    await user.click(await screen.findByRole('button', {name: 'Add Closed Day'}));
    await user.type(dialog().getByRole('combobox', {name: /Date/}), '24 December 2026{Enter}');
    fireEvent.keyDown(dialog().getByRole('textbox', {name: /Name/}), {key: 's', ctrlKey: true});
    await user.click(dialog().getByRole('button', {name: 'Cancel'}));

    await waitFor(() => expect(openDialog()).toBeNull());
    expect(calls(fetch).some((c) => c.startsWith('POST'))).toBe(false);
  });

  it("goes to the year of one added in another year", async () => {
    const {user, onSearchChange} = renderPage(
      {},
      {[`POST ${BASE}`]: () => json({id: '0000003c-0000-0000-0000-000000000004', date: '2027-01-04', name: 'Training day', isPast: false}, 201)},
    );

    await user.click(await screen.findByRole('button', {name: 'Add Closed Day'}));
    await user.type(dialog().getByRole('combobox', {name: /Date/}), '4 January 2027{Enter}');
    await user.type(dialog().getByRole('textbox', {name: /Name/}), 'Training day');
    await user.click(dialog().getByRole('button', {name: 'Add Closed Day'}));

    await waitFor(() => expect(onSearchChange).toHaveBeenCalledWith({year: 2027}));
    expect(await screen.findByText('Training day added as a closed day.')).toBeInTheDocument();
  });

  it('removes one still to come, after asking, but never a past one', async () => {
    withAnnouncements();
    const {user, fetch} = renderPage({}, {[`DELETE ${BASE}/${CHRISTMAS.id}`]: () => new Response(null, {status: 204})});

    const past = within(await rowOf('Spring bank holiday')).getByRole('button', {name: "Past days can't change"});
    expect(isDisabled(past)).toBe(true);
    await user.click(within(await rowOf('Christmas Day')).getByRole('button', {name: 'Remove Christmas Day'}));
    expect(dialog().getByText('Fri 25 Dec 2026')).toBeInTheDocument();
    await user.click(dialog().getByRole('button', {name: 'Remove'}));

    await waitFor(() => expect(calls(fetch)).toContain(`DELETE ${BASE}/${CHRISTMAS.id}`));
    await waitFor(() => expect(openDialog()).toBeNull());
    await expectAnnounced('Christmas Day removed');
  });

  it('removes nothing when Remove is called off', async () => {
    const {user, fetch} = renderPage();

    await user.click(within(await rowOf('Christmas Day')).getByRole('button', {name: 'Remove Christmas Day'}));
    await user.click(dialog().getByRole('button', {name: 'Cancel'}));

    await waitFor(() => expect(openDialog()).toBeNull());
    expect(calls(fetch).some((c) => c.startsWith('DELETE'))).toBe(false);
  });

  it("shows the API's reason when one can't be removed", async () => {
    const {user} = renderPage({}, {[`DELETE ${BASE}/${CHRISTMAS.id}`]: () => json({title: "Past days can't change."}, 409)});

    await user.click(within(await rowOf('Christmas Day')).getByRole('button', {name: 'Remove Christmas Day'}));
    await user.click(dialog().getByRole('button', {name: 'Remove'}));

    expect(await screen.findByText("Past days can't change.")).toBeInTheDocument();
  });

  it("adds the year's bank holidays in one call, saying what was added and what was left out", async () => {
    withAnnouncements();
    const {user, fetch} = renderPage(
      {year: 2027},
      {
        [`POST ${BASE}/bank-holidays`]: () =>
          json({
            year: 2027,
            added: [
              {id: '0000003c-0000-0000-0000-000000000005', date: '2027-01-01', name: "New Year's Day", isPast: false, bookings: {...EMPTY, date: '2027-01-01'}},
              {
                id: '0000003c-0000-0000-0000-000000000007',
                date: '2027-03-26',
                name: 'Good Friday',
                isPast: false,
                bookings: {date: '2027-03-26', appointments: 2, blocks: 0, meetings: 0, staff: [BUSY.staff[0]]},
              },
            ],
            skipped: [{date: '2027-12-27', name: 'Christmas Day', reason: 'AlreadyClosed'}],
          }),
      },
    );

    await user.click(await screen.findByRole('button', {name: 'Add Bank Holidays for 2027'}));

    // In the page, above the list (not squeezed into the header), until dismissed.
    const outcome = (await screen.findByText('Added 2 bank holidays.')).closest('.astryx-banner') as HTMLElement;
    expect(within(outcome).getByText('Christmas Day, Mon 27 Dec: already closed')).toBeInTheDocument();
    expect(within(outcome).getByText('On Fri 26 Mar: 2 appointments, for k.ashford. They stay; k.ashford has been told.')).toBeInTheDocument();
    expect(within(outcome).queryByText(/Fri 1 Jan/)).not.toBeInTheDocument();
    expect(await lastBody(fetch, 'POST')).toEqual({year: 2027});
    await expectAnnounced('Added 2 bank holidays. 1 left out. On Fri 26 Mar: 2 appointments, for k.ashford. They stay; k.ashford has been told.');
    await user.click(screen.getByRole('button', {name: /^Dismiss/}));
    await waitFor(() => expect(screen.queryByText('Added 2 bank holidays.')).not.toBeInTheDocument());
  });

  it("says so when the bank holidays can't be added", async () => {
    const {user} = renderPage({}, {[`POST ${BASE}/bank-holidays`]: () => json({title: 'Server error'}, 500)});

    await user.click(await screen.findByRole('button', {name: 'Add Bank Holidays for 2026'}));

    expect(await screen.findByText('The bank holidays could not be added. Please try again.')).toBeInTheDocument();
  });
});

describe('closed days search and words', () => {
  it('reads the year from the address, dropping anything else', () => {
    expect(parseClosedDaysSearch({year: '2027'})).toEqual({year: 2027});
    expect(parseClosedDaysSearch({year: 2030})).toEqual({year: 2030});
    expect(parseClosedDaysSearch({year: '1999'})).toEqual({});
    expect(parseClosedDaysSearch({year: 'soon'})).toEqual({});
    expect(parseClosedDaysSearch({})).toEqual({});
    expect(shownYear({}, '2026-11-18')).toBe(2026);
    expect(shownYear({year: 2028}, '2026-11-18')).toBe(2028);
  });

  it('says what adding bank holidays did', () => {
    expect(bankHolidaysOutcome({year: 2026, added: [], skipped: [{date: '2026-01-01', name: "New Year's Day", reason: 'Past'}]})).toEqual({
      message: 'No bank holidays added for 2026.',
      details: ["New Year's Day, Thu 1 Jan: past"],
    });
    expect(bankHolidaysOutcome({year: 2027, added: [{}, {}], skipped: [{date: '2027-01-01', name: 'Odd', reason: 'Other'}]})).toEqual({
      message: 'Added 2 bank holidays.',
      details: ['Odd, Fri 1 Jan: other'],
    });
  });

  it('says what adding one closed day did', () => {
    expect(closedDayOutcome({date: '2026-12-24', name: 'Training day'}, 'typed')).toEqual({message: 'Training day added as a closed day.', details: []});
    expect(closedDayOutcome({date: '2026-12-24', bookings: {...EMPTY, meetings: 2}}, 'Typed name')).toEqual({
      message: 'Typed name added as a closed day.',
      details: ['On Thu 24 Dec: 2 meetings. They stay.'],
    });
  });
});
