import {QueryClient} from '@tanstack/react-query';
import {renderHook, waitFor} from '@testing-library/react';
import type {ReactNode} from 'react';
import {AppProviders} from '../app/AppProviders';
import {calls, json, mockApi} from '../test/render';
import {setupKeys} from './pathwaySetup';
import {
  mySpecialtiesQuery,
  specialtiesQuery,
  specialtyKeys,
  specialtyNames,
  userSpecialtiesQuery,
  useSaveSpecialty,
  useSetUserSpecialties,
  useSpecialtyActions,
} from './specialties';

const DERMATOLOGY = {id: 's1', name: 'Dermatology', isArchived: false, version: 3, canBeDeleted: true};

/** A query client holding specialty lists, a user's specialties, the templates and the stage definitions, to see what's refreshed. */
function cachedClient() {
  const queryClient = new QueryClient();
  queryClient.setQueryData(specialtyKeys.list(true), [DERMATOLOGY]);
  queryClient.setQueryData(specialtyKeys.ofUser('k.ashford'), []);
  queryClient.setQueryData(specialtyKeys.mine, []);
  queryClient.setQueryData([...setupKeys.templates, 'all'], []);
  queryClient.setQueryData([...setupKeys.definitions, 'all'], []);
  const wrapper = ({children}: {children: ReactNode}) => <AppProviders queryClient={queryClient}>{children}</AppProviders>;
  return {queryClient, wrapper};
}

const isInvalidated = (queryClient: QueryClient, key: readonly unknown[]) => queryClient.getQueryState(key)?.isInvalidated;

async function expectSpecialtiesRefreshed(queryClient: QueryClient, {templates}: {templates: boolean}) {
  await waitFor(() => expect(isInvalidated(queryClient, specialtyKeys.list(true))).toBe(true));
  expect(isInvalidated(queryClient, specialtyKeys.ofUser('k.ashford'))).toBe(true);
  expect(isInvalidated(queryClient, specialtyKeys.mine)).toBe(true);
  await waitFor(() => expect(isInvalidated(queryClient, [...setupKeys.templates, 'all'])).toBe(templates));
  // Stage definitions don't name specialties.
  expect(isInvalidated(queryClient, [...setupKeys.definitions, 'all'])).toBe(false);
}

const bodyOf = async (fetch: ReturnType<typeof mockApi>, index = 0) => (fetch.mock.calls[index][0] as Request).json();

describe('specialty queries', () => {
  it('reads the active ones, all of them, a user\'s and my own', async () => {
    const fetch = mockApi({
      'GET /api/specialties': () => json([DERMATOLOGY]),
      'GET /api/auth/users/k.ashford/specialties': () => json([{id: 's1', name: 'Dermatology', isArchived: false}]),
      'GET /api/auth/me/specialties': () => json([]),
    });

    expect(await new QueryClient().fetchQuery(specialtiesQuery(false))).toEqual([DERMATOLOGY]);
    await new QueryClient().fetchQuery(specialtiesQuery(true));
    await new QueryClient().fetchQuery(userSpecialtiesQuery('k.ashford'));
    expect(await new QueryClient().fetchQuery(mySpecialtiesQuery)).toEqual([]);

    expect(calls(fetch)).toEqual([
      'GET /api/specialties?includeArchived=false',
      'GET /api/specialties?includeArchived=true',
      'GET /api/auth/users/k.ashford/specialties',
      'GET /api/auth/me/specialties',
    ]);
    expect(specialtiesQuery(true).queryKey).toEqual(['specialties', 'list', 'all']);
    expect(userSpecialtiesQuery('K.Ashford').queryKey).toEqual(['specialties', 'user', 'k.ashford']);
  });
});

