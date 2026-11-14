import {VStack} from '@astryxdesign/core/Stack';
import {Link} from '@astryxdesign/core/Link';
import {Text} from '@astryxdesign/core/Text';
import {Token, type TokenColor} from '@astryxdesign/core/Token';
import {formatShortDate} from '../../utils/dates';

const PRIORITY_COLORS: Record<string, TokenColor> = {Low: 'gray', Medium: 'blue', High: 'orange', Urgent: 'red'};

const STATUS_COLORS: Record<string, TokenColor> = {
  Unassigned: 'gray',
  Assigned: 'blue',
  InProgress: 'teal',
  InReview: 'purple',
  Completed: 'green',
  Blocked: 'orange',
  Cancelled: 'gray',
  Archived: 'gray',
};

/** A status in words: "InReview" → "In review". */
export function statusLabel(status: string): string {
  const words = status.replace(/(?<=[a-z])(?=[A-Z])/g, ' ').toLowerCase();
  return words.charAt(0).toUpperCase() + words.slice(1);
}

export function PriorityToken({priority}: {priority: string}) {
  return <Token size="sm" label={priority} color={PRIORITY_COLORS[priority] ?? 'default'} />;
}

export function StatusToken({status}: {status: string}) {
  return <Token size="sm" label={statusLabel(status)} color={STATUS_COLORS[status] ?? 'default'} />;
}

/**
 * The procedure's title, linked to its page, with its patient (linked to theirs) underneath. On a list that is
 * all one patient's, pass `secondary` (e.g. the template) instead of the patient.
 */
export function ProcedureCell({
  id,
  title,
  patientId,
  patientName,
  secondary,
}: {
  id: string;
  title: string;
  patientId?: string;
  patientName?: string;
  secondary?: string;
}) {
  return (
    <VStack gap={0} hAlign="start">
      <Link href={`/procedures/${id}`}>
        <Text type="body" weight="semibold">
          {title}
        </Text>
      </Link>
      {patientId && patientName ? (
        <Link href={`/patients/${patientId}`}>{patientName}</Link>
      ) : (
        secondary && (
          <Text type="supporting" color="secondary">
            {secondary}
          </Text>
        )
      )}
    </VStack>
  );
}

/** Who is assigned, or that nobody is. */
export function AssigneesCell({assignees}: {assignees: readonly string[]}) {
  return assignees.length > 0 ? (
    <Text type="body">{assignees.join(', ')}</Text>
  ) : (
    <Text type="body" color="secondary">
      Nobody
    </Text>
  );
}

/** The due date, flagged underneath when overdue (stacked, so narrow columns don't cut the flag off). */
export function DueDateCell({dueDate, isOverdue}: {dueDate: string; isOverdue: boolean}) {
  return (
    <VStack gap={1} hAlign="start">
      <Text type="body">{formatShortDate(dueDate)}</Text>
      {isOverdue && <Token size="sm" label="Overdue" color="red" />}
    </VStack>
  );
}

/**
 * How far through its stages a procedure is ("2 of 7"), and the stage being worked on. Blocked and in-review
 * procedures say so, since nobody can carry on with the stage.
 */
export function StageProgressCell({
  completed,
  total,
  currentStageName,
  status,
}: {
  completed: number;
  total: number;
  currentStageName?: string | null;
  status: string;
}) {
  return (
    <VStack gap={1} hAlign="start">
      <VStack gap={0}>
        <Text type="body">{currentStageName ?? 'All stages finished'}</Text>
        <Text type="supporting" color="secondary">
          {completed} of {total} stages done
        </Text>
      </VStack>
      {(status === 'Blocked' || status === 'InReview') && <StatusToken status={status} />}
    </VStack>
  );
}
