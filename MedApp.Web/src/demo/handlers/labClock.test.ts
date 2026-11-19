import {labClock, type LabClockInput} from './labClock';

const base: LabClockInput = {
  receivedAtUtc: '2026-11-02T10:00:00Z',
  status: 'Open',
  reportedAtUtc: null,
  test: {targetDays: 7, limitDays: 10, atRiskDays: 2},
  hospital: null,
  nowUtc: '2026-11-04T12:00:00Z',
};
const hospital = {reportNeededBy: null, isCancerPathway: false, collectedAtUtc: '2026-11-01T09:00:00Z', defaultDeadlineDays: 10};

describe('a lab case’s clock (MC19): whichever deadline is sooner applies', () => {
  it('takes the lab’s own target for a clinic case, in calendar days from receipt', () => {
    expect(labClock(base)).toMatchObject({labDueOn: '2026-11-09', labLimitOn: '2026-11-12', hospitalDueOn: null, dueOn: '2026-11-09', setBy: 'Lab', state: 'OnTime', daysLeft: 5});
  });

  it('takes the hospital’s date when it’s sooner, and the lab’s when the hospital’s is later', () => {
    expect(labClock({...base, hospital: {...hospital, reportNeededBy: '2026-11-06'}})).toMatchObject({dueOn: '2026-11-06', setBy: 'Hospital', hospitalDueBasis: 'ReportNeededBy', state: 'AtRisk'});
    expect(labClock({...base, hospital: {...hospital, reportNeededBy: '2026-11-20'}})).toMatchObject({dueOn: '2026-11-09', setBy: 'Lab', hospitalDueOn: '2026-11-20'});
    // No date: a cancer pathway is collection + 10 days; otherwise the hospital's default from receipt.
    expect(labClock({...base, hospital: {...hospital, isCancerPathway: true}})).toMatchObject({hospitalDueOn: '2026-11-11', hospitalDueBasis: 'CancerPathway', setBy: 'Lab'});
    expect(labClock({...base, test: {targetDays: 14, atRiskDays: 3}, hospital})).toMatchObject({hospitalDueOn: '2026-11-12', hospitalDueBasis: 'HospitalDefault', setBy: 'Hospital'});
  });

  it('is breached after the end of the due day until reported, and stops when reported', () => {
    expect(labClock({...base, nowUtc: '2026-11-09T23:30:00Z'})?.state).toBe('AtRisk');
    expect(labClock({...base, nowUtc: '2026-11-10T00:30:00Z'})).toMatchObject({state: 'Breached', daysLeft: -1});
    expect(labClock({...base, status: 'Reported', reportedAtUtc: '2026-11-09T16:00:00Z', nowUtc: '2026-11-20T09:00:00Z'})?.state).toBe('ReportedOnTime');
    expect(labClock({...base, status: 'Reported', reportedAtUtc: '2026-11-10T09:00:00Z'})?.state).toBe('ReportedLate');
    expect(labClock({...base, receivedAtUtc: null})).toBeUndefined();
  });
});
