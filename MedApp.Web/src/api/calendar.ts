import {queryOptions, useMutation, useQueryClient} from '@tanstack/react-query';
import {api, unwrap, type Schemas} from './client';
import {ApiError} from './errors';

/**
 * Staff calendars (docs/calendar-plan.md): an 08:00 to 18:00 working day, UK time, Monday to Friday, with blocked
 * time and appointments. Template blocks come from the person's default week template (or a day template applied to
 * that day), worked out by the API when the calendar is read. Times are UTC; parse them with `parseUtc`.
 *
 * Anyone can read any calendar; a calendar's blocks, day templates and default week are changed by its owner or an
 * admin (the response's `canEdit`). Day and week templates are each person's own: anyone reads them, their owner or an
 * admin changes them (their `canEdit`). Activity types are changed by admins. Meetings are arranged, changed and
 * cancelled by admins, and show on every attendee's calendar; each attendee accepts or declines for themselves (a
 * declined meeting is no longer busy time on their calendar). Admins also have the team view and suggested times.
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
export type Meeting = Schemas['MeetingResponse'];
export type MeetingAttendee = Schemas['MeetingAttendeeResponse'];
export type MeetingInput = Schemas['MeetingRequest'];
/** An attendee's answer to a meeting: `NoReply` until they answer (and again once it moves), `Accepted` or `Declined`. */
export type MeetingReply = 'NoReply' | 'Accepted' | 'Declined';
export type TeamCalendar = Schemas['TeamCalendarResponse'];
export type TimeRange = Schemas['TimeRangeResponse'];
export type Availability = Schemas['AvailabilityResponse'];
export type AvailabilityDay = Schemas['AvailabilityDayResponse'];
export type BusyTime = Schemas['BusyTimeResponse'];

/** The colours an activity type can have: Astryx Token colours, so their contrast comes from the theme. */
export const ACTIVITY_COLOURS = ['red', 'orange', 'yellow', 'green', 'teal', 'cyan', 'blue', 'purple', 'pink', 'gray'] as const;
export type ActivityColour = (typeof ACTIVITY_COLOURS)[number];

/**
 * Every calendar query is under ['calendar'] (meetings, the team view and availability too, so a change refreshes them
 * all); setup (activity types, templates) under ['calendarSetup'].
 */
export const calendarKeys = {
  all: ['calendar'] as const,
  calendar: (staffUserId: string | undefined, from: string, to: string) => ['calendar', 'days', staffUserId ?? 'me', from, to] as const,
  staff: ['calendar', 'staff'] as const,
  weekTemplateOf: (userId: string) => ['calendar', 'weekTemplate', userId] as const,
  team: (date: string, staffUserIds: readonly string[]) => ['calendar', 'team', date, [...staffUserIds].sort()] as const,
  availability: (input: AvailabilityInput) =>
    ['calendar', 'availability', [...input.staffUserIds].sort(), input.from, input.to, input.durationMinutes, input.exceptMeetingId ?? null] as const,
  meeting: (id: string) => ['calendar', 'meeting', id] as const,
  setup: ['calendarSetup'] as const,
  activityTypes: ['calendarSetup', 'activityTypes'] as const,
  dayTemplates: ['calendarSetup', 'dayTemplates'] as const,
  dayTemplatesOf: (ownerUserId: string) => ['calendarSetup', 'dayTemplates', 'of', ownerUserId] as const,
  weekTemplates: ['calendarSetup', 'weekTemplates'] as const,
  weekTemplatesOf: (ownerUserId: string) => ['calendarSetup', 'weekTemplates', 'of', ownerUserId] as const,
};

/** What `availabilityQuery` asks about: who, which UK days (at most 31), how long, and a meeting being moved. */
export interface AvailabilityInput {
  staffUserIds: readonly string[];
  from: string;
  to: string;
  durationMinutes: number;
  exceptMeetingId?: string;
}

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

/**
 * The same calendar without its appointments, so it names no patients and the API records no reads of them: for a
 * view that shows none, such as Apply Template's days (each day's template, and whether it's the day's own).
 */
