import {createFileRoute} from '@tanstack/react-router';
import {requireSignedIn} from '../auth/auth';
import {AppLayout} from '../components/navigation/AppLayout';
import {preload} from '../api/preload';
import {settingsQuery} from '../api/settings';
import {notificationsQuery} from '../api/notifications';

/** Every signed-in page sits under this layout. Its guard replaces Blazor's `Page.RequireAuth`. */
export const Route = createFileRoute('/_app')({
  beforeLoad: async ({context}) => {
    // Signed in, and not held to changing their password (which the API would refuse the rest for).
    await requireSignedIn({context});
    // The account's settings (the page is rendered with them) and the bell's notifications.
    await preload(context.queryClient, settingsQuery, notificationsQuery);
  },
  component: AppLayout,
});
