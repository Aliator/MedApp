import {keepPreviousData, queryOptions} from '@tanstack/react-query';
import {formatShortDate} from '../utils/dates';
import {formatFileSize} from '../utils/fileSize';
import {statusWords} from '../utils/statusWords';
import {NO_PATHWAY} from './attachments';
import {labOutcomeLabel} from './labCases';
import {describeNoteDeletionReason} from './noteDeletion';
import {resultActionLabel, resultStatusLabel} from './results';
import {describeSkipReason, skipReasonLabel} from './stageSkips';
import {api, unwrap, type Schemas} from './client';
import type {paths} from './schema';
import {PAGE_SIZE} from './search';

export type AuditSearch = NonNullable<paths['/api/pathways/audit/search']['get']['parameters']['query']>;
export type AuditEntry = Schemas['PatientPathwayAuditEntryResponse'];

/** The audit actions, as the API names them (never renumbered, so the history stays readable). */
export const AUDIT_ACTIONS = [
  'Created',
  'DetailsUpdated',
  'StatusChanged',
  'Assigned',
  'Unassigned',
  'StageStarted',
  'StageNotesUpdated',
  'StageFinished',
  'StageUnlocked',
  'Deleted',
  'AttachmentAdded',
  'AttachmentDeleted',
  'AttachmentDownloaded',
  'NoteAdded',
  'NoteEdited',
  'NoteDeleted',
  'StageSkipped',
  'BranchChosen',
  'StageReinstated',
  'SpecimenTaken',
  'SpecimenSent',
  'SpecimenReceived',
  'SpecimenReported',
  'SpecimenRejected',
  'SpecimenLost',
  'SpecimenEnteredInError',
  'SpecimenDetailsEdited',
  'ResultRecorded',
  'ResultSeen',
  'ResultSignedOff',
  'ResultReassigned',
  'ResultEnteredInError',
  'MdtListed',
  'MdtOutcomeRecorded',
  'MdtDeferred',
  'MdtWithdrawn',
  'StageHandedOver',
  'StagePickedUp',
  'BranchAdded',
  'LabCaseOpened',
  'LabStepRecorded',
  'LabStepEnteredInError',
  'LabReportAuthorised',
  'RepeatSampleRequested',
  'LabCaseEnteredInError',
] as const;
export type AuditAction = (typeof AUDIT_ACTIONS)[number];

const ACTION_LABELS: Record<AuditAction, string> = {
  Created: 'Created',
  DetailsUpdated: 'Details edited',
  StatusChanged: 'Status changed',
  Assigned: 'Assigned',
  Unassigned: 'Unassigned',
  StageStarted: 'Stage started',
  StageNotesUpdated: 'Stage notes saved',
  StageFinished: 'Stage finished',
  StageUnlocked: 'Stage unlocked',
  Deleted: 'Deleted',
  AttachmentAdded: 'File attached',
  AttachmentDeleted: 'File deleted',
  AttachmentDownloaded: 'File downloaded',
  NoteAdded: 'Note added',
  NoteEdited: 'Note edited',
  NoteDeleted: 'Note deleted',
  StageSkipped: 'Stage skipped',
  BranchChosen: 'Branch chosen',
  StageReinstated: 'Stage reinstated',
  SpecimenTaken: 'Specimen taken',
  SpecimenSent: 'Specimen sent',
  SpecimenReceived: 'Specimen received',
  SpecimenReported: 'Specimen reported',
  SpecimenRejected: 'Specimen rejected',
  SpecimenLost: 'Specimen lost',
  SpecimenEnteredInError: 'Specimen entered in error',
  SpecimenDetailsEdited: 'Specimen details edited',
  ResultRecorded: 'Result recorded',
  ResultSeen: 'Result seen',
  ResultSignedOff: 'Result signed off',
  ResultReassigned: 'Result reassigned',
  ResultEnteredInError: 'Result entered in error',
  MdtListed: 'Listed for MDT',
  MdtOutcomeRecorded: 'MDT outcome recorded',
  MdtDeferred: 'MDT item moved',
  MdtWithdrawn: 'Withdrawn from MDT',
  StageHandedOver: 'Stage handed over',
  StagePickedUp: 'Stage picked up',
  BranchAdded: 'Branch added',
  LabCaseOpened: 'Lab case opened',
  LabStepRecorded: 'Lab step recorded',
  LabStepEnteredInError: 'Lab step entered in error',
  LabReportAuthorised: 'Lab report authorised',
  RepeatSampleRequested: 'Repeat sample requested',
  LabCaseEnteredInError: 'Lab case entered in error',
};

