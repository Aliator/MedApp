import {createFileRoute} from '@tanstack/react-router';
import {requireAdmin} from '../../../auth/auth';
import {parseRetentionSearch} from '../../../pages/listSearch';
import {RetentionReviewPage, retentionListQuery} from '../../../pages/retention/RetentionReviewPage';
import {preload} from '../../../api/preload';
import {listSettingsFor} from '../../../app/listSettings';

export const Route = createFileRoute('/_app/admin/retention')({
  beforeLoad: requireAdmin,
  validateSearch: parseRetentionSearch,
  loaderDeps: ({search}) => search,
  loader: ({context, deps}) => preload(context.queryClient, retentionListQuery(deps, listSettingsFor(context.queryClient).pageSize)),
  component: function RetentionReviewRoute() {
    const navigate = Route.useNavigate();
    return <RetentionReviewPage search={Route.useSearch()} onSearchChange={(next) => void navigate({search: next})} />;
  },
});
