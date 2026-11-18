import {infiniteQueryOptions, queryOptions} from '@tanstack/react-query';
import {formatAppointmentTime, formatShortDate, ukDay} from '../utils/dates';
import {NO_PROCEDURE} from './attachments';
import {api, unwrap, type Schemas} from './client';

export type TimelineEntry = Schemas['PatientTimelineEntryResponse'];
export type TimelineChange = Schemas['PatientTimelineChangeResponse'];
export type TimelineOverview = Schemas['PatientTimelineOverviewResponse'];
export type TimelineUpcoming = Schemas['PatientTimelineUpcomingResponse'];
export type TimelineProcedure = Schemas['PatientTimelineProcedureResponse'];

/** The most procedures a timeline can be filtered to at once (the API's `GetPatientTimelineValidator.MaxProcedureIds`). */
export const MAX_TIMELINE_PROCEDURES = 50;

/** Entries per page of a timeline (each "Load more" adds as many again). */
export const TIMELINE_PAGE_SIZE = 20;

/** What a timeline can be filtered to, as the API names it. Only admins see Deleted. */
export const TIMELINE_KINDS = ['Created', 'Stages', 'Appointments', 'Notes', 'Assignments', 'Blocks', 'Status', 'DueDates', 'Details', 'Files', 'Deleted'] as const;
export type TimelineKind = (typeof TIMELINE_KINDS)[number];

const KIND_LABELS: Record<TimelineKind, string> = {
  Created: 'Created',
  Stages: 'Stages',
  Appointments: 'Appointments',
  Notes: 'Notes',
  Assignments: 'Assignments',
  Blocks: 'Blocks',
  Status: 'Status',
  DueDates: 'Due dates',
  Details: 'Other edits',
  Files: 'Files',
  Deleted: 'Deleted',
};

/** A kind in words, for its filter chip: "DueDates" → "Due dates". */
export const timelineKindLabel = (kind: TimelineKind) => KIND_LABELS[kind];

/** What a timeline is narrowed to: any of these kinds, and any of these procedures (none of either means all). */
export interface TimelineFilters {
  kinds: TimelineKind[];
  procedureIds: string[];
}

export const NO_TIMELINE_FILTERS: TimelineFilters = {kinds: [], procedureIds: []};

export const hasTimelineFilters = (filters: TimelineFilters) => filters.kinds.length > 0 || filters.procedureIds.length > 0;

/**
 * A patient's timeline (`GET api/patients/{id}/timeline`), newest first, a page at a time: each page's `nextCursor`
 * fetches the one after it. Keyed under ['procedures'], so a change to any procedure refreshes it. The filters are
 * sorted, so the same choice made in another order is the same query.
 */
export function patientTimelineQuery(patientId: string, filters: TimelineFilters = NO_TIMELINE_FILTERS) {
  const kinds = [...filters.kinds].sort();
  const procedureIds = [...filters.procedureIds].sort();
  return infiniteQueryOptions({
    queryKey: ['procedures', 'timeline', patientId, {kinds, procedureIds}],
    queryFn: ({pageParam}) =>
      unwrap(
        api.GET('/api/patients/{id}/timeline', {
          params: {
            path: {id: patientId},
            query: {
              PageSize: TIMELINE_PAGE_SIZE,
              ...(pageParam ? {Before: pageParam} : {}),
              ...(kinds.length > 0 ? {Kinds: kinds} : {}),
              ...(procedureIds.length > 0 ? {ProcedureIds: procedureIds} : {}),
            },
          },
        }),
      ),
    initialPageParam: null as string | null,
    getNextPageParam: (page) => page.nextCursor ?? undefined,
  });
}

/** What's due for a patient, and their procedures to filter the timeline by (`GET api/patients/{id}/timeline/overview`). */
export function patientTimelineOverviewQuery(patientId: string) {
  return queryOptions({
    queryKey: ['procedures', 'timeline', patientId, 'overview'],
    queryFn: () => unwrap(api.GET('/api/patients/{id}/timeline/overview', {params: {path: {id: patientId}}})),
  });
}

/** A day of a timeline: its UK date (YYYY-MM-DD) and its entries, newest first. */
export interface TimelineDay {
  day: string;
  entries: TimelineEntry[];
}

/** Entries (newest first) grouped by the UK day they happened on, in their order. */
export function groupByDay(entries: readonly TimelineEntry[]): TimelineDay[] {
  const days: TimelineDay[] = [];
  for (const entry of entries) {
    const day = ukDay(entry.atUtc ?? '');
    const last = days[days.length - 1];
    if (last?.day === day) last.entries.push(entry);
    else days.push({day, entries: [entry]});
  }
  return days;
}

/** A status or priority in words, lower case: "InReview" → "in review". */
const words = (value: string | null | undefined) => (value ?? '').replace(/(?<=[a-z])(?=[A-Z])/g, ' ').toLowerCase();
const capitalised = (text: string) => text.charAt(0).toUpperCase() + text.slice(1);
const date = (value: string | null | undefined) => (value ? formatShortDate(value) : 'none');
const quoted = (value: string | null | undefined) => (value?.trim() ? `"${value.trim()}"` : 'nothing');
const hasValues = (change: TimelineChange) => change.from != null || change.to != null;

