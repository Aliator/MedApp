import {createFileRoute} from '@tanstack/react-router';
import {requireAdmin} from '../../../../../auth/auth';
import {DayTemplateEditorPage} from '../../../../../pages/setup/DayTemplateEditorPage';
import {preload} from '../../../../../api/preload';
import {activityTypesQuery, dayTemplateQuery} from '../../../../../api/calendar';

export const Route = createFileRoute('/_app/admin/calendar-templates/day/$id')({
  beforeLoad: requireAdmin,
  loader: ({context, params}) => preload(context.queryClient, dayTemplateQuery(params.id), activityTypesQuery),
  component: function EditDayTemplateRoute() {
    return <DayTemplateEditorPage id={Route.useParams().id} />;
  },
});
