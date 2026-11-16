import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {useCurrentUser} from '../../auth/auth';
import {DayCalendar} from '../../components/calendar/DayCalendar';
import {Page} from '../../components/pages/Page';
import {AppIcon} from '../../components/shared/icons';

/**
 * Home, where the MedApp heading in the side nav leads: a welcome, and the signed-in user's day (decision 32), with
 * the full calendar a click away. A customisable home screen is on the to-do list.
 */
export function HomePage() {
  const me = useCurrentUser();
  return (
    <Page
      title={`Welcome, ${me.username}`}
      fill
      actions={<Button label="Open Calendar" variant="secondary" href="/calendar" icon={<Icon icon={AppIcon.Calendar} />} />}>
      <DayCalendar />
    </Page>
  );
}
