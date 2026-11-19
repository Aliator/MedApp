import {parseHospitalRequestsSearch} from './hospitalRequestsSearch';

describe('parseHospitalRequestsSearch', () => {
  it('keeps a valid hospital, page and sort', () => {
    expect(parseHospitalRequestsSearch({hospital: '000000C1-0000-4000-8000-000000000001', page: '2', sort: 'Needed', asc: 'false'})).toEqual({
      hospital: '000000c1-0000-4000-8000-000000000001',
      page: 2,
      sort: 'needed',
      asc: false,
    });
  });

  it('drops anything that is not valid', () => {
    expect(parseHospitalRequestsSearch({hospital: 'riverside', page: '0', sort: 'nhs', asc: 'maybe', q: '9991031375'})).toEqual({});
  });
});
