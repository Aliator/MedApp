import {formatDateTime, ageDescription, ageOn, formatDate, formatDayHeading, formatShortDate, formatTime, parseUtc, timeAgo, todayIso, ukDay} from './dates';

describe('todayIso', () => {
  it('formats a local date as YYYY-MM-DD', () => {
    expect(todayIso(new Date(2026, 0, 5))).toBe('2026-01-05');
  });
});

describe('ageOn', () => {
  it.each([
    ['1980-03-14', '2026-03-13', 45],
    ['1980-03-14', '2026-03-14', 46],
    ['1980-03-14', '2026-12-31', 46],
    ['2026-01-01', '2026-11-14', 0],
    ['2000-02-29', '2026-02-28', 25],
  ])('born %s is %i on %s', (dob, on, expected) => {
    expect(ageOn(dob, on)).toBe(expected);
  });

  it.each(['2030-01-01', 'not-a-date', '2023-02-30', ''])('returns null for %s', (dob) => {
    expect(ageOn(dob, '2026-11-14')).toBeNull();
  });
});

describe('ageDescription', () => {
  it.each([
    ['2026-11-14', '2026-11-14', '0 days'],
    ['2026-11-13', '2026-11-14', '1 day'],
    ['2026-10-18', '2026-11-14', '27 days'],
    ['2026-10-17', '2026-11-14', '4 weeks'],
    ['2026-08-15', '2026-11-14', '13 weeks'],
    ['2026-08-14', '2026-11-14', '3 months'],
    ['2025-11-14', '2026-11-14', '12 months'],
    ['2024-11-15', '2026-11-14', '23 months'],
    ['2024-11-14', '2026-11-14', '2 years'],
    ['1980-03-14', '2026-03-14', '46 years'],
    // Across a month end: 31 January to 28 February is under a whole month, but 4 weeks.
    ['2026-01-31', '2026-02-28', '4 weeks'],
  ])('gives someone born %s, on %s, as %s', (dob, on, expected) => {
    expect(ageDescription(dob, on)).toBe(expected);
  });

  it.each(['2030-01-01', 'not-a-date', ''])('returns null for %s', (dob) => {
    expect(ageDescription(dob, '2026-11-14')).toBeNull();
  });

  it('counts from today by default', () => {
    expect(ageDescription(todayIso())).toBe('0 days');
  });
});

describe('formatDate', () => {
  it('formats a date the UK way', () => {
    expect(formatDate('1980-03-14')).toBe('14 March 1980');
  });

  it('returns anything else unchanged', () => {
    expect(formatDate('14/03/1980')).toBe('14/03/1980');
  });

  it('has a short form for tables', () => {
    expect(formatShortDate('1976-12-01')).toBe('1 Dec 1976');
  });

  it('keeps a date-only value (a due date) on its day, summer and winter, whatever the time zone', () => {
    expect(formatDate('2026-07-01')).toBe('1 July 2026');
    expect(formatShortDate('2026-07-01')).toBe('1 Jul 2026');
    expect(formatDate('2026-11-03')).toBe('3 November 2026');
    expect(formatShortDate('2026-11-03')).toBe('3 Nov 2026');
  });
});

describe('the test time zone', () => {
  it('is neither UTC nor UK time, so a UTC time misread as local shows', () => {
    expect(Intl.DateTimeFormat().resolvedOptions().timeZone).toBe('America/New_York');
    expect(new Date(2026, 6, 1).getTimezoneOffset()).toBe(240);
    expect(new Date(2026, 10, 3).getTimezoneOffset()).toBe(300);
  });
});

describe('parseUtc', () => {
  it.each([
    // As the API sends them: from the database without a zone (before it marked them UTC), and with Z.
    ['2026-07-01T13:05:00', Date.UTC(2026, 6, 1, 13, 5)],
    ['2026-07-01T13:05:00Z', Date.UTC(2026, 6, 1, 13, 5)],
    ['2026-11-03T13:05:00', Date.UTC(2026, 10, 3, 13, 5)],
    ['2026-11-03T13:05:00Z', Date.UTC(2026, 10, 3, 13, 5)],
    // .NET's seven decimal places.
    ['2026-11-15T03:30:25.0763552', Date.UTC(2026, 10, 15, 3, 30, 25, 76)],
    ['2026-11-15T03:30:25.0763552Z', Date.UTC(2026, 10, 15, 3, 30, 25, 76)],
    // An offset is kept as given.
    ['2026-07-01T14:05:00+01:00', Date.UTC(2026, 6, 1, 13, 5)],
    ['2026-07-01T09:05:00-0400', Date.UTC(2026, 6, 1, 13, 5)],
  ])('reads %s as UTC', (value, expected) => {
    expect(parseUtc(value).getTime()).toBe(expected);
  });

  it.each(['2026-07-01', 'not a date', ''])('does not read %j as a moment', (value) => {
    expect(Number.isNaN(parseUtc(value).getTime())).toBe(true);
  });
});

