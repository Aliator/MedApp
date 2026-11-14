import {Icon} from '@astryxdesign/core/Icon';
import {Link} from '@astryxdesign/core/Link';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import {Heading, Text} from '@astryxdesign/core/Text';
import type {ReactNode} from 'react';
import {AppIcon} from '../shared/icons';

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
  /** 2 for a section's header within a page, such as each of Home's task lists. */
  level?: 1 | 2;
}

/**
 * A page's header (replaces Blazor's PageHeader): the title, then one line under it, with actions at the end.
 * The line holds the Back link (on pages you drill into) and/or the subtitle, and is the same height either way,
 * so the title and the content below sit in exactly the same place on every page.
 */
export function PageHeader({title, subtitle, backHref, onBack, backLabel, actions, level = 1}: PageHeaderProps) {
  const hasBack = backHref !== undefined || onBack !== undefined;

  return (
    <HStack gap={4} vAlign="center">
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
      <StackItem size="fill" />
      {actions && (
        <HStack gap={2} vAlign="center">
          {actions}
        </HStack>
      )}
    </HStack>
  );
}
