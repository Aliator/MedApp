import {createFileRoute} from '@tanstack/react-router';
import {requireAdmin} from '../../../../auth/auth';
import {parseSetupSearch} from '../../../../pages/listSearch';
import {PathwayTemplatesPage} from '../../../../pages/setup/PathwayTemplatesPage';
import {preload} from '../../../../api/preload';
import {allTemplatesQuery} from '../../../../api/pathwaySetup';

export const Route = createFileRoute('/_app/admin/pathway-templates/')({
  beforeLoad: requireAdmin,
  validateSearch: parseSetupSearch,
  loader: ({context}) => preload(context.queryClient, allTemplatesQuery),
  component: function TemplatesRoute() {
    const navigate = Route.useNavigate();
    return <PathwayTemplatesPage search={Route.useSearch()} onSearchChange={(next) => void navigate({search: next})} />;
  },
});
