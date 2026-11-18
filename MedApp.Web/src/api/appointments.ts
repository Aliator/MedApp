import {queryOptions, useMutation, useQueryClient} from '@tanstack/react-query';
import {api, unwrap, type Schemas} from './client';
import type {paths} from './schema';
import {pathwayKeys} from './pathways';
import {can, type Permission} from '../auth/permissions';

/**
 * Appointments: a patient seen by one member of staff, optionally for a pathway's stage. Requested ones ("Needs
 * booking") have no time yet; Booked ones have a member of staff and a time; then Attended or Did not attend, or
 * Cancelled (docs/calendar-plan.md, decision 16). Any staff member can book, reschedule, cancel or record the outcome
 * of any appointment. One recorded after the event (a walk-in) can be withdrawn as Entered in error, by whoever
 * recorded it or an admin: it stays in the history, off every calendar.
 */
export type Appointment = Schemas['AppointmentResponse'];
export type AppointmentPage = Schemas['AppointmentResponsePagedResponse'];
export type AppointmentSearch = NonNullable<paths['/api/appointments/search']['get']['parameters']['query']>;

/** The statuses, by the names the API uses. */
export const APPOINTMENT_STATUSES = ['Requested', 'Booked', 'Attended', 'DidNotAttend', 'Cancelled', 'EnteredInError'] as const;
export type AppointmentStatus = (typeof APPOINTMENT_STATUSES)[number];

/** The lengths an appointment can be booked for, in minutes; 30 by default (decision 30). */
export const APPOINTMENT_LENGTHS = [15, 30, 45, 60, 90, 120] as const;
export const DEFAULT_APPOINTMENT_LENGTH = 30;

/**
 * Every appointment query is keyed under ['appointments'], and every calendar under ['calendar'], so a change to any
 * appointment refreshes both (an appointment shows on a calendar), along with pathways (a stage's visit) and the
 * patient timelines (which list appointment changes).
 */
export const appointmentKeys = {
  all: ['appointments'] as const,
  search: (search: AppointmentSearch) => ['appointments', 'search', search] as const,
  one: (id: string) => ['appointments', id] as const,
};

/** One appointment (`GET api/appointments/{id}`). A missing one is an ApiError with status 404. */
export function appointmentQuery(id: string) {
  return queryOptions({
    queryKey: appointmentKeys.one(id),
    queryFn: () => unwrap(api.GET('/api/appointments/{id}', {params: {path: {id}}})),
  });
}

/**
 * A page of appointments (`GET api/appointments/search`): by patient, pathway, stage, member of staff, statuses and
 * UK days; sorted by `start` (default; Requested ones last), `requested`, `dueDate` (the pathway's) or `patient`.
 */
export function appointmentsQuery(search: AppointmentSearch) {
  return queryOptions({
    queryKey: appointmentKeys.search(search),
    queryFn: () => unwrap(api.GET('/api/appointments/search', {params: {query: search}})),
  });
}

/** The most appointments the pathway and patient pages list (the API's largest page). */
export const LISTED_APPOINTMENTS = 100;

/** A pathway's appointments, every status, by start (Requested ones last): the pathway page's list and stage visits. */
export const pathwayAppointmentsQuery = (pathwayId: string) => appointmentsQuery({PathwayId: pathwayId, PageSize: LISTED_APPOINTMENTS});

/** A patient's appointments, every status, by start (Requested ones last): the patient page's list. */
export const patientAppointmentsQuery = (patientId: string) => appointmentsQuery({PatientId: patientId, PageSize: LISTED_APPOINTMENTS});

/** The statuses still to happen (Requires appointment, or booked): a stage can have one at a time, and they freeze when a record is restricted. */
export const OPEN_APPOINTMENT_STATUSES: AppointmentStatus[] = ['Requested', 'Booked'];

/** How many of a patient's appointments are open (Requested or Booked): one row of a search, for its total. */
export function patientOpenAppointmentCountQuery(patientId: string) {
  return queryOptions({
    queryKey: [...appointmentKeys.all, 'open-count', patientId],
    queryFn: async () => {
      const page = await unwrap(
        api.GET('/api/appointments/search', {params: {query: {PatientId: patientId, Statuses: OPEN_APPOINTMENT_STATUSES, PageSize: 1}}}),
      );
      return page.totalCount ?? 0;
    },
  });
}

/** Refreshes everything an appointment change can show up in. */
export function useRefreshAppointments() {
  const queryClient = useQueryClient();
  return async (appointment?: Appointment) => {
    if (appointment?.id) queryClient.setQueryData(appointmentKeys.one(appointment.id), appointment);
    await Promise.all([
      queryClient.invalidateQueries({queryKey: appointmentKeys.all}),
      queryClient.invalidateQueries({queryKey: ['calendar']}),
      queryClient.invalidateQueries({queryKey: pathwayKeys.all}),
      queryClient.invalidateQueries({queryKey: ['notifications']}),
    ]);
  };
}

