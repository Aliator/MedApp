import {queryOptions, useMutation, useQueryClient} from '@tanstack/react-query';
import {api, unwrap, type Schemas} from './client';

/**
 * Consults (MC25 of `docs/multi-clinic-plan.md`): a pathway's team asks another team (a clinic or the lab) a question.
 * While it's open the asked team reads the pathway (a read-only `Consult` grant); their reply is kept on the pathway, and
 * only the asking team closes it (or withdraws it before a reply), which ends the grant. **MedApp never suggests a team
 * or a reply:** nothing here is prefilled. The audit log and notifications hold no question or reply.
 */
export type Consult = Schemas['ConsultResponse'];
export type PathwayConsults = Schemas['PathwayConsultsResponse'];

/** The most a question can hold, as the API allows. */
export const MAX_CONSULT_QUESTION_LENGTH = 2000;
/** The most a reply can hold, as the API allows. */
export const MAX_CONSULT_REPLY_LENGTH = 4000;

/** Every consult query is under ['consults'], so any change refreshes them all. */
export const consultKeys = {
  all: ['consults'] as const,
  pathway: (pathwayId: string) => ['consults', 'pathway', pathwayId] as const,
  incoming: (teamId: string | undefined) => ['consults', 'incoming', teamId ?? null] as const,
};

/** A pathway's consults, newest asked first, and whether the viewer may ask one. */
export function pathwayConsultsQuery(pathwayId: string) {
  return queryOptions({
    queryKey: consultKeys.pathway(pathwayId),
    queryFn: () => unwrap(api.GET('/api/consults/pathways/{pathwayId}', {params: {path: {pathwayId}}})),
  });
}

/** Open consults asked of the viewer's teams (or one of them), oldest asked first: what's waiting for a reply or to be closed. */
export function incomingConsultsQuery(teamId?: string) {
  return queryOptions({
    queryKey: consultKeys.incoming(teamId),
    queryFn: () => unwrap(api.GET('/api/consults/incoming', {params: {query: {teamId}}})),
  });
}

/** What a consult's status is called. */
const STATUS_WORDS: Record<string, string> = {
  Open: 'Awaiting reply',
  Replied: 'Replied',
  Closed: 'Closed',
  Withdrawn: 'Withdrawn',
};

export const consultStatusWords = (status: string | null | undefined) => STATUS_WORDS[status ?? ''] ?? status ?? '';

/** Whether a consult is still open (the asked team reads the pathway): awaiting a reply, or replied and not yet closed. */
export const isOpenConsult = (consult: Pick<Consult, 'status'>) => consult.status === 'Open' || consult.status === 'Replied';

/** Refreshes every consult list, and the pathways (a consult opens or ends the asked team's reading). */
function useRefreshConsults() {
  const queryClient = useQueryClient();
  return async () => {
    await Promise.all([
      queryClient.invalidateQueries({queryKey: consultKeys.all}),
      queryClient.invalidateQueries({queryKey: ['pathways']}),
    ]);
  };
}

/** Asks another team about a pathway: the team (none chosen for you) and the question (2,000 characters at most). */
export function useRequestConsult() {
  const onSuccess = useRefreshConsults();
  return useMutation({
    mutationFn: (body: {pathwayId: string; askedTeamId: string; question: string}) => unwrap(api.POST('/api/consults', {body})),
    onSuccess,
  });
}

/** Reply (the asked team), close and withdraw (the asking team): each from the consult's `version`, so a stale page gets the API's 409. */
export function useConsultActions() {
  const onSuccess = useRefreshConsults();
  const path = (id: string) => ({path: {id}});
  return {
    reply: useMutation({
      mutationFn: ({id, reply, expectedVersion}: {id: string; reply: string; expectedVersion: number}) =>
        unwrap(api.POST('/api/consults/{id}/reply', {params: path(id), body: {reply, expectedVersion}})),
      onSuccess,
    }),
    close: useMutation({
      mutationFn: ({id, expectedVersion}: {id: string; expectedVersion: number}) =>
        unwrap(api.POST('/api/consults/{id}/close', {params: path(id), body: {expectedVersion}})),
      onSuccess,
    }),
    withdraw: useMutation({
      mutationFn: ({id, expectedVersion}: {id: string; expectedVersion: number}) =>
        unwrap(api.POST('/api/consults/{id}/withdraw', {params: path(id), body: {expectedVersion}})),
      onSuccess,
    }),
  };
}
