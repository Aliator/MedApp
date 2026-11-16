import {createFileRoute} from '@tanstack/react-router';
import {preload} from '../api/preload';
import {settingsQuery} from '../api/settings';
import {requirePasswordChange} from '../auth/auth';
import {RequiredPasswordChangePage} from '../pages/account/RequiredPasswordChangePage';

/**
 * Outside the signed-in layout, like `/login`: the layout's guard sends anyone who must change the password they
 * were given here, and this one lets only them in. The account's settings (which the API still lets them read) give
 * the page their light or dark mode and text size.
 */
export const Route = createFileRoute('/change-password')({
  beforeLoad: requirePasswordChange,
  loader: ({context}) => preload(context.queryClient, settingsQuery),
  component: RequiredPasswordChangePage,
});
