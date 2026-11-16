import {createFileRoute} from '@tanstack/react-router';
import {securitySettingsQuery} from '../../../api/account';
import {preload} from '../../../api/preload';
import {requireAdmin} from '../../../auth/auth';
import {AdminSettingsPage} from '../../../pages/admin/AdminSettingsPage';

/** Admin only. Anyone else is sent home. */
export const Route = createFileRoute('/_app/admin/settings')({
  beforeLoad: requireAdmin,
  loader: ({context}) => preload(context.queryClient, securitySettingsQuery),
  component: AdminSettingsPage,
});
