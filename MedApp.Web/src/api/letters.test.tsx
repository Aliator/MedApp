import {QueryClient} from '@tanstack/react-query';
import {renderHook, waitFor} from '@testing-library/react';
import type {ReactNode} from 'react';
import {AppProviders} from '../app/AppProviders';
import {calls, json, mockApi} from '../test/render';
import {approvedLetter, LETTER_ID, LETTER_PATHWAY_ID, patientRecipient, practiceRecipient, testLetter} from '../test/letters';
import {ApiError} from './errors';
import {NO_PRACTICE_ID} from './practices';
import {
  PATIENTS_UNLISTED_PRACTICE,
  dispatchMethodWords,
  dispatchStatusWords,
  emptyRecipient,
  isDraftChanged,
  isReferrerTyped,
  letterCountQuery,
  letterDraftInput,
  letterHref,
  letterKeys,
  letterKindWords,
  letterPreviewUrl,
  letterQuery,
  lettersAboutQuery,
  letterStatusWords,
  letterViewLabel,
  needsLetterWords,
  recipientAddressLines,
  recipientInput,
  recipientKindWords,
  toRecipientRequest,
  useCreateLetter,
  useLetterActions,
} from './letters';

const RECIPIENT_ID = practiceRecipient().id!;

afterEach(() => {
  vi.unstubAllGlobals();
});

function hooks<T>(hook: () => T, queryClient = new QueryClient()) {
  const wrapper = ({children}: {children: ReactNode}) => <AppProviders queryClient={queryClient}>{children}</AppProviders>;
  return {queryClient, ...renderHook(hook, {wrapper})};
}

describe('letter words', () => {
  it('words kinds as the API does, with "Letter" for Other, and anything unknown as it came', () => {
    expect(letterKindWords('ClinicLetter')).toBe('Clinic letter');
    expect(letterKindWords('InformationRequest')).toBe('Request for more information');
    expect(letterKindWords('Other')).toBe('Letter');
    expect(letterKindWords('Telegram')).toBe('Telegram');
    expect(letterKindWords(null)).toBe('Letter');
  });

  it('words where a letter is', () => {
    expect(letterStatusWords({status: 'Draft', isReturned: false})).toBe('Draft');
    expect(letterStatusWords({status: 'Draft', isReturned: true})).toBe('Returned');
    expect(letterStatusWords({status: 'AwaitingApproval'})).toBe('Awaiting approval');
    expect(letterStatusWords({status: 'Approved', unsentCount: 1})).toBe('To send');
    expect(letterStatusWords({status: 'Approved', unsentCount: 0})).toBe('Approved');
    expect(letterStatusWords({status: 'Approved'})).toBe('Approved');
    expect(letterStatusWords({status: 'Withdrawn'})).toBe('Withdrawn');
    expect(letterStatusWords({status: 'Lost'})).toBe('Lost');
    expect(letterStatusWords({status: null})).toBe('');
  });

  it('words what a stage needs, with the right article', () => {
    expect(needsLetterWords('DischargeSummary')).toBe('Needs a discharge summary, approved and sent, before it can be finished.');
    expect(needsLetterWords('AppointmentLetter')).toBe('Needs an appointment letter, approved and sent, before it can be finished.');
    expect(needsLetterWords('None')).toBeNull();
    expect(needsLetterWords(null)).toBeNull();
  });

  it('words views, recipients and dispatch', () => {
    expect(letterViewLabel('ToSend')).toBe('To send');
    expect(letterViewLabel('Elsewhere' as never)).toBe('Elsewhere');
    expect(recipientKindWords('GpPractice')).toBe('GP practice');
    expect(recipientKindWords('Unknown')).toBe('Unknown');
    expect(recipientKindWords(null)).toBe('');
    expect(dispatchMethodWords('HandedToPatient')).toBe('Handed to the patient');
    expect(dispatchMethodWords('Pigeon')).toBe('Pigeon');
    expect(dispatchMethodWords(null)).toBe('');
    expect(dispatchStatusWords('NotSent')).toBe('Not sent');
    expect(dispatchStatusWords('Sent')).toBe('Sent');
    expect(dispatchStatusWords('Failed')).toBe('Failed');
    expect(dispatchStatusWords('Other')).toBe('Other');
    expect(dispatchStatusWords(null)).toBe('');
    expect(recipientAddressLines({addressLines: ' 1 Street \n\nTown', postcode: 'ZZ1 1ZZ'})).toEqual(['1 Street', 'Town', 'ZZ1 1ZZ']);
    expect(recipientAddressLines({addressLines: null, postcode: null})).toEqual([]);
    expect(letterHref('x')).toBe('/letters/x');
    expect(letterPreviewUrl('x')).toBe('/api/letters/x/preview');
  });
});