const FIELD_NAMES: Record<string, string> = {
  title: 'title',
  notes: 'notes',
  dueDate: 'due date',
  priority: 'priority',
  stageNotes: 'notes',
  appointmentNotes: 'notes',
  note: 'note',
};

/**
 * One edited field in words. Values come only where the API sends them (due dates and priorities to everyone, free
 * text only to admins); otherwise it says what changed.
 */
function describeChange(change: TimelineChange): string {
  const field = change.field ?? '';
  const name = capitalised(FIELD_NAMES[field] ?? field);
  if (!hasValues(change)) return `${name} changed`;
  switch (field) {
    case 'dueDate':
      return `Due date ${date(change.from)} → ${date(change.to)}`;
    case 'priority':
      return `Priority ${words(change.from)} → ${words(change.to)}`;
    case 'title':
      return `Title ${quoted(change.from)} → ${quoted(change.to)}`;
    default:
      return change.to?.trim() ? `${name}: ${change.to.trim()}` : `${name} cleared`;
  }
}

/** What an entry says: a headline, and a line of detail (empty when there's nothing to add). */
export interface TimelineWording {
  title: string;
  detail: string;
  /** A procedure note's text, as Markdown (admins only), shown formatted under the detail. */
  noteText?: string;
}

/** A note's text from its entry's changes (only admins get it), or undefined. */
const noteTextOf = (changes: TimelineChange[]) => changes.find((c) => c.to?.trim())?.to?.trim();

/**
 * An entry in words, e.g. {title: "Blocked", detail: "Was in progress. Awaiting results"}. It says only what the API
 * sent: staff never get notes, titles or reasons from the log, so for them those lines just say what changed.
 */
export function describeTimelineEntry(entry: TimelineEntry): TimelineWording {
  const stage = entry.stageName ?? 'a stage';
  const changes = entry.changes ?? [];
  switch (entry.action) {
    case 'Created':
      return {
        title: 'Procedure created',
        detail: [
          entry.templateName ? `From ${entry.templateName}` : null,
          entry.stageCount != null ? `${entry.stageCount} ${entry.stageCount === 1 ? 'stage' : 'stages'}` : null,
          entry.dueDate ? `due ${date(entry.dueDate)}` : null,
          entry.priority ? `${words(entry.priority)} priority` : null,
        ]
          .filter(Boolean)
          .join(', '),
      };
    case 'StageStarted':
      return {title: `Started ${stage}`, detail: ''};
    case 'StageFinished':
      return entry.finishedWithoutVisit
        ? {title: `Finished ${stage} without a visit`, detail: withoutVisitDetail(entry)}
        : {title: `Finished ${stage}`, detail: ''};
    case 'StageUnlocked':
      return {title: `Unlocked ${stage}`, detail: ''};
    case 'StageNotesUpdated':
      return {title: `Saved notes on ${stage}`, detail: changes.filter(hasValues).map(describeChange).join('; ')};
    // Staff are told that a note was added or edited, and by whom; only admins get its text.
    // The text is Markdown, so it's shown formatted (`noteText`), never as its raw marks.
    case 'NoteAdded':
      return {title: 'Note added', detail: '', noteText: noteTextOf(changes)};
    case 'NoteEdited': {
      const noteText = noteTextOf(changes);
      return {title: 'Note edited', detail: noteText ? 'It now reads:' : '', noteText};
    }
    case 'Assigned':
      return {title: `Assigned ${entry.assigneeUsername ?? 'someone'}`, detail: ''};
    case 'Unassigned':
      return {title: `Unassigned ${entry.assigneeUsername ?? 'someone'}`, detail: ''};
    case 'StatusChanged':
      return describeStatusChange(entry);
    case 'DetailsUpdated':
      return {
        title: entry.kind === 'DueDates' ? 'Due date changed' : 'Details edited',
        detail: changes.map(describeChange).join('; '),
      };
    case 'Deleted':
      return {title: 'Procedure deleted', detail: entry.fromStatus ? `Was ${words(entry.fromStatus)}` : ''};
    case 'AttachmentAdded':
      return {title: `Attached ${entry.fileName?.trim() || 'a file'}`, detail: `To ${attachedToWords(entry)}`};
    case 'AttachmentDeleted':
      return {title: `Deleted ${entry.fileName?.trim() || 'a file'}`, detail: `From ${attachedToWords(entry)}`};
    default:
      return isAppointmentEntry(entry) ? describeAppointmentEntry(entry) : {title: entry.action ?? 'Changed', detail: ''};
  }
}

/** A stage finished without a visit: what the stage asked for, and (to admins) why. */
function withoutVisitDetail(entry: TimelineEntry): string {
  const requirement =
    entry.visitRequirement === 'Required' ? 'A visit was required' : entry.visitRequirement === 'Suggested' ? 'A visit was suggested' : null;
  return [requirement, entry.reason?.trim()].filter(Boolean).join('. ');
}

