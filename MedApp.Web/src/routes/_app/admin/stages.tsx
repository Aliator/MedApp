import {createFileRoute} from '@tanstack/react-router';
import {requireAdmin} from '../../../auth/auth';
import {parseSetupSearch} from '../../../pages/listSearch';
import {StageDefinitionsPage} from '../../../pages/setup/StageDefinitionsPage';

export const Route = createFileRoute('/_app/admin/stages')({
  beforeLoad: requireAdmin,
  validateSearch: parseSetupSearch,
  component: function StageDefinitionsRoute() {
    const navigate = Route.useNavigate();
    return <StageDefinitionsPage search={Route.useSearch()} onSearchChange={(next) => void navigate({search: next})} />;
  },
});
