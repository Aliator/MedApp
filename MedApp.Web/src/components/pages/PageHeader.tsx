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
   * A message for the whole page, such as an error from saving or loading (an `AlertMessage`). It sits in the middle
   * of the header, between the title and the actions, so it never pushes the content down.
   */
  message?: ReactNode;
  /** 2 for a section's header within a page, such as each of Home's task lists. */
  level?: 1 | 2;
}

/**
 * A page's header (replaces Blazor's PageHeader): the title, then one line under it, with actions at the end and
 * any page message in the middle. Where they don't all fit on one line, the actions wrap under the title. In a half view, it ends with the button that closes that half.
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

  return (
    // Wraps when the title and the actions don't fit side by side (a phone, a half view): the actions go under the
    // title, and wrap among themselves, rather than running off the side of the page.
    <HStack gap={4} vAlign="center" wrap="wrap">
      <VStack gap={1}>
        <Heading level={level}>{title}</Heading>
        {(hasBack || subtitle) && (
          <HStack gap={4} vAlign="center">
            {hasBack && (
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
            )}
            {subtitle && (
              <Text type="body" color="secondary">
                {subtitle}
              </Text>
            )}
          </HStack>
        )}
      </VStack>
      <StackItem size="fill">
        {message && (
          <HStack hAlign="center" vAlign="center">
            {message}
          </HStack>
        )}
      </StackItem>
      {(actions || close) && (
        <HStack gap={2} vAlign="center" wrap="wrap">
          {actions}
          {close}
        </HStack>
      )}
    </HStack>
  );
}
