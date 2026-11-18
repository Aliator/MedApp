import {Icon, type IconType} from '@astryxdesign/core/Icon';
import {HStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {Token, type TokenColor} from '@astryxdesign/core/Token';
import {dataRequestStatusWords} from '../../api/dataRequests';
import {useAccessibility} from '../../app/accessibility';
import {formatShortDate} from '../../utils/dates';
import {AppIcon} from '../shared/icons';

const STATUS: Record<string, {color: TokenColor; icon: IconType}> = {
  Open: {color: 'blue', icon: AppIcon.StatusInProgress},
  Sent: {color: 'green', icon: AppIcon.Send},
  Refused: {color: 'red', icon: AppIcon.StatusCancelled},
  Withdrawn: {color: 'gray', icon: AppIcon.Withdraw},
};

/**
 * Where a data request stands ("Open", "Sent", "Refused" or "Withdrawn"), with its icon when "Icons on statuses" is on.
 * Use it wherever a request's status shows.
 */
export function DataRequestStatusToken({status}: {status: string | null | undefined}) {
  const {flags} = useAccessibility();
  const style = STATUS[status ?? ''];
  return (
    <Token
      size="sm"
      label={dataRequestStatusWords(status)}
      color={style?.color ?? 'default'}
      icon={flags.statusIcons && style ? <Icon icon={style.icon} /> : undefined}
    />
  );
}

/** "Overdue": an open request past its due date (UK). Words as well as colour, so it never relies on colour alone. */
export function OverdueToken() {
  const {flags} = useAccessibility();
  return <Token size="sm" label="Overdue" color="red" icon={flags.statusIcons ? <Icon icon={AppIcon.Overdue} /> : undefined} />;
}

/** When a request is due, with "Overdue" beside it once it's past. */
export function DueCell({dueOn, isOverdue}: {dueOn: string | null | undefined; isOverdue?: boolean}) {
  return (
    <HStack gap={2} vAlign="center" wrap="wrap">
      <Text type="body">{dueOn ? formatShortDate(dueOn) : '—'}</Text>
      {isOverdue && <OverdueToken />}
    </HStack>
  );
}

/** Whether a step (the identity check, the review) is done: "Yes" or "Not yet". */
export function DoneCell({isDone}: {isDone?: boolean}) {
  return (
    <Text type="body" color={isDone ? undefined : 'secondary'}>
      {isDone ? 'Yes' : 'Not yet'}
    </Text>
  );
}
