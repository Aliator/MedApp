import {useCurrentUser} from '../../auth/auth';
import {Page} from '../../components/pages/Page';

/** Home, where the MedApp heading in the side nav leads: a welcome (staff know their way round from the nav). */
export function HomePage() {
  const me = useCurrentUser();
  return <Page title={`Welcome, ${me.username}`} />;
}
