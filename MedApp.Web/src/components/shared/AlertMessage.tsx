import {Banner} from '@astryxdesign/core/Banner';
import {useHoverCard} from '@astryxdesign/core/HoverCard';
import {Icon} from '@astryxdesign/core/Icon';
import {IconButton} from '@astryxdesign/core/IconButton';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {VisuallyHidden} from '@astryxdesign/core/VisuallyHidden';
import {Fragment, useId, useRef} from 'react';
import {allMessages, type ValidationErrors} from '../../api/errors';
import {AppIcon} from './icons';

export interface AlertMessageProps {
  /**
   * The headline, on screen: two to six plain words naming what happened ("Not saved", "Stage can't be finished",
   * "Results not loaded"). It only names what `message` says; it never adds meaning of its own.
   */
  label: string;
  /** A key fact, shown after the label ("Restricted record: changes paused"), so it's read without opening anything. */
  summary?: string;
  /** The whole message (the API's words, the reason), over the page from the "i" at the end. Leave it out when the label says it all. */
  message?: string;
  /** Lines under the message in the overlay (each validation message, a reason, since when). */
  details?: string[];
  status?: 'error' | 'warning' | 'success' | 'info';
  /** Offers a Dismiss button, for a message that stays until it's read (a page body's outcome). */
  onDismiss?: () => void;
}

/**
 * The overlay never runs off a phone's screen, and wraps a long message to a readable width. Its surface is the
 * popover colour, as menus and popovers are, so it stands out where the page and card colours match.
 */
const CARD_STYLE = {
  // At least this wide, so an "i" near a phone's right edge moves the card left rather than squeezing it.
  minWidth: 'min(18rem, calc(100vw - var(--spacing-8)))',
  maxWidth: 'min(32rem, calc(100vw - var(--spacing-8)))',
  backgroundColor: 'var(--color-background-popover)',
} as const;

/**
 * Every banner is a single line, as tall as a title's line (the owner, 19 November 2026): less padding above and below
 * its headline than Astryx's Banner has. It has no size of its own, so it's a rule on its header area, for these
 * banners only (`data-medapp-alert`), given once whatever the number of banners (React puts one copy in the head).
 */
const COMPACT_CSS = '[data-medapp-alert] .astryx-banner, .astryx-banner[data-medapp-alert] { padding-block: var(--spacing-1); }';

/** Banner draws its own status icon unless given another: an error, and an info banner that ends in the "i", get an empty one. */
const NO_LEADING_ICON = <></>;

/** Words compared without case or a closing full stop, so "Patient saved." is all the label "Patient saved" says. */
const same = (a: string, b: string) => a.trim().replace(/\.$/, '').toLowerCase() === b.trim().replace(/\.$/, '').toLowerCase();

/**
 * Every banner in the app: a short headline in a status banner and, when there's more to say, an "i" at its end that
 * opens the whole message (and any details) over the page in an Astryx hover card. A page's own messages go in its
 * header (`Page`'s `message`); dialogs and cards put theirs where the user acts.
 *
 * The status shows by more than colour (WCAG 1.4.1): warnings and successes keep Banner's leading status icon; an error
 * has none (the owner, 19 November 2026): its headline says what failed ("Not saved"); an info banner with more to say
 * ends in the "i" instead, and one without keeps its icon. With nothing more than the headline, there's no "i".
 *
 * The "i" is a button ("Not saved, show details"). The whole message opens on hover, when it takes the keyboard's
 * focus, and on a click or tap, and stays while the pointer moves onto it, until Escape, or the pointer or focus
 * leaves (WCAG 1.4.13). Screen readers get the whole message without opening it: it's in the banner, visually hidden,
 * and describes the button. For a warning or error, that hidden message alone is an alert (announced when it appears);
 * the button and the overlay sit outside it, so opening the overlay never announces the message again. Without an "i",
 * the headline is the alert.
 */
export function AlertMessage({label, summary, message, details = [], status = 'error', onDismiss}: AlertMessageProps) {
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
  const compactStyle = (
    <style href="medapp-alert-banner" precedence="medapp">
      {COMPACT_CSS}
    </style>
  );
  const isUrgent = status === 'error' || status === 'warning';
  const shown = summary ? `${label}: ${summary}` : label;
  const said = message && message.trim() !== '' && !same(message, shown) && !same(message, label) ? message : undefined;
  const full = [...(said ? [said] : []), ...details];
  const headline = (
    <Text type="body" weight="semibold" color="inherit">
      {shown}
    </Text>
  );

  if (full.length === 0)
    return (
      <>
        {compactStyle}
        <Banner
          data-medapp-alert=""
          status={status}
          title={headline}
          container="card"
          icon={status === 'error' ? NO_LEADING_ICON : undefined}
          role={isUrgent ? 'alert' : undefined}
          isDismissable={onDismiss !== undefined}
          onDismiss={onDismiss}
          dismissLabel={`Dismiss ${shown}`}
        />
      </>
    );

  return (
    <>
      {compactStyle}
      <Banner
        data-medapp-alert=""
        status={status}
        container="card"
        icon={status === 'info' || status === 'error' ? NO_LEADING_ICON : undefined}
        isDismissable={onDismiss !== undefined}
        onDismiss={onDismiss}
        dismissLabel={`Dismiss ${shown}`}
        title={
          <>
            {/* The "i" right after the headline's last word; a long headline wraps beside it. */}
            <HStack gap={2} vAlign="center">
              {headline}
              <IconButton
                ref={anchor}
                variant="ghost"
                size="sm"
                label={`${shown}, show details`}
                icon={<Icon icon={AppIcon.Info} />}
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
            {/* Each line its own element, so each can be found on its own, with spaces between, so they're read apart. */}
            <VisuallyHidden id={fullId} role={isUrgent ? 'alert' : undefined}>
              {full.map((line, i) => (
                <Fragment key={`${i}-${line}`}>
                  {i > 0 && ' '}
                  <Text as="span">{line}</Text>
                </Fragment>
              ))}
            </VisuallyHidden>
            {renderHoverCard(
              <VStack gap={1}>
                {said && <Text type="body">{said}</Text>}
                {details.map((detail, i) => (
                  <Text key={`${i}-${detail}`} type="body" color={said ? 'secondary' : undefined}>
                    {detail}
                  </Text>
                ))}
              </VStack>,
              {style: CARD_STYLE},
            )}
          </>
        }
      />
    </>
  );
}

export interface ValidationAlertProps {
  errors: ValidationErrors;
  /**
   * The fields the form shows, whose messages show on the fields themselves (`fieldStatus`), so the banner leaves
   * them out. Without it, the banner's details are every message.
   */
  fields?: readonly string[];
  /** The headline: "Check the form" unless the form has better words. */
  label?: string;
}

/** A 400's errors as a banner: "Check the form", with the title and each message not already on one of `fields` behind the "i". */
export function ValidationAlert({errors, fields, label = 'Check the form'}: ValidationAlertProps) {
  const details = fields
    ? [...Object.entries(errors.fields).flatMap(([field, messages]) => (fields.includes(field) ? [] : messages)), ...errors.general]
    : allMessages(errors);
  return <AlertMessage label={label} message={errors.title} details={details} />;
}
