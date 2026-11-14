import {Card} from '@astryxdesign/core/Card';
import {Center} from '@astryxdesign/core/Center';
import {VStack} from '@astryxdesign/core/Stack';
import {Heading, Text} from '@astryxdesign/core/Text';

/** Home, as in the Blazor app: a narrow, centred welcome card for the signed-in user. */
export function HomePage() {
  return (
    <Center axis="horizontal">
      <Card padding={6} width={400}>
        <VStack gap={2}>
          <Heading level={1}>Welcome</Heading>
          <Text type="body">You are logged in.</Text>
        </VStack>
      </Card>
    </Center>
  );
}
