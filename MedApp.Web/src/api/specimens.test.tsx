import {QueryClient} from '@tanstack/react-query';
import {act, renderHook, waitFor} from '@testing-library/react';
import type {ReactNode} from 'react';
import {AppProviders} from '../app/AppProviders';
import {calls, json, mockApi} from '../test/render';
import {
  BIOPSY_TYPE_ID,
  DOCTOR_USER_ID,
  LABORATORIES,
  NORTHGATE_LAB_ID,
  SPECIMEN_ID,
  SPECIMEN_PATHWAY_ID,
  SPECIMEN_STAGE_ID,
  SPECIMEN_TYPES,
  testSpecimen,
  testSummary,
} from '../test/specimens';
import {stubXhr} from '../test/xhr';
import {pathwayKeys} from './pathways';
import {
  laboratoriesQuery,
  pathwaySpecimensQuery,
  rejectionReasonLabel,
  specimenKeys,
  specimenPrintHref,
  specimenQuery,
  specimenSearchQuery,
  specimenSetupKeys,
  specimenStatusLabel,
  specimenTypesQuery,
  specimenViewLabel,
  SPECIMEN_VIEWS,
  useFindSpecimen,
  useLaboratoryActions,
  useRecordSpecimen,
  useSaveLaboratory,
  useSaveSpecimenType,
  useSpecimenActions,
  useSpecimenTypeActions,
} from './specimens';

const pageOf = <T,>(items: T[]) => ({items, page: 1, pageSize: 10, totalCount: items.length, totalPages: 1});

/** A query client holding a specimen, a lab view, a setup list and a timeline, to see what's refreshed. */
function cachedClient() {
  const queryClient = new QueryClient();
  queryClient.setQueryData(specimenKeys.one(SPECIMEN_ID), testSpecimen());
  queryClient.setQueryData(specimenKeys.search({View: 'Expected'}), pageOf([testSummary()]));
  queryClient.setQueryData(specimenSetupKeys.types(false), SPECIMEN_TYPES);
  queryClient.setQueryData([...pathwayKeys.timelines, 'patient'], {pages: []});
  const wrapper = ({children}: {children: ReactNode}) => <AppProviders queryClient={queryClient}>{children}</AppProviders>;
  return {queryClient, wrapper};
}

const isInvalidated = (queryClient: QueryClient, key: readonly unknown[]) => queryClient.getQueryState(key)?.isInvalidated;
const bodyOf = (fetch: ReturnType<typeof mockApi>, index = 0) => (fetch.mock.calls[index][0] as Request).json();

describe('specimen words', () => {
  it('names statuses, rejection reasons and views', () => {
    expect(specimenStatusLabel('RejectedByLab')).toBe('Rejected by the lab');
    expect(specimenStatusLabel('EnteredInError')).toBe('Entered in error');
    expect(specimenStatusLabel('Somewhere')).toBe('Somewhere');
    expect(specimenStatusLabel(null)).toBe('');
    expect(rejectionReasonLabel('InsufficientSample')).toBe('Insufficient sample');
    expect(rejectionReasonLabel('WrongContainer')).toBe('Wrong container');
    expect(rejectionReasonLabel(undefined)).toBe('');
    expect(SPECIMEN_VIEWS.map(specimenViewLabel)).toEqual(['To send', 'Expected', 'In the lab', 'Overdue', 'Rejected and lost', 'Reported']);
    expect(specimenPrintHref(SPECIMEN_ID, 'request')).toBe(`/specimens/${SPECIMEN_ID}/print?sheet=request`);
  });
});

