import {queryOptions, useMutation, useQueryClient} from '@tanstack/react-query';
import {api, unwrap, type Schemas} from './client';
import {ApiError} from './errors';
import {setupKeys} from './pathwaySetup';

/**
 * Teams (Dermatology, Urology, ...): admins keep the list; each pathway template belongs to one, and members of
 * staff are given theirs on the Users page. Anyone can read them; only admins change them. A team in use can only
 * be archived, which keeps it where it is but stops it being newly chosen.
 */
export type Team = Schemas['TeamResponse'];
/** One of a member of staff's teams: its ID, name and whether it's archived. */
export type TeamMembership = Schemas['TeamMembershipResponse'];

const TEAMS = '/api/teams';
const TEAM = '/api/teams/{id}';

/** Every team query is keyed under ['teams'], so every change refreshes them all. */
export const teamKeys = {
  all: ['teams'] as const,
  list: (includeArchived: boolean) => ['teams', 'list', includeArchived ? 'all' : 'active'] as const,
  ofUser: (username: string) => ['teams', 'user', username.toLowerCase()] as const,
  mine: ['teams', 'mine'] as const,
};

/** The teams, sorted by name: only the active ones, or archived ones too (they carry `isArchived`). */
export function teamsQuery(includeArchived: boolean) {
  return queryOptions({
    queryKey: teamKeys.list(includeArchived),
    queryFn: () => unwrap(api.GET(TEAMS, {params: {query: {includeArchived}}})),
  });
}

/** A member of staff's teams (admins). An unknown user is an ApiError with status 404. */
export function userTeamsQuery(username: string) {
  return queryOptions({
    queryKey: teamKeys.ofUser(username),
    queryFn: () => unwrap(api.GET('/api/auth/users/{username}/teams', {params: {path: {username}}})),
  });
}

/** The signed-in user's own teams. */
export const myTeamsQuery = queryOptions({
  queryKey: teamKeys.mine,
  queryFn: () => unwrap(api.GET('/api/auth/me/teams')),
});

/**
 * What a change refreshes: teams and templates together (a template shows its team's name, and a team
 * counts its templates). After a 409 (someone changed it meanwhile, or the name is taken) they're read again too, so
 * the page shows what's there now, with its new version.
 */
function useRefreshTeams() {
  const queryClient = useQueryClient();
  const onSuccess = async () => {
    await queryClient.invalidateQueries({queryKey: teamKeys.all});
    await queryClient.invalidateQueries({queryKey: setupKeys.templates});
  };
  const onError = (error: unknown) => (error instanceof ApiError && error.status === 409 ? onSuccess() : undefined);
  return {onSuccess, onError};
}

/**
 * A team's waiting-time targets, in days, for each priority (1 to 365): the centre's own administrative targets
 * for the waiting-time clock (decision 29), not national rules. A clock copies its target when it starts, so a change
 * affects only clocks that start afterwards.
 */
export interface TeamTargets {
  urgent: number | null;
  high: number | null;
  medium: number | null;
  low: number | null;
}

/** The targets a new team starts with, as the API sets them when none are sent: 14, 28, 126 and 126 days. */
export const DEFAULT_TEAM_TARGETS: TeamTargets = {urgent: 14, high: 28, medium: 126, low: 126};

/** The fewest and most days a target can be. */
export const MIN_TARGET_DAYS = 1;
export const MAX_TARGET_DAYS = 365;

/** The priorities, most urgent first, with their labels (as the app labels priorities) and the API's field names. */
export const TARGET_PRIORITIES = [
  {key: 'urgent', label: 'Urgent', field: 'targetDaysUrgent'},
  {key: 'high', label: 'High', field: 'targetDaysHigh'},
  {key: 'medium', label: 'Medium', field: 'targetDaysMedium'},
  {key: 'low', label: 'Low', field: 'targetDaysLow'},
] as const;

/** A team's targets, as its form starts editing them. */
export const teamTargets = (team: Team): TeamTargets => ({
  urgent: team.targetDaysUrgent ?? null,
  high: team.targetDaysHigh ?? null,
  medium: team.targetDaysMedium ?? null,
  low: team.targetDaysLow ?? null,
});

