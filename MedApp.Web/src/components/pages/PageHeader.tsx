import {Icon} from '@astryxdesign/core/Icon';
import {IconButton} from '@astryxdesign/core/IconButton';
import {Link} from '@astryxdesign/core/Link';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import {Heading, Text} from '@astryxdesign/core/Text';
import type {ReactNode} from 'react';
import {AppIcon} from '../shared/icons';
import {useHalfView} from './halfView';

export interface PageHeaderProps {
  title: string;
  subtitle?: ReactNode;
  /** Shows a Back link, under the title in the subtitle's place, that goes here. */
  backHref?: string;
  /** Shows a Back link that runs this, when the target isn't a fixed address. */
  onBack?: () => void;
  /** Where Back goes, in words (e.g. "Back to patients"), shown as its tooltip. */
  backLabel?: string;
  /** Buttons at the end of the header, e.g. Search and Add. */
  actions?: ReactNode;
  /**
   * A message for the whole page, such as an error from saving or loading (an `AlertMessage`), or why it's read only. It
   * sits in the title's own line, right after the title (not beside the Back link or subtitle under it: the owner,
   * 19 November 2026), with the actions at the end, so it never pushes the content down.
   */
  message?: ReactNode;
  /** 2 for a section's header within a page, such as each of Home's task lists. */
  level?: 1 | 2;
}

/**
 * A page's message, in the title's line: it starts from no width and takes what the title and the actions leave, but no
 * less than this, so a long one wraps its own words rather than taking a line of its own and pushing the actions under
 * it. Only where even this much doesn't fit (a phone, a half view) does it go under the title.
 */
const MESSAGE_MIN_INLINE = '12rem';

/**
 * The message's space: from no width (and its own length doesn't count when the header works out whether the actions
 * fit beside the title: `contain`), growing into what's left, at least `MESSAGE_MIN_INLINE`.
 */
const MESSAGE_SPACE = {flexBasis: 0, minInlineSize: MESSAGE_MIN_INLINE, contain: 'inline-size'} as const;

/**
 * A page's header (replaces Blazor's PageHeader): the title, with any page message right after it in the same line,
 * then one line under it, with actions at the end. Where they don't all fit on one line, the message and then the
 * actions wrap under the title. In a half view, it ends with the button that closes that half, which stays on the title's line, top right.
 * The line holds the Back link (on pages you drill into) and/or the subtitle, and is the same height either way,
 * so the title and the content below sit in exactly the same place on every page.
 */
export function PageHeader({title, subtitle, backHref, onBack, backLabel, actions, message, level = 1}: PageHeaderProps) {
  const hasBack = backHref !== undefined || onBack !== undefined;
  // In a half view, the page's own header closes its half.
  const {onClose} = useHalfView();
  const close = level === 1 && onClose && (
    <IconButton label="Close half view" tooltip="Close half view" icon={<Icon icon={AppIcon.Cancel} />} variant="ghost" onClick={onClose} />
  );

  const header = (
    // Wraps when the title and the actions don't fit side by side (a phone, a half view): the actions go under the
    // title, and wrap among themselves, rather than running off the side of the page.
    <HStack gap={4} vAlign="center" wrap="wrap">
      {/* Fills the line up to the actions, so they sit at the end. */}
      <StackItem size="fill">
        <VStack gap={1}>
          {message ? (
            // The message starts right after the title, in the title's line (the row's gap apart).
            <HStack gap={4} vAlign="center" wrap="wrap">
              <Heading level={level}>{title}</Heading>
              <StackItem size="fill" style={MESSAGE_SPACE}>
                <HStack hAlign="start" vAlign="center">
                  {message}
                </HStack>
              </StackItem>
            </HStack>
          ) : (
            <Heading level={level}>{title}</Heading>
          )}
          {(hasBack || subtitle) && (
            <HStack gap={4} vAlign="center">
              {hasBack && (
                <span data-page-back="">
                <Link
                  href={backHref}
                  onClick={onBack}
                  tooltip={backLabel}
                  type="body"
                  color="secondary"
                  hasUnderline={false}>
                  <HStack gap={1} vAlign="center">
                    <Icon icon={AppIcon.Back} size="sm" />
                    Back
                  </HStack>
                </Link>
                </span>
              )}
              {subtitle && (
                <Text type="body" color="secondary">
                  {subtitle}
                </Text>
              )}
            </HStack>
          )}
        </VStack>
      </StackItem>
      {actions && (
        <HStack gap={2} vAlign="center" wrap="wrap">
          {actions}
        </HStack>
      )}
    </HStack>
  );

  if (!close) return header;
  // The close button outside the part that wraps, so it never moves under the title.
  return (
    <HStack gap={2} vAlign="start">
      <StackItem size="fill">{header}</StackItem>
      {close}
    </HStack>
  );
}
