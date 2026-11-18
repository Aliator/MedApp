import type {Appointment} from '../api/appointments';
import type {Calendar, CalendarBlock, CalendarDay, Meeting} from '../api/calendar';
import {addDays, isWeekday} from '../utils/calendarSlots';

/** Synthetic calendars and appointments for tests, shaped like the API's responses. */

export const STAFF_ME = {userId: '00000000-0000-0000-0000-0000000000a1', username: 'test-user'};
export const STAFF_OTHER = {userId: '00000000-0000-0000-0000-0000000000b2', username: 'b.staff'};

/**
 * The patient the test appointments are for (`GET /api/patients/patient-1`), as the appointment dialogs show them.
 * Synthetic, with an NHS number from the 999 range, which is never issued to real people.
 */
export const TEST_PATIENT = {
  id: 'patient-1',
  firstName: 'Test',
  lastName: 'Patient',
  dateOfBirth: '1980-01-01',
  email: 'test.patient@example.org',
  nhsNumber: '9990000018',
  isRestricted: false,
  isDisputed: false,
  isAnonymised: false,
};

/** The UK's offset from UTC on a date, in hours (0 in winter, 1 in summer time). */
function ukOffsetHours(date: string): number {
  const [year, month, day] = date.split('-').map(Number);
  const noonUtc = new Date(Date.UTC(year, month - 1, day, 12));
  const ukHour = Number(new Intl.DateTimeFormat('en-GB', {hour: '2-digit', hourCycle: 'h23', timeZone: 'Europe/London'}).format(noonUtc));
  return ukHour - 12;
}

/** A UK clock time on a date ("09:15") as the API sends it: UTC, with a Z. */
export function ukTime(date: string, time: string): string {
  const [year, month, day] = date.split('-').map(Number);
  const [hour, minute] = time.split(':').map(Number);
  return new Date(Date.UTC(year, month - 1, day, hour - ukOffsetHours(date), minute)).toISOString().replace('.000Z', 'Z');
}

/** A calendar day, working Monday to Friday, 08:00 to 18:00 UK time; past and today relative to `today`. */
export function calendarDay(date: string, today: string, overrides: Partial<CalendarDay> = {}): CalendarDay {
  return {
    date,
    isWorkingDay: isWeekday(date),
    isPast: date < today,
    isToday: date === today,
    workStartUtc: ukTime(date, '08:00'),
    workEndUtc: ukTime(date, '18:00'),
    dayTemplateId: null,
    dayTemplateName: null,
    isDayTemplateOverridden: false,
    ...overrides,
  };
}

/** A calendar from `from` for `dayCount` days (a week by default). */
export function testCalendar({
  from,
  dayCount = 7,
  today,
  staff = STAFF_ME,
  canEdit = true,
  blocks = [],
  appointments = [],
  ...rest
}: {
  from: string;
  dayCount?: number;
  today: string;
  staff?: {userId: string; username: string};
  canEdit?: boolean;
  blocks?: CalendarBlock[];
  appointments?: Appointment[];
} & Omit<Partial<Calendar>, 'days'>): Calendar {
  return {
    staffUserId: staff.userId,
    staffUsername: staff.username,
    from,
    to: addDays(from, dayCount - 1),
    workingDayStart: '08:00',
    workingDayEnd: '18:00',
    slotMinutes: 15,
    canEdit,
    defaultWeekTemplateId: null,
    defaultWeekTemplateName: null,
    days: Array.from({length: dayCount}, (_, i) => calendarDay(addDays(from, i), today)),
    blocks,
    appointments,
    ...rest,
  };
}

/** A stored block (or, with `isFromTemplate`, a template block) on a date, between two UK times. */
export function testBlock(date: string, start: string, end: string, overrides: Partial<CalendarBlock> = {}): CalendarBlock {
  return {
    blockId: overrides.isFromTemplate ? null : `block-${date}-${start}`,
    templateBlockId: overrides.isFromTemplate ? `template-block-${start}` : null,
    isFromTemplate: false,
    staffUserId: STAFF_ME.userId,
    date,
    startUtc: ukTime(date, start),
    endUtc: ukTime(date, end),
    activityTypeId: 'activity-lunch',
    activityName: 'Lunch',
    colour: 'green',
    note: null,
    canEdit: true,
    overlapsAppointment: false,
    ...overrides,
  };
}

/** A Booked appointment on a date, between two UK times, with everything allowed but recording its outcome. */
export function testAppointment(overrides: Partial<Appointment> & {date?: string; start?: string; end?: string} = {}): Appointment {
  const {date = '2026-11-23', start = '09:00', end = '09:30', ...rest} = overrides;
  return {
    id: `appointment-${date}-${start}`,
    patientId: 'patient-1',
    patientName: 'Test Patient',
    procedureId: 'procedure-1',
    procedureTitle: 'Knee review',
    procedureDueDate: '2026-12-01',
    stageId: 'stage-1',
    stageName: 'Assessment',
    visitRequirement: 'Required',
    status: 'Booked',
    staffUserId: STAFF_ME.userId,
    staffUsername: STAFF_ME.username,
    startUtc: ukTime(date, start),
    endUtc: ukTime(date, end),
    durationMinutes: 30,
    notes: null,
    cancelReason: null,
    isAutomatic: false,
    createdAtUtc: '2026-11-10T10:00:00Z',
    createdByUsername: 'test-admin',
    isRecordedAfterEvent: false,
    bookedAtUtc: '2026-11-10T10:00:00Z',
    bookedByUsername: 'test-admin',
    outcomeAtUtc: null,
    outcomeByUsername: null,
    lastUpdated: '2026-11-10T10:00:00Z',
    canBook: false,
    canReschedule: true,
    canRecordOutcome: false,
    canCancel: true,
    canEditNotes: true,
    ...rest,
  };
}

/** A Requested appointment (Needs booking): no member of staff or time yet. */
export function testRequested(overrides: Partial<Appointment> = {}): Appointment {
  return testAppointment({
    id: 'requested-1',
    status: 'Requested',
    staffUserId: null,
    staffUsername: null,
    startUtc: null,
    endUtc: null,
    durationMinutes: null,
    bookedAtUtc: null,
    bookedByUsername: null,
    canBook: true,
    canReschedule: false,
    ...overrides,
  });
}

/** A meeting on a date, between two UK times, with the signed-in user and b.staff attending; admins can change it. */
export function testMeeting(overrides: Partial<Meeting> & {date?: string; start?: string; end?: string} = {}): Meeting {
  const {date = '2026-11-24', start = '09:00', end = '10:00', ...rest} = overrides;
  return {
    id: `meeting-${date}-${start}`,
    title: 'Team huddle',
    notes: null,
    date,
    startUtc: ukTime(date, start),
    endUtc: ukTime(date, end),
    organiserUserId: STAFF_ME.userId,
    organiserUsername: STAFF_ME.username,
    attendees: [
      {userId: STAFF_ME.userId, username: STAFF_ME.username},
      {userId: STAFF_OTHER.userId, username: STAFF_OTHER.username},
    ],
    canEdit: true,
    overlapsAppointment: false,
    createdAtUtc: '2026-11-10T10:00:00Z',
    lastUpdated: '2026-11-10T10:00:00Z',
    ...rest,
  };
}