describe('specimen queries', () => {
  it('fetches one specimen, a view of them and a pathway\'s, leaving empty filters out', async () => {
    const fetch = mockApi({
      [`GET /api/specimens/${SPECIMEN_ID}`]: () => json(testSpecimen()),
      'GET /api/specimens/search': () => json(pageOf([testSummary()])),
    });

    expect((await new QueryClient().fetchQuery(specimenQuery(SPECIMEN_ID))).number).toBe('MA26-000123');
    await new QueryClient().fetchQuery(specimenSearchQuery({View: 'InLab', LaboratoryId: undefined, Page: 2}));
    await new QueryClient().fetchQuery(pathwaySpecimensQuery(SPECIMEN_PATHWAY_ID));

    expect(calls(fetch)).toEqual([
      `GET /api/specimens/${SPECIMEN_ID}`,
      'GET /api/specimens/search?View=InLab&Page=2',
      `GET /api/specimens/search?View=All&PathwayId=${SPECIMEN_PATHWAY_ID}&PageSize=100`,
    ]);
  });

  it('fetches the setup lists, open ones only unless asked for archived ones too', async () => {
    const fetch = mockApi({'GET /api/specimen-types': () => json(SPECIMEN_TYPES), 'GET /api/laboratories': () => json(LABORATORIES)});

    expect(await new QueryClient().fetchQuery(specimenTypesQuery())).toEqual(SPECIMEN_TYPES);
    expect(await new QueryClient().fetchQuery(laboratoriesQuery(true))).toEqual(LABORATORIES);

    expect(calls(fetch)).toEqual(['GET /api/specimen-types?includeArchived=false', 'GET /api/laboratories?includeArchived=true']);
  });
});

