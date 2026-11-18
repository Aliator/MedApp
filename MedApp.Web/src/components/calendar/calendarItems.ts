import type {TokenColor} from '@astryxdesign/core/Token';
import {appointmentStatusLabel, type Appointment} from '../../api/appointments';
import {hasDeclined, type Calendar, type CalendarBlock, type Meeting} from '../../api/calendar';
import {formatTimeRange} from '../../utils/calendarSlots';
import {ukDay} from '../../utils/dates';
import {APPOINTMENT_STATUS_COLOURS, activityColour} from './CalendarTokens';

/**
 * Something shown on a `CalendarGrid`: an appointment, blocked time or a meeting, placed by its time. The grid stays
 * generic, so Home, the Calendar page and anything else can show their own things on it; `appointmentItem`,
 * `blockItem` and `meetingItem` make items from the API's calendar.
 */
export interface CalendarGridItem {
  /** Unique on the grid. */
  key: string;
  /** Appointments and meetings stand out (a meeting with its icon); blocks are quieter. */
  kind: 'appointment' | 'block' | 'meeting';
  /** The UK day it's on (`yyyy-mm-dd`). */
  date: string;
  startUtc: string;
  endUtc: string;
  colour: TokenColor;
  /** The first line: a patient's name, an activity, a meeting's title. */
  label: string;
  /**
   * The second line: what it's for (nothing for an appointment that isn't for a procedure), a block's note, how many
   * are going to a meeting.
   */
  sublabel?: string | null;
  /** Everything about it, in words: its accessible name, and its tooltip. */
  description: string;
  /** Can be dragged to another time (its caller handles `onItemMove`). */
  isMovable?: boolean;
  /** Can only be dragged within its own day (a template block, changed for its day only). */
  movesWithinDay?: boolean;
  /** Flagged with a warning: a block a template change put over an appointment. */
  isFlagged?: boolean;
  /** Shown, but not opened (e.g. blocks on Home's day calendar). */
  isReadOnly?: boolean;
  /**
   * Shown faded, as no longer busy time: a meeting the calendar's owner declined. It's drawn untinted, behind what's
   * busy, with a dashed edge; its words keep their full contrast.
   */
  isFaded?: boolean;
}

/** What an appointment is for, in words: "Knee review: Assessment", "Knee review", or "Appointment". */
export function appointmentPurpose(appointment: Pick<Appointment, 'procedureTitle' | 'stageName'>): string {
  if (!appointment.procedureTitle) return 'Appointment';
  return appointment.stageName ? `${appointment.procedureTitle}: ${appointment.stageName}` : appointment.procedureTitle;
}

/** An appointment's key on a grid. */
export const appointmentKey = (id: string | undefined) => `appointment-${id}`;

/** A block's key on a grid: a stored block's ID, or a template block's on its day. */
export const blockKey = (block: CalendarBlock) => `block-${block.blockId ?? `${block.templateBlockId}-${block.date}`}`;

/** A meeting's key on a grid. */
export const meetingKey = (id: string | undefined) => `meeting-${id}`;

/**
 * A meeting's colour on a calendar: the seeded "Meeting" activity type's (teal), so a meeting looks like the meeting
 * time people block for themselves. It also has its icon (`AppIcon.Meeting`), so it never relies on colour alone.
 */
export const MEETING_COLOUR: TokenColor = 'teal';

/** How many are going to a meeting, leaving out those who declined: "1 attending", "4 attending". */
export const attendingCount = (meeting: Pick<Meeting, 'attendees'>) =>
  `${(meeting.attendees ?? []).filter((a) => a.reply !== 'Declined').length} attending`;

/**
 * A meeting on a calendar: its title, time and how many are going; movable by those who can change it (admins). On
 * the calendar of someone who declined it (`calendarOwnerId`) it's faded and says "Declined", as it's no longer busy
 * time for them; on everyone else's it shows as usual.
 */
