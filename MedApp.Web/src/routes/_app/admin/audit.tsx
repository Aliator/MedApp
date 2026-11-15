import {createFileRoute} from '@tanstack/react-router';
import {requireAdmin} from '../../../auth/auth';
import {parseAuditSearch} from '../../../pages/listSearch';
import {AuditLogPage} from '../../../pages/audit/AuditLogPage';
import {preload} from '../../../api/preload';
import {listSettingsFor} from '../../../app/listSettings';
import {auditListQuery} from '../../../pages/audit/AuditLogPage';

export const Route = createFileRoute('/_app/admin/audit')({
  beforeLoad: requireAdmin,
  validateSearch: parseAuditSearch,
  loaderDeps: ({search}) => search,
  loader: ({context, deps}) => preload(context.queryClient, auditListQuery(deps, listSettingsFor(context.queryClient).pageSize)),
  component: function AuditLogRoute() {
    const navigate = Route.useNavigate();
    return <AuditLogPage search={Route.useSearch()} onSearchChange={(next) => void navigate({search: next})} />;
  },
});
