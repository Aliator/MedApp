import {createFileRoute, redirect} from '@tanstack/react-router';
import {whoamiQuery} from '../auth/auth';
import {AppLayout} from '../components/navigation/AppLayout';

/** Every signed-in page sits under this layout. Its guard replaces Blazor's `Page.RequireAuth`. */
export const Route = createFileRoute('/_app')({
  beforeLoad: async ({context}) => {
    const user = await context.queryClient.ensureQueryData(whoamiQuery);
    if (!user.isAuthenticated) throw redirect({to: '/login'});
  },
  component: AppLayout,
});
