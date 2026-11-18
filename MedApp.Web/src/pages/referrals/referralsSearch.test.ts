import {defaultSort, parseReferralsSearch, referralViewOf, sortOf, viewFilters, viewStatuses} from './referralsSearch';

describe('parseReferralsSearch', () => {
  it('reads the view, page, sort and patient, dropping anything invalid', () => {
    expect(parseReferralsSearch({page: '3', sort: 'patient', asc: 'false', view: 'Closed', patient: ' Ahmed '})).toEqual({
      page: 3,
      sort: 'Patient',
      asc: false,
      view: 'closed',
      patient: 'Ahmed',
    });
    expect(parseReferralsSearch({page: '1', sort: 'nonsense', asc: 'maybe', view: 'awaiting', patient: ''})).toEqual({});
    expect(parseReferralsSearch({view: 'all', asc: 'true', page: 2})).toEqual({view: 'all', asc: true, page: 2});
    expect(parseReferralsSearch({view: 'everything', page: 'x'})).toEqual({});
  });

  it('reads specialties as IDs, dropping anything else', () => {
    const id = '0000005A-0000-0000-0000-0000000000AA';
    expect(parseReferralsSearch({specialty: `${id},Dermatology`})).toEqual({specialty: id.toLowerCase()});
    expect(parseReferralsSearch({specialty: 'Dermatology'})).toEqual({});
  });
});

describe('views', () => {
  it('start on the inbox, and ask for its statuses', () => {
    expect(referralViewOf({})).toBe('awaiting');
    expect(viewStatuses('awaiting')).toEqual(['AwaitingTriage']);
    expect(viewStatuses('information')).toEqual(['InformationRequested']);
    expect(parseReferralsSearch({view: 'Information'})).toEqual({view: 'information'});
    expect(viewStatuses('closed')).toEqual(['Accepted', 'Rejected', 'RedirectedElsewhere', 'Withdrawn', 'EnteredInError']);
    expect(viewStatuses('all')).toBeUndefined();
  });
});

describe('the waiting-time views', () => {
  it('read waiting and paused from the URL, and the clock sorts', () => {
    expect(parseReferralsSearch({view: 'Waiting', sort: 'wait'})).toEqual({view: 'waiting', sort: 'Wait'});
    expect(parseReferralsSearch({view: 'paused', sort: 'review'})).toEqual({view: 'paused', sort: 'Review'});
    expect(parseReferralsSearch({sort: 'target'})).toEqual({sort: 'Target'});
  });

  it('ask for the waiting list and the paused clocks', () => {
    expect(viewStatuses('waiting')).toEqual(['Accepted']);
    expect(viewStatuses('paused')).toBeUndefined();
    expect(viewFilters('waiting')).toEqual({Statuses: ['Accepted'], ClockStopped: false, Paused: undefined});
    expect(viewFilters('paused')).toEqual({Statuses: undefined, ClockStopped: undefined, Paused: true});
    expect(viewFilters('awaiting')).toEqual({Statuses: ['AwaitingTriage'], ClockStopped: undefined, Paused: undefined});
  });

  it('order each view by dates: the longest wait, the soonest review, or the oldest received first', () => {
    expect(defaultSort('awaiting')).toEqual({sort: 'Wait', asc: false});
    expect(defaultSort('waiting')).toEqual({sort: 'Wait', asc: false});
    expect(defaultSort('paused')).toEqual({sort: 'Review', asc: true});
    expect(defaultSort('closed')).toEqual({sort: 'Received', asc: true});
    expect(defaultSort('all')).toEqual({sort: 'Received', asc: true});
    expect(sortOf({})).toEqual({sort: 'Wait', asc: false});
    expect(sortOf({sort: 'Patient'})).toEqual({sort: 'Patient', asc: true});
    expect(sortOf({sort: 'Patient', asc: false})).toEqual({sort: 'Patient', asc: false});
  });
});