export function calendarDaysQuery({staffUserId, from, to}: {staffUserId?: string; from: string; to: string}) {
  return queryOptions({
    queryKey: [...calendarKeys.calendar(staffUserId, from, to), 'withoutAppointments'] as const,
    queryFn: () =>
      unwrap(api.GET('/api/calendar', {params: {query: {StaffUserId: staffUserId, From: from, To: to, WithoutAppointments: true}}})),
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

/** One person's day templates, archived ones included. */
export function dayTemplatesQuery(ownerUserId: string) {
  return queryOptions({
    queryKey: calendarKeys.dayTemplatesOf(ownerUserId),
    queryFn: () =>
      unwrap(api.GET('/api/calendar/staff/{userId}/day-templates', {params: {path: {userId: ownerUserId}, query: {includeArchived: true}}})),
  });
}

/** One day template (its current blocks). A missing one is an ApiError with status 404. */
export function dayTemplateQuery(id: string) {
  return queryOptions({
    queryKey: [...calendarKeys.dayTemplates, id],
    queryFn: () => unwrap(api.GET('/api/calendar/day-templates/{id}', {params: {path: {id}}})),
  });
}

/** One person's week templates, archived ones included. */
export function weekTemplatesQuery(ownerUserId: string) {
  return queryOptions({
    queryKey: calendarKeys.weekTemplatesOf(ownerUserId),
    queryFn: () =>
      unwrap(api.GET('/api/calendar/staff/{userId}/week-templates', {params: {path: {userId: ownerUserId}, query: {includeArchived: true}}})),
  });
}

/**
 * The team view (admins): one UK day of several people's calendars side by side (everyone with a staff role when
 * `staffUserIds` is empty), and `everyoneFree`, when they're all free (after now, inside the working day).
 */
export function teamCalendarQuery({date, staffUserIds}: {date: string; staffUserIds: readonly string[]}) {
  return queryOptions({
    queryKey: calendarKeys.team(date, staffUserIds),
    queryFn: () =>
      unwrap(api.GET('/api/calendar/team', {params: {query: {Date: date, StaffUserIds: staffUserIds.length ? [...staffUserIds] : undefined}}})),
  });
}

/**
 * When several people are all free (admins): each working day's free time, up to 10 suggested times for a meeting of
 * `durationMinutes` (earliest first, at most 3 a day), and each person's busy times, which never name a patient.
 */
export function availabilityQuery(input: AvailabilityInput) {
  return queryOptions({
    queryKey: calendarKeys.availability(input),
    queryFn: () =>
      unwrap(
        api.GET('/api/calendar/availability', {
          params: {
            query: {
              StaffUserIds: [...input.staffUserIds],
              From: input.from,
              To: input.to,
              DurationMinutes: input.durationMinutes,
              ExceptMeetingId: input.exceptMeetingId,
            },
          },
        }),
      ),
  });
}

/** One meeting. A missing (or cancelled) one is an ApiError with status 404. */
export function meetingQuery(id: string) {
  return queryOptions({
    queryKey: calendarKeys.meeting(id),
    queryFn: () => unwrap(api.GET('/api/calendar/meetings/{id}', {params: {path: {id}}})),
  });
}

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

/**
 * Adds a day template to `ownerUserId`'s own (no `id`) or saves one (its owner stays); changes apply from today. Its
 * owner or an admin.
 */
export function useSaveDayTemplate() {
  const onSuccess = useRefreshCalendars();
  return useMutation({
    mutationFn: ({id, ownerUserId, ...body}: DayTemplateInput & {id?: string; ownerUserId: string}) =>
      id
        ? unwrap(api.PUT('/api/calendar/day-templates/{id}', {params: {path: {id}}, body}))
        : unwrap(api.POST('/api/calendar/staff/{userId}/day-templates', {params: {path: {userId: ownerUserId}}, body})),
    onSuccess,
  });
}

/**
 * Adds a week template to `ownerUserId`'s own (no `id`) or saves one, using only their day templates; changes apply
 * from today. Its owner or an admin.
 */
export function useSaveWeekTemplate() {
  const onSuccess = useRefreshCalendars();
  return useMutation({
    mutationFn: ({id, ownerUserId, ...body}: WeekTemplateInput & {id?: string; ownerUserId: string}) =>
      id
        ? unwrap(api.PUT('/api/calendar/week-templates/{id}', {params: {path: {id}}, body}))
        : unwrap(api.POST('/api/calendar/staff/{userId}/week-templates', {params: {path: {userId: ownerUserId}}, body})),
    onSuccess,
  });
}

/**
 * Arranging, changing and cancelling meetings (admins). The API refuses a past day (409), a time outside the working
 * day (400) and a clash on any attendee's calendar (409, naming who and what: "k.ashford has Lunch, 12:00 to 13:00.").
 * A change sends the meeting's `lastUpdated` from the copy it was made from (`expectedLastUpdated`), and the API refuses
 * it (409) if anyone has changed the meeting since; the calendars are then refreshed, so a retry starts from theirs.
 */
export function useMeetingActions() {
  const onSuccess = useRefreshCalendars();
  return {
    create: useMutation({
      mutationFn: (body: MeetingInput) => unwrap(api.POST('/api/calendar/meetings', {body})),
      onSuccess,
    }),
    save: useMutation({
      mutationFn: ({id, ...body}: MeetingInput & {id: string}) =>
        unwrap(api.PUT('/api/calendar/meetings/{id}', {params: {path: {id}}, body})),
      onSuccess,
      onError: (error) => {
        if (error instanceof ApiError && error.status === 409) void onSuccess();
      },
    }),
    cancel: useMutation({
      mutationFn: (id: string) => unwrap(api.DELETE('/api/calendar/meetings/{id}', {params: {path: {id}}})),
      onSuccess,
    }),
  };
}

/**
 * The signed-in member of staff accepts or declines a meeting they're going to (its `canReply`), changing an earlier
 * reply if they like. The API refuses someone not going (403), a cancelled meeting (404), one that has ended (409),
 * and accepting again after declining once that time has been used (409, naming what's there). It isn't a change to
 * the meeting, so it sends no version. The calendars are refreshed after it, or after a 404 or 409: a decline frees
 * the time, and a refusal means what was shown is out of date.
 */
export function useMeetingReply() {
  const onSuccess = useRefreshCalendars();
  return useMutation({
    mutationFn: ({id, reply}: {id: string; reply: Exclude<MeetingReply, 'NoReply'>}) =>
      unwrap(api.PUT('/api/calendar/meetings/{id}/reply', {params: {path: {id}}, body: {reply}})),
    onSuccess,
    onError: (error) => {
      if (error instanceof ApiError && (error.status === 404 || error.status === 409)) void onSuccess();
    },
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
