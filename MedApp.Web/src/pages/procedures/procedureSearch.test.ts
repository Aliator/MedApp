import {parseProcedureSearch} from './procedureSearch';

describe('parseProcedureSearch', () => {
  it('keeps a request ID, in lower case', () => {
    expect(parseProcedureSearch({deletionRequest: ' 0000000D-0000-0000-0000-000000000001 '})).toEqual({
      deletionRequest: '0000000d-0000-0000-0000-000000000001',
    });
  });

  it('drops anything that is not an ID, so a bad link just shows the procedure', () => {
    expect(parseProcedureSearch({})).toEqual({});
    expect(parseProcedureSearch({deletionRequest: 'not-an-id'})).toEqual({});
    expect(parseProcedureSearch({deletionRequest: 42})).toEqual({});
    expect(parseProcedureSearch({deletionRequest: ['0000000d-0000-0000-0000-000000000001']})).toEqual({});
  });
});
