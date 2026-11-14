import {createFileRoute} from '@tanstack/react-router';
import {requireAdmin} from '../../../../auth/auth';
import {parseSetupSearch} from '../../../../pages/listSearch';
import {TemplatesPage} from '../../../../pages/setup/TemplatesPage';

export const Route = createFileRoute('/_app/admin/templates/')({
  beforeLoad: requireAdmin,
  validateSearch: parseSetupSearch,
  component: function TemplatesRoute() {
    const navigate = Route.useNavigate();
    return <TemplatesPage search={Route.useSearch()} onSearchChange={(next) => void navigate({search: next})} />;
  },
});
