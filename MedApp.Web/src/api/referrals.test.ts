import {QueryClient} from '@tanstack/react-query';
import {calls, json, mockApi} from '../test/render';
import {
  awaitingTriageCountQuery,
  isOpenReferralStatus,
  CLOCK_PAUSE_REASONS,
  openFirst,
  pauseNeedsDetail,
  pauseReasonWords,
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
  stopReasonWords,
  toReferralRequest,
  waitWords,
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
      'GET /api/referrals/awaiting-triage/count': () => json({count: 3, inYourTeams: true}),
    });
    const client = new QueryClient();

    expect(await client.fetchQuery(referralQuery(referralId))).toEqual({id: referralId});
    await client.fetchQuery(referralSearchQuery({Page: 1, Search: '', Statuses: [], TeamIds: undefined, PatientId: patientId}));
    expect(await client.fetchQuery(awaitingTriageCountQuery)).toEqual({count: 3, inYourTeams: true});
    await client.fetchQuery(patientReferralsQuery(patientId));
    await client.fetchQuery(patientOpenReferralsQuery(patientId));

    expect(calls(fetch)).toEqual([
      `GET /api/referrals/${referralId}`,
      `GET /api/referrals/search?Page=1&PatientId=${patientId}`,
      'GET /api/referrals/awaiting-triage/count',
      `GET /api/referrals/search?PatientId=${patientId}&Sort=registered&Ascending=false&PageSize=20`,
      `GET /api/referrals/search?PatientId=${patientId}&Statuses=AwaitingTriage&Statuses=InformationRequested&PageSize=20`,
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
    expect(referralEventWords(event({action: 'Registered', teamName: 'Dermatology', requestedPriority: 'High'}))).toEqual({
      title: 'Registered by k.ashford',
      detail: 'To Dermatology, High priority asked for',
    });
    expect(referralEventWords(event({action: 'Registered'}))).toEqual({title: 'Registered by k.ashford', detail: ''});
    expect(
      referralEventWords(
        event({action: 'DetailsEdited', changedFields: ['reason', 'receivedOn', 'specialty'], teamName: 'Urology', previousTeamName: 'Dermatology'}),
      ),
    ).toEqual({title: 'Details edited by k.ashford: reason, received date, team', detail: 'Team Dermatology → Urology'});
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
    expect(referralEventWords(event({action: 'TeamChanged', previousTeamName: 'Urology', teamName: 'Dermatology'}))).toEqual({
      title: 'Redirected by k.ashford',
      detail: 'Team Urology → Dermatology',
    });
    expect(referralEventWords(event({action: 'TeamChanged', teamName: 'Dermatology'}))).toEqual({
      title: 'Redirected by k.ashford',
      detail: 'Team unknown → Dermatology',
    });
    expect(referralEventWords(event({action: 'TeamChanged'}))).toEqual({title: 'Redirected by k.ashford', detail: ''});
    expect(referralEventWords(event({action: 'RedirectedElsewhere'}))).toEqual({title: 'Redirected elsewhere by k.ashford', detail: ''});
    expect(referralEventWords(event({action: 'InformationRequested'}))).toEqual({
      title: 'More information asked for by k.ashford',
      detail: 'Waiting for the referrer',
    });
    expect(referralEventWords(event({action: 'InformationReceived'}))).toEqual({
      title: 'Information received, recorded by k.ashford',
      detail: 'Back to awaiting triage',
    });
    expect(referralEventWords(event({action: 'SomethingNew', toStatus: 'AwaitingTriage'}))).toEqual({
      title: 'Something new by k.ashford',
      detail: 'Now awaiting triage',
    });
    expect(referralEventWords(event({action: null}))).toEqual({title: 'Changed by k.ashford', detail: ''});
  });
});

