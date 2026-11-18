import {createFileRoute} from '@tanstack/react-router';
import {parseClinicsSearch} from '../../../../pages/calendar/calendarSearch';
import {ClinicsPage, preloadClinicsPage} from '../../../../pages/calendar/ClinicsPage';

/** Anyone signed in: a week's clinic sessions. Holders of ManageClinics add, change and cancel them. */
export const Route = createFileRoute('/_app/calendar/clinics/')({
  validateSearch: parseClinicsSearch,
  loaderDeps: ({search}) => ({date: search.date, site: search.site}),
  loader: ({context, deps}) => preloadClinicsPage(context.queryClient, deps),
  component: ClinicsRoute,
});

function ClinicsRoute() {
  const search = Route.useSearch();
  const navigate = Route.useNavigate();
  return <ClinicsPage search={search} onSearchChange={(next) => void navigate({search: next})} />;
}