/** Whether an entry is from an appointment's history (its action is "Appointment" and what happened). */
const isAppointmentEntry = (entry: TimelineEntry) => entry.kind === 'Appointments' || (entry.action ?? '').startsWith('Appointment');

/** An appointment's time and member of staff: "Mon 23 Nov 2026, 10:00 to 10:30 with j.smith". */
function appointmentSlot(startUtc: string | null | undefined, endUtc: string | null | undefined, staff: string | null | undefined): string {
  if (!startUtc) return staff ? `With ${staff}` : '';
  return `${formatAppointmentTime(startUtc, endUtc)}${staff ? ` with ${staff}` : ''}`;
}

const sentences = (...parts: (string | null | undefined)[]) =>
  parts
    .map((part) => part?.trim())
    .filter(Boolean)
    .join('. ');

/** An outcome that corrects an earlier one says what it was. */
function correction(entry: TimelineEntry): string | null {
  if (entry.fromStatus === 'Attended') return 'Was marked Attended';
  if (entry.fromStatus === 'DidNotAttend') return 'Was marked Did not attend';
  return null;
}

/**
 * An appointment history entry in words. Staff get the structured values only (time, member of staff, stage);
 * cancellation reasons and notes come only to admins, so for staff a notes entry just says the notes changed.
 */
function describeAppointmentEntry(entry: TimelineEntry): TimelineWording {
  const about = entry.stageName ? ` for ${entry.stageName}` : '';
  const slot = appointmentSlot(entry.appointmentStartUtc, entry.appointmentEndUtc, entry.appointmentStaffUsername);
  switch (entry.action) {
    case 'AppointmentRequested':
      return {
        title: `Appointment requested${about}`,
        detail: entry.isAutomatic ? 'Requested automatically, as the stage involves a visit. Requires appointment' : 'Requires appointment',
      };
    case 'AppointmentBooked':
      return {title: `Appointment booked${about}`, detail: slot};
    case 'AppointmentRescheduled': {
      const before = appointmentSlot(entry.previousStartUtc, null, entry.previousStaffUsername);
      return {title: `Appointment rescheduled${about}`, detail: before ? `From ${before}, to ${slot}` : `To ${slot}`};
    }
    case 'AppointmentAttended':
      return {title: `Marked Attended${about}`, detail: sentences(slot, correction(entry))};
    case 'AppointmentDidNotAttend':
      return {title: `Marked Did not attend${about}`, detail: sentences(slot, correction(entry))};
    case 'AppointmentRecordedAfterEvent':
      // A walk-in, or one booked outside MedApp, recorded with its outcome; or a visit that was waiting in Requires Appointment.
      return {
        title: `Appointment recorded after the event${about}: ${entry.toStatus === 'DidNotAttend' ? 'did not attend' : 'attended'}`,
        detail: sentences(slot, entry.fromStatus === 'Requested' ? 'Was in Requires Appointment' : null),
      };
    case 'AppointmentCancelled':
      return {title: `Appointment cancelled${about}`, detail: sentences(slot, entry.isAutomatic ? 'Cancelled automatically' : null, entry.reason)};
    case 'AppointmentNotesUpdated': {
      const changes = (entry.changes ?? []).filter(hasValues).map(describeChange);
      return {title: `Appointment notes changed${about}`, detail: changes.join('; ')};
    }
    default:
      return {title: `Appointment changed${about}`, detail: slot};
  }
}

/**
 * Whether an entry is about the patient's own file, not one of their procedures: it has no procedure to link to
 * (its `procedureId` is all zeros and its title empty).
 */
export const isPatientEntry = (entry: TimelineEntry) => entry.attachedTo === 'Patient' || entry.procedureId === NO_PROCEDURE;

/** What a file was attached to, in words: "the patient", "the procedure", "the Consultation stage". */
function attachedToWords(entry: TimelineEntry): string {
  if (isPatientEntry(entry)) return 'the patient';
  if (entry.attachedTo === 'Stage') return entry.stageName ? `the ${entry.stageName} stage` : 'a stage';
  return 'the procedure';
}

const OPEN_STATUSES = new Set(['Unassigned', 'Assigned', 'InProgress', 'InReview']);

function describeStatusChange(entry: TimelineEntry): TimelineWording {
  const reason = entry.reason?.trim();
  if (entry.toStatus === 'Blocked')
    return {title: 'Blocked', detail: [`Was ${words(entry.fromStatus)}`, reason].filter(Boolean).join('. ')};
  // Unblocking puts it back where it was; cancelling a blocked procedure is a status change like any other.
  if (entry.fromStatus === 'Blocked' && OPEN_STATUSES.has(entry.toStatus ?? ''))
    return {title: 'Unblocked', detail: `Back to ${words(entry.toStatus)}`};
  const was = `Was ${words(entry.fromStatus)}${entry.isAutomatic ? ', changed automatically' : ''}`;
  return {title: `Status changed to ${words(entry.toStatus)}`, detail: [was, reason].filter(Boolean).join('. ')};
}
