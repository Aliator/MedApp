import {infiniteQueryOptions, queryOptions} from '@tanstack/react-query';
import {formatAppointmentTime, formatDateTime, formatShortDate, ukDay} from '../utils/dates';
import {statusWords} from '../utils/statusWords';
import {dnaFollowUpLabel} from './appointments';
import {NO_PATHWAY} from './attachments';
import {dispatchMethodWords, letterKindWords, recipientKindWords} from './letters';
import {pauseReasonWords, referralFieldWords, rejectionReasonWords, stopReasonWords} from './referrals';
import {skipReasonLabel} from './stageSkips';
import {resultActionLabel, resultStatusLabel} from './results';
import {consentFormLabel, signedByLabel} from './consent';
import {specimenStatusLabel} from './specimens';
import {api, unwrap, type Schemas} from './client';

export type TimelineEntry = Schemas['PatientTimelineEntryResponse'];
export type TimelineChange = Schemas['PatientTimelineChangeResponse'];
export type TimelineOverview = Schemas['PatientTimelineOverviewResponse'];
export type TimelineUpcoming = Schemas['PatientTimelineUpcomingResponse'];
export type TimelinePathway = Schemas['PatientTimelinePathwayResponse'];
/** A referral awaiting triage, beside the open pathways above the timeline. */
export type TimelineReferral = Schemas['PatientTimelineReferralResponse'];
/** Another team's pathway, as the team walls let it show: its clinic, in progress or completed, and since when. */
export type OtherTeamPathway = Schemas['OtherTeamPathwaySummaryResponse'];

/** Another team's pathway in words: "A pathway in Gynaecology, in progress since 3 Nov 2026" or "..., completed 17 Nov 2026". */
export const otherTeamPathwayLine = (pathway: OtherTeamPathway) =>
  `A pathway in ${pathway.teamName ?? 'another clinic'}, ${pathway.state === 'Completed' ? 'completed' : 'in progress since'} ${formatShortDate(pathway.since ?? '')}`;

/** The most pathways a timeline can be filtered to at once (the API's `GetPatientTimelineValidator.MaxPathwayIds`). */
export const MAX_TIMELINE_PATHWAYS = 50;

/** Entries per page of a timeline (each "Load more" adds as many again). */
export const TIMELINE_PAGE_SIZE = 20;

/** What a timeline can be filtered to, as the API names it. Only admins see Deleted. */
export const TIMELINE_KINDS = ['Created', 'Stages', 'Appointments', 'Referrals', 'WaitingTime', 'Specimens', 'Results', 'Consent', 'Letters', 'Notes', 'Assignments', 'Blocks', 'Status', 'DueDates', 'Details', 'Files', 'Mdt', 'Deleted'] as const;
export type TimelineKind = (typeof TIMELINE_KINDS)[number];

const KIND_LABELS: Record<TimelineKind, string> = {
  Created: 'Created',
  Stages: 'Stages',
  Appointments: 'Appointments',
  Referrals: 'Referrals',
  WaitingTime: 'Waiting time',
  Specimens: 'Samples',
  Results: 'Results',
  Consent: 'Consent',
  Letters: 'Letters',
  Notes: 'Notes',
  Assignments: 'Assignments',
  Blocks: 'On hold',
  Status: 'Status',
  DueDates: 'Target dates',
  Details: 'Other edits',
  Files: 'Files',
  Mdt: 'MDT',
  Deleted: 'Deleted',
};

/** A kind in words, for its filter chip: "DueDates" → "Target dates". */
export const timelineKindLabel = (kind: TimelineKind) => KIND_LABELS[kind];

/** What a timeline is narrowed to: any of these kinds, and any of these pathways (none of either means all). */
export interface TimelineFilters {
  kinds: TimelineKind[];
  pathwayIds: string[];
}

export const NO_TIMELINE_FILTERS: TimelineFilters = {kinds: [], pathwayIds: []};

