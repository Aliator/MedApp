import {queryOptions, useMutation, useQueryClient} from '@tanstack/react-query';
import {calendarKeys} from './calendar';
import {api, unwrap, type Schemas} from './client';

/**
 * Closed days (docs/calendar-plan.md): bank holidays and other days the clinic is closed, set by admins for everyone.
 * A closed day is like a weekend: nothing new can be booked, blocked or arranged on it (the calendar's day has
 * `isWorkingDay` false and `closedDayName`), and anything already on it stays, flagged. Past ones can't change.
 */
export type ClosedDay = Schemas['ClosedDayResponse'];
export type AddedBankHolidays = Schemas['AddBankHolidaysResponse'];
export type SkippedBankHoliday = Schemas['SkippedBankHolidayResponse'];
/** What's on a day for everyone (appointments Booked, Attended or Did not attend, stored blocks, meetings), and whose. */
export type DayBookings = Schemas['DayBookingsResponse'];

export const closedDayKeys = {
  all: ['closedDays'] as const,
  range: (from: string | undefined, to: string | undefined) => ['closedDays', from ?? null, to ?? null] as const,
  bookings: (date: string) => ['closedDays', 'bookings', date] as const,
};

/** The closed days from `from` to `to` (UK dates, `yyyy-mm-dd`, each optional), by date. Anyone with a role can read them. */
export function closedDaysQuery({from, to}: {from?: string; to?: string} = {}) {
  return queryOptions({
    queryKey: closedDayKeys.range(from, to),
    queryFn: () => unwrap(api.GET('/api/calendar/closed-days', {params: {query: {From: from, To: to}}})),
  });
}

/** One year's closed days (`yyyy`), for the Closed Days page. */
export const closedDaysOfYearQuery = (year: number) => closedDaysQuery({from: `${year}-01-01`, to: `${year}-12-31`});

/**
 * What's already on a day (admins), before closing it: counts of appointments, blocks and meetings, and each member of
 * staff with something then. Closing it leaves them all where they are, and tells those staff.
 */
export function closedDayBookingsQuery(date: string) {
  return queryOptions({
    queryKey: closedDayKeys.bookings(date),
    queryFn: () => unwrap(api.GET('/api/calendar/closed-days/bookings', {params: {query: {date}}})),
  });
}

/** How many of something, in words: "1 appointment", "14 appointments". */
const count = (n: number, one: string) => `${n} ${n === 1 ? one : `${one}s`}`;

/** A list in words: "a", "a and b", "a, b and c". */
export const listWords = (items: string[]) => (items.length < 2 ? (items[0] ?? '') : `${items.slice(0, -1).join(', ')} and ${items.at(-1)}`);

/** Whether anything is on the day. */
export const hasBookings = (bookings: DayBookings | null | undefined) =>
  !!bookings && (bookings.appointments ?? 0) + (bookings.blocks ?? 0) + (bookings.meetings ?? 0) > 0;

/**
 * What's on a day, in words, for closing it: "On Fri 27 Nov: 14 appointments, 1 block and 1 meeting, for k.ashford and
 * s.brightwell. They stay; each of them will be told." (`isDone`: "…each of them has been told."), or "Nothing is booked
 * then." `dayName` names the day ("Fri 27 Nov").
 */
export function bookingsWords(bookings: DayBookings, dayName: string, {isDone = false}: {isDone?: boolean} = {}): string {
  if (!hasBookings(bookings)) return 'Nothing is booked then.';
  const what = listWords(
    [
      bookings.appointments ? count(bookings.appointments, 'appointment') : null,
      bookings.blocks ? count(bookings.blocks, 'block') : null,
      bookings.meetings ? count(bookings.meetings, 'meeting') : null,
    ].filter((part): part is string => part !== null),
  );
  const who = (bookings.staff ?? []).map((s) => s.username ?? '').filter(Boolean);
  const told = who.length === 0 ? '' : `; ${who.length === 1 ? who[0] : 'each of them'} ${isDone ? 'has been' : 'will be'} told`;
  return `On ${dayName}: ${what}${who.length > 0 ? `, for ${listWords(who)}` : ''}. They stay${told}.`;
}

/** Refreshes the closed days, and every calendar (meetings, the team view and availability too), as each shows them. */
function useRefreshClosedDays() {
  const queryClient = useQueryClient();
  return async () => {
    await Promise.all([
      queryClient.invalidateQueries({queryKey: closedDayKeys.all}),
      queryClient.invalidateQueries({queryKey: calendarKeys.all}),
    ]);
  };
}

/**
 * Closes a day (admins): a date today or later and a name. A past date, or one already closed, is a 409. The response
 * says what was already on it (`bookings`); each member of staff with something then is told (a Day closed notification).
 */
export function useAddClosedDay() {
  const onSuccess = useRefreshClosedDays();
  return useMutation({
    mutationFn: (body: {date: string; name: string}) => unwrap(api.POST('/api/calendar/closed-days', {body})),
    onSuccess,
  });
}

/** Opens a closed day again (admins). A past one is a 409. */
export function useDeleteClosedDay() {
  const onSuccess = useRefreshClosedDays();
  return useMutation({
    mutationFn: (id: string) => unwrap(api.DELETE('/api/calendar/closed-days/{id}', {params: {path: {id}}})),
    onSuccess,
  });
}

/**
 * Closes a year's England and Wales bank holidays (admins), in one call: it says which were added (each with what was
 * already on it, `bookings`, whose staff are told), and which were left out and why (`AlreadyClosed`, or `Past`).
 */
export function useAddBankHolidays() {
  const onSuccess = useRefreshClosedDays();
  return useMutation({
    mutationFn: (year: number) => unwrap(api.POST('/api/calendar/closed-days/bank-holidays', {body: {year}})),
    onSuccess,
  });
}
