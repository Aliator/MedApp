import {QueryClient} from '@tanstack/react-query';
import {renderHook, waitFor} from '@testing-library/react';
import type {ReactNode} from 'react';
import {AppProviders} from '../app/AppProviders';
import {calls, json, mockApi} from '../test/render';
import {closedDayKeys} from './closedDays';
import {addressLine, roomsQuery, siteKeys, sitesQuery, useRoomActions, useSaveRoom, useSaveSite, useSiteActions} from './sites';

/** Synthetic sites: the main site, and an outreach clinic with a room. */
const MAIN = {id: '5173e000-0000-4000-8000-000000000001', name: 'Main site', addressLines: [], rooms: []};
const ROOM = {id: '0000005a-0000-0000-0000-000000000001', siteId: '5173e000-0000-4000-8000-000000000002', siteName: 'Northgate outreach clinic', name: 'Clinic room 1'};
const NORTHGATE = {
  id: '5173e000-0000-4000-8000-000000000002',
  name: 'Northgate outreach clinic',
  addressLines: ['1 Northgate Road', 'Testville'],
  postcode: 'ZZ99 3AA',
  phone: '07700 900123',
  rooms: [ROOM],
};

/** A query client holding a sites list, a rooms list and a year of closed days, to see what's refreshed. */
function cachedClient() {
  const queryClient = new QueryClient();
  queryClient.setQueryData(siteKeys.list(true), [MAIN, NORTHGATE]);
  queryClient.setQueryData(siteKeys.rooms(NORTHGATE.id, false), [ROOM]);
  queryClient.setQueryData(closedDayKeys.range('2026-01-01', '2026-12-31'), []);
  const wrapper = ({children}: {children: ReactNode}) => <AppProviders queryClient={queryClient}>{children}</AppProviders>;
  return {queryClient, wrapper};
}

const isInvalidated = (queryClient: QueryClient, key: readonly unknown[]) => queryClient.getQueryState(key)?.isInvalidated;

async function expectSitesRefreshed(queryClient: QueryClient, {closedDays}: {closedDays: boolean}) {
  await waitFor(() => expect(isInvalidated(queryClient, siteKeys.list(true))).toBe(true));
  expect(isInvalidated(queryClient, siteKeys.rooms(NORTHGATE.id, false))).toBe(true);
  expect(isInvalidated(queryClient, closedDayKeys.range('2026-01-01', '2026-12-31'))).toBe(closedDays);
}

const bodyOf = (fetch: ReturnType<typeof mockApi>, index = 0) => (fetch.mock.calls[index][0] as Request).json();

describe('site queries', () => {
  it('fetches the sites, open ones only unless asked for archived ones too', async () => {
    const fetch = mockApi({'GET /api/sites': () => json([MAIN, NORTHGATE])});

    expect(await new QueryClient().fetchQuery(sitesQuery())).toEqual([MAIN, NORTHGATE]);
    await new QueryClient().fetchQuery(sitesQuery({includeArchived: true}));

    expect(calls(fetch)).toEqual(['GET /api/sites?includeArchived=false', 'GET /api/sites?includeArchived=true']);
    expect(sitesQuery().queryKey).toEqual(['sites', 'list', false]);
  });

  it("fetches one site's rooms, or every site's", async () => {
    const fetch = mockApi({'GET /api/rooms': () => json([ROOM])});

    expect(await new QueryClient().fetchQuery(roomsQuery({siteId: NORTHGATE.id}))).toEqual([ROOM]);
    await new QueryClient().fetchQuery(roomsQuery({includeArchived: true}));

    expect(calls(fetch)).toEqual([`GET /api/rooms?siteId=${NORTHGATE.id}&includeArchived=false`, 'GET /api/rooms?includeArchived=true']);
    expect(roomsQuery().queryKey).toEqual(['sites', 'rooms', null, false]);
  });

  it("puts a site's address on one line, postcode last", () => {
    expect(addressLine(NORTHGATE)).toBe('1 Northgate Road, Testville, ZZ99 3AA');
    expect(addressLine({addressLines: [' '], postcode: null})).toBe('');
    expect(addressLine({})).toBe('');
  });
});

