import {calendarDay, STAFF_ME, STAFF_OTHER, testAppointment, testBlock, testCalendar, testMeeting, ukTime} from '../test/calendar';
import {
  addDays,
  addMinutes,
  busyTimes,
  dayOfWeek,
  formatCalendarDay,
  formatShortDay,
  formatShortDayDate,
  formatWeekday,
  formatTimeRange,
  freeStarts,
  freeStartsByDay,
  intervalOf,
  isMorning,
  isWeekday,
  minutesBetween,
  nextWeekday,
  sameMoment,
  ukToday,
  weekStart,
  worksThatDay,
} from './calendarSlots';
import {formatTime} from './dates';
import {testSession} from '../test/clinics';

// Monday 23 November 2026, early in the morning UK time.
const MONDAY = '2026-11-23';
const EARLY = new Date('2026-11-23T06:00:00Z');

describe('calendar days', () => {
  it('steps through dates, across months and years', () => {
    expect(addDays('2026-11-30', 1)).toBe('2026-12-01');
    expect(addDays('2026-12-31', 1)).toBe('2027-01-01');
    expect(addDays('2026-03-01', -1)).toBe('2026-02-28');
  });

  it('knows the day of the week, Monday first', () => {
    expect(dayOfWeek(MONDAY)).toBe(1);
    expect(dayOfWeek('2026-11-29')).toBe(7);
    expect(isWeekday('2026-11-27')).toBe(true);
    expect(isWeekday('2026-11-28')).toBe(false);
  });

  it('knows whether someone works a day: their hours say, or a weekday without them', () => {
    expect(worksThatDay({date: '2026-11-28', hasHours: true})).toBe(true);
    expect(worksThatDay({date: MONDAY, hasHours: false})).toBe(false);
    expect(worksThatDay({date: MONDAY})).toBe(true);
    expect(worksThatDay({date: '2026-11-28'})).toBe(false);
    expect(worksThatDay({})).toBe(false);
  });

  it('finds the Monday of a week', () => {
    expect(weekStart(MONDAY)).toBe(MONDAY);
    expect(weekStart('2026-11-29')).toBe(MONDAY);
    expect(weekStart('2026-11-25')).toBe(MONDAY);
  });

  it('skips weekends going forwards and back', () => {
    expect(nextWeekday('2026-11-27', 1)).toBe('2026-11-30');
    expect(nextWeekday(MONDAY, -1)).toBe('2026-11-20');
    expect(nextWeekday('2026-11-24', 1)).toBe('2026-11-25');
  });

  it('gives today in the UK, not in this browser', () => {
    // 23:30 in New York on the 22nd is 04:30 on the 23rd in London.
    expect(ukToday(new Date('2026-11-23T04:30:00Z'))).toBe(MONDAY);
    expect(ukToday(new Date('2026-07-01T23:30:00Z'))).toBe('2026-07-02');
  });
});

describe('moments', () => {
  it('adds minutes and measures between moments in UTC', () => {
    expect(addMinutes('2026-11-23T09:00:00Z', 45)).toBe('2026-11-23T09:45:00.000Z');
    expect(minutesBetween('2026-11-23T09:00:00Z', '2026-11-23T10:30:00Z')).toBe(90);
  });

  it('compares moments however they are written', () => {
    expect(sameMoment('2026-11-23T09:00:00Z', '2026-11-23T09:00:00.000Z')).toBe(true);
    expect(sameMoment('2026-11-23T09:00:00Z', '2026-11-23T09:15:00Z')).toBe(false);
    expect(sameMoment(null, '2026-11-23T09:00:00Z')).toBe(false);
  });

  it('splits the day into morning and afternoon, UK time', () => {
    expect(isMorning(ukTime(MONDAY, '11:45'))).toBe(true);
    expect(isMorning(ukTime(MONDAY, '12:00'))).toBe(false);
    // 11:00 UK in summer is 10:00 UTC; 12:00 UK is 11:00 UTC.
    expect(isMorning(ukTime('2026-07-01', '11:00'))).toBe(true);
    expect(isMorning(ukTime('2026-07-01', '12:00'))).toBe(false);
  });
});