describe('recipients', () => {
  it('sends only what each kind takes', () => {
    expect(toRecipientRequest(recipientInput(practiceRecipient()))).toEqual({kind: 'GpPractice', practiceId: practiceRecipient().practiceId});
    // None chosen goes as the empty ID (the API asks for one from the directory); the patient's unlisted one, as none.
    expect(toRecipientRequest(emptyRecipient('GpPractice'))).toEqual({kind: 'GpPractice', practiceId: NO_PRACTICE_ID});
    expect(toRecipientRequest({...emptyRecipient('GpPractice'), practiceId: PATIENTS_UNLISTED_PRACTICE})).toEqual({kind: 'GpPractice', practiceId: null});
    expect(recipientInput({...practiceRecipient(), practiceId: null}).practiceId).toBe(PATIENTS_UNLISTED_PRACTICE);
    expect(toRecipientRequest(recipientInput(patientRecipient()))).toEqual({kind: 'Patient'});
    expect(toRecipientRequest({...emptyRecipient('Referrer'), practiceId: 'p'})).toEqual({kind: 'Referrer'});
    expect(toRecipientRequest({...emptyRecipient('Referrer'), addressLines: ' 1 Street ', postcode: ''})).toEqual({
      kind: 'Referrer',
      addressLines: '1 Street',
      postcode: null,
    });
    expect(toRecipientRequest({...emptyRecipient('Other'), name: ' Dr Example ', addressLines: '1 Street', postcode: 'ZZ1 1ZZ', email: ' '})).toEqual({
      kind: 'Other',
      name: 'Dr Example',
      addressLines: '1 Street',
      postcode: 'ZZ1 1ZZ',
      email: null,
    });
    // A hospital (MC28) is typed in as someone else is, and keeps its kind.
    expect(toRecipientRequest({...emptyRecipient('Hospital'), name: 'Riverside General', addressLines: '1 Way'})).toMatchObject({kind: 'Hospital', name: 'Riverside General'});
  });

  it('reads a saved recipient, giving an unknown kind as Other and a key of its own when it has no ID', () => {
    expect(recipientInput(patientRecipient())).toMatchObject({key: patientRecipient().id, kind: 'Patient', name: 'Freya Ahmed'});
    const odd = recipientInput({id: undefined, kind: 'Carrier'});
    expect(odd).toMatchObject({kind: 'Other', practiceId: '', name: '', addressLines: '', postcode: '', email: ''});
    expect(odd.key).toMatch(/^recipient-/);
    expect(isReferrerTyped({kind: 'Referrer', practiceId: ''})).toBe(true);
    expect(isReferrerTyped({kind: 'Referrer', practiceId: 'p'})).toBe(false);
    expect(isReferrerTyped({kind: 'Other', practiceId: ''})).toBe(false);
  });

  it('knows when the draft has changed', () => {
    const letter = testLetter();
    const input = letterDraftInput(letter);
    expect(isDraftChanged(letter, input)).toBe(false);
    expect(isDraftChanged(letter, {...input, body: `${input.body}!`})).toBe(true);
    expect(isDraftChanged(letter, {...input, recipients: input.recipients.slice(1)})).toBe(true);
    expect(letterDraftInput(testLetter({letterhead: null, body: null, closing: null, largePrint: undefined, recipients: null}))).toEqual({
      letterhead: '',
      body: '',
      closing: '',
      largePrint: false,
      recipients: [],
    });
  });
});

describe('letter queries', () => {
  it('fetch one letter, a search without its empty filters, and the count', async () => {
    const fetch = mockApi({
      [`GET /api/letters/${LETTER_ID}`]: () => json(testLetter()),
      'GET /api/letters/search': () => json({items: [], totalCount: 0}),
      'GET /api/letters/count': () => json({toApprove: 1, toSend: 2}),
    });
    const client = new QueryClient();

    expect((await client.fetchQuery(letterQuery(LETTER_ID))).id).toBe(LETTER_ID);
    await client.fetchQuery(lettersAboutQuery({pathwayId: LETTER_PATHWAY_ID}));
    expect(await client.fetchQuery(letterCountQuery)).toEqual({toApprove: 1, toSend: 2});

    expect(calls(fetch)).toEqual([
      `GET /api/letters/${LETTER_ID}`,
      `GET /api/letters/search?PathwayId=${LETTER_PATHWAY_ID}&PageSize=20`,
      'GET /api/letters/count',
    ]);
  });
});

