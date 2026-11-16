import {
  calendarTemplateEditorHref,
  calendarTemplatesHref,
  parseCalendarTemplateEditorSearch,
  parseCalendarTemplatesSearch,
} from './calendarTemplatesSearch';

const STAFF = '3f2a1b4c-5d6e-4f70-8a9b-0c1d2e3f4a5b';

describe('parseCalendarTemplatesSearch', () => {
  it('reads the tab (Day templates unless it says otherwise), whose, and the list search', () => {
    expect(parseCalendarTemplatesSearch({tab: 'Week', name: 'standard', status: 'any', page: '2', staff: ` ${STAFF} `})).toEqual({
      tab: 'week',
      name: 'standard',
      status: 'any',
      page: 2,
      staff: STAFF,
    });
    expect(parseCalendarTemplatesSearch({tab: 'day'})).toEqual({});
    expect(parseCalendarTemplatesSearch({tab: 'default'})).toEqual({tab: 'default'});
  });

  it("reads the old admin page's Staff defaults tab as the Default week", () => {
    expect(parseCalendarTemplatesSearch({tab: 'staff'})).toEqual({tab: 'default'});
  });

  it('drops anything invalid', () => {
    expect(parseCalendarTemplatesSearch({tab: 7, staff: 'not-an-id'})).toEqual({});
    expect(parseCalendarTemplatesSearch({staff: 42})).toEqual({});
  });
});

describe('parseCalendarTemplateEditorSearch', () => {
  it('reads whose template it is, dropping anything else', () => {
    expect(parseCalendarTemplateEditorSearch({staff: STAFF, tab: 'week'})).toEqual({staff: STAFF});
    expect(parseCalendarTemplateEditorSearch({staff: 'x'})).toEqual({});
  });
});

describe('calendarTemplatesHref', () => {
  it("links to a tab of your own templates, or someone else's", () => {
    expect(calendarTemplatesHref()).toBe('/calendar/templates');
    expect(calendarTemplatesHref('day')).toBe('/calendar/templates');
    expect(calendarTemplatesHref('week')).toBe('/calendar/templates?tab=week');
    expect(calendarTemplatesHref('default', STAFF)).toBe(`/calendar/templates?tab=default&staff=${STAFF}`);
    expect(calendarTemplatesHref('day', STAFF)).toBe(`/calendar/templates?staff=${STAFF}`);
  });
});

describe('calendarTemplateEditorHref', () => {
  it('links to an editor, keeping whose', () => {
    expect(calendarTemplateEditorHref('day', 'new')).toBe('/calendar/templates/day/new');
    expect(calendarTemplateEditorHref('week', 'wt1', STAFF)).toBe(`/calendar/templates/week/wt1?staff=${STAFF}`);
  });
});
