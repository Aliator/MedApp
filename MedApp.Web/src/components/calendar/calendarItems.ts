import type {TokenColor} from '@astryxdesign/core/Token';
import {appointmentStatusLabel, type Appointment} from '../../api/appointments';
import type {Calendar, CalendarBlock} from '../../api/calendar';
import {formatTimeRange} from '../../utils/calendarSlots';
import {ukDay} from '../../utils/dates';
import {APPOINTMENT_STATUS_COLOURS, activityColour} from './CalendarTokens';

/**
 * Something shown on a `CalendarGrid`: an appointment or blocked time, placed by its time. The grid stays generic, so
 * Home, the Calendar page and anything else can show their own things on it; `appointmentItem` and `blockItem` make
 * items from the API's calendar.
 */
export interface CalendarGridItem {
  /** Unique on the grid. */
  key: string;
  /** Appointments stand out; blocks are quieter. */
  kind: 'appointment' | 'block';
  /** The UK day it's on (`yyyy-mm-dd`). */
  date: string;
  startUtc: string;
  endUtc: string;
  colour: TokenColor;
  /** The first line: a patient's name, an activity. */
  label: string;
  /** The second line: what it's for (nothing for an appointment that isn't for a procedure), a block's note. */
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

/** Blocked time on a calendar, in its activity's colour; movable when it can be changed. */
export function blockItem(block: CalendarBlock): CalendarGridItem {
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
    isMovable: block.canEdit === true,
    movesWithinDay: block.isFromTemplate === true,
    isFlagged: block.overlapsAppointment === true,
  };
}

/** Everything on a calendar as grid items: its blocks first, so its appointments draw over them. */
export function calendarItems(calendar: Pick<Calendar, 'blocks' | 'appointments'> | undefined): CalendarGridItem[] {
  if (!calendar) return [];
  return [
    ...(calendar.blocks ?? []).map(blockItem),
    ...(calendar.appointments ?? []).filter((a) => a.startUtc && a.endUtc).map(appointmentItem),
  ];
}