describe('specialty changes', () => {
  it('adds one, then refreshes the specialties and the templates', async () => {
    const fetch = mockApi({'POST /api/specialties': () => json(DERMATOLOGY, 201)});
    const {queryClient, wrapper} = cachedClient();
    const {result} = renderHook(() => useSaveSpecialty(), {wrapper});

    expect(await result.current.mutateAsync({name: 'Dermatology'})).toEqual(DERMATOLOGY);

    expect(calls(fetch)).toEqual(['POST /api/specialties']);
    expect(await bodyOf(fetch)).toEqual({name: 'Dermatology'});
    await expectSpecialtiesRefreshed(queryClient, {templates: true});
  });

  it('renames one from the version shown', async () => {
    const fetch = mockApi({'PUT /api/specialties/s1': () => json({...DERMATOLOGY, name: 'Skin', version: 4})});
    const {queryClient, wrapper} = cachedClient();
    const {result} = renderHook(() => useSaveSpecialty(), {wrapper});

    await result.current.mutateAsync({id: 's1', name: 'Skin', expectedVersion: 3});

    expect(await bodyOf(fetch)).toEqual({name: 'Skin', expectedVersion: 3});
    await expectSpecialtiesRefreshed(queryClient, {templates: true});
  });

  it('archives, restores and deletes one, archiving and restoring from the version shown', async () => {
    const fetch = mockApi({
      'POST /api/specialties/s1/archive': () => json({...DERMATOLOGY, isArchived: true}),
      'POST /api/specialties/s1/restore': () => json(DERMATOLOGY),
      'DELETE /api/specialties/s1': () => new Response(null, {status: 204}),
    });
    const {queryClient, wrapper} = cachedClient();
    const {result} = renderHook(() => useSpecialtyActions(), {wrapper});

    await result.current.archive.mutateAsync({id: 's1', expectedVersion: 3});
    await result.current.restore.mutateAsync({id: 's1', expectedVersion: 4});
    await result.current.remove.mutateAsync('s1');

    expect(calls(fetch)).toEqual(['POST /api/specialties/s1/archive', 'POST /api/specialties/s1/restore', 'DELETE /api/specialties/s1']);
    expect(await bodyOf(fetch, 0)).toEqual({expectedVersion: 3});
    expect(await bodyOf(fetch, 1)).toEqual({expectedVersion: 4});
    await expectSpecialtiesRefreshed(queryClient, {templates: true});
  });

  it('reads them again after a 409, so the page has the version that is there now, but not after other failures', async () => {
    mockApi({
      'PUT /api/specialties/s1': () => json({title: 'Someone changed this specialty meanwhile.', status: 409}, 409),
      'POST /api/specialties': () => json({title: 'Server error'}, 500),
    });
    const {queryClient, wrapper} = cachedClient();
    const {result} = renderHook(() => useSaveSpecialty(), {wrapper});

    await expect(result.current.mutateAsync({name: 'Skin'})).rejects.toThrow();
    expect(isInvalidated(queryClient, specialtyKeys.list(true))).toBe(false);
    await expect(result.current.mutateAsync({id: 's1', name: 'Skin', expectedVersion: 2})).rejects.toThrow();
    await expectSpecialtiesRefreshed(queryClient, {templates: true});
  });

  it("reads a user's specialties again after a 409", async () => {
    mockApi({'PUT /api/auth/users/k.ashford/specialties': () => json({title: 'Urology is archived.', status: 409}, 409)});
    const {queryClient, wrapper} = cachedClient();
    const {result} = renderHook(() => useSetUserSpecialties(), {wrapper});

    await expect(result.current.mutateAsync({username: 'k.ashford', specialtyIds: ['s9']})).rejects.toMatchObject({status: 409, title: 'Urology is archived.'});
    await waitFor(() => expect(isInvalidated(queryClient, specialtyKeys.ofUser('k.ashford'))).toBe(true));
  });

  it("sets a user's specialties to exactly those given, then refreshes the specialties but not the templates", async () => {
    const fetch = mockApi({'PUT /api/auth/users/k.ashford/specialties': () => json([{id: 's1', name: 'Dermatology', isArchived: false}])});
    const {queryClient, wrapper} = cachedClient();
    const {result} = renderHook(() => useSetUserSpecialties(), {wrapper});

    await result.current.mutateAsync({username: 'k.ashford', specialtyIds: ['s1']});

    expect(await bodyOf(fetch)).toEqual({specialtyIds: ['s1']});
    await expectSpecialtiesRefreshed(queryClient, {templates: false});
  });
});

describe('specialtyNames', () => {
  it('lists them by name, or says there are none', () => {
    expect(specialtyNames([{name: 'Dermatology'}, {name: 'Urology'}], 'None')).toBe('Dermatology, Urology');
    expect(specialtyNames([], 'None')).toBe('None');
  });
});