describe('specimen changes', () => {
  it('records a specimen at a stage, sending blanks as none and now as nothing, then refreshes the lists', async () => {
    const recorded = testSpecimen({id: '00000051-0000-0000-0000-0000000000ff'});
    const fetch = mockApi({[`POST /api/pathways/${SPECIMEN_PATHWAY_ID}/specimens`]: () => json(recorded, 201)});
    const {queryClient, wrapper} = cachedClient();
    const {result} = renderHook(() => useRecordSpecimen(SPECIMEN_PATHWAY_ID), {wrapper});

    await act(() =>
      result.current.mutateAsync({
        stageId: SPECIMEN_STAGE_ID,
        specimenTypeId: BIOPSY_TYPE_ID,
        laboratoryId: NORTHGATE_LAB_ID,
        site: '  ',
        requestNotes: ' Urgent please ',
        requestingUserId: DOCTOR_USER_ID,
      }),
    );

    expect(await bodyOf(fetch)).toEqual({
      stageId: SPECIMEN_STAGE_ID,
      specimenTypeId: BIOPSY_TYPE_ID,
      laboratoryId: NORTHGATE_LAB_ID,
      site: null,
      requestNotes: 'Urgent please',
      requestingUserId: DOCTOR_USER_ID,
      takenAtUtc: null,
    });
    expect(queryClient.getQueryData(specimenKeys.one(recorded.id!))).toEqual(recorded);
    await waitFor(() => expect(isInvalidated(queryClient, specimenKeys.search({View: 'Expected'}))).toBe(true));
    expect(isInvalidated(queryClient, [...pathwayKeys.timelines, 'patient'])).toBe(true);
  });

  it('sends each step with the version shown, and puts what comes back in the cache', async () => {
    const sent = testSpecimen({status: 'Sent', version: 4});
    const fetch = mockApi({
      [`PUT /api/specimens/${SPECIMEN_ID}`]: () => json(testSpecimen({version: 4})),
      'POST /api/specimens/*': () => json(sent),
    });
    const {queryClient, wrapper} = cachedClient();
    const {result} = renderHook(() => useSpecimenActions(SPECIMEN_ID), {wrapper});

    await act(() =>
      result.current.edit.mutateAsync({
        specimenTypeId: BIOPSY_TYPE_ID,
        laboratoryId: NORTHGATE_LAB_ID,
        takenAtUtc: '2026-11-02T09:00:00Z',
        site: 'Left forearm',
        requestNotes: '',
        requestingUserId: DOCTOR_USER_ID,
        expectedVersion: 3,
      }),
    );
    await act(() => result.current.sent.mutateAsync({reference: ' CR-1 ', expectedVersion: 3}));
    await act(() => result.current.received.mutateAsync({atUtc: '2026-11-03T10:00:00.000Z', reference: '', expectedVersion: 4}));
    await act(() => result.current.rejected.mutateAsync({reason: 'Leaked', detail: '', expectedVersion: 5}));
    await act(() => result.current.lost.mutateAsync({detail: ' Not in the batch ', expectedVersion: 6}));
    await act(() => result.current.enteredInError.mutateAsync({detail: 'Wrong patient', expectedVersion: 7}));

    expect(calls(fetch)).toEqual([
      `PUT /api/specimens/${SPECIMEN_ID}`,
      `POST /api/specimens/${SPECIMEN_ID}/sent`,
      `POST /api/specimens/${SPECIMEN_ID}/received`,
      `POST /api/specimens/${SPECIMEN_ID}/rejected`,
      `POST /api/specimens/${SPECIMEN_ID}/lost`,
      `POST /api/specimens/${SPECIMEN_ID}/entered-in-error`,
    ]);
    expect(await bodyOf(fetch, 0)).toEqual({
      specimenTypeId: BIOPSY_TYPE_ID,
      laboratoryId: NORTHGATE_LAB_ID,
      site: 'Left forearm',
      requestNotes: null,
      requestingUserId: DOCTOR_USER_ID,
      takenAtUtc: '2026-11-02T09:00:00Z',
      expectedVersion: 3,
    });
    expect(await bodyOf(fetch, 1)).toEqual({atUtc: null, reference: 'CR-1', expectedVersion: 3});
    expect(await bodyOf(fetch, 2)).toEqual({atUtc: '2026-11-03T10:00:00.000Z', reference: null, expectedVersion: 4});
    expect(await bodyOf(fetch, 3)).toEqual({reason: 'Leaked', detail: null, expectedVersion: 5});
    expect(await bodyOf(fetch, 4)).toEqual({detail: 'Not in the batch', expectedVersion: 6});
    expect(await bodyOf(fetch, 5)).toEqual({detail: 'Wrong patient', expectedVersion: 7});
    expect(queryClient.getQueryData(specimenKeys.one(SPECIMEN_ID))).toEqual(sent);
  });

  it('reads the specimen again after a 409', async () => {
    mockApi({[`POST /api/specimens/${SPECIMEN_ID}/sent`]: () => json({title: 'Someone else changed this specimen.'}, 409)});
    const {queryClient, wrapper} = cachedClient();
    const {result} = renderHook(() => useSpecimenActions(SPECIMEN_ID), {wrapper});

    await act(() => result.current.sent.mutateAsync({reference: '', expectedVersion: 3}).catch(() => undefined));

    await waitFor(() => expect(isInvalidated(queryClient, specimenKeys.one(SPECIMEN_ID))).toBe(true));
  });

  it("attaches the report as the file's bytes, from the version shown, then refreshes the files too", async () => {
    const sent = stubXhr();
    const reported = testSpecimen({status: 'Reported', version: 5});
    const {queryClient, wrapper} = cachedClient();
    queryClient.setQueryData(['attachments', {patientId: 'p'}], {items: []});
    const {result} = renderHook(() => useSpecimenActions(SPECIMEN_ID), {wrapper});
    const onProgress = vi.fn();

    let done: Promise<unknown> = Promise.resolve();
    act(() => {
      done = result.current.report.mutateAsync({file: new File(['x'], 'report ü.pdf'), expectedVersion: 4, onProgress});
    });
    await waitFor(() => expect(sent).toHaveLength(1));
    expect(sent[0].method).toBe('POST');
    expect(sent[0].url).toBe(`/api/specimens/${SPECIMEN_ID}/report?expectedVersion=4`);
    expect(sent[0].headers).toEqual({'Content-Type': 'application/octet-stream', 'X-File-Name': 'report%20%C3%BC.pdf'});
    sent[0].respond(200, reported);
    await act(() => done);

    expect(onProgress).toHaveBeenCalledWith(1);
    expect(queryClient.getQueryData(specimenKeys.one(SPECIMEN_ID))).toEqual(reported);
    await waitFor(() => expect(isInvalidated(queryClient, ['attachments', {patientId: 'p'}])).toBe(true));
  });

  it('finds a specimen by its number, in the body, and keeps it for its page', async () => {
    const fetch = mockApi({'POST /api/specimens/find': () => json(testSpecimen())});
    const {queryClient, wrapper} = cachedClient();
    queryClient.removeQueries({queryKey: specimenKeys.one(SPECIMEN_ID)});
    const {result} = renderHook(() => useFindSpecimen(), {wrapper});

    await act(() => result.current.mutateAsync(' ma26-000123 '));

    expect(calls(fetch)).toEqual(['POST /api/specimens/find']);
    expect(await bodyOf(fetch)).toEqual({number: 'ma26-000123'});
    expect(queryClient.getQueryData(specimenKeys.one(SPECIMEN_ID))).toEqual(testSpecimen());
  });
});

