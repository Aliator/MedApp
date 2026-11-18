import {createFileRoute} from '@tanstack/react-router';
import {requireAdmin} from '../../../auth/auth';
import {parseSetupSearch} from '../../../pages/listSearch';
import {SpecialtiesPage, specialtiesPageQuery} from '../../../pages/setup/SpecialtiesPage';
import {preload} from '../../../api/preload';

export const Route = createFileRoute('/_app/admin/specialties')({
  beforeLoad: requireAdmin,
  validateSearch: parseSetupSearch,
  loader: ({context}) => preload(context.queryClient, specialtiesPageQuery),
  component: function SpecialtiesRoute() {
    const navigate = Route.useNavigate();
    return <SpecialtiesPage search={Route.useSearch()} onSearchChange={(next) => void navigate({search: next})} />;
  },
});
