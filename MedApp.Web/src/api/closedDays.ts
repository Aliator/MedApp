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

export const closedDayKeys = {
  all: ['closedDays'] as const,
  range: (from: string | undefined, to: string | undefined) => ['closedDays', from ?? null, to ?? null] as const,
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

/** Closes a day (admins): a date today or later and a name. A past date, or one already closed, is a 409. */
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
 * Closes a year's England and Wales bank holidays (admins), in one call: it says which were added, and which were left
 * out and why (`AlreadyClosed`, or `Past`).
 */
export function useAddBankHolidays() {
  const onSuccess = useRefreshClosedDays();
  return useMutation({
    mutationFn: (year: number) => unwrap(api.POST('/api/calendar/closed-days/bank-holidays', {body: {year}})),
    onSuccess,
  });
}