export const hasTimelineFilters = (filters: TimelineFilters) => filters.kinds.length > 0 || filters.pathwayIds.length > 0;

/**
 * A patient's timeline (`GET api/patients/{id}/timeline`), newest first, a page at a time: each page's `nextCursor`
 * fetches the one after it. Keyed under ['pathways'], so a change to any pathway refreshes it. The filters are
 * sorted, so the same choice made in another order is the same query.
 */
export function patientTimelineQuery(patientId: string, filters: TimelineFilters = NO_TIMELINE_FILTERS) {
  const kinds = [...filters.kinds].sort();
  const pathwayIds = [...filters.pathwayIds].sort();
  return infiniteQueryOptions({
    queryKey: ['pathways', 'timeline', patientId, {kinds, pathwayIds}],
    queryFn: ({pageParam}) =>
      unwrap(
        api.GET('/api/patients/{id}/timeline', {
          params: {
            path: {id: patientId},
            query: {
              PageSize: TIMELINE_PAGE_SIZE,
              ...(pageParam ? {Before: pageParam} : {}),
              ...(kinds.length > 0 ? {Kinds: kinds} : {}),
              ...(pathwayIds.length > 0 ? {PathwayIds: pathwayIds} : {}),
            },
          },
        }),
      ),
    initialPageParam: null as string | null,
    getNextPageParam: (page) => page.nextCursor ?? undefined,
  });
}

