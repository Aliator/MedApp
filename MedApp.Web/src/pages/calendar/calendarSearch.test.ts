import {parseCalendarSearch, parseNeedsBookingSearch} from './calendarSearch';

const ID = '00000000-0000-0000-0000-0000000000a1';

describe('parseCalendarSearch', () => {
  it('keeps a valid day, the day view, a member of staff and an appointment', () => {
    expect(parseCalendarSearch({date: '2026-11-23', view: 'Day', staff: ID, appointment: ID})).toEqual({
      date: '2026-11-23',
      view: 'day',
      staff: ID,
      appointment: ID,
    });
  });

  it('keeps the team view, and who it shows: valid IDs, once each', () => {
    const other = '00000000-0000-0000-0000-0000000000B2';
    expect(parseCalendarSearch({view: 'TEAM', who: [ID, other.toLowerCase(), other, 'nobody', 7]})).toEqual({view: 'team', who: [ID, other.toLowerCase()]});
    expect(parseCalendarSearch({who: `${ID},${other}`})).toEqual({who: [ID, other.toLowerCase()]});
    expect(parseCalendarSearch({who: ['nobody']})).toEqual({});
    expect(parseCalendarSearch({who: {}})).toEqual({});
  });

  it('drops what it cannot read, and the week view, which is the default', () => {
    expect(parseCalendarSearch({date: '2026-02-30', view: 'week', staff: 'someone', appointment: 42})).toEqual({});
    expect(parseCalendarSearch({date: 'tomorrow', view: 'month', staff: ' ', appointment: {}})).toEqual({});
  });
});

describe('parseNeedsBookingSearch', () => {
  it('keeps a page, a sort, its direction and a patient search', () => {
    expect(parseNeedsBookingSearch({page: '3', sort: 'patient', asc: 'false', patient: ' Sample '})).toEqual({
      page: 3,
      sort: 'Patient',
      asc: false,
      patient: 'Sample',
    });
    expect(parseNeedsBookingSearch({asc: 'TRUE'})).toEqual({asc: true});
  });

  it('drops anything invalid', () => {
    expect(parseNeedsBookingSearch({page: '1', sort: 'colour', asc: 'maybe', patient: ''})).toEqual({});
    expect(parseNeedsBookingSearch({page: 'two'})).toEqual({});
  });
});
