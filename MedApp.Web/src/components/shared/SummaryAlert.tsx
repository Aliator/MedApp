import {Banner} from '@astryxdesign/core/Banner';
import {Button} from '@astryxdesign/core/Button';
import {useHoverCard} from '@astryxdesign/core/HoverCard';
import {Icon} from '@astryxdesign/core/Icon';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {VisuallyHidden} from '@astryxdesign/core/VisuallyHidden';
import {useId, type MouseEvent} from 'react';
import {AppIcon} from './icons';

export interface SummaryAlertProps {
  /** What the banner shows: a few words naming the state, e.g. "Restricted record". */
  label: string;
  /** The whole message, shown over the page from the label. */
  message: string;
  /** Lines under the message in the overlay (a reason, since when). */
  details?: string[];
  status?: 'error' | 'warning' | 'success' | 'info';
}

/** The overlay never runs off a phone's screen, and wraps a long message to a readable width. */
const CARD_STYLE = {maxWidth: 'min(32rem, calc(100vw - var(--spacing-8)))'} as const;

/** A click from a finger or pen, which the hover card's own tap handling opens and closes. */
const isTouchClick = (event: MouseEvent) => {
  const pointerType = (event.nativeEvent as Partial<PointerEvent>).pointerType;
  return pointerType === 'touch' || pointerType === 'pen';
};

/**
 * A standing message (one that stays while a state lasts, such as a restricted record) as a short label in a status
 * banner, with the whole message over the page in an Astryx hover card. Use it in a page's `message` for a long
 * warning or note people need to know is there on every visit but don't need to reread each time; keep
 * `AlertMessage` for errors and anything they must act on now (a failed save).
 *
 * The label is a button ("Restricted record, show details"). The whole message opens on hover, when it takes the
 * keyboard's focus, and on a click or tap, and stays while the pointer moves onto it, until Escape, or the pointer or
 * focus leaves (WCAG 1.4.13). Screen readers get the whole message without opening it: it's in the banner, visually
 * hidden, and describes the button; a warning or error is an alert, read in full, as `AlertMessage` is.
 */
export function SummaryAlert({label, message, details = [], status = 'warning'}: SummaryAlertProps) {
  const fullId = useId();
  // Destructured: the hook's `ref` is a callback ref, and the rest are plain values (the lint rule takes the object as a ref).
  const {
    ref: anchor,
    id: cardId,
    isOpen,
    show,
    renderHoverCard,
  } = useHoverCard({placement: 'below', alignment: 'start', label: `${label}: details`, touchTrigger: 'tap'});
  const isUrgent = status === 'error' || status === 'warning';

  return (
    <Banner
      status={status}
      container="card"
      role={isUrgent ? 'alert' : undefined}
      title={
        <>
          <Button
            ref={anchor}
            variant="ghost"
            size="sm"
            label={`${label}, show details`}
            endContent={<Icon icon={AppIcon.ChevronDown} />}
            aria-haspopup="dialog"
            aria-expanded={isOpen}
            aria-controls={isOpen ? cardId : undefined}
            aria-describedby={fullId}
            // Opens it again after Escape, where focus and hover don't (they haven't changed).
            onClick={(event: MouseEvent) => {
              if (!isTouchClick(event)) show();
            }}>
            {label}
          </Button>
          <VisuallyHidden id={fullId}>{[message, ...details].join(' ')}</VisuallyHidden>
          {renderHoverCard(
            <VStack gap={1}>
              <Text type="body">{message}</Text>
              {details.map((detail, i) => (
                <Text key={`${i}-${detail}`} type="body" color="secondary">
                  {detail}
                </Text>
              ))}
            </VStack>,
            {style: CARD_STYLE},
          )}
        </>
      }
    />
  );
}
