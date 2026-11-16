import {keepPreviousData, queryOptions} from '@tanstack/react-query';
import {formatShortDate} from '../utils/dates';
import {formatFileSize} from '../utils/fileSize';
import {NO_PROCEDURE} from './attachments';
import {api, unwrap, type Schemas} from './client';
import type {paths} from './schema';
import {PAGE_SIZE} from './search';

export type AuditSearch = NonNullable<paths['/api/procedures/audit/search']['get']['parameters']['query']>;
export type AuditEntry = Schemas['PatientProcedureAuditEntryResponse'];

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
};

/** The actions about attached files, for the log's Files look-up. */
export const ATTACHMENT_AUDIT_ACTIONS: readonly AuditAction[] = ['AttachmentAdded', 'AttachmentDeleted', 'AttachmentDownloaded'];

/** An action in words: "StageNotesUpdated" → "Stage notes saved". */
export const auditActionLabel = (action: string | null | undefined) =>
  ACTION_LABELS[action as AuditAction] ?? action ?? '';

/**
 * One page of the procedure audit log (`GET api/procedures/audit/search`, admins only), newest first unless
 * `Ascending`. While the next page loads, the current one stays on screen. Keyed under ['procedures'], so any
 * change to a procedure refreshes it.
 */
export function auditSearchQuery(search: AuditSearch) {
  const query = Object.fromEntries(
    Object.entries({PageSize: PAGE_SIZE, ...search}).filter(([, value]) => value !== undefined && value !== ''),
  ) as AuditSearch;
  return queryOptions({
    queryKey: ['procedures', 'audit', query],
    queryFn: () => unwrap(api.GET('/api/procedures/audit/search', {params: {query}})),
    placeholderData: keepPreviousData,
  });
}

type Details = Record<string, unknown>;
type FromTo = {from?: unknown; to?: unknown};

const words = (value: unknown) =>
  typeof value === 'string' ? value.replace(/(?<=[a-z])(?=[A-Z])/g, ' ').toLowerCase() : String(value ?? '');
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
 * What an audit entry's details say, in a line of words, e.g. "In progress → blocked: Awaiting results" or
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
    case 'AttachmentAdded':
    case 'AttachmentDeleted':
    case 'AttachmentDownloaded':
      return describeAttachment(d);
    default:
      return '';
  }
}

const ATTACHED_TO: Record<string, string> = {Patient: 'on the patient', Procedure: 'on the procedure', Stage: 'on the stage'};

/** A file's entry: "scan.pdf (1.2 MB), on the patient". */
function describeAttachment(d: Details): string {
  const name = typeof d.fileName === 'string' && d.fileName.trim() ? d.fileName.trim() : 'A file';
  const size = typeof d.size === 'number' ? ` (${formatFileSize(d.size)})` : '';
  const where = typeof d.attachedTo === 'string' && ATTACHED_TO[d.attachedTo] ? `, ${ATTACHED_TO[d.attachedTo]}` : '';
  return `${name}${size}${where}`;
}

/** Whether an entry is about a patient's own file, which has no procedure (its ID is all zeros, its title empty). */
export const isPatientFileEntry = (entry: Pick<AuditEntry, 'patientProcedureId'>) => entry.patientProcedureId === NO_PROCEDURE;
