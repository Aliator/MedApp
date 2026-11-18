import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {formatDate} from '../../utils/dates';

export interface PathwaySummaryProps {
  title?: string | null;
  patientName?: string | null;
  templateName?: string | null;
  /** YYYY-MM-DD. */
  dueDate?: string | null;
  priority?: string | null;
}

/**
 * A pathway at a glance, e.g. inside a create, save or delete confirmation. Leaves out what isn't given.
 */
export function PathwaySummary({title, patientName, templateName, dueDate, priority}: PathwaySummaryProps) {
  return (
    <MetadataList>
      {title && <MetadataListItem label="Title">{title}</MetadataListItem>}
      {patientName && <MetadataListItem label="Patient">{patientName}</MetadataListItem>}
      {templateName && <MetadataListItem label="Template">{templateName}</MetadataListItem>}
      {dueDate && <MetadataListItem label="Target date">{formatDate(dueDate)}</MetadataListItem>}
      {priority && <MetadataListItem label="Priority">{priority}</MetadataListItem>}
    </MetadataList>
  );
}
