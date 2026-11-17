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

  it('keeps the week view when named, as a narrow window would otherwise open on a day', () => {
    expect(parseCalendarSearch({view: 'Week'})).toEqual({view: 'week'});
  });

  it('keeps the row length a link names: 5 or 15 minutes', () => {
    expect(parseCalendarSearch({rows: '5'})).toEqual({rows: 5});
    expect(parseCalendarSearch({rows: 5})).toEqual({rows: 5});
    // 15 too: a link can name it over someone's own default of 5.
    expect(parseCalendarSearch({rows: '15'})).toEqual({rows: 15});
  });

  it('drops what it cannot read', () => {
    expect(parseCalendarSearch({date: '2026-02-30', staff: 'someone', appointment: 42, rows: '10'})).toEqual({});
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
