import {QueryClient} from '@tanstack/react-query';
import {renderHook, waitFor} from '@testing-library/react';
import type {ReactNode} from 'react';
import {AppProviders} from '../app/AppProviders';
import {calls, json, mockApi} from '../test/render';
import {closedDayKeys, closedDaysOfYearQuery, closedDaysQuery, useAddBankHolidays, useAddClosedDay, useDeleteClosedDay} from './closedDays';

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
