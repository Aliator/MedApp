import {QueryClient} from '@tanstack/react-query';
import {renderHook, waitFor} from '@testing-library/react';
import type {ReactNode} from 'react';
import {AppProviders} from '../app/AppProviders';
import {calls, json, mockApi} from '../test/render';
import {setupKeys} from './pathwaySetup';
import {
  DEFAULT_TEAM_TARGETS,
  myTeamsQuery,
  teamsQuery,
  teamKeys,
  teamNames,
  teamTargets,
  targetsWords,
  userTeamsQuery,
  useSaveTeam,
  useSetUserTeams,
  useTeamActions,
} from './teams';

const DERMATOLOGY = {id: 's1', name: 'Dermatology', isArchived: false, version: 3, canBeDeleted: true};

/** A query client holding team lists, a user's teams, the templates and the stage definitions, to see what's refreshed. */
function cachedClient() {
  const queryClient = new QueryClient();
  queryClient.setQueryData(teamKeys.list(true), [DERMATOLOGY]);
  queryClient.setQueryData(teamKeys.ofUser('k.ashford'), []);
  queryClient.setQueryData(teamKeys.mine, []);
  queryClient.setQueryData([...setupKeys.templates, 'all'], []);
  queryClient.setQueryData([...setupKeys.definitions, 'all'], []);
  const wrapper = ({children}: {children: ReactNode}) => <AppProviders queryClient={queryClient}>{children}</AppProviders>;
  return {queryClient, wrapper};
}

const isInvalidated = (queryClient: QueryClient, key: readonly unknown[]) => queryClient.getQueryState(key)?.isInvalidated;

async function expectTeamsRefreshed(queryClient: QueryClient, {templates}: {templates: boolean}) {
  await waitFor(() => expect(isInvalidated(queryClient, teamKeys.list(true))).toBe(true));
  expect(isInvalidated(queryClient, teamKeys.ofUser('k.ashford'))).toBe(true);
  expect(isInvalidated(queryClient, teamKeys.mine)).toBe(true);
  await waitFor(() => expect(isInvalidated(queryClient, [...setupKeys.templates, 'all'])).toBe(templates));
  // Stage definitions don't name teams.
  expect(isInvalidated(queryClient, [...setupKeys.definitions, 'all'])).toBe(false);
}

const bodyOf = async (fetch: ReturnType<typeof mockApi>, index = 0) => (fetch.mock.calls[index][0] as Request).json();

describe('team queries', () => {
  it('reads the active ones, all of them, a user\'s and my own', async () => {
    const fetch = mockApi({
      'GET /api/teams': () => json([DERMATOLOGY]),
      'GET /api/auth/users/k.ashford/teams': () => json([{id: 's1', name: 'Dermatology', isArchived: false}]),
      'GET /api/auth/me/teams': () => json([]),
    });

    expect(await new QueryClient().fetchQuery(teamsQuery(false))).toEqual([DERMATOLOGY]);
    await new QueryClient().fetchQuery(teamsQuery(true));
    await new QueryClient().fetchQuery(userTeamsQuery('k.ashford'));
    expect(await new QueryClient().fetchQuery(myTeamsQuery)).toEqual([]);

    expect(calls(fetch)).toEqual([
      'GET /api/teams?includeArchived=false',
      'GET /api/teams?includeArchived=true',
      'GET /api/auth/users/k.ashford/teams',
      'GET /api/auth/me/teams',
    ]);
    expect(teamsQuery(true).queryKey).toEqual(['teams', 'list', 'all']);
    expect(userTeamsQuery('K.Ashford').queryKey).toEqual(['teams', 'user', 'k.ashford']);
  });
});

