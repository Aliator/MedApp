import {Token, type TokenColor} from '@astryxdesign/core/Token';
import {labRequestPriorityLabel} from '../../api/hospitals';

const PRIORITY_COLORS: Record<string, TokenColor> = {Routine: 'default', Urgent: 'orange', Asap: 'orange', Stat: 'red'};

/**
 * A hospital request's priority, as the hospital gave it (FHIR's codes: routine, urgent, as soon as possible, stat).
 * MedApp records it; it never sets or suggests one.
 */
export function LabRequestPriorityToken({priority}: {priority: string | null | undefined}) {
  return <Token size="sm" label={labRequestPriorityLabel(priority)} color={PRIORITY_COLORS[priority ?? ''] ?? 'default'} />;
}