describe('formatDateTime', () => {
  it.each([
    ['2026-07-01T13:05:00Z', '1 Jul 2026, 14:05'],
    ['2026-07-01T13:05:00', '1 Jul 2026, 14:05'],
    ['2026-07-01T13:05:00.1234567', '1 Jul 2026, 14:05'],
    ['2026-11-03T13:05:00Z', '3 Nov 2026, 13:05'],
    ['2026-11-03T13:05:00', '3 Nov 2026, 13:05'],
    ['2026-11-03T13:05:00.1234567', '3 Nov 2026, 13:05'],
    // Across the clocks changing (29 March and 25 October 2026, at 01:00 UTC).
    ['2026-03-29T00:30:00', '29 Mar 2026, 00:30'],
    ['2026-03-29T01:30:00', '29 Mar 2026, 02:30'],
    ['2026-10-25T00:30:00', '25 Oct 2026, 01:30'],
    ['2026-10-25T01:30:00', '25 Oct 2026, 01:30'],
  ])('shows the UTC moment %s in UK time: %s', (value, expected) => {
    expect(formatDateTime(value)).toBe(expected);
  });

  it('leaves a date-only value as it is, as it is a whole day, not a moment', () => {
    expect(formatDateTime('2026-07-01')).toBe('2026-07-01');
  });

  it('returns anything it cannot read unchanged', () => {
    expect(formatDateTime('not a date')).toBe('not a date');
  });

  it('says how long ago a moment was, then its date and time from a day on', () => {
    const now = new Date('2026-07-01T12:00:00Z');
    expect(timeAgo('2026-07-01T11:59:40Z', now)).toBe('Just now');
    expect(timeAgo('2026-07-01T11:55:00', now)).toBe('5 min ago');
    expect(timeAgo('2026-07-01T11:00:00Z', now)).toBe('1 hour ago');
    expect(timeAgo('2026-07-01T09:00:00Z', now)).toBe('3 hours ago');
    expect(timeAgo('2026-06-29T09:00:00Z', now)).toBe('29 Jun 2026, 10:00');
  });

  it('reads a time without Z as UTC, summer and winter', () => {
    expect(timeAgo('2026-07-01T11:00:00', new Date('2026-07-01T12:00:00Z'))).toBe('1 hour ago');
    expect(timeAgo('2026-06-29T09:00:00', new Date('2026-07-01T12:00:00Z'))).toBe('29 Jun 2026, 10:00');
    expect(timeAgo('2026-11-15T11:00:00', new Date('2026-11-15T12:00:00Z'))).toBe('1 hour ago');
    expect(timeAgo('2026-11-13T09:00:00', new Date('2026-11-15T12:00:00Z'))).toBe('13 Nov 2026, 09:00');
  });

  it('gives back what it cannot read, and counts from now by default', () => {
    expect(timeAgo('not a date')).toBe('not a date');
    expect(timeAgo(new Date().toISOString())).toBe('Just now');
  });
});

describe('ukDay, formatDayHeading and formatTime', () => {
  it('reads a moment in UK time, in summer (BST) and winter (GMT)', () => {
    // 23:30 UTC in June is 00:30 the next day in the UK; in January, the same day.
    expect(ukDay('2026-06-15T23:30:00Z')).toBe('2026-06-16');
    expect(formatDayHeading('2026-06-15T23:30:00Z')).toBe('Tuesday 16 June 2026');
    expect(formatTime('2026-06-15T23:30:00Z')).toBe('00:30');
    expect(ukDay('2026-01-15T23:30:00Z')).toBe('2026-01-15');
    expect(formatTime('2026-01-15T23:30:00Z')).toBe('23:30');
  });

  it('takes a moment without a zone as UTC, as the API may send it', () => {
    expect(formatTime('2026-06-15T09:05:00')).toBe('10:05');
  });

  it('gives back what it cannot read', () => {
    expect(ukDay('soon')).toBe('soon');
    expect(formatDayHeading('soon')).toBe('soon');
    expect(formatTime('soon')).toBe('soon');
  });
});
