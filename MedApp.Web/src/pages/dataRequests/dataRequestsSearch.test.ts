import {parseDataRequestsSearch} from './dataRequestsSearch';

describe('dataRequestsSearch', () => {
  it('reads the view (any case) and page, dropping anything invalid', () => {
    expect(parseDataRequestsSearch({view: 'awaitingreview', page: '3'})).toEqual({view: 'AwaitingReview', page: 3});
    expect(parseDataRequestsSearch({view: 'Sent', page: 2})).toEqual({view: 'Sent', page: 2});
    expect(parseDataRequestsSearch({view: 'Everything', page: '1'})).toEqual({});
    expect(parseDataRequestsSearch({view: '', page: 'x'})).toEqual({});
    expect(parseDataRequestsSearch({view: {}, page: '-2'})).toEqual({});
  });
});