/** What's due for a patient, and their pathways to filter the timeline by (`GET api/patients/{id}/timeline/overview`). */
export function patientTimelineOverviewQuery(patientId: string) {
  return queryOptions({
    queryKey: ['pathways', 'timeline', patientId, 'overview'],
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

/** A status or priority in words, lower case: "InProgress" → "in progress", "Blocked" → "on hold". */
const words = (value: string | null | undefined) => statusWords(value ?? '');
const capitalised = (text: string) => text.charAt(0).toUpperCase() + text.slice(1);
const date = (value: string | null | undefined) => (value ? formatShortDate(value) : 'none');
const quoted = (value: string | null | undefined) => (value?.trim() ? `"${value.trim()}"` : 'nothing');
const hasValues = (change: TimelineChange) => change.from != null || change.to != null;

const FIELD_NAMES: Record<string, string> = {
  title: 'title',
  notes: 'notes',
  dueDate: 'target date',
  priority: 'priority',
  stageNotes: 'notes',
  appointmentNotes: 'notes',
  note: 'note',
};

/**
 * One edited field in words. Values come only where the API sends them (target dates and priorities to everyone, free
 * text only to admins); otherwise it says what changed.
 */
function describeChange(change: TimelineChange): string {
  const field = change.field ?? '';
  const name = capitalised(FIELD_NAMES[field] ?? field);
  if (!hasValues(change)) return `${name} changed`;
  switch (field) {
    case 'dueDate':
      return `Target date ${date(change.from)} → ${date(change.to)}`;
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
  /** A pathway note's text, as Markdown (admins only), shown formatted under the detail. */
  noteText?: string;
}

/** A note's text from its entry's changes (only admins get it), or undefined. */
const noteTextOf = (changes: TimelineChange[]) => changes.find((c) => c.to?.trim())?.to?.trim();

/**
 * An entry in words, e.g. {title: "Put on hold", detail: "Was in progress. Awaiting results"}. It says only what the API
 * sent: staff never get notes, titles or reasons from the log, so for them those lines just say what changed.
 */
export function describeTimelineEntry(entry: TimelineEntry): TimelineWording {
  const stage = entry.stageName ?? 'a stage';
  const changes = entry.changes ?? [];
  switch (entry.action) {
    case 'Created':
      return {
        title: 'Pathway created',
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
    // Skipping: the reason is a fixed choice, for everyone; its detail (in `reason`) is free text, for admins only.
    case 'StageSkipped':
      return {title: `Skipped ${stage}`, detail: sentences(skipReasonLabel(entry.skipReason), entry.reason)};
    // The clinician's choice at a branch point, and the other branches' stages it skipped (names the template's author wrote).
    case 'BranchChosen': {
      const skipped = entry.skippedStageNames ?? [];
      return {
        title: entry.branch ? `Chose the ${entry.branch} branch at ${stage}` : `Chose a branch at ${stage}`,
        detail: skipped.length > 0 ? `Skipped: ${skipped.join(', ')}` : '',
      };
    }
    // How it had been skipped, for everyone; why it's reinstated (free text), for admins only.
    case 'StageReinstated':
      return {
        title: `Reinstated ${stage}`,
        detail: sentences(entry.skipReason ? `Had been skipped: ${skipReasonLabel(entry.skipReason)}` : null, entry.reason),
      };
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
    // Deleted by an admin (kept, but hidden): staff see who and when; admins also the reason and the text it had.
    case 'NoteDeleted': {
      const noteText = changes.find((c) => c.from?.trim())?.from?.trim();
      const reason = entry.reason?.trim();
      return {
        title: 'Note deleted',
        detail: [reason ? `Reason: ${reason}` : null, noteText ? 'It read:' : null].filter(Boolean).join(' '),
        noteText,
      };
    }
    case 'Assigned':
      return {title: `Assigned to ${entry.assigneeUsername ?? 'someone'}`, detail: ''};
    case 'Unassigned':
      return {title: entry.assigneeUsername ? `No longer assigned to ${entry.assigneeUsername}` : 'Assignee removed', detail: ''};
    case 'StatusChanged':
      return describeStatusChange(entry);
    // A target date the waiting-time clock moved (a pause's review date, or back to the clock's target) says so.
    case 'DetailsUpdated':
      return {
        title: entry.fromClock ? 'Target date set by the waiting-time clock' : entry.kind === 'DueDates' ? 'Target date changed' : 'Details edited',
        detail: changes.map(describeChange).join('; '),
      };
    case 'Deleted':
      return {title: 'Pathway deleted', detail: entry.fromStatus ? `Was ${words(entry.fromStatus)}` : ''};
    case 'AttachmentAdded':
      return {title: `Attached ${entry.fileName?.trim() || 'a file'}`, detail: `To ${attachedToWords(entry)}`};
    case 'AttachmentDeleted':
      return {title: `Deleted ${entry.fileName?.trim() || 'a file'}`, detail: `From ${attachedToWords(entry)}`};
    // MDT items (decision 70): the team, the meetings' times and who was present; never the question or outcome.
    case 'MdtListed':
      return {title: `Listed for ${mdtName(entry)}`, detail: entry.meetingStartUtc ? `On ${formatDateTime(entry.meetingStartUtc)}` : ''};
    case 'MdtDeferred':
      return {
        title: `Moved to a later ${entry.teamName ? `${entry.teamName} MDT` : 'MDT'}`,
        detail: sentences(
          entry.previousMeetingStartUtc ? `From ${formatDateTime(entry.previousMeetingStartUtc)}` : null,
          entry.meetingStartUtc ? `To ${formatDateTime(entry.meetingStartUtc)}` : null,
        ),
      };
    case 'MdtWithdrawn':
      return {title: `Withdrawn from ${mdtName(entry)}`, detail: ''};
    case 'MdtOutcomeRecorded':
      return {
        title: `${mdtName(entry, true)} outcome recorded`,
        detail: (entry.presentUsernames ?? []).length ? `Present: ${(entry.presentUsernames ?? []).join(', ')}` : '',
      };
    default:
      if (isResultEntry(entry)) return describeResultEntry(entry);
      if (isSpecimenEntry(entry)) return describeSpecimenEntry(entry);
      if (isConsentEntry(entry)) return describeConsentEntry(entry);
      if (isLetterEntry(entry)) return describeLetterEntry(entry);
      if (isReferralEntry(entry)) return describeReferralEntry(entry);
      // An action this version doesn't know yet: never its code name.
      return isAppointmentEntry(entry) ? describeAppointmentEntry(entry) : {title: 'Record changed', detail: ''};
  }
}

/** Whether an entry is about consent (its action is "Consent" and what happened). */
export const isConsentEntry = (entry: TimelineEntry) => entry.kind === 'Consent' || (entry.action ?? '').startsWith('Consent');

/**
 * A consent entry in words: the form and its version (admin-written), who signed and when (structured, which staff
 * see). What was said when it was withdrawn, or what was wrong, comes only to admins (`reason`). MedApp records consent;
 * nothing here says whether it was valid.
 */
function describeConsentEntry(entry: TimelineEntry): TimelineWording {
  const form = consentFormLabel(entry.consentFormName, entry.consentFormVersion) || 'a consent form';
  const signed = sentences(
    entry.consentSignedOn ? `Signed ${formatShortDate(entry.consentSignedOn)}` : null,
    entry.consentSignedBy ? `By: ${signedByLabel(entry.consentSignedBy).toLowerCase()}` : null,
  );
  const reason = entry.reason?.trim();
  switch (entry.action) {
    case 'ConsentRecorded':
      return {title: `Consent recorded on ${form}`, detail: sentences(signed, entry.attachmentId ? 'Scan attached' : 'No scan')};
    case 'ConsentWithdrawn':
      return {title: `Consent withdrawn (${form})`, detail: sentences(signed, reason)};
    case 'ConsentEnteredInError':
      return {title: `Consent record marked entered in error (${form})`, detail: sentences(signed, reason)};
    default:
      return {title: 'Consent changed', detail: signed};
  }
}

/** Whether an entry is about a specimen (its action is "Specimen" and what happened). */
export const isSpecimenEntry = (entry: TimelineEntry) => entry.kind === 'Specimens' || (entry.action ?? '').startsWith('Specimen');

const SPECIMEN_FIELDS: Record<string, string> = {
  specimenType: 'type',
  laboratory: 'lab',
  takenAtUtc: 'time taken',
  requestingUser: 'requesting clinician',
  site: 'site',
  requestNotes: 'notes for the lab',
};

/** A specimen's changed field in words, with its values where the API sends them (type, lab, time taken, requester). */
function describeSpecimenChange(change: TimelineChange): string {
  const field = change.field ?? '';
  const name = SPECIMEN_FIELDS[field] ?? field;
  if (!hasValues(change)) return `${capitalised(name)} changed`;
  if (field === 'takenAtUtc') return `Time taken ${change.from ? formatDateTime(change.from) : 'none'} → ${change.to ? formatDateTime(change.to) : 'none'}`;
  if (field === 'site' || field === 'requestNotes') return `${capitalised(name)}: ${change.to?.trim() || 'none'}`;
  return `${capitalised(name)} ${change.from ?? 'none'} → ${change.to ?? 'none'}`;
}

/** A specimen status in words, lower case: "RejectedByLab" → "rejected by the lab". */
const specimenWords = (status: string | null | undefined) => specimenStatusLabel(status).toLowerCase();

/**
 * A specimen entry in words: what happened to it, with its type and lab (admin-written names, which staff see). Staff
 * get the structured values only; the site, the notes for the lab, a rejection's reason and detail, and what a loss or
 * an entry in error said come only to admins. The number links to the specimen (`PatientTimeline`).
 */
function describeSpecimenEntry(entry: TimelineEntry): TimelineWording {
  const type = entry.specimenTypeName?.trim() || 'A';
  const lab = entry.laboratoryName?.trim();
  const was = entry.fromStatus ? `Was ${specimenWords(entry.fromStatus)}` : null;
  const reason = entry.reason?.trim();
  switch (entry.action) {
    case 'SpecimenTaken':
      return {
        title: `${type} sample taken`,
        detail: sentences(lab ? `For ${lab}` : null, ...(entry.changes ?? []).filter(hasValues).map(describeSpecimenChange)),
      };
    case 'SpecimenSent':
      return {title: `${type} sample sent${lab ? ` to ${lab}` : ''}`, detail: ''};
    case 'SpecimenReceived':
      return {title: `${type} sample received${lab ? ` by ${lab}` : ''}`, detail: ''};
    case 'SpecimenReported':
      return {title: `${type} sample reported`, detail: lab ? `The report from ${lab} is attached` : 'The report is attached'};
    case 'SpecimenRejected':
      return {title: `${type} sample rejected${lab ? ` by ${lab}` : ' by the lab'}`, detail: sentences(was, reason ? `Reason: ${reason}` : null)};
    case 'SpecimenLost':
      return {title: `${type} sample marked lost`, detail: sentences(was, reason)};
    case 'SpecimenEnteredInError':
      return {title: `${type} sample marked entered in error`, detail: sentences(was, reason)};
    case 'SpecimenDetailsEdited':
      return {title: `${type} sample: details edited`, detail: (entry.changes ?? []).map(describeSpecimenChange).join('; ')};
    default:
      return {title: `${type} sample changed`, detail: entry.toStatus ? `Now ${specimenWords(entry.toStatus)}` : ''};
  }
}

/** Whether an entry is about a result (its action is "Result" and what happened). */
export const isResultEntry = (entry: TimelineEntry) => entry.kind === 'Results' || (entry.action ?? '').startsWith('Result');

/** A result's status in words, lower case: "AwaitingSignOff" → "awaiting sign-off". */
const resultWords = (status: string | null | undefined) => resultStatusLabel(status).toLowerCase();

/**
 * A result entry in words: what happened to it, and what it came from (a specimen's type, or a report from
 * elsewhere). Staff get the structured values only (statuses, the action by name, usernames); its title, a sign-off's
 * detail and what an entry in error said come only to admins (`reason`, and the title as a `title` change). The
 * result and its specimen are linked by `PatientTimeline`. Nothing here says what a report showed.
 */
function describeResultEntry(entry: TimelineEntry): TimelineWording {
  const what = entry.resultSource === 'ExternalReport' ? 'External report' : `${entry.specimenTypeName?.trim() || 'Sample'} result`;
  const reason = entry.reason?.trim();
  const was = entry.fromStatus ? `Was ${resultWords(entry.fromStatus)}` : null;
  switch (entry.action) {
    case 'ResultRecorded': {
      const title = entry.changes?.find((c) => c.field === 'title')?.to?.trim();
      return {title: `${what} recorded, awaiting sign-off`, detail: title ? `Title: ${title}` : ''};
    }
    case 'ResultSeen':
      return {title: `${what} marked seen`, detail: ''};
    case 'ResultSignedOff':
      return {
        title: `${what} signed off`,
        detail: sentences(entry.resultAction ? resultActionLabel(entry.resultAction) : null, reason),
      };
    case 'ResultReassigned': {
      const change = entry.changes?.find((c) => c.field === 'responsibleClinician');
      return {
        title: `${what} reassigned`,
        detail: change ? `Responsible clinician ${change.from?.trim() || 'none'} → ${change.to?.trim() || 'none'}` : '',
      };
    }
    case 'ResultEnteredInError':
      return {title: `${what} marked entered in error`, detail: sentences(was, reason)};
    default:
      return {title: `${what} changed`, detail: entry.toStatus ? `Now ${resultWords(entry.toStatus)}` : ''};
  }
}

/** Whether an entry is about a letter (its action is "Letter" and what happened). */
export const isLetterEntry = (entry: TimelineEntry) => entry.kind === 'Letters' || (entry.action ?? '').startsWith('Letter');

/** Who a letter was sent to, in a sentence: "the GP practice", "the patient", "the referrer", "someone else". */
function recipientWords(kind: string | null | undefined): string {
  if (kind === 'GpPractice') return 'the GP practice';
  const words = recipientKindWords(kind);
  return words ? words.charAt(0).toLowerCase() + words.slice(1) : 'a recipient';
}

/**
 * A letter's entry in words: "Discharge summary started", "... submitted for approval" (to whom), "... returned to its
 * author", "... approved", "... sent to the GP practice" (how) or "... couldn't be sent to ..." (a secure email the
 * mail server refused), and "... withdrawn". With the template it was written from. A return comment or a withdrawal
 * reason (`reason`) comes only to admins; the letter's text never comes at all.
 */
function describeLetterEntry(entry: TimelineEntry): TimelineWording {
  const letter = letterKindWords(entry.letterKind);
  const template = entry.letterTemplateName?.trim() ? `From ${entry.letterTemplateName.trim()}` : null;
  const reason = entry.reason?.trim();
  switch (entry.action) {
    case 'LetterDrafted':
      return {title: `${letter} started`, detail: sentences(template)};
    case 'LetterSubmitted':
      return {
        title: `${letter} submitted for approval`,
        detail: sentences(entry.approverUsername ? `To ${entry.approverUsername}` : null, template),
      };
    case 'LetterReturned':
      return {title: `${letter} returned to its author`, detail: sentences(template, reason ? `Comment: ${reason}` : null)};
    case 'LetterApproved':
      return {title: `${letter} approved`, detail: sentences(template)};
    case 'LetterSent':
      return entry.dispatchStatus === 'Failed'
        ? {
            title: `${letter} couldn't be sent to ${recipientWords(entry.recipientKind)}`,
            detail: sentences(`${dispatchMethodWords(entry.dispatchMethod) || 'Secure email from MedApp'} failed`, template),
          }
        : {
            title: `${letter} sent to ${recipientWords(entry.recipientKind)}`,
            detail: sentences(dispatchMethodWords(entry.dispatchMethod), template),
          };
    case 'LetterWithdrawn':
      return {title: `${letter} withdrawn`, detail: sentences(template, reason ? `Reason: ${reason}` : null)};
    default:
      return {title: `${letter} changed`, detail: sentences(template)};
  }
}

/** Whether an entry is from a referral's history (its action is "Referral" and what happened). */
export const isReferralEntry = (entry: TimelineEntry) =>
  entry.kind === 'Referrals' || entry.kind === 'WaitingTime' || (entry.action ?? '').startsWith('Referral');

/** A referral's changed field in words, with its values where the API sends them (dates, priorities, teams). */
function describeReferralChange(change: TimelineChange): string {
  const field = change.field ?? '';
  const name = capitalised(referralFieldWords(field));
  if (!hasValues(change)) return `${name} changed`;
  switch (field) {
    case 'receivedOn':
      return `Received date ${date(change.from)} → ${date(change.to)}`;
    case 'requestedPriority':
      return `Requested priority ${words(change.from)} → ${words(change.to)}`;
    default:
      return `${name} ${change.from ?? 'none'} → ${change.to ?? 'none'}`;
  }
}

/**
 * A referral history entry in words. Staff get the structured values only (the team, the requested priority,
 * which fields were edited, the status after); the reason for referral, a withdrawal's or an entry in error's
 * detail and the referring practice come only to admins.
 */
function describeReferralEntry(entry: TimelineEntry): TimelineWording {
  const referral = entry.teamName ? `Referral to ${entry.teamName}` : 'Referral';
  const reason = entry.reason?.trim();
  const practice = entry.referrerPractice?.trim();
  switch (entry.action) {
    case 'ReferralRegistered':
      return {
        title: `${referral} registered`,
        detail: sentences(
          [practice ? `From ${practice}` : null, entry.priority ? `${words(entry.priority)} priority asked for` : null]
            .filter(Boolean)
            .join(', '),
          reason ? `Reason: ${reason}` : null,
        ),
      };
    case 'ReferralDetailsEdited':
      return {title: `${referral}: details edited`, detail: (entry.changes ?? []).map(describeReferralChange).join('; ')};
    // Triage: staff get the template, the priority chosen and the rejection reason (fixed choices); the detail
    // (`reason`) and where else it went (`redirectedTo`) are free text, so only admins get them.
    case 'ReferralAccepted':
      return {
        title: `${referral} accepted`,
        detail: [entry.templateName ? `Pathway from ${entry.templateName}` : null, entry.priority ? `${words(entry.priority)} priority` : null]
          .filter(Boolean)
          .join(', '),
      };
    case 'ReferralRejected':
      return {title: `${referral} rejected`, detail: sentences(rejectionReasonWords(entry.rejectionReason), reason)};
    case 'ReferralRedirectedElsewhere': {
      const to = entry.redirectedTo?.trim();
      return {title: `${referral} redirected elsewhere`, detail: sentences(to ? `To ${to}` : null, reason)};
    }
    case 'ReferralTeamChanged':
      return {
        title: 'Referral redirected to another team',
        detail: sentences(
          entry.teamName ? `Team ${entry.previousTeamName ?? 'unknown'} → ${entry.teamName}` : null,
          reason,
        ),
      };
    case 'ReferralWithdrawn':
      return {title: `${referral} withdrawn`, detail: reason ?? ''};
    case 'ReferralEnteredInError':
      return {title: `${referral} marked entered in error`, detail: reason ?? ''};
    case 'ReferralInformationRequested':
      return {title: `${referral}: more information asked for`, detail: 'Waiting for the referrer'};
    case 'ReferralInformationReceived':
      return {title: `${referral}: information received`, detail: 'Back to awaiting triage'};
    // The waiting-time clock: the pause reason is a fixed choice (everyone); its detail (`reason`) only to admins.
    case 'ReferralClockPaused':
      return {
        title: 'Waiting-time clock paused',
        detail: sentences(
          [pauseReasonWords(entry.pauseReason) || null, entry.reviewOn ? `review ${date(entry.reviewOn)}` : null].filter(Boolean).join(', '),
          reason,
        ),
      };
    case 'ReferralClockResumed':
      return {
        title: 'Waiting-time clock resumed',
        detail: entry.isAutomatic ? 'Resumed automatically, as the clock stopped' : '',
      };
    case 'ReferralClockStopped':
      return {
        title: 'Waiting-time clock stopped',
        detail: [
          stopReasonWords(entry.stopReason) || null,
          entry.daysWaited != null ? `day ${entry.daysWaited}${entry.targetDays != null ? ` of ${entry.targetDays}` : ''}` : null,
          entry.breached ? 'breached' : null,
        ]
          .filter(Boolean)
          .join(', '),
      };
    case 'ReferralTargetChanged':
      return {
        title: 'Waiting-time target changed',
        detail:
          entry.targetDays != null
            ? `${entry.previousTargetDays != null ? `${entry.previousTargetDays} → ` : ''}${entry.targetDays} days${entry.priority ? ` (${words(entry.priority)} priority)` : ''}`
            : '',
      };
    default:
      return {
        title: `${referral} changed`,
        detail: entry.toStatus ? `Now ${words(entry.toStatus)}` : '',
      };
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
  if (entry.fromStatus === 'Attended') return 'Was marked as attended';
  if (entry.fromStatus === 'DidNotAttend') return 'Was marked as did not attend';
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
        detail: entry.isAutomatic ? 'Added to Requires Appointment automatically, as the stage needs a visit' : 'Added to Requires Appointment',
      };
    case 'AppointmentBooked':
      return {title: `Appointment booked${about}`, detail: slot};
    case 'AppointmentRescheduled': {
      const before = appointmentSlot(entry.previousStartUtc, null, entry.previousStaffUsername);
      return {title: `Appointment rescheduled${about}`, detail: before ? `From ${before}, to ${slot}` : `To ${slot}`};
    }
    case 'AppointmentAttended':
      return {title: `Marked as attended${about}`, detail: sentences(slot, correction(entry))};
    case 'AppointmentDidNotAttend':
      return {title: `Marked as did not attend${about}`, detail: sentences(slot, correction(entry))};
    case 'AppointmentRecordedAfterEvent':
      // A walk-in, or one booked outside MedApp, recorded with its outcome; or a visit that was waiting in Requires Appointment.
      return {
        title: `Appointment recorded after the event${about}: ${entry.toStatus === 'DidNotAttend' ? 'did not attend' : 'attended'}`,
        detail: sentences(slot, entry.fromStatus === 'Requested' ? 'Was in Requires Appointment' : null),
      };
    case 'AppointmentCancelled':
      return {title: `Appointment cancelled${about}`, detail: sentences(slot, entry.isAutomatic ? 'Cancelled automatically' : null, entry.reason)};
    // A walk-in withdrawn: it didn't happen, or is on the wrong patient. Why comes only to admins, as for cancelling.
    case 'AppointmentEnteredInError':
      return {title: `Appointment marked entered in error${about}`, detail: sentences(slot, entry.reason)};
    // After a Did not attend, what happens next. Its detail (free text) comes only to admins, as the reason.
    case 'AppointmentDnaFollowUpRecorded':
      return {
        title: `Did not attend follow-up recorded${about}: ${dnaFollowUpLabel(entry.dnaFollowUp) || 'unknown'}`,
        detail: sentences(slot, entry.reason),
      };
    case 'AppointmentNotesUpdated': {
      const changes = (entry.changes ?? []).filter(hasValues).map(describeChange);
      return {title: `Appointment notes changed${about}`, detail: changes.join('; ')};
    }
    default:
      return {title: `Appointment changed${about}`, detail: slot};
  }
}

/**
 * Whether an entry is about the patient's own file, not one of their pathways: it has no pathway to link to
 * (its `pathwayId` is all zeros and its title empty).
 */
export const isPatientEntry = (entry: TimelineEntry) =>
  entry.attachedTo === 'Patient' || (entry.attachedTo !== 'Referral' && entry.pathwayId === NO_PATHWAY);

/** Whether an entry is about a file attached to one of the patient's referrals (its letter). */
export const isReferralFileEntry = (entry: TimelineEntry) => entry.attachedTo === 'Referral';

/** What a file was attached to, in words: "the patient", "the referral", "the pathway", "the Consultation stage". */
function attachedToWords(entry: TimelineEntry): string {
  if (isReferralFileEntry(entry)) return 'the referral';
  if (isPatientEntry(entry)) return 'the patient';
  if (entry.attachedTo === 'Stage') return entry.stageName ? `the ${entry.stageName} stage` : 'a stage';
  return 'the pathway';
}

const OPEN_STATUSES = new Set(['Unassigned', 'Assigned', 'InProgress', 'InReview']);

function describeStatusChange(entry: TimelineEntry): TimelineWording {
  const reason = entry.reason?.trim();
  if (entry.toStatus === 'Blocked')
    return {title: 'Put on hold', detail: [`Was ${words(entry.fromStatus)}`, reason].filter(Boolean).join('. ')};
  // Taking it off hold puts it back where it was; cancelling a pathway on hold is a status change like any other.
  if (entry.fromStatus === 'Blocked' && OPEN_STATUSES.has(entry.toStatus ?? ''))
    return {title: 'Taken off hold', detail: `Back to ${words(entry.toStatus)}`};
  const was = `Was ${words(entry.fromStatus)}${entry.isAutomatic ? ', changed automatically' : ''}`;
  return {title: `Status changed to ${words(entry.toStatus)}`, detail: [was, reason].filter(Boolean).join('. ')};
}

/** "the Dermatology MDT" (or "The Dermatology MDT" at the start of a sentence); "the MDT" without a team. */
function mdtName(entry: TimelineEntry, isStart = false): string {
  const the = isStart ? 'The' : 'the';
  return entry.teamName ? `${the} ${entry.teamName} MDT` : `${the} MDT`;
}