describe('site changes', () => {
  it('adds a site, then refreshes the sites and the closed days', async () => {
    const fetch = mockApi({'POST /api/sites': () => json(NORTHGATE, 201)});
    const {queryClient, wrapper} = cachedClient();
    const {result} = renderHook(() => useSaveSite(), {wrapper});

    const body = {name: NORTHGATE.name, addressLines: NORTHGATE.addressLines, postcode: NORTHGATE.postcode, phone: NORTHGATE.phone};
    expect(await result.current.mutateAsync(body)).toEqual(NORTHGATE);

    expect(calls(fetch)).toEqual(['POST /api/sites']);
    expect(await bodyOf(fetch)).toEqual(body);
    await expectSitesRefreshed(queryClient, {closedDays: true});
  });

  it("replaces a site's details, and the closed days, which carry its name, are refreshed", async () => {
    const fetch = mockApi({[`PUT /api/sites/${MAIN.id}`]: () => json({...MAIN, name: 'Central clinic'})});
    const {queryClient, wrapper} = cachedClient();
    const {result} = renderHook(() => useSaveSite(), {wrapper});

    await result.current.mutateAsync({id: MAIN.id, name: 'Central clinic', addressLines: [], postcode: null, phone: null});

    expect(await bodyOf(fetch)).toEqual({name: 'Central clinic', addressLines: [], postcode: null, phone: null});
    await expectSitesRefreshed(queryClient, {closedDays: true});
  });

  it('archives, restores and deletes a site, refreshing the sites', async () => {
    const fetch = mockApi({
      [`POST /api/sites/${NORTHGATE.id}/archive`]: () => json({...NORTHGATE, isArchived: true}),
      [`POST /api/sites/${NORTHGATE.id}/restore`]: () => json(NORTHGATE),
      [`DELETE /api/sites/${NORTHGATE.id}`]: () => new Response(null, {status: 204}),
    });
    const {queryClient, wrapper} = cachedClient();
    const {result} = renderHook(() => useSiteActions(), {wrapper});

    await result.current.archive.mutateAsync(NORTHGATE.id);
    await result.current.restore.mutateAsync(NORTHGATE.id);
    await result.current.remove.mutateAsync(NORTHGATE.id);

    expect(calls(fetch)).toEqual([
      `POST /api/sites/${NORTHGATE.id}/archive`,
      `POST /api/sites/${NORTHGATE.id}/restore`,
      `DELETE /api/sites/${NORTHGATE.id}`,
    ]);
    await expectSitesRefreshed(queryClient, {closedDays: false});
  });
});

describe('room changes', () => {
  it('adds a room to a site, and renames one, refreshing the sites', async () => {
    const fetch = mockApi({
      'POST /api/rooms': () => json(ROOM, 201),
      [`PUT /api/rooms/${ROOM.id}`]: () => json({...ROOM, name: 'Clinic room 2'}),
    });
    const {queryClient, wrapper} = cachedClient();
    const {result} = renderHook(() => useSaveRoom(), {wrapper});

    await result.current.mutateAsync({siteId: NORTHGATE.id, name: 'Clinic room 1'});
    await result.current.mutateAsync({id: ROOM.id, siteId: NORTHGATE.id, name: 'Clinic room 2'});

    expect(calls(fetch)).toEqual(['POST /api/rooms', `PUT /api/rooms/${ROOM.id}`]);
    expect(await bodyOf(fetch, 0)).toEqual({siteId: NORTHGATE.id, name: 'Clinic room 1'});
    expect(await bodyOf(fetch, 1)).toEqual({name: 'Clinic room 2'});
    await expectSitesRefreshed(queryClient, {closedDays: false});
  });

  it('archives, restores and deletes a room, refreshing the sites', async () => {
    const fetch = mockApi({
      [`POST /api/rooms/${ROOM.id}/archive`]: () => json({...ROOM, isArchived: true}),
      [`POST /api/rooms/${ROOM.id}/restore`]: () => json(ROOM),
      [`DELETE /api/rooms/${ROOM.id}`]: () => new Response(null, {status: 204}),
    });
    const {queryClient, wrapper} = cachedClient();
    const {result} = renderHook(() => useRoomActions(), {wrapper});

    await result.current.archive.mutateAsync(ROOM.id);
    await result.current.restore.mutateAsync(ROOM.id);
    await result.current.remove.mutateAsync(ROOM.id);

    expect(calls(fetch)).toEqual([`POST /api/rooms/${ROOM.id}/archive`, `POST /api/rooms/${ROOM.id}/restore`, `DELETE /api/rooms/${ROOM.id}`]);
    await expectSitesRefreshed(queryClient, {closedDays: false});
  });
});
