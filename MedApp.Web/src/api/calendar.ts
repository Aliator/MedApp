import {queryOptions, useMutation, useQueryClient} from '@tanstack/react-query';
import {api, unwrap, type Schemas} from './client';

/**
 * Staff calendars (docs/calendar-plan.md): an 08:00 to 18:00 working day, UK time, Monday to Friday, with blocked
 * time and appointments. Template blocks come from the person's default week template (or a day template applied to
 * that day), worked out by the API when the calendar is read. Times are UTC; parse them with `parseUtc`.
 *
 * Anyone can read any calendar; a calendar's blocks, day templates and default week are changed by its owner or an
 * admin (the response's `canEdit`). Activity types, day templates and week templates are changed by admins.
 */
export type Calendar = Schemas['CalendarResponse'];
export type CalendarDay = Schemas['CalendarDayResponse'];
export type CalendarBlock = Schemas['CalendarBlockResponse'];
export type StaffMember = Schemas['StaffMemberResponse'];
export type StaffWeekTemplate = Schemas['StaffWeekTemplateResponse'];
export type ActivityType = Schemas['ActivityTypeResponse'];
export type DayTemplate = Schemas['DayTemplateResponse'];
export type DayTemplateBlock = Schemas['DayTemplateBlockResponse'];
export type WeekTemplate = Schemas['WeekTemplateResponse'];
export type DayTemplateInput = Schemas['DayTemplateRequest'];
export type WeekTemplateInput = Schemas['WeekTemplateRequest'];
export type BlockInput = Schemas['UpdateCalendarBlockRequest'];

/** The colours an activity type can have: Astryx Token colours, so their contrast comes from the theme. */
export const ACTIVITY_COLOURS = ['red', 'orange', 'yellow', 'green', 'teal', 'cyan', 'blue', 'purple', 'pink', 'gray'] as const;
export type ActivityColour = (typeof ACTIVITY_COLOURS)[number];

/** Every calendar query is under ['calendar']; setup (activity types, templates) under ['calendarSetup']. */
export const calendarKeys = {
  all: ['calendar'] as const,
  calendar: (staffUserId: string | undefined, from: string, to: string) => ['calendar', 'days', staffUserId ?? 'me', from, to] as const,
  staff: ['calendar', 'staff'] as const,
  weekTemplateOf: (userId: string) => ['calendar', 'weekTemplate', userId] as const,
  setup: ['calendarSetup'] as const,
  activityTypes: ['calendarSetup', 'activityTypes'] as const,
  dayTemplates: ['calendarSetup', 'dayTemplates'] as const,
  weekTemplates: ['calendarSetup', 'weekTemplates'] as const,
};

/**
 * One person's calendar from `from` to `to` (UK days, `yyyy-mm-dd`, at most 31 days): its days, blocks (stored and
 * from templates) and appointments. No `staffUserId` means the signed-in user's.
 */
export function calendarQuery({staffUserId, from, to}: {staffUserId?: string; from: string; to: string}) {
  return queryOptions({
    queryKey: calendarKeys.calendar(staffUserId, from, to),
    queryFn: () =>
      unwrap(api.GET('/api/calendar', {params: {query: {StaffUserId: staffUserId, From: from, To: to}}})),
  });
}

/** Everyone with a role (who can have a calendar), by username, with their default week template. */
export const staffQuery = queryOptions({
  queryKey: calendarKeys.staff,
  queryFn: () => unwrap(api.GET('/api/calendar/staff')),
});

/** A person's default week template. */
export function staffWeekTemplateQuery(userId: string) {
  return queryOptions({
    queryKey: calendarKeys.weekTemplateOf(userId),
    queryFn: () => unwrap(api.GET('/api/calendar/staff/{userId}/week-template', {params: {path: {userId}}})),
  });
}

/** Every activity type, archived ones included (they carry `isArchived`). */
export const activityTypesQuery = queryOptions({
  queryKey: calendarKeys.activityTypes,
  queryFn: () => unwrap(api.GET('/api/calendar/activity-types', {params: {query: {includeArchived: true}}})),
});

/** Every day template, archived ones included. */
export const dayTemplatesQuery = queryOptions({
  queryKey: calendarKeys.dayTemplates,
  queryFn: () => unwrap(api.GET('/api/calendar/day-templates', {params: {query: {includeArchived: true}}})),
});

/** One day template (its current blocks). A missing one is an ApiError with status 404. */
export function dayTemplateQuery(id: string) {
  return queryOptions({
    queryKey: [...calendarKeys.dayTemplates, id],
    queryFn: () => unwrap(api.GET('/api/calendar/day-templates/{id}', {params: {path: {id}}})),
  });
}

/** Every week template, archived ones included. */
export const weekTemplatesQuery = queryOptions({
  queryKey: calendarKeys.weekTemplates,
  queryFn: () => unwrap(api.GET('/api/calendar/week-templates', {params: {query: {includeArchived: true}}})),
});

/** One week template (always Monday to Friday). A missing one is an ApiError with status 404. */
export function weekTemplateQuery(id: string) {
  return queryOptions({
    queryKey: [...calendarKeys.weekTemplates, id],
    queryFn: () => unwrap(api.GET('/api/calendar/week-templates/{id}', {params: {path: {id}}})),
  });
}

/** Refreshes calendars and setup: templates and activity types show on calendars. */
function useRefreshCalendars() {
  const queryClient = useQueryClient();
  return async () => {
    await Promise.all([
      queryClient.invalidateQueries({queryKey: calendarKeys.all}),
      queryClient.invalidateQueries({queryKey: calendarKeys.setup}),
    ]);
  };
}

