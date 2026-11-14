import {Icon} from '@astryxdesign/core/Icon';
import {IconButton} from '@astryxdesign/core/IconButton';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import {Heading, Text} from '@astryxdesign/core/Text';
import type {ReactNode} from 'react';
import {AppIcon} from '../shared/icons';

export interface PageHeaderProps {
  title: string;
  subtitle?: ReactNode;
  /** Shows a back button that goes here. */
  backHref?: string;
  /** Shows a back button that runs this, when the target isn't a fixed address. */
  onBack?: () => void;
  backLabel?: string;
  /** Buttons at the end of the header, e.g. Search and Add. */
  actions?: ReactNode;
  /** 2 for a section's header within a page, such as each of Home's task lists. */
  level?: 1 | 2;
}

/** A page's title row (replaces Blazor's PageHeader): optional back button, title and subtitle, then actions. */
export function PageHeader({title, subtitle, backHref, onBack, backLabel = 'Back', actions, level = 1}: PageHeaderProps) {
  const hasBack = backHref !== undefined || onBack !== undefined;

  return (
    <HStack gap={4} vAlign="center">
      {hasBack && (
        <IconButton
          label={backLabel}
          tooltip={backLabel}
          icon={<Icon icon={AppIcon.Back} />}
          variant="secondary"
          href={backHref}
          onClick={onBack}
        />
      )}
      <VStack gap={1}>
        <Heading level={level}>{title}</Heading>
        {subtitle && (
          <Text type="body" color="secondary">
            {subtitle}
          </Text>
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
