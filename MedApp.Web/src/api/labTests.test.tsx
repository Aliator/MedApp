import {QueryClient} from '@tanstack/react-query';
import {act, renderHook, waitFor} from '@testing-library/react';
import type {ReactNode} from 'react';
import {AppProviders} from '../app/AppProviders';
import {calls, json, mockApi} from '../test/render';
import {HISTOLOGY_TEST_ID, LAB_TESTS} from '../test/specimens';
import {
  LAB_STEP_KINDS,
  LAB_TEST_KINDS,
  labStepKindLabel,
  labTestKeys,
  labTestKindLabel,
  labTestsQuery,
  labTestTimesWords,
  useLabTestActions,
  useSaveLabTest,
} from './labTests';

function cachedClient() {
  const queryClient = new QueryClient();
  queryClient.setQueryData(labTestKeys.list(true), LAB_TESTS);
  const wrapper = ({children}: {children: ReactNode}) => <AppProviders queryClient={queryClient}>{children}</AppProviders>;
  return {queryClient, wrapper};
}

const bodyOf = (fetch: ReturnType<typeof mockApi>, index = 0) => (fetch.mock.calls[index][0] as Request).json();

describe('lab test words', () => {
  it('names kinds, step kinds and time limits', () => {
    expect(LAB_TEST_KINDS.map(labTestKindLabel)).toEqual(['Histology', 'Cytology', 'Haematology', 'Clinical chemistry', 'Microbiology', 'Other']);
    expect(LAB_STEP_KINDS.map(labStepKindLabel)).toEqual(['Standard', 'Extra tests', 'Sent for a second opinion', 'Second opinion back']);
    expect(labTestKindLabel('Somewhere')).toBe('Somewhere');
    expect(labStepKindLabel(null)).toBe('');
    expect(labTestTimesWords(LAB_TESTS[0])).toBe('Target 7 days, limit 10, at risk 2 days before');
    expect(labTestTimesWords({targetDays: 1, limitDays: 2, atRiskDays: 1})).toBe('Target 1 day, limit 2, at risk 1 day before');
  });
});

describe('lab test queries and changes', () => {
  it('fetches the catalogue, open tests only unless asked for archived ones too', async () => {
    const fetch = mockApi({'GET /api/lab-tests': () => json(LAB_TESTS)});

    expect(await new QueryClient().fetchQuery(labTestsQuery())).toEqual(LAB_TESTS);
    await new QueryClient().fetchQuery(labTestsQuery(true));

    expect(calls(fetch)).toEqual(['GET /api/lab-tests?includeArchived=false', 'GET /api/lab-tests?includeArchived=true']);
  });

  it('adds and changes a test with its steps in order, then refreshes the list', async () => {
    const fetch = mockApi({
      'POST /api/lab-tests': () => json(LAB_TESTS[0], 201),
      [`PUT /api/lab-tests/${HISTOLOGY_TEST_ID}`]: () => json(LAB_TESTS[0]),
    });
    const {queryClient, wrapper} = cachedClient();
    const {result} = renderHook(() => useSaveLabTest(), {wrapper});
    const input = {
      name: 'Skin histology',
      kind: 'Histology' as const,
      snomedCode: null,
      targetDays: 7,
      limitDays: 10,
      atRiskDays: 2,
      steps: [{name: 'Cut-up', kind: 'Standard' as const, isOptional: false}],
    };

    await act(() => result.current.mutateAsync(input));
    await act(() => result.current.mutateAsync({...input, id: HISTOLOGY_TEST_ID, limitDays: 12}));

    expect(calls(fetch)).toEqual(['POST /api/lab-tests', `PUT /api/lab-tests/${HISTOLOGY_TEST_ID}`]);
    expect(await bodyOf(fetch, 0)).toEqual(input);
    expect(await bodyOf(fetch, 1)).toEqual({...input, limitDays: 12});
    await waitFor(() => expect(queryClient.getQueryState(labTestKeys.list(true))?.isInvalidated).toBe(true));
  });

  it('archives, restores and deletes a test', async () => {
    const fetch = mockApi({
      'POST /api/lab-tests/*': () => json(LAB_TESTS[1]),
      'DELETE /api/lab-tests/*': () => new Response(null, {status: 204}),
    });
    const {wrapper} = cachedClient();
    const {result} = renderHook(() => useLabTestActions(), {wrapper});

    await act(() => result.current.archive.mutateAsync('t1'));
    await act(() => result.current.restore.mutateAsync('t1'));
    await act(() => result.current.remove.mutateAsync('t1'));

    expect(calls(fetch)).toEqual(['POST /api/lab-tests/t1/archive', 'POST /api/lab-tests/t1/restore', 'DELETE /api/lab-tests/t1']);
  });
});