/** A team's targets in words: "Urgent 14, High 28, Medium 126, Low 126 days". */
export const targetsWords = (team: Team) =>
  `${TARGET_PRIORITIES.map(({key, label}) => `${label} ${teamTargets(team)[key] ?? '?'}`).join(', ')} days`;

/**
 * A team's name and waiting-time targets, and for a change the version the admin started from
 * (`expectedVersion`), so a stale one is a 409. Targets left out (or null) aren't sent: the API then uses its defaults
 * for a new team, and leaves a changed one's as they were.
 */
export interface TeamInput {
  id?: string;
  name: string;
  targets?: TeamTargets;
  expectedVersion?: number;
}

/** The targets as the API's fields, leaving out any not given. */
function targetFields(targets: TeamTargets | undefined) {
  if (!targets) return {};
  return Object.fromEntries(
    TARGET_PRIORITIES.filter(({key}) => targets[key] !== null).map(({key, field}) => [field, targets[key]]),
  ) as {targetDaysUrgent?: number; targetDaysHigh?: number; targetDaysMedium?: number; targetDaysLow?: number};
}

/** Adds a team (no `id`) or changes one's name and targets, returning it. A name already taken is a 409. */
export function useSaveTeam() {
  const refresh = useRefreshTeams();
  return useMutation({
    mutationFn: ({id, name, targets, expectedVersion}: TeamInput) =>
      id
        ? unwrap(api.PUT(TEAM, {params: {path: {id}}, body: {name, ...targetFields(targets), expectedVersion}}))
        : unwrap(api.POST(TEAMS, {body: {name, ...targetFields(targets)}})),
    ...refresh,
  });
}

/** A team and the version the admin saw, for archiving or restoring it. */
export interface TeamVersion {
  id: string;
  expectedVersion: number;
}

/**
 * Archive, restore and delete. Archiving and restoring send the version shown, so a change made meanwhile is a 409;
 * only a team nothing uses can be deleted (`canBeDeleted`; a 409 otherwise).
 */
export function useTeamActions() {
  const refresh = useRefreshTeams();
  return {
    archive: useMutation({
      mutationFn: ({id, expectedVersion}: TeamVersion) =>
        unwrap(api.POST('/api/teams/{id}/archive', {params: {path: {id}}, body: {expectedVersion}})),
      ...refresh,
    }),
    restore: useMutation({
      mutationFn: ({id, expectedVersion}: TeamVersion) =>
        unwrap(api.POST('/api/teams/{id}/restore', {params: {path: {id}}, body: {expectedVersion}})),
      ...refresh,
    }),
    remove: useMutation({
      mutationFn: (id: string) => unwrap(api.DELETE(TEAM, {params: {path: {id}}})),
      ...refresh,
    }),
  };
}

/**
 * Sets a member of staff's teams to exactly these (admins), returning them. An archived one they already have
 * can be kept, but not newly given (409); an unknown user or team is a 404.
 */
export function useSetUserTeams() {
  const queryClient = useQueryClient();
  const refresh = () => queryClient.invalidateQueries({queryKey: teamKeys.all});
  return useMutation({
    mutationFn: ({username, teamIds}: {username: string; teamIds: string[]}) =>
      unwrap(api.PUT('/api/auth/users/{username}/teams', {params: {path: {username}}, body: {teamIds}})),
    // Their teams, the members counts, and the admin's own if they were the one changed; after a 409 (a
    // team archived meanwhile) too, so the dialog shows it archived.
    onSuccess: refresh,
    onError: (error) => (error instanceof ApiError && error.status === 409 ? refresh() : undefined),
  });
}

/** Teams in words: "Dermatology, Urology", or `none` when there are none. */
export function teamNames(teams: readonly TeamMembership[], none: string): string {
  const names = teams.map((s) => s.name ?? '').filter(Boolean);
  return names.length ? names.join(', ') : none;
}