describe('team changes', () => {
  it('adds one, then refreshes the teams and the templates', async () => {
    const fetch = mockApi({'POST /api/teams': () => json(DERMATOLOGY, 201)});
    const {queryClient, wrapper} = cachedClient();
    const {result} = renderHook(() => useSaveTeam(), {wrapper});

    expect(await result.current.mutateAsync({name: 'Dermatology'})).toEqual(DERMATOLOGY);

    expect(calls(fetch)).toEqual(['POST /api/teams']);
    expect(await bodyOf(fetch)).toEqual({name: 'Dermatology'});
    await expectTeamsRefreshed(queryClient, {templates: true});
  });

  it('renames one from the version shown', async () => {
    const fetch = mockApi({'PUT /api/teams/s1': () => json({...DERMATOLOGY, name: 'Skin', version: 4})});
    const {queryClient, wrapper} = cachedClient();
    const {result} = renderHook(() => useSaveTeam(), {wrapper});

    await result.current.mutateAsync({id: 's1', name: 'Skin', expectedVersion: 3});

    expect(await bodyOf(fetch)).toEqual({name: 'Skin', expectedVersion: 3});
    await expectTeamsRefreshed(queryClient, {templates: true});
  });

  it('sends the waiting-time targets given, adding or changing one, and leaves out any not given', async () => {
    const fetch = mockApi({'POST /api/teams': () => json(DERMATOLOGY, 201), 'PUT /api/teams/s1': () => json(DERMATOLOGY)});
    const {wrapper} = cachedClient();
    const {result} = renderHook(() => useSaveTeam(), {wrapper});

    await result.current.mutateAsync({name: 'Dermatology', targets: DEFAULT_TEAM_TARGETS});
    await result.current.mutateAsync({id: 's1', name: 'Skin', targets: {urgent: 7, high: null, medium: 90, low: 120}, expectedVersion: 3});

    expect(await bodyOf(fetch, 0)).toEqual({name: 'Dermatology', targetDaysUrgent: 14, targetDaysHigh: 28, targetDaysMedium: 126, targetDaysLow: 126});
    expect(await bodyOf(fetch, 1)).toEqual({name: 'Skin', targetDaysUrgent: 7, targetDaysMedium: 90, targetDaysLow: 120, expectedVersion: 3});
  });

  it('archives, restores and deletes one, archiving and restoring from the version shown', async () => {
    const fetch = mockApi({
      'POST /api/teams/s1/archive': () => json({...DERMATOLOGY, isArchived: true}),
      'POST /api/teams/s1/restore': () => json(DERMATOLOGY),
      'DELETE /api/teams/s1': () => new Response(null, {status: 204}),
    });
    const {queryClient, wrapper} = cachedClient();
    const {result} = renderHook(() => useTeamActions(), {wrapper});

    await result.current.archive.mutateAsync({id: 's1', expectedVersion: 3});
    await result.current.restore.mutateAsync({id: 's1', expectedVersion: 4});
    await result.current.remove.mutateAsync('s1');

    expect(calls(fetch)).toEqual(['POST /api/teams/s1/archive', 'POST /api/teams/s1/restore', 'DELETE /api/teams/s1']);
    expect(await bodyOf(fetch, 0)).toEqual({expectedVersion: 3});
    expect(await bodyOf(fetch, 1)).toEqual({expectedVersion: 4});
    await expectTeamsRefreshed(queryClient, {templates: true});
  });

  it('reads them again after a 409, so the page has the version that is there now, but not after other failures', async () => {
    mockApi({
      'PUT /api/teams/s1': () => json({title: 'Someone changed this team meanwhile.', status: 409}, 409),
      'POST /api/teams': () => json({title: 'Server error'}, 500),
    });
    const {queryClient, wrapper} = cachedClient();
    const {result} = renderHook(() => useSaveTeam(), {wrapper});

    await expect(result.current.mutateAsync({name: 'Skin'})).rejects.toThrow();
    expect(isInvalidated(queryClient, teamKeys.list(true))).toBe(false);
    await expect(result.current.mutateAsync({id: 's1', name: 'Skin', expectedVersion: 2})).rejects.toThrow();
    await expectTeamsRefreshed(queryClient, {templates: true});
  });

  it("reads a user's teams again after a 409", async () => {
    mockApi({'PUT /api/auth/users/k.ashford/teams': () => json({title: 'Urology is archived.', status: 409}, 409)});
    const {queryClient, wrapper} = cachedClient();
    const {result} = renderHook(() => useSetUserTeams(), {wrapper});

    await expect(result.current.mutateAsync({username: 'k.ashford', teams: [{teamId: 's9', role: 'Member'}]})).rejects.toMatchObject({status: 409, title: 'Urology is archived.'});
    await waitFor(() => expect(isInvalidated(queryClient, teamKeys.ofUser('k.ashford'))).toBe(true));
  });

  it("sets a user's teams to exactly those given, then refreshes the teams but not the templates", async () => {
    const fetch = mockApi({'PUT /api/auth/users/k.ashford/teams': () => json([{id: 's1', name: 'Dermatology', isArchived: false}])});
    const {queryClient, wrapper} = cachedClient();
    const {result} = renderHook(() => useSetUserTeams(), {wrapper});

    await result.current.mutateAsync({username: 'k.ashford', teams: [{teamId: 's1', role: 'ClinicAdmin'}]});

    expect(await bodyOf(fetch)).toEqual({teams: [{teamId: 's1', role: 'ClinicAdmin'}]});
    await expectTeamsRefreshed(queryClient, {templates: false});
  });
});

describe('team targets', () => {
  it("reads a team's targets, and says them in words, most urgent first", () => {
    const team = {targetDaysUrgent: 7, targetDaysHigh: 21, targetDaysMedium: 90, targetDaysLow: 120};
    expect(teamTargets(team)).toEqual({urgent: 7, high: 21, medium: 90, low: 120});
    expect(teamTargets({})).toEqual({urgent: null, high: null, medium: null, low: null});
    expect(targetsWords(team)).toBe('Urgent 7, High 21, Medium 90, Low 120 days');
    expect(targetsWords({targetDaysUrgent: 7})).toBe('Urgent 7, High ?, Medium ?, Low ? days');
  });
});

describe('teamNames', () => {
  it('lists them by name, or says there are none', () => {
    expect(teamNames([{name: 'Dermatology'}, {name: 'Urology'}], 'None')).toBe('Dermatology, Urology');
    expect(teamNames([], 'None')).toBe('None');
  });
});
