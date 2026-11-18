import {createFileRoute} from '@tanstack/react-router';
import {requirePermission} from '../../../../auth/auth';
import {RoleEditorPage} from '../../../../pages/roles/RoleEditorPage';

export const Route = createFileRoute('/_app/admin/roles/new')({
  beforeLoad: requirePermission('ManageRoles'),
  component: RoleEditorPage,
});
