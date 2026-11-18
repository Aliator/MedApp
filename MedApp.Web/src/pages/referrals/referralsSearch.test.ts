import {parseReferralsSearch, referralViewOf, viewStatuses} from './referralsSearch';

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
