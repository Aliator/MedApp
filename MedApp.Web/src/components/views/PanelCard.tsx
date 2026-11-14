import {Card} from '@astryxdesign/core/Card';
import {Icon} from '@astryxdesign/core/Icon';
import {IconButton} from '@astryxdesign/core/IconButton';
import {Section} from '@astryxdesign/core/Section';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import {Heading} from '@astryxdesign/core/Text';
import type {ReactNode, Ref} from 'react';
import {AppIcon} from '../shared/icons';

export interface PanelHeaderProps {
  title: string;
  /** Controls at the end of the bar, e.g. a CollapseButton. */
  actions?: ReactNode;
  /** Lines above and below the bar; below by default (a card's top bar). */
  dividers?: ('top' | 'bottom')[];
  ref?: Ref<HTMLElement>;
  /** Makes the whole bar clickable, e.g. to fold its section (a click on a control in `actions` reaches it too). */
  onClick?: () => void;
}

/**
 * A header bar: a section title with controls at the end, on a shade a little different from the card (the same
 * as the footer) so it stands out. A PanelCard's top bar, and the same bar for further sections inside the card
 * (e.g. Stages under Details on a narrow window), so they all look alike.
 */
export function PanelHeader({title, actions, dividers = ['bottom'], ref, onClick}: PanelHeaderProps) {
  return (
    <Section
      ref={ref}
      variant="muted"
      paddingInline={6}
      paddingBlock={3}
      dividers={dividers}
      onClick={onClick}
      // A clickable bar shows it (no Section prop for the cursor).
      style={onClick ? {cursor: 'pointer'} : undefined}>
      <HStack gap={3} vAlign="center">
        <Heading level={2}>{title}</Heading>
        <StackItem size="fill" />
        {actions}
      </HStack>
    </Section>
  );
}

/**
 * The chevron that folds a section away or opens it, for a PanelHeader. `name` is lower case, e.g. "details".
 * Leave out `onToggle` when the bar itself is clickable: the button's click (from a mouse or the keyboard)
 * reaches the bar, which toggles once.
 */
export function CollapseButton({name, isOpen, onToggle}: {name: string; isOpen: boolean; onToggle?: () => void}) {
  const label = `${isOpen ? 'Hide' : 'Show'} ${name}`;
  return (
    <IconButton
      label={label}
      tooltip={label}
      icon={<Icon icon={isOpen ? AppIcon.ChevronUp : AppIcon.ChevronDown} />}
      variant="ghost"
      aria-expanded={isOpen}
      onClick={onToggle}
    />
  );
}

export interface PanelCardProps {
  /** The header bar's title, e.g. "Details" or "Stages". */
  title: string;
  /** Controls at the end of the header bar, e.g. a CollapseButton. */
  headerActions?: ReactNode;
  /** The body, which scrolls inside the card when it's taller than the space. */
  children: ReactNode;
  /** Pad the body (the default), or not, for content laid out in its own sections, such as a stage list. */
  isBodyPadded?: boolean;
  /**
   * Scroll the whole body when it's too tall (the default). Pass false when the body lays out its own scrolling
   * parts: it gets the card's remaining height (`height="100%"`) and nothing outside those parts moves.
   */
  isBodyScrollable?: boolean;
  /** Buttons in a footer pinned to the bottom, aligned to the end. */
  footerActions?: ReactNode;
}

/**
 * A card that fills the height it's given (e.g. a column of a `Page` with `fill`), with a header bar at the top,
 * a body that scrolls inside the card, and an optional footer pinned to the bottom. The page stays put; only
 * the card's body scrolls.
 */
export function PanelCard({
  title,
  headerActions,
  children,
  isBodyPadded = true,
  isBodyScrollable = true,
  footerActions,
}: PanelCardProps) {
  return (
    <Card padding={0} width="100%" height="100%">
      <VStack gap={0} height="100%">
        <PanelHeader title={title} actions={headerActions} />
        <StackItem size="fill" isScrollable={isBodyScrollable}>
          {isBodyPadded ? (
            <Section variant="transparent" padding={6}>
              <VStack gap={8}>{children}</VStack>
            </Section>
          ) : (
            children
          )}
        </StackItem>
        {footerActions && (
          <Section variant="muted" padding={4} dividers={['top']}>
            <HStack gap={2} vAlign="center" hAlign="end">
              {footerActions}
            </HStack>
          </Section>
        )}
      </VStack>
    </Card>
  );
}