describe('busyTimes', () => {
  it('counts blocks and appointments that take time, leaving out the one being rescheduled', () => {
    const calendar = {
      blocks: [testBlock(MONDAY, '12:00', '13:00')],
      appointments: [
        testAppointment({id: 'a', date: MONDAY, start: '09:00', end: '09:30'}),
        testAppointment({id: 'b', date: MONDAY, start: '10:00', end: '10:30', status: 'Attended'}),
        testAppointment({id: 'c', date: MONDAY, start: '11:00', end: '11:30', status: 'Cancelled'}),
        testAppointment({id: 'd', date: MONDAY, start: '14:00', end: '14:30', status: 'DidNotAttend'}),
      ],
    };

    expect(busyTimes(calendar)).toHaveLength(4);
    expect(busyTimes(calendar, 'a')).toHaveLength(3);
    expect(busyTimes({})).toEqual([]);
  });

  it('counts meetings as busy, so nothing is booked or blocked over one', () => {
    const meeting = testMeeting({date: MONDAY, start: '09:00', end: '10:00'});
    const calendar = testCalendar({from: MONDAY, dayCount: 1, today: MONDAY, meetings: [meeting, {...meeting, id: 'no-time', startUtc: undefined}]});

    expect(busyTimes(calendar)).toEqual([intervalOf(meeting.startUtc!, meeting.endUtc!)]);
    const times = freeStartsByDay(calendar, 30, EARLY)[MONDAY].map(formatTime);
    expect(times).toContain('08:30');
    expect(times).not.toContain('09:00');
    expect(times).not.toContain('09:30');
    expect(times).toContain('10:00');
  });

  it('counts a clinic session as busy (appointments go into its slots), but not a cancelled one', () => {
    const session = testSession({date: MONDAY, starts: '09:00', ends: '10:00'});
    const calendar = testCalendar({from: MONDAY, dayCount: 1, today: MONDAY, sessions: [session, {...session, id: 'cancelled', status: 'Cancelled', startUtc: ukTime(MONDAY, '14:00'), endUtc: ukTime(MONDAY, '15:00')}]});

    expect(busyTimes(calendar)).toEqual([intervalOf(session.startUtc!, session.endUtc!)]);
  });

  it("leaves out a meeting the calendar's owner declined, so its time is free for them, but not for anyone else", () => {
    const meeting = testMeeting({
      date: MONDAY,
      start: '09:00',
      end: '10:00',
      attendees: [
        {userId: STAFF_ME.userId, username: STAFF_ME.username, reply: 'Declined'},
        {userId: STAFF_OTHER.userId, username: STAFF_OTHER.username, reply: 'Accepted'},
      ],
    });
    const mine = testCalendar({from: MONDAY, dayCount: 1, today: MONDAY, meetings: [meeting]});
    const theirs = testCalendar({from: MONDAY, dayCount: 1, today: MONDAY, staff: STAFF_OTHER, meetings: [meeting]});

    expect(busyTimes(mine)).toEqual([]);
    expect(freeStartsByDay(mine, 30, EARLY)[MONDAY].map(formatTime)).toEqual(expect.arrayContaining(['09:00', '09:30']));
    expect(busyTimes(theirs)).toEqual([intervalOf(meeting.startUtc!, meeting.endUtc!)]);
    expect(freeStartsByDay(theirs, 30, EARLY)[MONDAY].map(formatTime)).not.toContain('09:30');
  });
});

