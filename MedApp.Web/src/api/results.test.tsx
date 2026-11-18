import {QueryClient} from '@tanstack/react-query';
import {act, renderHook, waitFor} from '@testing-library/react';
import type {ReactNode} from 'react';
import {AppProviders} from '../app/AppProviders';
import {calls, json, mockApi} from '../test/render';
import {RESULT_ID, RESULT_PATHWAY_ID, RESULT_REPORT_ID, SENIOR_USER_ID, testResult, testResultSummary} from '../test/results';
import {pathwayKeys} from './pathways';
import {
  pathwayResultsQuery,
  RESULT_ACTIONS,
  RESULT_VIEWS,
  resultActionLabel,
  resultCountQuery,
  resultHref,
  resultKeys,
  resultQuery,
  resultSearchQuery,
  resultSourceLabel,
  resultStatusLabel,
  resultViewLabel,
  useRecordExternalResult,
  useResultActions,
} from './results';
import {specimenKeys} from './specimens';

const pageOf = <T,>(items: T[]) => ({items, page: 1, pageSize: 10, totalCount: items.length, totalPages: 1});

/** A query client holding a result, a list, the count, a pathway, a specimen, files and a timeline, to see what's refreshed. */
function cachedClient() {
  const queryClient = new QueryClient();
  queryClient.setQueryData(resultKeys.one(RESULT_ID), testResult());
  queryClient.setQueryData(resultKeys.search({View: 'AwaitingMine'}), pageOf([testResultSummary()]));
  queryClient.setQueryData(resultKeys.count, {awaitingMine: 1});
  queryClient.setQueryData(['pathways', RESULT_PATHWAY_ID], {id: RESULT_PATHWAY_ID});
  queryClient.setQueryData(specimenKeys.one('s'), {id: 's'});
  queryClient.setQueryData(['attachments', {patientId: 'p'}], {items: []});
  queryClient.setQueryData([...pathwayKeys.timelines, 'patient'], {pages: []});
  const wrapper = ({children}: {children: ReactNode}) => <AppProviders queryClient={queryClient}>{children}</AppProviders>;
  return {queryClient, wrapper};
}

const isInvalidated = (queryClient: QueryClient, key: readonly unknown[]) => queryClient.getQueryState(key)?.isInvalidated;
const bodyOf = (fetch: ReturnType<typeof mockApi>, index = 0) => (fetch.mock.calls[index][0] as Request).json();

describe('result words', () => {
  it('names statuses, actions in their fixed order, sources and views', () => {
    expect(resultStatusLabel('AwaitingSignOff')).toBe('Awaiting sign-off');
    expect(resultStatusLabel('SignedOff')).toBe('Signed off');
    expect(resultStatusLabel('EnteredInError')).toBe('Entered in error');
    expect(resultStatusLabel('Somewhere')).toBe('Somewhere');
    expect(resultStatusLabel(null)).toBe('');
    expect(RESULT_ACTIONS.map((a) => a.label)).toEqual([
      'No further action',
      'Patient informed',
      'Appointment arranged',
      'To be discussed at MDT',
      'Referred on',
      'Other',
    ]);
    expect(resultActionLabel('ForMdtDiscussion')).toBe('To be discussed at MDT');
    expect(resultActionLabel(undefined)).toBe('');
    expect(resultSourceLabel('Specimen')).toBe('Specimen report');
    expect(resultSourceLabel('ExternalReport')).toBe('External report');
    expect(RESULT_VIEWS.map((v) => resultViewLabel(v.value))).toEqual([
      'Awaiting my sign-off',
      'Awaiting sign-off (everyone)',
      'Needs a clinician',
      'Signed off',
    ]);
    expect(resultHref(RESULT_ID)).toBe(`/results/${RESULT_ID}`);
  });
});

describe('result queries', () => {
  it("fetches one result, a view of them, a pathway's and the count, leaving empty filters out", async () => {
    const fetch = mockApi({
      [`GET /api/results/${RESULT_ID}`]: () => json(testResult()),
      'GET /api/results/search': () => json(pageOf([testResultSummary()])),
      'GET /api/results/count': () => json({awaitingMine: 4}),
    });

    expect((await new QueryClient().fetchQuery(resultQuery(RESULT_ID))).title).toBe('Skin biopsy (punch) report');
    await new QueryClient().fetchQuery(resultSearchQuery({View: 'Awaiting', TeamId: undefined, Page: 2}));
    await new QueryClient().fetchQuery(pathwayResultsQuery(RESULT_PATHWAY_ID));
    expect((await new QueryClient().fetchQuery(resultCountQuery)).awaitingMine).toBe(4);

    expect(calls(fetch)).toEqual([
      `GET /api/results/${RESULT_ID}`,
      'GET /api/results/search?View=Awaiting&Page=2',
      `GET /api/results/search?View=All&PathwayId=${RESULT_PATHWAY_ID}&PageSize=100`,
      'GET /api/results/count',
    ]);
  });
});

