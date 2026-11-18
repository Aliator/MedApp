import {QueryClient} from '@tanstack/react-query';
import {calls, json, mockApi} from '../test/render';
import {
  awaitingTriageCountQuery,
  openFirst,
  patientOpenReferralsQuery,
  patientReferralsQuery,
  referralEventWords,
  referralFieldWords,
  referralInput,
  referralQuery,
  referralSearchQuery,
  referralStatusLabel,
  rejectionNeedsDetail,
  rejectionReasonWords,
  toReferralRequest,
  type ReferralEvent,
  type ReferralSummary,
} from './referrals';

const referralId = '00000041-0000-0000-0000-000000000001';
const patientId = '00000041-0000-0000-0000-000000000002';

describe('referral queries', () => {
  it('fetch one referral, a search without its empty filters, and the count', async () => {
    const fetch = mockApi({
      [`GET /api/referrals/${referralId}`]: () => json({id: referralId}),
      'GET /api/referrals/search': () => json({items: [], totalCount: 0}),
      'GET /api/referrals/awaiting-triage/count': () => json({count: 3, inYourSpecialties: true}),
    });
    const client = new QueryClient();

    expect(await client.fetchQuery(referralQuery(referralId))).toEqual({id: referralId});
    await client.fetchQuery(referralSearchQuery({Page: 1, Search: '', Statuses: [], SpecialtyIds: undefined, PatientId: patientId}));
    expect(await client.fetchQuery(awaitingTriageCountQuery)).toEqual({count: 3, inYourSpecialties: true});
    await client.fetchQuery(patientReferralsQuery(patientId));
    await client.fetchQuery(patientOpenReferralsQuery(patientId));

    expect(calls(fetch)).toEqual([
      `GET /api/referrals/${referralId}`,
      `GET /api/referrals/search?Page=1&PatientId=${patientId}`,
      'GET /api/referrals/awaiting-triage/count',
      `GET /api/referrals/search?PatientId=${patientId}&Sort=registered&Ascending=false&PageSize=20`,
      `GET /api/referrals/search?PatientId=${patientId}&Statuses=AwaitingTriage&PageSize=20`,
    ]);
  });
});

describe('referral words', () => {
  it('names statuses and fields', () => {
    expect(referralStatusLabel('AwaitingTriage')).toBe('Awaiting triage');
    expect(referralStatusLabel('RedirectedElsewhere')).toBe('Redirected elsewhere');
    expect(referralStatusLabel('EnteredInError')).toBe('Entered in error');
    expect(referralStatusLabel(null)).toBe('');
    expect(referralFieldWords('receivedOn')).toBe('received date');
    expect(referralFieldWords('somethingNew')).toBe('somethingNew');
    expect(referralFieldWords('directoryPractice')).toBe('practice from the directory');
  });

  const event = (fields: Partial<ReferralEvent>): ReferralEvent => ({id: 'e', atUtc: '2026-11-02T10:00:00Z', username: 'k.ashford', changedFields: [], ...fields});

  it('words each history entry', () => {
    expect(referralEventWords(event({action: 'Registered', specialtyName: 'Dermatology', requestedPriority: 'High'}))).toEqual({
      title: 'Registered by k.ashford',
      detail: 'To Dermatology, High priority asked for',
    });
    expect(referralEventWords(event({action: 'Registered'}))).toEqual({title: 'Registered by k.ashford', detail: ''});
    expect(
      referralEventWords(
        event({action: 'DetailsEdited', changedFields: ['reason', 'receivedOn', 'specialty'], specialtyName: 'Urology', previousSpecialtyName: 'Dermatology'}),
      ),
    ).toEqual({title: 'Details edited by k.ashford: reason, received date, specialty', detail: 'Specialty Dermatology → Urology'});
    expect(referralEventWords(event({action: 'DetailsEdited', changedFields: null}))).toEqual({title: 'Details edited by k.ashford', detail: ''});
    expect(referralEventWords(event({action: 'Withdrawn'}))).toEqual({title: 'Withdrawn by k.ashford', detail: ''});
    expect(referralEventWords(event({action: 'EnteredInError', username: ''}))).toEqual({title: 'Marked entered in error by someone', detail: ''});
    expect(referralEventWords(event({action: 'Accepted', toStatus: 'Accepted', templateName: 'Skin check', priority: 'High'}))).toEqual({
      title: 'Accepted by k.ashford',
      detail: 'Skin check, High priority',
    });
    expect(referralEventWords(event({action: 'Accepted'}))).toEqual({title: 'Accepted by k.ashford', detail: ''});
    expect(referralEventWords(event({action: 'Rejected', rejectionReason: 'InsufficientInformation'}))).toEqual({
      title: 'Rejected by k.ashford',
      detail: 'Insufficient information',
    });
    expect(referralEventWords(event({action: 'SpecialtyChanged', previousSpecialtyName: 'Urology', specialtyName: 'Dermatology'}))).toEqual({
      title: 'Redirected by k.ashford',
      detail: 'Specialty Urology → Dermatology',
    });
    expect(referralEventWords(event({action: 'SpecialtyChanged', specialtyName: 'Dermatology'}))).toEqual({
      title: 'Redirected by k.ashford',
      detail: 'Specialty unknown → Dermatology',
    });
    expect(referralEventWords(event({action: 'SpecialtyChanged'}))).toEqual({title: 'Redirected by k.ashford', detail: ''});
    expect(referralEventWords(event({action: 'RedirectedElsewhere'}))).toEqual({title: 'Redirected elsewhere by k.ashford', detail: ''});
    expect(referralEventWords(event({action: 'TargetChanged', toStatus: 'AwaitingTriage'}))).toEqual({
      title: 'Target changed by k.ashford',
      detail: 'Now awaiting triage',
    });
    expect(referralEventWords(event({action: null}))).toEqual({title: 'Changed by k.ashford', detail: ''});
  });
});