/** The actions about attached files, for the log's Files look-up. */
export const ATTACHMENT_AUDIT_ACTIONS: readonly AuditAction[] = ['AttachmentAdded', 'AttachmentDeleted', 'AttachmentDownloaded'];

/** An action in words: "StageNotesUpdated" → "Stage notes saved". */
export const auditActionLabel = (action: string | null | undefined) =>
  ACTION_LABELS[action as AuditAction] ?? action ?? '';

/**
 * One page of the pathway audit log (`GET api/pathways/audit/search`, admins only), newest first unless
 * `Ascending`. While the next page loads, the current one stays on screen. Keyed under ['pathways'], so any
 * change to a pathway refreshes it.
 */
export function auditSearchQuery(search: AuditSearch) {
  const query = Object.fromEntries(
    Object.entries({PageSize: PAGE_SIZE, ...search}).filter(([, value]) => value !== undefined && value !== ''),
  ) as AuditSearch;
  return queryOptions({
    queryKey: ['pathways', 'audit', query],
    queryFn: () => unwrap(api.GET('/api/pathways/audit/search', {params: {query}})),
    placeholderData: keepPreviousData,
  });
}

type Details = Record<string, unknown>;
type FromTo = {from?: unknown; to?: unknown};

const text = (value: unknown) => (typeof value === 'string' && value.trim() ? value.trim() : null);
const words = (value: unknown) => (typeof value === 'string' ? statusWords(value) : String(value ?? ''));
const quoted = (value: unknown) => (typeof value === 'string' && value.trim() ? `"${value.trim()}"` : 'nothing');
const date = (value: unknown) => (typeof value === 'string' && value ? formatShortDate(value) : 'none');

/** What each edited detail says: "Title: "A" → "B"". Notes say only that they changed, as they can be long. */
function detailChanges(changes: Record<string, FromTo>): string {
  const parts = Object.entries(changes).map(([field, change]) => {
    switch (field) {
      case 'title':
        return `Title ${quoted(change.from)} → ${quoted(change.to)}`;
      case 'dueDate':
        return `Due ${date(change.from)} → ${date(change.to)}`;
      case 'priority':
        return `Priority ${words(change.from)} → ${words(change.to)}`;
      case 'notes':
        return 'Notes changed';
      default:
        return `${field} changed`;
    }
  });
  return parts.join('; ');
}

function parse(details: string | null | undefined): Details {
  try {
    const value: unknown = JSON.parse(details ?? '{}');
    return typeof value === 'object' && value !== null ? (value as Details) : {};
  } catch {
    return {};
  }
}

/**
 * What an audit entry's details say, in a line of words, e.g. "In progress → on hold: Awaiting results" or
 * "From Diagnostic procedure, 7 stages, due 7 Nov 2026, high priority". Empty when there's nothing to add to the
 * action and stage.
 */