/** A new appointment: with a member of staff and a time it's Booked; with none of them, Requested (Requires appointment). */
export type NewAppointment = Schemas['CreateAppointmentRequest'];

export function useCreateAppointment() {
  const refresh = useRefreshAppointments();
  return useMutation({
    mutationFn: (body: NewAppointment) => unwrap(api.POST('/api/appointments', {body})),
    onSuccess: refresh,
  });
}

/**
 * An appointment that has already happened (a walk-in, or one booked outside MedApp), recorded after the event with its
 * outcome: in the last 30 days, on a working day, inside the working day, started by now, and not over another of the
 * member of staff's appointments. For a stage whose visit is in Requires Appointment, that appointment is the one recorded.
 */
export type PastAppointment = Schemas['RecordPastAppointmentRequest'];

/** How far back a past appointment can be recorded, in days (the API's limit). */
export const PAST_APPOINTMENT_DAYS = 30;

export function useRecordPastAppointment() {
  const refresh = useRefreshAppointments();
  return useMutation({
    mutationFn: (body: PastAppointment) => unwrap(api.POST('/api/appointments/past', {body})),
    onSuccess: refresh,
  });
}

const APPOINTMENT = '/api/appointments/{id}';

/** What marking a walk-in Entered in error did: the appointment, and what the server says about its stage, if any. */
export type EnteredInErrorResult = Schemas['EnteredInErrorResponse'];

/**
 * Whether `user` may mark an appointment Entered in error (which needs BookAppointments): a holder of CorrectRecords
 * whenever the API allows it (`canMarkEnteredInError`: recorded after the event, still Attended or Did not attend, the
 * patient not anonymised), and whoever recorded it only in its first 30 days (`canRecorderMarkEnteredInError`).
 * Anyone else gets the API's 403.
 */
export const canMarkEnteredInError = (appointment: Appointment, user: {username: string; permissions: readonly Permission[]}) =>
  appointment.canMarkEnteredInError === true &&
  can(user, 'BookAppointments') &&
  (can(user, 'CorrectRecords') ||
    (appointment.bookedByUsername === user.username && appointment.canRecorderMarkEnteredInError === true));

/**
 * Everything that can be done to an existing appointment. Each returns the updated appointment, which replaces the
 * cached one; calendars, lists, pathways and timelines are refreshed.
 */
export function useAppointmentActions() {
  const refresh = useRefreshAppointments();
  const path = (id: string) => ({params: {path: {id}}});
  return {
    /** Books a Requested appointment, or reschedules a Booked one (another time, or another member of staff). */
    book: useMutation({
      mutationFn: ({id, ...body}: Schemas['BookAppointmentRequest'] & {id: string}) =>
        unwrap(api.POST(`${APPOINTMENT}/book`, {...path(id), body})),
      onSuccess: refresh,
    }),
    /** Attended or DidNotAttend, once its start time has passed (and to correct one). */
    recordOutcome: useMutation({
      mutationFn: ({id, outcome}: {id: string; outcome: 'Attended' | 'DidNotAttend'}) =>
        unwrap(api.POST(`${APPOINTMENT}/outcome`, {...path(id), body: {outcome}})),
      onSuccess: refresh,
    }),
    cancel: useMutation({
      mutationFn: ({id, reason}: {id: string; reason: string}) =>
        unwrap(api.POST(`${APPOINTMENT}/cancel`, {...path(id), body: {reason}})),
      onSuccess: refresh,
    }),
    saveNotes: useMutation({
      mutationFn: ({id, notes}: {id: string; notes: string}) => unwrap(api.PUT(`${APPOINTMENT}/notes`, {...path(id), body: {notes}})),
      onSuccess: refresh,
    }),
    /**
     * Withdraws a walk-in recorded in error (a visit that didn't happen, or on the wrong patient), with a reason: off the
     * calendar, kept in the history. A stage's visit it took over is asked for again where it can be (`stageMessage`).
     */
    markEnteredInError: useMutation({
      mutationFn: ({id, reason}: {id: string; reason: string}) =>
        unwrap(api.POST(`${APPOINTMENT}/entered-in-error`, {...path(id), body: {reason}})),
      onSuccess: (result: EnteredInErrorResult) => refresh(result.appointment),
    }),
  };
}

/** A status in words: "Requires appointment", "Booked", "Attended", "Did not attend", "Cancelled", "Entered in error". */
export function appointmentStatusLabel(status: string | null | undefined): string {
  switch (status) {
    case 'Requested':
      return 'Requires appointment';
    case 'DidNotAttend':
      return 'Did not attend';
    case 'EnteredInError':
      return 'Entered in error';
    default:
      return status ?? '';
  }
}