const DAY = '/api/calendar/staff/{userId}/days/{date}';

/**
 * Changes to one person's calendar: blocks, one day's template blocks, the day's template, and their default week.
 * Its owner or an admin; the API refuses anyone else (403), anything on a past day (409) and clashes (409).
 */
export function useCalendarActions() {
  const onSuccess = useRefreshCalendars();
  return {
    addBlock: useMutation({
      mutationFn: (body: Schemas['CreateCalendarBlockRequest']) => unwrap(api.POST('/api/calendar/blocks', {body})),
      onSuccess,
    }),
    saveBlock: useMutation({
      mutationFn: ({id, ...body}: BlockInput & {id: string}) =>
        unwrap(api.PUT('/api/calendar/blocks/{id}', {params: {path: {id}}, body})),
      onSuccess,
    }),
    deleteBlock: useMutation({
      mutationFn: (id: string) => unwrap(api.DELETE('/api/calendar/blocks/{id}', {params: {path: {id}}})),
      onSuccess,
    }),
    /** Changes a template block on one day only (stores an exception and a block with the new values). */
    changeTemplateBlock: useMutation({
      mutationFn: ({userId, date, templateBlockId, ...body}: BlockInput & {userId: string; date: string; templateBlockId: string}) =>
        unwrap(api.PUT(`${DAY}/template-blocks/{templateBlockId}`, {params: {path: {userId, date, templateBlockId}}, body})),
      onSuccess,
    }),
    /** Removes a template block from one day only. */
    removeTemplateBlock: useMutation({
      mutationFn: ({userId, date, templateBlockId}: {userId: string; date: string; templateBlockId: string}) =>
        unwrap(api.DELETE(`${DAY}/template-blocks/{templateBlockId}`, {params: {path: {userId, date, templateBlockId}}})),
      onSuccess,
    }),
    /** Uses a day template (or none, `dayTemplateId: null`) on one day instead of the week template's. */
    applyDayTemplate: useMutation({
      mutationFn: ({userId, date, dayTemplateId}: {userId: string; date: string; dayTemplateId: string | null}) =>
        unwrap(api.PUT(`${DAY}/day-template`, {params: {path: {userId, date}}, body: {dayTemplateId}})),
      onSuccess,
    }),
    /** Goes back to the week template's day template on that day. */
    clearDayTemplate: useMutation({
      mutationFn: ({userId, date}: {userId: string; date: string}) =>
        unwrap(api.DELETE(`${DAY}/day-template`, {params: {path: {userId, date}}})),
      onSuccess,
    }),
    /** Sets (or, with null, removes) a person's default week template, from today. */
    setWeekTemplate: useMutation({
      mutationFn: ({userId, weekTemplateId}: {userId: string; weekTemplateId: string | null}) =>
        unwrap(api.PUT('/api/calendar/staff/{userId}/week-template', {params: {path: {userId}}, body: {weekTemplateId}})),
      onSuccess,
    }),
  };
}

/** Adds an activity type (no `id`) or saves one. Admins only. */
export function useSaveActivityType() {
  const onSuccess = useRefreshCalendars();
  return useMutation({
    mutationFn: ({id, name, colour}: {id?: string; name: string; colour: string}) =>
      id
        ? unwrap(api.PATCH('/api/calendar/activity-types/{id}', {params: {path: {id}}, body: {name, colour}}))
        : unwrap(api.POST('/api/calendar/activity-types', {body: {name, colour}})),
    onSuccess,
  });
}

/** Adds a day template (no `id`) or saves one; changes apply from today. Admins only. */
export function useSaveDayTemplate() {
  const onSuccess = useRefreshCalendars();
  return useMutation({
    mutationFn: ({id, ...body}: DayTemplateInput & {id?: string}) =>
      id
        ? unwrap(api.PUT('/api/calendar/day-templates/{id}', {params: {path: {id}}, body}))
        : unwrap(api.POST('/api/calendar/day-templates', {body})),
    onSuccess,
  });
}

/** Adds a week template (no `id`) or saves one; changes apply from today. Admins only. */
export function useSaveWeekTemplate() {
  const onSuccess = useRefreshCalendars();
  return useMutation({
    mutationFn: ({id, ...body}: WeekTemplateInput & {id?: string}) =>
      id
        ? unwrap(api.PUT('/api/calendar/week-templates/{id}', {params: {path: {id}}, body}))
        : unwrap(api.POST('/api/calendar/week-templates', {body})),
    onSuccess,
  });
}

type SetupKind = 'activity-types' | 'day-templates' | 'week-templates';

/**
 * Archive, restore and delete for activity types, day templates and week templates. One in use is archived (no
 * longer offered, but kept where it's used); only one never used can be deleted (its `canBeDeleted`).
 */
export function useCalendarSetupActions(kind: SetupKind) {
  const onSuccess = useRefreshCalendars();
  const one = `/api/calendar/${kind}/{id}` as const;
  const path = (id: string) => ({params: {path: {id}}});
  return {
    archive: useMutation({
      mutationFn: (id: string) => unwrap(api.POST(`${one}/archive` as '/api/calendar/activity-types/{id}/archive', path(id))),
      onSuccess,
    }),
    restore: useMutation({
      mutationFn: (id: string) => unwrap(api.POST(`${one}/restore` as '/api/calendar/activity-types/{id}/restore', path(id))),
      onSuccess,
    }),
    remove: useMutation({
      mutationFn: (id: string) => unwrap(api.DELETE(one as '/api/calendar/activity-types/{id}', path(id))),
      onSuccess,
    }),
  };
}
