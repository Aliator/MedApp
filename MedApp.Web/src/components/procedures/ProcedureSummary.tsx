import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {formatDate} from '../../utils/dates';

export interface ProcedureSummaryProps {
  title?: string | null;
  patientName?: string | null;
  templateName?: string | null;
  /** YYYY-MM-DD. */
  dueDate?: string | null;
  priority?: string | null;
}

/**
 * A procedure at a glance, e.g. inside a create, save or delete confirmation. Leaves out what isn't given.
 */
export function ProcedureSummary({title, patientName, templateName, dueDate, priority}: ProcedureSummaryProps) {
  return (
    <MetadataList>
      {title && <MetadataListItem label="Title">{title}</MetadataListItem>}
      {patientName && <MetadataListItem label="Patient">{patientName}</MetadataListItem>}
      {templateName && <MetadataListItem label="Template">{templateName}</MetadataListItem>}
      {dueDate && <MetadataListItem label="Due">{formatDate(dueDate)}</MetadataListItem>}
      {priority && <MetadataListItem label="Priority">{priority}</MetadataListItem>}
    </MetadataList>
  );
}
