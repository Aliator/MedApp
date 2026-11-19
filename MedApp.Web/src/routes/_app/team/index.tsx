import {createFileRoute} from '@tanstack/react-router';
import {preload} from '../../../api/preload';
import {requirePermission, whoamiQuery} from '../../../auth/auth';
import {TeamHomePage, teamHomeQueries} from '../../../pages/team/TeamHomePage';

/** A clinic team's home (MC24), for anyone who can see patients; the side nav leads with it for clinic staff. */
export const Route = createFileRoute('/_app/team/')({
  beforeLoad: requirePermission('ViewPatients'),
  loader: async ({context}) => {
    const user = await context.queryClient.ensureQueryData(whoamiQuery);
    return preload(context.queryClient, ...teamHomeQueries(user));
  },
  component: TeamHomePage,
});
