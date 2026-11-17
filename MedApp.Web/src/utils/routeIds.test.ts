import {lowerCaseId} from './routeIds';

describe('lowerCaseId', () => {
  it('reads an ID in the address in lower case, as the API returns it', () => {
    expect(lowerCaseId.parse({id: '327460EC-AB12-4C3D-9E8F-0123456789AB'})).toEqual({id: '327460ec-ab12-4c3d-9e8f-0123456789ab'});
  });

  it('writes it as it is', () => {
    expect(lowerCaseId.stringify({id: '327460ec-ab12-4c3d-9e8f-0123456789ab'})).toEqual({id: '327460ec-ab12-4c3d-9e8f-0123456789ab'});
  });
});
