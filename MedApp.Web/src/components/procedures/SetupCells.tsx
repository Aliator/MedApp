import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {Token} from '@astryxdesign/core/Token';
import type {ReactNode} from 'react';

/** Whether a stage definition or template is in use (Active) or archived: kept, but no longer offered. */
export function SetupStatusToken({isArchived}: {isArchived: boolean}) {
  return isArchived ? <Token size="sm" label="Archived" color="gray" /> : <Token size="sm" label="Active" color="green" />;
}

/**
 * A setup item's name (a stage definition, template, activity type, ...), with a smaller second line (its
 * description, or its stages), and optionally a token beside the name (e.g. a stage's Visit required).
 */
export function SetupNameCell({name, secondary, token}: {name: string; secondary?: string | null; token?: ReactNode}) {
  const nameText = (
    <Text type="body" weight="semibold" maxLines={1}>
      {name}
    </Text>
  );
  return (
    <VStack gap={0}>
      {token ? (
        <HStack gap={2} vAlign="center" wrap="wrap">
          {nameText}
          {token}
        </HStack>
      ) : (
        nameText
      )}
      {secondary && (
        <Text type="supporting" color="secondary" maxLines={1}>
          {secondary}
        </Text>
      )}
    </VStack>
  );
}
