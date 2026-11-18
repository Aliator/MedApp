import {createFileRoute} from '@tanstack/react-router';
import {securitySettingsQuery} from '../../../api/account';
import {preload} from '../../../api/preload';
import {requirePermission} from '../../../auth/auth';
import {AdminSettingsPage} from '../../../pages/admin/AdminSettingsPage';

/** For holders of ManageSecuritySettings; anyone else is told the page needs it. */
export const Route = createFileRoute('/_app/admin/settings')({
  beforeLoad: requirePermission('ManageSecuritySettings'),
  loader: ({context}) => preload(context.queryClient, securitySettingsQuery),
  component: AdminSettingsPage,
});
