import {QueryClient} from '@tanstack/react-query';
import {renderHook, waitFor} from '@testing-library/react';
import type {ReactNode} from 'react';
import {AppProviders} from '../app/AppProviders';
import {calls, json, mockApi} from '../test/render';
import {
  bookingsWords,
  closedDayBookingsQuery,
  closedDayKeys,
  closedDaysOfYearQuery,
  closedDaysQuery,
  hasBookings,
  isClosedEverywhere,
  listWords,
  useAddBankHolidays,
  useAddClosedDay,
  useDeleteClosedDay,
} from './closedDays';

const CHRISTMAS = {id: '0000003c-0000-0000-0000-000000000001', date: '2026-12-25', name: 'Christmas Day', isPast: false};

/** A query client holding a closed days list, a calendar, a team view and a calendar setup list, to see what's refreshed. */
function cachedClient() {
  const queryClient = new QueryClient();
  queryClient.setQueryData(closedDayKeys.range('2026-01-01', '2026-12-31'), [CHRISTMAS]);
  queryClient.setQueryData(['calendar', 'days', 'me', '2026-12-21', '2026-12-27'], {});
  queryClient.setQueryData(['calendar', 'team', '2026-12-24', []], {});
  queryClient.setQueryData(['calendarSetup', 'activityTypes'], []);
  const wrapper = ({children}: {children: ReactNode}) => <AppProviders queryClient={queryClient}>{children}</AppProviders>;
  return {queryClient, wrapper};
}

async function expectRefreshed(queryClient: QueryClient) {
  await waitFor(() => expect(queryClient.getQueryState(closedDayKeys.range('2026-01-01', '2026-12-31'))?.isInvalidated).toBe(true));
  expect(queryClient.getQueryState(['calendar', 'days', 'me', '2026-12-21', '2026-12-27'])?.isInvalidated).toBe(true);
  expect(queryClient.getQueryState(['calendar', 'team', '2026-12-24', []])?.isInvalidated).toBe(true);
  // Activity types and templates don't change with a closed day.
  expect(queryClient.getQueryState(['calendarSetup', 'activityTypes'])?.isInvalidated).toBe(false);
}

describe('closedDaysQuery', () => {
  it('fetches the closed days in a range, or all of them', async () => {
    const fetch = mockApi({'GET /api/calendar/closed-days': () => json([CHRISTMAS])});

    expect(await new QueryClient().fetchQuery(closedDaysOfYearQuery(2026))).toEqual([CHRISTMAS]);
    await new QueryClient().fetchQuery(closedDaysQuery({from: '2026-11-18'}));
    await new QueryClient().fetchQuery(closedDaysQuery());

    expect(calls(fetch)).toEqual([
      'GET /api/calendar/closed-days?From=2026-01-01&To=2026-12-31',
      'GET /api/calendar/closed-days?From=2026-11-18',
      'GET /api/calendar/closed-days',
    ]);
    expect(closedDaysOfYearQuery(2026).queryKey).toEqual(['closedDays', '2026-01-01', '2026-12-31']);
    expect(closedDaysQuery().queryKey).toEqual(['closedDays', null, null]);
  });
});

