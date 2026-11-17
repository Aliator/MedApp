import {createFileRoute} from '@tanstack/react-router';
import {preload} from '../../../../../api/preload';
import {dayTemplatesQuery, staffQuery} from '../../../../../api/calendar';
import {templatesOwnerId} from '../../../../../pages/setup/calendarTemplatesOwner';
import {parseCalendarTemplateEditorSearch} from '../../../../../pages/setup/calendarTemplatesSearch';
import {WeekTemplateEditorPage} from '../../../../../pages/setup/WeekTemplateEditorPage';

export const Route = createFileRoute('/_app/calendar/templates/week/new')({
  validateSearch: parseCalendarTemplateEditorSearch,
  loaderDeps: ({search}) => ({staff: search.staff}),
  loader: async ({context, deps}) => {
    // The owner's day templates, which are all a week template can use.
    const owner = await templatesOwnerId(context.queryClient, deps.staff);
    return preload(context.queryClient, staffQuery, ...(owner ? [dayTemplatesQuery(owner)] : []));
  },
  component: function NewWeekTemplateRoute() {
    return <WeekTemplateEditorPage staff={Route.useSearch().staff} />;
  },
});
