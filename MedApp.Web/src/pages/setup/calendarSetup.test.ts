import {addMinutes, apiTime, blockLabel, blockProblem, byStart, clockTime, count, dayParts, quarterHours, weekSummary, type BlockRow} from './calendarSetup';

const row = (key: string, startTime: string, endTime: string, activityTypeId = 'lunch'): BlockRow => ({key, activityTypeId, startTime, endTime, note: ''});
const names = (id: string) => ({lunch: 'Lunch', admin: 'Admin time'})[id] ?? 'an activity';

describe('calendar setup times', () => {
  it('reads and writes the API clock times', () => {
    expect(clockTime('12:00:00')).toBe('12:00');
    expect(clockTime(undefined)).toBe('');
    expect(apiTime('12:00')).toBe('12:00:00');
  });

  it('steps through the working day in quarter hours', () => {
    const times = quarterHours();
    expect(times[0]).toBe('08:00');
    expect(times[1]).toBe('08:15');
    expect(times[times.length - 1]).toBe('18:00');
    expect(times).toHaveLength(41);
  });

  it('moves a time, kept inside the bounds of any working day', () => {
    expect(addMinutes('12:00', 60)).toBe('13:00');
    expect(addMinutes('17:30', 60)).toBe('18:30');
    expect(addMinutes('08:30', -60)).toBe('07:30');
    expect(addMinutes('06:30', -60)).toBe('06:00');
    expect(addMinutes('22:30', 60)).toBe('23:00');
  });

  it('labels a block and counts things', () => {
    expect(blockLabel({activityName: 'Lunch', startTime: '12:00:00', endTime: '13:00:00'})).toBe('Lunch, 12:00–13:00');
    expect(blockLabel({startTime: '12:00:00', endTime: '13:00:00'})).toBe(', 12:00–13:00');
    expect(count(1, 'block', 'blocks')).toBe('1 block');
    expect(count(2, 'block', 'blocks')).toBe('2 blocks');
  });
});

describe('weekSummary', () => {
  const day = (dayOfWeek: string, dayTemplateId: string | null, dayTemplateName: string | null = null) => ({dayOfWeek, dayTemplateId, dayTemplateName});

  it('says when every weekday is the same', () => {
    const days = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'].map((d) => day(d, 'std', 'Standard day'));
    expect(weekSummary({days})).toBe('Mon to Fri: Standard day');
    expect(weekSummary({days: []})).toBe('Mon to Fri: No template');
  });

  it('groups the days with the same day template', () => {
    const days = [
      day('Monday', 'std', 'Standard day'),
      day('Tuesday', 'std', 'Standard day'),
      day('Wednesday', 'half', 'Half day'),
      day('Thursday', 'std', 'Standard day'),
      day('Friday', null),
    ];
    expect(weekSummary({days})).toBe('Mon, Tue, Thu: Standard day · Wed: Half day · Fri: No template');
    expect(weekSummary({days: [day('Monday', 'x')]})).toBe('Mon: Unknown · Tue, Wed, Thu, Fri: No template');
  });
});

describe('blockProblem', () => {
  it('finds a block that ends before it starts, or is outside the working day', () => {
    expect(blockProblem(row('a', '12:00', '12:00'), [], names)).toEqual({field: 'endTime', message: 'Ends before it starts.'});
    expect(blockProblem(row('a', '05:55', '09:00'), [], names)).toEqual({field: 'startTime', message: 'Starts before 06:00.'});
    expect(blockProblem(row('a', '17:00', '23:05'), [], names)).toEqual({field: 'endTime', message: 'Ends after 23:00.'});
    expect(blockProblem(row('a', '07:00', '19:30'), [], names)).toBeNull();
  });

  it('names the block one overlaps, and passes blocks that only touch', () => {
    const lunch = row('a', '12:00', '13:00');
    const admin = row('b', '12:30', '14:00', 'admin');
    const after = row('c', '13:00', '14:00');

    expect(blockProblem(admin, [lunch, admin], names)).toEqual({field: 'startTime', message: 'Overlaps Lunch, 12:00 to 13:00.'});
    expect(blockProblem(after, [lunch, after], names)).toBeNull();
  });
});

describe('byStart and dayParts', () => {
  it('orders blocks by start, then end', () => {
    expect(byStart([row('a', '13:00', '14:00'), row('b', '09:00', '10:00'), row('c', '09:00', '09:30')]).map((r) => r.key)).toEqual(['c', 'b', 'a']);
  });

  it('gives the day as blocks and the free time around them', () => {
    const lunch = row('a', '12:00', '13:00');
    const late = row('b', '17:00', '18:00');
    expect(dayParts([late, lunch])).toEqual([
      {kind: 'free', startTime: '08:00', endTime: '12:00'},
      {kind: 'block', row: lunch},
      {kind: 'free', startTime: '13:00', endTime: '17:00'},
      {kind: 'block', row: late},
    ]);
    expect(dayParts([])).toEqual([{kind: 'free', startTime: '08:00', endTime: '18:00'}]);
  });

  it('lists overlapping blocks as they are', () => {
    const long = row('a', '08:00', '12:00');
    const inside = row('b', '09:00', '10:00');
    expect(dayParts([long, inside])).toEqual([
      {kind: 'block', row: long},
      {kind: 'block', row: inside},
      {kind: 'free', startTime: '12:00', endTime: '18:00'},
    ]);
  });
});
