import {createFileRoute} from '@tanstack/react-router';
import {requirePermission} from '../../../../auth/auth';
import {RoleEditorPage} from '../../../../pages/roles/RoleEditorPage';
import {preload} from '../../../../api/preload';
import {roleQuery} from '../../../../api/roles';

export const Route = createFileRoute('/_app/admin/roles/$id')({
  beforeLoad: requirePermission('ManageRoles'),
  loader: ({context, params}) => preload(context.queryClient, roleQuery(params.id)),
  component: function EditRoleRoute() {
    return <RoleEditorPage id={Route.useParams().id} />;
  },
});
