import {parsePathwaySearch} from './pathwaySearch';

describe('parsePathwaySearch', () => {
  it('keeps a request ID, in lower case', () => {
    expect(parsePathwaySearch({deletionRequest: ' 0000000D-0000-0000-0000-000000000001 '})).toEqual({
      deletionRequest: '0000000d-0000-0000-0000-000000000001',
    });
  });

  it('drops anything that is not an ID, so a bad link just shows the pathway', () => {
    expect(parsePathwaySearch({})).toEqual({});
    expect(parsePathwaySearch({deletionRequest: 'not-an-id'})).toEqual({});
    expect(parsePathwaySearch({deletionRequest: 42})).toEqual({});
    expect(parsePathwaySearch({deletionRequest: ['0000000d-0000-0000-0000-000000000001']})).toEqual({});
  });
});
