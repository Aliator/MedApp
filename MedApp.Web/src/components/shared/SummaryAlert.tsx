import {AlertMessage, type AlertMessageProps} from './AlertMessage';

export type SummaryAlertProps = Omit<AlertMessageProps, 'message' | 'onDismiss'> & {
  /** The whole message, over the page from the "i" at the end. */
  message: string;
};

/**
 * A standing message (one that stays while a state lasts, such as a restricted record): an `AlertMessage` that is a
 * warning unless told otherwise, with the key fact in `summary` so it's on screen without opening anything.
 */
export function SummaryAlert({status = 'warning', ...props}: SummaryAlertProps) {
  return <AlertMessage status={status} {...props} />;
}
