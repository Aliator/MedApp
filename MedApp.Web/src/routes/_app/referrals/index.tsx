import {createFileRoute} from '@tanstack/react-router';
import {preload} from '../../../api/preload';
import {listSettingsFor} from '../../../app/listSettings';
import {ReferralsListPage, referralsListQuery} from '../../../pages/referrals/ReferralsListPage';
import {parseReferralsSearch} from '../../../pages/referrals/referralsSearch';
import {clinicFor} from '../../../components/teams/ClinicPicker';

export const Route = createFileRoute('/_app/referrals/')({
  validateSearch: parseReferralsSearch,
  loaderDeps: ({search}) => search,
  loader: ({context, deps}) =>
    preload(context.queryClient, referralsListQuery(deps, listSettingsFor(context.queryClient).pageSize, clinicFor(context.queryClient, 'referrals', deps.clinic))),
  component: ReferralsRoute,
});

function ReferralsRoute() {
  const search = Route.useSearch();
  const navigate = Route.useNavigate();
  return <ReferralsListPage search={search} onSearchChange={(next) => void navigate({search: next})} />;
}
