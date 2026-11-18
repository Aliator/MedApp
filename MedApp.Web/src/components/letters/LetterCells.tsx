import {Icon, type IconType} from '@astryxdesign/core/Icon';
import {Token, type TokenColor} from '@astryxdesign/core/Token';
import {dispatchStatusWords, letterStatusWords, type LetterSummary} from '../../api/letters';
import {useAccessibility} from '../../app/accessibility';
import {AppIcon} from '../shared/icons';

const STATUS: Record<string, {color: TokenColor; icon: IconType}> = {
  Draft: {color: 'gray', icon: AppIcon.Edit},
  Returned: {color: 'orange', icon: AppIcon.Warning},
  'Awaiting approval': {color: 'blue', icon: AppIcon.StatusInProgress},
  'To send': {color: 'purple', icon: AppIcon.Send},
  Approved: {color: 'green', icon: AppIcon.Success},
  Withdrawn: {color: 'red', icon: AppIcon.Withdraw},
};

/**
 * Where a letter is, in words ("Draft", "Returned", "Awaiting approval", "To send", "Approved", "Withdrawn"), with its
 * icon when "Icons on statuses" is on. Use it wherever a letter's status shows.
 */
export function LetterStatusToken({letter}: {letter: Pick<LetterSummary, 'status' | 'isReturned' | 'unsentCount'>}) {
  const {flags} = useAccessibility();
  const words = letterStatusWords(letter);
  const style = STATUS[words];
  return <Token size="sm" label={words} color={style?.color ?? 'default'} icon={flags.statusIcons && style ? <Icon icon={style.icon} /> : undefined} />;
}

const DISPATCH: Record<string, {color: TokenColor; icon: IconType}> = {
  NotSent: {color: 'gray', icon: AppIcon.Send},
  Sent: {color: 'green', icon: AppIcon.Success},
  Failed: {color: 'red', icon: AppIcon.Warning},
};

/** Whether a recipient has the letter: "Not sent", "Sent" or "Failed" (a secure email the mail server refused). */
export function DispatchStatusToken({status}: {status: string | null | undefined}) {
  const {flags} = useAccessibility();
  const style = DISPATCH[status ?? ''];
  return (
    <Token
      size="sm"
      label={dispatchStatusWords(status)}
      color={style?.color ?? 'default'}
      icon={flags.statusIcons && style ? <Icon icon={style.icon} /> : undefined}
    />
  );
}
