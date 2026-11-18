import {createFileRoute} from '@tanstack/react-router';
import {requirePermission} from '../../../auth/auth';
import {parseSetupSearch} from '../../../pages/listSearch';
import {StageDefinitionsPage} from '../../../pages/setup/StageDefinitionsPage';
import {preload} from '../../../api/preload';
import {stageDefinitionsQuery} from '../../../api/pathwaySetup';

export const Route = createFileRoute('/_app/admin/stages')({
  beforeLoad: requirePermission('ManageSetup'),
  validateSearch: parseSetupSearch,
  loader: ({context}) => preload(context.queryClient, stageDefinitionsQuery),
  component: function StageDefinitionsRoute() {
    const navigate = Route.useNavigate();
    return <StageDefinitionsPage search={Route.useSearch()} onSearchChange={(next) => void navigate({search: next})} />;
  },
});