describe('rejection reasons', () => {
  it('words each reason, and needs a detail only for Other', () => {
    expect(rejectionReasonWords('NotOfferedHere')).toBe('Not offered here');
    expect(rejectionReasonWords('DoesNotMeetCriteria')).toBe('Does not meet the criteria');
    expect(rejectionReasonWords('Duplicate')).toBe('Duplicate');
    expect(rejectionReasonWords('Unknown')).toBe('Unknown');
    expect(rejectionReasonWords(null)).toBe('');
    expect(rejectionNeedsDetail('Other')).toBe(true);
    expect(rejectionNeedsDetail('Duplicate')).toBe(false);
  });
});

describe('referral details', () => {
  it('sends blank optional fields as null, the ODS code in capitals, and trims', () => {
    expect(
      toReferralRequest({
        specialtyId: 's',
        receivedOn: '2026-11-02',
        practiceId: 'p1',
        referrerPractice: ' Synthetic Surgery ',
        referrerClinician: '  ',
        referrerOdsCode: 'a12bc',
        requestedPriority: 'High',
        reason: ' Reason ',
      }),
    ).toEqual({
      specialtyId: 's',
      receivedOn: '2026-11-02',
      practiceId: 'p1',
      referrerPractice: 'Synthetic Surgery',
      referrerClinician: null,
      referrerOdsCode: 'A12BC',
      requestedPriority: 'High',
      reason: 'Reason',
    });
    expect(toReferralRequest({...referralInput({}), specialtyId: ''})).toMatchObject({specialtyId: undefined, receivedOn: undefined, practiceId: null});
  });

  it('starts an edit from the referral, with Medium when it has no priority', () => {
    expect(referralInput({specialtyId: 's', receivedOn: '2026-11-02', practiceId: 'p1', referrerPractice: 'P', referrerClinician: 'C', referrerOdsCode: 'A1B', requestedPriority: 'Low', reason: 'R'})).toEqual({
      specialtyId: 's',
      receivedOn: '2026-11-02',
      practiceId: 'p1',
      referrerPractice: 'P',
      referrerClinician: 'C',
      referrerOdsCode: 'A1B',
      requestedPriority: 'Low',
      reason: 'R',
    });
    expect(referralInput({}).requestedPriority).toBe('Medium');
  });

  it('puts open referrals first, oldest received first', () => {
    const r = (id: string, status: string, receivedOn: string): ReferralSummary => ({id, status, receivedOn});
    expect(
      openFirst([r('a', 'Withdrawn', '2026-01-01'), r('b', 'AwaitingTriage', '2026-03-01'), r('c', 'AwaitingTriage', '2026-02-01'), r('d', 'Accepted', '2026-04-01')]).map(
        (x) => x.id,
      ),
    ).toEqual(['c', 'b', 'a', 'd']);
  });
});