export function describeAuditDetails(entry: Pick<AuditEntry, 'action' | 'details'>): string {
  const d = parse(entry.details);
  switch (entry.action) {
    case 'Created':
      return [
        d.template ? `From ${String(d.template)}` : null,
        typeof d.stageCount === 'number' ? `${d.stageCount} ${d.stageCount === 1 ? 'stage' : 'stages'}` : null,
        d.dueDate ? `due ${date(d.dueDate)}` : null,
        d.priority ? `${words(d.priority)} priority` : null,
      ]
        .filter(Boolean)
        .join(', ');
    case 'DetailsUpdated':
      return detailChanges((d.changes ?? {}) as Record<string, FromTo>);
    case 'StatusChanged': {
      const change = `${words(d.from).replace(/^./, (c) => c.toUpperCase())} → ${words(d.to)}`;
      const reason = typeof d.reason === 'string' && d.reason.trim() ? `: ${d.reason.trim()}` : '';
      return `${change}${d.automatic ? ' (automatic)' : ''}${reason}`;
    }
    case 'Assigned':
    case 'Unassigned':
      return typeof d.username === 'string' ? d.username : '';
    case 'StageNotesUpdated': {
      const change = `${quoted(d.from)} → ${quoted(d.to)}`;
      return change.charAt(0).toUpperCase() + change.slice(1);
    }
    case 'Deleted':
      return d.status ? `Was ${words(d.status)}` : '';
    case 'StageFinished': {
      // Finished without an attended visit: an admin's bypass of a required one (with a reason), or a suggested one.
      if (d.withoutVisit !== true) return '';
      const requirement = typeof d.visitRequirement === 'string' && d.visitRequirement ? ` (${words(d.visitRequirement)})` : '';
      const reason = typeof d.reason === 'string' && d.reason.trim() ? `: ${d.reason.trim()}` : '';
      return `Finished without a visit${requirement}${reason}`;
    }
    case 'AttachmentAdded':
    case 'AttachmentDeleted':
    case 'AttachmentDownloaded':
      return describeAttachment(d);
    // The log names the note and its version, never its text (that's in the note's versions).
    case 'NoteAdded':
    case 'NoteEdited':
      return typeof d.version === 'number' ? `Version ${d.version}` : '';
    // The version deleted and the admin's reason; still never the note's text.
    // The reason is a choice (by name), with an optional detail: "Version 2: Wrong patient: belongs to the other pathway".
    case 'NoteDeleted': {
      const version = typeof d.version === 'number' ? `Version ${d.version}` : '';
      const reason = describeNoteDeletionReason(
        typeof d.deletionReason === 'string' ? d.deletionReason : null,
        typeof d.detail === 'string' ? d.detail : null,
      );
      return [version, reason].filter(Boolean).join(': ');
    }
    // An optional stage skipped: the reason chosen, and its detail ("Other: seen at another hospital").
    case 'StageSkipped':
      return describeSkipReason(text(d.skipReason), text(d.detail));
    // The clinician's choice at a branch point, and the other branches' stages it skipped.
    case 'BranchChosen': {
      const skipped = Array.isArray(d.skippedStages) ? d.skippedStages.filter((s): s is string => typeof s === 'string') : [];
      return [text(d.branch) ? `Chose ${text(d.branch)}` : null, skipped.length > 0 ? `skipped ${skipped.join(', ')}` : null]
        .filter(Boolean)
        .join('; ');
    }
    // The relay (MC7): which team passed the stage to which, by the teams' names.
    case 'StageHandedOver':
      return text(d.from) && text(d.to) ? `${text(d.from)} → ${text(d.to)}` : '';
    // A template's branch added on new information (MC10): which, and how many stages; the reason is on the pathway.
    case 'BranchAdded': {
      const count = Array.isArray(d.stageIds) ? d.stageIds.length : 0;
      return [text(d.branch) ? `Added ${text(d.branch)}` : null, count > 0 ? `${count} ${count === 1 ? 'stage' : 'stages'}` : null]
        .filter(Boolean)
        .join(': ');
    }
    // A skipped stage to run again: how it had been skipped, and why it's reinstated.
    case 'StageReinstated': {
      const was = text(d.skipReason) ? `Was skipped (${skipReasonLabel(text(d.skipReason)).toLowerCase()})` : '';
      return [was, text(d.reason)].filter(Boolean).join(': ');
    }
    // A lab case's entries (MC15): its number, and the step and outcome recorded; never the results or a detail typed.
    case 'LabCaseOpened':
      return [text(d.number), text(d.test)].filter(Boolean).join(', ');
    case 'LabStepRecorded':
    case 'LabStepEnteredInError':
      return [text(d.number), [text(d.step), text(d.outcome) ? labOutcomeLabel(text(d.outcome)) : null].filter(Boolean).join(' ')].filter(Boolean).join(': ');
    case 'LabReportAuthorised':
    case 'RepeatSampleRequested':
    case 'LabCaseEnteredInError':
      return text(d.number) ?? '';
    default:
      if ((entry.action ?? '').startsWith('Result')) return describeResult(d);
      return (entry.action ?? '').startsWith('Specimen') ? describeSpecimen(d) : '';
  }
}

