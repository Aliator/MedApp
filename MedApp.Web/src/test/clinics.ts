import type {ClinicPattern, ClinicSession, ClinicSessionDetail} from '../api/clinics';
import {addMinutes} from '../utils/calendarSlots';
import {STAFF_ME, ukTime} from './calendar';

/** Synthetic clinic sessions and patterns for tests, shaped like the API's responses. */

export const ROOM = {id: 'room-2', name: 'Room 2', siteId: 'site-main', siteName: 'Main site', isArchived: false, canBeDeleted: false};
export const TEAM = {id: 'team-derm', name: 'Synthetic dermatology', isArchived: false};

/** A Planned session on `date`, 09:00 to 12:00 UK time by default, in 20-minute slots, with `booked` slots taken. */
export function testSession({
  id = 'session-1',
  date = '2026-11-24',
  starts = '09:00',
  ends = '12:00',
  slotMinutes = 20,
  booked = 0,
  ...overrides
}: Partial<ClinicSession> & {starts?: string; ends?: string; booked?: number} = {}): ClinicSession {
  const startUtc = ukTime(date, starts);
  const endUtc = ukTime(date, ends);
  const slotCount = Math.round((new Date(endUtc).getTime() - new Date(startUtc).getTime()) / 60_000 / slotMinutes);
  const free = Array.from({length: slotCount - booked}, (_, i) => addMinutes(startUtc, slotMinutes * (i + booked)).replace('.000Z', 'Z'));
  return {
    id,
    staffUserId: STAFF_ME.userId,
    staffUsername: STAFF_ME.username,
    roomId: ROOM.id,
    roomName: ROOM.name,
    siteId: ROOM.siteId,
    siteName: ROOM.siteName,
    teamId: TEAM.id,
    teamName: TEAM.name,
    patternId: null,
    date,
    startUtc,
    endUtc,
    slotMinutes,
    slotCount,
    bookedCount: booked,
    freeSlotCount: free.length,
    freeSlotStartsUtc: free,
    status: 'Planned',
    cancelReason: null,
    cancelledAtUtc: null,
    lastUpdated: '2026-11-20T10:00:00Z',
    canChange: true,
    ...overrides,
  };
}

/** A session with its slots: the first `booked` taken by synthetic patients. */
export function testSessionDetail(session: ClinicSession = testSession({booked: 1})): ClinicSessionDetail {
  return {
    session,
    slots: Array.from({length: session.slotCount ?? 0}, (_, i) => {
      const start = addMinutes(session.startUtc ?? '', (session.slotMinutes ?? 0) * i);
      const isBooked = i < (session.bookedCount ?? 0);
      return {
        startUtc: start,
        endUtc: addMinutes(start, session.slotMinutes ?? 0),
        isPast: false,
        appointmentId: isBooked ? `appointment-${i}` : null,
        patientId: isBooked ? `patient-${i}` : null,
        patientName: isBooked ? `Test Patient${i}` : null,
        appointmentStatus: isBooked ? 'Booked' : null,
      };
    }),
  };
}

/** A pattern: Tuesdays 09:00 to 12:00, 20-minute slots, every week from 24 November. */
export function testPattern(overrides: Partial<ClinicPattern> = {}): ClinicPattern {
  return {
    id: 'pattern-1',
    staffUserId: STAFF_ME.userId,
    staffUsername: STAFF_ME.username,
    roomId: ROOM.id,
    roomName: ROOM.name,
    siteId: ROOM.siteId,
    siteName: ROOM.siteName,
    teamId: TEAM.id,
    teamName: TEAM.name,
    dayOfWeek: 'Tuesday',
    starts: '09:00:00',
    ends: '12:00:00',
    slotMinutes: 20,
    everyWeeks: 1,
    fromDate: '2026-11-24',
    untilDate: null,
    hasEnded: false,
    lastUpdated: '2026-11-20T10:00:00Z',
    ...overrides,
  };
}