export function meetingItem(meeting: Meeting, calendarOwnerId?: string | null): CalendarGridItem {
  const start = meeting.startUtc ?? '';
  const end = meeting.endUtc ?? '';
  const title = meeting.title ?? '';
  const isDeclined = hasDeclined(meeting, calendarOwnerId);
  const said = isDeclined ? 'Declined' : attendingCount(meeting);
  return {
    key: meetingKey(meeting.id),
    kind: 'meeting',
    date: meeting.date ?? ukDay(start),
    startUtc: start,
    endUtc: end,
    colour: MEETING_COLOUR,
    label: title,
    sublabel: said,
    description: `${title}, meeting, ${formatTimeRange(start, end)}, ${said}`,
    isMovable: meeting.canEdit === true,
    isFaded: isDeclined,
  };
}

/** An appointment on a calendar, coloured by its status; movable while it can be rescheduled. */
export function appointmentItem(appointment: Appointment): CalendarGridItem {
  const start = appointment.startUtc ?? '';
  const end = appointment.endUtc ?? '';
  const status = appointment.status ?? '';
  const patient = appointment.patientName ?? '';
  // One that isn't for a procedure has nothing more to say on its card than whose it is.
  const purpose = appointment.procedureTitle ? appointmentPurpose(appointment) : null;
  return {
    key: appointmentKey(appointment.id),
    kind: 'appointment',
    date: ukDay(start),
    startUtc: start,
    endUtc: end,
    colour: APPOINTMENT_STATUS_COLOURS[status] ?? 'blue',
    label: patient,
    sublabel: purpose,
    description: `${patient}, ${purpose ?? 'appointment'}, ${formatTimeRange(start, end)}, ${appointmentStatusLabel(status)}`,
    isMovable: appointment.canReschedule === true,
  };
}

/**
 * Blocked time on a calendar, in its activity's colour. Movable only when asked for (`isMovable`: the Calendar page's
 * Edit blocks mode) and it can be changed (its calendar's owner or an admin, not on a past day: the API's `canEdit`).
 */
export function blockItem(block: CalendarBlock, {isMovable = false}: {isMovable?: boolean} = {}): CalendarGridItem {
  const start = block.startUtc ?? '';
  const end = block.endUtc ?? '';
  const parts = [
    block.activityName ?? '',
    block.note,
    formatTimeRange(start, end),
    block.isFromTemplate ? 'from the day template' : null,
    block.overlapsAppointment ? 'overlaps an appointment' : null,
  ];
  return {
    key: blockKey(block),
    kind: 'block',
    date: block.date ?? '',
    startUtc: start,
    endUtc: end,
    colour: activityColour(block.colour),
    label: block.activityName ?? '',
    sublabel: block.note,
    description: parts.filter(Boolean).join(', '),
    isMovable: isMovable && block.canEdit === true,
    // The API changes a template block for its own day only, so it can't be dragged to another.
    movesWithinDay: block.isFromTemplate === true,
    isFlagged: block.overlapsAppointment === true,
  };
}

/**
 * Everything on a calendar as grid items: its blocks first, then its meetings, so its appointments draw over them.
 * Appointments and meetings are movable by their own flags; blocks only with `movableBlocks` (the Calendar page's Edit
 * blocks mode), and never on a day the calendar says is past. A meeting the calendar's owner (`staffUserId`) declined
 * is faded (`meetingItem`).
 */
export function calendarItems(
  calendar: Pick<Calendar, 'blocks' | 'appointments' | 'meetings' | 'days' | 'staffUserId'> | undefined,
  {movableBlocks = false}: {movableBlocks?: boolean} = {},
): CalendarGridItem[] {
  if (!calendar) return [];
  const pastDays = new Set((calendar.days ?? []).filter((d) => d.isPast).map((d) => d.date));
  return [
    ...(calendar.blocks ?? []).map((b) => blockItem(b, {isMovable: movableBlocks && !pastDays.has(b.date)})),
    ...(calendar.meetings ?? []).filter((m) => m.startUtc && m.endUtc).map((m) => meetingItem(m, calendar.staffUserId)),
    ...(calendar.appointments ?? []).filter((a) => a.startUtc && a.endUtc).map(appointmentItem),
  ];
}
