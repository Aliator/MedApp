import {createFileRoute} from '@tanstack/react-router';
import {requirePermission} from '../../../../auth/auth';
import {parseSetupSearch} from '../../../../pages/listSearch';
import {LetterTemplatesPage, letterTemplatesPageQuery} from '../../../../pages/setup/LetterTemplatesPage';
import {preload} from '../../../../api/preload';

export const Route = createFileRoute('/_app/admin/letter-templates/')({
  beforeLoad: requirePermission('ManageSetup'),
  validateSearch: parseSetupSearch,
  loader: ({context}) => preload(context.queryClient, letterTemplatesPageQuery),
  component: function LetterTemplatesRoute() {
    const navigate = Route.useNavigate();
    return <LetterTemplatesPage search={Route.useSearch()} onSearchChange={(next) => void navigate({search: next})} />;
  },
});
