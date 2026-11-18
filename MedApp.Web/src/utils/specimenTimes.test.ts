import {durationWords, specimenTurnarounds, ukDateTime, ukDateTimeToUtc} from './specimenTimes';

describe('durationWords', () => {
  it('says days and hours, leaving out minutes', () => {
    expect(durationWords('2026-11-02T09:00:00Z', '2026-11-04T12:59:00Z')).toBe('2 days 3 hours');
    expect(durationWords('2026-11-02T09:00:00Z', '2026-11-03T09:10:00Z')).toBe('1 day');
    expect(durationWords('2026-11-02T09:00:00Z', '2026-11-02T10:00:00Z')).toBe('1 hour');
    expect(durationWords('2026-11-02T09:00:00Z', '2026-11-02T14:30:00Z')).toBe('5 hours');
  });

  it('says minutes under an hour', () => {
    expect(durationWords('2026-11-02T09:00:00Z', '2026-11-02T09:40:00Z')).toBe('40 minutes');
    expect(durationWords('2026-11-02T09:00:00Z', '2026-11-02T09:01:00Z')).toBe('1 minute');
    expect(durationWords('2026-11-02T09:00:00Z', '2026-11-02T09:00:00Z')).toBe('0 minutes');
  });

  it("says nothing when a time is missing or unreadable, or the second is before the first", () => {
    expect(durationWords(null, '2026-11-02T09:00:00Z')).toBeNull();
    expect(durationWords('2026-11-02T09:00:00Z', undefined)).toBeNull();
    expect(durationWords('soon', '2026-11-02T09:00:00Z')).toBeNull();
    expect(durationWords('2026-11-02T09:00:00Z', '2026-11-01T09:00:00Z')).toBeNull();
  });

  it('reads a time without a Z as UTC', () => {
    expect(durationWords('2026-11-02T09:00:00', '2026-11-02T11:00:00Z')).toBe('2 hours');
  });
});

describe('specimenTurnarounds', () => {
  it('gives each step once both its times are known', () => {
    expect(
      specimenTurnarounds({
        takenAtUtc: '2026-11-02T09:00:00Z',
        sentAtUtc: '2026-11-02T15:00:00Z',
        receivedAtUtc: '2026-11-03T10:00:00Z',
        reportedAtUtc: '2026-11-05T12:00:00Z',
      }),
    ).toEqual([
      {label: 'Taken to sent', words: '6 hours'},
      {label: 'Sent to received', words: '19 hours'},
      {label: 'Received to reported', words: '2 days 2 hours'},
      {label: 'Taken to reported', words: '3 days 3 hours'},
    ]);
  });

  it('leaves out steps not reached yet', () => {
    expect(specimenTurnarounds({takenAtUtc: '2026-11-02T09:00:00Z', sentAtUtc: '2026-11-02T09:30:00Z'})).toEqual([
      {label: 'Taken to sent', words: '30 minutes'},
    ]);
    expect(specimenTurnarounds({takenAtUtc: '2026-11-02T09:00:00Z'})).toEqual([]);
  });
});

describe('UK day and clock times', () => {
  it('turns a UK time into UTC, in winter and in summer time', () => {
    expect(ukDateTimeToUtc({date: '2026-11-18', time: '09:30'})).toBe('2026-11-18T09:30:00.000Z');
    expect(ukDateTimeToUtc({date: '2026-07-01', time: '09:30'})).toBe('2026-07-01T08:30:00.000Z');
    expect(ukDateTimeToUtc({date: '2026-07-01', time: '00:15'})).toBe('2026-06-30T23:15:00.000Z');
    expect(ukDateTimeToUtc({date: '2026-11-18', time: '23:59:00'})).toBe('2026-11-18T23:59:00.000Z');
  });

  it('gives nothing for a missing or unreadable day or time', () => {
    expect(ukDateTimeToUtc({date: '2026-11-18'})).toBeNull();
    expect(ukDateTimeToUtc({time: '09:30'})).toBeNull();
    expect(ukDateTimeToUtc({date: '18/11/2026', time: '09:30'})).toBeNull();
    expect(ukDateTimeToUtc({date: '2026-11-18', time: '25:00'})).toBeNull();
    expect(ukDateTimeToUtc({date: '2026-11-18', time: 'half nine'})).toBeNull();
  });

  it('reads a moment as its UK day and clock time', () => {
    expect(ukDateTime('2026-07-01T23:15:00Z')).toEqual({date: '2026-07-02', time: '00:15'});
    expect(ukDateTime('2026-11-18T09:30:00Z')).toEqual({date: '2026-11-18', time: '09:30'});
  });
});
