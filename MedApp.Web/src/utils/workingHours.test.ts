import {hourOptions, hoursSummary, rowProblem, siteHoursSummary, siteInput, siteRows, staffInput, staffRows, type HoursRow} from './workingHours';

const row = (values: Partial<HoursRow> = {}): HoursRow => ({day: 'Monday', isOn: true, from: '09:00', to: '17:00', siteId: 'main', ...values});

describe('working hours', () => {
  it('offers every 15 minutes from 06:00 to 23:00, and a time off the steps when it is the current one', () => {
    const times = hourOptions().map((o) => o.value);
    expect(times[0]).toBe('06:00');
    expect(times.at(-1)).toBe('23:00');
    expect(times).toHaveLength(69);
    expect(hourOptions('09:05').map((o) => o.value)).toContain('09:05');
  });

  it("makes the editor's seven rows from someone's hours, or the default's days, starting new days at a site", () => {
    const own = staffRows(
      {hasOwnHours: true, days: [{dayOfWeek: 'Saturday', starts: '09:00:00', ends: '13:00:00', siteId: 'north', siteName: 'North'}]},
      'main',
    );
    expect(own.map((r) => r.day)).toEqual(['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday']);
    expect(own[5]).toEqual({day: 'Saturday', isOn: true, from: '09:00', to: '13:00', siteId: 'north'});
    expect(own[0]).toEqual({day: 'Monday', isOn: false, from: '08:00', to: '18:00', siteId: 'main'});

    const onDefault = staffRows(
      {hasOwnHours: false, days: [{dayOfWeek: 'Monday', starts: '08:00:00', ends: '18:00:00', siteId: null}]},
      'main',
    );
    expect(onDefault[0]).toEqual({day: 'Monday', isOn: true, from: '08:00', to: '18:00', siteId: 'main'});
    expect(staffRows(undefined).every((r) => !r.isOn)).toBe(true);
  });

  it('sends only the days that are on, in the API form', () => {
    const rows = [row(), row({day: 'Tuesday', isOn: false}), row({day: 'Saturday', from: '09:00', to: '13:00', siteId: 'north'})];
    expect(staffInput(rows)).toEqual([
      {dayOfWeek: 'Monday', starts: '09:00:00', ends: '17:00:00', siteId: 'main'},
      {dayOfWeek: 'Saturday', starts: '09:00:00', ends: '13:00:00', siteId: 'north'},
    ]);
    expect(siteInput(rows)).toEqual([
      {dayOfWeek: 'Monday', opens: '09:00:00', closes: '17:00:00'},
      {dayOfWeek: 'Saturday', opens: '09:00:00', closes: '13:00:00'},
    ]);
    expect(siteRows({hasOwnHours: true, days: [{dayOfWeek: 'Tuesday', opens: '08:00:00', closes: '20:00:00'}]})[1]).toEqual({
      day: 'Tuesday',
      isOn: true,
      from: '08:00',
      to: '20:00',
      siteId: '',
    });
  });

  it('says what is wrong with a day that is on, and nothing about one that is off', () => {
    expect(rowProblem(row({from: '17:00', to: '09:00'}), true)).toBe('Ends before it starts.');
    expect(rowProblem(row({siteId: ''}), true)).toBe('Choose a site.');
    expect(rowProblem(row({siteId: ''}), false)).toBeNull();
    expect(rowProblem(row({isOn: false, from: '17:00', to: '09:00'}), true)).toBeNull();
    expect(rowProblem(row(), true)).toBeNull();
  });

  it('sums hours up in a line, the days with the same times together', () => {
    const weekdays = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'].map((d) => ({dayOfWeek: d, from: '08:00:00', to: '18:00:00'}));
    expect(hoursSummary(weekdays)).toBe('Mon to Fri, 08:00 to 18:00');
    expect(
      hoursSummary([
        {dayOfWeek: 'Saturday', from: '09:00:00', to: '13:00:00'},
        {dayOfWeek: 'Monday', from: '09:00:00', to: '17:00:00'},
        {dayOfWeek: 'Wednesday', from: '09:00:00', to: '17:00:00'},
      ]),
    ).toBe('Mon, Wed, 09:00 to 17:00 · Sat, 09:00 to 13:00');
    expect(hoursSummary([])).toBe('None');
    expect(siteHoursSummary({days: [{dayOfWeek: 'Tuesday', opens: '08:00:00', closes: '20:00:00'}]})).toBe('Tue, 08:00 to 20:00');
  });
});
