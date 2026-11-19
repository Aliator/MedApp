import {StackItem, VStack} from '@astryxdesign/core/Stack';
import {useQuery} from '@tanstack/react-query';
import {myDetailsQuery} from '../../api/account';
import {useCurrentUser} from '../../auth/auth';
import {DayCalendar} from '../../components/calendar/DayCalendar';
import {NeedsAttention} from '../../components/home/NeedsAttention';
import {Page} from '../../components/pages/Page';

/**
 * Home, where the MedApp heading in the side nav leads: a welcome (by full name, from the user's staff details, or
 * their username when they have none), what needs the signed-in user's attention (counts
 * only, each linking to its page; hidden when there's nothing), and their day (decision 32), with the full calendar a
 * click away (the day's Open Calendar button). A customisable home screen is on the to-do list.
 */
export function HomePage() {
  const me = useCurrentUser();
  const details = useQuery(myDetailsQuery);
  const name = details.data?.fullName?.trim() || me.username;
  return (
    <Page title={`Welcome, ${name}`} fill>
      <VStack gap={6} height="100%">
        <NeedsAttention />
        <StackItem size="fill">
          <DayCalendar />
        </StackItem>
      </VStack>
    </Page>
  );
}
