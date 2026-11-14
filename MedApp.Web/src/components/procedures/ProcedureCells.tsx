import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import {Link} from '@astryxdesign/core/Link';
import {Text} from '@astryxdesign/core/Text';
import {pixel, proportional} from '@astryxdesign/core/Table';
import {Token, type TokenColor} from '@astryxdesign/core/Token';
import {formatShortDate} from '../../utils/dates';
import {AppIcon} from '../shared/icons';
import {TableActions} from '../tables/TableActions';

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
 * The procedure's title, linked to its page. It wraps onto a second line, and only truncates beyond that: two lines
 * is as tall as the other cells (a stage and its progress, a date and Overdue), so the row doesn't grow. The patient
 * and template have columns of their own.
 */
export function ProcedureCell({id, title}: {id: string; title: string}) {
  return (
    // The Text outside the link, so the ellipsis is measured against the cell's width.
    <Text type="body" weight="semibold" maxLines={2}>
      <Link href={`/procedures/${id}`}>{title}</Link>
    </Text>
  );
}

/**
 * Column widths shared by the procedure lists, so they look alike: the text columns share the space (the title
 * most), and the short ones (tokens, dates, buttons) are fixed, wide enough for their longest value.
 */
export const PROCEDURE_COLUMN_WIDTH = {
  // Low floors, so a half-width list (Home) squeezes them and truncates rather than scrolling sideways.
  procedure: proportional(2, {minWidth: 72}),
  patient: proportional(1.5, {minWidth: 56}),
  template: proportional(1.5, {minWidth: 56}),
  stage: proportional(1.5, {minWidth: 64}),
  assigned: proportional(1.5, {minWidth: 56}),
  status: pixel(108),
  due: pixel(112),
  priority: pixel(82),
  /** Two icon buttons. */
  actions: pixel(84),
};

/** A procedure row's actions: View procedure, and View patient on lists with a patient column (`patientId`). */
export function ProcedureActions({id, patientId}: {id: string; patientId?: string}) {
  return (
    <TableActions
      actions={[
        {label: 'View procedure', icon: AppIcon.View, href: `/procedures/${id}`},
        ...(patientId ? [{label: 'View patient', icon: AppIcon.Patients, href: `/patients/${patientId}`}] : []),
      ]}
    />
  );
}

/** Who is assigned, or that nobody is, on one line (truncated, with the whole list in a tooltip). */
export function AssigneesCell({assignees}: {assignees: readonly string[]}) {
  return assignees.length > 0 ? (
    <Text type="body" maxLines={1}>
      {assignees.join(', ')}
    </Text>
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
 * The stage being worked on ("Completed" once every stage is finished), and how far through its stages the
 * procedure is ("2 of 7 stages done"). Each is one line, truncated.
 *
 * A list without a status column passes `status`, and a blocked or in-review procedure says so beside the
 * progress, since nobody can carry on with it. A list with one leaves it out.
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
  status?: string;
}) {
  const isHeld = status === 'Blocked' || status === 'InReview';
  return (
    <VStack gap={0}>
      <Text type="body" maxLines={1}>
        {currentStageName ?? 'Completed'}
      </Text>
      <HStack gap={1} vAlign="center">
        {isHeld && <StatusToken status={status} />}
        <StackItem size="fill">
          <Text type="supporting" color="secondary" maxLines={1}>
            {completed} of {total} stages done
          </Text>
        </StackItem>
      </HStack>
    </VStack>
  );
}
