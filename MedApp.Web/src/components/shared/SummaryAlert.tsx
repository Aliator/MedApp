import {Banner} from '@astryxdesign/core/Banner';
import {useHoverCard} from '@astryxdesign/core/HoverCard';
import {Icon} from '@astryxdesign/core/Icon';
import {IconButton} from '@astryxdesign/core/IconButton';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {VisuallyHidden} from '@astryxdesign/core/VisuallyHidden';
import {useId, useRef} from 'react';
import {AppIcon} from './icons';

export interface SummaryAlertProps {
  /** A few words naming the state, e.g. "Restricted record". Also names the overlay ("Restricted record: details"). */
  label: string;
  /** The key fact, shown on screen after the label ("Restricted record: changes paused"), so it's read without opening. */
  summary?: string;
  /** The whole message, shown over the page from the status icon at the end. */
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

/** The status's own icon (the one Astryx's Banner shows for it), at the end as the button that opens the message. */
const STATUS_ICON = {
  info: AppIcon.Info,
  warning: AppIcon.Warning,
  error: AppIcon.Error,
  success: AppIcon.Success,
} as const;

/**
 * The banner's leading status icon, left out: the status's icon is the button at the end instead. Banner draws its
 * own icon unless given another, so it gets an empty one.
 */
const NO_LEADING_ICON = <></>;

/**
 * A standing message (one that stays while a state lasts, such as a restricted record) as a short label in a status
 * banner, ending in the status's icon (info, warning…), with the whole message over the page in an Astryx hover card. Use it in a page's `message` for a long
 * warning or note people need to know is there on every visit but don't need to reread each time; keep
 * `AlertMessage` for errors and anything they must act on now (a failed save). Put the key fact in `summary`, so it's
 * on screen without opening anything.
 *
 * The label is plain text, with no leading icon; the icon at its end is the button ("Restricted record: changes
 * paused, show details"). The whole message opens on hover, when it takes the keyboard's focus, and on a click or tap, and stays while the pointer moves onto it, until Escape, or
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
      icon={NO_LEADING_ICON}
      title={
        <>
          {/* The icon right after the label's last word; a long label wraps beside it. */}
          <HStack gap={2} vAlign="center">
            <Text type="body" color="inherit">
              {shown}
            </Text>
            <IconButton
              ref={anchor}
              variant="ghost"
              size="sm"
              label={`${shown}, show details`}
              icon={<Icon icon={STATUS_ICON[status]} />}
              aria-haspopup="dialog"
              aria-expanded={isOpen}
              aria-controls={isOpen ? cardId : undefined}
              aria-describedby={fullId}
              onPointerDown={() => {
                wasOpenAtPress.current = isOpen;
              }}
              // Opens it again after Escape, where focus and hover don't (they haven't changed). A click never closes it:
              // a mouse that clicks has usually opened it by hovering already.
              onClick={() => {
                if (!wasOpenAtPress.current) show();
                wasOpenAtPress.current = false;
              }}
            />
          </HStack>
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
