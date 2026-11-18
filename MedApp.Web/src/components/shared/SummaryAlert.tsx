import {Banner} from '@astryxdesign/core/Banner';
import {Button} from '@astryxdesign/core/Button';
import {useHoverCard} from '@astryxdesign/core/HoverCard';
import {Icon} from '@astryxdesign/core/Icon';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {VisuallyHidden} from '@astryxdesign/core/VisuallyHidden';
import {useId, useRef} from 'react';
import {AppIcon} from './icons';

export interface SummaryAlertProps {
  /** A few words naming the state, e.g. "Restricted record". Also names the overlay ("Restricted record: details"). */
  label: string;
  /** The key fact, shown on screen after the label ("Restricted record: changes paused"), so it's read without opening. */
  summary?: string;
  /** The whole message, shown over the page from the label. */
  message: string;
  /** Lines under the message in the overlay (a reason, since when). */
  details?: string[];
  status?: 'error' | 'warning' | 'success' | 'info';
}

/**
 * The overlay never runs off a phone's screen, and wraps a long message to a readable width. Its surface is the
 * popover colour, as menus and popovers are, so it stands out where the page and card colours match.
 */
const CARD_STYLE = {
  maxWidth: 'min(32rem, calc(100vw - var(--spacing-8)))',
  backgroundColor: 'var(--color-background-popover)',
} as const;

/**
 * Astryx's Button is one line, and cuts a label too long for its room short with "…". This label wraps instead, so
 * its words are never cut in a narrow header: the button grows a line at a time from its usual height.
 */
export const LABEL_STYLE = {
  whiteSpace: 'normal',
  overflowWrap: 'anywhere',
  textAlign: 'start',
  height: 'auto',
  minHeight: 'var(--size-element-sm)',
} as const;

/**
 * A standing message (one that stays while a state lasts, such as a restricted record) as a short label in a status
 * banner, with the whole message over the page in an Astryx hover card. Use it in a page's `message` for a long
 * warning or note people need to know is there on every visit but don't need to reread each time; keep
 * `AlertMessage` for errors and anything they must act on now (a failed save). Put the key fact in `summary`, so it's
 * on screen without opening anything.
 *
 * The label is a button ("Restricted record: changes paused, show details"). The whole message opens on hover, when
 * it takes the keyboard's focus, and on a click or tap, and stays while the pointer moves onto it, until Escape, or
 * the pointer or focus leaves (WCAG 1.4.13). Screen readers get the whole message without opening it: it's in the
 * banner, visually hidden, and describes the button. For a warning or error, that hidden message alone is an alert
 * (announced when it appears, as `AlertMessage`'s is); the button and the overlay sit outside it, so opening the
 * overlay never announces the message again.
 */
export function SummaryAlert({label, summary, message, details = [], status = 'warning'}: SummaryAlertProps) {
  const fullId = useId();
  // Destructured: the hook's `ref` is a callback ref, and the rest are plain values (the lint rule takes the object as a ref).
  const {
    ref: anchor,
    id: cardId,
    isOpen,
    show,
    renderHoverCard,
  } = useHoverCard({placement: 'below', alignment: 'start', label: `${label}: details`, touchTrigger: 'tap'});
  // Whether the overlay was open when this press began. A tap's click opens or closes it through the hover card's own
  // tap handling; the click handler below only opens it, so it must not reopen what that tap just closed. Read at the
  // press, not from the click's pointer type, which iOS Safari reports as "mouse" for a tap.
  const wasOpenAtPress = useRef(false);
  const isUrgent = status === 'error' || status === 'warning';
  const shown = summary ? `${label}: ${summary}` : label;

  return (
    <Banner
      status={status}
      container="card"
      title={
        <>
          <Button
            ref={anchor}
            variant="ghost"
            size="sm"
            label={`${shown}, show details`}
            endContent={<Icon icon={AppIcon.ChevronDown} />}
            aria-haspopup="dialog"
            aria-expanded={isOpen}
            aria-controls={isOpen ? cardId : undefined}
            aria-describedby={fullId}
            style={LABEL_STYLE}
            onPointerDown={() => {
              wasOpenAtPress.current = isOpen;
            }}
            // Opens it again after Escape, where focus and hover don't (they haven't changed). A click never closes it:
            // a mouse that clicks has usually opened it by hovering already.
            onClick={() => {
              if (!wasOpenAtPress.current) show();
              wasOpenAtPress.current = false;
            }}>
            {shown}
          </Button>
          <VisuallyHidden id={fullId} role={isUrgent ? 'alert' : undefined}>
            {[message, ...details].join(' ')}
          </VisuallyHidden>
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
