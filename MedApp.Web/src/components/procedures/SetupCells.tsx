import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {Token} from '@astryxdesign/core/Token';

/** Whether a stage definition or template is in use (Active) or archived: kept, but no longer offered. */
export function SetupStatusToken({isArchived}: {isArchived: boolean}) {
  return isArchived ? <Token size="sm" label="Archived" color="gray" /> : <Token size="sm" label="Active" color="green" />;
}

/** A stage definition's or template's name, with a smaller second line (its description, or its stages). */
export function SetupNameCell({name, secondary}: {name: string; secondary?: string | null}) {
  return (
    <VStack gap={0}>
      <Text type="body" weight="semibold" maxLines={1}>
        {name}
      </Text>
      {secondary && (
        <Text type="supporting" color="secondary" maxLines={1}>
          {secondary}
        </Text>
      )}
    </VStack>
  );
}
