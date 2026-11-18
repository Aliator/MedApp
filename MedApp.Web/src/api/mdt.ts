import {queryOptions, useMutation, useQueryClient} from '@tanstack/react-query';
import {api, unwrap, type Schemas} from './client';

/**
 * The MDT list (docs/centre-phases-3-5-plan.md, decisions 141 to 144): pathways put forward for a team's
 * multidisciplinary team meeting (a calendar meeting marked as its MDT), with the question for the team and, once
 * discussed, the outcome as a person wrote it and who was present. **MedApp never suggests an outcome:** nothing here
 * is prefilled. Reading a list records PatientListed for each patient on it; a pathway's items record PathwayViewed.
 */
export type MdtItem = Schemas['MdtItemResponse'];
export type MdtMeeting = Schemas['MdtMeetingResponse'];
export type MdtMeetingList = Schemas['MdtMeetingListResponse'];

/** Every MDT query is under ['mdt'], so any change refreshes them all. */
export const mdtKeys = {
  all: ['mdt'] as const,
  meetings: (teamId: string | undefined) => ['mdt', 'meetings', teamId ?? null] as const,
  list: (meetingId: string) => ['mdt', 'list', meetingId] as const,
  awaiting: (teamId: string | undefined) => ['mdt', 'awaiting', teamId ?? null] as const,
  pathway: (pathwayId: string) => ['mdt', 'pathway', pathwayId] as const,
};

/** The MDTs from 60 days ago to 180 days ahead, by start: one team's, or every one's. Names no patient. */
export function mdtMeetingsQuery(teamId?: string) {
  return queryOptions({
    queryKey: mdtKeys.meetings(teamId),
    queryFn: () => unwrap(api.GET('/api/mdt/meetings', {params: {query: {teamId}}})),
  });
}

/** One MDT's list, in the order its items were listed. */
export function mdtMeetingListQuery(meetingId: string) {
  return queryOptions({
    queryKey: mdtKeys.list(meetingId),
    queryFn: () => unwrap(api.GET('/api/mdt/meetings/{meetingId}/items', {params: {path: {meetingId}}})),
  });
}

/** Open items whose MDT was cancelled ("Meeting cancelled: list again"). */
export function mdtAwaitingMeetingQuery(teamId?: string) {
  return queryOptions({
    queryKey: mdtKeys.awaiting(teamId),
    queryFn: () => unwrap(api.GET('/api/mdt/awaiting-meeting', {params: {query: {teamId}}})),
  });
}

/** A pathway's MDT items, newest listed first. */
export function pathwayMdtItemsQuery(pathwayId: string) {
  return queryOptions({
    queryKey: mdtKeys.pathway(pathwayId),
    queryFn: () => unwrap(api.GET('/api/mdt/pathways/{pathwayId}/items', {params: {path: {pathwayId}}})),
  });
}

/** MDTs still to come or under way (not ended), soonest first, for listing or deferring an item. */
export const upcomingMeetings = (meetings: readonly MdtMeeting[], now = new Date()) =>
  meetings
    .filter((m) => new Date(m.endUtc ?? 0).getTime() > now.getTime())
    .sort((a, b) => new Date(a.startUtc ?? 0).getTime() - new Date(b.startUtc ?? 0).getTime());

/** What an item's status is called. */
export const MDT_STATUS_WORDS: Record<string, string> = {
  Listed: 'Listed',
  Deferred: 'Deferred',
  Discussed: 'Discussed',
  Withdrawn: 'Withdrawn',
};

export const mdtStatusWords = (status: string | null | undefined) => MDT_STATUS_WORDS[status ?? ''] ?? status ?? '';

/** Refreshes every MDT list, and the timeline (MDT entries are on it). */
function useRefreshMdt() {
  const queryClient = useQueryClient();
  return async () => {
    await Promise.all([
      queryClient.invalidateQueries({queryKey: mdtKeys.all}),
      queryClient.invalidateQueries({queryKey: ['pathways', 'timeline']}),
    ]);
  };
}

/** Lists a pathway for an MDT that hasn't ended, with the question for the team (1,000 characters at most). */
export function useListForMdt() {
  const onSuccess = useRefreshMdt();
  return useMutation({
    mutationFn: (body: {pathwayId: string; meetingId: string; question: string}) => unwrap(api.POST('/api/mdt/items', {body})),
    onSuccess,
  });
}

/** Withdraw, defer and record the outcome: each from the item's `version`, so a stale page gets the API's 409. */
export function useMdtItemActions() {
  const onSuccess = useRefreshMdt();
  const path = (id: string) => ({path: {id}});
  return {
    withdraw: useMutation({
      mutationFn: ({id, expectedVersion}: {id: string; expectedVersion: number}) =>
        unwrap(api.POST('/api/mdt/items/{id}/withdraw', {params: path(id), body: {expectedVersion}})),
      onSuccess,
    }),
    defer: useMutation({
      mutationFn: ({id, meetingId, expectedVersion}: {id: string; meetingId: string; expectedVersion: number}) =>
        unwrap(api.POST('/api/mdt/items/{id}/defer', {params: path(id), body: {meetingId, expectedVersion}})),
      onSuccess,
    }),
    recordOutcome: useMutation({
      mutationFn: ({id, outcome, presentUserIds, expectedVersion}: {id: string; outcome: string; presentUserIds: string[]; expectedVersion: number}) =>
        unwrap(api.POST('/api/mdt/items/{id}/outcome', {params: path(id), body: {outcome, presentUserIds, expectedVersion}})),
      onSuccess,
    }),
  };
}
