import {createFileRoute, redirect} from '@tanstack/react-router';
import {whoamiQuery} from '../../../auth/auth';
import {NotPortedPage} from '../../../pages/placeholder/NotPortedPage';

/** Admin only. Anyone else is sent home, as the Blazor Users page does. */
export const Route = createFileRoute('/_app/admin/users')({
  beforeLoad: async ({context}) => {
    const user = await context.queryClient.ensureQueryData(whoamiQuery);
    if (!user.isAdmin) throw redirect({to: '/'});
  },
  component: () => <NotPortedPage title="User Management" />,
});