describe('waiting-time clock words', () => {
  const event = (fields: Partial<ReferralEvent>): ReferralEvent => ({id: 'e', atUtc: '2026-11-02T10:00:00Z', username: 'k.ashford', ...fields});

  it('words the clock events in its history', () => {
    expect(referralEventWords(event({action: 'ClockPaused', pauseReason: 'PatientChoseToWait', reviewOn: '2027-02-03'}))).toEqual({
      title: 'Clock paused by k.ashford',
      detail: 'Patient chose to wait, review 3 Feb 2027',
    });
    expect(referralEventWords(event({action: 'ClockPaused'}))).toEqual({title: 'Clock paused by k.ashford', detail: ''});
    expect(referralEventWords(event({action: 'ClockResumed'}))).toEqual({title: 'Clock resumed by k.ashford', detail: ''});
    expect(
      referralEventWords(event({action: 'ClockStopped', stopReason: 'StopStageFinished', daysWaited: 130, targetDays: 126, breached: true})),
    ).toEqual({title: 'Clock stopped', detail: 'Clock-stop stage finished, day 130 of 126, breached'});
    expect(referralEventWords(event({action: 'ClockStopped', stopReason: 'PathwayDischarged', daysWaited: 12}))).toEqual({
      title: 'Clock stopped',
      detail: 'Discharged, day 12',
    });
    expect(referralEventWords(event({action: 'ClockStopped'}))).toEqual({title: 'Clock stopped', detail: ''});
    expect(referralEventWords(event({action: 'TargetChanged', previousTargetDays: 126, targetDays: 28, priority: 'High'}))).toEqual({
      title: 'Target changed',
      detail: '126 → 28 days (High priority)',
    });
    expect(referralEventWords(event({action: 'TargetChanged', targetDays: 28}))).toEqual({title: 'Target changed', detail: '28 days'});
    expect(referralEventWords(event({action: 'TargetChanged'}))).toEqual({title: 'Target changed', detail: ''});
  });

  it('words pause and stop reasons, the wait, and needs a detail only for Other', () => {
    expect(CLOCK_PAUSE_REASONS.map((r) => r.label)).toEqual(['Patient chose to wait', 'Patient unavailable', 'Other']);
    expect(pauseReasonWords('PatientUnavailable')).toBe('Patient unavailable');
    expect(pauseReasonWords('SomethingNew')).toBe('SomethingNew');
    expect(pauseReasonWords(null)).toBe('');
    expect(pauseNeedsDetail('Other')).toBe(true);
    expect(pauseNeedsDetail('PatientChoseToWait')).toBe(false);
    expect(stopReasonWords('StopStageFinished')).toBe('Clock-stop stage finished');
    expect(stopReasonWords('PathwayCancelled')).toBe('Pathway cancelled');
    expect(stopReasonWords('RedirectedElsewhere')).toBe('Redirected elsewhere');
    expect(stopReasonWords('EnteredInError')).toBe('Entered in error');
    expect(stopReasonWords('SomethingNew')).toBe('Something new');
    expect(stopReasonWords(undefined)).toBe('');
    expect(waitWords({daysWaited: 84, targetDays: 126})).toBe('Day 84 of 126');
    expect(waitWords({})).toBe('Day 0 of 0');
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
        teamId: 's',
        receivedOn: '2026-11-02',
        practiceId: 'p1',
        referrerPractice: ' Synthetic Surgery ',
        referrerClinician: '  ',
        referrerOdsCode: 'a12bc',
        requestedPriority: 'High',
        reason: ' Reason ',
      }),
    ).toEqual({
      teamId: 's',
      receivedOn: '2026-11-02',
      practiceId: 'p1',
      referrerPractice: 'Synthetic Surgery',
      referrerClinician: null,
      referrerOdsCode: 'A12BC',
      requestedPriority: 'High',
      reason: 'Reason',
    });
    expect(toReferralRequest({...referralInput({}), teamId: ''})).toMatchObject({teamId: undefined, receivedOn: undefined, practiceId: null});
  });

  it('starts an edit from the referral, with Medium when it has no priority', () => {
    expect(referralInput({teamId: 's', receivedOn: '2026-11-02', practiceId: 'p1', referrerPractice: 'P', referrerClinician: 'C', referrerOdsCode: 'A1B', requestedPriority: 'Low', reason: 'R'})).toEqual({
      teamId: 's',
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
      openFirst([r('a', 'Withdrawn', '2026-01-01'), r('b', 'AwaitingTriage', '2026-03-01'), r('c', 'InformationRequested', '2026-02-01'), r('d', 'Accepted', '2026-04-01')]).map(
        (x) => x.id,
      ),
    ).toEqual(['c', 'b', 'a', 'd']);
    expect(isOpenReferralStatus('InformationRequested')).toBe(true);
    expect(isOpenReferralStatus('Accepted')).toBe(false);
    expect(isOpenReferralStatus(null)).toBe(false);
  });
});
