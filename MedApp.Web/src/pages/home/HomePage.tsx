import {StackItem, VStack} from '@astryxdesign/core/Stack';
import {useCurrentUser} from '../../auth/auth';
import {DayCalendar} from '../../components/calendar/DayCalendar';
import {NeedsAttention} from '../../components/home/NeedsAttention';
import {Page} from '../../components/pages/Page';

/**
 * Home, where the MedApp heading in the side nav leads: a welcome, what needs the signed-in user's attention (counts
 * only, each linking to its page; hidden when there's nothing), and their day (decision 32), with the full calendar a
 * click away (the day's Open Calendar button). A customisable home screen is on the to-do list.
 */
export function HomePage() {
  const me = useCurrentUser();
  return (
    <Page title={`Welcome, ${me.username}`} fill>
      <VStack gap={6} height="100%">
        <NeedsAttention />
        <StackItem size="fill">
          <DayCalendar />
        </StackItem>
      </VStack>
    </Page>
  );
}
