import {createFileRoute} from '@tanstack/react-router';
import {requireAdmin} from '../../../../../auth/auth';
import {DayTemplateEditorPage} from '../../../../../pages/setup/DayTemplateEditorPage';
import {preload} from '../../../../../api/preload';
import {activityTypesQuery} from '../../../../../api/calendar';

export const Route = createFileRoute('/_app/admin/calendar-templates/day/new')({
  beforeLoad: requireAdmin,
  loader: ({context}) => preload(context.queryClient, activityTypesQuery),
  component: function NewDayTemplateRoute() {
    return <DayTemplateEditorPage />;
  },
});
