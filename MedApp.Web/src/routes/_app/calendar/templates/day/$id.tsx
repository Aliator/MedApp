import {createFileRoute} from '@tanstack/react-router';
import {preload} from '../../../../../api/preload';
import {activityTypesQuery, dayTemplateQuery, staffQuery} from '../../../../../api/calendar';
import {DayTemplateEditorPage} from '../../../../../pages/setup/DayTemplateEditorPage';
import {parseCalendarTemplateEditorSearch} from '../../../../../pages/setup/calendarTemplatesSearch';

export const Route = createFileRoute('/_app/calendar/templates/day/$id')({
  validateSearch: parseCalendarTemplateEditorSearch,
  loader: ({context, params}) => preload(context.queryClient, dayTemplateQuery(params.id), activityTypesQuery, staffQuery),
  component: function EditDayTemplateRoute() {
    return <DayTemplateEditorPage id={Route.useParams().id} staff={Route.useSearch().staff} />;
  },
});
