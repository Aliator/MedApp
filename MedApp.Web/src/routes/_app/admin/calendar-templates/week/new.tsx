import {createFileRoute} from '@tanstack/react-router';
import {requireAdmin} from '../../../../../auth/auth';
import {WeekTemplateEditorPage} from '../../../../../pages/setup/WeekTemplateEditorPage';
import {preload} from '../../../../../api/preload';
import {dayTemplatesQuery} from '../../../../../api/calendar';

export const Route = createFileRoute('/_app/admin/calendar-templates/week/new')({
  beforeLoad: requireAdmin,
  loader: ({context}) => preload(context.queryClient, dayTemplatesQuery),
  component: function NewWeekTemplateRoute() {
    return <WeekTemplateEditorPage />;
  },
});
