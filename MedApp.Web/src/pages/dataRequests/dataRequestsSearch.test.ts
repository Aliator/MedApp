import {dataRequestsSortOf, parseDataRequestsSearch} from './dataRequestsSearch';

describe('dataRequestsSearch', () => {
  it('reads the view (any case) and page, dropping anything invalid', () => {
    expect(parseDataRequestsSearch({view: 'awaitingreview', page: '3'})).toEqual({view: 'AwaitingReview', page: 3});
    expect(parseDataRequestsSearch({view: 'Sent', page: 2})).toEqual({view: 'Sent', page: 2});
    expect(parseDataRequestsSearch({view: 'Everything', page: '1'})).toEqual({});
    expect(parseDataRequestsSearch({view: '', page: 'x'})).toEqual({});
    expect(parseDataRequestsSearch({view: {}, page: '-2'})).toEqual({});
  });

  it('reads the patient searched for and the sort', () => {
    expect(parseDataRequestsSearch({patient: ' Freya ', sort: 'Due', asc: 'false'})).toEqual({patient: 'Freya', sort: 'due', asc: false});
    expect(parseDataRequestsSearch({sort: 'received', asc: 'true'})).toEqual({sort: 'received', asc: true});
    expect(parseDataRequestsSearch({sort: 'identity', asc: 'maybe'})).toEqual({});
  });

  it("marks each view's own order, or the sort asked for", () => {
    expect(dataRequestsSortOf({}, 'Open')).toEqual({sort: 'due', asc: true});
    expect(dataRequestsSortOf({}, 'AwaitingReview')).toEqual({sort: 'due', asc: true});
    expect(dataRequestsSortOf({}, 'Sent')).toEqual({sort: 'received', asc: false});
    expect(dataRequestsSortOf({}, 'All')).toBeNull();
    expect(dataRequestsSortOf({sort: 'patient'}, 'All')).toEqual({sort: 'patient', asc: true});
  });
});
