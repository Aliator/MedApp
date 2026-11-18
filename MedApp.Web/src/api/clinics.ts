import {queryOptions, useMutation, useQueryClient} from '@tanstack/react-query';
import {appointmentKeys} from './appointments';
import {calendarKeys} from './calendar';
import {api, unwrap, type Schemas} from './client';

/**
 * Clinic sessions (docs/centre-phases-3-5-plan.md, decisions 123 to 127): a clinician's time in a room for a specialty,
 * cut into slots that appointments are booked into, one patient each. Anyone reads them; holders of ManageClinics add,
 * change and cancel them, and keep the clinic patterns that make them when asked (Create sessions). A session is busy
 * time on its clinician's calendar, and a room holds one session at a time.
 */
export type ClinicSession = Schemas['ClinicSessionResponse'];
export type ClinicSessionDetail = Schemas['ClinicSessionDetailResponse'];
export type ClinicSlot = Schemas['ClinicSlotResponse'];
export type ClinicSessionInput = Schemas['ClinicSessionRequest'];
export type CancelledSession = Schemas['CancelClinicSessionResponse'];
export type PatientToTell = Schemas['PatientToTellResponse'];
export type ClinicPattern = Schemas['ClinicPatternResponse'];
export type ClinicPatternInput = Schemas['ClinicPatternRequest'];
export type CreatedSessions = Schemas['CreateSessionsResponse'];

/** The shortest and longest slot, in minutes (the API's `ClinicLimits`). */
export const MIN_SLOT_MINUTES = 5;
export const MAX_SLOT_MINUTES = 120;
/** Create sessions makes them for 1 to this many weeks ahead. */
export const MAX_WEEKS_AHEAD = 13;
/** The slot lengths offered. */
export const SLOT_LENGTHS = [10, 15, 20, 30, 40, 45, 60] as const;

export interface SessionsFilter {
  from: string;
  to: string;
  staffUserId?: string;
  siteId?: string;
  roomId?: string;
  specialtyId?: string;
  includeCancelled?: boolean;
  /** Only sessions with a slot free and not yet started (for booking). */
  withFreeSlots?: boolean;
}

/** Every clinic query is under ['clinics'], so one change refreshes them all. */
export const clinicKeys = {
  all: ['clinics'] as const,
  sessions: (filter: SessionsFilter) => ['clinics', 'sessions', filter] as const,
  session: (id: string) => ['clinics', 'session', id] as const,
  patterns: ['clinics', 'patterns'] as const,
};

/** Sessions starting from one UK date to another (at most 62 days), by start. */
export function sessionsQuery(filter: SessionsFilter) {
  return queryOptions({
    queryKey: clinicKeys.sessions(filter),
    queryFn: () =>
      unwrap(
        api.GET('/api/clinics/sessions', {
          params: {
            query: {
              From: filter.from,
              To: filter.to,
              StaffUserId: filter.staffUserId,
              SiteId: filter.siteId,
              RoomId: filter.roomId,
              SpecialtyId: filter.specialtyId,
              IncludeCancelled: filter.includeCancelled,
              WithFreeSlots: filter.withFreeSlots,
            },
          },
        }),
      ),
  });
}

/** A session with its slots, and who is booked into each (a read of those patients, logged by the API). */
export function sessionQuery(id: string) {
  return queryOptions({
    queryKey: clinicKeys.session(id),
    queryFn: () => unwrap(api.GET('/api/clinics/sessions/{id}', {params: {path: {id}}})),
  });
}

/** Every clinic pattern (ManageClinics). */
export const patternsQuery = queryOptions({
  queryKey: clinicKeys.patterns,
  queryFn: () => unwrap(api.GET('/api/clinics/patterns')),
});

/** After a change: the clinics, and the calendars and appointments that show their sessions and bookings. */
function useRefreshClinics() {
  const queryClient = useQueryClient();
  return async () => {
    await Promise.all([
      queryClient.invalidateQueries({queryKey: clinicKeys.all}),
      queryClient.invalidateQueries({queryKey: calendarKeys.all}),
      queryClient.invalidateQueries({queryKey: appointmentKeys.all}),
    ]);
  };
}

/**
 * Adds a session (no `id`) or changes one (with `id` and the `expectedLastUpdated` read). A clash on the clinician's
 * calendar or in the room is a 409 naming it; with bookings, only the room and specialty can change (409 otherwise).
 */
export function useSaveSession() {
  const onSuccess = useRefreshClinics();
  return useMutation({
    mutationFn: ({id, expectedLastUpdated, ...body}: ClinicSessionInput & {id?: string; expectedLastUpdated?: string}) =>
      id
        ? unwrap(api.PUT('/api/clinics/sessions/{id}', {params: {path: {id}}, body: {...body, expectedLastUpdated}}))
        : unwrap(api.POST('/api/clinics/sessions', {body})),
    onSuccess,
  });
}

/** Cancels a session and its booked appointments, with a reason; it returns the patients to tell. */
export function useCancelSession() {
  const onSuccess = useRefreshClinics();
  return useMutation({
    /** `tellPatients` (sent only when offered: patient messages are on) texts or emails each patient as they chose. */
    mutationFn: ({id, reason, expectedLastUpdated, tellPatients}: {id: string; reason: string; expectedLastUpdated: string; tellPatients?: boolean}) =>
      unwrap(
        api.POST('/api/clinics/sessions/{id}/cancel', {
          params: {path: {id}},
          body: tellPatients === undefined ? {reason, expectedLastUpdated} : {reason, expectedLastUpdated, tellPatients},
        }),
      ),
    onSuccess,
  });
}

/** Adds a pattern (no `id`) or changes one (with `id` and `expectedLastUpdated`); sessions already made stay. */
export function useSavePattern() {
  const onSuccess = useRefreshClinics();
  return useMutation({
    mutationFn: ({id, ...body}: ClinicPatternInput & {id?: string}) =>
      id ? unwrap(api.PUT('/api/clinics/patterns/{id}', {params: {path: {id}}, body})) : unwrap(api.POST('/api/clinics/patterns', {body})),
    onSuccess,
  });
}

/** Create sessions: a pattern's sessions for the next 1 to 13 weeks; it says what it made and skipped. */
export function useCreateSessions() {
  const onSuccess = useRefreshClinics();
  return useMutation({
    mutationFn: ({id, weeks}: {id: string; weeks: number}) =>
      unwrap(api.POST('/api/clinics/patterns/{id}/create-sessions', {params: {path: {id}}, body: {weeks}})),
    onSuccess,
  });
}

/** How full a session is, in words: "4 of 9 free", "Full", or "Cancelled". */
export function slotsWords(session: Pick<ClinicSession, 'status' | 'freeSlotCount' | 'slotCount'>): string {
  if (session.status === 'Cancelled') return 'Cancelled';
  const free = session.freeSlotCount ?? 0;
  return free === 0 ? 'Full' : `${free} of ${session.slotCount ?? 0} free`;
}
