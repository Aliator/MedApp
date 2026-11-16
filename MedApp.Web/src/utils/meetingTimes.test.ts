import {
  busyLines,
  clockTime,
  firstFit,
  formatSuggestion,
  laterFrom,
  lengthLabel,
  lengthOptions,
  startOptions,
  suggestionRange,
  workingDayOffset,
  workingTime,
} from './meetingTimes';

describe('meetingTimes', () => {
  it('turns a time on a UK working day into UTC, in winter and in summer time', () => {
    expect(workingTime('2026-11-24', 0)).toBe('2026-11-24T08:00:00.000Z');
    expect(workingTime('2026-11-24', 125)).toBe('2026-11-24T10:05:00.000Z');
    // British Summer Time: 08:00 UK time is 07:00 UTC.
    expect(workingTime('2026-06-15', 0)).toBe('2026-06-15T07:00:00.000Z');
    expect(workingTime('2026-06-15', 600)).toBe('2026-06-15T17:00:00.000Z');
  });

  it('reads a moment as its UK day and minutes after 08:00', () => {
    expect(workingDayOffset('2026-11-24T10:30:00Z')).toEqual({date: '2026-11-24', minutes: 150});
    expect(workingDayOffset('2026-06-15T07:05:00Z')).toEqual({date: '2026-06-15', minutes: 5});
    expect(workingDayOffset('2026-11-24T07:00:00Z')).toEqual({date: '2026-11-24', minutes: -60});
  });

  it('writes minutes after 08:00 as a clock time', () => {
    expect(clockTime(0)).toBe('08:00');
    expect(clockTime(95)).toBe('09:35');
    expect(clockTime(600)).toBe('18:00');
  });

  it('says a length in words, and offers the usual lengths plus any other', () => {
    expect(lengthLabel(15)).toBe('15 minutes');
    expect(lengthLabel(60)).toBe('1 hour');
    expect(lengthLabel(90)).toBe('1 hour 30 minutes');
    expect(lengthLabel(120)).toBe('2 hours');
    expect(lengthOptions(60)).toEqual([15, 30, 45, 60, 90, 120]);
    expect(lengthOptions(50)).toEqual([15, 30, 45, 50, 60, 90, 120]);
  });

  it('offers starts every 5 minutes that end by 18:00, keeping a chosen one', () => {
    const starts = startOptions(120);
    expect(starts[0]).toBe(0);
    expect(starts[1]).toBe(5);
    expect(starts.at(-1)).toBe(480);
    expect(starts).toHaveLength(97);
    // A chosen start that no longer fits (a longer length) stays, so the field still shows it.
    expect(startOptions(120, 540).at(-1)).toBe(540);
    expect(startOptions(120, 3)).toContain(3);
    // Outside the working day, it isn't offered.
    expect(startOptions(120, 600)).not.toContain(600);
    expect(startOptions(120, -5)).not.toContain(-5);
  });

  it('asks for two weeks of suggestions, and looks on from the last one, or past the range', () => {
    expect(suggestionRange('2026-11-16')).toEqual({from: '2026-11-16', to: '2026-11-29'});
    const range = suggestionRange('2026-11-16');
    expect(laterFrom(range, [{startUtc: '2026-11-17T09:00:00Z', endUtc: '2026-11-17T10:00:00Z'}, {startUtc: '2026-11-20T14:00:00Z', endUtc: '2026-11-20T15:00:00Z'}])).toBe(
      '2026-11-21',
    );
    expect(laterFrom(range, [])).toBe('2026-11-30');
  });

  it('writes a suggestion for its button', () => {
    expect(formatSuggestion('2026-11-17T10:00:00Z', '2026-11-17T11:00:00Z')).toBe('Tue 17 Nov, 10:00 to 11:00');
    expect(formatSuggestion('2026-06-16T09:00:00Z', '2026-06-16T09:30:00Z')).toBe('Tue 16 Jun, 10:00 to 10:30');
  });

  it("says who's busy at a time, earliest first, once each", () => {
    const busy = [
      {staffUserId: 'b', username: 'b.staff', startUtc: '2026-11-24T12:00:00Z', endUtc: '2026-11-24T13:00:00Z', what: 'Lunch'},
      {staffUserId: 'a', username: 'a.staff', startUtc: '2026-11-24T10:30:00Z', endUtc: '2026-11-24T11:30:00Z', what: 'another appointment'},
      {staffUserId: 'c', username: 'c.staff', startUtc: '2026-11-24T10:30:00Z', endUtc: '2026-11-24T11:00:00Z', what: "the meeting 'MDT'"},
      {staffUserId: 'c', username: 'c.staff', startUtc: '2026-11-24T10:30:00Z', endUtc: '2026-11-24T11:00:00Z', what: "the meeting 'MDT'"},
      // Deleted account, and nothing said about what.
      {staffUserId: 'd', username: null, startUtc: '2026-11-24T11:45:00Z', endUtc: '2026-11-24T12:15:00Z', what: null},
      // Not then: before, touching the start, and with no times.
      {staffUserId: 'a', username: 'a.staff', startUtc: '2026-11-24T08:00:00Z', endUtc: '2026-11-24T10:00:00Z', what: 'Lunch'},
      {staffUserId: 'a', username: 'a.staff', startUtc: null as unknown as string, endUtc: null as unknown as string, what: 'Lunch'},
    ];
    expect(busyLines(busy, '2026-11-24T10:00:00Z', '2026-11-24T12:30:00Z')).toEqual([
      'a.staff has another appointment, 10:30 to 11:30',
      "c.staff has the meeting 'MDT', 10:30 to 11:00",
      'Deleted user has something on, 11:45 to 12:15',
      'b.staff has Lunch, 12:00 to 13:00',
    ]);
    expect(busyLines(busy, '2026-11-24T14:00:00Z', '2026-11-24T15:00:00Z')).toEqual([]);
  });

  it('finds the first start in free time that fits the length, on a 5-minute step', () => {
    const free = [
      {startUtc: '2026-11-24T09:02:00Z', endUtc: '2026-11-24T09:30:00Z'},
      {startUtc: null as unknown as string, endUtc: null as unknown as string},
      {startUtc: '2026-11-24T10:03:00Z', endUtc: '2026-11-24T11:10:00Z'},
    ];
    // 09:05 to 09:30 is too short for an hour; 10:05 to 11:05 fits.
    expect(firstFit('2026-11-24', free, 60)).toBe(125);
    expect(firstFit('2026-11-24', free, 15)).toBe(65);
    expect(firstFit('2026-11-24', free, 120)).toBeUndefined();
    // Free time from before 08:00 and to after 18:00 is cut to the working day.
    expect(firstFit('2026-11-24', [{startUtc: '2026-11-24T07:00:00Z', endUtc: '2026-11-24T19:00:00Z'}], 60)).toBe(0);
    expect(firstFit('2026-11-24', [{startUtc: '2026-11-24T17:30:00Z', endUtc: '2026-11-24T19:00:00Z'}], 60)).toBeUndefined();
  });
});
