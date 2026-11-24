import {Avatar} from '@astryxdesign/core/Avatar';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useIsCompactRow} from './RowCards';

export interface AvatarCellProps {
  /** Shown first, and used for the avatar's initials. */
  primaryText: string;
  /** A second, smaller line, e.g. an ID. Truncated to one line. */
  secondaryText?: string;
}

/**
 * A person's avatar with one or two lines of text, for a table's first column (replaces Blazor's AvatarCell). On a
 * narrow card (a phone) the text takes the width left beside the avatar, cut with an ellipsis rather than overflowing.
 */
export function AvatarCell({primaryText, secondaryText}: AvatarCellProps) {
  const text = (
    <VStack gap={0.5}>
      <Text type="body" weight="semibold" maxLines={1}>
        {primaryText}
      </Text>
      {secondaryText && (
        <Text type="supporting" color="secondary" maxLines={1}>
          {secondaryText}
        </Text>
      )}
    </VStack>
  );
  return (
    <HStack gap={3} vAlign="center">
      <Avatar name={primaryText} size="md" tooltip={false} />
      {useIsCompactRow() ? <StackItem size="fill">{text}</StackItem> : text}
    </HStack>
  );
}