describe('closed day changes', () => {
  it('adds one, then refreshes the closed days and every calendar', async () => {
    const fetch = mockApi({'POST /api/calendar/closed-days': () => json(CHRISTMAS, 201)});
    const {queryClient, wrapper} = cachedClient();
    const {result} = renderHook(() => useAddClosedDay(), {wrapper});

    expect(await result.current.mutateAsync({date: '2026-12-25', name: 'Christmas Day'})).toEqual(CHRISTMAS);

    expect(calls(fetch)).toEqual(['POST /api/calendar/closed-days']);
    expect(await (fetch.mock.calls[0][0] as Request).json()).toEqual({date: '2026-12-25', name: 'Christmas Day'});
    await expectRefreshed(queryClient);
  });

  it('closes a day at one site, sending its ID', async () => {
    const refit = {id: '0000003c-0000-0000-0000-000000000009', date: '2026-12-01', name: 'Refit', siteId: '5173e000-0000-4000-8000-000000000002', siteName: 'Northgate outreach clinic', isPast: false};
    const fetch = mockApi({'POST /api/calendar/closed-days': () => json(refit, 201)});
    const {queryClient, wrapper} = cachedClient();
    const {result} = renderHook(() => useAddClosedDay(), {wrapper});

    expect(await result.current.mutateAsync({date: '2026-12-01', name: 'Refit', siteId: refit.siteId})).toEqual(refit);

    expect(await (fetch.mock.calls[0][0] as Request).json()).toEqual({date: '2026-12-01', name: 'Refit', siteId: refit.siteId});
    await expectRefreshed(queryClient);
  });

  it('removes one, then refreshes them', async () => {
    const fetch = mockApi({[`DELETE /api/calendar/closed-days/${CHRISTMAS.id}`]: () => new Response(null, {status: 204})});
    const {queryClient, wrapper} = cachedClient();
    const {result} = renderHook(() => useDeleteClosedDay(), {wrapper});

    await result.current.mutateAsync(CHRISTMAS.id);

    expect(calls(fetch)).toEqual([`DELETE /api/calendar/closed-days/${CHRISTMAS.id}`]);
    await expectRefreshed(queryClient);
  });

  it("adds a year's bank holidays in one call, then refreshes them", async () => {
    const added = {year: 2027, added: [{...CHRISTMAS, date: '2027-12-27'}], skipped: []};
    const fetch = mockApi({'POST /api/calendar/closed-days/bank-holidays': () => json(added)});
    const {queryClient, wrapper} = cachedClient();
    const {result} = renderHook(() => useAddBankHolidays(), {wrapper});

    expect(await result.current.mutateAsync(2027)).toEqual(added);

    expect(await (fetch.mock.calls[0][0] as Request).json()).toEqual({year: 2027});
    await expectRefreshed(queryClient);
  });
});

describe('closedDayBookingsQuery', () => {
  it("fetches what's on a day", async () => {
    const bookings = {date: '2026-12-24', appointments: 1, blocks: 0, meetings: 0, staff: []};
    const fetch = mockApi({'GET /api/calendar/closed-days/bookings': () => json(bookings)});

    expect(await new QueryClient().fetchQuery(closedDayBookingsQuery('2026-12-24'))).toEqual(bookings);
    expect(calls(fetch)).toEqual(['GET /api/calendar/closed-days/bookings?date=2026-12-24']);
  });
});

describe('isClosedEverywhere', () => {
  it("tells a day closed everywhere from a site's", () => {
    expect(isClosedEverywhere({})).toBe(true);
    expect(isClosedEverywhere({siteId: null})).toBe(true);
    expect(isClosedEverywhere({siteId: '5173e000-0000-4000-8000-000000000002'})).toBe(false);
  });
});

describe('bookings in words', () => {
  const staff = (username: string) => ({userId: `id-${username}`, username, appointments: 1, blocks: 0, meetings: 0});

  it('lists things as people do', () => {
    expect(listWords([])).toBe('');
    expect(listWords(['a'])).toBe('a');
    expect(listWords(['a', 'b'])).toBe('a and b');
    expect(listWords(['a', 'b', 'c'])).toBe('a, b and c');
  });

  it("says what's on a day, for whom, and who is told", () => {
    const day = {date: '2026-12-24', appointments: 1, blocks: 2, meetings: 0, staff: [staff('k.ashford')]};
    expect(bookingsWords(day, 'Thu 24 Dec')).toBe('On Thu 24 Dec: 1 appointment and 2 blocks, for k.ashford. They stay; k.ashford will be told.');
    expect(bookingsWords({...day, staff: [staff('a.one'), staff('b.two'), staff('c.three')]}, 'Thu 24 Dec', {isDone: true})).toBe(
      'On Thu 24 Dec: 1 appointment and 2 blocks, for a.one, b.two and c.three. They stay; each of them has been told.',
    );
    expect(bookingsWords({date: '2026-12-24', meetings: 1}, 'Thu 24 Dec')).toBe('On Thu 24 Dec: 1 meeting. They stay.');
    expect(bookingsWords({date: '2026-12-24', appointments: 0, blocks: 0, meetings: 0}, 'Thu 24 Dec')).toBe('Nothing is booked then.');
    expect(hasBookings(null)).toBe(false);
    expect(hasBookings({blocks: 1})).toBe(true);
  });
});