describe('result changes', () => {
  it('records an external result, trimming the title, then refreshes the lists, the count and its pathway', async () => {
    const recorded = testResult({id: '00000061-0000-0000-0000-0000000000ff', source: 'ExternalReport'});
    const fetch = mockApi({'POST /api/results/external': () => json(recorded, 201)});
    const {queryClient, wrapper} = cachedClient();
    const {result} = renderHook(() => useRecordExternalResult(), {wrapper});

    await act(() =>
      result.current.mutateAsync({attachmentId: RESULT_REPORT_ID, title: ' CT chest report ', pathwayId: RESULT_PATHWAY_ID, responsibleUserId: SENIOR_USER_ID}),
    );

    expect(await bodyOf(fetch)).toEqual({
      attachmentId: RESULT_REPORT_ID,
      title: 'CT chest report',
      pathwayId: RESULT_PATHWAY_ID,
      responsibleUserId: SENIOR_USER_ID,
    });
    expect(queryClient.getQueryData(resultKeys.one(recorded.id!))).toEqual(recorded);
    await waitFor(() => expect(isInvalidated(queryClient, resultKeys.count)).toBe(true));
    expect(isInvalidated(queryClient, resultKeys.search({View: 'AwaitingMine'}))).toBe(true);
    expect(isInvalidated(queryClient, ['pathways', RESULT_PATHWAY_ID])).toBe(true);
    expect(isInvalidated(queryClient, [...pathwayKeys.timelines, 'patient'])).toBe(true);
  });

  it('sends each change with the version shown, and puts what comes back in the cache', async () => {
    const signedOff = testResult({status: 'SignedOff', version: 5});
    const fetch = mockApi({'POST /api/results/*': () => json(signedOff)});
    const {queryClient, wrapper} = cachedClient();
    const {result} = renderHook(() => useResultActions(RESULT_ID), {wrapper});

    await act(() => result.current.seen.mutateAsync({expectedVersion: 2}));
    await act(() => result.current.signOff.mutateAsync({action: 'NoFurtherAction', detail: '  ', expectedVersion: 3}));
    await act(() => result.current.signOff.mutateAsync({action: 'Other', detail: ' Phoned the GP ', expectedVersion: 3}));
    await act(() => result.current.reassign.mutateAsync({responsibleUserId: SENIOR_USER_ID, expectedVersion: 4}));

    expect(calls(fetch)).toEqual([
      `POST /api/results/${RESULT_ID}/seen`,
      `POST /api/results/${RESULT_ID}/sign-off`,
      `POST /api/results/${RESULT_ID}/sign-off`,
      `POST /api/results/${RESULT_ID}/reassign`,
    ]);
    expect(await bodyOf(fetch, 0)).toEqual({expectedVersion: 2});
    expect(await bodyOf(fetch, 1)).toEqual({action: 'NoFurtherAction', detail: null, expectedVersion: 3});
    expect(await bodyOf(fetch, 2)).toEqual({action: 'Other', detail: 'Phoned the GP', expectedVersion: 3});
    expect(await bodyOf(fetch, 3)).toEqual({responsibleUserId: SENIOR_USER_ID, expectedVersion: 4});
    expect(queryClient.getQueryData(resultKeys.one(RESULT_ID))).toEqual(signedOff);
    await waitFor(() => expect(isInvalidated(queryClient, resultKeys.count)).toBe(true));
  });

  it('marks it entered in error, then refreshes the files and the specimens too', async () => {
    const fetch = mockApi({[`POST /api/results/${RESULT_ID}/entered-in-error`]: () => json(testResult({status: 'EnteredInError', reportFileName: null}))});
    const {queryClient, wrapper} = cachedClient();
    const {result} = renderHook(() => useResultActions(RESULT_ID), {wrapper});

    await act(() => result.current.enteredInError.mutateAsync({detail: ' Wrong patient ', expectedVersion: 2}));

    expect(await bodyOf(fetch)).toEqual({detail: 'Wrong patient', expectedVersion: 2});
    expect(isInvalidated(queryClient, ['attachments', {patientId: 'p'}])).toBe(true);
    expect(isInvalidated(queryClient, specimenKeys.one('s'))).toBe(true);
  });

  it('reads the result again after a 409', async () => {
    mockApi({[`POST /api/results/${RESULT_ID}/seen`]: () => json({title: 'Someone else changed this result.'}, 409)});
    const {queryClient, wrapper} = cachedClient();
    const {result} = renderHook(() => useResultActions(RESULT_ID), {wrapper});

    await act(() => result.current.seen.mutateAsync({expectedVersion: 2}).catch(() => undefined));

    await waitFor(() => expect(isInvalidated(queryClient, resultKeys.one(RESULT_ID))).toBe(true));
  });
});