describe('letter changes', () => {
  it('starts a letter, caching it and refreshing the lists', async () => {
    const fetch = mockApi({'POST /api/letters': () => json(testLetter(), 201)});
    const queryClient = new QueryClient();
    queryClient.setQueryData(letterKeys.count, {toApprove: 0, toSend: 0});
    const {result} = hooks(() => useCreateLetter(), queryClient);

    await result.current.mutateAsync({templateId: 't1', pathwayId: LETTER_PATHWAY_ID});

    expect(calls(fetch)).toEqual(['POST /api/letters']);
    expect(await (fetch.mock.calls[0][0] as Request).json()).toEqual({templateId: 't1', pathwayId: LETTER_PATHWAY_ID});
    expect(queryClient.getQueryData(letterKeys.one(LETTER_ID))).toMatchObject({id: LETTER_ID});
    await waitFor(() => expect(queryClient.getQueryState(letterKeys.count)?.isInvalidated).toBe(true));
  });

  it('sends each change to its endpoint, from the version shown', async () => {
    const returned = testLetter({version: 4, pathwayId: null});
    const fetch = mockApi({
      [`PUT /api/letters/${LETTER_ID}`]: () => json(returned),
      [`POST /api/letters/${LETTER_ID}/*`]: () => json(returned),
    });
    const {result, queryClient} = hooks(() => useLetterActions(LETTER_ID));
    const input = letterDraftInput(testLetter());

    await result.current.save.mutateAsync({expectedVersion: 3, input});
    await result.current.submit.mutateAsync({approverUserId: 'a1', expectedVersion: 4});
    await result.current.approve.mutateAsync({expectedVersion: 4});
    await result.current.returnToAuthor.mutateAsync({comment: 'Check the plan', expectedVersion: 4});
    await result.current.withdraw.mutateAsync({reason: 'Wrong plan', expectedVersion: 4});
    await result.current.sendSecureEmail.mutateAsync({recipientId: RECIPIENT_ID, expectedVersion: 4});
    await result.current.recordSent.mutateAsync({recipientId: RECIPIENT_ID, method: 'Post', sentOn: '2026-11-18', expectedVersion: 4});

    expect(calls(fetch)).toEqual([
      `PUT /api/letters/${LETTER_ID}`,
      `POST /api/letters/${LETTER_ID}/submit`,
      `POST /api/letters/${LETTER_ID}/approve`,
      `POST /api/letters/${LETTER_ID}/return`,
      `POST /api/letters/${LETTER_ID}/withdraw`,
      `POST /api/letters/${LETTER_ID}/recipients/${RECIPIENT_ID}/send`,
      `POST /api/letters/${LETTER_ID}/recipients/${RECIPIENT_ID}/record-sent`,
    ]);
    const bodies = await Promise.all(fetch.mock.calls.map(([request]) => (request as Request).json()));
    expect(bodies[0]).toEqual({
      letterhead: input.letterhead,
      body: input.body,
      closing: input.closing,
      largePrint: false,
      recipients: [{kind: 'GpPractice', practiceId: practiceRecipient().practiceId}, {kind: 'Patient'}],
      expectedVersion: 3,
    });
    expect(bodies[3]).toEqual({comment: 'Check the plan', expectedVersion: 4});
    expect(bodies[6]).toEqual({method: 'Post', sentOn: '2026-11-18', expectedVersion: 4});
    expect(queryClient.getQueryData(letterKeys.one(LETTER_ID))).toMatchObject({version: 4});
  });

  it('discards a draft, dropping it from the cache', async () => {
    mockApi({[`POST /api/letters/${LETTER_ID}/discard`]: () => json(testLetter())});
    const queryClient = new QueryClient();
    queryClient.setQueryData(letterKeys.one(LETTER_ID), testLetter());
    const {result} = hooks(() => useLetterActions(LETTER_ID), queryClient);

    await result.current.discard.mutateAsync({expectedVersion: 3});

    expect(queryClient.getQueryData(letterKeys.one(LETTER_ID))).toBeUndefined();
  });

  it('writes a correction, and reads the withdrawn letter again for its link', async () => {
    const correction = testLetter({id: 'c1', replacesLetterId: LETTER_ID});
    const fetch = mockApi({[`POST /api/letters/${LETTER_ID}/correction`]: () => json(correction, 201)});
    const queryClient = new QueryClient();
    queryClient.setQueryData(letterKeys.one(LETTER_ID), approvedLetter({status: 'Withdrawn'}));
    const {result} = hooks(() => useLetterActions(LETTER_ID), queryClient);

    expect(await result.current.correction.mutateAsync()).toEqual(correction);

    expect(calls(fetch)).toEqual([`POST /api/letters/${LETTER_ID}/correction`]);
    expect(queryClient.getQueryData(letterKeys.one('c1'))).toEqual(correction);
    await waitFor(() => expect(queryClient.getQueryState(letterKeys.one(LETTER_ID))?.isInvalidated).toBe(true));
  });

  it('reads the letter again after a 409, but not after another refusal', async () => {
    mockApi({
      [`POST /api/letters/${LETTER_ID}/approve`]: () => json({title: 'Someone changed it meanwhile.'}, 409),
      [`POST /api/letters/${LETTER_ID}/return`]: () => json({title: 'Not yours to return.'}, 403),
    });
    const queryClient = new QueryClient();
    queryClient.setQueryData(letterKeys.one(LETTER_ID), testLetter());
    const {result} = hooks(() => useLetterActions(LETTER_ID), queryClient);

    await expect(result.current.returnToAuthor.mutateAsync({comment: 'x', expectedVersion: 3})).rejects.toBeInstanceOf(ApiError);
    expect(queryClient.getQueryState(letterKeys.one(LETTER_ID))?.isInvalidated).toBe(false);
    await expect(result.current.approve.mutateAsync({expectedVersion: 3})).rejects.toBeInstanceOf(ApiError);
    await waitFor(() => expect(queryClient.getQueryState(letterKeys.one(LETTER_ID))?.isInvalidated).toBe(true));
  });
});
