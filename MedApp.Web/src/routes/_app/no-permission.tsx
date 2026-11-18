import {createFileRoute} from '@tanstack/react-router';
import {isPermission, type Permission} from '../../auth/permissions';
import {NoPermissionPage} from '../../pages/permissions/NoPermissionPage';

/** Where `requirePermission` sends someone whose roles don't allow a page, naming the permission it needs. */
export const Route = createFileRoute('/_app/no-permission')({
  validateSearch: (search: Record<string, unknown>): {permission?: Permission} =>
    isPermission(search.permission) ? {permission: search.permission} : {},
  component: function NoPermissionRoute() {
    return <NoPermissionPage permission={Route.useSearch().permission} />;
  },
});
