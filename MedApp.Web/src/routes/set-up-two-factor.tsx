import {createFileRoute} from '@tanstack/react-router';
import {twoFactorStatusQuery} from '../api/account';
import {preload} from '../api/preload';
import {settingsQuery} from '../api/settings';
import {requireTwoFactorSetup} from '../auth/auth';
import {RequiredTwoFactorSetupPage} from '../pages/account/RequiredTwoFactorSetupPage';

/**
 * Outside the signed-in layout, like `/change-password`: the layout's guard sends an admin who must set up two-factor
 * authentication here, and this one lets only them in. The account's settings (which the API still lets them read)
 * give the page their light or dark mode and text size, and their two-factor state says whether they use codes by
 * email (which the requirement doesn't accept). The key is made as the page opens in the browser, not here.
 */
export const Route = createFileRoute('/set-up-two-factor')({
  beforeLoad: requireTwoFactorSetup,
  loader: ({context}) => preload(context.queryClient, settingsQuery, twoFactorStatusQuery),
  component: RequiredTwoFactorSetupPage,
});