describe('specimen setup changes', () => {
  it('adds and changes types and labs, then refreshes the lists and the specimens', async () => {
    const fetch = mockApi({
      'POST /api/specimen-types': () => json(SPECIMEN_TYPES[0], 201),
      [`PUT /api/specimen-types/${BIOPSY_TYPE_ID}`]: () => json(SPECIMEN_TYPES[0]),
      'POST /api/laboratories': () => json(LABORATORIES[0], 201),
      [`PUT /api/laboratories/${NORTHGATE_LAB_ID}`]: () => json(LABORATORIES[0]),
    });
    const {queryClient, wrapper} = cachedClient();
    const {result} = renderHook(() => ({type: useSaveSpecimenType(), lab: useSaveLaboratory()}), {wrapper});

    await act(() => result.current.type.mutateAsync({name: 'Skin biopsy (punch)', expectedTurnaroundDays: 5}));
    await act(() => result.current.type.mutateAsync({id: BIOPSY_TYPE_ID, name: 'Skin biopsy', expectedTurnaroundDays: 6}));
    await act(() => result.current.lab.mutateAsync({name: 'Northgate pathology', usesMedApp: true}));
    await act(() => result.current.lab.mutateAsync({id: NORTHGATE_LAB_ID, name: 'Northgate', usesMedApp: false}));

    expect(calls(fetch)).toEqual([
      'POST /api/specimen-types',
      `PUT /api/specimen-types/${BIOPSY_TYPE_ID}`,
      'POST /api/laboratories',
      `PUT /api/laboratories/${NORTHGATE_LAB_ID}`,
    ]);
    expect(await bodyOf(fetch, 1)).toEqual({name: 'Skin biopsy', expectedTurnaroundDays: 6});
    expect(await bodyOf(fetch, 3)).toEqual({name: 'Northgate', usesMedApp: false});
    await waitFor(() => expect(isInvalidated(queryClient, specimenSetupKeys.types(false))).toBe(true));
    expect(isInvalidated(queryClient, specimenKeys.one(SPECIMEN_ID))).toBe(true);
  });

  it('archives, restores and deletes types and labs', async () => {
    const fetch = mockApi({
      'POST /api/specimen-types/*': () => json(SPECIMEN_TYPES[0]),
      'DELETE /api/specimen-types/*': () => new Response(null, {status: 204}),
      'POST /api/laboratories/*': () => json(LABORATORIES[0]),
      'DELETE /api/laboratories/*': () => new Response(null, {status: 204}),
    });
    const {wrapper} = cachedClient();
    const {result} = renderHook(() => ({type: useSpecimenTypeActions(), lab: useLaboratoryActions()}), {wrapper});

    await act(() => result.current.type.archive.mutateAsync('t1'));
    await act(() => result.current.type.restore.mutateAsync('t1'));
    await act(() => result.current.type.remove.mutateAsync('t1'));
    await act(() => result.current.lab.archive.mutateAsync('l1'));
    await act(() => result.current.lab.restore.mutateAsync('l1'));
    await act(() => result.current.lab.remove.mutateAsync('l1'));

    expect(calls(fetch)).toEqual([
      'POST /api/specimen-types/t1/archive',
      'POST /api/specimen-types/t1/restore',
      'DELETE /api/specimen-types/t1',
      'POST /api/laboratories/l1/archive',
      'POST /api/laboratories/l1/restore',
      'DELETE /api/laboratories/l1',
    ]);
  });
});
