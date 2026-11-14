import {formatDateTime, ageOn, formatDate, formatShortDate, todayIso} from './dates';

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
});

describe('formatDateTime', () => {
  it('shows a UTC moment in UK time, summer and winter', () => {
    expect(formatDateTime('2026-07-01T13:05:00Z')).toBe('1 Jul 2026, 14:05');
    expect(formatDateTime('2026-11-03T13:05:00Z')).toBe('3 Nov 2026, 13:05');
  });

  it('returns anything it cannot read unchanged', () => {
    expect(formatDateTime('not a date')).toBe('not a date');
  });
});
