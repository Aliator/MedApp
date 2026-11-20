import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useClipboard} from '@astryxdesign/core/hooks';
import {AppIcon} from '../components/shared/icons';

/** A step's sample text, as plain selectable lines, with a button that copies them all (one per line). */
export function SampleText({lines}: {lines: string[]}) {
  const {copy, isCopied} = useClipboard({announce: 'Copied'});
  return (
    <VStack gap={2}>
      <VStack gap={1}>
        {lines.map((line) => (
          <Text key={line} type="body">
            {line}
          </Text>
        ))}
      </VStack>
      <HStack>
        <Button
          label={isCopied ? 'Copied' : 'Copy the Notes'}
          variant="secondary"
          icon={<Icon icon={isCopied ? AppIcon.Check : AppIcon.Copy} />}
          onClick={() => void copy(lines.join('\n'))}
        />
      </HStack>
    </VStack>
  );
}
