import {Avatar} from '@astryxdesign/core/Avatar';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';

export interface AvatarCellProps {
  /** Shown first, and used for the avatar's initials. */
  primaryText: string;
  /** A second, smaller line, e.g. an ID. Truncated to one line. */
  secondaryText?: string;
}

/** A person's avatar with one or two lines of text, for a table's first column (replaces Blazor's AvatarCell). */
export function AvatarCell({primaryText, secondaryText}: AvatarCellProps) {
  return (
    <HStack gap={3} vAlign="center">
      <Avatar name={primaryText} size="md" tooltip={false} />
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
    </HStack>
  );
}
