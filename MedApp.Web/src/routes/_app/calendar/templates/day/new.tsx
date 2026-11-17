import {createFileRoute} from '@tanstack/react-router';
import {preload} from '../../../../../api/preload';
import {activityTypesQuery, staffQuery} from '../../../../../api/calendar';
import {DayTemplateEditorPage} from '../../../../../pages/setup/DayTemplateEditorPage';
import {parseCalendarTemplateEditorSearch} from '../../../../../pages/setup/calendarTemplatesSearch';

export const Route = createFileRoute('/_app/calendar/templates/day/new')({
  validateSearch: parseCalendarTemplateEditorSearch,
  loader: ({context}) => preload(context.queryClient, activityTypesQuery, staffQuery),
  component: function NewDayTemplateRoute() {
    return <DayTemplateEditorPage staff={Route.useSearch().staff} />;
  },
});
