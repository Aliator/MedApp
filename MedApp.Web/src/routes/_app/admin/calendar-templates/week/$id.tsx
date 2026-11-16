import {createFileRoute} from '@tanstack/react-router';
import {requireAdmin} from '../../../../../auth/auth';
import {WeekTemplateEditorPage} from '../../../../../pages/setup/WeekTemplateEditorPage';
import {preload} from '../../../../../api/preload';
import {dayTemplatesQuery, weekTemplateQuery} from '../../../../../api/calendar';

export const Route = createFileRoute('/_app/admin/calendar-templates/week/$id')({
  beforeLoad: requireAdmin,
  loader: ({context, params}) => preload(context.queryClient, weekTemplateQuery(params.id), dayTemplatesQuery),
  component: function EditWeekTemplateRoute() {
    return <WeekTemplateEditorPage id={Route.useParams().id} />;
  },
});
