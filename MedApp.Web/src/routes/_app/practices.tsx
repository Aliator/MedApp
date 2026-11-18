import {createFileRoute} from '@tanstack/react-router';
import {preload} from '../../api/preload';
import {requirePermission} from '../../auth/auth';
import {parseSetupSearch} from '../../pages/listSearch';
import {PracticesPage, practicesPageQuery} from '../../pages/practices/PracticesPage';

/**
 * The GP practice directory, for admins (ManageSetup), in the side nav's Calendar category (the owner, 18 November
 * 2026). Staff still choose a patient's practice on the patient form, through the API, whose permissions are unchanged.
 */
export const Route = createFileRoute('/_app/practices')({
  beforeLoad: requirePermission('ManageSetup'),
  validateSearch: parseSetupSearch,
  loader: ({context}) => preload(context.queryClient, practicesPageQuery),
  component: function PracticesRoute() {
    const navigate = Route.useNavigate();
    return <PracticesPage search={Route.useSearch()} onSearchChange={(next) => void navigate({search: next})} />;
  },
});
