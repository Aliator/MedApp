import {createFileRoute} from '@tanstack/react-router';
import {preload} from '../../../../../api/preload';
import {dayTemplatesQuery, staffQuery, weekTemplateQuery} from '../../../../../api/calendar';
import {parseCalendarTemplateEditorSearch} from '../../../../../pages/setup/calendarTemplatesSearch';
import {WeekTemplateEditorPage} from '../../../../../pages/setup/WeekTemplateEditorPage';

export const Route = createFileRoute('/_app/calendar/templates/week/$id')({
  validateSearch: parseCalendarTemplateEditorSearch,
  loader: async ({context, params}) => {
    await preload(context.queryClient, weekTemplateQuery(params.id), staffQuery);
    // Then its owner's day templates, which are all it can use (on the server it waited for the template; in the
    // browser, once the template is here the page asks for them itself).
    const owner = context.queryClient.getQueryData(weekTemplateQuery(params.id).queryKey)?.ownerUserId;
    if (owner) await preload(context.queryClient, dayTemplatesQuery(owner));
  },
  component: function EditWeekTemplateRoute() {
    return <WeekTemplateEditorPage id={Route.useParams().id} staff={Route.useSearch().staff} />;
  },
});