describe('freeStarts', () => {
  const day = calendarDay(MONDAY, MONDAY);

  it('offers every 15 minutes of an empty day that leaves room before its end', () => {
    const starts = freeStarts(day, [], 30, EARLY);

    expect(starts).toHaveLength(39);
    expect(formatTime(starts[0])).toBe('08:00');
    expect(formatTime(starts[starts.length - 1])).toBe('17:30');
  });

  it('offers every 5 minutes instead, for a calendar showing 5-minute rows', () => {
    const starts = freeStarts(day, [], 30, EARLY, 5).map(formatTime);
    expect(starts).toHaveLength(115);
    expect(starts.slice(0, 3)).toEqual(['08:00', '08:05', '08:10']);
    expect(freeStartsByDay({days: [day]}, 30, EARLY, undefined, 5)[MONDAY]).toHaveLength(115);
  });

  it('leaves out times that would overlap something, and fits up to it exactly', () => {
    const busy = [intervalOf(ukTime(MONDAY, '09:00'), ukTime(MONDAY, '10:00'))];
    const times = freeStarts(day, busy, 30, EARLY).map(formatTime);

    expect(times).toContain('08:30');
    expect(times).not.toContain('08:45');
    expect(times).not.toContain('09:30');
    expect(times).toContain('10:00');
  });

  it('leaves out times that have passed', () => {
    const times = freeStarts(day, [], 15, new Date(ukTime(MONDAY, '10:05'))).map(formatTime);

    expect(times[0]).toBe('10:15');
  });

  it('offers nothing on a weekend, a past day, or for no length', () => {
    expect(freeStarts(calendarDay('2026-11-28', MONDAY), [], 30, EARLY)).toEqual([]);
    expect(freeStarts(calendarDay('2026-11-20', MONDAY), [], 30, EARLY)).toEqual([]);
    expect(freeStarts(day, [], 0, EARLY)).toEqual([]);
    expect(freeStarts({...day, workStartUtc: undefined}, [], 30, EARLY)).toEqual([]);
  });

  it('starts at 08:00 UK time on both sides of a clock change', () => {
    // The clocks went back on Sunday 25 October 2026: Friday the 23rd was summer time, Monday the 26th isn't.
    const calendar = testCalendar({from: '2026-10-19', dayCount: 14, today: '2026-10-19'});
    const byDay = freeStartsByDay(calendar, 60, new Date('2026-10-19T05:00:00Z'));

    expect(byDay['2026-10-23'][0]).toBe('2026-10-23T07:00:00.000Z');
    expect(byDay['2026-10-26'][0]).toBe('2026-10-26T08:00:00.000Z');
    expect(formatTime(byDay['2026-10-23'][0])).toBe('08:00');
    expect(formatTime(byDay['2026-10-26'][0])).toBe('08:00');
    expect(formatTime(byDay['2026-10-26'].at(-1)!)).toBe('17:00');
    expect(byDay['2026-10-24']).toEqual([]);
    expect(byDay['2026-10-25']).toEqual([]);
  });
});

describe('freeStartsByDay', () => {
  it("lets a rescheduled appointment move into its own time, but not anyone else's", () => {
    const own = testAppointment({id: 'own', date: MONDAY, start: '09:00', end: '09:30'});
    const calendar = testCalendar({from: MONDAY, dayCount: 1, today: MONDAY, appointments: [own]});

    const ignoringOwn = freeStartsByDay(calendar, 30, EARLY, 'own')[MONDAY].map(formatTime);
    const notIgnoring = freeStartsByDay(calendar, 30, EARLY)[MONDAY].map(formatTime);

    expect(ignoringOwn).toContain('09:00');
    expect(ignoringOwn).toContain('08:45');
    expect(notIgnoring).not.toContain('09:00');
    expect(notIgnoring).not.toContain('08:45');
  });

  it('counts template blocks as taken, and gives no slots for a calendar without days', () => {
    const calendar = testCalendar({
      from: MONDAY,
      dayCount: 1,
      today: MONDAY,
      blocks: [testBlock(MONDAY, '08:00', '17:00', {isFromTemplate: true})],
    });

    expect(freeStartsByDay(calendar, 60, EARLY)[MONDAY].map(formatTime)).toEqual(['17:00']);
    expect(freeStartsByDay({}, 30, EARLY)).toEqual({});
  });
});

describe('words', () => {
  it('names days and times as UK readers expect', () => {
    expect(formatCalendarDay(MONDAY)).toBe('Monday 23 November');
    expect(formatShortDay(MONDAY)).toBe('Mon 23');
    expect(formatWeekday(MONDAY)).toBe('Monday');
    expect(formatShortDayDate('2026-12-25')).toBe('Fri 25 Dec 2026');
    expect(formatShortDayDate('2026-12-25', {withYear: false})).toBe('Fri 25 Dec');
    expect(formatTimeRange(ukTime(MONDAY, '09:00'), ukTime(MONDAY, '09:30'))).toBe('09:00 to 09:30');
  });
});