/**
 * A specimen's entry: its number, type and lab, the status it moved between, a rejection's reason (a fixed choice) and
 * the fields an edit changed: "MA26-000123, Skin biopsy (punch), to Northgate pathology: Sent → received". The audit
 * holds no free text about a specimen (its site, notes and details typed are on the specimen itself).
 */
function describeSpecimen(d: Details): string {
  const text = (value: unknown) => (typeof value === 'string' && value.trim() ? value.trim() : null);
  const what = [text(d.number), text(d.specimenType), text(d.laboratory) ? `to ${text(d.laboratory)}` : null].filter(Boolean).join(', ');
  const status = text(d.from) && text(d.to) ? `${words(d.from).replace(/^./, (c) => c.toUpperCase())} → ${words(d.to)}` : null;
  const fields = Array.isArray(d.fields) ? d.fields.filter((f): f is string => typeof f === 'string') : [];
  const rest = [
    status,
    text(d.rejectionReason) ? `Reason: ${words(d.rejectionReason)}` : null,
    fields.length ? `Changed: ${fields.join(', ')}` : null,
  ].filter(Boolean);
  return [what, rest.join('; ')].filter(Boolean).join(': ');
}

/**
 * A result's entry: what it came from (its specimen's number and type, or a report from elsewhere), the status it moved
 * between, what was done (the action by name) and, reassigned, who it went from and to: "MA26-000123, Skin biopsy
 * (punch): Awaiting sign-off → signed off; Patient informed". The audit holds no free text about a result (its title,
 * the sign-off's detail and what an entry in error said are on the result itself).
 */
function describeResult(d: Details): string {
  const what =
    text(d.source) === 'ExternalReport' && !text(d.number) ? 'External report' : [text(d.number), text(d.specimenType)].filter(Boolean).join(', ');
  const status =
    text(d.from) && text(d.to) ? `${resultStatusLabel(text(d.from))} → ${resultStatusLabel(text(d.to)).toLowerCase()}` : null;
  // A reassignment's clinicians: `changes.responsibleClinician.{from, to}` (usernames), as ResultChange writes them.
  const changes = (d.changes ?? {}) as Record<string, {from?: unknown; to?: unknown} | undefined>;
  const responsible = changes.responsibleClinician;
  const from = text(responsible?.from);
  const to = text(responsible?.to);
  const reassigned = from || to ? `${from ?? 'nobody'} → ${to ?? 'nobody'}` : null;
  const rest = [status, text(d.action) ? resultActionLabel(text(d.action)) : null, reassigned].filter(Boolean);
  return [what, rest.join('; ')].filter(Boolean).join(': ');
}

const ATTACHED_TO: Record<string, string> = {Patient: 'on the patient', Pathway: 'on the pathway', Stage: 'on the stage'};

/** A file's entry: "scan.pdf (1.2 MB), on the patient". */
function describeAttachment(d: Details): string {
  const name = typeof d.fileName === 'string' && d.fileName.trim() ? d.fileName.trim() : 'A file';
  const size = typeof d.size === 'number' ? ` (${formatFileSize(d.size)})` : '';
  const where = typeof d.attachedTo === 'string' && ATTACHED_TO[d.attachedTo] ? `, ${ATTACHED_TO[d.attachedTo]}` : '';
  return `${name}${size}${where}`;
}

/** Whether an entry is about a patient's own file, which has no pathway (its ID is all zeros, its title empty). */
export const isPatientFileEntry = (entry: Pick<AuditEntry, 'patientPathwayId'>) => entry.patientPathwayId === NO_PATHWAY;
